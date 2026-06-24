

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
Backup-Log .\ci_pass.log
.\ci-pass.ps1 *> .\ci_pass.log

# Regenerate the approved all-testfiles baseline.
# The updated scripts should now write multi-line baseline JSON.
Backup-Log .\ci_baseline.log
.\ci-baseline.ps1 *> .\ci_baseline.log

# Confirm the formatted baseline still matches a fresh current run.
Backup-Log .\ci_check.log
.\ci-check.ps1 *> .\ci_check.log

