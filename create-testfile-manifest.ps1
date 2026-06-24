# regenerates the all-testfiles manifest with stable snapshot file names

[CmdletBinding()]
param(
    [string]$TestFilesRoot = "TestFiles",
    [string]$ManifestPath = "tools\vle-ci\manifests\all-testfiles.json",
    [string]$BaselineDir = "tests\snapshots\baselines\development-main\all-testfiles",
    [int]$DelayMs = 3000,
    [switch]$NoStableSnapshotNames
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

function ConvertTo-RepoRelativePath {
    param(
        [string]$RepoRoot,
        [string]$Path
    )

    $rootPath = [System.IO.Path]::GetFullPath($RepoRoot)
    if (!$rootPath.EndsWith([System.IO.Path]::DirectorySeparatorChar.ToString())) {
        $rootPath += [System.IO.Path]::DirectorySeparatorChar
    }

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $rootUri = New-Object System.Uri($rootPath)
    $pathUri = New-Object System.Uri($fullPath)
    $relativeUri = $rootUri.MakeRelativeUri($pathUri)
    return ([System.Uri]::UnescapeDataString($relativeUri.ToString())).Replace("\", "/")
}

function ConvertTo-NormalizedKey {
    param([string]$Path)

    return $Path.Replace("\", "/").ToLowerInvariant()
}

function ConvertTo-SnapshotSafeFileName {
    param([string]$FilePath)

    if ([string]::IsNullOrWhiteSpace($FilePath)) {
        return "untitled"
    }

    $name = [System.IO.Path]::GetFileName($FilePath)
    foreach ($c in [System.IO.Path]::GetInvalidFileNameChars()) {
        $name = $name.Replace([string]$c, "_")
    }

    return $name
}

function Get-SnapshotIndexFromName {
    param([string]$SnapshotFileName)

    if ($SnapshotFileName -match "^(?<Index>[0-9]{4})-") {
        return [int]$Matches["Index"]
    }

    return 0
}

function Read-JsonFile {
    param([string]$Path)

    return ([System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8) | ConvertFrom-Json)
}

function Get-PropertyValue {
    param(
        [object]$Object,
        [string[]]$Names
    )

    if ($null -eq $Object) {
        return ""
    }

    foreach ($name in $Names) {
        $property = $Object.PSObject.Properties[$name]
        if ($null -ne $property -and $null -ne $property.Value) {
            return [string]$property.Value
        }
    }

    return ""
}

function Get-ManifestEntryPath {
    param([object]$Entry)

    if ($Entry -is [string]) {
        return [string]$Entry
    }

    return Get-PropertyValue -Object $Entry -Names @("Path", "File", "SourceFile")
}

function Get-ManifestEntrySnapshotFileName {
    param([object]$Entry)

    if ($Entry -is [string]) {
        return ""
    }

    return Get-PropertyValue -Object $Entry -Names @("SnapshotFileName", "SnapshotName", "OutputName")
}

function Add-SnapshotNameMapping {
    param(
        [hashtable]$SnapshotNameByPath,
        [hashtable]$UsedSnapshotNames,
        [ref]$MaxSnapshotIndex,
        [string]$RelativePath,
        [string]$SnapshotFileName,
        [switch]$Overwrite
    )

    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [string]::IsNullOrWhiteSpace($SnapshotFileName)) {
        return
    }

    $key = ConvertTo-NormalizedKey -Path $RelativePath
    if ($Overwrite.IsPresent -or !$SnapshotNameByPath.ContainsKey($key)) {
        $SnapshotNameByPath[$key] = $SnapshotFileName
    }

    $UsedSnapshotNames[$SnapshotFileName.ToLowerInvariant()] = $true
    $index = Get-SnapshotIndexFromName -SnapshotFileName $SnapshotFileName
    if ($index -gt $MaxSnapshotIndex.Value) {
        $MaxSnapshotIndex.Value = $index
    }
}

function Add-BaselineSnapshotNameMappings {
    param(
        [string]$BaselineRoot,
        [hashtable]$SnapshotNameByPath,
        [hashtable]$UsedSnapshotNames,
        [hashtable]$BaselineKnownKeys,
        [ref]$MaxSnapshotIndex
    )

    if (!(Test-Path -LiteralPath $BaselineRoot)) {
        return
    }

    foreach ($snapshotPath in @(Get-ChildItem -LiteralPath $BaselineRoot -Filter "*.snapshot.json" -File -ErrorAction SilentlyContinue)) {
        try {
            $snapshot = Read-JsonFile -Path $snapshotPath.FullName
            $relativePath = Get-PropertyValue -Object $snapshot -Names @("FileRelativePath", "FilePath")
            $baselineKey = ConvertTo-NormalizedKey -Path $relativePath
            if (![string]::IsNullOrWhiteSpace($baselineKey)) {
                $BaselineKnownKeys[$baselineKey] = $true
            }

            Add-SnapshotNameMapping `
                -SnapshotNameByPath $SnapshotNameByPath `
                -UsedSnapshotNames $UsedSnapshotNames `
                -MaxSnapshotIndex $MaxSnapshotIndex `
                -RelativePath $relativePath `
                -SnapshotFileName $snapshotPath.Name `
                -Overwrite
        }
        catch {
            Write-Warning "Could not read baseline snapshot $($snapshotPath.FullName): $_"
        }
    }
}

function Add-ExistingManifestSnapshotNameMappings {
    param(
        [string]$ExistingManifestPath,
        [hashtable]$SnapshotNameByPath,
        [hashtable]$UsedSnapshotNames,
        [ref]$MaxSnapshotIndex
    )

    if (!(Test-Path -LiteralPath $ExistingManifestPath)) {
        return
    }

    try {
        $manifest = Read-JsonFile -Path $ExistingManifestPath
        foreach ($entry in @($manifest.Files)) {
            $relativePath = Get-ManifestEntryPath -Entry $entry
            $snapshotFileName = Get-ManifestEntrySnapshotFileName -Entry $entry
            Add-SnapshotNameMapping `
                -SnapshotNameByPath $SnapshotNameByPath `
                -UsedSnapshotNames $UsedSnapshotNames `
                -MaxSnapshotIndex $MaxSnapshotIndex `
                -RelativePath $relativePath `
                -SnapshotFileName $snapshotFileName
        }
    }
    catch {
        Write-Warning "Could not read existing manifest $ExistingManifestPath: $_"
    }
}

function Test-IsExcludedPath {
    param(
        [string]$RelativePath
    )

    $normalized = ConvertTo-NormalizedKey -Path $RelativePath
    $parts = $normalized.Split([char[]]@("/"), [System.StringSplitOptions]::RemoveEmptyEntries)
    foreach ($part in $parts) {
        if ($part -in @(".vs", "bin", "obj", "packages")) {
            return $true
        }
    }

    return $false
}

function Get-VerilogTestFiles {
    param(
        [string]$RepoRoot,
        [string]$TestFilesRoot
    )

    $testRoot = Join-RepoPath -RepoRoot $RepoRoot -RelativePath $TestFilesRoot
    if (!(Test-Path -LiteralPath $testRoot)) {
        throw "TestFiles root not found: $testRoot"
    }

    $extensions = @(".v", ".sv", ".svh", ".vh", ".verilog")
    $result = @()

    foreach ($file in @(Get-ChildItem -LiteralPath $testRoot -Recurse -File -ErrorAction SilentlyContinue)) {
        if ($extensions -notcontains $file.Extension.ToLowerInvariant()) {
            continue
        }

        $relativePath = ConvertTo-RepoRelativePath -RepoRoot $RepoRoot -Path $file.FullName
        if (Test-IsExcludedPath -RelativePath $relativePath) {
            continue
        }

        $key = ConvertTo-NormalizedKey -Path $relativePath
        $result += [pscustomobject]@{
            RelativePath = $relativePath
            FullName = $file.FullName
            Key = $key
        }
    }

    return @($result | Sort-Object RelativePath)
}

function New-SnapshotFileName {
    param(
        [string]$SourcePath,
        [int]$Index
    )

    $safeName = ConvertTo-SnapshotSafeFileName -FilePath $SourcePath
    return ("{0:0000}-{1}.snapshot.json" -f $Index, $safeName)
}

function Write-Utf8JsonFile {
    param(
        [string]$Path,
        [object]$Value
    )

    $parent = Split-Path -Parent $Path
    if (![string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $text = $Value | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText($Path, ($text + [Environment]::NewLine), $utf8NoBom)
}

$repoRoot = Get-RepoRoot
[System.IO.Directory]::SetCurrentDirectory($repoRoot)
Set-Location -LiteralPath $repoRoot

$resolvedManifestPath = Join-RepoPath -RepoRoot $repoRoot -RelativePath $ManifestPath
$resolvedBaselineDir = Join-RepoPath -RepoRoot $repoRoot -RelativePath $BaselineDir

$snapshotNameByPath = @{}
$usedSnapshotNames = @{}
$baselineKnownKeys = @{}
$maxSnapshotIndex = 0

if (!$NoStableSnapshotNames.IsPresent) {
    Add-ExistingManifestSnapshotNameMappings `
        -ExistingManifestPath $resolvedManifestPath `
        -SnapshotNameByPath $snapshotNameByPath `
        -UsedSnapshotNames $usedSnapshotNames `
        -MaxSnapshotIndex ([ref]$maxSnapshotIndex)

    # The approved baseline is authoritative for existing files. This keeps old
    # snapshot file names fixed even when new files are run before them.
    Add-BaselineSnapshotNameMappings `
        -BaselineRoot $resolvedBaselineDir `
        -SnapshotNameByPath $snapshotNameByPath `
        -UsedSnapshotNames $usedSnapshotNames `
        -BaselineKnownKeys $baselineKnownKeys `
        -MaxSnapshotIndex ([ref]$maxSnapshotIndex)
}

$allFiles = @(Get-VerilogTestFiles -RepoRoot $repoRoot -TestFilesRoot $TestFilesRoot)
$newFiles = @($allFiles | Where-Object { !$baselineKnownKeys.ContainsKey($_.Key) } | Sort-Object RelativePath)
$knownFiles = @($allFiles | Where-Object { $baselineKnownKeys.ContainsKey($_.Key) } | Sort-Object `
    @{ Expression = { Get-SnapshotIndexFromName -SnapshotFileName $snapshotNameByPath[$_.Key] } },
    @{ Expression = { $_.RelativePath } })

$nextSnapshotIndex = $maxSnapshotIndex + 1
foreach ($file in $newFiles) {
    if ($snapshotNameByPath.ContainsKey($file.Key)) {
        continue
    }

    do {
        $snapshotFileName = New-SnapshotFileName -SourcePath $file.FullName -Index $nextSnapshotIndex
        $nextSnapshotIndex++
    } while ($usedSnapshotNames.ContainsKey($snapshotFileName.ToLowerInvariant()))

    $snapshotNameByPath[$file.Key] = $snapshotFileName
    $usedSnapshotNames[$snapshotFileName.ToLowerInvariant()] = $true
}

# New files are opened first, but existing files keep their prior snapshot names.
$orderedFiles = @($newFiles + $knownFiles)
$fileEntries = @()
foreach ($file in $orderedFiles) {
    if ($NoStableSnapshotNames.IsPresent) {
        $fileEntries += $file.RelativePath
    }
    else {
        $fileEntries += [ordered]@{
            Path = $file.RelativePath
            SnapshotFileName = $snapshotNameByPath[$file.Key]
        }
    }
}

$manifest = [ordered]@{
    RunName = "all-testfiles"
    DelayMs = $DelayMs
    FreshInstancePerFile = $true
    Files = @($fileEntries)
}

Write-Utf8JsonFile -Path $resolvedManifestPath -Value $manifest

Write-Host "Generated manifest: $resolvedManifestPath"
Write-Host "  Files:          $($orderedFiles.Count)"
Write-Host "  New files:      $($newFiles.Count)"
Write-Host "  Existing files: $($knownFiles.Count)"
Write-Host "  Stable names:   $(!$NoStableSnapshotNames.IsPresent)"
