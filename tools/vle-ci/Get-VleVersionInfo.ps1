<#
.SYNOPSIS
    Reports VerilogLanguageExtension release/version metadata.

.DESCRIPTION
    Reads source.extension.vsixmanifest, Properties/AssemblyInfo.cs, and
    VerilogLanguagePackage.cs, then emits one object with the release metadata
    that should stay in sync for a VLE release.
#>

[CmdletBinding()]
param(
    [string]$RepoRoot = "",
    [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-DefaultRepoRoot {
    $scriptDir = Split-Path -Parent $PSCommandPath
    return (Resolve-Path (Join-Path $scriptDir "../..")).Path
}

function Resolve-RepoPath {
    param(
        [string]$Root,
        [string]$RelativePath
    )

    return Join-Path $Root $RelativePath
}

function Get-RegexCapture {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Name
    )

    $match = [regex]::Match($Text, $Pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if (!$match.Success) {
        throw "Could not find $Name"
    }

    return $match.Groups[1].Value
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Get-DefaultRepoRoot
}
else {
    $RepoRoot = (Resolve-Path $RepoRoot).Path
}

$vsixManifestPath = Resolve-RepoPath -Root $RepoRoot -RelativePath "source.extension.vsixmanifest"
$assemblyInfoPath = Resolve-RepoPath -Root $RepoRoot -RelativePath "Properties/AssemblyInfo.cs"
$packagePath = Resolve-RepoPath -Root $RepoRoot -RelativePath "VerilogLanguagePackage.cs"

foreach ($path in @($vsixManifestPath, $assemblyInfoPath, $packagePath)) {
    if (!(Test-Path $path)) {
        throw "Required VLE version source file not found: $path"
    }
}

[xml]$manifestXml = Get-Content -Raw -Encoding UTF8 -Path $vsixManifestPath
$ns = New-Object System.Xml.XmlNamespaceManager($manifestXml.NameTable)
$ns.AddNamespace("vsx", "http://schemas.microsoft.com/developer/vsx-schema/2011")
$identityNode = $manifestXml.SelectSingleNode("/vsx:PackageManifest/vsx:Metadata/vsx:Identity", $ns)
if ($null -eq $identityNode) {
    throw "Could not find VSIX Identity node in $vsixManifestPath"
}
$vsixVersion = $identityNode.GetAttribute("Version")
if ([string]::IsNullOrWhiteSpace($vsixVersion)) {
    throw "Could not find VSIX Identity Version in $vsixManifestPath"
}

$assemblyText = Get-Content -Raw -Encoding UTF8 -Path $assemblyInfoPath
$assemblyVersion = Get-RegexCapture -Text $assemblyText -Pattern '\[assembly:\s*AssemblyVersion\("([^"]+)"\)\]' -Name "AssemblyVersion"
$assemblyFileVersion = Get-RegexCapture -Text $assemblyText -Pattern '\[assembly:\s*AssemblyFileVersion\("([^"]+)"\)\]' -Name "AssemblyFileVersion"
$assemblyInformationalVersion = Get-RegexCapture -Text $assemblyText -Pattern '\[assembly:\s*AssemblyInformationalVersion\("([^"]+)"\)\]' -Name "AssemblyInformationalVersion"

$packageText = Get-Content -Raw -Encoding UTF8 -Path $packagePath
$menuMatch = [regex]::Match(
    $packageText,
    '\[ProvideMenuResource\(\s*"([^"]+)"\s*,\s*(\d+)\s*\)\]',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (!$menuMatch.Success) {
    throw "Could not find ProvideMenuResource in $packagePath"
}

$result = [ordered]@{
    VsixManifestVersion = $vsixVersion
    AssemblyVersion = $assemblyVersion
    AssemblyFileVersion = $assemblyFileVersion
    AssemblyInformationalVersion = $assemblyInformationalVersion
    ProvideMenuResourceName = $menuMatch.Groups[1].Value
    ProvideMenuResourceVersion = [int]$menuMatch.Groups[2].Value
}

if ($AsJson) {
    $result | ConvertTo-Json -Depth 5
}
else {
    [pscustomobject]$result
}
