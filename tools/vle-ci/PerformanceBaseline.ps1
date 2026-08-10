Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$baselineHelperPath = Join-Path $PSScriptRoot "SnapshotBaseline.ps1"
if ($null -eq (Get-Command Read-VleJsonFile -ErrorAction SilentlyContinue)) {
    if (!(Test-Path -LiteralPath $baselineHelperPath -PathType Leaf)) {
        throw "Snapshot baseline helpers not found: $baselineHelperPath"
    }

    . $baselineHelperPath
}

function Get-VlePerformancePropertyValue {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Object,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [object]$DefaultValue = $null
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        return $DefaultValue
    }

    return $property.Value
}

function Get-VlePerformanceTimingRecord {
    param(
        [Parameter(Mandatory = $true)]
        [object]$RunInfo,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $runTiming = Get-VlePerformancePropertyValue `
        -Object $RunInfo `
        -Name "RunTiming" `
        -DefaultValue @()

    foreach ($record in @($runTiming)) {
        if ([string](Get-VlePerformancePropertyValue `
                -Object $record `
                -Name "Name" `
                -DefaultValue "") -ceq $Name) {
            return $record
        }
    }

    return $null
}

function Get-VlePerformanceElapsedSeconds {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Record,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $value = Get-VlePerformancePropertyValue `
        -Object $Record `
        -Name "ElapsedSeconds"
    if ($null -eq $value) {
        throw "$Description does not contain ElapsedSeconds. Rerun ci-check.ps1 with the updated timing exporter."
    }

    return [double]$value
}

function Assert-VlePerformanceRunInfoReady {
    param(
        [Parameter(Mandatory = $true)]
        [object]$RunInfo
    )

    $status = [string](Get-VlePerformancePropertyValue `
        -Object $RunInfo `
        -Name "Status" `
        -DefaultValue "")
    if ($status -cne "Completed") {
        throw "Current run-info.json status is '$status', expected 'Completed'."
    }

    $expected = [int](Get-VlePerformancePropertyValue `
        -Object $RunInfo `
        -Name "ExpectedSnapshots" `
        -DefaultValue 0)
    $actual = [int](Get-VlePerformancePropertyValue `
        -Object $RunInfo `
        -Name "ActualSnapshots" `
        -DefaultValue 0)
    if ($expected -lt 1 -or $actual -ne $expected) {
        throw "Current run-info.json snapshot count is $actual / $expected; a complete run is required."
    }

    $snapshotExport = Get-VlePerformanceTimingRecord `
        -RunInfo $RunInfo `
        -Name "Snapshot export"
    if ($null -eq $snapshotExport) {
        throw "Current run-info.json does not contain the 'Snapshot export' CI timing record."
    }
    $null = Get-VlePerformanceElapsedSeconds `
        -Record $snapshotExport `
        -Description "Snapshot export timing"

    $total = Get-VlePerformanceTimingRecord `
        -RunInfo $RunInfo `
        -Name "Total"
    if ($null -eq $total) {
        throw "Current run-info.json does not contain the 'Total' CI timing record."
    }
    $null = Get-VlePerformanceElapsedSeconds `
        -Record $total `
        -Description "Total CI timing"

    $timings = @(Get-VlePerformancePropertyValue `
            -Object $RunInfo `
            -Name "Timings" `
            -DefaultValue @())
    if ($timings.Count -ne $expected) {
        throw "Current run-info.json contains $($timings.Count) per-file timing records, expected $expected."
    }

    foreach ($timing in $timings) {
        $path = [string](Get-VlePerformancePropertyValue `
            -Object $timing `
            -Name "Path" `
            -DefaultValue "")
        if ([string]::IsNullOrWhiteSpace($path)) {
            throw "A per-file timing record is missing Path."
        }

        $timingStatus = [string](Get-VlePerformancePropertyValue `
            -Object $timing `
            -Name "Status" `
            -DefaultValue "")
        if ($timingStatus -cne "Completed") {
            throw "Per-file timing for '$path' has status '$timingStatus', expected 'Completed'."
        }

        $null = Get-VlePerformanceElapsedSeconds `
            -Record $timing `
            -Description "Per-file timing for '$path'"
    }
}

