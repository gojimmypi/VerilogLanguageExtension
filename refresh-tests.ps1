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
.\ci-pass.ps1 *>&1 | Tee-Object -FilePath .\ci_pass.log

# Regenerate the approved all-testfiles baseline.
# The updated scripts should now write multi-line baseline JSON.
Backup-Log .\ci_baseline.log
.\ci-baseline.ps1 *>&1 | Tee-Object -FilePath .\ci_baseline.log

# Confirm the formatted baseline still matches a fresh current run.
Backup-Log .\ci_check.log
.\ci-check.ps1 *>&1 | Tee-Object -FilePath .\ci_check.log
