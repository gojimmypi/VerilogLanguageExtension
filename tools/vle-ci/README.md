# VerilogLanguageExtension Local Snapshot CI

This directory contains local CI tooling for regression-testing the Visual Studio
editor behavior of VerilogLanguageExtension.

The goal is to make syntax highlighting, tokenization, symbol discovery, and
hover text testable from a repeatable command line flow. The tooling does not
compare screenshots or theme colors. It compares deterministic JSON snapshots
exported from the Visual Studio Experimental Instance.

## What this protects

This system is intended to catch regressions such as:

- hover text disappearing for a known signal such as `dout`
- `signed` or `unsigned` being accidentally registered as a variable name
- Verilog literals being split incorrectly, such as `32'hffff_0000` or `3'b 010`
- syntax highlighting/classification output changing unexpectedly
- one opened file poisoning parser state for another file
- snapshot/exporter exceptions being silently ignored

The snapshots are editor/parser data, not screenshots. They include information
such as classifications, token tags, parser tokens, symbols, hover text, source
hashes, and exporter errors.

## Required setup

Run these commands from the repository root:

```powershell
cd C:\workspace\VerilogLanguageExtension
```

PowerShell may block local scripts. For the current PowerShell process only:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

These tools expect:

- Visual Studio installed with VSIX/MSBuild support
- Python available as `python`
- the extension built in `Debug` configuration for snapshot-on-open export
- Visual Studio Experimental Instance root suffix `Exp`, unless overridden

Snapshot export is intentionally local-only. The normal output goes under:

```text
artifacts\snapshots\current
```

Do not commit `artifacts\`, `bin\`, `obj\`, `packages\`, or local VSIX output.

## Quick start

Generate the all-testfiles manifest:

```powershell
.\create-testfile-manifest.ps1
```

Run the full expectation pass:

```powershell
.\ci-pass.ps1
```

Create or refresh the approved all-testfiles baseline:

```powershell
.\ci-baseline.ps1
```

Check the current output against the approved baseline:

```powershell
.\ci-check.ps1
```

A good run ends with:

```text
Snapshot regression check passed.
Local CI completed successfully.
```

## Recommended workflow

Use this flow when editing tokenizer, parser, classifier, tagger, or hover logic:

1. Run `.\ci-pass.ps1`.
2. Make one small source change.
3. Run `.\ci-pass.ps1` again.
4. Run `.\ci-check.ps1` before committing.
5. If behavior changed intentionally, inspect the snapshot diff.
6. Only then run `.\ci-baseline.ps1` to approve the new baseline.

Do not use `ci-baseline.ps1` to make a failing run pass. It means "approve the
current behavior as the new truth".

## File overview

### `create-testfile-manifest.ps1`

Creates:

```text
tools\vle-ci\manifests\all-testfiles.json
```

It scans `TestFiles` recursively and includes files with these extensions:

```text
.v
.sv
.svh
.vh
.verilog
```

The generated all-testfiles manifest uses:

```json
{
    "RunName": "all-testfiles",
    "DelayMs": 1000,
    "FreshInstancePerFile": true
}
```

`FreshInstancePerFile` is important for the all-testfiles baseline. It avoids
flaky failures where Visual Studio reuses views or delays events after many file
opens.

Run from the repository root:

```powershell
.\create-testfile-manifest.ps1
```

### `ci-pass.ps1`

Runs all test files against targeted expectations only.

Equivalent command:

```powershell
.\tools\vle-ci\Run-LocalCI.ps1 `
    -Manifest tools\vle-ci\manifests\all-testfiles.json
```

Use this as the fastest normal confidence check. It builds the extension, exports
snapshots for all manifest files, and checks the expectation files under:

```text
tools\vle-ci\expectations
```

### `ci-baseline.ps1`

Updates the approved all-testfiles baseline.

Equivalent command:

```powershell
.\tools\vle-ci\Run-LocalCI.ps1 `
    -Manifest tools\vle-ci\manifests\all-testfiles.json `
    -Baseline tests\snapshots\baselines\development-main\all-testfiles `
    -UpdateBaseline
```

Use this only after manually reviewing and approving the current snapshot output.

### `ci-check.ps1`

Checks current all-testfiles snapshots against the approved all-testfiles
baseline.

Equivalent command:

```powershell
.\tools\vle-ci\Run-LocalCI.ps1 `
    -Manifest tools\vle-ci\manifests\all-testfiles.json `
    -Baseline tests\snapshots\baselines\development-main\all-testfiles
