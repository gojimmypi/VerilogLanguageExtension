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
    [switch]$SkipInitialVisualStudioCleanup,
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

function Get-NormalizedFullPath {
    param([string]$Path)

    return [System.IO.Path]::GetFullPath($Path)
}

function Resolve-RepoPath {
    param(
        [string]$RepoRoot,
        [string]$Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $Path))
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

    $configText = @(
        "Enable=1",
        "OutputDir=$ActiveOutputDir",
        "RunName=$RunName",
        "DelayMs=$DelayMs",
        "RepoRoot=$RepoRoot"
    ) -join [Environment]::NewLine

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($ConfigFile, ($configText + [Environment]::NewLine), $utf8NoBom)

    $env:VLE_SNAPSHOT_ENABLE = "1"
    $env:VLE_SNAPSHOT_OUTPUT_DIR = $ActiveOutputDir
    $env:VLE_SNAPSHOT_RUN_NAME = $RunName
    $env:VLE_SNAPSHOT_DELAY_MS = $DelayMs
    $env:VLE_REPO_ROOT = $RepoRoot

    # GitCommit is intentionally not passed to the per-file snapshot exporter.
    # Keep generated snapshot JSON and run-info JSON stable for diff review.
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

function Wait-SnapshotFileReady {
    param(
        [string]$Path,
        [int]$TimeoutSeconds = 30
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastLength = -1L
    $lastWriteUtc = [datetime]::MinValue
    $stableCount = 0
    $lastError = $null

    do {
        try {
            if (!(Test-Path -LiteralPath $Path)) {
                throw "File not found: $Path"
            }

            $item = Get-Item -LiteralPath $Path -ErrorAction Stop
            $stream = [System.IO.File]::Open($item.FullName, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::None)
            $stream.Dispose()

            if (($item.Length -eq $lastLength) -and ($item.LastWriteTimeUtc -eq $lastWriteUtc)) {
                $stableCount++
            }
            else {
                $stableCount = 1
                $lastLength = $item.Length
                $lastWriteUtc = $item.LastWriteTimeUtc
            }

            if ($stableCount -ge 2) {
                return
            }
        }
        catch {
            $lastError = $_
            $stableCount = 0
        }

        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)

    if ($null -ne $lastError) {
        throw "Snapshot file stayed locked or unstable for $TimeoutSeconds seconds: $Path. Last error: $lastError"
    }

    throw "Snapshot file stayed unstable for $TimeoutSeconds seconds: $Path"
}

function Invoke-FileOperationWithRetry {
    param(
        [scriptblock]$Operation,
        [string]$Description,
        [int]$MaxAttempts = 40,
        [int]$DelayMilliseconds = 250
    )

    $lastError = $null

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            & $Operation
            return
        }
        catch {
            $lastError = $_
            if ($attempt -ge $MaxAttempts) {
                break
            }

            Start-Sleep -Milliseconds $DelayMilliseconds
        }
    }

    throw "$Description failed after $MaxAttempts attempts. Last error: $lastError"
}

