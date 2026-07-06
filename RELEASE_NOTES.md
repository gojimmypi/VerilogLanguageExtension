# Verilog Language Extension v0.4.1.5 Release Notes

This is the VS2022 and VS2026 release.

For prior versions, see:

https://github.com/gojimmypi/VerilogLanguageExtension/blob/main/releases/README.md

Release date: 2026-07-05

## Overview

v0.4.1.5 is a navigation, release-polish, and CI-health update. The main user-visible improvement is expanded Verilog navigation support, including Peek Definition and Find All References, along with improved definition lookup across parsed files. This release also tightens VSIX metadata, release version checks, snapshot baseline validation, and local script organization.

## User-visible editor improvements

* Added a Verilog `Peek Definition` command for supported Verilog editor views.

* Added a Verilog `Find All References` command that writes navigable results to a dedicated `VLE Find All References` output pane.

* Improved definition lookup used by navigation features, including support for module, macro, constant, local-scope, module-scope, and cross-file parsed definitions.

* Added fallback text-based definition lookup for cases where parser metadata is unavailable or incomplete.

* Added reference classification for found symbols, including declaration, read, write, and general reference results.

* Improved context reporting for reference results by including containing module/type and local member scope when available.

## Navigation and command integration

* Added shared Verilog definition-resolution logic under `Navigation/VerilogDefinitionResolver.cs`.

* Added Verilog reference search logic under `Navigation/VerilogReferenceFinder.cs`.

* Added new package initialization for Peek Definition and Find All References commands.

* Added VSCT menu entries for `Peek Definition` and `Find All References` in the Verilog editor context menu.

* Bumped the Visual Studio menu resource version so updated command-table changes are refreshed in installed VSIX builds.

## Parser and symbol lookup improvements

* Added cross-file parsed-definition lookup for already-open or already-parsed Verilog files.

* Added candidate-file search fallback for definition lookup.

* Added safer cloning of definition-location candidates so returned locations carry the correct source file path.

* Added helper logic to avoid broad cross-file lookup for ordinary lowercase/local-style identifiers unless the lookup looks like a likely global Verilog symbol.

## Lifecycle, diagnostics, and debug support

* Added conditional diagnostics helpers for exception/debug logging.

* Added `VLE_DEBUG_EXCEPTIONS` to Debug builds.

* Reworked token tagger lifetime handling so Visual Studio can receive disposable tagger leases while the shared tokenizing core remains singleton-per-buffer.

* Added classifier disposal support and cleanup for aggregator event subscriptions.

## Build, VSIX metadata, and Marketplace polish

* Bumped VSIX, assembly file, and informational versions to `0.4.1.5`.

* Updated the VSIX display name to `Verilog Language Extension`.

* Replaced the multi-line VSIX manifest description with a concise Marketplace-friendly description.

* Added a VSIX icon asset.

* Updated VSIX tags to use semicolon-delimited Marketplace-style tags.

* Updated the VSIX release-notes link to point to `RELEASE_NOTES.md`.

* Added `ATTRIBUTION.md`.

* Added `MARKETPLACE_TEXT.md` to track Marketplace listing text in source control.

* Moved older version-specific release notes out of Marketplace text and into `RELEASE_NOTES.md`.

## Local scripts and repository workflow

* Moved root-level local CI wrapper scripts under the `scripts/` directory.

* Added `scripts/README.md` documenting the local snapshot CI workflow, refresh scripts, text cleanup, and VSIX build helper.

* Added script wrappers for `ci-pass`, `ci-baseline`, `ci-check`, manifest refresh, single-file checks, and refresh-log workflows.

* Added `scripts/text-clean.ps1` for normalizing known text files.

* Updated local workflow documentation to include unblocking PowerShell scripts after download/clone when needed.

## CI and release validation

* Added `Get-VleVersionInfo.ps1` to report VSIX, assembly, and menu-resource version metadata.

* Added `Assert-VleReleaseInfo.ps1` to verify VSIX manifest version, assembly file version, informational version, stable assembly version, and menu-resource metadata.

* Added `Assert-SnapshotBaselineVersion.ps1` to verify approved snapshot baseline metadata against the current source version information.

