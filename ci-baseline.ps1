# updates the all-testfiles baseline

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$baselineDir = "tests\snapshots\baselines\development-main\all-testfiles"

function Remove-SnapshotGitCommit {
    param([object]$Json)

    if ($null -eq $Json) {
        return
    }

    $gitCommitProperty = $Json.PSObject.Properties["GitCommit"]
    if ($null -ne $gitCommitProperty) {
        $Json.PSObject.Properties.Remove("GitCommit")
    }
}

function Format-JsonFile {
    param([string]$Path)

    if (!(Test-Path $Path)) {
        return
    }

    try {
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        $rawJson = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
        $json = $rawJson | ConvertFrom-Json

        if ((Split-Path $Path -Leaf) -like "*.snapshot.json") {
            Remove-SnapshotGitCommit -Json $json
        }

        $text = $json | ConvertTo-Json -Depth 100
        [System.IO.File]::WriteAllText($Path, ($text + [Environment]::NewLine), $utf8NoBom)
    }
    catch {
        Write-Warning "Could not format JSON $Path`: $_"
    }
}

function Format-GeneratedJsonFiles {
    param([string]$Directory)

    if (!(Test-Path $Directory)) {
        return
    }

    foreach ($jsonFile in @(Get-ChildItem -Path $Directory -Filter "*.json" -File -Recurse -ErrorAction SilentlyContinue)) {
        Format-JsonFile -Path $jsonFile.FullName
    }
}

# Refresh the all-testfiles manifest first. Existing baseline snapshot names are
# preserved, and new files are placed first in the run order.
.\create-testfile-manifest.ps1 -BaselineDir $baselineDir

# Run the baseline update for the all-testfiles manifest.
# -Manifest selects the generated manifest that opens all Verilog test files.
# -Baseline selects the approved baseline directory to replace.
# -UpdateBaseline tells Run-LocalCI.ps1 to write current snapshots into that baseline.
.\tools\vle-ci\Run-LocalCI.ps1 `
    -Manifest tools\vle-ci\manifests\all-testfiles.json `
    -Baseline $baselineDir `
    -UpdateBaseline

# Belt-and-suspenders formatting for the files just generated under tests\snapshots\baselines.
# Run-LocalCI.ps1 and Compare-Snapshots.py should already write formatted JSON, but this keeps
# ci-baseline.ps1 safe if either script is run from an older local copy.
Format-GeneratedJsonFiles -Directory $baselineDir

# The baseline refresh completed successfully. Clear the temporary "new file"
# priority markers so the next run treats these files as normal baseline files.
.\create-testfile-manifest.ps1 -BaselineDir $baselineDir -AcceptCurrentManifest