function Copy-SnapshotToFinalName {
    param(
        [System.IO.FileInfo]$SnapshotFile,
        [string]$FinalOutputDir,
        [int]$Index,
        [string]$SourceFilePath,
        [string]$SnapshotFileName = ""
    )

    if (![string]::IsNullOrWhiteSpace($SnapshotFileName)) {
        if (!(Test-SnapshotFileNameIsSafe -SnapshotFileName $SnapshotFileName)) {
            throw "Unsafe SnapshotFileName in manifest: $SnapshotFileName"
        }
        $targetName = $SnapshotFileName
    }
    else {
        $safeName = ConvertTo-SnapshotSafeFileName -FilePath $SourceFilePath
        $targetName = ("{0:0000}-{1}.snapshot.json" -f $Index, $safeName)
    }

    $targetPath = Get-NormalizedFullPath -Path (Join-Path $FinalOutputDir $targetName)
    $sourcePath = Get-NormalizedFullPath -Path $SnapshotFile.FullName

    if (!(Test-Path -LiteralPath $sourcePath)) {
        throw "Snapshot disappeared before it could be copied: $sourcePath"
    }

    Wait-SnapshotFileReady -Path $sourcePath -TimeoutSeconds 30

    if ($sourcePath -ieq $targetPath) {
        return $targetPath
    }

    if (Test-Path -LiteralPath $targetPath) {
        Invoke-FileOperationWithRetry `
            -Description "Remove old snapshot $targetPath" `
            -Operation { Remove-Item -LiteralPath $targetPath -Force -ErrorAction Stop }
    }

    Invoke-FileOperationWithRetry `
        -Description "Copy snapshot $sourcePath to $targetPath" `
        -Operation { Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force -ErrorAction Stop }

    Wait-SnapshotFileReady -Path $targetPath -TimeoutSeconds 30
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
        [string]$Status = "Started",
        [string]$StartedAt = "",
        [string]$CompletedAt = "",
        [double]$ElapsedSeconds = 0.0,
        [string]$Elapsed = "",
        [int]$ExpectedSnapshots = 0,
        [int]$ActualSnapshots = 0,
        [object[]]$Timings = @()
    )

    $runInfo = [ordered]@{
        SchemaVersion = 1
        RunName = $RunName
        Manifest = $Manifest.Replace("\", "/")
        Status = $Status
        StartedAt = $StartedAt
        CompletedAt = $CompletedAt
        ElapsedSeconds = $ElapsedSeconds
        Elapsed = $Elapsed
        ExpectedSnapshots = $ExpectedSnapshots
        ActualSnapshots = $ActualSnapshots
        Timings = @($Timings)
    }

    $parent = Split-Path -Parent $Path
    if (![string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $text = $runInfo | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText($Path, ($text + [Environment]::NewLine), $utf8NoBom)
}

function Add-SnapshotTimingRecord {
    param(
        [int]$Index,
        [int]$Count,
        [string]$Path,
        [string]$SnapshotFileName,
        [System.Diagnostics.Stopwatch]$Stopwatch,
        [datetime]$StartedAt = [datetime]::MinValue,
        [string]$Status = "Completed"
    )

    if ($null -eq $Stopwatch) {
        return $null
    }

    if ($Stopwatch.IsRunning) {
        $Stopwatch.Stop()
    }

    $completedAt = Get-Date
    $startedAtUtc = ""
    if ($StartedAt -ne [datetime]::MinValue) {
        $startedAtUtc = $StartedAt.ToUniversalTime().ToString("o")
    }

    $elapsedSeconds = [Math]::Round($Stopwatch.Elapsed.TotalSeconds, 3)
    $record = [ordered]@{
        Index = $Index
        Count = $Count
        Path = $Path.Replace("\", "/")
        SnapshotFileName = $SnapshotFileName
        Status = $Status
        StartedAtUtc = $startedAtUtc
        CompletedAtUtc = $completedAt.ToUniversalTime().ToString("o")
        ElapsedSeconds = $elapsedSeconds
        Elapsed = $Stopwatch.Elapsed.ToString("c")
    }

    $recordObject = [pscustomobject]$record
    [void]$script:snapshotTimingRecords.Add($recordObject)
    Write-Host ("Timing: snapshot [{0}/{1}] {2}: {3:N3}s ({4})" -f $Index, $Count, $Path, $elapsedSeconds, $Status)
    return $recordObject
}

function Set-SnapshotProcessingTime {
    param(
        [string]$Path,
        [object]$TimingRecord,
        [datetime]$RunStartedAt
    )

    if ($null -eq $TimingRecord -or !(Test-Path -LiteralPath $Path)) {
        return
    }

    try {
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        $rawJson = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
        $json = $rawJson | ConvertFrom-Json

        foreach ($propertyName in @("ProcessingTime", "RunTiming")) {
            $property = $json.PSObject.Properties[$propertyName]
            if ($null -ne $property) {
                $json.PSObject.Properties.Remove($propertyName)
            }
        }

        $processingTime = [ordered]@{
            SchemaVersion = 1
            RunStartedAtUtc = $RunStartedAt.ToUniversalTime().ToString("o")
            StartedAtUtc = [string]$TimingRecord.StartedAtUtc
            CompletedAtUtc = [string]$TimingRecord.CompletedAtUtc
            ElapsedSeconds = [double]$TimingRecord.ElapsedSeconds
            Elapsed = [string]$TimingRecord.Elapsed
            Index = [int]$TimingRecord.Index
            Count = [int]$TimingRecord.Count
            Status = [string]$TimingRecord.Status
        }

        $json | Add-Member -MemberType NoteProperty -Name "ProcessingTime" -Value ([pscustomobject]$processingTime)
        $text = $json | ConvertTo-Json -Depth 100
        [System.IO.File]::WriteAllText($Path, ($text + [Environment]::NewLine), $utf8NoBom)
    }
    catch {
        Write-Warning "Could not write processing time to snapshot $Path`: $_"
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

function Get-ManifestInt {
    param(
        [object]$ManifestObject,
        [string]$PropertyName,
        [int]$DefaultValue
    )

    $property = $ManifestObject.PSObject.Properties[$PropertyName]
    if ($null -eq $property -or $null -eq $property.Value) {
        return $DefaultValue
    }

    $value = 0
    if (![int]::TryParse([string]$property.Value, [ref]$value)) {
        throw "Manifest property $PropertyName must be an integer."
    }

    return $value
}

function Get-ManifestEntryProperty {
    param(
        [object]$FileEntry,
        [string[]]$Names
    )

    if ($null -eq $FileEntry) {
        return ""
    }

    foreach ($name in $Names) {
        $property = $FileEntry.PSObject.Properties[$name]
        if ($null -ne $property -and $null -ne $property.Value) {
            return [string]$property.Value
        }
    }

    return ""
}

function Get-ManifestEntryPath {
    param([object]$FileEntry)

    if ($FileEntry -is [string]) {
        return [string]$FileEntry
    }

    $path = Get-ManifestEntryProperty -FileEntry $FileEntry -Names @("Path", "File", "SourceFile")
    if ([string]::IsNullOrWhiteSpace($path)) {
        throw "Manifest file entry is missing Path."
    }

    return $path
}

function Get-ManifestEntrySnapshotFileName {
    param([object]$FileEntry)

    if ($FileEntry -is [string]) {
        return ""
    }

    return Get-ManifestEntryProperty -FileEntry $FileEntry -Names @("SnapshotFileName", "SnapshotName", "OutputName")
}

function Test-SnapshotFileNameIsSafe {
    param([string]$SnapshotFileName)

    if ([string]::IsNullOrWhiteSpace($SnapshotFileName)) {
        return $true
    }

    if ([System.IO.Path]::IsPathRooted($SnapshotFileName)) {
        return $false
    }

    if ($SnapshotFileName.Contains("/") -or $SnapshotFileName.Contains("\")) {
        return $false
    }

    foreach ($c in [System.IO.Path]::GetInvalidFileNameChars()) {
        if ($SnapshotFileName.Contains([string]$c)) {
            return $false
        }
    }

    return $SnapshotFileName.EndsWith(".snapshot.json", [System.StringComparison]::OrdinalIgnoreCase)
}

$repoRoot = Get-RepoRoot
$manifestPath = (Resolve-Path -LiteralPath (Resolve-RepoPath -RepoRoot $repoRoot -Path $Manifest)).Path
$manifestJson = [System.IO.File]::ReadAllText($manifestPath, [System.Text.Encoding]::UTF8) | ConvertFrom-Json

$manifestFreshInstancePerFile = Get-ManifestBoolean -ManifestObject $manifestJson -PropertyName "FreshInstancePerFile"
$useFreshInstancePerFile = $FreshInstancePerFile.IsPresent -or $manifestFreshInstancePerFile
$effectiveMaxWaitSeconds = Get-ManifestInt -ManifestObject $manifestJson -PropertyName "MaxWaitSeconds" -DefaultValue $MaxWaitSeconds
if ($effectiveMaxWaitSeconds -lt 1) {
    throw "MaxWaitSeconds must be at least 1."
}

$finalOutputDir = Get-NormalizedFullPath -Path (Resolve-RepoPath -RepoRoot $repoRoot -Path $OutputDir)
$configFile = Join-Path ([System.IO.Path]::GetTempPath()) "VerilogLanguage.ExportSnapshots.config"
$devenv = Get-DevenvPath -RequestedPath $VisualStudioPath
$runInfoPath = Join-Path $finalOutputDir "run-info.json"
$snapshotRunStartedAt = Get-Date
$snapshotRunStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$snapshotTimingRecords = New-Object System.Collections.Generic.List[object]

Write-Host "Snapshot output: $finalOutputDir"
Write-Host "Visual Studio: $devenv"
Write-Host "Run name: $($manifestJson.RunName)"
Write-Host "Fresh instance per file: $useFreshInstancePerFile"
Write-Host "Max wait seconds: $effectiveMaxWaitSeconds"
Write-Host "Leave Visual Studio open: $($LeaveVisualStudioOpen.IsPresent)"
Write-Host "Skip initial Visual Studio cleanup: $($SkipInitialVisualStudioCleanup.IsPresent)"

try {
    if (!$SkipInitialVisualStudioCleanup.IsPresent) {
        # Start from a known state before clearing output. This avoids stale VS
        # file handles from a failed previous run keeping _work_* files locked.
        Stop-ExperimentalDevenv -RequestedRootSuffix $RootSuffix
    }

    if (Test-Path -LiteralPath $finalOutputDir) {
        Invoke-FileOperationWithRetry `
            -Description "Remove old snapshot output directory $finalOutputDir" `
            -Operation { Remove-Item -LiteralPath $finalOutputDir -Recurse -Force -ErrorAction Stop }
    }
    New-Item -ItemType Directory -Force -Path $finalOutputDir | Out-Null

    Write-SnapshotRunInfo `
        -Path $runInfoPath `
        -RunName ([string]$manifestJson.RunName) `
        -Manifest $Manifest `
        -Status "Started" `
        -StartedAt ($snapshotRunStartedAt.ToString("o")) `
        -ExpectedSnapshots (@($manifestJson.Files).Count)
    Write-Host "Run info: $runInfoPath"

    $index = 0
    $fileCount = @($manifestJson.Files).Count
    foreach ($fileEntry in $manifestJson.Files) {
        $index++
        $fileTimingStartedAt = Get-Date
        $fileStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $file = Get-ManifestEntryPath -FileEntry $fileEntry
        $snapshotFileName = Get-ManifestEntrySnapshotFileName -FileEntry $fileEntry
        $filePath = Resolve-RepoPath -RepoRoot $repoRoot -Path $file
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

        if ([string]::IsNullOrWhiteSpace($snapshotFileName)) {
            Write-Host "Opening [$index/$fileCount] $file"
        }
        else {
            Write-Host "Opening [$index/$fileCount] $file -> $snapshotFileName"
        }
        $startedAt = Get-Date
        $openArguments = "/RootSuffix " + (Quote-CommandLineArgument $RootSuffix) + " /NoSplash " + (Quote-CommandLineArgument $filePath)
        Start-Process -FilePath $devenv -ArgumentList $openArguments -WindowStyle Minimized | Out-Null

        $configuredDelayMs = 0
        [void][int]::TryParse([string]$manifestJson.DelayMs, [ref]$configuredDelayMs)
        $minimumWait = [Math]::Ceiling($configuredDelayMs / 1000.0) + $DelaySeconds + 15
        $deadlineSeconds = [Math]::Max($effectiveMaxWaitSeconds, [int]$minimumWait)
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

            Add-SnapshotTimingRecord `
                -Index $index `
                -Count $fileCount `
                -Path $file `
                -SnapshotFileName $snapshotFileName `
                -Stopwatch $fileStopwatch `
                -StartedAt $fileTimingStartedAt `
                -Status "Failed"

            throw "No snapshot was written after opening $file. The file exists and VS was launched, but no export appeared within $deadlineSeconds seconds."
        }

        if ($useFreshInstancePerFile) {
            Stop-ExperimentalDevenv -RequestedRootSuffix $RootSuffix
        }

        $finalSnapshotPath = Copy-SnapshotToFinalName `
            -SnapshotFile $snapshotFile `
            -FinalOutputDir $finalOutputDir `
            -Index $index `
            -SourceFilePath $filePath `
            -SnapshotFileName $snapshotFileName

        Format-JsonFile -Path $finalSnapshotPath
        Write-Host "Snapshot: $finalSnapshotPath"

        if ($useFreshInstancePerFile) {
            if (Test-Path $activeOutputDir) {
                Invoke-FileOperationWithRetry `
                    -Description "Remove work snapshot directory $activeOutputDir" `
                    -Operation { Remove-Item -LiteralPath $activeOutputDir -Recurse -Force -ErrorAction Stop }
            }
        }
        elseif (!$LeaveVisualStudioOpen.IsPresent) {
            # Close the active document so repeated files in the manifest create a fresh view.
            $closeArguments = "/RootSuffix " + (Quote-CommandLineArgument $RootSuffix) + " /NoSplash /Command File.Close"
            Start-Process -FilePath $devenv -ArgumentList $closeArguments -WindowStyle Minimized | Out-Null
            Start-Sleep -Seconds 1
        }

        $timingRecord = Add-SnapshotTimingRecord `
            -Index $index `
            -Count $fileCount `
            -Path $file `
            -SnapshotFileName (Split-Path -Leaf $finalSnapshotPath) `
            -Stopwatch $fileStopwatch `
            -StartedAt $fileTimingStartedAt

        Set-SnapshotProcessingTime `
            -Path $finalSnapshotPath `
            -TimingRecord $timingRecord `
            -RunStartedAt $snapshotRunStartedAt
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
if ($snapshotRunStopwatch.IsRunning) {
    $snapshotRunStopwatch.Stop()
}
$snapshotCompletedAt = Get-Date

Write-SnapshotRunInfo `
    -Path $runInfoPath `
    -RunName ([string]$manifestJson.RunName) `
    -Manifest $Manifest `
    -Status "Completed" `
    -StartedAt ($snapshotRunStartedAt.ToString("o")) `
    -CompletedAt ($snapshotCompletedAt.ToString("o")) `
    -ElapsedSeconds ([Math]::Round($snapshotRunStopwatch.Elapsed.TotalSeconds, 3)) `
    -Elapsed ($snapshotRunStopwatch.Elapsed.ToString("c")) `
    -ExpectedSnapshots $expectedCount `
    -ActualSnapshots $actualCount `
    -Timings ($snapshotTimingRecords.ToArray())

Write-Host "Snapshots written: $actualCount / expected $expectedCount"
Write-Host ("Timing: snapshot export total: {0:N3}s" -f [Math]::Round($snapshotRunStopwatch.Elapsed.TotalSeconds, 3))

if ($actualCount -lt $expectedCount) {
    throw "Not all snapshots were written. Close the Experimental Instance and rerun."
}
