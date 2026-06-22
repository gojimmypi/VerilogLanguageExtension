# create-testfile-manifest.ps1

$repoRoot = (Resolve-Path .).Path

$files = Get-ChildItem .\TestFiles -Recurse -File |
    Where-Object { $_.Extension -in ".v", ".sv", ".svh", ".vh", ".verilog" } |
    Sort-Object FullName |
    ForEach-Object {
        $_.FullName.Substring($repoRoot.Length + 1).Replace("\", "/")
    }

$manifest = [ordered]@{
    RunName = "all-testfiles"
    DelayMs = 3000
    FreshInstancePerFile = $true
    Files = $files
}

$manifest | ConvertTo-Json -Depth 5 |
    Set-Content .\tools\vle-ci\manifests\all-testfiles.json -Encoding UTF8
