<#
.SYNOPSIS
    Checks that the approved snapshot baseline run-info matches source metadata.

.DESCRIPTION
    Compares tests/snapshots/.../run-info.json against the current VSIX manifest,
    assembly versions, ProvideMenuResource value, and the actual number of
    checked-in *.snapshot.json baseline files. It also verifies that approved
    run-info contains only stable metadata rather than timestamps, timings, or
    Git identifiers from the machine that generated it.
#>

[CmdletBinding()]
param(
    [string]$RepoRoot = "",
    [string]$BaselineDir = "tests/snapshots/baselines/development-main/all-testfiles"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-DefaultRepoRoot {
    $scriptDir = Split-Path -Parent $PSCommandPath
    return (Resolve-Path (Join-Path $scriptDir "../..")).Path
}

function Add-Failure {
    param(
        [System.Collections.Generic.List[string]]$Failures,
        [string]$Message
    )

    $Failures.Add($Message) | Out-Null
    Write-Host "FAIL: $Message"
}

function Get-ObjectPropertyValue {
    param(
        [object]$Object,
        [string]$Name
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Assert-RunInfoEqual {
    param(
        [System.Collections.Generic.List[string]]$Failures,
        [object]$RunInfo,
        [string]$Name,
        [object]$Expected
    )

    $actual = Get-ObjectPropertyValue -Object $RunInfo -Name $Name
    if ($null -eq $actual) {
        Add-Failure `
            -Failures $Failures `
            -Message "run-info.json is missing $Name"
    }
    elseif ([string]$actual -ne [string]$Expected) {
        Add-Failure `
            -Failures $Failures `
            -Message "run-info.json $Name is '$actual', expected '$Expected'"
    }
    else {
        Write-Host "PASS: run-info.json $Name == $Expected"
    }
}

function Assert-RunInfoPropertyAbsent {
    param(
        [System.Collections.Generic.List[string]]$Failures,
        [object]$RunInfo,
        [string]$Name
    )

    if ($null -ne $RunInfo.PSObject.Properties[$Name]) {
        Add-Failure `
            -Failures $Failures `
            -Message "run-info.json contains volatile field $Name"
    }
    else {
        Write-Host "PASS: run-info.json omits volatile field $Name"
    }
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Get-DefaultRepoRoot
}
else {
    $RepoRoot = (Resolve-Path $RepoRoot).Path
}

$getInfoScript = Join-Path $PSScriptRoot "Get-VleVersionInfo.ps1"
if (!(Test-Path $getInfoScript)) {
    throw "Required script not found: $getInfoScript"
}

$baselinePath = Join-Path $RepoRoot $BaselineDir
$runInfoPath = Join-Path $baselinePath "run-info.json"
if (!(Test-Path $runInfoPath)) {
    throw "Baseline run-info.json not found: $runInfoPath"
}

$info = & $getInfoScript -RepoRoot $RepoRoot
$runInfo = Get-Content -Raw -Encoding UTF8 -Path $runInfoPath |
    ConvertFrom-Json
$failures = New-Object 'System.Collections.Generic.List[string]'

Assert-RunInfoEqual `
    -Failures $failures `
    -RunInfo $runInfo `
    -Name "Status" `
    -Expected "Completed"
Assert-RunInfoEqual `
    -Failures $failures `
    -RunInfo $runInfo `
    -Name "VsixManifestVersion" `
    -Expected $info.VsixManifestVersion
Assert-RunInfoEqual `
    -Failures $failures `
    -RunInfo $runInfo `
    -Name "AssemblyVersion" `
    -Expected $info.AssemblyVersion
Assert-RunInfoEqual `
    -Failures $failures `
    -RunInfo $runInfo `
    -Name "AssemblyFileVersion" `
    -Expected $info.AssemblyFileVersion
Assert-RunInfoEqual `
    -Failures $failures `
    -RunInfo $runInfo `
    -Name "AssemblyInformationalVersion" `
    -Expected $info.AssemblyInformationalVersion
Assert-RunInfoEqual `
    -Failures $failures `
    -RunInfo $runInfo `
    -Name "ProvideMenuResourceName" `
    -Expected $info.ProvideMenuResourceName
Assert-RunInfoEqual `
    -Failures $failures `
    -RunInfo $runInfo `
    -Name "ProvideMenuResourceVersion" `
    -Expected $info.ProvideMenuResourceVersion

$snapshotCount = @(Get-ChildItem `
        -Path $baselinePath `
        -Filter "*.snapshot.json" `
        -File `
        -ErrorAction SilentlyContinue).Count
foreach ($name in @(
        "ExpectedSnapshots",
        "ActualSnapshots",
        "SnapshotCount")) {
    Assert-RunInfoEqual `
        -Failures $failures `
        -RunInfo $runInfo `
        -Name $name `
        -Expected $snapshotCount
}

foreach ($name in @(
        "StartedAt",
        "CompletedAt",
        "ElapsedSeconds",
        "Elapsed",
        "Timings",
        "ProcessingTime",
        "ProcessingTimeBucket",
        "RunTiming",
        "CiProcessingTime",
        "CiElapsedSeconds",
        "GitCommit",
        "GitCommitFull")) {
    Assert-RunInfoPropertyAbsent `
        -Failures $failures `
        -RunInfo $runInfo `
        -Name $name
}

if ($failures.Count -gt 0) {
    throw (
        "Snapshot baseline version check failed with $($failures.Count) " +
        "issue(s). Regenerate and commit the approved baseline run-info.json.")
}

Write-Host "Snapshot baseline version check passed."
