param(
    [string]$SourceFile = "TestFiles/comma.v",
    [string]$RootSuffix = "Exp",
    [switch]$CloseVisualStudioWhenDone,
    [switch]$ResetVisualStudioBeforeRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    if (![string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        return (Resolve-Path -LiteralPath $PSScriptRoot).Path
    }

    return (Resolve-Path -LiteralPath (Split-Path -Parent $MyInvocation.MyCommand.Path)).Path
}

function Join-RepoPath {
    param(
        [string]$RepoRoot,
        [string]$RelativePath
    )

    if ([System.IO.Path]::IsPathRooted($RelativePath)) {
        return [System.IO.Path]::GetFullPath($RelativePath)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $RelativePath))
}

function Write-Utf8TextFile {
    param(
        [string]$Path,
        [string]$Text
    )

    $parent = Split-Path -Parent $Path
    if (![string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }

    # Generated JSON is UTF-8 without BOM.
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Text, $utf8NoBom)
}

function ConvertTo-NormalizedSnapshotPath {
    param([string]$Path)

    return $Path.Replace("\", "/").ToLowerInvariant()
}

function Get-NormalizedLeafName {
    param([string]$Path)

    $normalized = ConvertTo-NormalizedSnapshotPath -Path $Path
    $parts = $normalized.Split([char[]]@("/"), [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($parts.Count -lt 1) {
        return ""
    }

    return $parts[$parts.Count - 1]
}

function Test-ExpectationMatchesSourceFile {
    param(
        [string]$ExpectedFile,
        [string]$SourceFile
    )

    if ([string]::IsNullOrWhiteSpace($ExpectedFile)) {
        return $false
    }

    $expected = ConvertTo-NormalizedSnapshotPath -Path $ExpectedFile
    $source = ConvertTo-NormalizedSnapshotPath -Path $SourceFile
    $expectedLeaf = Get-NormalizedLeafName -Path $expected
    $sourceLeaf = Get-NormalizedLeafName -Path $source

    return ($source.EndsWith($expected) -or
        $expected.EndsWith($source) -or
        (![string]::IsNullOrWhiteSpace($expectedLeaf) -and $expectedLeaf -eq $sourceLeaf))
}

function New-FilteredExpectationDirectory {
    param(
        [string]$SourceFile,
        [string]$ExpectationsRoot,
        [string]$FilteredExpectationsRoot
    )

    Remove-Item $FilteredExpectationsRoot -Recurse -Force -ErrorAction SilentlyContinue
    New-Item $FilteredExpectationsRoot -ItemType Directory -Force | Out-Null

    if (!(Test-Path $ExpectationsRoot)) {
        Write-Host "Expectation directory not found, skipping targeted expectations: $ExpectationsRoot"
        return 0
    }

    $matchedCount = 0
    foreach ($expectationPath in @(Get-ChildItem $ExpectationsRoot -Filter "*.expect.json" -File)) {
        $expectation = Get-Content $expectationPath.FullName -Raw | ConvertFrom-Json
        if (Test-ExpectationMatchesSourceFile -ExpectedFile $expectation.File -SourceFile $SourceFile) {
            Copy-Item -Force -Path $expectationPath.FullName -Destination (Join-Path $FilteredExpectationsRoot $expectationPath.Name)
            $matchedCount++
        }
    }

    return $matchedCount
}

function Invoke-CompareSnapshots {
    param(
        [string]$CompareScript,
        [string]$CurrentDir,
        [string]$ExpectationsDir = ""
    )

    if ([string]::IsNullOrWhiteSpace($ExpectationsDir)) {
        & python $CompareScript --current $CurrentDir
    }
    else {
        & python $CompareScript --current $CurrentDir --expectations $ExpectationsDir
    }

    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        Write-Warning "Compare-Snapshots.py returned exit code $exitCode; continuing because check-file.ps1 is a one-file local check."
    }

    $global:LASTEXITCODE = 0
    return $exitCode
}

$repoRoot = Get-RepoRoot
[System.IO.Directory]::SetCurrentDirectory($repoRoot)
Set-Location -LiteralPath $repoRoot

$manifestName = "single-testfile"
$manifestPath = Join-RepoPath -RepoRoot $repoRoot -RelativePath "tools\vle-ci\manifests\$manifestName.json"
$outputDir = Join-RepoPath -RepoRoot $repoRoot -RelativePath "artifacts\snapshots\$manifestName"
$expectations = Join-RepoPath -RepoRoot $repoRoot -RelativePath "tools\vle-ci\expectations"
$filteredExpectations = Join-RepoPath -RepoRoot $repoRoot -RelativePath "artifacts\snapshots\$manifestName-expectations"
$exportScript = Join-RepoPath -RepoRoot $repoRoot -RelativePath "tools\vle-ci\Export-Snapshots.ps1"
$compareScript = Join-RepoPath -RepoRoot $repoRoot -RelativePath "tools\vle-ci\Compare-Snapshots.py"
$baselineDir = Join-RepoPath -RepoRoot $repoRoot -RelativePath "tests\snapshots\baselines\development-main\all-testfiles"

if (!(Test-Path $exportScript)) {
    throw "Export script not found: $exportScript"
}

if (!(Test-Path $compareScript)) {
    throw "Compare script not found: $compareScript"
}

# Recreate the one-file snapshot output directory.
# This avoids stale snapshots from previous full or partial CI runs.
Remove-Item $outputDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item $outputDir -ItemType Directory -Force | Out-Null

# Create a temporary one-file manifest.
# RunName is kept as all-testfiles so snapshot content stays comparable
# with the normal approved baseline naming/content.
# DelayMs controls how long VS waits after opening the file.
# FreshInstancePerFile is false because this manifest contains only one file.
$manifest = @{
    RunName = "all-testfiles"
    DelayMs = 3000
    FreshInstancePerFile = $false
    Files = @(
        $SourceFile
    )
}
$manifestJson = $manifest | ConvertTo-Json -Depth 5
Write-Utf8TextFile -Path $manifestPath -Text ($manifestJson + [Environment]::NewLine)

# Export only the file listed in the temporary manifest.
# The one-file wrapper is intentionally interactive: by default it leaves the
# VS Experimental Instance open and does not reset it before the run.
# Use -CloseVisualStudioWhenDone to close it after export, or
# -ResetVisualStudioBeforeRun to close the matching Experimental Instance first.
$exportArgs = @{
    Manifest = $manifestPath
    OutputDir = $outputDir
    RootSuffix = $RootSuffix
    MaxWaitSeconds = 45
    SkipBackgroundProcessCleanup = $true
}

if (!$CloseVisualStudioWhenDone.IsPresent) {
    $exportArgs["LeaveVisualStudioOpen"] = $true
}

if (!$ResetVisualStudioBeforeRun.IsPresent) {
    $exportArgs["SkipInitialVisualStudioCleanup"] = $true
}

& $exportScript @exportArgs

if ($LASTEXITCODE -ne 0) {
    throw "Export-Snapshots.ps1 failed with exit code $LASTEXITCODE"
}

# Confirm exactly one snapshot was generated.
# This prevents accidentally comparing stale files or checking the wrong run.
$currentSnapshots = @(Get-ChildItem $outputDir -Filter "*.snapshot.json" -File)

if ($currentSnapshots.Count -ne 1) {
    throw "Expected exactly one snapshot in $outputDir, found $($currentSnapshots.Count)"
}

$current = $currentSnapshots[0]

# First run the built-in snapshot sanity checks on the isolated one-file output.
# Do not pass the full expectations directory here; those expectations cover
# other test files and would fail because this run intentionally exported one file.
[void](Invoke-CompareSnapshots -CompareScript $compareScript -CurrentDir $outputDir)

# If there are targeted expectation files for this source, copy only those
# expectations to a temporary directory and run them against the one-file output.
$matchedExpectations = New-FilteredExpectationDirectory `
    -SourceFile $SourceFile `
    -ExpectationsRoot $expectations `
    -FilteredExpectationsRoot $filteredExpectations

if ($matchedExpectations -gt 0) {
    Write-Host "Targeted expectations matched: $matchedExpectations"
    [void](Invoke-CompareSnapshots `
        -CompareScript $compareScript `
        -CurrentDir $outputDir `
        -ExpectationsDir $filteredExpectations)
}
else {
    Write-Host "No targeted expectations matched $SourceFile; skipping expectation checks."
}

