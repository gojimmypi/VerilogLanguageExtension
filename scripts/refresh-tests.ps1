# refreshes the all-testfiles manifest and approved baseline


# Make scripts fail faster and more predictably.
#
# Without strict mode, PowerShell may silently treat undefined variables as $null.
# With strict mode, that becomes an error. This helps catch typos and bad assumptions early.
#

Set-StrictMode -Version Latest

# Makes most non-terminating errors become terminating errors. For example,
# many PowerShell cmdlets normally print an error but keep going. With this setting,
# the script stops instead, so CI does not continue after a failed step:
$ErrorActionPreference = "Stop"

$scriptDir = if (![string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    $PSScriptRoot
}
else {
    Split-Path -Parent $MyInvocation.MyCommand.Path
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $scriptDir "..")).Path
Set-Location -LiteralPath $repoRoot

function Backup-Log {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    # Preserve the previous log before overwriting it.
    if (Test-Path -LiteralPath $Path) {
        Copy-Item -LiteralPath $Path -Destination "$Path.old" -Force
    }
}

# Run expectation-only CI first. This regenerates current snapshots and checks sanity/expectations.
Backup-Log (Join-Path $repoRoot "ci_pass.log")
& (Join-Path $scriptDir "ci-pass.ps1") *>&1 | Tee-Object -FilePath (Join-Path $repoRoot "ci_pass.log")

# Regenerate the approved all-testfiles baseline.
# The updated scripts should now write multi-line baseline JSON.
Backup-Log (Join-Path $repoRoot "ci_baseline.log")
& (Join-Path $scriptDir "ci-baseline.ps1") *>&1 | Tee-Object -FilePath (Join-Path $repoRoot "ci_baseline.log")

# Confirm the formatted baseline still matches a fresh current run.
Backup-Log (Join-Path $repoRoot "ci_check.log")
& (Join-Path $scriptDir "ci-check.ps1") *>&1 | Tee-Object -FilePath (Join-Path $repoRoot "ci_check.log")
