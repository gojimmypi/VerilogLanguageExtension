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
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.ComponentModel.Design;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace VerilogLanguage.Testing
{
    internal sealed class ExportSnapshotCommand
    {
        private readonly AsyncPackage _package;

        private ExportSnapshotCommand(AsyncPackage package, OleMenuCommandService commandService) {
            _package = package;

            var cmdId = new CommandID(GuidList.GuidVerilogLanguageCmdSet, (int)PkgCmdIDList.CmdIdExportVerilogSnapshot);
            var cmd = new OleMenuCommand(Execute, cmdId);
            commandService.AddCommand(cmd);
        }

        public static async Task InitializeAsync(AsyncPackage package) {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService == null) {
                return;
            }

            _ = new ExportSnapshotCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e) {
            ThreadHelper.ThrowIfNotOnUIThread();

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ExecuteAsync().ConfigureAwait(true);
            });
        }

        private async Task ExecuteAsync() {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            IWpfTextView view = await TryGetActiveWpfTextViewAsync().ConfigureAwait(true);
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

            var componentModelObj = await _package.GetServiceAsync(typeof(SComponentModel)).ConfigureAwait(true);
            var componentModel = componentModelObj as IComponentModel;
            if (componentModel == null) {
                return;
            }

            var bufferAggFactory = componentModel.GetService<IBufferTagAggregatorFactoryService>();
            if (bufferAggFactory == null) {
                VsShellUtilities.ShowMessageBox(
                    _package,
                    "IBufferTagAggregatorFactoryService not available.",
                    "Export Verilog Snapshot",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }

            var exporter = new SnapshotExporter(bufferAggFactory);

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

        private async Task<IWpfTextView> TryGetActiveWpfTextViewAsync() {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var textManagerObj = await _package.GetServiceAsync(typeof(SVsTextManager)).ConfigureAwait(true);
            var textManager = textManagerObj as IVsTextManager;
            if (textManager == null) {
                return null;
            }

            var componentModelObj = await _package.GetServiceAsync(typeof(SComponentModel)).ConfigureAwait(true);
            var componentModel = componentModelObj as IComponentModel;
            if (componentModel == null) {
                return null;
            }

            var editorAdapters = componentModel.GetService<IVsEditorAdaptersFactoryService>();
            if (editorAdapters == null) {
                return null;
            }

            IVsTextView activeView;
            int hr = textManager.GetActiveView(1, null, out activeView);
            if (ErrorHandler.Failed(hr) || activeView == null) {
                return null;
            }

            return editorAdapters.GetWpfTextView(activeView);
        }

        private async Task<string> TryGetActiveDocumentPathAsync() {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var monitorSelectionObj = await _package.GetServiceAsync(typeof(SVsShellMonitorSelection)).ConfigureAwait(true);
            var monitorSelection = monitorSelectionObj as IVsMonitorSelection;
            if (monitorSelection == null) {
                return null;
            }

            IntPtr hierarchyPtr = IntPtr.Zero;
            IntPtr docDataPtr = IntPtr.Zero;

            try {
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
            finally {
                if (hierarchyPtr != IntPtr.Zero) {
                    Marshal.Release(hierarchyPtr);
                }

                if (docDataPtr != IntPtr.Zero) {
                    Marshal.Release(docDataPtr);
                }
            }
        }
    }
}
