<#
.SYNOPSIS
    Checks that the approved snapshot baseline run-info matches source metadata.

.DESCRIPTION
    Compares tests/snapshots/.../run-info.json against the current VSIX manifest,
    assembly versions, ProvideMenuResource value, and the actual number of
    checked-in *.snapshot.json baseline files.
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
        Add-Failure -Failures $Failures -Message "run-info.json is missing $Name"
    }
    elseif ([string]$actual -ne [string]$Expected) {
        Add-Failure -Failures $Failures -Message "run-info.json $Name is '$actual', expected '$Expected'"
    }
    else {
        Write-Host "PASS: run-info.json $Name == $Expected"
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
$runInfo = Get-Content -Raw -Encoding UTF8 -Path $runInfoPath | ConvertFrom-Json
$failures = New-Object 'System.Collections.Generic.List[string]'

Assert-RunInfoEqual -Failures $failures -RunInfo $runInfo -Name "VsixManifestVersion" -Expected $info.VsixManifestVersion
Assert-RunInfoEqual -Failures $failures -RunInfo $runInfo -Name "AssemblyVersion" -Expected $info.AssemblyVersion
Assert-RunInfoEqual -Failures $failures -RunInfo $runInfo -Name "AssemblyFileVersion" -Expected $info.AssemblyFileVersion
Assert-RunInfoEqual -Failures $failures -RunInfo $runInfo -Name "AssemblyInformationalVersion" -Expected $info.AssemblyInformationalVersion
Assert-RunInfoEqual -Failures $failures -RunInfo $runInfo -Name "ProvideMenuResourceName" -Expected $info.ProvideMenuResourceName
Assert-RunInfoEqual -Failures $failures -RunInfo $runInfo -Name "ProvideMenuResourceVersion" -Expected $info.ProvideMenuResourceVersion

$snapshotCount = @(Get-ChildItem -Path $baselinePath -Filter "*.snapshot.json" -File -ErrorAction SilentlyContinue).Count
Assert-RunInfoEqual -Failures $failures -RunInfo $runInfo -Name "SnapshotCount" -Expected $snapshotCount

$processingTime = Get-ObjectPropertyValue -Object $runInfo -Name "ProcessingTime"
if ($null -eq $processingTime) {
    $processingTime = Get-ObjectPropertyValue -Object $runInfo -Name "ProcessingTimeBucket"
}
if ($null -ne $processingTime) {
    Write-Host "INFO: baseline snapshot processing time == $processingTime"
}
else {
    Write-Host "INFO: baseline snapshot processing time was not recorded"
}

$ciProcessingTime = Get-ObjectPropertyValue -Object $runInfo -Name "CiProcessingTime"
if ($null -ne $ciProcessingTime) {
    Write-Host "INFO: baseline CI processing time == $ciProcessingTime"
}

$runTiming = Get-ObjectPropertyValue -Object $runInfo -Name "RunTiming"
if ($null -ne $runTiming) {
    $runTimingCount = @($runTiming).Count
    Write-Host "INFO: baseline CI timing records == $runTimingCount"
}

$gitCommit = Get-ObjectPropertyValue -Object $runInfo -Name "GitCommit"
if ($null -ne $gitCommit) {
    Write-Host "INFO: baseline GitCommit == $gitCommit"
}

if ($failures.Count -gt 0) {
    throw "Snapshot baseline version check failed with $($failures.Count) issue(s). Regenerate and commit the approved baseline run-info.json."
}

Write-Host "Snapshot baseline version check passed."
