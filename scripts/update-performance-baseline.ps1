# Updates the approved all-testfiles performance baseline from the latest
# completed artifacts\snapshots\current\run-info.json.

[CmdletBinding()]
param(
    [string]$CurrentRunInfo = "artifacts\snapshots\current\run-info.json",
    [string]$Destination = "tests\snapshots\performance-baselines\development-main\all-testfiles.performance.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = if (![string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    $PSScriptRoot
}
else {
    Split-Path -Parent $MyInvocation.MyCommand.Path
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $scriptDir "..")).Path
Set-Location -LiteralPath $repoRoot

function Resolve-RepositoryPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

$performanceHelpers = Join-Path $repoRoot "tools\vle-ci\PerformanceBaseline.ps1"
if (!(Test-Path -LiteralPath $performanceHelpers -PathType Leaf)) {
    throw "Performance baseline helpers not found: $performanceHelpers"
}

. $performanceHelpers

$currentRunInfoPath = Resolve-RepositoryPath -Path $CurrentRunInfo
$destinationPath = Resolve-RepositoryPath -Path $Destination

$allowedRoot = [System.IO.Path]::GetFullPath((Join-Path `
    $repoRoot `
    "tests\snapshots\performance-baselines"))
$trimChars = @(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar)
$allowedRootWithSeparator = $allowedRoot.TrimEnd($trimChars) +
    [System.IO.Path]::DirectorySeparatorChar
if (!$destinationPath.StartsWith(
        $allowedRootWithSeparator,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw (
        "Performance baseline destination must be below " +
        "$allowedRootWithSeparator. Requested: $destinationPath")
}

Write-VlePerformanceBaseline `
    -CurrentRunInfoPath $currentRunInfoPath `
    -DestinationPath $destinationPath
