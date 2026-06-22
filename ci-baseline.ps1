# updates the all-testfiles baseline

.\tools\vle-ci\Run-LocalCI.ps1 `
    -Manifest tools\vle-ci\manifests\all-testfiles.json `
    -Baseline tests\snapshots\baselines\development-main\all-testfiles `
    -UpdateBaseline
