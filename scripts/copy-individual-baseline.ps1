
$currentSnapshots = @(
    Get-ChildItem `
        -LiteralPath ".\artifacts\snapshots\single-testfile" `
        -Filter "*.snapshot.json" `
        -File
)

if ($currentSnapshots.Count -ne 1) {
    throw "Expected exactly one generated snapshot; found $($currentSnapshots.Count)."
}

$baselineDir = ".\tests\snapshots\baselines\development-main\all-testfiles"
$baselinePath = Join-Path $baselineDir $entry.SnapshotFileName

Copy-Item `
    -LiteralPath $currentSnapshots[0].FullName `
    -Destination $baselinePath
