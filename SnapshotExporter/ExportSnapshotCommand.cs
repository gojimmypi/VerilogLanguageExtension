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
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.ComponentModel.Design;
using System.IO;
using System.Threading.Tasks;

namespace VerilogLanguage.Testing
{
    internal sealed class ExportSnapshotCommand
    {
        private readonly AsyncPackage _package;
        private readonly OleMenuCommand _command;

        private ExportSnapshotCommand(AsyncPackage package, OleMenuCommandService commandService) {
            _package = package;

            var cmdId = new CommandID(GuidList.GuidVerilogLanguageCmdSet, PkgCmdIDList.CmdIdExportVerilogSnapshot);

            _command = new OleMenuCommand(Execute, cmdId);
            _command.BeforeQueryStatus += OnBeforeQueryStatus;

            commandService.AddCommand(_command);
        }

        public static async Task InitializeAsync(AsyncPackage package) {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            object commandServiceObj = await package.GetServiceAsync(typeof(IMenuCommandService)).ConfigureAwait(true);
            var commandService = commandServiceObj as OleMenuCommandService;
            if (commandService == null) {
                return;
            }

            _ = new ExportSnapshotCommand(package, commandService);
        }

        private void OnBeforeQueryStatus(object sender, EventArgs e) {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Always show/enable. Even if view acquisition fails, we show a message box.
            _command.Visible = true;
            _command.Enabled = true;
        }

        private void Execute(object sender, EventArgs e) {
            ThreadHelper.ThrowIfNotOnUIThread();

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                try {
                    await ExecuteAsync().ConfigureAwait(true);
                }
                catch (Exception ex) {
                    VsShellUtilities.ShowMessageBox(
                        _package,
                        ex.ToString(),
                        "Export Verilog Snapshot - Exception",
                        OLEMSGICON.OLEMSGICON_CRITICAL,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                }
            });
        }

        private async Task ExecuteAsync() {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            IWpfTextView view = await TryGetActiveWpfTextView_NoAdaptersAsync().ConfigureAwait(true);
            if (view == null) {
                VsShellUtilities.ShowMessageBox(
                    _package,
                    "No active editor view found.",
                    "Export Verilog Snapshot",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }

            string filePath = await TryGetActiveDocumentPathAsync().ConfigureAwait(true);

            object componentModelObj = await _package.GetServiceAsync(typeof(SComponentModel)).ConfigureAwait(true);
            var componentModel = componentModelObj as IComponentModel;
            if (componentModel == null) {
                VsShellUtilities.ShowMessageBox(
                    _package,
                    "SComponentModel not available.",
                    "Export Verilog Snapshot",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }

            var classifierAgg = componentModel.GetService<IClassifierAggregatorService>();
            var bufferAggFactory = componentModel.GetService<IBufferTagAggregatorFactoryService>();

            if (classifierAgg == null || bufferAggFactory == null) {
                VsShellUtilities.ShowMessageBox(
                    _package,
                    "Required editor services not available.",
                    "Export Verilog Snapshot",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }

            var exporter = new SnapshotExporter(classifierAgg, bufferAggFactory);

            EditorSnapshotExport export = exporter.Export(view, filePath);

            string outDir = Path.Combine(Path.GetTempPath(), "VerilogLanguageSnapshot");
            string outFile = Path.Combine(outDir, MakeSafeFileName(filePath) + ".snapshot.json");
            SnapshotExporter.WriteJson(export, outFile);

            VsShellUtilities.ShowMessageBox(
                _package,
                "Wrote snapshot:\r\n" + outFile,
                "Export Verilog Snapshot",
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        private static string MakeSafeFileName(string filePath) {
            if (string.IsNullOrEmpty(filePath)) {
                return "untitled";
            }

            string name = Path.GetFileName(filePath);
            foreach (char c in Path.GetInvalidFileNameChars()) {
                name = name.Replace(c, '_');
            }

            return name;
        }

        // Avoid IVsEditorAdaptersFactoryService.
        // Pull IWpfTextViewHost from IVsTextView via IVsUserData.
        private async Task<IWpfTextView> TryGetActiveWpfTextView_NoAdaptersAsync() {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            object textManagerObj = await _package.GetServiceAsync(typeof(SVsTextManager)).ConfigureAwait(true);
            var textManager = textManagerObj as IVsTextManager;
            if (textManager == null) {
                return null;
            }

            IVsTextView activeView;
            int hr = textManager.GetActiveView(1, null, out activeView);
            if (ErrorHandler.Failed(hr) || activeView == null) {
                return null;
            }

            var userData = activeView as IVsUserData;
            if (userData == null) {
                return null;
            }

            object holder;
            Guid guidViewHost = Microsoft.VisualStudio.Editor.DefGuidList.guidIWpfTextViewHost;

            hr = userData.GetData(ref guidViewHost, out holder);
            if (ErrorHandler.Failed(hr) || holder == null) {
                return null;
            }

            var host = holder as IWpfTextViewHost;
            if (host == null) {
                return null;
            }

            return host.TextView;
        }

        private async Task<string> TryGetActiveDocumentPathAsync() {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            object monitorSelectionObj = await _package.GetServiceAsync(typeof(SVsShellMonitorSelection)).ConfigureAwait(true);
            var monitorSelection = monitorSelectionObj as IVsMonitorSelection;
            if (monitorSelection == null) {
                return null;
            }

            int hr = monitorSelection.GetCurrentElementValue((uint)VSConstants.VSSELELEMID.SEID_DocumentFrame, out object frameObj);
            if (ErrorHandler.Failed(hr) || frameObj == null) {
                return null;
            }

            var frame = frameObj as IVsWindowFrame;
            if (frame == null) {
                return null;
            }

            hr = frame.GetProperty((int)__VSFPROPID.VSFPROPID_pszMkDocument, out object mkDoc);
            if (ErrorHandler.Failed(hr)) {
                return null;
            }

            return mkDoc as string;
        }
    }
}