* Updated the build workflow to support an optional expected release version input.

* Added CI checks for release metadata and approved snapshot baseline metadata.

* Added upload of a `vle-version-info` artifact.

* Enabled VSIX artifact upload from the build workflow.

* Removed the older separate `vsix-build-publish.yml` workflow and consolidated disabled publish placeholders into the main VSIX build workflow.

## Snapshot CI and baseline updates

* Regenerated current snapshot artifacts and approved all-testfiles baselines.

* Added release/version metadata to snapshot `run-info.json`.

* Added CI timing metadata using coarse elapsed-time buckets to reduce noisy diffs while still tracking meaningful performance changes.

* Added safety checks so baseline updates can only target approved baseline directories under `tests/snapshots/baselines`.

## Known limitations

* Navigation and reference search are parser-assisted and text-search-assisted, not a full SystemVerilog language server.

* Cross-file lookup depends on already parsed files and bounded candidate-file search.

* Include-file discovery and full project-wide semantic analysis are still limited.

* The extension still does not compile, synthesize, lint, or upload designs to hardware.


## Verilog Language Extension v0.4.0.0 Release Notes

Release date: 2026-06-25

Compared baseline: v0.3.5.4

## Overview

v0.4.0.0 is a major editor-quality, parser reliability, and release-polish update for the Verilog Language Extension. The update keeps the extension focused on Visual Studio Verilog/SystemVerilog editor support: syntax highlighting, hover text, outlining, and local development/testing tools. It does not add synthesis, programming, linting, or full language-server functionality.

## User-visible editor improvements

* Added support for `.svh` SystemVerilog header files.

* Added Verilog-specific code outlining/folding for modules, functions, tasks, begin/end blocks, case blocks, always blocks, if/else blocks, and preprocessor conditional regions.

* Added classification/colorization for static double-quoted string literals.

* Added classification/colorization for function names in declarations and references.

* Added classification/colorization and hover handling for macro references and macro condition operands.

* Added special handling for conditional macro-controlled definitions so mutually exclusive preprocessor branches are not automatically treated as plain duplicate declarations.

* Added/improved classification entries for additional Verilog/SystemVerilog tokens, including `automatic`, `bit`, `logic`, macros, static strings, function names, and duplicate declarations.

* Improved QuickInfo behavior so hovers come from the correct file and snapshot instead of stale global parser state.

* Improved local function/task scope lookup so local declarations are found before module/global declarations when colorizing and showing hover text.

## Parser, tokenization, and refresh fixes

* Added per-file, per-snapshot parse-data caching. Parser outputs now carry the target file and snapshot version, which reduces cross-file and stale-snapshot contamination when multiple Verilog files are open.

* Updated token colorization to reject stale parse data when a file is marked dirty or is still reparsing.

* Updated reparse flow to use an immutable captured snapshot for the full parse transaction.

* Fixed duplicate declaration marking to use the same snapshot that was parsed, rather than fetching a potentially newer current snapshot mid-parse.

* Added follow-up reparse queuing when an edit arrives while a parse is already running, so edit requests are not silently lost.

* Improved edit invalidation so changed spans refresh immediately, with a second refresh after threaded reparsing finishes.

* Improved handling for paste/delete/multi-change edits by scanning all changes and using `ContainsRefreshChar` style checks rather than assuming a single edit payload.

* Improved block-comment state handling with a per-snapshot line-start cache, reducing repeated document-prefix scans during viewport classification.

* Improved token span safety checks around empty spans, EOF, zero-length buffers, and snapshot mapping.

## Lifecycle and performance improvements

* Made the token tagger singleton-per-buffer to prevent duplicate `Changed` event subscriptions and duplicate token scans.

* Made the classifier singleton-per-buffer and moved token aggregator creation into the singleton factory so repeated `CreateTagger` calls do not create extra aggregators.

* Added explicit token tagger disposal that unsubscribes from `_buffer.Changed`, stops/disposes the reparse-completion timer, and clears cached block-comment state.

* Added a bounded reparse-completion watcher so a stuck parse flag cannot leave a timer running indefinitely.

* Added safer `TagsChanged` raising through the captured UI synchronization context, with an optional JoinableTaskFactory code path left behind for future testing.

