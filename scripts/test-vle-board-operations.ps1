# Test the Verilog project template board build/upload/verify operation contract.
# Default mode is Static so this can run in GitHub Actions without FPGA hardware.

[CmdletBinding()]
param(
    [string]$ProjectPath = '',

    [ValidateSet('Static', 'Build', 'Hardware', 'All')]
    [string]$Mode = 'Static',

    [string[]]$Board = @(),

    [string]$LogDirectory = '',

    [int]$TimeoutSeconds = 1800,

    [switch]$SkipMakeDryRun,

    [switch]$SkipMsBuild,

    [switch]$AllowToolSkips,

    [switch]$List
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$script:Results = New-Object System.Collections.Generic.List[object]
$script:ProjectText = ''
$script:AllProjectText = ''
$script:TargetNames = @{}

function Add-Result {
    param(
        [string]$BoardName,
        [string]$Operation,
        [string]$Status,
        [string]$Message,
        [string]$LogPath = ''
    )

    $script:Results.Add([pscustomobject]@{
        Board = $BoardName
        Operation = $Operation
        Status = $Status
        Message = $Message
        LogPath = $LogPath
    }) | Out-Null

    $prefix = ('[{0}] {1} {2}' -f $Status, $BoardName, $Operation).Trim()
    if ($Message) {
        Write-Host ($prefix + ': ' + $Message)
    }
    else {
        Write-Host $prefix
    }
}


function Add-ToolUnavailableResult {
    param(
        [string]$BoardName,
        [string]$Operation,
        [string]$Message
    )

    if ($AllowToolSkips) {
        Add-Result -BoardName $BoardName -Operation $Operation -Status 'SKIP' -Message $Message
    }
    else {
        Add-Result -BoardName $BoardName -Operation $Operation -Status 'FAIL' -Message ($Message + ' Use -AllowToolSkips to treat this as SKIP.')
    }
}

function Normalize-PathText {
    param([string]$Text)
    if ($null -eq $Text) {
        return ''
    }
    return $Text.Replace('\', '/')
}

function Test-TextContains {
    param(
        [string]$Haystack,
        [string]$Needle
    )

    $h = Normalize-PathText $Haystack
    $n = Normalize-PathText $Needle
    return ($h.IndexOf($n, [System.StringComparison]::OrdinalIgnoreCase) -ge 0)
}

function Test-TextMentionsOutput {
    param(
        [string]$Haystack,
        [string]$ExpectedOutput
    )

    $normalized = Normalize-PathText $ExpectedOutput
    if (Test-TextContains -Haystack $Haystack -Needle $normalized) {
        return $true
    }

    $lastSlash = $normalized.LastIndexOf('/')
    if ($lastSlash -lt 0) {
        return (Test-TextContains -Haystack $Haystack -Needle $normalized)
    }

    $dir = $normalized.Substring(0, $lastSlash)
    $leaf = $normalized.Substring($lastSlash + 1)

    if ((Test-TextContains -Haystack $Haystack -Needle $dir) -and (Test-TextContains -Haystack $Haystack -Needle $leaf)) {
        return $true
    }
    return $false
}


function Expand-MakefileText {
    param([string]$Text)

    if ($null -eq $Text) {
        return ''
    }

    $variables = @{}

    # Split the Makefile text into lines. This regex matches either LF line
    # endings or CRLF line endings by making a carriage return before the
    # newline optional.
    $MakefileLineEndingPattern = '\r?\n'
    $lines = $Text -split $MakefileLineEndingPattern

    # Match a simple Makefile variable assignment.
    # Supported forms are:
    #   NAME = value
    #   NAME := value
    #   NAME ?= value
    # Capture group 1 is the variable name. It must start with a letter or
    # underscore and may then contain letters, digits, or underscores.
    # The assignment operator is matched but not captured.
    # Capture group 2 is the assigned value, with leading/trailing whitespace
    # outside the value ignored.
    # This intentionally does not implement a full Makefile parser; it does not
    # handle +=, define/endef blocks, multiline continuations, export/override
    # prefixes, conditional syntax, inline comments, or target-specific variables.
    $MakeVariableAssignmentPattern = '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*(?::=|\?=|=)\s*(.*?)\s*$'

    foreach ($line in $lines) {
        $trimmed = $line.Trim()
        if ($trimmed -eq '' -or $trimmed.StartsWith('#')) {
            continue
        }

        if ($line -match $MakeVariableAssignmentPattern) {
            $name = $matches[1]
            $value = $matches[2].Trim()
            $variables[$name] = $value
        }
    }

    # Match a Makefile-style variable reference inside an already-parsed value.
    # Supported forms are:
    #   $(NAME)
    #   ${NAME}
    # The leading '$' and the surrounding parentheses/braces are matched.
    # Capture group 1 contains the variable name from the $(NAME) form.
    # Capture group 2 contains the variable name from the ${NAME} form.
    # Replacement code should use group 1 when it is present, otherwise group 2.
    # This intentionally handles only simple variable references. It does not
    # parse nested references, Make functions such as $(shell ...), pattern
    # substitutions, automatic variables such as $@ or $<, or escaped dollar
    # signs such as $$.
    $MakeVariableReferencePattern = '\$\(([^)]+)\)|\$\{([^}]+)\}'

    function Expand-OneMakeValue {
        param(
            [string]$Value,
            [hashtable]$Variables
        )

        $expanded = $Value

        # Expand variable references repeatedly so chained assignments resolve,
        # for example BUILD_DIR -> BOARD_DIR -> BIT_FILE. The hard limit
        # prevents an infinite loop if variables reference each other.
        for ($i = 0; $i -lt 20; $i++) {
            $prior = $expanded
            $expanded = [regex]::Replace($expanded, $MakeVariableReferencePattern, {
                param($match)
                $key = $match.Groups[1].Value
                if ([string]::IsNullOrEmpty($key)) {
                    $key = $match.Groups[2].Value
                }

                if ($Variables.ContainsKey($key)) {
                    return [string]$Variables[$key]
                }
                return $match.Value
            })

            if ($expanded -eq $prior) {
                break
            }
        }

        return $expanded
    }

    $names = @($variables.Keys)
    foreach ($name in $names) {
        $variables[$name] = Expand-OneMakeValue -Value ([string]$variables[$name]) -Variables $variables
    }

    $expandedText = $Text

    # Expand variable references in the full Makefile text so later static
    # checks can find generated paths after simple Makefile variables resolve.
    # The hard limit prevents an infinite loop if variables reference each other.
    for ($i = 0; $i -lt 20; $i++) {
        $prior = $expandedText
        $expandedText = [regex]::Replace($expandedText, $MakeVariableReferencePattern, {
            param($match)
            $key = $match.Groups[1].Value
            if ([string]::IsNullOrEmpty($key)) {
                $key = $match.Groups[2].Value
            }

            if ($variables.ContainsKey($key)) {
                return [string]$variables[$key]
            }
            return $match.Value
        })

        if ($expandedText -eq $prior) {
            break
        }
    }

    $variableValues = ($variables.GetEnumerator() | ForEach-Object { $_.Value }) -join "`n"
    return ($Text + "`n" + $expandedText + "`n" + $variableValues)
}

function New-BoardSpec {
    param(
        [string]$Name,
        [string]$Platform,
        [string]$Makefile,
        [string]$MakeTarget,
        [string]$ExpectedOutput,
        [string]$BuildTarget,
        [string]$DebugTarget,
        [string]$ReleaseTarget,
        [string]$UploadTarget,
        [string]$VerifyTarget,
        [string]$CleanTarget,
        [string[]]$RequiredFiles,
        [string[]]$ForbiddenRootArtifacts
    )

    return [pscustomobject]@{
        Name = $Name
        Platform = $Platform
        Makefile = $Makefile
        MakeTarget = $MakeTarget
        ExpectedOutput = $ExpectedOutput
        BuildTarget = $BuildTarget
        DebugTarget = $DebugTarget
        ReleaseTarget = $ReleaseTarget
        UploadTarget = $UploadTarget
        VerifyTarget = $VerifyTarget
        CleanTarget = $CleanTarget
        RequiredFiles = $RequiredFiles
        ForbiddenRootArtifacts = $ForbiddenRootArtifacts
    }
}

function Get-BoardSpecs {
    $specs = @(
        (New-BoardSpec `
            -Name 'ULX3S 12K' `
            -Platform 'ULX3S ECP5-12K' `
            -Makefile 'boards/ulx3s/Makefile-ULX3S-12F.mk' `
            -MakeTarget 'build/ulx3s-12k/ulx3s.bit' `
            -ExpectedOutput 'build/ulx3s-12k/ulx3s.bit' `
            -BuildTarget 'Build_ULX3S' `
            -DebugTarget 'Debug_ULX3S_12K' `
            -ReleaseTarget 'Release_ULX3S_12K' `
            -UploadTarget 'Upload_ULX3S_12K' `
            -VerifyTarget 'Verify_ULX3S_12K' `
            -CleanTarget 'Clean_ULX3S' `
            -RequiredFiles @('top.v', 'boards/ulx3s/ulx3s_v20.lpf', 'boards/ulx3s/Makefile-ULX3S-12F.mk') `
            -ForbiddenRootArtifacts @('top.json', 'ulx3s_out.config', 'ulx3s.bit')),

        (New-BoardSpec `
            -Name 'ULX3S 25K' `
            -Platform 'ULX3S ECP5-25K' `
            -Makefile 'boards/ulx3s/Makefile-ULX3S-25F.mk' `
            -MakeTarget 'build/ulx3s-25k/ulx3s.bit' `
            -ExpectedOutput 'build/ulx3s-25k/ulx3s.bit' `
            -BuildTarget 'Build_ULX3S' `
            -DebugTarget 'Debug_ULX3S_25K' `
            -ReleaseTarget 'Release_ULX3S_25K' `
            -UploadTarget 'Upload_ULX3S_25K' `
            -VerifyTarget 'Verify_ULX3S_25K' `
            -CleanTarget 'Clean_ULX3S' `
            -RequiredFiles @('top.v', 'boards/ulx3s/ulx3s_v20.lpf', 'boards/ulx3s/Makefile-ULX3S-25F.mk') `
            -ForbiddenRootArtifacts @('top.json', 'ulx3s_out.config', 'ulx3s.bit')),

        (New-BoardSpec `
            -Name 'ULX3S 45K' `
            -Platform 'ULX3S ECP5-45K' `
            -Makefile 'boards/ulx3s/Makefile-ULX3S-45F.mk' `
            -MakeTarget 'build/ulx3s-45k/ulx3s.bit' `
            -ExpectedOutput 'build/ulx3s-45k/ulx3s.bit' `
            -BuildTarget 'Build_ULX3S' `
            -DebugTarget 'Debug_ULX3S_45K' `
            -ReleaseTarget 'Release_ULX3S_45K' `
            -UploadTarget 'Upload_ULX3S_45K' `
            -VerifyTarget 'Verify_ULX3S_45K' `
            -CleanTarget 'Clean_ULX3S' `
            -RequiredFiles @('top.v', 'boards/ulx3s/ulx3s_v20.lpf', 'boards/ulx3s/Makefile-ULX3S-45F.mk') `
            -ForbiddenRootArtifacts @('top.json', 'ulx3s_out.config', 'ulx3s.bit')),

        (New-BoardSpec `
            -Name 'ULX3S 85K' `
            -Platform 'ULX3S ECP5-85K' `
            -Makefile 'boards/ulx3s/Makefile-ULX3S-85F.mk' `
            -MakeTarget 'build/ulx3s-85k/ulx3s.bit' `
            -ExpectedOutput 'build/ulx3s-85k/ulx3s.bit' `
            -BuildTarget 'Build_ULX3S' `
            -DebugTarget 'Debug_ULX3S_85K' `
            -ReleaseTarget 'Release_ULX3S_85K' `
            -UploadTarget 'Upload_ULX3S_85K' `
            -VerifyTarget 'Verify_ULX3S_85K' `
            -CleanTarget 'Clean_ULX3S' `
            -RequiredFiles @('top.v', 'boards/ulx3s/ulx3s_v20.lpf', 'boards/ulx3s/Makefile-ULX3S-85F.mk') `
            -ForbiddenRootArtifacts @('top.json', 'ulx3s_out.config', 'ulx3s.bit')),

        (New-BoardSpec `
            -Name 'ULX4M-LS 12K' `
            -Platform 'ULX4M-LS ECP5-12K' `
            -Makefile 'boards/ulx4m/Makefile-ULX4M-LS-12F.mk' `
            -MakeTarget 'build/ulx4m-ls-12k/ulx4m.bit' `
            -ExpectedOutput 'build/ulx4m-ls-12k/ulx4m.bit' `
            -BuildTarget 'Build_ULX4M' `
            -DebugTarget 'Debug_ULX4M_LS_12K' `
            -ReleaseTarget 'Release_ULX4M_LS_12K' `
            -UploadTarget 'Upload_ULX4M_LS_12K' `
            -VerifyTarget 'Verify_ULX4M_LS_12K' `
            -CleanTarget 'Clean_ULX4M' `
            -RequiredFiles @('boards/ulx4m/top_ulx4m.v', 'boards/ulx4m/ulx4m_minimal.lpf', 'boards/ulx4m/Makefile-ULX4M-LS-12F.mk') `
            -ForbiddenRootArtifacts @('ulx4m.json', 'ulx4m_out.config', 'ulx4m.bit')),

        (New-BoardSpec `
            -Name 'ULX4M-LD 85K' `
            -Platform 'ULX4M-LD ECP5-85K' `
            -Makefile 'boards/ulx4m/Makefile-ULX4M-LD-85F.mk' `
            -MakeTarget 'build/ulx4m-ld-85k/ulx4m.bit' `
            -ExpectedOutput 'build/ulx4m-ld-85k/ulx4m.bit' `
            -BuildTarget 'Build_ULX4M' `
            -DebugTarget 'Debug_ULX4M_LD_85K' `
            -ReleaseTarget 'Release_ULX4M_LD_85K' `
            -UploadTarget 'Upload_ULX4M_LD_85K' `
            -VerifyTarget 'Verify_ULX4M_LD_85K' `
            -CleanTarget 'Clean_ULX4M' `
            -RequiredFiles @('boards/ulx4m/top_ulx4m.v', 'boards/ulx4m/ulx4m_minimal.lpf', 'boards/ulx4m/Makefile-ULX4M-LD-85F.mk') `
            -ForbiddenRootArtifacts @('ulx4m.json', 'ulx4m_out.config', 'ulx4m.bit')),

        (New-BoardSpec `
            -Name 'iCEBreaker' `
            -Platform 'iCEBreaker' `
            -Makefile 'boards/icebreaker/main.mk' `
            -MakeTarget 'build/icebreaker/top_icebreaker.bin' `
            -ExpectedOutput 'build/icebreaker/top_icebreaker.bin' `
            -BuildTarget 'Build_iCEBreaker' `
            -DebugTarget 'Debug_iCEBreaker' `
            -ReleaseTarget 'Release_iCEBreaker' `
            -UploadTarget 'Upload_iCEBreaker' `
            -VerifyTarget 'Verify_iCEBreaker' `
            -CleanTarget 'Clean_iCEBreaker' `
            -RequiredFiles @('top_icebreaker.v', 'boards/icebreaker/icebreaker.pcf', 'boards/icebreaker/main.mk') `
            -ForbiddenRootArtifacts @('top_icebreaker.json', 'top_icebreaker.asc', 'top_icebreaker.bin', 'top_icebreaker.rpt', 'top_icebreaker.yslog', 'top_icebreaker.nplog')),

        (New-BoardSpec `
            -Name 'Orange Crab' `
            -Platform 'Orange Crab' `
            -Makefile 'boards/orangecrab/Makefile' `
            -MakeTarget 'build/orangecrab/blink.dfu' `
            -ExpectedOutput 'build/orangecrab/blink.dfu' `
            -BuildTarget 'Build_Orange_Crab' `
            -DebugTarget 'Debug_Orange_Crab' `
            -ReleaseTarget 'Release_Orange_Crab' `
            -UploadTarget 'Upload_Orange_Crab' `
            -VerifyTarget 'Verify_Orange_Crab' `
            -CleanTarget 'Clean_Orange_Crab' `
            -RequiredFiles @('boards/orangecrab/blink.v', 'boards/orangecrab/orangecrab_r0.1.pcf', 'boards/orangecrab/orangecrab_r0.2.pcf', 'boards/orangecrab/Makefile') `
            -ForbiddenRootArtifacts @('blink.json', 'blink_out.config', 'blink.bit', 'blink.dfu')),

        (New-BoardSpec `
            -Name 'tinyFPGA BX' `
            -Platform 'tinyFPGA BX' `
            -Makefile 'boards/tinyfpga_bx/Makefile' `
            -MakeTarget 'build/tinyfpga-bx/TinyFPGA_B.bin' `
            -ExpectedOutput 'build/tinyfpga-bx/TinyFPGA_B.bin' `
            -BuildTarget 'Build_TinyFPGA_BX' `
            -DebugTarget 'Debug_TinyFPGA_BX' `
            -ReleaseTarget 'Release_TinyFPGA_BX' `
            -UploadTarget 'Upload_TinyFPGA_BX' `
            -VerifyTarget 'Verify_TinyFPGA_BX' `
            -CleanTarget 'Clean_TinyFPGA_BX' `
            -RequiredFiles @('boards/tinyfpga_bx/TinyFPGA_B.v', 'boards/tinyfpga_bx/pins.pcf', 'boards/tinyfpga_bx/Makefile') `
            -ForbiddenRootArtifacts @('TinyFPGA_B.blif', 'TinyFPGA_B.json', 'TinyFPGA_B.asc', 'TinyFPGA_B.bin', 'TinyFPGA_B.rpt', 'TinyFPGA_B.yslog', 'TinyFPGA_B.nplog'))
    )

    return $specs
}

function Resolve-ProjectFile {
    param([string]$PathText)

    $candidates = New-Object System.Collections.Generic.List[string]

    if ($PathText) {
        if (Test-Path -LiteralPath $PathText -PathType Leaf) {
            return (Resolve-Path -LiteralPath $PathText).Path
        }
        if (Test-Path -LiteralPath $PathText -PathType Container) {
            $dir = (Resolve-Path -LiteralPath $PathText).Path
            $preferred = Join-Path $dir 'VerilogProject.csproj'
            if (Test-Path -LiteralPath $preferred) {
                return (Resolve-Path -LiteralPath $preferred).Path
            }
            $found = Get-ChildItem -LiteralPath $dir -Filter '*.csproj' -File | Where-Object { $_.Name -notlike '*Template*' } | Select-Object -First 1
            if ($found) {
                return $found.FullName
            }
        }
        throw "ProjectPath not found: $PathText"
    }

    $cwd = (Get-Location).Path
    $scriptRoot = $PSScriptRoot

    $candidates.Add((Join-Path $cwd 'AddedExtensionProjectTemplates/VerilogProject/VerilogProject.csproj')) | Out-Null
    $candidates.Add((Join-Path $cwd 'VerilogProject.csproj')) | Out-Null
    if ($scriptRoot) {
        $candidates.Add((Join-Path $scriptRoot '../AddedExtensionProjectTemplates/VerilogProject/VerilogProject.csproj')) | Out-Null
        $candidates.Add((Join-Path $scriptRoot '../VerilogProject.csproj')) | Out-Null
    }

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw 'Unable to find VerilogProject.csproj. Pass -ProjectPath explicitly.'
}

function Expand-ProjectPathText {
    param(
        [string]$PathText,
        [string]$ProjectFile,
        [string]$ImportingFile
    )

    $projectDir = Split-Path -Parent $ProjectFile
    $importingDir = Split-Path -Parent $ImportingFile
    $expanded = $PathText

    $expanded = $expanded.Replace('$(MSBuildThisFileDirectory)', $importingDir + [System.IO.Path]::DirectorySeparatorChar)
    $expanded = $expanded.Replace('$(MSBuildProjectDirectory)', $projectDir)
    $expanded = $expanded.Replace('$(MSBuildProjectFullPath)', $ProjectFile)

    if ($expanded.StartsWith('.\')) {
        $expanded = $expanded.Substring(2)
    }
    if ($expanded.StartsWith('./')) {
        $expanded = $expanded.Substring(2)
    }

    if ([System.IO.Path]::IsPathRooted($expanded)) {
        return $expanded
    }

    return (Join-Path $importingDir $expanded)
}

function Add-ImportedProjectFile {
    param(
        [string]$File,
        [string]$ProjectFile,
        [System.Collections.Generic.List[string]]$Files,
        [hashtable]$Seen
    )

    if (-not (Test-Path -LiteralPath $File -PathType Leaf)) {
        return
    }

    $resolved = (Resolve-Path -LiteralPath $File).Path
    $key = $resolved.ToLowerInvariant()
    if ($Seen.ContainsKey($key)) {
        return
    }

    $Seen[$key] = $true
    $Files.Add($resolved) | Out-Null

    [xml]$xml = Get-Content -LiteralPath $resolved -Raw
    $imports = Select-Xml -Xml $xml -XPath '//*[local-name()="Import"]'

    # Match any unresolved MSBuild-style property reference in an import path.
    # Supported form is $(NAME). The whole property reference is matched; no
    # capture groups are used. This is used only to decide whether the path
    # still contains properties after the small known-property substitution below.
    $MsBuildPropertyReferencePattern = '\$\([^)]*\)'

    foreach ($import in $imports) {
        $importPath = $import.Node.Project
        if (-not $importPath) {
            continue
        }

        if ($importPath -match $MsBuildPropertyReferencePattern) {
            # Only expand the few MSBuild properties this script understands.
            # Other property-based imports are ignored so Static mode remains safe.
            $knownImportPath = $importPath
            $knownImportPath = $knownImportPath.Replace('$(MSBuildThisFileDirectory)', '')
            $knownImportPath = $knownImportPath.Replace('$(MSBuildProjectDirectory)', '')
            $knownImportPath = $knownImportPath.Replace('$(MSBuildProjectFullPath)', '')
            if ($knownImportPath -match $MsBuildPropertyReferencePattern) {
                continue
            }
        }

        $fullPath = Expand-ProjectPathText -PathText $importPath -ProjectFile $ProjectFile -ImportingFile $resolved
        Add-ImportedProjectFile -File $fullPath -ProjectFile $ProjectFile -Files $Files -Seen $Seen
    }
}

function Get-ImportedProjectFiles {
    param([string]$ProjectFile)

    $files = New-Object System.Collections.Generic.List[string]
    $seen = @{}
    Add-ImportedProjectFile -File $ProjectFile -ProjectFile $ProjectFile -Files $files -Seen $seen
    return $files.ToArray()
}

function Load-ProjectData {
    param([string]$ProjectFile)

    $script:ProjectText = Get-Content -LiteralPath $ProjectFile -Raw
    $allText = New-Object System.Text.StringBuilder
    $targetMap = @{}

    $projectFiles = Get-ImportedProjectFiles -ProjectFile $ProjectFile
    foreach ($file in $projectFiles) {
        $text = Get-Content -LiteralPath $file -Raw
        [void]$allText.AppendLine($text)

        [xml]$xml = $text
        $targets = Select-Xml -Xml $xml -XPath '//*[local-name()="Target"]'
        foreach ($target in $targets) {
            $name = $target.Node.Name
            if ($name -and -not $targetMap.ContainsKey($name)) {
                $targetMap[$name] = $file
            }
        }
    }

    $script:AllProjectText = $allText.ToString()
    $script:TargetNames = $targetMap
}

function Find-MSBuild {
    if ($env:MSBUILD_EXE -and (Test-Path -LiteralPath $env:MSBUILD_EXE)) {
        return (Resolve-Path -LiteralPath $env:MSBUILD_EXE).Path
    }

    $cmd = Get-Command 'msbuild.exe' -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    $vswherePaths = @()
    if (${env:ProgramFiles(x86)}) {
        $vswherePaths += (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe')
    }
    if ($env:ProgramFiles) {
        $vswherePaths += (Join-Path $env:ProgramFiles 'Microsoft Visual Studio/Installer/vswhere.exe')
    }

    foreach ($vswhere in $vswherePaths) {
        if (Test-Path -LiteralPath $vswhere) {
            $found = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' 2>$null | Select-Object -First 1
            if ($found -and (Test-Path -LiteralPath $found)) {
                return $found
            }
        }
    }

    return ''
}

function Find-WSL {
    if ($env:OS -ne 'Windows_NT') {
        return ''
    }

    $windir = $env:windir
    if (-not $windir) {
        return ''
    }

    $candidates = @(
        (Join-Path $windir 'system32/wsl.exe'),
        (Join-Path $windir 'Sysnative/wsl.exe')
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    return ''
}

function ConvertTo-WslPath {
    param(
        [string]$WslExe,
        [string]$WindowsPath
    )

    $converted = & $WslExe wslpath -a $WindowsPath 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $converted) {
        throw "wslpath failed for $WindowsPath"
    }
    return $converted.Trim()
}

function ConvertTo-SafeLogName {
    param([string]$Text)

    # Match any character that is not safe for the generated diagnostic log
    # file names used by this script. Allowed characters are ASCII letters,
    # digits, underscore, period, and hyphen. Each unsafe character is replaced
    # with an underscore. No capture groups are used.
    $UnsafeLogNameCharacterPattern = '[^A-Za-z0-9_.-]'

    return ($Text -replace $UnsafeLogNameCharacterPattern, '_')
}

function Quote-Shell {
    param([string]$Text)

    # Match a literal single quote inside text that will be wrapped in single
    # quotes for a POSIX shell command. Each matched quote is replaced with the
    # standard close-quote, escaped-quote, reopen-quote sequence. No capture
    # groups are used.
    $ShellSingleQuotePattern = "'"

    return "'" + ($Text -replace $ShellSingleQuotePattern, "'\''") + "'"
}

function Join-CommandLine {
    param([string[]]$Arguments)

    # Match any whitespace character or a double quote. Arguments matching
    # this regex need double-quote wrapping for the displayed command line.
    # No capture groups are used.
    $CommandLineNeedsQuotingPattern = '[\s"]'

    # Match each literal double quote inside an argument so it can be
    # backslash-escaped within the displayed double-quoted argument.
    # No capture groups are used.
    $CommandLineDoubleQuotePattern = '"'

    $quoted = foreach ($arg in $Arguments) {
        if ($null -eq $arg) {
            '""'
        }
        elseif ($arg -match $CommandLineNeedsQuotingPattern) {
            '"' + ($arg -replace $CommandLineDoubleQuotePattern, '\"') + '"'
        }
        else {
            $arg
        }
    }

    return ($quoted -join ' ')
}


function Stop-ProcessTree {
    param([System.Diagnostics.Process]$Process)

    if ($null -eq $Process -or $Process.HasExited) {
        return
    }

    if ($env:OS -eq 'Windows_NT') {
        $taskkill = Join-Path $env:windir 'system32/taskkill.exe'
        if (Test-Path -LiteralPath $taskkill) {
            & $taskkill /PID $Process.Id /T /F 1>$null 2>$null
            return
        }
    }

    try {
        $Process.Kill()
    }
    catch {
    }
}

function Invoke-ExternalCommand {
    param(
        [string]$BoardName,
        [string]$Operation,
        [string]$FileName,
        [string[]]$Arguments,
        [string]$WorkingDirectory,
        [string]$LogFile,
        [int]$Timeout
    )

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $FileName
    $psi.Arguments = Join-CommandLine -Arguments $Arguments
    $psi.WorkingDirectory = $WorkingDirectory
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi

    $stdoutFile = $LogFile + '.out.txt'
    $stderrFile = $LogFile + '.err.txt'
    $combinedFile = $LogFile

    $stdoutBuilder = New-Object System.Text.StringBuilder
    $stderrBuilder = New-Object System.Text.StringBuilder

    [void]$process.Start()
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()

    $finished = $process.WaitForExit($Timeout * 1000)
    if (-not $finished) {
        Stop-ProcessTree -Process $process
        [void]$process.WaitForExit(5000)

        $stdout = ''
        $stderr = ''
        if ($stdoutTask.Wait(5000)) {
            $stdout = $stdoutTask.Result
        }
        if ($stderrTask.Wait(5000)) {
            $stderr = $stderrTask.Result
        }

        $combined = @()
        $combined += ('Command: {0} {1}' -f $FileName, $psi.Arguments)
        $combined += ('WorkingDirectory: {0}' -f $WorkingDirectory)
        $combined += ('ExitCode: TIMEOUT')
        $combined += ('TimeoutSeconds: {0}' -f $Timeout)
        $combined += ''
        $combined += 'STDOUT before timeout:'
        $combined += $stdout
        $combined += ''
        $combined += 'STDERR before timeout:'
        $combined += $stderr
        Set-Content -LiteralPath $combinedFile -Value $combined -Encoding UTF8

        Add-Result -BoardName $BoardName -Operation $Operation -Status 'FAIL' -Message "Timed out after $Timeout seconds" -LogPath $combinedFile
        return $false
    }

    $stdoutTask.Wait()
    $stderrTask.Wait()
    $stdout = $stdoutTask.Result
    $stderr = $stderrTask.Result

    Set-Content -LiteralPath $stdoutFile -Value $stdout -Encoding UTF8
    Set-Content -LiteralPath $stderrFile -Value $stderr -Encoding UTF8

    $combined = @()
    $combined += ('Command: {0} {1}' -f $FileName, $psi.Arguments)
    $combined += ('WorkingDirectory: {0}' -f $WorkingDirectory)
    $combined += ('ExitCode: {0}' -f $process.ExitCode)
    $combined += ''
    $combined += 'STDOUT:'
    $combined += $stdout
    $combined += ''
    $combined += 'STDERR:'
    $combined += $stderr
    Set-Content -LiteralPath $combinedFile -Value $combined -Encoding UTF8

    if ($process.ExitCode -eq 0) {
        Add-Result -BoardName $BoardName -Operation $Operation -Status 'PASS' -Message 'Command completed' -LogPath $combinedFile
        return $true
    }

    Add-Result -BoardName $BoardName -Operation $Operation -Status 'FAIL' -Message "Command exited with code $($process.ExitCode)" -LogPath $combinedFile
    return $false
}

function Test-StaticContract {
    param(
        [object]$Spec,
        [string]$ProjectDir
    )

    $requiredTargets = @(
        $Spec.BuildTarget,
        $Spec.DebugTarget,
        $Spec.ReleaseTarget,
        $Spec.UploadTarget,
        $Spec.VerifyTarget,
        $Spec.CleanTarget
    )

    foreach ($target in $requiredTargets) {
        if ($script:TargetNames.ContainsKey($target)) {
            Add-Result -BoardName $Spec.Name -Operation "target $target" -Status 'PASS' -Message 'Target exists'
        }
        else {
            Add-Result -BoardName $Spec.Name -Operation "target $target" -Status 'FAIL' -Message 'Target is missing'
        }
    }

    if (Test-TextContains -Haystack $script:AllProjectText -Needle $Spec.Platform) {
        Add-Result -BoardName $Spec.Name -Operation 'platform' -Status 'PASS' -Message $Spec.Platform
    }
    else {
        Add-Result -BoardName $Spec.Name -Operation 'platform' -Status 'FAIL' -Message "Missing platform text: $($Spec.Platform)"
    }

    if (Test-TextContains -Haystack $script:AllProjectText -Needle $Spec.Makefile) {
        Add-Result -BoardName $Spec.Name -Operation 'makefile property' -Status 'PASS' -Message $Spec.Makefile
    }
    else {
        Add-Result -BoardName $Spec.Name -Operation 'makefile property' -Status 'FAIL' -Message "Project does not reference $($Spec.Makefile)"
    }

    if (Test-TextMentionsOutput -Haystack $script:AllProjectText -ExpectedOutput $Spec.ExpectedOutput) {
        Add-Result -BoardName $Spec.Name -Operation 'bitstream path' -Status 'PASS' -Message $Spec.ExpectedOutput
    }
    else {
        Add-Result -BoardName $Spec.Name -Operation 'bitstream path' -Status 'FAIL' -Message "Project should reference $($Spec.ExpectedOutput)"
    }

    foreach ($file in $Spec.RequiredFiles) {
        $path = Join-Path $ProjectDir $file
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            Add-Result -BoardName $Spec.Name -Operation "file $file" -Status 'PASS' -Message 'File exists'
        }
        else {
            Add-Result -BoardName $Spec.Name -Operation "file $file" -Status 'FAIL' -Message 'File is missing'
        }
    }

    $makefilePath = Join-Path $ProjectDir $Spec.Makefile
    if (Test-Path -LiteralPath $makefilePath -PathType Leaf) {
        $makeText = Get-Content -LiteralPath $makefilePath -Raw
        $expandedMakeText = Expand-MakefileText -Text $makeText
        if (Test-TextMentionsOutput -Haystack $expandedMakeText -ExpectedOutput $Spec.MakeTarget) {
            Add-Result -BoardName $Spec.Name -Operation 'make output target' -Status 'PASS' -Message $Spec.MakeTarget
        }
        else {
            Add-Result -BoardName $Spec.Name -Operation 'make output target' -Status 'FAIL' -Message "Makefile should build $($Spec.MakeTarget)"
        }
    }

    foreach ($artifact in $Spec.ForbiddenRootArtifacts) {
        $rootArtifact = Join-Path $ProjectDir $artifact
        if (Test-Path -LiteralPath $rootArtifact) {
            Add-Result -BoardName $Spec.Name -Operation "root artifact $artifact" -Status 'FAIL' -Message 'Generated artifact exists in project root'
        }
        else {
            Add-Result -BoardName $Spec.Name -Operation "root artifact $artifact" -Status 'PASS' -Message 'Not present in project root'
        }
    }
}


function Test-CompatibilityProjectTemplate {
    param(
        [object]$Spec,
        [string]$ProjectDir
    )

    $templateProjectPath = Join-Path $ProjectDir 'ProjectTemplate.csproj'
    if (-not (Test-Path -LiteralPath $templateProjectPath -PathType Leaf)) {
        return
    }

    $templateProjectText = Get-Content -LiteralPath $templateProjectPath -Raw

    if (Test-TextContains -Haystack $templateProjectText -Needle $Spec.Platform) {
        Add-Result -BoardName $Spec.Name -Operation 'ProjectTemplate platform' -Status 'PASS' -Message $Spec.Platform
    }
    else {
        Add-Result -BoardName $Spec.Name -Operation 'ProjectTemplate platform' -Status 'FAIL' -Message "ProjectTemplate.csproj is missing platform text: $($Spec.Platform)"
    }

    if (Test-TextContains -Haystack $templateProjectText -Needle $Spec.Makefile) {
        Add-Result -BoardName $Spec.Name -Operation 'ProjectTemplate makefile' -Status 'PASS' -Message $Spec.Makefile
    }
    else {
        Add-Result -BoardName $Spec.Name -Operation 'ProjectTemplate makefile' -Status 'FAIL' -Message "ProjectTemplate.csproj does not reference $($Spec.Makefile)"
    }

    if (Test-TextMentionsOutput -Haystack $templateProjectText -ExpectedOutput $Spec.ExpectedOutput) {
        Add-Result -BoardName $Spec.Name -Operation 'ProjectTemplate bitstream path' -Status 'PASS' -Message $Spec.ExpectedOutput
    }
    else {
        Add-Result -BoardName $Spec.Name -Operation 'ProjectTemplate bitstream path' -Status 'FAIL' -Message "ProjectTemplate.csproj should reference $($Spec.ExpectedOutput)"
    }
}

function Invoke-MakeDryRun {
    param(
        [object]$Spec,
        [string]$ProjectDir,
        [string]$LogDir
    )

    $safeName = ConvertTo-SafeLogName $Spec.Name
    $logFile = Join-Path $LogDir ("make-dryrun-$safeName.log")

    if ($env:OS -eq 'Windows_NT') {
        $wsl = Find-WSL
        if (-not $wsl) {
            Add-ToolUnavailableResult -BoardName $Spec.Name -Operation 'make dry-run' -Message 'wsl.exe not found'
            return
        }

        try {
            $wslProject = ConvertTo-WslPath -WslExe $wsl -WindowsPath $ProjectDir
        }
        catch {
            Add-Result -BoardName $Spec.Name -Operation 'make dry-run' -Status 'FAIL' -Message $_.Exception.Message
            return
        }

        $command = 'cd ' + (Quote-Shell $wslProject) + ' && make -n ' + (Quote-Shell $Spec.MakeTarget) + ' -f ' + (Quote-Shell $Spec.Makefile) + ' && make -n clean -f ' + (Quote-Shell $Spec.Makefile)
        Invoke-ExternalCommand -BoardName $Spec.Name -Operation 'make dry-run' -FileName $wsl -Arguments @('bash', '-lc', $command) -WorkingDirectory $ProjectDir -LogFile $logFile -Timeout 120 | Out-Null
    }
    else {
        $make = Get-Command 'make' -ErrorAction SilentlyContinue
        if (-not $make) {
            Add-ToolUnavailableResult -BoardName $Spec.Name -Operation 'make dry-run' -Message 'make not found'
            return
        }

        Invoke-ExternalCommand -BoardName $Spec.Name -Operation 'make dry-run' -FileName $make.Source -Arguments @('-n', $Spec.MakeTarget, '-f', $Spec.Makefile) -WorkingDirectory $ProjectDir -LogFile $logFile -Timeout 120 | Out-Null
        Invoke-ExternalCommand -BoardName $Spec.Name -Operation 'make clean dry-run' -FileName $make.Source -Arguments @('-n', 'clean', '-f', $Spec.Makefile) -WorkingDirectory $ProjectDir -LogFile (Join-Path $LogDir ("make-dryrun-$safeName-clean.log")) -Timeout 120 | Out-Null
    }
}

function Invoke-MSBuildOperation {
    param(
        [object]$Spec,
        [string]$ProjectFile,
        [string]$ProjectDir,
        [string]$LogDir,
        [string]$Configuration,
        [string]$OperationName,
        [string]$Target = 'Build',
        [int]$Timeout
    )

    $msbuild = Find-MSBuild
    if (-not $msbuild) {
        Add-ToolUnavailableResult -BoardName $Spec.Name -Operation $OperationName -Message 'MSBuild not found'
        return
    }

    $safeName = ConvertTo-SafeLogName $Spec.Name
    $safeConfig = ConvertTo-SafeLogName $Configuration
    $safeOperation = ConvertTo-SafeLogName $OperationName
    $logFile = Join-Path $LogDir ("msbuild-$safeName-$safeConfig-$safeOperation.log")

    $args = @(
        $ProjectFile,
        '/restore',
        ("/t:$Target"),
        ("/p:Configuration=$Configuration"),
        ("/p:Platform=$($Spec.Platform)"),
        '/m:1',
        '/v:minimal',
        '/nologo'
    )

    Invoke-ExternalCommand -BoardName $Spec.Name -Operation $OperationName -FileName $msbuild -Arguments $args -WorkingDirectory $ProjectDir -LogFile $logFile -Timeout $Timeout | Out-Null
}

function Assert-NoRootArtifactsAfterBuild {
    param(
        [object]$Spec,
        [string]$ProjectDir
    )

    foreach ($artifact in $Spec.ForbiddenRootArtifacts) {
        $rootArtifact = Join-Path $ProjectDir $artifact
        if (Test-Path -LiteralPath $rootArtifact) {
            Add-Result -BoardName $Spec.Name -Operation "post-build root artifact $artifact" -Status 'FAIL' -Message 'Generated artifact was created in project root'
        }
        else {
            Add-Result -BoardName $Spec.Name -Operation "post-build root artifact $artifact" -Status 'PASS' -Message 'Root stayed clean'
        }
    }
}

$specs = Get-BoardSpecs
if ($Board.Count -gt 0) {
    $selected = @()
    foreach ($name in $Board) {
        $match = $specs | Where-Object { $_.Name -ieq $name -or $_.Platform -ieq $name }
        if (-not $match) {
            throw "Unknown board: $name"
        }
        $selected += $match
    }
    $specs = $selected
}

if ($List) {
    $specs | Select-Object Name, Platform, Makefile, MakeTarget, ExpectedOutput | Format-Table -AutoSize
    exit 0
}

$projectFile = Resolve-ProjectFile -PathText $ProjectPath
$projectDir = Split-Path -Parent $projectFile

if (-not $LogDirectory) {
    $LogDirectory = Join-Path $projectDir 'build/vle-board-operation-check'
}
New-Item -ItemType Directory -Force -Path $LogDirectory | Out-Null

Load-ProjectData -ProjectFile $projectFile

Write-Host "Project: $projectFile"
Write-Host "Mode: $Mode"
Write-Host "LogDirectory: $LogDirectory"
Write-Host ''

foreach ($spec in $specs) {
    Test-StaticContract -Spec $spec -ProjectDir $projectDir
    Test-CompatibilityProjectTemplate -Spec $spec -ProjectDir $projectDir
}

if (($Mode -eq 'Build' -or $Mode -eq 'All') -and -not $SkipMakeDryRun) {
    foreach ($spec in $specs) {
        Invoke-MakeDryRun -Spec $spec -ProjectDir $projectDir -LogDir $LogDirectory
    }
}

if (($Mode -eq 'Build' -or $Mode -eq 'All') -and -not $SkipMsBuild) {
    foreach ($spec in $specs) {
        Invoke-MSBuildOperation -Spec $spec -ProjectFile $projectFile -ProjectDir $projectDir -LogDir $LogDirectory -Configuration 'Debug' -OperationName 'msbuild Clean' -Target 'Clean' -Timeout $TimeoutSeconds
        Invoke-MSBuildOperation -Spec $spec -ProjectFile $projectFile -ProjectDir $projectDir -LogDir $LogDirectory -Configuration 'Debug' -OperationName 'msbuild Debug build' -Target 'Build' -Timeout $TimeoutSeconds
        Invoke-MSBuildOperation -Spec $spec -ProjectFile $projectFile -ProjectDir $projectDir -LogDir $LogDirectory -Configuration 'Release' -OperationName 'msbuild Release build' -Target 'Build' -Timeout $TimeoutSeconds
        Assert-NoRootArtifactsAfterBuild -Spec $spec -ProjectDir $projectDir
    }
}

if ($Mode -eq 'Hardware' -or $Mode -eq 'All') {
    foreach ($spec in $specs) {
        Invoke-MSBuildOperation -Spec $spec -ProjectFile $projectFile -ProjectDir $projectDir -LogDir $LogDirectory -Configuration 'Upload' -OperationName 'msbuild Upload' -Target 'Build' -Timeout $TimeoutSeconds
        Invoke-MSBuildOperation -Spec $spec -ProjectFile $projectFile -ProjectDir $projectDir -LogDir $LogDirectory -Configuration 'Verify' -OperationName 'msbuild Verify' -Target 'Build' -Timeout $TimeoutSeconds
    }
}

$summaryPath = Join-Path $LogDirectory 'summary.json'
$script:Results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

Write-Host ''
Write-Host 'Summary:'
$script:Results | Group-Object Status | Sort-Object Name | ForEach-Object {
    Write-Host ('  {0}: {1}' -f $_.Name, $_.Count)
}
Write-Host "Summary JSON: $summaryPath"

$failCount = @($script:Results | Where-Object { $_.Status -eq 'FAIL' }).Count
if ($failCount -gt 0) {
    Write-Error "Board operation check failed with $failCount failure(s)." -ErrorAction Continue
    exit 1
}

Write-Host 'Board operation check passed.'
exit 0
