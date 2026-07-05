# VLE repository scripts

This directory contains the repository-level helper scripts that used to live in the repository root.
Run them from the repository root with `./scripts/<name>` unless a note below says otherwise. The PowerShell wrappers also resolve the repository root from their own location, so they can be launched from another current directory.

## Snapshot CI wrappers

### `ci-pass.ps1`

Purpose: regenerate the all-testfiles manifest and run the current snapshot export against expectations only. This is the first sanity check before approving any baseline change.

Typical use:

```powershell
.\scripts\ci-pass.ps1
```

What to check:

- The manifest is regenerated under `tools/vle-ci/manifests/all-testfiles.json`.
- Snapshot export succeeds.
- Expectation checks pass.
- `artifacts/snapshots/current/run-info.json` includes timing and release metadata.

### `ci-baseline.ps1`

Purpose: regenerate the approved all-testfiles baseline after you have intentionally accepted the current snapshot output.

Typical use:

```powershell
.\scripts\ci-baseline.ps1
```

What it does:

- Refreshes the all-testfiles manifest.
- Runs `tools/vle-ci/Run-LocalCI.ps1` with `-UpdateBaseline`.
- Updates `tests/snapshots/baselines/development-main/all-testfiles`.
- Formats generated JSON as UTF-8 without BOM.
- Accepts the current manifest ordering after a successful baseline update.

Safety note: `Run-LocalCI.ps1` only allows `-UpdateBaseline` paths under `tests/snapshots/baselines`.

### `ci-check.ps1`

Purpose: regenerate the manifest and compare current snapshots against the approved all-testfiles baseline.

Typical use:

```powershell
.\scripts\ci-check.ps1
```

Use this after `ci-baseline.ps1` to confirm the approved baseline matches a fresh run.

### `create-testfile-manifest.ps1`

Purpose: regenerate `tools/vle-ci/manifests/all-testfiles.json` with stable snapshot file names.

Typical use:

```powershell
.\scripts\create-testfile-manifest.ps1
```

Useful options:

```powershell
# Seed stable names from the approved baseline.
.\scripts\create-testfile-manifest.ps1 -BaselineDir tests\snapshots\baselines\development-main\all-testfiles

# Clear the temporary IsNew markers after a successful baseline refresh.
.\scripts\create-testfile-manifest.ps1 -BaselineDir tests\snapshots\baselines\development-main\all-testfiles -AcceptCurrentManifest
```

## Convenience wrappers

### `refresh-tests.ps1`

Purpose: run the normal three-step local refresh sequence and save logs in the repository root.

```powershell
.\scripts\refresh-tests.ps1
```

Equivalent sequence:

```powershell
.\scripts\ci-pass.ps1 *> .\ci_pass.log
.\scripts\ci-baseline.ps1 *> .\ci_baseline.log
.\scripts\ci-check.ps1 *> .\ci_check.log
```

Before overwriting a log, the script copies the old log to `<name>.old`.

### `refresh-ci-check.ps1`

Purpose: run only `ci-check.ps1` and write `ci_check.log` in the repository root.

```powershell
.\scripts\refresh-ci-check.ps1
```

## Single-file and text helpers

### `check-file.ps1`

Purpose: run a targeted one-file snapshot/export review against a selected Verilog test file.

```powershell
.\scripts\check-file.ps1 -SourceFile TestFiles\comma.v
```

Common options:

```powershell
# Reuse an existing Visual Studio experimental instance.
.\scripts\check-file.ps1 -SourceFile TestFiles\comma.v -ReuseExistingVisualStudio

# Leave Visual Studio open after the run.
.\scripts\check-file.ps1 -SourceFile TestFiles\comma.v -LeaveVisualStudioOpen
```

### `text-clean.ps1`

Purpose: normalize a fixed list of known text files by removing UTF-8 BOMs, normalizing line endings, and ensuring a final newline. Review the file list before running it.

```powershell
.\scripts\text-clean.ps1
```

### `build_vsix.bat`

Purpose: local command-prompt VSIX build/debug helper. It cleans `bin` and `obj`, rebuilds the project, expands the generated VSIX, and prints selected package artifacts.

```cmd
scripts\build_vsix.bat
```

## Release-health scripts used by CI

The release/version checks remain under `tools/vle-ci` because they are CI implementation details rather than root-level user wrappers:

```text
tools/vle-ci/Get-VleVersionInfo.ps1
tools/vle-ci/Assert-VleReleaseInfo.ps1
tools/vle-ci/Assert-SnapshotBaselineVersion.ps1
tools/vle-ci/Run-LocalCI.ps1
```

The GitHub workflows call those scripts directly.

## Recommended local test sequence

After moving or editing scripts, run:

```powershell
# Validate the manifest wrapper and expectation-only path.
.\scripts\ci-pass.ps1

# Regenerate the approved all-testfiles baseline when the snapshot changes are intentional.
.\scripts\ci-baseline.ps1

# Confirm the committed baseline matches a fresh current run.
.\scripts\ci-check.ps1

# Check release metadata directly.
.\tools\vle-ci\Get-VleVersionInfo.ps1 -AsJson
.\tools\vle-ci\Assert-VleReleaseInfo.ps1
.\tools\vle-ci\Assert-SnapshotBaselineVersion.ps1 -BaselineDir tests\snapshots\baselines\development-main\all-testfiles
```

For a smaller path check without opening every test file, run:

```powershell
.\scripts\create-testfile-manifest.ps1
```
