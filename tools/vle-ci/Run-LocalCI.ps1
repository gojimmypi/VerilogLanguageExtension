<#
.SYNOPSIS
    Local CI/CD entry point for VerilogLanguageExtension.

.DESCRIPTION
    Builds the extension, exports editor semantic snapshots through the VS
    Experimental Instance, and compares them against expectations and optionally
    an approved baseline.
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Debug",
    [string]$Manifest = "tools/vle-ci/manifests/cold-open.json",
    [string]$Baseline = "",
    [switch]$UpdateBaseline,
    [switch]$AllowNewSnapshots,
    [switch]$SkipBuild,
    [switch]$SkipSnapshots,
    [string]$RootSuffix = "Exp"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    $scriptDir = Split-Path -Parent $PSCommandPath
    return (Resolve-Path (Join-Path $scriptDir "../..")).Path
}

function Get-VsWherePath {
    $programFilesX86 = ${env:ProgramFiles(x86)}
    if ([string]::IsNullOrWhiteSpace($programFilesX86)) {
        return $null
    }

    $candidate = Join-Path $programFilesX86 "Microsoft Visual Studio/Installer/vswhere.exe"
    if (Test-Path $candidate) {
        return $candidate
    }

    return $null
}

function Get-MSBuildPath {
    $vswhere = Get-VsWherePath
    if ($null -ne $vswhere) {
        $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild/**/Bin/MSBuild.exe" | Select-Object -First 1
        if (![string]::IsNullOrWhiteSpace($msbuild) -and (Test-Path $msbuild)) {
            return $msbuild
        }
    }

    $fallback = "C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
    if (Test-Path $fallback) {
        return $fallback
    }

    throw "Could not find MSBuild.exe. Run from a Visual Studio Developer PowerShell or install VS Build Tools."
}

function Format-JsonFile {
    param([string]$Path)

    if (!(Test-Path $Path)) {
        return
    }

    try {
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        $rawJson = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
        $json = $rawJson | ConvertFrom-Json
        $text = $json | ConvertTo-Json -Depth 100
        [System.IO.File]::WriteAllText($Path, ($text + [Environment]::NewLine), $utf8NoBom)
    }
    catch {
        Write-Warning "Could not format JSON $Path`: $_"
    }
}

function Format-GeneratedJsonFiles {
    param([string]$Directory)

    if (!(Test-Path $Directory)) {
        return
    }

    foreach ($jsonFile in @(Get-ChildItem -Path $Directory -Filter "*.json" -File -Recurse -ErrorAction SilentlyContinue)) {
        Format-JsonFile -Path $jsonFile.FullName
    }
}

function Resolve-LocalCiPath {
    param(
        [string]$RepoRoot,
        [string]$Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $Path))
}

