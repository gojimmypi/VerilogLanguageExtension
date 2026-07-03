// file: SnapshotExporter/ExportSnapshotCommand.cs
//***************************************************************************
//
//  MIT License
//
//  Copyright(c) 2025 gojimmypi
//
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in all
//  copies or substantial portions of the Software.
//
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//  SOFTWARE.
//
//***************************************************************************


using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VerilogLanguage.Testing;
using Task = System.Threading.Tasks.Task;

using Microsoft.VisualStudio.Utilities;
using System.Collections.Generic;

namespace VerilogLanguage.Testing
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class ExportSnapshotCommand
    {
        /// <summary>
        /// Snapshot exporter command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Go To Definition command ID.
        /// </summary>
        public const int GoToDefinitionCommandId = 0x0102;

        /// <summary>
        /// Command menu group (command set GUID). See the mathcing value in VerilogLanguagePackage.vsct:
        ///   GuidSymbol name="guidVerilogLanguagePackageCmdSet"
        /// </summary>
        public static readonly Guid CommandSet = new Guid("C7F8B2F5-7A01-4A07-8E4B-4A29F77A0B9F");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExportSnapshotCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private ExportSnapshotCommand(AsyncPackage package, OleMenuCommandService commandService) {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);

            var goToDefinitionCommandID = new CommandID(CommandSet, GoToDefinitionCommandId);
            var goToDefinitionMenuItem = new OleMenuCommand(this.ExecuteGoToDefinition, goToDefinitionCommandID);
            goToDefinitionMenuItem.BeforeQueryStatus += this.BeforeQueryStatusGoToDefinition;
            commandService.AddCommand(goToDefinitionMenuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static ExportSnapshotCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package) {
            // Switch to the main thread - the call to AddCommand in SnapshotExporter's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new ExportSnapshotCommand(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e) {
            ThreadHelper.ThrowIfNotOnUIThread();

            try {
                IWpfTextView textView = GetActiveWpfTextView();
                if (textView == null) {
                    VsShellUtilities.ShowMessageBox(
                        this.package,
                        "No active editor text view was found.",
                        "Snapshot Exporter",
                        OLEMSGICON.OLEMSGICON_WARNING,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    return;
                }

                IComponentModel componentModel = Package.GetGlobalService(typeof(SComponentModel)) as IComponentModel;
                if (componentModel == null) {
                    VsShellUtilities.ShowMessageBox(
                        this.package,
                        "SComponentModel was not available (MEF).",
                        "Snapshot Exporter",
                        OLEMSGICON.OLEMSGICON_CRITICAL,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    return;
                }

                IClassifierAggregatorService ClassifierAggregatorService =
                    componentModel.GetService<IClassifierAggregatorService>();

                IBufferTagAggregatorFactoryService BufferTagAggregatorFactoryService =
                    componentModel.GetService<IBufferTagAggregatorFactoryService>();

                ITextDocumentFactoryService textDocumentFactoryService =
                    componentModel.GetService<ITextDocumentFactoryService>();

                string filePath = null;
                if (textDocumentFactoryService != null) {
                    ITextDocument doc;
                    ITextBuffer editBuffer = textView.TextViewModel != null
                        ? textView.TextViewModel.EditBuffer
                        : textView.TextBuffer;

                    if (textDocumentFactoryService.TryGetTextDocument(editBuffer, out doc) && doc != null) {
                        filePath = doc.FilePath;
                    }
                }

                var exporter = new VerilogLanguage.Testing.SnapshotExporter(ClassifierAggregatorService, BufferTagAggregatorFactoryService);

                EditorSnapshotExport export = exporter.Export(textView, filePath);

                string outFile = SnapshotExportSettings.MakeSnapshotFilePath(filePath, 0);

                VerilogLanguage.Testing.SnapshotExporter.WriteJson(export, outFile);

                string message = string.Format(CultureInfo.CurrentCulture, "Snapshot exported to:\r\n{0}", outFile);
                OLEMSGICON icon = OLEMSGICON.OLEMSGICON_INFO;

                if (export.Errors != null && export.Errors.Count > 0) {
                    message = string.Format(
                        CultureInfo.CurrentCulture,
                        "Snapshot exported to:\r\n{0}\r\n\r\nWarning: {1} exporter error(s) were recorded in the JSON output.",
                        outFile,
                        export.Errors.Count);
                    icon = OLEMSGICON.OLEMSGICON_WARNING;
                }

                VsShellUtilities.ShowMessageBox(
                    this.package,
                    message,
                    "Snapshot Exporter",
                    icon,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
            catch (Exception ex) {
                VsShellUtilities.ShowMessageBox(
                    this.package,
                    ex.ToString(),
                    "Snapshot Exporter",
                    OLEMSGICON.OLEMSGICON_CRITICAL,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        private void BeforeQueryStatusGoToDefinition(object sender, EventArgs e) {
            ThreadHelper.ThrowIfNotOnUIThread();

            OleMenuCommand command = sender as OleMenuCommand;
            if (command == null) {
                return;
            }

            IWpfTextView textView = GetActiveWpfTextView();
            bool isVerilogTextView = textView != null && IsVerilogTextView(textView);

            command.Visible = isVerilogTextView;
            command.Enabled = isVerilogTextView;
        }

        private void ExecuteGoToDefinition(object sender, EventArgs e) {
            ThreadHelper.ThrowIfNotOnUIThread();

            try {
                IWpfTextView textView = GetActiveWpfTextView();
                if (textView == null) {
                    ShowGoToDefinitionMessage("No active editor text view was found.", OLEMSGICON.OLEMSGICON_WARNING);
                    return;
                }

                SnapshotSpan tokenSpan;
                string lookupText;
                if (!TryGetIdentifierSpanAtCaret(textView, out tokenSpan, out lookupText)) {
                    ShowGoToDefinitionMessage("Place the caret on a Verilog identifier first.", OLEMSGICON.OLEMSGICON_WARNING);
                    return;
                }

                VerilogGlobals.VerilogDefinitionLocation definition;
                if (!TryFindDefinition(tokenSpan, lookupText, out definition)) {
                    ShowGoToDefinitionMessage(
                        string.Format(CultureInfo.CurrentCulture, "No definition was found for '{0}' in this file.", lookupText),
                        OLEMSGICON.OLEMSGICON_WARNING);
                    return;
                }

                MoveCaretToDefinition(textView, definition);
            }
            catch (Exception ex) {
                ShowGoToDefinitionMessage(ex.ToString(), OLEMSGICON.OLEMSGICON_CRITICAL);
            }
        }

        private void ShowGoToDefinitionMessage(string message, OLEMSGICON icon) {
            ThreadHelper.ThrowIfNotOnUIThread();

            VsShellUtilities.ShowMessageBox(
                this.package,
                message,
                "Verilog Go To Definition",
                icon,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        private static bool IsVerilogTextView(IWpfTextView textView) {
            if (textView == null || textView.TextBuffer == null || textView.TextBuffer.ContentType == null) {
                return false;
            }

            return textView.TextBuffer.ContentType.IsOfType("verilog");
        }

        private static bool TryGetIdentifierSpanAtCaret(IWpfTextView textView, out SnapshotSpan tokenSpan, out string lookupText) {
            tokenSpan = new SnapshotSpan();
            lookupText = string.Empty;

            if (textView == null || textView.TextBuffer == null || textView.TextBuffer.CurrentSnapshot == null) {
                return false;
            }

            ITextSnapshot snapshot = textView.TextBuffer.CurrentSnapshot;
            if (snapshot.Length == 0) {
                return false;
            }

            int position = textView.Caret.Position.BufferPosition.Position;
            if (position >= snapshot.Length) {
                position = snapshot.Length - 1;
            }

            if (position < 0) {
                return false;
            }

            if (!IsIdentifierCharacter(snapshot[position]) && position > 0 && IsIdentifierCharacter(snapshot[position - 1])) {
                position--;
            }

            if (!IsIdentifierCharacter(snapshot[position])) {
                return false;
            }

            int start = position;
            while (start > 0 && IsIdentifierCharacter(snapshot[start - 1])) {
                start--;
            }

            if (start > 0 && snapshot[start - 1] == '`') {
                start--;
            }

            int end = position + 1;
            while (end < snapshot.Length && IsIdentifierCharacter(snapshot[end])) {
                end++;
            }

            if (end <= start) {
                return false;
            }

            tokenSpan = new SnapshotSpan(snapshot, Span.FromBounds(start, end));
            lookupText = tokenSpan.GetText();

            return IsIdentifierLookupText(lookupText);
        }

        private static bool IsIdentifierLookupText(string text) {
            if (string.IsNullOrEmpty(text)) {
                return false;
            }

            string identifier = text;
            if (identifier.StartsWith("`", StringComparison.Ordinal)) {
                identifier = identifier.Substring(1);
            }

            if (identifier.Length == 0) {
                return false;
            }

            if (identifier[0] == '\\') {
                return identifier.Length > 1;
            }

            if (!(char.IsLetter(identifier[0]) || identifier[0] == '_')) {
                return false;
            }

            for (int i = 1; i < identifier.Length; i++) {
                if (!IsIdentifierCharacter(identifier[i])) {
                    return false;
                }
            }

            return true;
        }

        private static bool IsIdentifierCharacter(char c) {
            return char.IsLetterOrDigit(c) || c == '_' || c == '$' || c == '\\';
        }

        private static bool TryFindDefinition(
            SnapshotSpan tokenSpan,
            string lookupText,
            out VerilogGlobals.VerilogDefinitionLocation definition) {

            definition = null;

            if (tokenSpan.Snapshot == null || string.IsNullOrEmpty(lookupText)) {
                return false;
            }

            ITextSnapshot snapshot = tokenSpan.Snapshot;
            string thisFile = VerilogGlobals.GetDocumentPath(snapshot);
            if (string.IsNullOrEmpty(thisFile)) {
                return false;
            }

            VerilogGlobals.ParseDataSnapshot parseData;
            if (!VerilogGlobals.TryGetParseData(thisFile, snapshot.Version.VersionNumber, false, out parseData)) {
                VerilogGlobals.ParseStatusController.NeedReparse_SetValue(thisFile, true);
                VerilogGlobals.Reparse(snapshot.TextBuffer, thisFile);
                if (!VerilogGlobals.TryGetParseData(thisFile, snapshot.Version.VersionNumber, false, out parseData)) {
                    return false;
                }
            }

            if (parseData == null || parseData.VerilogDefinitionLocations == null) {
                return false;
            }

            ITextSnapshotLine lineInfo = snapshot.GetLineFromPosition(tokenSpan.Start.Position);
            int lineNumber = lineInfo.LineNumber;
            int linePosition = tokenSpan.Start.Position - lineInfo.Start.Position;

            string macroName = lookupText.StartsWith("`", StringComparison.Ordinal)
                ? lookupText.Substring(1)
                : string.Empty;

            if (!string.IsNullOrEmpty(macroName) && TryGetDefinitionFromScope(parseData, VerilogGlobals.SCOPE_MACRO, macroName, out definition)) {
                return true;
            }

            string moduleScope = parseData.TextModuleName(lineNumber, linePosition);
            string activeLocalScope;
            if (TryFindActiveLocalScope(snapshot, lineNumber, parseData, out activeLocalScope) &&
                TryGetDefinitionFromScope(parseData, activeLocalScope, lookupText, out definition)) {
                return true;
            }

            if (TryGetDefinitionFromScope(parseData, moduleScope, lookupText, out definition)) {
                return true;
            }

            if (TryGetDefinitionFromScope(parseData, VerilogGlobals.SCOPE_CONST, lookupText, out definition)) {
                return true;
            }

            if (parseData.VerilogVariables != null && parseData.VerilogVariables.ContainsKey(lookupText) &&
                TryGetDefinitionFromScope(parseData, lookupText, lookupText, out definition)) {
                return true;
            }

            if (TryGetDefinitionFromScope(parseData, VerilogGlobals.SCOPE_MACRO, lookupText, out definition)) {
                return true;
            }

            return false;
        }

        private static bool TryGetDefinitionFromScope(
            VerilogGlobals.ParseDataSnapshot parseData,
            string scope,
            string lookupText,
            out VerilogGlobals.VerilogDefinitionLocation definition) {

            definition = null;

            if (parseData == null || parseData.VerilogDefinitionLocations == null ||
                string.IsNullOrEmpty(scope) || string.IsNullOrEmpty(lookupText)) {
                return false;
            }

            Dictionary<string, VerilogGlobals.VerilogDefinitionLocation> scopeMap;
            if (!parseData.VerilogDefinitionLocations.TryGetValue(scope, out scopeMap) || scopeMap == null) {
                return false;
            }

            return scopeMap.TryGetValue(lookupText, out definition) && definition != null;
        }

        private static bool TryFindActiveLocalScope(
            ITextSnapshot snapshot,
            int lineNumber,
            VerilogGlobals.ParseDataSnapshot parseData,
            out string activeLocalScope) {

            activeLocalScope = string.Empty;

            if (snapshot == null || parseData == null || lineNumber < 0) {
                return false;
            }

            int lastLine = Math.Min(lineNumber, snapshot.LineCount - 1);
            for (int i = lastLine; i >= 0; i--) {
                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(i);
                string lineText = line.GetText();

                if (VerilogGlobals.IsEndFunctionLineText(lineText) || VerilogGlobals.IsEndTaskLineText(lineText)) {
                    return false;
                }

                string functionName;
                if (VerilogGlobals.TryGetFunctionNameFromLineText(lineText, out functionName)) {
                    string moduleScope = parseData.TextModuleName(line.LineNumber, 0);
                    activeLocalScope = VerilogGlobals.FunctionLocalScopeName(moduleScope, functionName);
                    return true;
                }

                string taskName;
                if (VerilogGlobals.TryGetTaskNameFromLineText(lineText, out taskName)) {
                    string moduleScope = parseData.TextModuleName(line.LineNumber, 0);
                    activeLocalScope = VerilogGlobals.TaskLocalScopeName(moduleScope, taskName);
                    return true;
                }
            }

            return false;
        }

        private static void MoveCaretToDefinition(IWpfTextView textView, VerilogGlobals.VerilogDefinitionLocation definition) {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (textView == null || definition == null || textView.TextBuffer == null) {
                return;
            }

            ITextSnapshot snapshot = textView.TextBuffer.CurrentSnapshot;
            if (definition.LineNumber < 0 || definition.LineNumber >= snapshot.LineCount) {
                return;
            }

            ITextSnapshotLine line = snapshot.GetLineFromLineNumber(definition.LineNumber);
            int column = definition.LinePosition;
            if (column < 0) {
                column = 0;
            }

            if (column > line.Length) {
                column = line.Length;
            }

            int position = line.Start.Position + column;
            textView.Caret.MoveTo(new SnapshotPoint(snapshot, position));
            textView.Selection.Clear();
            textView.ViewScroller.EnsureSpanVisible(new SnapshotSpan(snapshot, new Span(position, Math.Max(1, Math.Min(definition.Length, snapshot.Length - position)))));
            textView.VisualElement.Focus();
        }

        private IWpfTextView GetActiveWpfTextView() {
            ThreadHelper.ThrowIfNotOnUIThread();

            IVsTextManager textManager = Package.GetGlobalService(typeof(SVsTextManager)) as IVsTextManager;
            if (textManager == null) {
                return null;
            }

            IVsTextView vsTextView;
            int hr = textManager.GetActiveView(1, null, out vsTextView);
            if (hr != VSConstants.S_OK || vsTextView == null) {
                return null;
            }

            IComponentModel componentModel = Package.GetGlobalService(typeof(SComponentModel)) as IComponentModel;
            if (componentModel == null) {
                return null;
            }

            IVsEditorAdaptersFactoryService adapters =
                componentModel.GetService<IVsEditorAdaptersFactoryService>();
            if (adapters == null) {
                return null;
            }

            return adapters.GetWpfTextView(vsTextView);
        }

    } /* SnapshowExporter class */
} /* Namespace */
