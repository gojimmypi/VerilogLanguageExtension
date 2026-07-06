<#
.SYNOPSIS
    Checks VerilogLanguageExtension release/version metadata.

.DESCRIPTION
    Validates that the VSIX manifest version, AssemblyFileVersion, and
    AssemblyInformationalVersion agree. AssemblyVersion is checked against the
    stable package-load value by default, not against the VSIX release version.
#>

[CmdletBinding()]
param(
    [string]$RepoRoot = "",
    [string]$ExpectedVersion = "",
    [string]$ExpectedAssemblyVersion = "0.4.0.0"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-DefaultRepoRoot {
    $scriptDir = Split-Path -Parent $PSCommandPath
    return (Resolve-Path (Join-Path $scriptDir "../..")).Path
}

function Add-Failure {
    param(
        [System.Collections.Generic.List[string]]$Failures,
        [string]$Message
    )

    $Failures.Add($Message) | Out-Null
    Write-Host "FAIL: $Message"
}

function Assert-Equal {
    param(
        [System.Collections.Generic.List[string]]$Failures,
        [string]$Name,
        [string]$Actual,
        [string]$Expected
    )

    if ($Actual -ne $Expected) {
        Add-Failure -Failures $Failures -Message "$Name is '$Actual', expected '$Expected'"
    }
    else {
        Write-Host "PASS: $Name == $Expected"
    }
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Get-DefaultRepoRoot
}
else {
    $RepoRoot = (Resolve-Path $RepoRoot).Path
}

$getInfoScript = Join-Path $PSScriptRoot "Get-VleVersionInfo.ps1"
if (!(Test-Path $getInfoScript)) {
    throw "Required script not found: $getInfoScript"
}

$info = & $getInfoScript -RepoRoot $RepoRoot
if ([string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    $ExpectedVersion = $info.VsixManifestVersion
}

$failures = New-Object 'System.Collections.Generic.List[string]'

Assert-Equal -Failures $failures -Name "VSIX manifest version" -Actual $info.VsixManifestVersion -Expected $ExpectedVersion
Assert-Equal -Failures $failures -Name "AssemblyFileVersion" -Actual $info.AssemblyFileVersion -Expected $ExpectedVersion

if ($info.AssemblyInformationalVersion -ne $ExpectedVersion -and !$info.AssemblyInformationalVersion.StartsWith(($ExpectedVersion + "+")) -and !$info.AssemblyInformationalVersion.StartsWith(($ExpectedVersion + "-"))) {
    Add-Failure -Failures $failures -Message "AssemblyInformationalVersion is '$($info.AssemblyInformationalVersion)', expected '$ExpectedVersion' or '$ExpectedVersion+...'"
}
else {
    Write-Host "PASS: AssemblyInformationalVersion is compatible with $ExpectedVersion"
}

if (![string]::IsNullOrWhiteSpace($ExpectedAssemblyVersion)) {
    Assert-Equal -Failures $failures -Name "AssemblyVersion" -Actual $info.AssemblyVersion -Expected $ExpectedAssemblyVersion
}
else {
    Write-Host "INFO: AssemblyVersion == $($info.AssemblyVersion)"
}

if ($info.ProvideMenuResourceName -ne "Menus.ctmenu") {
    Add-Failure -Failures $failures -Message "ProvideMenuResource name is '$($info.ProvideMenuResourceName)', expected 'Menus.ctmenu'"
}
elseif ($info.ProvideMenuResourceVersion -lt 1) {
    Add-Failure -Failures $failures -Message "ProvideMenuResource version must be positive, found $($info.ProvideMenuResourceVersion)"
}
else {
    Write-Host "INFO: ProvideMenuResource == $($info.ProvideMenuResourceName), $($info.ProvideMenuResourceVersion)"
}

if ($failures.Count -gt 0) {
    throw "VLE release metadata check failed with $($failures.Count) issue(s)."
}

Write-Host "VLE release metadata check passed."