function Assert-UpdateBaselinePath {
    param(
        [string]$RepoRoot,
        [string]$BaselinePath
    )

    $allowedRoot = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot "tests/snapshots/baselines"))
    $candidate = [System.IO.Path]::GetFullPath($BaselinePath)
    $comparison = [System.StringComparison]::OrdinalIgnoreCase

    $trimChars = @([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $allowedRootTrimmed = $allowedRoot.TrimEnd($trimChars)
    $candidateTrimmed = $candidate.TrimEnd($trimChars)
    if ($candidateTrimmed -eq $allowedRootTrimmed) {
        throw "-UpdateBaseline cannot replace the baseline root directory: $allowedRoot"
    }

    $allowedPrefix = $allowedRootTrimmed + [System.IO.Path]::DirectorySeparatorChar
    if (!$candidate.StartsWith($allowedPrefix, $comparison)) {
        throw "-UpdateBaseline may only update approved baselines under $allowedRoot. Refusing to update: $candidate"
    }
}

function Get-ElapsedTimeBucket {
    param([double]$Seconds)

    if ($Seconds -lt 10) {
        return "0-9s"
    }
    if ($Seconds -lt 30) {
        return "10-29s"
    }
    if ($Seconds -lt 60) {
        return "30-59s"
    }
    if ($Seconds -lt 120) {
        return "1-2m"
    }
    if ($Seconds -lt 300) {
        return "2-5m"
    }
    if ($Seconds -lt 600) {
        return "5-10m"
    }
    if ($Seconds -lt 1800) {
        return "10-30m"
    }

    return "30m+"
}

function Add-CiTimingRecord {
    param(
        [string]$Name,
        [System.Diagnostics.Stopwatch]$Stopwatch,
        [string]$Status = "Completed"
    )

    if ($null -eq $Stopwatch) {
        return
    }

    if ($Stopwatch.IsRunning) {
        $Stopwatch.Stop()
    }

    $elapsedSeconds = [Math]::Round($Stopwatch.Elapsed.TotalSeconds, 3)
    $elapsedBucket = Get-ElapsedTimeBucket -Seconds $Stopwatch.Elapsed.TotalSeconds
    $record = [pscustomobject][ordered]@{
        Name = $Name
        Status = $Status
        ElapsedSeconds = $elapsedSeconds
        ElapsedBucket = $elapsedBucket
    }

    [void]$script:ciTimingRecords.Add($record)
    Write-Host ("Timing: CI {0}: {1:N3}s ({2}, {3})" -f $Name, $elapsedSeconds, $elapsedBucket, $Status)
    return $record
}

function Add-NoteProperty {
    param(
        [object]$Object,
        [string]$Name,
        [object]$Value
    )

    $Object | Add-Member -MemberType NoteProperty -Name $Name -Value $Value -Force
}

function Add-RunInfoVersionMetadata {
    param(
        [string]$RepoRoot,
        [string]$SnapshotDirectory
    )

    $runInfoPath = Join-Path $SnapshotDirectory "run-info.json"
    if (!(Test-Path $runInfoPath)) {
        Write-Warning "Snapshot run-info.json not found; release metadata was not recorded: $runInfoPath"
        return
    }

    $versionInfoScript = Join-Path $RepoRoot "tools/vle-ci/Get-VleVersionInfo.ps1"
    if (!(Test-Path $versionInfoScript)) {
        Write-Warning "Get-VleVersionInfo.ps1 not found; release metadata was not recorded."
        return
    }

    $runInfo = Get-Content -Raw -Encoding UTF8 -Path $runInfoPath | ConvertFrom-Json
    $versionInfo = & $versionInfoScript -RepoRoot $RepoRoot

    Add-NoteProperty -Object $runInfo -Name "VsixManifestVersion" -Value $versionInfo.VsixManifestVersion
    Add-NoteProperty -Object $runInfo -Name "AssemblyVersion" -Value $versionInfo.AssemblyVersion
    Add-NoteProperty -Object $runInfo -Name "AssemblyFileVersion" -Value $versionInfo.AssemblyFileVersion
    Add-NoteProperty -Object $runInfo -Name "AssemblyInformationalVersion" -Value $versionInfo.AssemblyInformationalVersion
    Add-NoteProperty -Object $runInfo -Name "ProvideMenuResourceName" -Value $versionInfo.ProvideMenuResourceName
    Add-NoteProperty -Object $runInfo -Name "ProvideMenuResourceVersion" -Value $versionInfo.ProvideMenuResourceVersion

    $snapshotCount = @(Get-ChildItem -Path $SnapshotDirectory -Filter "*.snapshot.json" -File -ErrorAction SilentlyContinue).Count
    Add-NoteProperty -Object $runInfo -Name "SnapshotCount" -Value $snapshotCount

    try {
        $gitCommit = (& git -C $RepoRoot rev-parse --short HEAD 2>$null).Trim()
        $gitCommitFull = (& git -C $RepoRoot rev-parse HEAD 2>$null).Trim()
        if (![string]::IsNullOrWhiteSpace($gitCommit)) {
            Add-NoteProperty -Object $runInfo -Name "GitCommit" -Value $gitCommit
        }
        if (![string]::IsNullOrWhiteSpace($gitCommitFull)) {
            Add-NoteProperty -Object $runInfo -Name "GitCommitFull" -Value $gitCommitFull
        }
    }
    catch {
        Write-Warning "Could not record Git commit in run-info.json: $_"
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $text = $runInfo | ConvertTo-Json -Depth 100
    [System.IO.File]::WriteAllText($runInfoPath, ($text + [Environment]::NewLine), $utf8NoBom)
}

function Add-RunInfoCiTimingMetadata {
    param([string]$SnapshotDirectory)

    $runInfoPath = Join-Path $SnapshotDirectory "run-info.json"
    if (!(Test-Path $runInfoPath)) {
        return
    }

    $runInfo = Get-Content -Raw -Encoding UTF8 -Path $runInfoPath | ConvertFrom-Json
    $timingRecords = @($script:ciTimingRecords.ToArray())
    Add-NoteProperty -Object $runInfo -Name "RunTiming" -Value $timingRecords

    $totalRecord = @($timingRecords | Where-Object { $_.Name -eq "Total" } | Select-Object -Last 1)
    if ($totalRecord.Count -gt 0) {
        Add-NoteProperty -Object $runInfo -Name "CiProcessingTime" -Value $totalRecord[0].ElapsedBucket
        Add-NoteProperty -Object $runInfo -Name "CiElapsedSeconds" -Value $totalRecord[0].ElapsedSeconds
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $text = $runInfo | ConvertTo-Json -Depth 100
    [System.IO.File]::WriteAllText($runInfoPath, ($text + [Environment]::NewLine), $utf8NoBom)
}

$script:ciTimingRecords = New-Object 'System.Collections.Generic.List[object]'
$ciTotalStopwatch = [System.Diagnostics.Stopwatch]::StartNew()

$repoRoot = Get-RepoRoot
$solution = Join-Path $repoRoot "VerilogLanguageExtension.sln"
$currentSnapshots = Join-Path $repoRoot "artifacts/snapshots/current"
$expectations = Join-Path $repoRoot "tools/vle-ci/expectations"
$compareTool = Join-Path $repoRoot "tools/vle-ci/Compare-Snapshots.py"
$baselinePath = ""

if (!$SkipBuild) {
    $buildStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $msbuild = Get-MSBuildPath
        Write-Host "MSBuild: $msbuild"

        # Build the solution explicitly because this repo has both solution and project files.
        & $msbuild $solution /restore /m /p:Configuration=$Configuration
        if ($LASTEXITCODE -ne 0) {
            throw "MSBuild failed with exit code $LASTEXITCODE"
        }

        Add-CiTimingRecord -Name "Build" -Stopwatch $buildStopwatch | Out-Null
    }
    catch {
        Add-CiTimingRecord -Name "Build" -Stopwatch $buildStopwatch -Status "Failed" | Out-Null
        throw
    }
}
else {
    Write-Host "Skipping build."
}

if (!$SkipSnapshots) {
    $snapshotStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        # Export snapshots from the selected manifest into the standard current artifact directory.
        & (Join-Path $repoRoot "tools/vle-ci/Export-Snapshots.ps1") `
            -Manifest $Manifest `
            -OutputDir "artifacts/snapshots/current" `
            -RootSuffix $RootSuffix

        Add-CiTimingRecord -Name "Snapshot export" -Stopwatch $snapshotStopwatch | Out-Null
    }
    catch {
        Add-CiTimingRecord -Name "Snapshot export" -Stopwatch $snapshotStopwatch -Status "Failed" | Out-Null
        Add-RunInfoCiTimingMetadata -SnapshotDirectory $currentSnapshots
        throw
    }

    Add-RunInfoVersionMetadata -RepoRoot $repoRoot -SnapshotDirectory $currentSnapshots

    $formatCurrentStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        Format-GeneratedJsonFiles -Directory $currentSnapshots
        Add-CiTimingRecord -Name "Format current JSON" -Stopwatch $formatCurrentStopwatch | Out-Null
    }
    catch {
        Add-CiTimingRecord -Name "Format current JSON" -Stopwatch $formatCurrentStopwatch -Status "Failed" | Out-Null
        throw
    }
}
else {
    Write-Host "Skipping snapshot export."
}

$python = "python"
$compareArgs = @("$compareTool", "--current", "$currentSnapshots", "--expectations", "$expectations")

if (![string]::IsNullOrWhiteSpace($Baseline)) {
    $baselinePath = Resolve-LocalCiPath -RepoRoot $repoRoot -Path $Baseline
    if ($UpdateBaseline) {
        Assert-UpdateBaselinePath -RepoRoot $repoRoot -BaselinePath $baselinePath
    }

    $compareArgs += @("--baseline", "$baselinePath")
    if ($UpdateBaseline) {
        $compareArgs += "--update-baseline"
    }
    if ($AllowNewSnapshots) {
        $compareArgs += "--allow-new-snapshots"
    }
}
elseif ($UpdateBaseline) {
    throw "-UpdateBaseline requires -Baseline"
}
elseif ($AllowNewSnapshots) {
    throw "-AllowNewSnapshots requires -Baseline"
}

$compareStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
try {
    # Compare current snapshots against expectations and, when requested, the approved baseline.
    & $python @compareArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Snapshot comparison failed with exit code $LASTEXITCODE"
    }

    Add-CiTimingRecord -Name "Snapshot compare" -Stopwatch $compareStopwatch | Out-Null
}
catch {
    Add-CiTimingRecord -Name "Snapshot compare" -Stopwatch $compareStopwatch -Status "Failed" | Out-Null
    if (!$SkipSnapshots) {
        Add-RunInfoCiTimingMetadata -SnapshotDirectory $currentSnapshots
    }
    throw
}

if ($UpdateBaseline -and ![string]::IsNullOrWhiteSpace($baselinePath)) {
    Add-RunInfoVersionMetadata -RepoRoot $repoRoot -SnapshotDirectory $baselinePath

    $formatBaselineStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        Format-GeneratedJsonFiles -Directory $baselinePath
        Add-CiTimingRecord -Name "Format baseline JSON" -Stopwatch $formatBaselineStopwatch | Out-Null
    }
    catch {
        Add-CiTimingRecord -Name "Format baseline JSON" -Stopwatch $formatBaselineStopwatch -Status "Failed" | Out-Null
        throw
    }
}

Add-CiTimingRecord -Name "Total" -Stopwatch $ciTotalStopwatch | Out-Null

if (!$SkipSnapshots) {
    Add-RunInfoCiTimingMetadata -SnapshotDirectory $currentSnapshots
    Format-JsonFile -Path (Join-Path $currentSnapshots "run-info.json")

    if ($UpdateBaseline -and ![string]::IsNullOrWhiteSpace($baselinePath)) {
        Add-RunInfoCiTimingMetadata -SnapshotDirectory $baselinePath
        Format-JsonFile -Path (Join-Path $baselinePath "run-info.json")
    }
}

Write-Host "Local CI completed successfully."
