# VerilogLanguageExtension code_polish branch review

Scope: static review of the uploaded `code_polish.zip` branch snapshot, with `development-main.zip` used as the baseline for branch-regression context. I did not run a Visual Studio/MSBuild build in the sandbox because the VS SDK toolchain is not available here.

## Executive summary

`code_polish` contains useful newer work: the VS package/menu command plumbing and Snapshot Exporter feature are the main value. I would keep developing on this branch, but I would fix the project-file/build issues before making functional changes.

## Must fix before relying on the branch

1. `VerilogLanguage.csproj:5` sets `VSToolsPath` before `VisualStudioVersion` is assigned.

   Current:

   ```xml
   <VSToolsPath Condition="'$(VSToolsPath)' == ''">$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)</VSToolsPath>
   <VisualStudioVersion Condition="'$(VisualStudioVersion)' == '' And Exists('$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v18.0\VSSDK\Microsoft.VsSDK.targets')">18.0</VisualStudioVersion>
   ```

   If `VisualStudioVersion` is empty on entry, `VSToolsPath` can become `...\VisualStudio\v` and never recompute. Use the `development-main` ordering: assign `VisualStudioVersion`, then assign `VSToolsPath` with `And '$(VisualStudioVersion)' != ''`.

2. `VerilogLanguage.csproj:98-99` regresses package references compared with main.

   Main uses `Microsoft.VisualStudio.SDK 17.14.40265`, `Microsoft.VisualStudio.SDK.Analyzers`, `Microsoft.VisualStudio.Threading.Analyzers`, and `Microsoft.VSSDK.BuildTools 17.14.2120`, all with `PrivateAssets="all"`. `code_polish` uses `Microsoft.VisualStudio.SDK 17.0.32112.339`, drops the analyzers, and omits `PrivateAssets="all"`.

   Keep the newer main package references and add the code_polish package/VSCT pieces around them.

3. `FirstCommand.cs` is compiled but appears to be a leftover template/sample command.

   `VerilogLanguage.csproj:106` compiles `FirstCommand.cs`. The file exposes a command that displays `"Hello from an extension!"` (`FirstCommand.cs:75-84`), but the package does not initialize it and the VSCT does not define its command id. Remove this file and remove `CmdIdFirstCommand` from `SnapshotExporter/PkgCmdIDList.cs` unless you intentionally want a second command.

4. `build_vsix.bat` is stale and will fail or print the wrong files.

   It tries to type `SnapshotExporter\VerilogLanguagePackage.cs` even though the file is at repo root, and it types `VerilogLanguage.vsct` even though this branch uses `VerilogLanguagePackage.vsct`.

5. The branch archive contains generated/restored artifacts that should not be in a clean branch.

   The uploaded tree includes `obj/`, `packages/`, old `packages.config`-era packages, and many release `.vsix` files. The `.gitignore` excludes several of these, but the branch snapshot still contains them. Before publishing this branch, remove generated artifacts from source control unless there is a specific reason to keep release binaries.

## Snapshot Exporter review

6. `SnapshotExporter/SnapshotExporter.cs:124` creates an `ITagAggregator<VerilogTokenTag>` but never disposes it.

   `ITagAggregator<T>` is disposable. In the manual export path this may leak subscriptions/resources per export. Use a `using` block or explicitly dispose after `agg.GetTags(...)` completes.

7. `SnapshotExporter/SnapshotExporter.cs:92-98` silently swallows classifier exceptions and returns a partial export.

   This is useful for keeping the command alive, but it makes the JSON look valid while hiding that the export failed. Prefer adding an `Errors` list to `EditorSnapshotExport`, or showing a warning in the command result.

8. `SnapshotExporter/SnapshotExportOnOpen.cs:72-74` starts a fire-and-forget async export with no exception handling.

   This is DEBUG-gated and temp-file-gated, which is good, but `ExportAfterDelayAsync` should still catch/log exceptions. Also consider checking whether the view was closed after the delay before exporting.

9. `SnapshotExporter/SnapshotModel.cs` omits the source text.

   That may be intentional, but for regression testing classification output, a snapshot is much easier to compare if it includes either full text, a content hash, or per-line hash. At minimum add a text hash so a stale JSON cannot be mistaken for the current file.

## Editor/tagger review

10. `VerilogToken/VerilogTokenTagProvider.cs:44-48` dereferences `buffer` before the null check.

   Move the debug `buffer.ContentType.TypeName` line after `if (buffer == null) return null;`.

11. `Classification/VerilogClassifier.cs:125-135` creates a token aggregator before the singleton classifier is retrieved.

   The comment says the classifier must be singleton per buffer, but each `CreateTagger` call still creates a new `VerilogTagAggregator` before `GetOrCreateSingletonProperty(...)`. Move `aggregatorFactory.CreateTagAggregator<VerilogTokenTag>(buffer)` inside the singleton lambda so it only happens when the singleton classifier is first created.

12. `VerilogToken/VerilogTokenTagger.cs:207-227` still scans from the start of the document to the current span every time `GetTags` runs.

   This is likely still a large-file performance problem. `GetTags` calls `IsOpenBlockComment(spans)` at line 352; `IsOpenBlockComment` walks `sc[0].Snapshot.Lines` from the beginning to the requested span. For a large file, repeated viewport classifications become O(file-prefix length) over and over. Cache block-comment state per line or rely on parse-state data generated during reparse.

13. `VerilogToken/VerilogTokenTagger.cs:47-50` and `:72` create a timer and subscribe to `_buffer.Changed`, but the tagger does not implement disposal.

   Since the branch now intentionally makes the token tagger a singleton per buffer, this is less dangerous than repeated taggers, but the timer and event subscription still deserve a lifecycle plan. Either implement disposal through a known buffer-closed path, or avoid keeping a `Timer` alive after it is disabled.

14. `Completion/CompletionController.cs:123-126` marks the file as needing reparse for every VSStd2K command.

   This is safer than stale parse state, but it can be noisy. Consider only setting `NeedReparse` for commands that actually change the buffer, or let `BufferChanged` be the source of truth.

## Packaging/release cleanup

15. `VerilogLanguage.csproj:42-43` includes debug symbols in the VSIX globally.

   That is useful for local debug, but probably not for Release/Marketplace. Move those properties into the Debug property group or set them false for Release.

16. `source.extension.vsixmanifest:33-34` still links to `blob/master` even though the repo default branch is `main`.

   Use `blob/main` for README/release notes links, or use repository-relative release pages that do not depend on the old branch name.

17. `VerilogLanguage.claude.csproj` is a stale alternate project file.

   It has some useful project-reference ideas, but it points at `VerilogLanguage.vsct` and `SnapshotExporter\VerilogLanguagePackage.cs`, neither of which exists in this branch. Delete it or rename it clearly as notes outside the solution.

## Recommended next patch order

1. Fix `VerilogLanguage.csproj` by starting from `development-main` and adding only the VS package, VSCT, Resources, and SnapshotExporter compile items.
2. Delete `FirstCommand.cs`, `VerilogLanguage.claude.csproj`, generated `obj/`, restored `packages/`, and stale local artifacts.
3. Fix `build_vsix.bat` paths and make it fail fast with `if errorlevel 1 exit /b 1`.
4. Fix the two singleton/resource issues: move classifier aggregator creation into the singleton lambda and dispose the snapshot export aggregator.
5. Then test in `/rootsuffix Exp` that the Tools menu command appears and that `link /dump /resources bin\Debug\VerilogLanguage.dll | findstr /i CTMENU` finds the menu resource.
