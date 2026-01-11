// file: SnapshotExporter/SnapshotExporter.cs
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
using System.IO;
using System.Web.Script.Serialization;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;

namespace VerilogLanguage.Testing
{
    internal sealed class SnapshotExporter
    {
        private readonly IBufferTagAggregatorFactoryService _bufferAggregatorFactory;

        public SnapshotExporter(IBufferTagAggregatorFactoryService bufferAggregatorFactory) {
            _bufferAggregatorFactory = bufferAggregatorFactory;
        }

        public EditorSnapshotExport Export(IWpfTextView textView, string filePath) {
            if (textView == null) {
                throw new ArgumentNullException("textView");
            }

            ITextSnapshot snapshot = textView.TextSnapshot;

            var export = new EditorSnapshotExport();
            export.FilePath = filePath;
            export.SnapshotLength = snapshot.Length;
            export.SnapshotVersion = snapshot.Version.VersionNumber;

            ExportClassifierTags(snapshot, export);
            ExportVerilogTokenTags(snapshot, export);

            return export;
        }

        private void ExportClassifierTags(ITextSnapshot snapshot, EditorSnapshotExport export) {
            if (_bufferAggregatorFactory == null) {
                return;
            }

            // This captures the ClassificationTags produced by your VerilogClassifier.
            var agg = _bufferAggregatorFactory.CreateTagAggregator<ClassificationTag>(snapshot.TextBuffer);

            SnapshotSpan all = new SnapshotSpan(snapshot, 0, snapshot.Length);

            foreach (IMappingTagSpan<ClassificationTag> mts in agg.GetTags(all)) {
                if (mts == null || mts.Tag == null) {
                    continue;
                }

                NormalizedSnapshotSpanCollection mapped = mts.Span.GetSpans(snapshot);
                if (mapped == null || mapped.Count == 0) {
                    continue;
                }

                SnapshotSpan s = mapped[0];

                var run = new ClassificationRun();
                run.Start = s.Start.Position;
                run.Length = s.Length;

                // ClassificationTag.ClassificationType.Classification is the string name
                if (mts.Tag.ClassificationType != null) {
                    run.Types.Add(mts.Tag.ClassificationType.Classification);
                }

                export.Classifications.Add(run);
            }
        }

        private void ExportVerilogTokenTags(ITextSnapshot snapshot, EditorSnapshotExport export) {
            if (_bufferAggregatorFactory == null) {
                return;
            }

            // Export underlying VerilogTokenTag spans too (useful for debugging).
            var agg = _bufferAggregatorFactory.CreateTagAggregator<VerilogLanguage.VerilogToken.VerilogTokenTag>(snapshot.TextBuffer);

            SnapshotSpan all = new SnapshotSpan(snapshot, 0, snapshot.Length);

            foreach (IMappingTagSpan<VerilogLanguage.VerilogToken.VerilogTokenTag> mts in agg.GetTags(all)) {
                if (mts == null || mts.Tag == null) {
                    continue;
                }

                NormalizedSnapshotSpanCollection mapped = mts.Span.GetSpans(snapshot);
                if (mapped == null || mapped.Count == 0) {
                    continue;
                }

                SnapshotSpan s = mapped[0];

                var tr = new TagRun();
                tr.Start = s.Start.Position;
                tr.Length = s.Length;
                tr.TagType = typeof(VerilogLanguage.VerilogToken.VerilogTokenTag).FullName;
                tr.TagDetail = mts.Tag.type.ToString();

                export.Tags.Add(tr);
            }
        }

        public static void WriteJson(EditorSnapshotExport export, string outputFile) {
            if (export == null) {
                throw new ArgumentNullException("export");
            }

            string dir = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            var serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;

            string json = serializer.Serialize(export);

            File.WriteAllText(outputFile, json);
        }
    }
}
