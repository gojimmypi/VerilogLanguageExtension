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

function Remove-SnapshotGitCommit {
    param([object]$Json)

    if ($null -eq $Json) {
        return
    }

    $gitCommitProperty = $Json.PSObject.Properties["GitCommit"]
    if ($null -ne $gitCommitProperty) {
        $Json.PSObject.Properties.Remove("GitCommit")
    }
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

        if ((Split-Path $Path -Leaf) -like "*.snapshot.json") {
            Remove-SnapshotGitCommit -Json $json
        }

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
    $record = [ordered]@{
        Name = $Name
        Status = $Status
        ElapsedSeconds = $elapsedSeconds
        Elapsed = $Stopwatch.Elapsed.ToString("c")
    }

    [void]$script:ciTimingRecords.Add([pscustomobject]$record)
    Write-Host ("Timing: {0}: {1:N3}s ({2})" -f $Name, $elapsedSeconds, $Status)
}

function Invoke-TimedCiStep {
    param(
        [string]$Name,
        [scriptblock]$Operation
    )

    $stepStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        & $Operation
        Add-CiTimingRecord -Name $Name -Stopwatch $stepStopwatch -Status "Completed"
    }
    catch {
        Add-CiTimingRecord -Name $Name -Stopwatch $stepStopwatch -Status "Failed"
        throw
    }
}

function Write-CiTimingArtifact {
    param(
        [string]$RepoRoot,
        [string]$Status
    )

    if ($script:ciStopwatch.IsRunning) {
        $script:ciStopwatch.Stop()
    }

    $completedAt = Get-Date
    $artifactDir = Join-Path $RepoRoot "artifacts/ci"
    New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

    $artifactPath = Join-Path $artifactDir "run-timing.json"
    $timing = [ordered]@{
        SchemaVersion = 1
        Status = $Status
        StartedAt = $script:ciStartedAt.ToString("o")
        CompletedAt = $completedAt.ToString("o")
        StartedAtUtc = $script:ciStartedAt.ToUniversalTime().ToString("o")
        CompletedAtUtc = $completedAt.ToUniversalTime().ToString("o")
        TotalSeconds = [Math]::Round($script:ciStopwatch.Elapsed.TotalSeconds, 3)
        Total = $script:ciStopwatch.Elapsed.ToString("c")
        Steps = @($script:ciTimingRecords.ToArray())
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $text = $timing | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText($artifactPath, ($text + [Environment]::NewLine), $utf8NoBom)
    Write-Host ("CI timing artifact: {0}" -f $artifactPath)
    Write-Host ("Timing: total: {0:N3}s ({1})" -f $timing.TotalSeconds, $Status)
}

$repoRoot = Get-RepoRoot
$solution = Join-Path $repoRoot "VerilogLanguageExtension.sln"
$currentSnapshots = Join-Path $repoRoot "artifacts/snapshots/current"
$expectations = Join-Path $repoRoot "tools/vle-ci/expectations"
$compareTool = Join-Path $repoRoot "tools/vle-ci/Compare-Snapshots.py"
$ciStartedAt = Get-Date
$ciStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$ciTimingRecords = New-Object System.Collections.Generic.List[object]
$ciStatus = "Failed"

try {
    if (!$SkipBuild) {
        Invoke-TimedCiStep -Name "Build" -Operation {
            $msbuild = Get-MSBuildPath
            Write-Host "MSBuild: $msbuild"
            & $msbuild $solution /restore /m /p:Configuration=$Configuration
            if ($LASTEXITCODE -ne 0) {
                throw "MSBuild failed with exit code $LASTEXITCODE"
            }
        }
    }

    if (!$SkipSnapshots) {
        Invoke-TimedCiStep -Name "Snapshot export" -Operation {
            & (Join-Path $repoRoot "tools/vle-ci/Export-Snapshots.ps1") -Manifest $Manifest -OutputDir "artifacts/snapshots/current" -RootSuffix $RootSuffix
            Format-GeneratedJsonFiles -Directory $currentSnapshots
        }
    }

    $python = "python"
    $compareArgs = @("$compareTool", "--current", "$currentSnapshots", "--expectations", "$expectations")
    $baselinePath = ""

    if (![string]::IsNullOrWhiteSpace($Baseline)) {
        $baselinePath = Join-Path $repoRoot $Baseline
        $compareArgs += @("--baseline", "$baselinePath")
        if ($UpdateBaseline) {
            $compareArgs += "--update-baseline"
        }
    }
    elseif ($UpdateBaseline) {
        throw "-UpdateBaseline requires -Baseline"
    }

    if ($AllowNewSnapshots) {
        $compareArgs += "--allow-new-snapshots"
    }

    Invoke-TimedCiStep -Name "Snapshot comparison" -Operation {
        & $python @compareArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Snapshot comparison failed with exit code $LASTEXITCODE"
        }
    }

    if ($UpdateBaseline -and ![string]::IsNullOrWhiteSpace($baselinePath)) {
        Invoke-TimedCiStep -Name "Baseline formatting" -Operation {
            Format-GeneratedJsonFiles -Directory $baselinePath
        }
    }

    $ciStatus = "Completed"
    Write-Host "Local CI completed successfully."
}
finally {
    try {
        Write-CiTimingArtifact -RepoRoot $repoRoot -Status $ciStatus
    }
    catch {
        Write-Warning "Could not write CI timing artifact: $_"
    }
}