function ConvertTo-VlePerformanceBaseline {
    param(
        [Parameter(Mandatory = $true)]
        [object]$RunInfo
    )

    Assert-VlePerformanceRunInfoReady -RunInfo $RunInfo

    $snapshotExport = Get-VlePerformanceTimingRecord `
        -RunInfo $RunInfo `
        -Name "Snapshot export"
    $total = Get-VlePerformanceTimingRecord `
        -RunInfo $RunInfo `
        -Name "Total"

    $perFile = New-Object System.Collections.Generic.List[object]
    foreach ($timing in @(Get-VlePerformancePropertyValue `
            -Object $RunInfo `
            -Name "Timings" `
            -DefaultValue @())) {
        $perFile.Add([pscustomobject][ordered]@{
            Index = [int](Get-VlePerformancePropertyValue -Object $timing -Name "Index" -DefaultValue 0)
            Path = ([string](Get-VlePerformancePropertyValue -Object $timing -Name "Path" -DefaultValue "")).Replace("\", "/")
            SnapshotFileName = [string](Get-VlePerformancePropertyValue -Object $timing -Name "SnapshotFileName" -DefaultValue "")
            ElapsedSeconds = [Math]::Round((Get-VlePerformanceElapsedSeconds -Record $timing -Description "Per-file timing"), 3)
            ElapsedBucket = [string](Get-VlePerformancePropertyValue -Object $timing -Name "ElapsedBucket" -DefaultValue "")
        })
    }

    $source = [ordered]@{}
    foreach ($name in @(
            "VsixManifestVersion",
            "AssemblyVersion",
            "AssemblyFileVersion",
            "AssemblyInformationalVersion",
            "GitCommit")) {
        $value = Get-VlePerformancePropertyValue `
            -Object $RunInfo `
            -Name $name
        if ($null -ne $value -and ![string]::IsNullOrWhiteSpace([string]$value)) {
            $source[$name] = $value
        }
    }

    return [pscustomobject][ordered]@{
        SchemaVersion = 1
        RunName = [string](Get-VlePerformancePropertyValue -Object $RunInfo -Name "RunName" -DefaultValue "")
        Manifest = ([string](Get-VlePerformancePropertyValue -Object $RunInfo -Name "Manifest" -DefaultValue "")).Replace("\", "/")
        ExpectedSnapshots = [int](Get-VlePerformancePropertyValue -Object $RunInfo -Name "ExpectedSnapshots" -DefaultValue 0)
        SnapshotCount = [int](Get-VlePerformancePropertyValue `
            -Object $RunInfo `
            -Name "SnapshotCount" `
            -DefaultValue (Get-VlePerformancePropertyValue -Object $RunInfo -Name "ActualSnapshots" -DefaultValue 0))
        Source = [pscustomobject]$source
        Metrics = [pscustomobject][ordered]@{
            SnapshotExportSeconds = [Math]::Round((Get-VlePerformanceElapsedSeconds -Record $snapshotExport -Description "Snapshot export timing"), 3)
            SnapshotExportBucket = [string](Get-VlePerformancePropertyValue -Object $snapshotExport -Name "ElapsedBucket" -DefaultValue "")
            TotalCiSeconds = [Math]::Round((Get-VlePerformanceElapsedSeconds -Record $total -Description "Total CI timing"), 3)
            TotalCiBucket = [string](Get-VlePerformancePropertyValue -Object $total -Name "ElapsedBucket" -DefaultValue "")
        }
        Timings = @($perFile.ToArray() | Sort-Object Index, Path)
    }
}

