# Shared snapshot-baseline serialization and portability helpers.
#
# Approved baselines must be written through these functions so every writer
# uses the historical Windows PowerShell JSON layout, CRLF line endings, and
# UTF-8 without BOM. Compare-Snapshots.py deliberately does not serialize JSON
# itself; it delegates baseline updates to Write-SnapshotBaseline.ps1.

Set-StrictMode -Version Latest

function Assert-VleCanonicalPowerShell {
    $edition = [string]$PSVersionTable.PSEdition
    $version = $PSVersionTable.PSVersion

    if ($edition -cne "Desktop" -or
            $version.Major -ne 5 -or
            $version.Minor -ne 1) {
        throw (
            "Approved snapshot baselines must be written with Windows " +
            "PowerShell 5.1. Current host: PSEdition='$edition', " +
            "PSVersion='$version'.")
    }
}

function Read-VleJsonFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $text = [System.IO.File]::ReadAllText(
        $Path,
        [System.Text.Encoding]::UTF8)
    return ($text | ConvertFrom-Json)
}

function Write-VleJsonFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [object]$Value
    )

    $parent = Split-Path -Parent $Path
    if (![string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $text = $Value | ConvertTo-Json -Depth 100

    # Baseline JSON is intentionally CRLF on every Windows machine. Using an
    # explicit newline prevents host differences from turning an otherwise
    # semantic baseline update into a whole-file diff.
    [System.IO.File]::WriteAllText(
        $Path,
        ($text + "`r`n"),
        $utf8NoBom)
}

function Get-VleSnapshotVolatileFields {
    return @(
        "ExtensionVersion",
        "GeneratedAtUtc",
        "GitCommit",
        "ProcessingTime",
        "ProcessingTimeBucket",
        "RunTiming")
}

function Get-VleRunInfoVolatileFields {
    return @(
        "StartedAt",
        "CompletedAt",
        "ElapsedSeconds",
        "Elapsed",
        "Timings",
        "ProcessingTime",
        "ProcessingTimeBucket",
        "RunTiming",
        "CiProcessingTime",
        "CiElapsedSeconds",
        "GitCommit",
        "GitCommitFull")
}

function Get-VleNormalizedSlashPath {
    param([AllowNull()][string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ""
    }

    return $Path.Replace("\", "/")
}

function Get-VleSnapshotRepositoryRoot {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Snapshot
    )

    $filePathProperty = $Snapshot.PSObject.Properties["FilePath"]
    $relativePathProperty = $Snapshot.PSObject.Properties["FileRelativePath"]
    if ($null -eq $filePathProperty -or $null -eq $relativePathProperty) {
        return ""
    }

    $filePath = Get-VleNormalizedSlashPath -Path ([string]$filePathProperty.Value)
    $relativePath = Get-VleNormalizedSlashPath -Path ([string]$relativePathProperty.Value)
    if ([string]::IsNullOrWhiteSpace($filePath) -or
            [string]::IsNullOrWhiteSpace($relativePath)) {
        return ""
    }

    if (!$filePath.EndsWith(
            $relativePath,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        return ""
    }

    return $filePath.Substring(
        0,
        $filePath.Length - $relativePath.Length).TrimEnd([char[]]@("/"))
}

function Get-VlePortablePathText {
    param(
        [AllowNull()][string]$Path,
        [AllowNull()][string]$RepositoryRoot
    )

    $normalized = Get-VleNormalizedSlashPath -Path $Path
    $root = (Get-VleNormalizedSlashPath -Path $RepositoryRoot).TrimEnd(
        [char[]]@("/"))

    if (![string]::IsNullOrWhiteSpace($root) -and
            $normalized.StartsWith(
                ($root + "/"),
                [System.StringComparison]::OrdinalIgnoreCase)) {
        $normalized = $normalized.Substring($root.Length + 1)
    }
    else {
        $marker = "/TestFiles/"
        $markerIndex = $normalized.IndexOf(
            $marker,
            [System.StringComparison]::OrdinalIgnoreCase)
        if ($markerIndex -ge 0) {
            $normalized = $normalized.Substring($markerIndex + 1)
        }
    }

    return $normalized.TrimStart([char[]]@("/"))
}

function ConvertTo-VlePortableHoverText {
    param(
        [AllowNull()][string]$HoverText,
        [AllowNull()][string]$RepositoryRoot
    )

    if ([string]::IsNullOrEmpty($HoverText)) {
        return $HoverText
    }

    # Capture newline delimiters so the string value itself is not changed from
    # CRLF to LF while repository roots are removed from File: lines.
    $parts = [System.Text.RegularExpressions.Regex]::Split(
        $HoverText,
        "(`r`n|`n|`r)")

    $builder = New-Object System.Text.StringBuilder
    for ($index = 0; $index -lt $parts.Count; $index++) {
        $part = [string]$parts[$index]
        if (($index % 2) -eq 0 -and
                $part.StartsWith(
                    "File:",
                    [System.StringComparison]::Ordinal)) {
            $pathText = $part.Substring(5).Trim()
            $portablePath = Get-VlePortablePathText `
                -Path $pathText `
                -RepositoryRoot $RepositoryRoot
            $part = "File: $portablePath"
        }

        [void]$builder.Append($part)
    }

    return $builder.ToString()
}

function Remove-VleJsonProperty {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Object,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -ne $property) {
        $Object.PSObject.Properties.Remove($Name)
    }
}

function Copy-VleJsonObject {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Value
    )

    return (($Value | ConvertTo-Json -Depth 100) | ConvertFrom-Json)
}

function ConvertTo-VlePortableSnapshot {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Snapshot
    )

    # Clone the parsed object so callers can retain the unmodified current
    # snapshot for comparisons and diagnostics.
    $portable = Copy-VleJsonObject -Value $Snapshot
    $repositoryRoot = Get-VleSnapshotRepositoryRoot -Snapshot $portable

    foreach ($field in @(Get-VleSnapshotVolatileFields)) {
        Remove-VleJsonProperty -Object $portable -Name $field
    }

    $relativeProperty = $portable.PSObject.Properties["FileRelativePath"]
    $filePathProperty = $portable.PSObject.Properties["FilePath"]
    $sourcePath = ""
    if ($null -ne $relativeProperty -and
            ![string]::IsNullOrWhiteSpace([string]$relativeProperty.Value)) {
        $sourcePath = [string]$relativeProperty.Value
    }
    elseif ($null -ne $filePathProperty) {
        $sourcePath = [string]$filePathProperty.Value
    }

    $portablePath = Get-VlePortablePathText `
        -Path $sourcePath `
        -RepositoryRoot $repositoryRoot
    if (![string]::IsNullOrWhiteSpace($portablePath)) {
        if ($null -ne $relativeProperty) {
            $relativeProperty.Value = $portablePath
        }
        else {
            $portable | Add-Member `
                -MemberType NoteProperty `
                -Name "FileRelativePath" `
                -Value $portablePath
        }

        if ($null -ne $filePathProperty) {
            $filePathProperty.Value = $portablePath
        }
        else {
            $portable | Add-Member `
                -MemberType NoteProperty `
                -Name "FilePath" `
                -Value $portablePath
        }
    }

    foreach ($collectionName in @("Tags", "Symbols")) {
        $collectionProperty = $portable.PSObject.Properties[$collectionName]
        if ($null -eq $collectionProperty -or
                $null -eq $collectionProperty.Value) {
            continue
        }

        foreach ($item in @($collectionProperty.Value)) {
            if ($null -eq $item) {
                continue
            }

            $hoverProperty = $item.PSObject.Properties["HoverText"]
            if ($null -ne $hoverProperty -and $null -ne $hoverProperty.Value) {
                $hoverProperty.Value = ConvertTo-VlePortableHoverText `
                    -HoverText ([string]$hoverProperty.Value) `
                    -RepositoryRoot $repositoryRoot
            }
        }
    }

    return $portable
}

