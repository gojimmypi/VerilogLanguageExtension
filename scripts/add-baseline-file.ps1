# Adds one Verilog/SystemVerilog test file to the approved snapshot baseline.
# It does not run the full ci-baseline.ps1 refresh.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateNotNullOrEmpty()]
    [string]$SourceFile,

    [ValidateRange(1, 3600)]
    [int]$MaxWaitSeconds = 180,

    [switch]$UpdateExisting
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path (Join-Path $PSScriptRoot "..") "tools/vle-ci/SnapshotBaseline.ps1")

Assert-VleCanonicalPowerShell

function Get-RepoRoot {
    return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
}

function Get-RepoRelativePath {
    param(
        [string]$RepoRoot,
        [string]$Path
    )

    $rootPath = [System.IO.Path]::GetFullPath($RepoRoot)
    if (!$rootPath.EndsWith([System.IO.Path]::DirectorySeparatorChar.ToString())) {
        $rootPath += [System.IO.Path]::DirectorySeparatorChar
    }

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $relativeUri = (New-Object System.Uri($rootPath)).MakeRelativeUri(
        (New-Object System.Uri($fullPath)))
    $relativePath = [System.Uri]::UnescapeDataString($relativeUri.ToString())

    if ($relativePath -eq ".." -or $relativePath.StartsWith("../")) {
        throw "Path is outside the repository: $fullPath"
    }

    return $relativePath.Replace("\", "/")
}

function Get-NormalizedPath {
    param([string]$Path)

    return $Path.Replace("\", "/").ToLowerInvariant()
}

function Read-JsonFile {
    param([string]$Path)

    return ([System.IO.File]::ReadAllText(
            $Path,
            [System.Text.Encoding]::UTF8) | ConvertFrom-Json)
}

function Write-JsonFile {
    param(
        [string]$Path,
        [object]$Value
    )

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $text = $Value | ConvertTo-Json -Depth 100
    [System.IO.File]::WriteAllText(
        $Path,
        ($text + [Environment]::NewLine),
        $utf8NoBom)
}

function Get-SnapshotSourcePath {
    param(
        [object]$Snapshot,
        [string]$RepoRoot
    )

    $property = $Snapshot.PSObject.Properties["FileRelativePath"]
    if ($null -ne $property) {
        if (![string]::IsNullOrWhiteSpace([string]$property.Value)) {
            return [string]$property.Value
        }
    }

    $property = $Snapshot.PSObject.Properties["FilePath"]
    if ($null -ne $property -and $null -ne $property.Value) {
        $filePath = [string]$property.Value
        if ([System.IO.Path]::IsPathRooted($filePath)) {
            return Get-RepoRelativePath -RepoRoot $RepoRoot -Path $filePath
        }

        return $filePath
    }

    return ""
}

$repoRoot = Get-RepoRoot
Set-Location -LiteralPath $repoRoot
[System.IO.Directory]::SetCurrentDirectory($repoRoot)

$manifestRelative = "tools\vle-ci\manifests\all-testfiles.json"
$baselineDirRelative = "tests\snapshots\baselines\development-main\all-testfiles"
$outputDirRelative = "artifacts\snapshots\single-testfile"

$manifestPath = Join-Path $repoRoot $manifestRelative
$baselineDir = Join-Path $repoRoot $baselineDirRelative
$outputDir = Join-Path $repoRoot $outputDirRelative
$createManifestScript = Join-Path $PSScriptRoot "create-testfile-manifest.ps1"
$checkFileScript = Join-Path $PSScriptRoot "check-file.ps1"

$requestedPath = $SourceFile.Trim()
$normalizedRequestedPath = $requestedPath.Replace("\", "/")
while ($normalizedRequestedPath.StartsWith("./")) {
    $normalizedRequestedPath = $normalizedRequestedPath.Substring(2)
}

if ([System.IO.Path]::IsPathRooted($requestedPath)) {
    $sourcePath = [System.IO.Path]::GetFullPath($requestedPath)
}
elseif ($normalizedRequestedPath.StartsWith(
        "TestFiles/",
        [System.StringComparison]::OrdinalIgnoreCase)) {
    $sourcePath = [System.IO.Path]::GetFullPath(
        (Join-Path $repoRoot $normalizedRequestedPath))
}
else {
    $sourcePath = [System.IO.Path]::GetFullPath(
        (Join-Path (Join-Path $repoRoot "TestFiles") $normalizedRequestedPath))
}

if (!(Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "Test file not found: $sourcePath"
}

$extension = [System.IO.Path]::GetExtension($sourcePath).ToLowerInvariant()
if ($extension -notin @(".v", ".sv", ".svh", ".vh", ".verilog")) {
    throw "Unsupported test-file extension '$extension': $sourcePath"
}

$relativeSource = Get-RepoRelativePath -RepoRoot $repoRoot -Path $sourcePath
$normalizedSource = Get-NormalizedPath -Path $relativeSource
if (!$normalizedSource.StartsWith("testfiles/")) {
    throw "The source file must be under TestFiles: $sourcePath"
}

$manifestExisted = Test-Path -LiteralPath $manifestPath -PathType Leaf
$originalManifest = $null
$originalManifestJson = $null
if ($manifestExisted) {
    $originalManifest = [System.IO.File]::ReadAllBytes($manifestPath)
    $originalManifestJson = Read-JsonFile -Path $manifestPath
}

$baselinePath = ""
$baselineTempPath = ""
$manifestTempPath = "$manifestPath.$PID.tmp"
$baselineInstalled = $false
$originalBaseline = $null
$completed = $false

try {
    $operationName = if ($UpdateExisting.IsPresent) { "Updating" } else { "Adding" }
    Write-Host "$operationName baseline for $relativeSource"

    if ($UpdateExisting.IsPresent) {
        if (!$manifestExisted) {
            throw "Cannot update a baseline without the existing manifest: $manifestPath"
        }

        # An existing-file update must not regenerate or reformat the manifest.
        $manifest = Read-JsonFile -Path $manifestPath
    }
    else {
        # This updates the filename manifest only. It does not export all test files.
        $manifestArgs = @{
            ManifestPath = $manifestRelative
            BaselineDir = $baselineDirRelative
        }
        if ($null -ne $originalManifestJson) {
            if ($null -ne $originalManifestJson.PSObject.Properties["DelayMs"]) {
                $manifestArgs["DelayMs"] = [int]$originalManifestJson.DelayMs
            }
            if ($null -ne $originalManifestJson.PSObject.Properties["MaxWaitSeconds"]) {
                $manifestArgs["MaxWaitSeconds"] = [int]$originalManifestJson.MaxWaitSeconds
            }
        }

        & $createManifestScript @manifestArgs
        $manifest = Read-JsonFile -Path $manifestPath
    }
    $matches = @($manifest.Files | Where-Object {
            (Get-NormalizedPath -Path ([string]$_.Path)) -eq $normalizedSource
        })

    if ($matches.Count -ne 1) {
        throw "Expected one manifest entry for $relativeSource; found $($matches.Count)."
    }

    $entry = $matches[0]
    if ([string]::IsNullOrWhiteSpace([string]$entry.SnapshotFileName)) {
        throw "Manifest entry has no stable SnapshotFileName: $relativeSource"
    }
    $entryIsNew = [bool]$entry.IsNew
    if ($UpdateExisting.IsPresent -and $entryIsNew) {
        throw "File is still marked new; omit -UpdateExisting to add its first baseline: $relativeSource"
    }
    if (!$UpdateExisting.IsPresent -and !$entryIsNew) {
        throw "File already has an approved baseline. Use -UpdateExisting after reviewing the semantic diff: $relativeSource"
    }

    New-Item -ItemType Directory -Force -Path $baselineDir | Out-Null
    $baselinePath = Join-Path $baselineDir ([string]$entry.SnapshotFileName)
    $baselineTempPath = "$baselinePath.$PID.tmp"

    if ($UpdateExisting.IsPresent) {
        if (!(Test-Path -LiteralPath $baselinePath -PathType Leaf)) {
            throw "Approved baseline not found for update: $baselinePath"
        }
        $originalBaseline = [System.IO.File]::ReadAllBytes($baselinePath)
    }
    elseif (Test-Path -LiteralPath $baselinePath) {
        throw "Baseline already exists; refusing to overwrite it: $baselinePath"
    }

    foreach ($candidate in @(Get-ChildItem `
            -LiteralPath $baselineDir `
            -Filter "*.snapshot.json" `
            -File `
            -ErrorAction SilentlyContinue)) {
        $candidateSnapshot = Read-JsonFile -Path $candidate.FullName
        $candidateSource = Get-SnapshotSourcePath -Snapshot $candidateSnapshot -RepoRoot $repoRoot
        if ((Get-NormalizedPath -Path $candidateSource) -eq $normalizedSource -and
                ![string]::Equals($candidate.FullName, $baselinePath, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "A second baseline for this source already exists: $($candidate.FullName)"
        }
    }

    # check-file.ps1 recreates the single-testfile output and opens only this file.
    $checkArgs = @{
        SourceFile = $relativeSource
        MaxWaitSeconds = $MaxWaitSeconds
        CloseVisualStudioWhenDone = $true
    }
    if ($UpdateExisting.IsPresent) {
        $checkArgs["AllowBaselineDifference"] = $true
    }

    & $checkFileScript @checkArgs

    $currentSnapshots = @(Get-ChildItem `
            -LiteralPath $outputDir `
            -Filter "*.snapshot.json" `
            -File `
            -ErrorAction SilentlyContinue)

    if ($currentSnapshots.Count -ne 1) {
        throw "Expected one generated snapshot; found $($currentSnapshots.Count)."
    }

    $currentSnapshot = Read-JsonFile -Path $currentSnapshots[0].FullName
    $currentSource = Get-SnapshotSourcePath -Snapshot $currentSnapshot -RepoRoot $repoRoot
    if ((Get-NormalizedPath -Path $currentSource) -ne $normalizedSource) {
        throw "Generated snapshot is for '$currentSource', not '$relativeSource'."
    }

    # Use the same portability transform and canonical serializer as the full
    # ci-baseline.ps1 flow. This prevents single-file updates from introducing
    # absolute paths, volatile or release-only fields, LF-only JSON, or
    # Python-style spacing.
    $portableSnapshot = ConvertTo-VlePortableSnapshot -Snapshot $currentSnapshot
    Write-VleJsonFile -Path $baselineTempPath -Value $portableSnapshot

    $writtenSnapshot = Read-VleJsonFile -Path $baselineTempPath
    $expectedJson = $portableSnapshot | ConvertTo-Json -Depth 100
    $writtenJson = $writtenSnapshot | ConvertTo-Json -Depth 100
    if ($expectedJson -cne $writtenJson) {
        throw "Staged baseline verification failed."
    }

    # Accept only the requested new entry. Existing-file updates leave the
    # manifest byte-for-byte unchanged.
    if ($entryIsNew) {
        $entry.IsNew = $false
        Write-JsonFile -Path $manifestTempPath -Value $manifest
    }

    Move-Item -LiteralPath $baselineTempPath -Destination $baselinePath -Force
    $baselineInstalled = $true
    if ($entryIsNew) {
        Move-Item -LiteralPath $manifestTempPath -Destination $manifestPath -Force
    }

    $installedManifest = Read-JsonFile -Path $manifestPath
    $installedMatches = @($installedManifest.Files | Where-Object {
            (Get-NormalizedPath -Path ([string]$_.Path)) -eq $normalizedSource
        })
    if ($installedMatches.Count -ne 1 -or [bool]$installedMatches[0].IsNew) {
        throw "Installed manifest verification failed for $relativeSource."
    }
    if (!(Test-Path -LiteralPath $baselinePath -PathType Leaf)) {
        throw "Installed baseline verification failed: $baselinePath"
    }

    $completed = $true
}
finally {
    if (![string]::IsNullOrWhiteSpace($baselineTempPath)) {
        Remove-Item -LiteralPath $baselineTempPath -Force -ErrorAction SilentlyContinue
    }
    Remove-Item -LiteralPath $manifestTempPath -Force -ErrorAction SilentlyContinue

    if (!$completed) {
        if ($baselineInstalled -and (Test-Path -LiteralPath $baselinePath)) {
            if ($null -ne $originalBaseline) {
                [System.IO.File]::WriteAllBytes($baselinePath, $originalBaseline)
            }
            else {
                Remove-Item -LiteralPath $baselinePath -Force
            }
        }

        if ($manifestExisted) {
            [System.IO.File]::WriteAllBytes($manifestPath, $originalManifest)
        }
        else {
            Remove-Item -LiteralPath $manifestPath -Force -ErrorAction SilentlyContinue
        }
    }
}

Write-Host ""
$completedVerb = if ($UpdateExisting.IsPresent) { "updated" } else { "added" }
Write-Host "Baseline $completedVerb successfully:"
Write-Host "  Source:   $relativeSource"
Write-Host "  Snapshot: $baselineDirRelative\$($entry.SnapshotFileName)"
Write-Host "  Full baseline refresh: not run"
Write-Host ""

if ($null -ne (Get-Command git -ErrorAction SilentlyContinue)) {
    & git -C $repoRoot status --short -- `
        $relativeSource `
        $manifestRelative `
        (Join-Path $baselineDirRelative ([string]$entry.SnapshotFileName))
    $global:LASTEXITCODE = 0
}