function Write-VlePerformanceBaseline {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CurrentRunInfoPath,

        [Parameter(Mandatory = $true)]
        [string]$DestinationPath
    )

    Assert-VleCanonicalPowerShell

    if (!(Test-Path -LiteralPath $CurrentRunInfoPath -PathType Leaf)) {
        throw "Current run-info.json not found: $CurrentRunInfoPath"
    }

    $runInfo = Read-VleJsonFile -Path $CurrentRunInfoPath
    $baseline = ConvertTo-VlePerformanceBaseline -RunInfo $runInfo

    $destinationFullPath = [System.IO.Path]::GetFullPath($DestinationPath)
    $destinationParent = Split-Path -Parent $destinationFullPath
    New-Item -ItemType Directory -Force -Path $destinationParent | Out-Null
    $temporaryPath = Join-Path `
        $destinationParent `
        (".{0}.tmp.{1}.{2}" -f `
            (Split-Path -Leaf $destinationFullPath),
            $PID,
            [System.Guid]::NewGuid().ToString("N"))

    try {
        Write-VleJsonFile -Path $temporaryPath -Value $baseline
        if (Test-Path -LiteralPath $destinationFullPath -PathType Leaf) {
            [System.IO.File]::Replace(
                $temporaryPath,
                $destinationFullPath,
                $null)
        }
        else {
            [System.IO.File]::Move($temporaryPath, $destinationFullPath)
        }
    }
    finally {
        Remove-Item `
            -LiteralPath $temporaryPath `
            -Force `
            -ErrorAction SilentlyContinue
    }

    Write-Host "Performance baseline written: $destinationFullPath"
    Write-Host ("  Snapshot export: {0:N3}s" -f $baseline.Metrics.SnapshotExportSeconds)
    Write-Host ("  Total CI:        {0:N3}s" -f $baseline.Metrics.TotalCiSeconds)
    Write-Host ("  Per-file timings: {0}" -f @($baseline.Timings).Count)
}

function Get-VlePerformanceResult {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [double]$BaselineSeconds,

        [Parameter(Mandatory = $true)]
        [double]$CurrentSeconds,

        [Parameter(Mandatory = $true)]
        [double]$SamePercent,

        [Parameter(Mandatory = $true)]
        [double]$RegressionPercent,

        [Parameter(Mandatory = $true)]
        [double]$MinimumRegressionSeconds
    )

    $deltaSeconds = $CurrentSeconds - $BaselineSeconds
    $deltaPercent = if ($BaselineSeconds -gt 0.0) {
        ($deltaSeconds / $BaselineSeconds) * 100.0
    }
    elseif ($CurrentSeconds -eq 0.0) {
        0.0
    }
    else {
        100.0
    }

    $sameTolerance = [Math]::Max(1.0, $BaselineSeconds * ($SamePercent / 100.0))
    $regressionThreshold = [Math]::Max(
        $MinimumRegressionSeconds,
        $BaselineSeconds * ($RegressionPercent / 100.0))

    $status = "Same"
    if ($deltaSeconds -lt (-1.0 * $sameTolerance)) {
        $status = "Better"
    }
    elseif ($deltaSeconds -gt $sameTolerance) {
        $status = "Worse"
    }

    return [pscustomobject][ordered]@{
        Name = $Name
        BaselineSeconds = [Math]::Round($BaselineSeconds, 3)
        CurrentSeconds = [Math]::Round($CurrentSeconds, 3)
        DeltaSeconds = [Math]::Round($deltaSeconds, 3)
        DeltaPercent = [Math]::Round($deltaPercent, 1)
        Status = $status
        IsRegression = ($deltaSeconds -gt $regressionThreshold)
    }
}

