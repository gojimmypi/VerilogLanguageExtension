# checks current output against the saved baseline

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

# Refresh the all-testfiles manifest first. Existing baseline snapshot names are
# preserved, and new files are placed first in the run order.
& (Join-Path $scriptDir "create-testfile-manifest.ps1")

& (Join-Path $repoRoot "tools\vle-ci\Run-LocalCI.ps1") `
    -Manifest tools\vle-ci\manifests\all-testfiles.json `
    -Baseline tests\snapshots\baselines\development-main\all-testfiles `
    -AllowNewSnapshots
