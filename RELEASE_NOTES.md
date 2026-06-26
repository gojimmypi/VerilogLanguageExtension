# Verilog Language Extension Release Notes

This is the VS2022 and VS2026 release.

For prior versions, see:

https://github.com/gojimmypi/VerilogLanguageExtension/blob/main/releases/README.md

# Verilog Language Extension v0.4.0.0 Release Notes

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