function Write-VlePerformanceResultLine {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Result,

        [int]$NameWidth = 22
    )

    $deltaText = "{0:+0.0;-0.0;0.0}%" -f [double]$Result.DeltaPercent
    $line = "  {0,-$NameWidth} {1,9:N3}s -> {2,9:N3}s  {3,-6} ({4})" -f `
        [string]$Result.Name,
        [double]$Result.BaselineSeconds,
        [double]$Result.CurrentSeconds,
        ([string]$Result.Status).ToUpperInvariant(),
        $deltaText
    Write-Host $line
}

function Compare-VlePerformanceBaseline {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CurrentRunInfoPath,

        [Parameter(Mandatory = $true)]
        [string]$BaselinePath,

        [double]$SamePercent = 10.0,
        [double]$RegressionPercent = 25.0,
        [double]$MinimumRegressionSeconds = 10.0,
        [int]$MaximumPerFileResults = 10,
        [switch]$FailOnRegression
    )

    if ($SamePercent -lt 0.0) {
        throw "SamePercent cannot be negative."
    }
    if ($RegressionPercent -le $SamePercent) {
        throw "RegressionPercent must be greater than SamePercent."
    }
    if ($MinimumRegressionSeconds -lt 0.0) {
        throw "MinimumRegressionSeconds cannot be negative."
    }
    if ($MaximumPerFileResults -lt 0) {
        throw "MaximumPerFileResults cannot be negative."
    }

    if (!(Test-Path -LiteralPath $CurrentRunInfoPath -PathType Leaf)) {
        throw "Current run-info.json not found: $CurrentRunInfoPath"
    }
    if (!(Test-Path -LiteralPath $BaselinePath -PathType Leaf)) {
        throw "Performance baseline not found: $BaselinePath"
    }

    $runInfo = Read-VleJsonFile -Path $CurrentRunInfoPath
    Assert-VlePerformanceRunInfoReady -RunInfo $runInfo
    $baseline = Read-VleJsonFile -Path $BaselinePath

    $schemaVersion = [int](Get-VlePerformancePropertyValue `
        -Object $baseline `
        -Name "SchemaVersion" `
        -DefaultValue 0)
    if ($schemaVersion -ne 1) {
        throw "Unsupported performance baseline schema version: $schemaVersion"
    }

    $baselineRunName = [string](Get-VlePerformancePropertyValue -Object $baseline -Name "RunName" -DefaultValue "")
    $currentRunName = [string](Get-VlePerformancePropertyValue -Object $runInfo -Name "RunName" -DefaultValue "")
    if ($baselineRunName -cne $currentRunName) {
        throw "Performance baseline run name '$baselineRunName' does not match current run '$currentRunName'."
    }

    $baselineExpected = [int](Get-VlePerformancePropertyValue -Object $baseline -Name "ExpectedSnapshots" -DefaultValue 0)
    $currentExpected = [int](Get-VlePerformancePropertyValue -Object $runInfo -Name "ExpectedSnapshots" -DefaultValue 0)
    if ($baselineExpected -ne $currentExpected) {
        Write-Warning (
            "Performance baseline expects $baselineExpected snapshots; " +
            "the current run expects $currentExpected. New or removed files " +
            "will be reported separately.")
    }

    $baselineMetrics = Get-VlePerformancePropertyValue -Object $baseline -Name "Metrics"
    if ($null -eq $baselineMetrics) {
        throw "Performance baseline does not contain Metrics."
    }

    $baselineSnapshotExportSeconds = [double](Get-VlePerformancePropertyValue `
        -Object $baselineMetrics `
        -Name "SnapshotExportSeconds" `
        -DefaultValue 0.0)
    $baselineTotalCiSeconds = [double](Get-VlePerformancePropertyValue `
        -Object $baselineMetrics `
        -Name "TotalCiSeconds" `
        -DefaultValue 0.0)
    if ($baselineSnapshotExportSeconds -le 0.0 -or $baselineTotalCiSeconds -le 0.0) {
        throw "Performance baseline metrics must contain positive elapsed seconds."
    }

    $snapshotExport = Get-VlePerformanceTimingRecord -RunInfo $runInfo -Name "Snapshot export"
    $total = Get-VlePerformanceTimingRecord -RunInfo $runInfo -Name "Total"

    $summaryResults = @(
        (Get-VlePerformanceResult `
            -Name "Snapshot export" `
            -BaselineSeconds $baselineSnapshotExportSeconds `
            -CurrentSeconds (Get-VlePerformanceElapsedSeconds -Record $snapshotExport -Description "Snapshot export timing") `
            -SamePercent $SamePercent `
            -RegressionPercent $RegressionPercent `
            -MinimumRegressionSeconds $MinimumRegressionSeconds)
        (Get-VlePerformanceResult `
            -Name "Total CI" `
            -BaselineSeconds $baselineTotalCiSeconds `
            -CurrentSeconds (Get-VlePerformanceElapsedSeconds -Record $total -Description "Total CI timing") `
            -SamePercent $SamePercent `
            -RegressionPercent $RegressionPercent `
            -MinimumRegressionSeconds $MinimumRegressionSeconds)
    )

    Write-Host "Performance comparison:"
    foreach ($result in $summaryResults) {
        Write-VlePerformanceResultLine -Result $result
    }

    $baselineByPath = @{}
    foreach ($timing in @(Get-VlePerformancePropertyValue -Object $baseline -Name "Timings" -DefaultValue @())) {
        $path = [string](Get-VlePerformancePropertyValue -Object $timing -Name "Path" -DefaultValue "")
        if (![string]::IsNullOrWhiteSpace($path)) {
            $baselineByPath[$path.Replace("\", "/")] = $timing
        }
    }

    $perFileResults = New-Object System.Collections.Generic.List[object]
    $newPaths = New-Object System.Collections.Generic.List[string]
    foreach ($timing in @(Get-VlePerformancePropertyValue -Object $runInfo -Name "Timings" -DefaultValue @())) {
        $path = ([string](Get-VlePerformancePropertyValue -Object $timing -Name "Path" -DefaultValue "")).Replace("\", "/")
        if (!$baselineByPath.ContainsKey($path)) {
            [void]$newPaths.Add($path)
            continue
        }

        $baselineTiming = $baselineByPath[$path]
        $result = Get-VlePerformanceResult `
            -Name $path `
            -BaselineSeconds (Get-VlePerformanceElapsedSeconds -Record $baselineTiming -Description "Baseline per-file timing for '$path'") `
            -CurrentSeconds (Get-VlePerformanceElapsedSeconds -Record $timing -Description "Per-file timing for '$path'") `
            -SamePercent $SamePercent `
            -RegressionPercent $RegressionPercent `
            -MinimumRegressionSeconds $MinimumRegressionSeconds
        [void]$perFileResults.Add($result)
        [void]$baselineByPath.Remove($path)
    }

    $worse = @()
    if ($MaximumPerFileResults -gt 0) {
        $worse = @($perFileResults.ToArray() |
            Where-Object { $_.Status -eq "Worse" } |
            Sort-Object DeltaSeconds -Descending |
            Select-Object -First $MaximumPerFileResults)
    }
    if ($worse.Count -gt 0) {
        Write-Host "Slowest per-file regressions:"
        foreach ($result in $worse) {
            Write-VlePerformanceResultLine -Result $result -NameWidth 48
        }
    }

    $better = @()
    if ($MaximumPerFileResults -gt 0) {
        $better = @($perFileResults.ToArray() |
            Where-Object { $_.Status -eq "Better" } |
            Sort-Object DeltaSeconds |
            Select-Object -First ([Math]::Min(5, $MaximumPerFileResults)))
    }
    if ($better.Count -gt 0) {
        Write-Host "Largest per-file improvements:"
        foreach ($result in $better) {
            Write-VlePerformanceResultLine -Result $result -NameWidth 48
        }
    }

    if ($newPaths.Count -gt 0) {
        Write-Host "New files without a performance baseline: $($newPaths.Count)"
    }
    if ($baselineByPath.Count -gt 0) {
        Write-Host "Baseline files missing from the current run: $($baselineByPath.Count)"
    }

    $allResults = @($summaryResults) + @($perFileResults.ToArray())
    $regressions = @($allResults | Where-Object { $_.IsRegression })
    if ($regressions.Count -gt 0) {
        Write-Warning ("Performance regression threshold exceeded by {0} metric(s)." -f $regressions.Count)
    }
    else {
        Write-Host "Performance regression threshold: passed."
    }

    return [pscustomobject][ordered]@{
        Passed = (!$FailOnRegression.IsPresent -or $regressions.Count -eq 0)
        RegressionCount = $regressions.Count
        SummaryResults = $summaryResults
        PerFileResults = @($perFileResults.ToArray())
        NewPathCount = $newPaths.Count
        MissingPathCount = $baselineByPath.Count
    }
}
