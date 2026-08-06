<#
.SYNOPSIS
    Writes a portable snapshot baseline using the repository's canonical JSON format.

.DESCRIPTION
    This is the only full-baseline command-line writer. It converts current
    snapshots to repository-relative paths, removes volatile snapshot and
    run-info fields, preserves hover-text line endings, and writes Windows
    PowerShell 5.1 JSON with CRLF and UTF-8 without BOM. The complete
    destination is staged before replacement, and interrupted replacements are
    recovered from a sibling backup directory on the next run.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CurrentDirectory,

    [Parameter(Mandatory = $true)]
    [string]$BaselineDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "SnapshotBaseline.ps1")

Write-VleSnapshotBaseline `
    -CurrentDirectory $CurrentDirectory `
    -BaselineDirectory $BaselineDirectory

Write-Host "Updated baseline: $([System.IO.Path]::GetFullPath($BaselineDirectory))"
