<#
Checks release-relevant text files for UTF-8 validity and common bad characters.

Default behavior:
- Scans common source, project, script, HDL, config, and documentation text files.
- Excludes local/build/cache directories such as .git, .vs, bin, obj, and packages.
- Optional -ExcludePath skips exact repo-relative paths or whole subtrees.
- Fails on invalid UTF-8 byte sequences.
- Fails on common smart punctuation and invisible/problem characters.

Recommended VLE release check:
    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\check-text-hygiene.ps1 -RequireNoBom -RequireCrLf -RequireFinalNewline
#>

[CmdletBinding()]
param(
    [string]$Root = ".",
    [string[]]$ExcludePath = @(),
    [switch]$RequireNoBom,
    [switch]$RequireCrLf,
    [switch]$RequireFinalNewline,
    [switch]$AsciiOnly,
    [int]$MaxFailures = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$rootPath = (Resolve-Path -LiteralPath $Root).Path.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)

$textExtensions = @(
    ".appxmanifest",
    ".bat",
    ".cmd",
    ".config",
    ".cs",
    ".csproj",
    ".editorconfig",
    ".gitignore",
    ".json",
    ".md",
    ".pcf",
    ".props",
    ".ps1",
    ".psd1",
    ".psm1",
    ".py",
    ".resx",
    ".ruleset",
    ".sdc",
    ".sh",
    ".sln",
    ".svg",
    ".sv",
    ".svh",
    ".targets",
    ".tcl",
    ".txt",
    ".v",
    ".vh",
    ".vsct",
    ".vstemplate",
    ".xaml",
    ".xml",
    ".xsd",
    ".yaml",
    ".yml"
)

$textFileNames = @(
    ".editorconfig",
    ".gitignore",
    "LICENSE",
    "Makefile",
    "README"
)

$excludedDirectoryNames = @(
    ".git",
    ".vs",
    "bin",
    "obj",
    "packages",
    "TestResults",
    "node_modules"
)

$badCharacters = @(
    @{ Name = "em dash"; CodePoint = 0x2014; Replacement = "-" },
    @{ Name = "en dash"; CodePoint = 0x2013; Replacement = "-" },
    @{ Name = "minus sign"; CodePoint = 0x2212; Replacement = "-" },
    @{ Name = "left single smart quote"; CodePoint = 0x2018; Replacement = "'" },
    @{ Name = "right single smart quote"; CodePoint = 0x2019; Replacement = "'" },
    @{ Name = "left double smart quote"; CodePoint = 0x201C; Replacement = '"' },
    @{ Name = "right double smart quote"; CodePoint = 0x201D; Replacement = '"' },
    @{ Name = "ellipsis"; CodePoint = 0x2026; Replacement = "..." },
    @{ Name = "non-breaking space"; CodePoint = 0x00A0; Replacement = " " },
    @{ Name = "zero-width space"; CodePoint = 0x200B; Replacement = "" },
    @{ Name = "byte order mark in text"; CodePoint = 0xFEFF; Replacement = "" }
)

$failures = New-Object System.Collections.Generic.List[string]
$utf8Strict = New-Object System.Text.UTF8Encoding($false, $true)

function Add-Failure {
    param([string]$Message)

    if ($script:failures.Count -lt $MaxFailures) {
        [void]$script:failures.Add($Message)
    }
}

