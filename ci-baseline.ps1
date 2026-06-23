# updates the all-testfiles baseline

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$baselineDir = "tests\snapshots\baselines\development-main\all-testfiles"

function Format-JsonFile {
    param([string]$Path)

    if (!(Test-Path $Path)) {
        return
    }

    try {
        $json = Get-Content -Raw -Path $Path | ConvertFrom-Json
        $text = $json | ConvertTo-Json -Depth 100
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
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
