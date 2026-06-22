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

namespace VerilogLanguage.Testing
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class ExportSnapshotCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

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
