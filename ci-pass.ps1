# runs all-testfiles expectations only

# Refresh the all-testfiles manifest first. Existing baseline snapshot names are
# preserved, and new files are placed first in the run order.
.\create-testfile-manifest.ps1

.\tools\vle-ci\Run-LocalCI.ps1 `
    -Manifest tools\vle-ci\manifests\all-testfiles.json