```

Use this before committing or before release candidate testing.

## Core tools

### `tools\vle-ci\Run-LocalCI.ps1`

Top-level local CI entry point.

It performs these steps:

1. Finds MSBuild through `vswhere`.
2. Builds `VerilogLanguageExtension.sln`.
3. Runs `Export-Snapshots.ps1` unless `-SkipSnapshots` is used.
4. Runs `Compare-Snapshots.py` against targeted expectations.
5. Optionally compares against a baseline.
6. Optionally updates a baseline when `-UpdateBaseline` is used.

Common usage:

```powershell
.\tools\vle-ci\Run-LocalCI.ps1
```

Run a specific manifest:

```powershell
.\tools\vle-ci\Run-LocalCI.ps1 `
    -Manifest tools\vle-ci\manifests\cold-open.json
```

Run without rebuilding:

```powershell
.\tools\vle-ci\Run-LocalCI.ps1 `
    -Manifest tools\vle-ci\manifests\all-testfiles.json `
    -SkipBuild
```

Build only:

```powershell
.\tools\vle-ci\Run-LocalCI.ps1 `
    -SkipSnapshots
```

Use a different Experimental Instance root suffix:

```powershell
.\tools\vle-ci\Run-LocalCI.ps1 `
    -RootSuffix Exp2
```

### `tools\vle-ci\Export-Snapshots.ps1`

Launches Visual Studio and opens files listed in a manifest. The extension's
DEBUG-only `SnapshotExportOnOpen` listener writes deterministic JSON snapshot
files.

Main parameters:

```powershell
-Manifest              Path to the manifest JSON
-OutputDir             Snapshot output directory
-RootSuffix            Visual Studio root suffix, default Exp
-DelaySeconds          Delay after opening a file
-MaxWaitSeconds        Maximum wait for a snapshot
-FreshInstancePerFile  Open each file in a fresh VS Experimental Instance
-LeaveVisualStudioOpen Keep VS open after exporting
-VisualStudioPath      Explicit path to devenv.exe
```

Normal direct use:

```powershell
.\tools\vle-ci\Export-Snapshots.ps1 `
    -Manifest tools\vle-ci\manifests\all-testfiles.json `
    -OutputDir artifacts\snapshots\current
```

Use `FreshInstancePerFile` for broad all-testfile coverage:

```powershell
.\tools\vle-ci\Export-Snapshots.ps1 `
    -Manifest tools\vle-ci\manifests\all-testfiles.json `
    -FreshInstancePerFile
```

Do not use `FreshInstancePerFile` for state-leak manifests. State-leak tests
must reuse one Visual Studio instance so one file can affect another.

### `tools\vle-ci\Compare-Snapshots.py`

Compares snapshot JSON output and checks targeted expectations.

It intentionally normalizes away machine-specific data such as absolute path
noise and snapshot file order details that are not semantically useful. It
compares editor/parser output such as:

- `Classifications`
- `Tags`
- `Tokens`
- `Symbols`
- `HoverText`
- `TextSha256`
- exporter `Errors`

Common direct usage:

```powershell
python .\tools\vle-ci\Compare-Snapshots.py `
    --current artifacts\snapshots\current `
    --expectations tools\vle-ci\expectations
```

Compare against a baseline:

```powershell
python .\tools\vle-ci\Compare-Snapshots.py `
    --current artifacts\snapshots\current `
    --baseline tests\snapshots\baselines\development-main\all-testfiles `
    --expectations tools\vle-ci\expectations
```

Update a baseline:

```powershell
python .\tools\vle-ci\Compare-Snapshots.py `
    --current artifacts\snapshots\current `
    --baseline tests\snapshots\baselines\development-main\all-testfiles `
    --update-baseline
```

## Manifests

Manifests live under:

```text
tools\vle-ci\manifests
```

A manifest controls which files are opened and in what order.

Example:

```json
{
    "RunName": "cold-open",
    "DelayMs": 500,
    "Files": [
        "TestFiles/issue10.v",
        "TestFiles/issue21.v",
        "TestFiles/issue21_mini.v",
        "TestFiles/picorv32.v"
    ]
}
```

For all-testfile coverage, use:

```json
{
    "RunName": "all-testfiles",
    "DelayMs": 1000,
    "FreshInstancePerFile": true,
    "Files": [
        "TestFiles/issue10.v"
    ]
}
```

For state-leak testing, do not use `FreshInstancePerFile`. Reopen important
files after other files:

```json
{
    "RunName": "multi-open-forward",
    "DelayMs": 500,
    "Files": [
        "TestFiles/issue10.v",
        "TestFiles/issue21.v",
        "TestFiles/picorv32.v",
        "TestFiles/issue10.v"
    ]
}
```

The repeated final open checks that `issue10.v` still has the expected parser,
symbol, and hover behavior after other files were opened.

## Expectations