## Snapshot Exporter and local CI

* Added Visual Studio package/menu plumbing for a Snapshot Exporter command.

* Added `SnapshotExporter` classes that can export editor classification, Verilog token tags, parser data, symbols, hover text, source hashes, and exporter errors to deterministic JSON.

* Added export-on-open support controlled by environment variables, a temp-file gate, or a temp config file.

* Added local snapshot CI tooling under `tools/vle-ci`, including manifest support, snapshot export orchestration, snapshot comparison, and documentation.

* Added wrapper scripts for the normal local workflow:
  * `ci-pass.ps1`
  * `ci-baseline.ps1`
  * `ci-check.ps1`
  * `create-testfile-manifest.ps1`
  * `refresh-tests.ps1`
  * `refresh-ci-check.ps1`

* Added checked-in test manifests, expectations, and baseline snapshot JSON files for regression coverage.

## Build, packaging, and project updates

* Bumped extension/package version from v0.3.5.4 to v0.4.0.0 in the VSIX manifest and assembly metadata.

* Added assembly file and informational version attributes for v0.4.0.0.

* Added package loading support through `ProvideCodeBase` and VS package assets in the VSIX manifest.

* Updated the VSIX manifest description and tags to include ASIC, RTL, and Tiny Tapeout relevance.

* Updated manifest documentation links from `master` to `main`.

* Added or updated Visual Studio SDK/MSBuild package references for the VS2022/VS2026-oriented build flow.

* Moved Release VSIX debug-symbol inclusion to false while keeping Debug symbol inclusion enabled.

* Added `build_vsix.bat` for a repeatable command-line VSIX build path.

* Added `.editorconfig` and text-hygiene tooling.

## Source cleanup

* Removed older sample/reference extension code that was not part of the Verilog editor path, including legacy `EditorClassifier`, `Reference_Services`, and generic tagger/outlining sample files.

* Replaced generic outlining sample code with Verilog-specific outlining code.

* Moved or consolidated token-related files under the `VerilogToken` folder.

* Added more structured test files, including larger Verilog/RTL examples used by the snapshot exporter and local CI.

## Known limitations unchanged

* Include files are still not searched.

* The extension still does not compile, synthesize, lint, or upload designs to hardware.

* There is still no import/export flow for user color settings.

* The local snapshot CI depends on a Windows Visual Studio Experimental Instance and is not a pure headless compiler-only test.

## Notable files changed or added

* `Classification/VerilogClassifier.cs`
* `Classification/ClassificationFormat.cs`
* `Classification/ClassificationType.cs`
* `VerilogToken/VerilogTokenTagger.cs`
* `VerilogToken/VerilogTokenTagProvider.cs`
* `VerilogToken/VerilogTokenTypes.cs`
* `Globals/Parsing.cs`
* `Globals/BufferAttributes.cs`
* `Globals/VerilogGlobals.cs`
* `Intellisense/VerilogQuickInfoSource.cs`
* `Outlining/VerilogOutliningTagger.cs`
* `Outlining/VerilogOutliningTaggerProvider.cs`
* `SnapshotExporter/*`
* `tools/vle-ci/*`
* `VerilogLanguage.csproj`
* `source.extension.vsixmanifest`
* `Properties/AssemblyInfo.cs`

# Earlier release notes moved from Marketplace text

These notes were previously embedded in the Marketplace overview. Keep version-specific release history here so the Marketplace text can stay focused on the current feature summary.

## v0.3.5.4

* Fixed Ctrl-C in a multiple-pane window such as Git Diff, where a `value cannot be null` error could occur.

## v0.3.5.3

* Changed supported Visual Studio versions to only Visual Studio 2022 and Visual Studio 2026. See GitHub for older Visual Studio versions.

## v0.3.3

* Improved syntax highlighting, particularly variable handling, initialization hover text, and initial load behavior.

## v0.3.1

* Fixed syntax highlighting for Visual Studio 2015 after support for multiple file extensions was added.

## v0.2.1

* Improved processing for large files. Files larger than 8K are processed in the background.
* At that time, the viewport was not refreshed automatically after background processing completed. Mouse hovers and key presses could help nudge updates.
