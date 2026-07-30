# Lists newly added contents of the testfiles manifest.
#
# Refresh the manifest with:
#
#  .\scripts\create-testfile-manifest.ps1
#
# Update all testfiles with:
#
#  .\scripts\refresh-tests.ps1
#
# Generate a specific testfile snapshot with:
#
#   .\scripts\check-file.ps1 -SourceFile "TestFiles\z386.sv"
#
$manifestPath = ".\tools\vle-ci\manifests\all-testfiles.json"

$manifest = Get-Content -LiteralPath $manifestPath -Raw |
    ConvertFrom-Json

$entry = @($manifest.Files) | Where-Object {
    ($_.Path -replace '\\', '/') -ieq "TestFiles/z386.sv"
}

if (@($entry).Count -ne 1) {
    throw "Expected exactly one manifest entry for TestFiles/z386.sv."
}

$entry | Format-List Path, SnapshotFileName, IsNew
