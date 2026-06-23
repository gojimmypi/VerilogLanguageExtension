# Run expectation-only CI first. This regenerates current snapshots and checks sanity/expectations.
.\ci-pass.ps1

# Regenerate the approved all-testfiles baseline.
# The updated scripts should now write multi-line baseline JSON.
.\ci-baseline.ps1

# Confirm the formatted baseline still matches a fresh current run.
.\ci-check.ps1
