Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = if (![string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    $PSScriptRoot
}
else {
    Split-Path -Parent $MyInvocation.MyCommand.Path
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $scriptDir "..")).Path
Set-Location -LiteralPath $repoRoot

& (Join-Path $scriptDir "ci-check.ps1") *>&1 | Tee-Object -FilePath (Join-Path $repoRoot "ci_check.log")
