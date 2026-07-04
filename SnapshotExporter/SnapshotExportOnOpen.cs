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

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading;
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
#if DEBUG
        /* See ExportAfterDelayAsync */
        private static int _snapshotSequence;
#endif

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
            if (textView == null || textView.IsClosed) {
                return;
            }

            if (!SnapshotExportSettings.IsExportOnOpenEnabled()) {
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
            try {
                // Must remain on UI thread for editor services; delay still yields to message pump.
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (textView == null || textView.IsClosed) {
                    return;
                }

                // Two yields + a configurable delay avoids "empty" exports before classification/taggers are ready.
                await Task.Yield();
                await Task.Delay(SnapshotExportSettings.GetDelayMs()).ConfigureAwait(true);
                await Task.Yield();

                if (textView == null || textView.IsClosed) {
                    return;
                }

                string filePath = null;

                ITextDocument doc;
                ITextBuffer editBuffer = textView.TextViewModel != null
                    ? textView.TextViewModel.EditBuffer
                    : textView.TextBuffer;

                if (TextDocumentFactoryService != null &&
                    TextDocumentFactoryService.TryGetTextDocument(editBuffer, out doc) &&
                    doc != null) {
                    filePath = doc.FilePath;
                }

                var exporter = new VerilogLanguage.Testing.SnapshotExporter(ClassifierAggregatorService, BufferTagAggregatorFactoryService);

                EditorSnapshotExport export = exporter.Export(textView, filePath, SnapshotExportSettings.GetRunName());

                int sequence = Interlocked.Increment(ref _snapshotSequence);
                string outFile = SnapshotExportSettings.MakeSnapshotFilePath(filePath, sequence);

                VerilogLanguage.Testing.SnapshotExporter.WriteJson(export, outFile);
            }
            catch (Exception ex) {
                Trace.WriteLine("VerilogLanguage SnapshotExportOnOpen failed: " + ex);
            }
        }
#endif
    }
}
