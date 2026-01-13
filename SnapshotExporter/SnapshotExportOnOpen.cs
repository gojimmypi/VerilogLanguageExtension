// file: SnapshotExporter/SnapshotExportOnOpen.cs
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

using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using System.IO;
using System.Threading.Tasks;
using VerilogLanguage.Testing;

namespace VerilogLanguage
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("verilog")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class SnapshotExportOnOpen : IWpfTextViewCreationListener
    {
        [Import]
        internal ITextDocumentFactoryService TextDocumentFactoryService = null;

        [Import]
        internal IClassifierAggregatorService ClassifierAggregatorService = null;

        [Import]
        internal IBufferTagAggregatorFactoryService BufferTagAggregatorFactoryService = null;

        public void TextViewCreated(IWpfTextView textView) {
#if !DEBUG
            return;
#else
            if (textView == null) {
                return;
            }

            string gateFile = Path.Combine(Path.GetTempPath(), "VerilogLanguage.ExportSnapshots.enable");
            if (!File.Exists(gateFile)) {
                return;
            }

            if (ClassifierAggregatorService == null || BufferTagAggregatorFactoryService == null) {
                return;
            }

            // Delay export so classification/taggers have time to attach and produce spans.
            _ = ExportAfterDelayAsync(textView);
#endif
        }

#if DEBUG
        private async Task ExportAfterDelayAsync(IWpfTextView textView) {
            // Must remain on UI thread for editor services; delay still yields to message pump.
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Two yields + a small delay has proven enough to avoid "empty" exports in practice.
            await Task.Yield();
            await Task.Delay(250).ConfigureAwait(true);
            await Task.Yield();

            IServiceProvider serviceProvider =
                Package.GetGlobalService(typeof(SVsServiceProvider)) as IServiceProvider;

            IComponentModel componentModel = Package.GetGlobalService(typeof(SComponentModel)) as IComponentModel;
            if (componentModel == null) {
                VsShellUtilities.ShowMessageBox(
                    serviceProvider,
                        "SComponentModel was not available (MEF).",
                        "Snapshot Export On Open",
                        OLEMSGICON.OLEMSGICON_CRITICAL,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }

            string filePath = null;

            ITextDocument doc;

            //TextDocumentFactoryService.TryGetTextDocument(textView.TextBuffer, out doc) &&
            if (TextDocumentFactoryService != null &&
                TextDocumentFactoryService.TryGetTextDocument(textView.TextViewModel.EditBuffer, out doc) &&
                doc != null) {
                filePath = doc.FilePath;
            }
            IViewTagAggregatorFactoryService ViewTagAggregatorFactoryService =
                componentModel.GetService<IViewTagAggregatorFactoryService>();

            var exporter = new VerilogLanguage.Testing.SnapshotExporter(ClassifierAggregatorService,
                                                                         BufferTagAggregatorFactoryService,
                                                                         ViewTagAggregatorFactoryService);

            EditorSnapshotExport export = exporter.Export(textView, filePath);

            string outDir = Path.Combine(Path.GetTempPath(), "VerilogLanguageSnapshot");
            string outFile = Path.Combine(outDir, MakeSafeFileName(filePath) + ".snapshot.json");

            VerilogLanguage.Testing.SnapshotExporter.WriteJson(export, outFile);
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
#endif
    }
}
