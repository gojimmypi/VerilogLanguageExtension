param(
    [string]$SourceFile = "TestFiles/comma.v",
    [string]$RootSuffix = "Exp",
    [string]$VisualStudioPath = "",
    [string]$Configuration = "Debug",
    [switch]$CloseVisualStudioWhenDone,
    [switch]$LeaveVisualStudioOpen,
    [switch]$ResetVisualStudioBeforeRun,
    [switch]$ReuseExistingVisualStudio,
    [switch]$SkipExtensionPrep,
    [switch]$SkipBuildAndDeploy
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    if (![string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        return (Resolve-Path -LiteralPath $PSScriptRoot).Path
    }

    return (Resolve-Path -LiteralPath (Split-Path -Parent $MyInvocation.MyCommand.Path)).Path
}

function Join-RepoPath {
    param(
        [string]$RepoRoot,
        [string]$RelativePath
    )

    if ([System.IO.Path]::IsPathRooted($RelativePath)) {
        return [System.IO.Path]::GetFullPath($RelativePath)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $RelativePath))
}

function Write-Utf8TextFile {
    param(
        [string]$Path,
        [string]$Text
    )

    $parent = Split-Path -Parent $Path
    if (![string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }

    # Generated JSON is UTF-8 without BOM.
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Text, $utf8NoBom)
}

function Copy-SnapshotForReviewDiff {
    param(
        [string]$SourcePath,
        [string]$DestinationPath
    )

    $rawJson = [System.IO.File]::ReadAllText($SourcePath, [System.Text.Encoding]::UTF8)
    $json = $rawJson | ConvertFrom-Json

    foreach ($propertyName in @("GeneratedAtUtc", "GitCommit", "ProcessingTime", "RunTiming")) {
        $property = $json.PSObject.Properties[$propertyName]
        if ($null -ne $property) {
            $json.PSObject.Properties.Remove($propertyName)
        }
    }

    $text = $json | ConvertTo-Json -Depth 100
    Write-Utf8TextFile -Path $DestinationPath -Text ($text + [Environment]::NewLine)
}

