<#
.SYNOPSIS
    Launches a Visual Studio experimental instance with SnapshotExportOnOpen enabled.

.DESCRIPTION
    This script is intentionally local-only. It builds no source by itself; it opens
    the files listed in a manifest and lets the DEBUG-only SnapshotExportOnOpen MEF
    listener save deterministic JSON snapshots.

    Close any old Experimental Instance before running this for best repeatability.
#>

[CmdletBinding()]
param(
    [string]$Manifest = "tools/vle-ci/manifests/cold-open.json",
    [string]$OutputDir = "artifacts/snapshots/current",
    [string]$RootSuffix = "Exp",
    [int]$DelaySeconds = 2,
    [int]$MaxWaitSeconds = 60,
    [switch]$FreshInstancePerFile,
    [switch]$LeaveVisualStudioOpen,
    [switch]$SkipBackgroundProcessCleanup,
    [string]$VisualStudioPath = ""
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

function Get-DevenvPath {
    param([string]$RequestedPath)

    if (![string]::IsNullOrWhiteSpace($RequestedPath)) {
        if (!(Test-Path $RequestedPath)) {
            throw "Visual Studio path not found: $RequestedPath"
        }
        return (Resolve-Path $RequestedPath).Path
    }

    $vswhere = Get-VsWherePath
    if ($null -ne $vswhere) {
        $installPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.CoreEditor -property installationPath
        if (![string]::IsNullOrWhiteSpace($installPath)) {
            $candidate = Join-Path $installPath "Common7/IDE/devenv.exe"
            if (Test-Path $candidate) {
                return $candidate
            }
        }
    }

    throw "Could not find devenv.exe. Pass -VisualStudioPath."
}

function Quote-CommandLineArgument {
    param([string]$Value)

    if ($null -eq $Value) {
        return '""'
    }

    return '"' + $Value.Replace('"', '\"') + '"'
}

function ConvertTo-SnapshotSafeFileName {
    param([string]$FilePath)

    if ([string]::IsNullOrWhiteSpace($FilePath)) {
        return "untitled"
    }

    $name = [System.IO.Path]::GetFileName($FilePath)
    foreach ($c in [System.IO.Path]::GetInvalidFileNameChars()) {
        $name = $name.Replace([string]$c, "_")
    }

    return $name
}

function Get-ExperimentalDevenvProcesses {
    param([string]$RequestedRootSuffix)

    $processes = @(Get-CimInstance Win32_Process -Filter "Name = 'devenv.exe'" -ErrorAction SilentlyContinue)
    foreach ($process in $processes) {
        $commandLine = [string]$process.CommandLine
        if ([string]::IsNullOrWhiteSpace($commandLine)) {
            continue
        }

        if ($commandLine -match "(?i)/RootSuffix\s+`"?$([regex]::Escape($RequestedRootSuffix))`"?") {
            $process
        }
    }
}

function Get-ChildProcessIds {
    param([int]$ParentProcessId)

    $children = @(Get-CimInstance Win32_Process -Filter "ParentProcessId = $ParentProcessId" -ErrorAction SilentlyContinue)
    foreach ($child in $children) {
        [int]$child.ProcessId
        foreach ($grandChildId in Get-ChildProcessIds -ParentProcessId ([int]$child.ProcessId)) {
            [int]$grandChildId
        }
    }
}

function Stop-ProcessTree {
    param([int]$RootProcessId)

    $processIds = @()
    foreach ($childId in Get-ChildProcessIds -ParentProcessId $RootProcessId) {
        $processIds += [int]$childId
    }
    $processIds += [int]$RootProcessId

    foreach ($processId in ($processIds | Select-Object -Unique | Sort-Object -Descending)) {
        try {
            Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
        }
        catch {
            Write-Warning "Could not stop process $processId`: $_"
        }
    }
}

function Test-IsVisualStudioCopilotProcess {
    param([object]$Process)

    $commandLine = [string]$Process.CommandLine
    if ([string]::IsNullOrWhiteSpace($commandLine)) {
        return $false
    }

    return ($commandLine -match "(?i)\\Common7\\IDE\\Extensions\\Microsoft\\Copilot\\") -or
        ($commandLine -match "(?i)copilot-language-server") -or
        ($commandLine -match "(?i)github.?copilot")
}

function Stop-VisualStudioBackgroundProcesses {
    param([switch]$IncludeCopilot)

    # PerfWatson frequently survives after a crashed Experimental Instance and
    # can hold resources or produce modal crash dialogs during the next CI pass.
    foreach ($process in @(Get-Process -Name "PerfWatson2" -ErrorAction SilentlyContinue)) {
        try {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
        catch {
            Write-Warning "Could not stop PerfWatson2 process $($process.Id): $_"
        }
    }

    if (!$IncludeCopilot.IsPresent) {
        return
    }

    # Visual Studio Copilot can leave one node.exe-backed language server per
    # short-lived Experimental Instance. With FreshInstancePerFile this can grow
    # into dozens of 700+ MB processes. These are local CI leftovers; killing
    # them is safer than letting the machine run out of memory. Limit node/nodejs
    # cleanup to command lines that clearly belong to Visual Studio Copilot.
    $copilotProcessNames = @(
        "copilot-language-server.exe",
        "node.exe",
        "nodejs.exe"
    )

    foreach ($processName in $copilotProcessNames) {
        $copilotProcesses = @(Get-CimInstance Win32_Process -Filter "Name = '$processName'" -ErrorAction SilentlyContinue)
        foreach ($process in $copilotProcesses) {
            if (!(Test-IsVisualStudioCopilotProcess -Process $process)) {
                continue
            }

            try {
                Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
            }
            catch {
                Write-Warning "Could not stop Visual Studio Copilot process $($process.ProcessId): $_"
            }
        }
    }
}

function Stop-ExperimentalDevenv {
    param([string]$RequestedRootSuffix)

    $processes = @(Get-ExperimentalDevenvProcesses -RequestedRootSuffix $RequestedRootSuffix)
    foreach ($process in $processes) {
        Stop-ProcessTree -RootProcessId ([int]$process.ProcessId)
    }

    if ($processes.Count -gt 0) {
        Start-Sleep -Seconds 2
    }

    if (!$SkipBackgroundProcessCleanup.IsPresent) {
        Stop-VisualStudioBackgroundProcesses -IncludeCopilot
    }
}

function Write-SnapshotConfig {
    param(
        [string]$ConfigFile,
        [string]$ActiveOutputDir,
        [string]$RunName,
        [string]$DelayMs,
        [string]$RepoRoot
    )

    @(
        "Enable=1",
        "OutputDir=$ActiveOutputDir",
        "RunName=$RunName",
        "DelayMs=$DelayMs",
        "RepoRoot=$RepoRoot"
    ) | Set-Content -Encoding UTF8 -Path $ConfigFile

    $env:VLE_SNAPSHOT_ENABLE = "1"
    $env:VLE_SNAPSHOT_OUTPUT_DIR = $ActiveOutputDir
    $env:VLE_SNAPSHOT_RUN_NAME = $RunName
    $env:VLE_SNAPSHOT_DELAY_MS = $DelayMs
    $env:VLE_REPO_ROOT = $RepoRoot

    # GitCommit is intentionally not passed to the per-file snapshot exporter.
    # It is written once to run-info.json instead, which keeps snapshot diffs focused.
    Remove-Item Env:\VLE_GIT_COMMIT -ErrorAction SilentlyContinue
}

function Get-LatestSnapshotFile {
    param(
        [string]$Directory,
        [datetime]$StartedAt
    )

    $slopStart = $StartedAt.AddSeconds(-2)
    $files = @(Get-ChildItem -Path $Directory -Filter "*.snapshot.json" -File -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTime -ge $slopStart } |
        Sort-Object LastWriteTime -Descending)

    if ($files.Count -lt 1) {
        return $null
    }

    return $files[0]
}

function Copy-SnapshotToFinalName {
    param(
        [System.IO.FileInfo]$SnapshotFile,
        [string]$FinalOutputDir,
        [int]$Index,
        [string]$SourceFilePath
    )

    $safeName = ConvertTo-SnapshotSafeFileName -FilePath $SourceFilePath
    $targetName = ("{0:0000}-{1}.snapshot.json" -f $Index, $safeName)
    $targetPath = Join-Path $FinalOutputDir $targetName

    if ($SnapshotFile.FullName -ieq $targetPath) {
        return $targetPath
    }

    if (Test-Path $targetPath) {
        Remove-Item -Force $targetPath
    }

    Move-Item -Force -Path $SnapshotFile.FullName -Destination $targetPath
    return $targetPath
}

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

function Write-SnapshotRunInfo {
    param(
        [string]$Path,
        [string]$RunName,
        [string]$Manifest,
        [string]$GitCommit
    )

    $runInfo = [ordered]@{
        SchemaVersion = 1
        RunName = $RunName
        GitCommit = $GitCommit
        Manifest = $Manifest.Replace("\", "/")
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $text = $runInfo | ConvertTo-Json -Depth 5
    [System.IO.File]::WriteAllText($Path, ($text + [Environment]::NewLine), $utf8NoBom)
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

function Get-ManifestBoolean {
    param(
        [object]$ManifestObject,
        [string]$PropertyName
    )

    $property = $ManifestObject.PSObject.Properties[$PropertyName]
    if ($null -eq $property) {
        return $false
    }

    $value = $property.Value
    if ($value -is [bool]) {
        return [bool]$value
    }

    return [System.Convert]::ToBoolean([string]$value)
}

$repoRoot = Get-RepoRoot
$manifestPath = Resolve-Path (Join-Path $repoRoot $Manifest)
$manifestJson = [System.IO.File]::ReadAllText($manifestPath, [System.Text.Encoding]::UTF8) | ConvertFrom-Json

$manifestFreshInstancePerFile = Get-ManifestBoolean -ManifestObject $manifestJson -PropertyName "FreshInstancePerFile"
$useFreshInstancePerFile = $FreshInstancePerFile.IsPresent -or $manifestFreshInstancePerFile

$finalOutputDir = Join-Path $repoRoot $OutputDir
if (Test-Path $finalOutputDir) {
    Remove-Item -Recurse -Force $finalOutputDir
}
New-Item -ItemType Directory -Force -Path $finalOutputDir | Out-Null

$gitCommit = ""
try {
    $gitCommit = (git -C $repoRoot rev-parse --short HEAD 2>$null).Trim()
}
catch {
    $gitCommit = ""
}

$configFile = Join-Path ([System.IO.Path]::GetTempPath()) "VerilogLanguage.ExportSnapshots.config"
$devenv = Get-DevenvPath -RequestedPath $VisualStudioPath

Write-Host "Snapshot output: $finalOutputDir"
Write-Host "Visual Studio: $devenv"
Write-Host "Run name: $($manifestJson.RunName)"
Write-Host "Fresh instance per file: $useFreshInstancePerFile"

$runInfoPath = Join-Path $finalOutputDir "run-info.json"
Write-SnapshotRunInfo `
    -Path $runInfoPath `
    -RunName ([string]$manifestJson.RunName) `
    -Manifest $Manifest `
    -GitCommit $gitCommit
Write-Host "Git commit reference: $runInfoPath"

try {
    # Start from a known state. This avoids the common case where an already-open
    # Experimental Instance has the target file loaded, so no new text view is created.
    Stop-ExperimentalDevenv -RequestedRootSuffix $RootSuffix

    $index = 0
    foreach ($file in $manifestJson.Files) {
        $index++
        $filePath = Join-Path $repoRoot ([string]$file)
        if (!(Test-Path $filePath)) {
            throw "Manifest file not found: $filePath"
        }

        $activeOutputDir = $finalOutputDir
        if ($useFreshInstancePerFile) {
            Stop-ExperimentalDevenv -RequestedRootSuffix $RootSuffix
            $activeOutputDir = Join-Path $finalOutputDir ("_work_{0:0000}" -f $index)
            if (Test-Path $activeOutputDir) {
                Remove-Item -Recurse -Force $activeOutputDir
            }
            New-Item -ItemType Directory -Force -Path $activeOutputDir | Out-Null
        }

        Write-SnapshotConfig `
            -ConfigFile $configFile `
            -ActiveOutputDir $activeOutputDir `
            -RunName ([string]$manifestJson.RunName) `
            -DelayMs ([string]$manifestJson.DelayMs) `
            -RepoRoot $repoRoot

        Write-Host "Opening $file"
        $startedAt = Get-Date
        $openArguments = "/RootSuffix " + (Quote-CommandLineArgument $RootSuffix) + " /NoSplash " + (Quote-CommandLineArgument $filePath)
        Start-Process -FilePath $devenv -ArgumentList $openArguments -WindowStyle Minimized | Out-Null

        $configuredDelayMs = 0
        [void][int]::TryParse([string]$manifestJson.DelayMs, [ref]$configuredDelayMs)
        $minimumWait = [Math]::Ceiling($configuredDelayMs / 1000.0) + $DelaySeconds + 15
        $deadlineSeconds = [Math]::Max($MaxWaitSeconds, [int]$minimumWait)
        $deadline = (Get-Date).AddSeconds($deadlineSeconds)
        $snapshotFile = $null

        do {
            Start-Sleep -Seconds 1
            $snapshotFile = Get-LatestSnapshotFile -Directory $activeOutputDir -StartedAt $startedAt
        } while ($null -eq $snapshotFile -and (Get-Date) -lt $deadline)

        if ($null -eq $snapshotFile) {
            $latest = @(Get-ChildItem -Path $activeOutputDir -Filter "*.snapshot.json" -File -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTime -Descending |
                Select-Object -First 5 |
                ForEach-Object { "  " + $_.FullName + "  " + $_.LastWriteTime.ToString("s") })

            Write-Host "No new snapshot was found in: $activeOutputDir"
            if ($latest.Count -gt 0) {
                Write-Host "Latest snapshot files:"
                $latest | ForEach-Object { Write-Host $_ }
            }

            throw "No snapshot was written after opening $file. The file exists and VS was launched, but no export appeared within $deadlineSeconds seconds."
        }

        $finalSnapshotPath = Copy-SnapshotToFinalName `
            -SnapshotFile $snapshotFile `
            -FinalOutputDir $finalOutputDir `
            -Index $index `
            -SourceFilePath $filePath

        Format-JsonFile -Path $finalSnapshotPath
        Write-Host "Snapshot: $finalSnapshotPath"

        if ($useFreshInstancePerFile) {
            Stop-ExperimentalDevenv -RequestedRootSuffix $RootSuffix
            if (Test-Path $activeOutputDir) {
                Remove-Item -Recurse -Force $activeOutputDir
            }
        }
        else {
            # Close the active document so repeated files in the manifest create a fresh view.
            $closeArguments = "/RootSuffix " + (Quote-CommandLineArgument $RootSuffix) + " /NoSplash /Command File.Close"
            Start-Process -FilePath $devenv -ArgumentList $closeArguments -WindowStyle Minimized | Out-Null
            Start-Sleep -Seconds 1
        }
    }
}
finally {
    if (!$LeaveVisualStudioOpen.IsPresent) {
        Stop-ExperimentalDevenv -RequestedRootSuffix $RootSuffix
    }

    if (Test-Path $configFile) {
        Remove-Item -Force $configFile -ErrorAction SilentlyContinue
    }
}

Format-GeneratedJsonFiles -Directory $finalOutputDir

$expectedCount = @($manifestJson.Files).Count
$actualCount = @(Get-ChildItem -Path $finalOutputDir -Filter "*.snapshot.json" -File -ErrorAction SilentlyContinue).Count
Write-Host "Snapshots written: $actualCount / expected $expectedCount"

if ($actualCount -lt $expectedCount) {
    throw "Not all snapshots were written. Close the Experimental Instance and rerun."
}
