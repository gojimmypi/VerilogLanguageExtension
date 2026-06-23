param(
    [string]$SourceFile = "TestFiles/testfile-5K.v"
)

$ErrorActionPreference = "Stop"

$manifestName = "single-testfile"
$manifestPath = ".\tools\vle-ci\manifests\$manifestName.json"
$outputDir = ".\artifacts\snapshots\$manifestName"

# Recreate the one-file snapshot output directory.
# This avoids stale snapshots from previous full or partial CI runs.
Remove-Item $outputDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item $outputDir -ItemType Directory -Force | Out-Null

# Create a temporary one-file manifest.
# RunName is kept as all-testfiles so snapshot content stays comparable
# with the normal approved baseline naming/content.
# DelayMs controls how long VS waits after opening the file.
# FreshInstancePerFile is false because this manifest contains only one file.
@{
    RunName = "all-testfiles"
    DelayMs = 3000
    FreshInstancePerFile = $false
    Files = @(
        $SourceFile
    )
} |
    ConvertTo-Json -Depth 5 |
    Set-Content $manifestPath -Encoding UTF8

# Kill leftover Visual Studio Experimental, PerfWatson, and Copilot language-server
# processes before running the one-file export.
Get-Process devenv,PerfWatson2,copilot-language-server -ErrorAction SilentlyContinue |
    Stop-Process -Force

# Kill only node/nodejs processes that clearly belong to Visual Studio Copilot.
# This avoids killing unrelated Node.js development processes.
Get-CimInstance Win32_Process -Filter "Name = 'node.exe' OR Name = 'nodejs.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -match '\\Common7\\IDE\\Extensions\\Microsoft\\Copilot\\|copilot-language-server|github.?copilot' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force }

# Export only the file listed in the temporary manifest.
# -Manifest points to the one-file manifest.
# -OutputDir writes to the isolated one-file snapshot directory.
# -MaxWaitSeconds limits how long the script waits for the snapshot.
.\tools\vle-ci\Export-Snapshots.ps1 `
    -Manifest $manifestPath `
    -OutputDir $outputDir `
    -MaxWaitSeconds 45

if ($LASTEXITCODE -ne 0) {
    throw "Export-Snapshots.ps1 failed with exit code $LASTEXITCODE"
}

# Confirm exactly one snapshot was generated.
# This prevents accidentally comparing stale files or checking the wrong run.
$currentSnapshots = Get-ChildItem $outputDir -Filter "*.snapshot.json"

if ($currentSnapshots.Count -ne 1) {
    throw "Expected exactly one snapshot in $outputDir, found $($currentSnapshots.Count)"
}

$current = $currentSnapshots[0]

# Run snapshot sanity checks and expectation checks against the isolated
# one-file snapshot output directory.
python .\tools\vle-ci\Compare-Snapshots.py `
    --current $outputDir `
    --expectations .\tools\vle-ci\expectations

if ($LASTEXITCODE -ne 0) {
    throw "Compare-Snapshots.py expectation check failed with exit code $LASTEXITCODE"
}

# Find the matching approved baseline snapshot from the full all-testfiles baseline.
# The wildcard handles the numeric prefix changing between manifests.
$baseName = Split-Path $SourceFile -Leaf
$baseline = Get-ChildItem .\tests\snapshots\baselines\development-main\all-testfiles\*-$baseName.snapshot.json |
    Select-Object -First 1

if ($null -eq $baseline) {
    throw "No matching baseline snapshot found for $SourceFile"
}

# Compare the generated one-file snapshot against the approved baseline.
# --no-index lets git compare arbitrary files, not only tracked files.
git diff --no-index -- $baseline.FullName $current.FullName

# Clean up Visual Studio/Copilot processes after the run.
# This is intentional because Export-Snapshots.ps1 launches Visual Studio.
Get-Process devenv,PerfWatson2,copilot-language-server -ErrorAction SilentlyContinue |
    Stop-Process -Force

# Clean up only VS Copilot node/nodejs helper processes after the run.
Get-CimInstance Win32_Process -Filter "Name = 'node.exe' OR Name = 'nodejs.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -match '\\Common7\\IDE\\Extensions\\Microsoft\\Copilot\\|copilot-language-server|github.?copilot' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force }

Write-Host "One-file snapshot check complete:"
Write-Host "  Source:   $SourceFile"
Write-Host "  Current:  $($current.FullName)"
Write-Host "  Baseline: $($baseline.FullName)"