Expectations live under:

```text
tools\vle-ci\expectations
```

Each `*.expect.json` file targets one input file and checks known-important
behavior.

Example:

```json
{
    "File": "TestFiles/issue10.v",
    "MustHaveSymbols": [
        {
            "Name": "dout",
            "HoverContains": [
                "output",
                "reg",
                "[7:0]",
                "unsigned",
                "dout"
            ]
        }
    ],
    "MustNotHaveSymbols": [
        "signed",
        "unsigned"
    ],
    "MustHaveText": [
        {
            "Text": "dout",
            "HoverContains": "unsigned dout"
        }
    ]
}
```

Use expectations for known bug classes. They produce direct failure messages such
as:

```text
missing symbol dout
forbidden symbol exists: unsigned
text dout hover did not contain unsigned dout
```

## Baselines

Baselines live under:

```text
tests\snapshots\baselines
```

A baseline is an approved set of snapshot JSON files. It captures broader
highlighting, token, symbol, and hover behavior.

For all test files, use:

```text
tests\snapshots\baselines\development-main\all-testfiles
```

Create or refresh the baseline:

```powershell
.\ci-baseline.ps1
```

Check against the baseline:

```powershell
.\ci-check.ps1
```

Only update a baseline after reviewing the current output. Updating the baseline
means future runs will treat the current behavior as correct.

## Snapshot output

Current snapshots are written to:

```text
artifacts\snapshots\current
```

Snapshot files are named with a sequence prefix:

```text
0001-issue10.v.snapshot.json
0002-issue21.v.snapshot.json
```

The sequence prefix is intentional. It makes repeated-open scenarios testable.

Snapshot JSON includes data such as:

```json
{
    "SchemaVersion": 2,
    "RunName": "cold-open",
    "FileRelativePath": "TestFiles/issue10.v",
    "TextSha256": "...",
    "ContentType": "verilog",
    "Errors": [],
    "Tags": [],
    "Tokens": [],
    "Symbols": []
}
```

`Errors` should normally be empty. If exporter or classifier exceptions occur,
they should be recorded there and cause expectation checks to fail.

## Troubleshooting

### PowerShell says scripts are not digitally signed

Use a process-only execution-policy bypass:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

Or call a script with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\ci-pass.ps1
```

### Build fails because `VerilogLanguage.dll` is in use

Close the Visual Studio Experimental Instance or kill `devenv.exe`:

```powershell
Get-Process devenv -ErrorAction SilentlyContinue | Stop-Process -Force
```

Then rerun the command.

### No snapshot was written

Common causes:

- the file was already open
- Visual Studio reused an existing view
- the Experimental Instance was still running from a previous test
- the file took longer than expected to parse/export
- the extension was not loaded in the selected root suffix

For all-testfiles, use a manifest with:

```json
"FreshInstancePerFile": true
```

For large files, increase:

```json
"DelayMs": 1000
```

or higher.

### Files with spaces do not open

Paths in manifests are JSON strings and may contain spaces. `Export-Snapshots.ps1`
quotes paths before passing them to `devenv.exe`. If a path still fails, confirm
the file exists exactly as listed.

### Snapshot comparison fails after an intentional parser change

Inspect the failure. If the new behavior is correct:

1. Update or add expectations if a targeted rule changed.
2. Run `.\ci-baseline.ps1` to approve the new baseline.
3. Run `.\ci-check.ps1` to verify the baseline comparison passes.

### Snapshot comparison fails after an unintentional parser change

Do not update the baseline. Fix the parser/classifier/tagger change and rerun
`.\ci-pass.ps1`.

## What to commit

Commit:

```text
tools\vle-ci\*.ps1
tools\vle-ci\*.py
tools\vle-ci\README.md
tools\vle-ci\manifests\*.json
tools\vle-ci\expectations\*.expect.json
tests\snapshots\baselines\...\*.snapshot.json
```

Do not commit:

```text
artifacts\
bin\
obj\
packages\
*.vsix
```

## Practical commands

Run all expectations:

```powershell
.\ci-pass.ps1
```

Update all-testfiles baseline:

```powershell
.\ci-baseline.ps1
```

Check current output against all-testfiles baseline:

```powershell
.\ci-check.ps1
```

Run cold-open only:

```powershell
.\tools\vle-ci\Run-LocalCI.ps1 `
    -Manifest tools\vle-ci\manifests\cold-open.json
```

Run state-leak scenario:

```powershell
.\tools\vle-ci\Run-LocalCI.ps1 `
    -Manifest tools\vle-ci\manifests\multi-open-forward.json
```

Regenerate all-testfiles manifest:

```powershell
.\create-testfile-manifest.ps1
```