function ConvertTo-NormalizedSnapshotPath {
    param([string]$Path)

    return $Path.Replace("\", "/").ToLowerInvariant()
}

function Get-NormalizedLeafName {
    param([string]$Path)

    $normalized = ConvertTo-NormalizedSnapshotPath -Path $Path
    $parts = $normalized.Split([char[]]@("/"), [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($parts.Count -lt 1) {
        return ""
    }

    return $parts[$parts.Count - 1]
}

function Test-ExpectationMatchesSourceFile {
    param(
        [string]$ExpectedFile,
        [string]$SourceFile
    )

    if ([string]::IsNullOrWhiteSpace($ExpectedFile)) {
        return $false
    }

    $expected = ConvertTo-NormalizedSnapshotPath -Path $ExpectedFile
    $source = ConvertTo-NormalizedSnapshotPath -Path $SourceFile
    $expectedLeaf = Get-NormalizedLeafName -Path $expected
    $sourceLeaf = Get-NormalizedLeafName -Path $source

    return ($source.EndsWith($expected) -or
        $expected.EndsWith($source) -or
        (![string]::IsNullOrWhiteSpace($expectedLeaf) -and $expectedLeaf -eq $sourceLeaf))
}


function Get-SnapshotRelativeSourceFile {
    param([string]$SnapshotPath)

    try {
        $rawJson = [System.IO.File]::ReadAllText($SnapshotPath, [System.Text.Encoding]::UTF8)
        $json = $rawJson | ConvertFrom-Json
        $relativeProperty = $json.PSObject.Properties["FileRelativePath"]
        if ($null -ne $relativeProperty -and $null -ne $relativeProperty.Value) {
            return [string]$relativeProperty.Value
        }

        $pathProperty = $json.PSObject.Properties["FilePath"]
        if ($null -ne $pathProperty -and $null -ne $pathProperty.Value) {
            return [string]$pathProperty.Value
        }
    }
    catch {
        Write-Warning "Could not read baseline snapshot $SnapshotPath`: $_"
    }

    return ""
}

function Find-BaselineSnapshotForSourceFile {
    param(
        [string]$SourceFile,
        [string]$BaselineDir
    )

    if (!(Test-Path -LiteralPath $BaselineDir)) {
        return @()
    }

    $matches = @()
    foreach ($snapshotPath in @(Get-ChildItem -LiteralPath $BaselineDir -Filter "*.snapshot.json" -File -ErrorAction SilentlyContinue)) {
        $snapshotSourceFile = Get-SnapshotRelativeSourceFile -SnapshotPath $snapshotPath.FullName
        if (Test-ExpectationMatchesSourceFile -ExpectedFile $snapshotSourceFile -SourceFile $SourceFile) {
            $matches += $snapshotPath
        }
    }

    if ($matches.Count -gt 0) {
        return @($matches | Sort-Object Name | Select-Object -First 1)
    }

    $baseName = Split-Path $SourceFile -Leaf
    return @(Get-ChildItem -Path (Join-Path $BaselineDir "*-$baseName.snapshot.json") -File -ErrorAction SilentlyContinue |
        Sort-Object Name |
        Select-Object -First 1)
}

function New-FilteredExpectationDirectory {
    param(
        [string]$SourceFile,
        [string]$ExpectationsRoot,
        [string]$FilteredExpectationsRoot
    )

    Remove-Item $FilteredExpectationsRoot -Recurse -Force -ErrorAction SilentlyContinue
    New-Item $FilteredExpectationsRoot -ItemType Directory -Force | Out-Null

    if (!(Test-Path $ExpectationsRoot)) {
        Write-Host "Expectation directory not found, skipping targeted expectations: $ExpectationsRoot"
        return 0
    }

    $matchedCount = 0
    foreach ($expectationPath in @(Get-ChildItem $ExpectationsRoot -Filter "*.expect.json" -File)) {
        $expectation = Get-Content $expectationPath.FullName -Raw | ConvertFrom-Json
        if (Test-ExpectationMatchesSourceFile -ExpectedFile $expectation.File -SourceFile $SourceFile) {
            Copy-Item -Force -Path $expectationPath.FullName -Destination (Join-Path $FilteredExpectationsRoot $expectationPath.Name)
            $matchedCount++
        }
    }

    return $matchedCount
}

function Invoke-CompareSnapshots {
    param(
        [string]$CompareScript,
        [string]$CurrentDir,
        [string]$ExpectationsDir = ""
    )

    if ([string]::IsNullOrWhiteSpace($ExpectationsDir)) {
        & python $CompareScript --current $CurrentDir
    }
    else {
        & python $CompareScript --current $CurrentDir --expectations $ExpectationsDir
    }

    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        Write-Warning "Compare-Snapshots.py returned exit code $exitCode; continuing because check-file.ps1 is a one-file local check."
    }

    $global:LASTEXITCODE = 0
    return $exitCode
}

function Resolve-VisualStudioPath {
    param([string]$RequestedPath)

    if (![string]::IsNullOrWhiteSpace($RequestedPath)) {
        $resolved = [System.IO.Path]::GetFullPath($RequestedPath)
        if (!(Test-Path -LiteralPath $resolved)) {
            throw "Visual Studio not found: $resolved"
        }
        return $resolved
    }

    $candidatePaths = @(
        "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe",
        "C:\Program Files\Microsoft Visual Studio\18\Enterprise\Common7\IDE\devenv.exe",
        "C:\Program Files\Microsoft Visual Studio\18\Professional\Common7\IDE\devenv.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\devenv.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\devenv.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe"
    )

    foreach ($candidate in $candidatePaths) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path -LiteralPath $vswhere) {
        $installPath = (& $vswhere -latest -products * -property installationPath 2>$null | Select-Object -First 1)
        if (![string]::IsNullOrWhiteSpace($installPath)) {
            $candidate = Join-Path $installPath "Common7\IDE\devenv.exe"
            if (Test-Path -LiteralPath $candidate) {
                return $candidate
            }
        }
    }

    throw "Could not find Visual Studio. Pass -VisualStudioPath."
}

function Get-VisualStudioMajorVersion {
    param([string]$ResolvedVisualStudioPath)

    $normalized = $ResolvedVisualStudioPath.Replace("/", "\")
    if ($normalized -match "\\Microsoft Visual Studio\\(?<Major>[0-9]+)\\") {
        return $Matches["Major"]
    }

    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($ResolvedVisualStudioPath)
    if ($versionInfo -ne $null -and $versionInfo.FileMajorPart -gt 0) {
        return [string]$versionInfo.FileMajorPart
    }

    throw "Could not determine Visual Studio major version from: $ResolvedVisualStudioPath"
}

function Get-MsBuildPathForVisualStudio {
    param([string]$ResolvedVisualStudioPath)

    $ideDir = Split-Path -Parent $ResolvedVisualStudioPath
    $common7Dir = Split-Path -Parent $ideDir
    $installDir = Split-Path -Parent $common7Dir
    $candidate = Join-Path $installDir "MSBuild\Current\Bin\MSBuild.exe"

    if (Test-Path -LiteralPath $candidate) {
        return $candidate
    }

    throw "MSBuild not found for Visual Studio path: $ResolvedVisualStudioPath"
}

function Stop-ExperimentalVisualStudio {
    param(
        [string]$ResolvedVisualStudioPath,
        [string]$RootSuffix
    )

    $rootSuffixPattern = "(?i)/rootsuffix\s+" + [regex]::Escape($RootSuffix) + "(\s|$)"
    $targetPath = [System.IO.Path]::GetFullPath($ResolvedVisualStudioPath)

    foreach ($process in @(Get-CimInstance Win32_Process -Filter "name = 'devenv.exe'" -ErrorAction SilentlyContinue)) {
        $commandLine = [string]$process.CommandLine
        $exePath = [string]$process.ExecutablePath
        if ([string]::IsNullOrWhiteSpace($commandLine)) {
            continue
        }

        if ($commandLine -notmatch $rootSuffixPattern) {
            continue
        }

        if (![string]::IsNullOrWhiteSpace($exePath)) {
            $exeFullPath = [System.IO.Path]::GetFullPath($exePath)
            if (![string]::Equals($exeFullPath, $targetPath, [System.StringComparison]::OrdinalIgnoreCase)) {
                continue
            }
        }

        Write-Host "Stopping Experimental Visual Studio PID $($process.ProcessId): $exePath"
        Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
    }
}

function Get-ExperimentalHiveDirectories {
    param(
        [string]$VisualStudioMajor,
        [string]$RootSuffix
    )

    $visualStudioLocalAppData = Join-Path $env:LOCALAPPDATA "Microsoft\VisualStudio"
    if (!(Test-Path -LiteralPath $visualStudioLocalAppData)) {
        return @()
    }

    $pattern = ("{0}.0*{1}" -f $VisualStudioMajor, $RootSuffix)
    return @(Get-ChildItem -LiteralPath $visualStudioLocalAppData -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like $pattern })
}

function Clear-ExperimentalExtensionState {
    param(
        [System.IO.DirectoryInfo[]]$Hives
    )

    foreach ($hive in @($Hives)) {
        Write-Host "Cleaning Visual Studio hive: $($hive.FullName)"

        $componentModelCache = Join-Path $hive.FullName "ComponentModelCache"
        if (Test-Path -LiteralPath $componentModelCache) {
            Write-Host "  Removing MEF cache: $componentModelCache"
            Remove-Item -LiteralPath $componentModelCache -Recurse -Force -ErrorAction SilentlyContinue
        }

        $extensionsDir = Join-Path $hive.FullName "Extensions"
        if (Test-Path -LiteralPath $extensionsDir) {
            $deployedDlls = @(Get-ChildItem -LiteralPath $extensionsDir -Recurse -Filter "VerilogLanguage.dll" -File -ErrorAction SilentlyContinue)
            foreach ($dll in $deployedDlls) {
                $extensionFolder = $dll.Directory.FullName
                Write-Host "  Removing deployed VLE extension: $extensionFolder"
                Remove-Item -LiteralPath $extensionFolder -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

function Invoke-ProjectBuildForExpHive {
    param(
        [string]$MsBuildPath,
        [string]$ProjectPath,
        [string]$Configuration,
        [string]$RootSuffix
    )

    Write-Host "Building $Configuration VSIX through MSBuild/VSSDK for root suffix '$RootSuffix'."
    Write-Host "MSBuild: $MsBuildPath"

    $commonArgs = @(
        $ProjectPath,
        "/p:Configuration=$Configuration",
        "/p:DeployExtension=true",
        "/p:VSSDKTargetPlatformRegRootSuffix=$RootSuffix",
        "/v:minimal",
        "/nologo"
    )

    & $MsBuildPath @commonArgs "/t:Restore"
    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild restore failed with exit code $LASTEXITCODE"
    }

    & $MsBuildPath @commonArgs "/t:Rebuild"
    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild rebuild failed with exit code $LASTEXITCODE"
    }
}

function Confirm-ExperimentalDeployment {
    param(
        [System.IO.DirectoryInfo[]]$Hives,
        [string]$RepoRoot,
        [string]$Configuration
    )

    $builtDll = Join-Path $RepoRoot ("bin\{0}\VerilogLanguage.dll" -f $Configuration)
    if (!(Test-Path -LiteralPath $builtDll)) {
        throw "Built VLE DLL not found: $builtDll"
    }

    $builtInfo = Get-Item -LiteralPath $builtDll
    $deployedDlls = @()
    foreach ($hive in @($Hives)) {
        $extensionsDir = Join-Path $hive.FullName "Extensions"
        if (Test-Path -LiteralPath $extensionsDir) {
            $deployedDlls += @(Get-ChildItem -LiteralPath $extensionsDir -Recurse -Filter "VerilogLanguage.dll" -File -ErrorAction SilentlyContinue)
        }
    }

    if ($deployedDlls.Count -lt 1) {
        throw "No Experimental VLE DLL was found after build/deploy."
    }

    $newest = @($deployedDlls | Sort-Object LastWriteTime -Descending | Select-Object -First 1)[0]
    Write-Host "Confirmed Experimental VLE DLL: $($newest.FullName)"
    Write-Host "  Built:    $($builtInfo.LastWriteTime)  $($builtInfo.Length) bytes"
    Write-Host "  Deployed: $($newest.LastWriteTime)  $($newest.Length) bytes"

    if ($newest.LastWriteTime -lt $builtInfo.LastWriteTime.AddSeconds(-2)) {
        throw "Experimental VLE DLL is older than the built DLL. Deployment did not refresh the Exp hive."
    }
}

$repoRoot = Get-RepoRoot
[System.IO.Directory]::SetCurrentDirectory($repoRoot)
Set-Location -LiteralPath $repoRoot

$manifestName = "single-testfile"
$manifestPath = Join-RepoPath -RepoRoot $repoRoot -RelativePath "tools\vle-ci\manifests\$manifestName.json"
$outputDir = Join-RepoPath -RepoRoot $repoRoot -RelativePath "artifacts\snapshots\$manifestName"
$expectations = Join-RepoPath -RepoRoot $repoRoot -RelativePath "tools\vle-ci\expectations"
$filteredExpectations = Join-RepoPath -RepoRoot $repoRoot -RelativePath "artifacts\snapshots\$manifestName-expectations"
$exportScript = Join-RepoPath -RepoRoot $repoRoot -RelativePath "tools\vle-ci\Export-Snapshots.ps1"
$compareScript = Join-RepoPath -RepoRoot $repoRoot -RelativePath "tools\vle-ci\Compare-Snapshots.py"
$baselineDir = Join-RepoPath -RepoRoot $repoRoot -RelativePath "tests\snapshots\baselines\development-main\all-testfiles"
$projectPath = Join-RepoPath -RepoRoot $repoRoot -RelativePath "VerilogLanguage.csproj"

if (!(Test-Path -LiteralPath $exportScript)) {
    throw "Export script not found: $exportScript"
}

if (!(Test-Path -LiteralPath $compareScript)) {
    throw "Compare script not found: $compareScript"
}

if (!(Test-Path -LiteralPath $projectPath)) {
    throw "Project file not found: $projectPath"
}

$resolvedVisualStudioPath = Resolve-VisualStudioPath -RequestedPath $VisualStudioPath
$visualStudioMajor = Get-VisualStudioMajorVersion -ResolvedVisualStudioPath $resolvedVisualStudioPath
$msBuildPath = Get-MsBuildPathForVisualStudio -ResolvedVisualStudioPath $resolvedVisualStudioPath
$hives = @(Get-ExperimentalHiveDirectories -VisualStudioMajor $visualStudioMajor -RootSuffix $RootSuffix)

$effectiveCloseVisualStudioWhenDone = $false

#if ($LeaveVisualStudioOpen.IsPresent) {
#    $effectiveCloseVisualStudioWhenDone = $false
#}
#if ($CloseVisualStudioWhenDone.IsPresent) {
#    $effectiveCloseVisualStudioWhenDone = $true
#}
#
$effectiveResetVisualStudioBeforeRun = $true
if ($ReuseExistingVisualStudio.IsPresent) {
    $effectiveResetVisualStudioBeforeRun = $false
}
if ($ResetVisualStudioBeforeRun.IsPresent) {
    $effectiveResetVisualStudioBeforeRun = $true
}

Write-Host "Single-file defaults:"
Write-Host "  Clean Experimental extension cache: $(!$SkipExtensionPrep.IsPresent)"
Write-Host "  Build/deploy through MSBuild:       $(!$SkipBuildAndDeploy.IsPresent)"
Write-Host "  VSIXInstaller:                     disabled"
Write-Host "  Reset Visual Studio before run:    $effectiveResetVisualStudioBeforeRun"
Write-Host "  Close Visual Studio when done:     $effectiveCloseVisualStudioWhenDone"
Write-Host "  Visual Studio:                     $resolvedVisualStudioPath"
Write-Host "  Root suffix:                       $RootSuffix"
Write-Host "  Hive pattern:                      $visualStudioMajor.0*$RootSuffix"

if ($effectiveResetVisualStudioBeforeRun) {
    Stop-ExperimentalVisualStudio -ResolvedVisualStudioPath $resolvedVisualStudioPath -RootSuffix $RootSuffix
}

if (!$SkipExtensionPrep.IsPresent) {
    if ($hives.Count -lt 1) {
        Write-Host "No existing Experimental hive matched $visualStudioMajor.0*$RootSuffix. It may be created during deployment."
    }
    else {
        Clear-ExperimentalExtensionState -Hives $hives
    }
}

if (!$SkipBuildAndDeploy.IsPresent) {
    Invoke-ProjectBuildForExpHive `
        -MsBuildPath $msBuildPath `
        -ProjectPath $projectPath `
        -Configuration $Configuration `
        -RootSuffix $RootSuffix

    $hives = @(Get-ExperimentalHiveDirectories -VisualStudioMajor $visualStudioMajor -RootSuffix $RootSuffix)
    Confirm-ExperimentalDeployment -Hives $hives -RepoRoot $repoRoot -Configuration $Configuration
}

# Recreate the one-file snapshot output directory.
# This avoids stale snapshots from previous full or partial CI runs.
Remove-Item $outputDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item $outputDir -ItemType Directory -Force | Out-Null

# Create a temporary one-file manifest.
# RunName is kept as all-testfiles so snapshot content stays comparable
# with the normal approved baseline naming/content.
# DelayMs controls how long VS waits after opening the file.
# FreshInstancePerFile is false because this manifest contains only one file.
$baselineForSource = @(Find-BaselineSnapshotForSourceFile -SourceFile $SourceFile -BaselineDir $baselineDir)
if ($baselineForSource.Count -gt 0) {
    $sourceManifestEntry = [ordered]@{
        Path = $SourceFile
        SnapshotFileName = $baselineForSource[0].Name
    }
}
else {
    $sourceManifestEntry = $SourceFile
}

$manifest = @{
    RunName = "all-testfiles"
    DelayMs = 3000
    FreshInstancePerFile = $false
    Files = @(
        $sourceManifestEntry
    )
}
$manifestJson = $manifest | ConvertTo-Json -Depth 5
Write-Utf8TextFile -Path $manifestPath -Text ($manifestJson + [Environment]::NewLine)

# Export only the file listed in the temporary manifest.
$exportArgs = @{
    Manifest = $manifestPath
    OutputDir = $outputDir
    RootSuffix = $RootSuffix
    MaxWaitSeconds = 45
    SkipBackgroundProcessCleanup = $true
}

if (!$effectiveCloseVisualStudioWhenDone) {
    $exportArgs["LeaveVisualStudioOpen"] = $true
}

if (![string]::IsNullOrWhiteSpace($resolvedVisualStudioPath)) {
    # Pin snapshot export to the same Visual Studio installation used for build/debug.
    $exportArgs["VisualStudioPath"] = $resolvedVisualStudioPath
}

if ($ReuseExistingVisualStudio.IsPresent -and !$ResetVisualStudioBeforeRun.IsPresent) {
    $exportArgs["SkipInitialVisualStudioCleanup"] = $true
}

# Run the snapshot exporter with explicit arguments so RootSuffix, VS path,
# cleanup behavior, and output directory are easy to audit in the console log.
& $exportScript @exportArgs

if ($LASTEXITCODE -ne 0) {
    throw "Export-Snapshots.ps1 failed with exit code $LASTEXITCODE"
}

# Confirm exactly one snapshot was generated.
# This prevents accidentally comparing stale files or checking the wrong run.
$currentSnapshots = @(Get-ChildItem $outputDir -Filter "*.snapshot.json" -File)

if ($currentSnapshots.Count -ne 1) {
    throw "Expected exactly one snapshot in $outputDir, found $($currentSnapshots.Count)"
}

$current = $currentSnapshots[0]

# First run the built-in snapshot sanity checks on the isolated one-file output.
# Do not pass the full expectations directory here; those expectations cover
# other test files and would fail because this run intentionally exported one file.
[void](Invoke-CompareSnapshots -CompareScript $compareScript -CurrentDir $outputDir)

# If there are targeted expectation files for this source, copy only those
# expectations to a temporary directory and run them against the one-file output.
$matchedExpectations = New-FilteredExpectationDirectory `
    -SourceFile $SourceFile `
    -ExpectationsRoot $expectations `
    -FilteredExpectationsRoot $filteredExpectations

if ($matchedExpectations -gt 0) {
    Write-Host "Targeted expectations matched: $matchedExpectations"
    [void](Invoke-CompareSnapshots `
        -CompareScript $compareScript `
        -CurrentDir $outputDir `
        -ExpectationsDir $filteredExpectations)
}
else {
    Write-Host "No targeted expectations matched $SourceFile; skipping expectation checks."
}

# Find the matching approved baseline snapshot from the full all-testfiles baseline.
# Prefer FileRelativePath inside the snapshot; fall back to the leaf-name wildcard
# for older baselines.
$baseline = @(Find-BaselineSnapshotForSourceFile -SourceFile $SourceFile -BaselineDir $baselineDir)

if ($baseline.Count -lt 1) {
    Write-Warning "No matching baseline snapshot found for $SourceFile; skipping baseline diff."
}
else {
    # Compare the generated one-file snapshot against the approved baseline.
    # --no-index lets git compare arbitrary files, not only tracked files.
    & git -C $repoRoot diff --no-index -- $baseline[0].FullName $current.FullName
    $diffExitCode = $LASTEXITCODE

    if ($diffExitCode -eq 0) {
        Write-Host "Baseline diff: no differences."
    }
    elseif ($diffExitCode -eq 1) {
        Write-Host "Baseline diff: differences shown above."
    }
    else {
        Write-Warning "git diff returned exit code $diffExitCode."
    }

    $global:LASTEXITCODE = 0
}

Write-Host "One-file snapshot check complete:"
Write-Host "  Source:   $SourceFile"
Write-Host "  Current:  $($current.FullName)"
if ($baseline.Count -ge 1) {
    Write-Host "  Baseline: $($baseline[0].FullName)"
}
else {
    Write-Host "  Baseline: not found"
}

$global:LASTEXITCODE = 0
