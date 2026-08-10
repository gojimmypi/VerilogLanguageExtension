<#
.SYNOPSIS
    Fast self-test for the canonical snapshot-baseline writer.
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "SnapshotBaseline.ps1")

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (!$Condition) {
        throw $Message
    }
}

function Assert-CrlfUtf8NoBom {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $hasBom = $bytes.Length -ge 3 -and
        $bytes[0] -eq 0xEF -and
        $bytes[1] -eq 0xBB -and
        $bytes[2] -eq 0xBF
    Assert-True `
        -Condition (!$hasBom) `
        -Message "Baseline JSON contains a UTF-8 BOM: $Path"

    $text = [System.IO.File]::ReadAllText(
        $Path,
        [System.Text.Encoding]::UTF8)
    $withoutCrlf = $text.Replace("`r`n", "")
    Assert-True `
        -Condition (!$withoutCrlf.Contains("`n") -and
            !$withoutCrlf.Contains("`r")) `
        -Message "Baseline JSON is not consistently CRLF: $Path"
}

function Assert-PropertyAbsent {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Object,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Context
    )

    Assert-True `
        -Condition ($null -eq $Object.PSObject.Properties[$Name]) `
        -Message "$Context retained field '$Name'."
}

Assert-VleCanonicalPowerShell

$tempRoot = Join-Path `
    ([System.IO.Path]::GetTempPath()) `
    ("vle-baseline-writer-{0}" -f [System.Guid]::NewGuid().ToString("N"))
$currentDir = Join-Path $tempRoot "current"
$baselineDir = Join-Path $tempRoot "baseline"
$baselineSnapshotPath = Join-Path `
    $baselineDir `
    "0001-writer_test.sv.snapshot.json"
$baselineRunInfoPath = Join-Path $baselineDir "run-info.json"
$currentRunInfoPath = Join-Path $currentDir "run-info.json"

try {
    New-Item -ItemType Directory -Force -Path $currentDir | Out-Null

    $snapshot = [pscustomobject][ordered]@{
        SchemaVersion = 3
        RunName = "writer-self-test"
        ExtensionVersion = "0.0.0.0"
        FilePath = "C:\workspace\VerilogLanguageExtension\TestFiles\writer_test.sv"
        FileRelativePath = "TestFiles\writer_test.sv"
        ContentType = "verilog"
        SnapshotLength = 1
        SnapshotVersion = 1
        TextSha256 = "test"
        Errors = @()
        Classifications = @()
        Tags = @(
            [pscustomobject][ordered]@{
                Start = 0
                Length = 1
                Line = 1
                Column = 1
                Text = "X"
                TagDetail = "test"
                HoverText = "Value: 1`r`nFile: C:\workspace\VerilogLanguageExtension\TestFiles\writer_test.sv`r`nLine: 1"
            }
        )
        Tokens = @()
        Symbols = @()
        GeneratedAtUtc = "volatile"
        GitCommit = "volatile"
        ProcessingTime = "volatile"
        ProcessingTimeBucket = "volatile"
        RunTiming = @("volatile")
    }

    $runInfo = [pscustomobject][ordered]@{
        SchemaVersion = 1
        RunName = "writer-self-test"
        Manifest = "tools\vle-ci\manifests\writer-self-test.json"
        Status = "Completed"
        StartedAt = "volatile"
        CompletedAt = "volatile"
        ElapsedSeconds = 1.0
        Elapsed = "00:00:01"
        ExpectedSnapshots = 1
        ActualSnapshots = 1
        Timings = @("volatile")
        VsixManifestVersion = "0.0.0.0"
        AssemblyVersion = "0.0.0.0"
        AssemblyFileVersion = "0.0.0.0"
        AssemblyInformationalVersion = "0.0.0.0"
        ProvideMenuResourceName = "Menus.ctmenu"
        ProvideMenuResourceVersion = 5
        SnapshotCount = 1
        ProcessingTime = "volatile"
        ProcessingTimeBucket = "volatile"
        GitCommit = "volatile"
        GitCommitFull = "volatile"
        RunTiming = @("volatile")
        CiProcessingTime = "volatile"
        CiElapsedSeconds = 1.0
    }

    Write-VleJsonFile `
        -Path (Join-Path $currentDir "0001-writer_test.sv.snapshot.json") `
        -Value $snapshot
    Write-VleJsonFile `
        -Path $currentRunInfoPath `
        -Value $runInfo

    Write-VleSnapshotBaseline `
        -CurrentDirectory $currentDir `
        -BaselineDirectory $baselineDir

    Assert-True `
        -Condition (Test-Path -LiteralPath $baselineSnapshotPath -PathType Leaf) `
        -Message "Baseline writer self-test did not create the snapshot."
    Assert-True `
        -Condition (Test-Path -LiteralPath $baselineRunInfoPath -PathType Leaf) `
        -Message "Baseline writer self-test did not create run-info.json."

    Assert-CrlfUtf8NoBom -Path $baselineSnapshotPath
    Assert-CrlfUtf8NoBom -Path $baselineRunInfoPath

    $writtenSnapshot = Read-VleJsonFile -Path $baselineSnapshotPath
    Assert-True `
        -Condition ([string]$writtenSnapshot.FilePath -ceq
            "TestFiles/writer_test.sv") `
        -Message "FilePath was not made repository-relative."
    Assert-True `
        -Condition ([string]$writtenSnapshot.FileRelativePath -ceq
            "TestFiles/writer_test.sv") `
        -Message "FileRelativePath was not normalized."
    Assert-True `
        -Condition ([string]$writtenSnapshot.Tags[0].HoverText -ceq
            "Value: 1`r`nFile: TestFiles/writer_test.sv`r`nLine: 1") `
        -Message "Hover source location or line endings were not preserved."

    foreach ($field in @(Get-VleSnapshotVolatileFields)) {
        Assert-PropertyAbsent `
            -Object $writtenSnapshot `
            -Name $field `
            -Context "Baseline snapshot"
    }

    $writtenRunInfo = Read-VleJsonFile -Path $baselineRunInfoPath
    foreach ($field in @(Get-VleRunInfoVolatileFields)) {
        Assert-PropertyAbsent `
            -Object $writtenRunInfo `
            -Name $field `
            -Context "Baseline run-info.json"
    }

    Assert-True `
        -Condition ([string]$writtenRunInfo.Manifest -ceq
            "tools/vle-ci/manifests/writer-self-test.json") `
        -Message "run-info.json Manifest was not normalized."
    Assert-True `
        -Condition ([string]$writtenRunInfo.Status -ceq "Completed") `
        -Message "run-info.json Status was not preserved."
    Assert-True `
        -Condition ([int]$writtenRunInfo.ExpectedSnapshots -eq 1 -and
            [int]$writtenRunInfo.ActualSnapshots -eq 1 -and
            [int]$writtenRunInfo.SnapshotCount -eq 1) `
        -Message "run-info.json snapshot counts were not preserved."
    Assert-True `
        -Condition ([string]$writtenRunInfo.VsixManifestVersion -ceq
            "0.0.0.0") `
        -Message "run-info.json release metadata was not preserved."

    $snapshotHashBefore = (Get-FileHash `
        -LiteralPath $baselineSnapshotPath `
        -Algorithm SHA256).Hash
    $runInfoHashBefore = (Get-FileHash `
        -LiteralPath $baselineRunInfoPath `
        -Algorithm SHA256).Hash

    Write-VleSnapshotBaseline `
        -CurrentDirectory $currentDir `
        -BaselineDirectory $baselineDir

    $snapshotHashAfter = (Get-FileHash `
        -LiteralPath $baselineSnapshotPath `
        -Algorithm SHA256).Hash
    $runInfoHashAfter = (Get-FileHash `
        -LiteralPath $baselineRunInfoPath `
        -Algorithm SHA256).Hash
    Assert-True `
        -Condition ($snapshotHashBefore -ceq $snapshotHashAfter -and
            $runInfoHashBefore -ceq $runInfoHashAfter) `
        -Message "Repeated baseline writes were not byte-identical."

    # Simulate termination after the old baseline was moved aside. The next
    # writer invocation must restore the backup before validating new input.
    $backupDir = Join-Path $tempRoot ".baseline.old-update"
    Move-Item -LiteralPath $baselineDir -Destination $backupDir
    $missingRunInfoPath = "$currentRunInfoPath.missing"
    Move-Item -LiteralPath $currentRunInfoPath -Destination $missingRunInfoPath

    $recoveryFailureObserved = $false
    try {
        Write-VleSnapshotBaseline `
            -CurrentDirectory $currentDir `
            -BaselineDirectory $baselineDir
    }
    catch {
        if ($_.Exception.Message -like
                "Current snapshot run-info.json not found:*") {
            $recoveryFailureObserved = $true
        }
        else {
            throw
        }
    }
    finally {
        Move-Item `
            -LiteralPath $missingRunInfoPath `
            -Destination $currentRunInfoPath `
            -ErrorAction SilentlyContinue
    }

    Assert-True `
        -Condition $recoveryFailureObserved `
        -Message "Interrupted-update recovery test did not reach the expected failure."
    Assert-True `
        -Condition (Test-Path -LiteralPath $baselineDir -PathType Container) `
        -Message "Interrupted-update recovery did not restore the baseline."
    Assert-True `
        -Condition (!(Test-Path -LiteralPath $backupDir)) `
        -Message "Interrupted-update recovery left the backup directory behind."

    Write-Host "Snapshot baseline writer self-test passed."
}
finally {
    Remove-Item `
        -LiteralPath $tempRoot `
        -Recurse `
        -Force `
        -ErrorAction SilentlyContinue
}
