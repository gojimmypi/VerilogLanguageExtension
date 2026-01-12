// file: SnapshotExporter/SnapshotExportOnOpen.cs
//***************************************************************************
//
//  MIT License
//
//  Copyright(c) 2025 gojimmypi
//
//***************************************************************************

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
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

            string filePath = null;

            ITextDocument doc;
            if (TextDocumentFactoryService != null &&
                TextDocumentFactoryService.TryGetTextDocument(textView.TextViewModel.EditBuffer, out doc) &&
                doc != null) {
                filePath = doc.FilePath;
            }

            var exporter = new SnapshotExporter(ClassifierAggregatorService, BufferTagAggregatorFactoryService);

            EditorSnapshotExport export = exporter.Export(textView, filePath);

            string outDir = Path.Combine(Path.GetTempPath(), "VerilogLanguageSnapshot");
            string outFile = Path.Combine(outDir, MakeSafeFileName(filePath) + ".snapshot.json");

            SnapshotExporter.WriteJson(export, outFile);
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