function Get-RelativePath {
    param([string]$FullName)

    if ($FullName.StartsWith($script:rootPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $FullName.Substring($script:rootPath.Length).TrimStart("\", "/")
    }

    return $FullName
}

function Get-NormalizedRelativePathKey {
    param([string]$Path)

    $pathValue = $Path.Trim()

    if ([string]::IsNullOrWhiteSpace($pathValue)) {
        return ""
    }

    try {
        if ([System.IO.Path]::IsPathRooted($pathValue)) {
            $fullPath = [System.IO.Path]::GetFullPath($pathValue)
        } else {
            $fullPath = [System.IO.Path]::GetFullPath((Join-Path -Path $script:rootPath -ChildPath $pathValue))
        }

        $relativePath = Get-RelativePath -FullName $fullPath
    } catch {
        $relativePath = $pathValue
    }

    return (($relativePath -replace '/', '\').Trim('\'))
}

function Get-NormalizedFullPathKey {
    param([string]$Path)

    $pathValue = $Path.Trim()

    if ([string]::IsNullOrWhiteSpace($pathValue)) {
        return ""
    }

    try {
        if ([System.IO.Path]::IsPathRooted($pathValue)) {
            $fullPath = [System.IO.Path]::GetFullPath($pathValue)
        } else {
            $fullPath = [System.IO.Path]::GetFullPath((Join-Path -Path $script:rootPath -ChildPath $pathValue))
        }
    } catch {
        return ""
    }

    return $fullPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
}

$script:explicitExcludedPathKeys = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$script:explicitExcludedFullPathKeys = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)

foreach ($path in $ExcludePath) {
    $pathKey = Get-NormalizedRelativePathKey -Path $path
    $fullPathKey = Get-NormalizedFullPathKey -Path $path

    if ($pathKey.Length -gt 0) {
        [void]$script:explicitExcludedPathKeys.Add($pathKey)
    }

    if ($fullPathKey.Length -gt 0) {
        [void]$script:explicitExcludedFullPathKeys.Add($fullPathKey)
    }
}

function Test-IsExcludedPath {
    param([string]$FullName)

    $relativePath = Get-RelativePath -FullName $FullName
    $relativePathKey = Get-NormalizedRelativePathKey -Path $relativePath
    $fullPathKey = Get-NormalizedFullPathKey -Path $FullName
    $parts = $relativePath -split "[\\/]"

    foreach ($part in $parts) {
        if ($script:excludedDirectoryNames -contains $part) {
            return $true
        }
    }

    if ($script:explicitExcludedPathKeys.Contains($relativePathKey)) {
        return $true
    }

    if ($fullPathKey.Length -gt 0 -and $script:explicitExcludedFullPathKeys.Contains($fullPathKey)) {
        return $true
    }

    foreach ($excludedPathKey in $script:explicitExcludedPathKeys) {
        if ($relativePathKey.StartsWith(($excludedPathKey + '\'), [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    foreach ($excludedFullPathKey in $script:explicitExcludedFullPathKeys) {
        if ($fullPathKey.StartsWith(($excludedFullPathKey + [System.IO.Path]::DirectorySeparatorChar), [System.StringComparison]::OrdinalIgnoreCase) -or
            $fullPathKey.StartsWith(($excludedFullPathKey + [System.IO.Path]::AltDirectorySeparatorChar), [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

function Test-IsTextFile {
    param([System.IO.FileInfo]$File)

    $name = $File.Name
    $extension = $File.Extension.ToLowerInvariant()

    if ($script:textFileNames -contains $name) {
        return $true
    }

    if ($script:textExtensions -contains $extension) {
        return $true
    }

    return $false
}

function Get-CodePointText {
    param([int]$CodePoint)

    return ("U+{0:X4}" -f $CodePoint)
}

function Check-LineEndings {
    param(
        [string]$RelativePath,
        [string]$Text
    )

    $crlfCount = ([regex]::Matches($Text, "`r`n")).Count
    $textWithoutCrlf = [regex]::Replace($Text, "`r`n", "")
    $bareLfCount = ([regex]::Matches($textWithoutCrlf, "`n")).Count
    $bareCrCount = ([regex]::Matches($textWithoutCrlf, "`r")).Count

    if ($bareLfCount -gt 0 -or $bareCrCount -gt 0) {
        Add-Failure ("{0}: line endings are not CRLF-only (CRLF={1}, bare LF={2}, bare CR={3})" -f $RelativePath, $crlfCount, $bareLfCount, $bareCrCount)
    }
}

function Check-FinalNewline {
    param(
        [string]$RelativePath,
        [string]$Text
    )

    if ($Text.Length -gt 0 -and -not $Text.EndsWith("`n", [System.StringComparison]::Ordinal)) {
        Add-Failure ("{0}: missing final newline" -f $RelativePath)
    }
}

function Check-BadCharacters {
    param(
        [string]$RelativePath,
        [string]$Text
    )

    $lines = $Text -split "`r`n|`n|`r", -1

    for ($lineIndex = 0; $lineIndex -lt $lines.Count; $lineIndex++) {
        $line = $lines[$lineIndex]
        $lineNumber = $lineIndex + 1

        foreach ($badCharacter in $script:badCharacters) {
            $codePoint = [int]$badCharacter.CodePoint
            $needle = [string][char]$codePoint
            $index = $line.IndexOf($needle, [System.StringComparison]::Ordinal)

            while ($index -ge 0) {
                $columnNumber = $index + 1
                Add-Failure ("{0}:{1}:{2}: forbidden {3} {4}; use '{5}'" -f $RelativePath, $lineNumber, $columnNumber, $badCharacter.Name, (Get-CodePointText -CodePoint $codePoint), $badCharacter.Replacement)
                $index = $line.IndexOf($needle, $index + 1, [System.StringComparison]::Ordinal)
            }
        }

        for ($charIndex = 0; $charIndex -lt $line.Length; $charIndex++) {
            $codePoint = [int][char]$line[$charIndex]

            if (($codePoint -lt 32 -and $codePoint -notin @(9, 10, 13)) -or $codePoint -eq 127) {
                Add-Failure ("{0}:{1}:{2}: forbidden control character {3}" -f $RelativePath, $lineNumber, ($charIndex + 1), (Get-CodePointText -CodePoint $codePoint))
            }

            if ($AsciiOnly -and $codePoint -gt 127) {
                Add-Failure ("{0}:{1}:{2}: non-ASCII character {3}" -f $RelativePath, $lineNumber, ($charIndex + 1), (Get-CodePointText -CodePoint $codePoint))
            }
        }
    }
}

$files = Get-ChildItem -LiteralPath $rootPath -Recurse -File |
    Where-Object { -not (Test-IsExcludedPath -FullName $_.FullName) } |
    Where-Object { Test-IsTextFile -File $_ } |
    Sort-Object FullName

$checkedCount = 0

foreach ($file in $files) {
    $checkedCount++
    $relativePath = Get-RelativePath -FullName $file.FullName
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)

    $hasUtf8Bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF

    if ($RequireNoBom -and $hasUtf8Bom) {
        Add-Failure ("{0}: UTF-8 BOM found; save as UTF-8 without BOM" -f $relativePath)
    }

    try {
        $text = $utf8Strict.GetString($bytes)
    } catch [System.Text.DecoderFallbackException] {
        Add-Failure ("{0}: invalid UTF-8 byte sequence" -f $relativePath)
        continue
    }

    if ($RequireCrLf) {
        Check-LineEndings -RelativePath $relativePath -Text $text
    }

    if ($RequireFinalNewline) {
        Check-FinalNewline -RelativePath $relativePath -Text $text
    }

    Check-BadCharacters -RelativePath $relativePath -Text $text
}

if ($failures.Count -gt 0) {
    Write-Host "Text hygiene check failed."
    Write-Host "Files checked: $checkedCount"
    Write-Host "Failures found: $($failures.Count)"

    foreach ($failure in $failures) {
        Write-Host "::error::$failure"
    }

    if ($failures.Count -ge $MaxFailures) {
        Write-Host "::warning::Failure output was capped at $MaxFailures findings."
    }

    exit 1
}

Write-Host "Text hygiene check passed. Files checked: $checkedCount"