# Find the matching approved baseline snapshot from the full all-testfiles baseline.
# The wildcard handles the numeric prefix changing between manifests.
$baseName = Split-Path $SourceFile -Leaf
$baseline = @(Get-ChildItem -Path (Join-Path $baselineDir "*-$baseName.snapshot.json") -File -ErrorAction SilentlyContinue |
    Select-Object -First 1)

if ($baseline.Count -lt 1) {
    Write-Warning "No matching baseline snapshot found for $SourceFile; skipping baseline diff."
}
else {
    # Compare the generated one-file snapshot against the approved baseline.
    # --no-index lets git compare arbitrary files, not only tracked files.
    & git -C $repoRoot diff --no-index -- $baseline[0].FullName $current.FullName
    $diffExitCode = $LASTEXITCODE

    if ($diffExitCode -eq 0) {
        Write-Host "Baseline diff: no differences."
    }
    elseif ($diffExitCode -eq 1) {
        Write-Host "Baseline diff: differences shown above."
    }
    else {
        Write-Warning "git diff returned exit code $diffExitCode."
    }

    $global:LASTEXITCODE = 0
}

Write-Host "One-file snapshot check complete:"
Write-Host "  Source:   $SourceFile"
Write-Host "  Current:  $($current.FullName)"
if ($baseline.Count -ge 1) {
    Write-Host "  Baseline: $($baseline[0].FullName)"
}
else {
    Write-Host "  Baseline: not found"
}

$global:LASTEXITCODE = 0
