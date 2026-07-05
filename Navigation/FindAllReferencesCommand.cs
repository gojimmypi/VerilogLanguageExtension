// file: Navigation/FindAllReferencesCommand.cs
//***************************************************************************
//
//  MIT License
//
//  Copyright (c) 2025-2026 gojimmypi
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
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace VerilogLanguage.Navigation
{
    internal sealed class FindAllReferencesCommand
    {
        public const int CommandId = 0x0104;
        public static readonly Guid CommandSet = new Guid("C7F8B2F5-7A01-4A07-8E4B-4A29F77A0B9F");

        private static readonly Guid OutputPaneGuid = new Guid("F0B86396-7E2B-4999-9EC6-17E7D73B4D2F");
        private const string OutputPaneTitle = "VLE Find All References";

        private readonly AsyncPackage package;

        private FindAllReferencesCommand(AsyncPackage package, OleMenuCommandService commandService) {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var commandID = new CommandID(CommandSet, CommandId);
            var menuItem = new OleMenuCommand(this.Execute, commandID);
            menuItem.BeforeQueryStatus += this.BeforeQueryStatus;
            commandService.AddCommand(menuItem);
        }

        public static FindAllReferencesCommand Instance
        {
            get;
            private set;
        }

        public static async Task InitializeAsync(AsyncPackage package) {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new FindAllReferencesCommand(package, commandService);
        }

        private void BeforeQueryStatus(object sender, EventArgs e) {
            ThreadHelper.ThrowIfNotOnUIThread();

            OleMenuCommand command = sender as OleMenuCommand;
            if (command == null) {
                return;
            }

            IWpfTextView textView = GetActiveWpfTextView();
            bool isVerilogTextView = IsVerilogTextView(textView);
            command.Visible = isVerilogTextView;
            command.Enabled = isVerilogTextView;
        }

        private void Execute(object sender, EventArgs e) {
            ThreadHelper.ThrowIfNotOnUIThread();

            try {
                IWpfTextView textView = GetActiveWpfTextView();
                if (textView == null) {
                    ShowMessage("No active editor text view was found.", OLEMSGICON.OLEMSGICON_WARNING);
                    return;
                }

                SnapshotSpan tokenSpan;
                string lookupText;
                if (!VerilogDefinitionResolver.TryGetIdentifierSpanAtCaret(textView, out tokenSpan, out lookupText)) {
                    ShowMessage("Place the caret on a Verilog identifier first.", OLEMSGICON.OLEMSGICON_WARNING);
                    return;
                }

                VerilogGlobals.VerilogDefinitionLocation definition;
                List<VerilogReferenceFinder.VerilogReferenceLocation> references;
                string failureMessage;
                if (!VerilogReferenceFinder.TryFindReferences(tokenSpan, lookupText, out definition, out references, out failureMessage)) {
                    ShowMessage(failureMessage, OLEMSGICON.OLEMSGICON_WARNING);
                    return;
                }

                WriteReferencesToOutputPane(lookupText, definition, references);
            }
            catch (Exception ex) {
                ShowMessage(ex.ToString(), OLEMSGICON.OLEMSGICON_CRITICAL);
            }
        }

        private void WriteReferencesToOutputPane(
            string lookupText,
            VerilogGlobals.VerilogDefinitionLocation definition,
            IList<VerilogReferenceFinder.VerilogReferenceLocation> references) {

            ThreadHelper.ThrowIfNotOnUIThread();

            IVsOutputWindowPane pane = GetOutputPane();
            if (pane == null) {
                ShowMessage("The Visual Studio Output window was not available.", OLEMSGICON.OLEMSGICON_WARNING);
                return;
            }

            pane.Clear();

            string definitionText = definition == null
                ? string.Empty
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "Definition: {0}({1},{2}) scope={3}",
                    definition.FilePath,
                    definition.LineNumber + 1,
                    definition.LinePosition + 1,
                    definition.Scope);

            StringBuilder builder = new StringBuilder();
            builder.AppendFormat(CultureInfo.InvariantCulture, "Find All References for '{0}'\r\n", lookupText);
            if (!string.IsNullOrEmpty(definitionText)) {
                builder.Append(definitionText);
                builder.Append("\r\n");
            }

            builder.AppendFormat(CultureInfo.InvariantCulture, "References found: {0}\r\n", references == null ? 0 : references.Count);
            builder.Append("Double-click a file(line,column) line in the Output window to navigate.\r\n\r\n");

            if (references != null) {
                foreach (VerilogReferenceFinder.VerilogReferenceLocation reference in references) {
                    builder.AppendFormat(
                        CultureInfo.InvariantCulture,
                        "{0}({1},{2}): {3}: {4}",
                        reference.FilePath,
                        reference.LineNumber + 1,
                        reference.LinePosition + 1,
                        reference.Kind,
                        reference.LineText);

                    if (!string.IsNullOrEmpty(reference.ContainingType) || !string.IsNullOrEmpty(reference.ContainingMember)) {
                        builder.AppendFormat(
                            CultureInfo.InvariantCulture,
                            "    [{0}{1}{2}]",
                            reference.ContainingType,
                            string.IsNullOrEmpty(reference.ContainingMember) ? string.Empty : " / ",
                            reference.ContainingMember);
                    }

                    builder.Append("\r\n");
                }
            }

            pane.OutputString(builder.ToString());
            ShowOutputWindow();
            pane.Activate();
        }

        private static void ShowOutputWindow() {
            ThreadHelper.ThrowIfNotOnUIThread();

            dynamic dte = Package.GetGlobalService(typeof(SDTE));
            if (dte == null) {
                return;
            }

            try {
                dte.ExecuteCommand("View.Output");
            }
            catch {
                // If the Output window cannot be opened automatically, the results
                // still remain available from View -> Output.
            }
        }

        private static IVsOutputWindowPane GetOutputPane() {
            ThreadHelper.ThrowIfNotOnUIThread();

            IVsOutputWindow outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (outputWindow == null) {
                return null;
            }

            Guid paneGuid = OutputPaneGuid;
            outputWindow.CreatePane(ref paneGuid, OutputPaneTitle, 1, 1);

            IVsOutputWindowPane pane;
            if (ErrorHandler.Failed(outputWindow.GetPane(ref paneGuid, out pane))) {
                return null;
            }

            return pane;
        }

        private void ShowMessage(string message, OLEMSGICON icon) {
            ThreadHelper.ThrowIfNotOnUIThread();

            VsShellUtilities.ShowMessageBox(
                this.package,
                message,
                "Verilog Find All References",
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

        private static IWpfTextView GetActiveWpfTextView() {
            ThreadHelper.ThrowIfNotOnUIThread();

            IVsTextManager textManager = Package.GetGlobalService(typeof(SVsTextManager)) as IVsTextManager;
            if (textManager == null) {
                return null;
            }

            IVsTextView vsTextView;
            int hr = textManager.GetActiveView(0, null, out vsTextView);
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
    }
}
