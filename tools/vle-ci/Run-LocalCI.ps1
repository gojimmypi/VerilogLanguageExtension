<#
.SYNOPSIS
    Local CI/CD entry point for VerilogLanguageExtension.

.DESCRIPTION
    Builds the extension, exports editor semantic snapshots through the VS
    Experimental Instance, and compares them against expectations and optionally
    an approved baseline.
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Debug",
    [string]$Manifest = "tools/vle-ci/manifests/cold-open.json",
    [string]$Baseline = "",
    [switch]$UpdateBaseline,
    [switch]$SkipBuild,
    [switch]$SkipSnapshots,
    [string]$RootSuffix = "Exp"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    $scriptDir = Split-Path -Parent $PSCommandPath
    return (Resolve-Path (Join-Path $scriptDir "../..")).Path
}

function Get-VsWherePath {
    $programFilesX86 = ${env:ProgramFiles(x86)}
    if ([string]::IsNullOrWhiteSpace($programFilesX86)) {
        return $null
    }

    $candidate = Join-Path $programFilesX86 "Microsoft Visual Studio/Installer/vswhere.exe"
    if (Test-Path $candidate) {
        return $candidate
    }

    return $null
}

function Get-MSBuildPath {
    $vswhere = Get-VsWherePath
    if ($null -ne $vswhere) {
        $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild/**/Bin/MSBuild.exe" | Select-Object -First 1
        if (![string]::IsNullOrWhiteSpace($msbuild) -and (Test-Path $msbuild)) {
            return $msbuild
        }
    }

    $fallback = "C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
    if (Test-Path $fallback) {
        return $fallback
    }

    throw "Could not find MSBuild.exe. Run from a Visual Studio Developer PowerShell or install VS Build Tools."
}

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

$repoRoot = Get-RepoRoot
$solution = Join-Path $repoRoot "VerilogLanguageExtension.sln"
$currentSnapshots = Join-Path $repoRoot "artifacts/snapshots/current"
$expectations = Join-Path $repoRoot "tools/vle-ci/expectations"
$compareTool = Join-Path $repoRoot "tools/vle-ci/Compare-Snapshots.py"

if (!$SkipBuild) {
    $msbuild = Get-MSBuildPath
    Write-Host "MSBuild: $msbuild"
    & $msbuild $solution /restore /m /p:Configuration=$Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild failed with exit code $LASTEXITCODE"
    }
}

if (!$SkipSnapshots) {
    & (Join-Path $repoRoot "tools/vle-ci/Export-Snapshots.ps1") -Manifest $Manifest -OutputDir "artifacts/snapshots/current" -RootSuffix $RootSuffix
    Format-GeneratedJsonFiles -Directory $currentSnapshots
}

$python = "python"
$compareArgs = @("$compareTool", "--current", "$currentSnapshots", "--expectations", "$expectations")
$baselinePath = ""

if (![string]::IsNullOrWhiteSpace($Baseline)) {
    $baselinePath = Join-Path $repoRoot $Baseline
    $compareArgs += @("--baseline", "$baselinePath")
    if ($UpdateBaseline) {
        $compareArgs += "--update-baseline"
    }
}
elseif ($UpdateBaseline) {
    throw "-UpdateBaseline requires -Baseline"
}

& $python @compareArgs
if ($LASTEXITCODE -ne 0) {
    throw "Snapshot comparison failed with exit code $LASTEXITCODE"
}

if ($UpdateBaseline -and ![string]::IsNullOrWhiteSpace($baselinePath)) {
    Format-GeneratedJsonFiles -Directory $baselinePath
}

Write-Host "Local CI completed successfully."
