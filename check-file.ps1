

# Create a temporary one-file manifest.
# RunName is kept as all-testfiles so the generated snapshot content matches
# the normal baseline run name.
# DelayMs controls how long the extension waits before exporting after VS opens the file.
# FreshInstancePerFile is false because there is only one file.
@{
    RunName = "all-testfiles"
    DelayMs = 3000
    FreshInstancePerFile = $false
    Files = @(
        "TestFiles/testfile-5K.v"
    )
} |
    ConvertTo-Json -Depth 5 |
    Set-Content .\tools\vle-ci\manifests\single-testfile-5K.json -Encoding UTF8

# Export only the file listed in the temporary manifest.
# -Manifest points to the one-file manifest.
# -OutputDir writes to the normal current snapshot directory.
# -MaxWaitSeconds limits how long the script waits for the snapshot.
.\tools\vle-ci\Export-Snapshots.ps1 `
    -Manifest .\tools\vle-ci\manifests\single-testfile-5K.json `
    -OutputDir artifacts\snapshots\current `
    -MaxWaitSeconds 45


# Run snapshot sanity checks and any matching expectation files against the
# one generated current snapshot.
# This avoids comparing against the full 40-file baseline.
python .\tools\vle-ci\Compare-Snapshots.py `
    --current .\artifacts\snapshots\current `
    --expectations .\tools\vle-ci\expectations

$current = Get-ChildItem .\artifacts\snapshots\current\*-testfile-5K.v.snapshot.json |
    Select-Object -First 1

# Find the matching approved baseline snapshot from the full all-testfiles baseline.
$baseline = Get-ChildItem .\tests\snapshots\baselines\development-main\all-testfiles\*-testfile-5K.v.snapshot.json |
    Select-Object -First 1

# Compare the two JSON files with git diff --no-index.
# --no-index lets git compare arbitrary files, not only tracked files.
git diff --no-index -- $baseline.FullName $current.FullName