function ConvertTo-VleStableRunInfo {
    param(
        [Parameter(Mandatory = $true)]
        [object]$RunInfo
    )

    $stable = Copy-VleJsonObject -Value $RunInfo
    foreach ($field in @(Get-VleRunInfoVolatileFields)) {
        Remove-VleJsonProperty -Object $stable -Name $field
    }

    $manifestProperty = $stable.PSObject.Properties["Manifest"]
    if ($null -ne $manifestProperty -and $null -ne $manifestProperty.Value) {
        $manifestProperty.Value = Get-VleNormalizedSlashPath `
            -Path ([string]$manifestProperty.Value)
    }

    return $stable
}

function Get-VleRelativeFilePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $rootPath = [System.IO.Path]::GetFullPath($Root)
    if (!$rootPath.EndsWith(
            [System.IO.Path]::DirectorySeparatorChar.ToString())) {
        $rootPath += [System.IO.Path]::DirectorySeparatorChar
    }

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $relativeUri = (New-Object System.Uri($rootPath)).MakeRelativeUri(
        (New-Object System.Uri($fullPath)))
    return [System.Uri]::UnescapeDataString($relativeUri.ToString()).Replace(
        "/",
        [System.IO.Path]::DirectorySeparatorChar.ToString())
}

function Write-VlePortableSnapshotFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourcePath,

        [Parameter(Mandatory = $true)]
        [string]$DestinationPath
    )

    $snapshot = Read-VleJsonFile -Path $SourcePath
    $portable = ConvertTo-VlePortableSnapshot -Snapshot $snapshot
    Write-VleJsonFile -Path $DestinationPath -Value $portable
}

function Get-VleRequiredRunInfoValue {
    param(
        [Parameter(Mandatory = $true)]
        [object]$RunInfo,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $property = $RunInfo.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        throw "Current snapshot run-info.json is missing required field '$Name'."
    }

    return $property.Value
}

function Assert-VleRunInfoReadyForBaseline {
    param(
        [Parameter(Mandatory = $true)]
        [object]$RunInfo,

        [Parameter(Mandatory = $true)]
        [int]$SnapshotCount
    )

    $status = [string](Get-VleRequiredRunInfoValue `
        -RunInfo $RunInfo `
        -Name "Status")
    if ($status -cne "Completed") {
        throw "Current snapshot run-info.json status is '$status', expected 'Completed'."
    }

    foreach ($name in @(
            "ExpectedSnapshots",
            "ActualSnapshots",
            "SnapshotCount")) {
        $value = [int](Get-VleRequiredRunInfoValue `
            -RunInfo $RunInfo `
            -Name $name)
        if ($value -ne $SnapshotCount) {
            throw (
                "Current snapshot run-info.json $name is $value, expected " +
                "$SnapshotCount.")
        }
    }

    foreach ($name in @(
            "VsixManifestVersion",
            "AssemblyVersion",
            "AssemblyFileVersion",
            "AssemblyInformationalVersion",
            "ProvideMenuResourceName",
            "ProvideMenuResourceVersion")) {
        $value = [string](Get-VleRequiredRunInfoValue `
            -RunInfo $RunInfo `
            -Name $name)
        if ([string]::IsNullOrWhiteSpace($value)) {
            throw "Current snapshot run-info.json field '$name' is empty."
        }
    }
}

function Open-VleBaselineUpdateLock {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $stream = $null
    try {
        $stream = [System.IO.File]::Open(
            $Path,
            [System.IO.FileMode]::OpenOrCreate,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::None)

        $lockText = "PID=$PID`r`nStarted=$((Get-Date).ToString('o'))`r`n"
        $lockBytes = [System.Text.Encoding]::UTF8.GetBytes($lockText)
        $stream.SetLength(0)
        $stream.Write($lockBytes, 0, $lockBytes.Length)
        $stream.Flush()
        return $stream
    }
    catch {
        if ($null -ne $stream) {
            $stream.Dispose()
        }

        throw (
            "Could not acquire the snapshot baseline update lock '$Path': " +
            $_.Exception.Message)
    }
}

function Write-VleSnapshotBaseline {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CurrentDirectory,

        [Parameter(Mandatory = $true)]
        [string]$BaselineDirectory
    )

    Assert-VleCanonicalPowerShell

    $currentRoot = [System.IO.Path]::GetFullPath($CurrentDirectory)
    $baselineRoot = [System.IO.Path]::GetFullPath($BaselineDirectory)
    $parent = Split-Path -Parent $baselineRoot
    $leaf = Split-Path -Leaf $baselineRoot
    New-Item -ItemType Directory -Force -Path $parent | Out-Null

    $backupRoot = Join-Path $parent ".$leaf.old-update"
    $lockPath = Join-Path $parent ".$leaf.update.lock"
    $operationId = "{0}-{1}" -f $PID, [System.Guid]::NewGuid().ToString("N")
    $stagingRoot = Join-Path $parent ".$leaf.tmp-update.$operationId"
    $lockStream = $null

    try {
        $lockStream = Open-VleBaselineUpdateLock -Path $lockPath

        # Recover a baseline left in the backup location by a process that was
        # terminated between the two directory moves.
        if (!(Test-Path -LiteralPath $baselineRoot) -and
                (Test-Path -LiteralPath $backupRoot)) {
            Move-Item -LiteralPath $backupRoot -Destination $baselineRoot
        }
        elseif ((Test-Path -LiteralPath $baselineRoot) -and
                (Test-Path -LiteralPath $backupRoot)) {
            Remove-Item `
                -LiteralPath $backupRoot `
                -Recurse `
                -Force `
                -ErrorAction Stop
        }

        # No other writer can be active while the lock is held, so matching
        # staging directories are remnants of interrupted older runs.
        foreach ($staleStaging in @(Get-ChildItem `
                -LiteralPath $parent `
                -Directory `
                -Filter ".$leaf.tmp-update*" `
                -ErrorAction SilentlyContinue)) {
            Remove-Item `
                -LiteralPath $staleStaging.FullName `
                -Recurse `
                -Force `
                -ErrorAction Stop
        }

        if (!(Test-Path -LiteralPath $currentRoot -PathType Container)) {
            throw "Current snapshot directory not found: $currentRoot"
        }

        $currentSnapshots = @(Get-ChildItem `
                -LiteralPath $currentRoot `
                -Filter "*.snapshot.json" `
                -File `
                -Recurse `
                -ErrorAction Stop |
            Sort-Object FullName)
        if ($currentSnapshots.Count -eq 0) {
            throw "No current snapshots found in $currentRoot"
        }

        $runInfoSource = Join-Path $currentRoot "run-info.json"
        if (!(Test-Path -LiteralPath $runInfoSource -PathType Leaf)) {
            throw "Current snapshot run-info.json not found: $runInfoSource"
        }

        $runInfo = Read-VleJsonFile -Path $runInfoSource
        Assert-VleRunInfoReadyForBaseline `
            -RunInfo $runInfo `
            -SnapshotCount $currentSnapshots.Count
        $stableRunInfo = ConvertTo-VleStableRunInfo -RunInfo $runInfo

        New-Item `
            -ItemType Directory `
            -Force `
            -Path $stagingRoot |
            Out-Null

        $baselineMoved = $false
        try {
            foreach ($source in $currentSnapshots) {
                $relativePath = Get-VleRelativeFilePath `
                    -Root $currentRoot `
                    -Path $source.FullName
                $destination = Join-Path $stagingRoot $relativePath
                Write-VlePortableSnapshotFile `
                    -SourcePath $source.FullName `
                    -DestinationPath $destination
            }

            Write-VleJsonFile `
                -Path (Join-Path $stagingRoot "run-info.json") `
                -Value $stableRunInfo

            $stagedSnapshots = @(Get-ChildItem `
                    -LiteralPath $stagingRoot `
                    -Filter "*.snapshot.json" `
                    -File `
                    -Recurse `
                    -ErrorAction Stop)
            if ($stagedSnapshots.Count -ne $currentSnapshots.Count) {
                $message = (
                    "Staged baseline snapshot count mismatch: expected {0}, " +
                    "found {1}.") -f $currentSnapshots.Count, $stagedSnapshots.Count
                throw $message
            }

            foreach ($stagedPath in $stagedSnapshots) {
                $stagedSnapshot = Read-VleJsonFile -Path $stagedPath.FullName
                $stagedFilePath = [string]$stagedSnapshot.FilePath
                $stagedRelativePath = [string]$stagedSnapshot.FileRelativePath
                if ([System.IO.Path]::IsPathRooted($stagedFilePath) -or
                        $stagedFilePath -cne $stagedRelativePath -or
                        $stagedFilePath.Contains("\")) {
                    throw (
                        "Staged snapshot contains a non-portable path: " +
                        $stagedPath.FullName)
                }

                foreach ($field in @(Get-VleSnapshotVolatileFields)) {
                    if ($null -ne $stagedSnapshot.PSObject.Properties[$field]) {
                        throw (
                            "Staged snapshot retained field '$field': " +
                            $stagedPath.FullName)
                    }
                }

                foreach ($collectionName in @("Tags", "Symbols")) {
                    $collection = $stagedSnapshot.PSObject.Properties[
                        $collectionName]
                    if ($null -eq $collection -or
                            $null -eq $collection.Value) {
                        continue
                    }

                    foreach ($item in @($collection.Value)) {
                        $hover = $item.PSObject.Properties["HoverText"]
                        if ($null -ne $hover -and
                                ([string]$hover.Value -match
                                    "(?im)^File:\s*(?:[A-Z]:[\\/]|\\\\)")) {
                            throw (
                                "Staged snapshot retained an absolute hover " +
                                "path: " + $stagedPath.FullName)
                        }
                    }
                }
            }

            $stagedRunInfoPath = Join-Path $stagingRoot "run-info.json"
            $stagedRunInfo = Read-VleJsonFile -Path $stagedRunInfoPath
            Assert-VleRunInfoReadyForBaseline `
                -RunInfo $stagedRunInfo `
                -SnapshotCount $stagedSnapshots.Count
            foreach ($field in @(Get-VleRunInfoVolatileFields)) {
                if ($null -ne $stagedRunInfo.PSObject.Properties[$field]) {
                    throw "Staged run-info.json retained field '$field'."
                }
            }

            if (Test-Path -LiteralPath $baselineRoot) {
                Move-Item -LiteralPath $baselineRoot -Destination $backupRoot
                $baselineMoved = $true
            }

            Move-Item -LiteralPath $stagingRoot -Destination $baselineRoot

            if ($baselineMoved -and
                    (Test-Path -LiteralPath $backupRoot)) {
                Remove-Item `
                    -LiteralPath $backupRoot `
                    -Recurse `
                    -Force `
                    -ErrorAction Stop
            }
        }
        catch {
            if (!(Test-Path -LiteralPath $baselineRoot) -and
                    (Test-Path -LiteralPath $backupRoot)) {
                Move-Item -LiteralPath $backupRoot -Destination $baselineRoot
            }

            Remove-Item `
                -LiteralPath $stagingRoot `
                -Recurse `
                -Force `
                -ErrorAction SilentlyContinue
            throw
        }
    }
    finally {
        if ($null -ne $lockStream) {
            $lockStream.Dispose()
        }

        Remove-Item `
            -LiteralPath $lockPath `
            -Force `
            -ErrorAction SilentlyContinue
    }
}
