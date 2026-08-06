# updates the all-testfiles baseline

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

$baselineDir = "tests\snapshots\baselines\development-main\all-testfiles"

# Verify Windows PowerShell 5.1 serialization, stable metadata, portable paths,
# repeatability, and interrupted-update recovery before replacing a baseline.
& (Join-Path $repoRoot "tools\vle-ci\Test-SnapshotBaselineWriter.ps1")

# Refresh the all-testfiles manifest first. Existing baseline snapshot names are
# preserved, and new files are placed first in the run order.
& (Join-Path $scriptDir "create-testfile-manifest.ps1") -BaselineDir $baselineDir

# Run the baseline update for the all-testfiles manifest. Compare-Snapshots.py
# validates snapshot sanity and expectations, then delegates the actual write to
# tools\vle-ci\Write-SnapshotBaseline.ps1. That single writer preserves the
# historical Windows PowerShell 5.1 JSON layout, CRLF, and UTF-8 without BOM.
# It excludes per-snapshot release versions and volatile run timing metadata.
& (Join-Path $repoRoot "tools\vle-ci\Run-LocalCI.ps1") `
    -Manifest tools\vle-ci\manifests\all-testfiles.json `
    -Baseline $baselineDir `
    -UpdateBaseline

# The baseline refresh completed successfully. Clear the temporary "new file"
# priority markers so the next run treats these files as normal baseline files.
& (Join-Path $scriptDir "create-testfile-manifest.ps1") `
    -BaselineDir $baselineDir `
    -AcceptCurrentManifest
