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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace VerilogLanguage.Testing
{
    internal sealed class SnapshotExporter
    {
        private readonly IClassifierAggregatorService _classifierAggregatorService;
        private readonly IBufferTagAggregatorFactoryService _bufferAggregatorFactory;

        public SnapshotExporter(
            IClassifierAggregatorService ClassifierAggregatorService,
            IBufferTagAggregatorFactoryService BufferAggregatorFactory) {

            _classifierAggregatorService = ClassifierAggregatorService;
            _bufferAggregatorFactory = BufferAggregatorFactory;
        }

        public EditorSnapshotExport Export(IWpfTextView textView, string filePath) {
            return Export(textView, filePath, SnapshotExportSettings.GetRunName());
        }

        public EditorSnapshotExport Export(IWpfTextView textView, string filePath, string runName) {
            if (textView == null) {
                throw new ArgumentNullException("textView");
            }

            // IMPORTANT: use the EDIT buffer, not the view/projection buffer.
            ITextBuffer editBuffer = textView.TextViewModel != null
                ? textView.TextViewModel.EditBuffer
                : textView.TextBuffer;

            ITextSnapshot snapshot = editBuffer.CurrentSnapshot;

            var export = new EditorSnapshotExport();
            export.RunName = string.IsNullOrWhiteSpace(runName) ? SnapshotExportSettings.GetRunName() : runName;
            export.ExtensionVersion = GetExtensionVersion();
            export.GitCommit = SnapshotExportSettings.GetGitCommit();
            export.FilePath = filePath;
            export.FileRelativePath = SnapshotExportSettings.MakeRelativePath(filePath);
            export.ContentType = editBuffer.ContentType != null ? editBuffer.ContentType.TypeName : string.Empty;
            export.SnapshotLength = snapshot.Length;
            export.SnapshotVersion = snapshot.Version.VersionNumber;
            export.TextSha256 = ComputeTextSha256(snapshot);

            ExportParserTokens(snapshot, export);
            ExportClassifierSpans(editBuffer, snapshot, export);
            ExportVerilogTokenTags(editBuffer, snapshot, export);
            ExportSymbols(filePath, snapshot, export);

            return export;
        }

        private static void ExportParserTokens(ITextSnapshot snapshot, EditorSnapshotExport export) {
            if (snapshot == null) {
                AddError(export, "Parser token export skipped: snapshot was not available.");
                return;
            }

            try {
                VerilogLanguage.VerilogGlobals.VerilogToken priorToken = new VerilogLanguage.VerilogGlobals.VerilogToken();

                foreach (ITextSnapshotLine line in snapshot.Lines) {
                    string lineText = line.GetText();
                    VerilogLanguage.VerilogGlobals.VerilogToken[] tokens = VerilogLanguage.VerilogGlobals.VerilogKeywordSplit(lineText, priorToken);
                    int searchIndex = 0;

                    if (tokens != null) {
                        foreach (VerilogLanguage.VerilogGlobals.VerilogToken token in tokens) {
                            string text = token.Part ?? string.Empty;
                            if (text.Length == 0) {
                                continue;
                            }

                            int columnIndex = lineText.IndexOf(text, searchIndex, StringComparison.Ordinal);
                            if (columnIndex < 0) {
                                columnIndex = Math.Min(searchIndex, lineText.Length);
                            }

                            var run = new TokenRun();
                            run.Start = line.Start.Position + columnIndex;
                            run.Length = text.Length;
                            run.Line = line.LineNumber + 1;
                            run.Column = columnIndex + 1;
                            run.Text = text;
                            run.Context = token.Context.ToString();
                            export.Tokens.Add(run);

                            searchIndex = Math.Min(columnIndex + text.Length, lineText.Length);
                        }

                        if (tokens.Length > 0) {
                            priorToken = tokens[tokens.Length - 1];
                        }
                    }
                }
            }
            catch (Exception ex) {
                AddError(export,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Parser token export failed: {0}: {1}",
                        ex.GetType().FullName,
                        ex.Message));
            }
        }

        private void ExportClassifierSpans(ITextBuffer buffer, ITextSnapshot snapshot, EditorSnapshotExport export) {
            if (_classifierAggregatorService == null) {
                AddError(export, "Classifier export skipped: IClassifierAggregatorService was not available.");
                return;
            }

            IClassifier classifier = _classifierAggregatorService.GetClassifier(buffer);
            if (classifier == null) {
                AddError(export, "Classifier export skipped: no classifier was available for the edit buffer.");
                return;
            }

            const int chunkSize = 4096;
            int pos = 0;

            if (snapshot.Length == 0) {
                return;
            }

            while (pos < snapshot.Length) {
                int len = Math.Min(chunkSize, snapshot.Length - pos);
                var span = new SnapshotSpan(snapshot, pos, len);

                IList<ClassificationSpan> spans;
                try {
                    spans = classifier.GetClassificationSpans(span);
                }
                catch (Exception ex) {
                    AddError(export,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Classifier export failed at offset {0}: {1}: {2}",
                            pos,
                            ex.GetType().FullName,
                            ex.Message));
                    return;
                }

                if (spans != null) {
                    foreach (ClassificationSpan cs in spans) {
                        if (cs == null || cs.ClassificationType == null) {
                            continue;
                        }

                        var run = new ClassificationRun();
                        run.Start = cs.Span.Start.Position;
                        run.Length = cs.Span.Length;
                        FillSpanLocationAndText(cs.Span, run);
                        run.Types.Add(cs.ClassificationType.Classification);

                        export.Classifications.Add(run);
                    }
                }

                pos += len;
            }
        }

        private void ExportVerilogTokenTags(ITextBuffer buffer, ITextSnapshot snapshot, EditorSnapshotExport export) {
            if (_bufferAggregatorFactory == null) {
                AddError(export, "Verilog token tag export skipped: IBufferTagAggregatorFactoryService was not available.");
                return;
            }

            try {
                using (ITagAggregator<VerilogLanguage.VerilogToken.VerilogTokenTag> agg =
                    _bufferAggregatorFactory.CreateTagAggregator<VerilogLanguage.VerilogToken.VerilogTokenTag>(buffer)) {

                    if (agg == null) {
                        AddError(export, "Verilog token tag export skipped: no tag aggregator was available for the edit buffer.");
                        return;
                    }

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
                        FillSpanLocationAndText(s, tr);

                        string hoverText;
                        if (VerilogLanguage.VerilogHoverInfo.TryGetHoverText(mts.Tag.type, snapshot, s, out hoverText)) {
                            tr.HoverText = hoverText;
                        }

                        export.Tags.Add(tr);
                    }
                }
            }
            catch (Exception ex) {
                AddError(export,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Verilog token tag export failed: {0}: {1}",
                        ex.GetType().FullName,
                        ex.Message));
            }
        }

        private static void ExportSymbols(string filePath, ITextSnapshot snapshot, EditorSnapshotExport export) {
            if (snapshot == null) {
                AddError(export, "Symbol export skipped: snapshot was not available.");
                return;
            }

            string targetFile = VerilogLanguage.VerilogGlobals.GetDocumentPath(snapshot);
            if (string.IsNullOrEmpty(targetFile)) {
                targetFile = filePath;
            }

            if (string.IsNullOrEmpty(targetFile)) {
                AddError(export, "Symbol export skipped: document path was not available.");
                return;
            }

            try {
                VerilogLanguage.VerilogGlobals.ParseDataSnapshot parseData;
                if (!VerilogLanguage.VerilogGlobals.TryGetParseData(targetFile, snapshot.Version.VersionNumber, false, out parseData) ||
                    parseData == null) {
                    AddError(export,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Symbol export skipped: parse data was not available for {0} snapshot version {1}.",
                            SnapshotExportSettings.MakeRelativePath(targetFile),
                            snapshot.Version.VersionNumber));
                    return;
                }

                foreach (KeyValuePair<string, Dictionary<string, VerilogLanguage.VerilogToken.VerilogTokenTypes>> scope in parseData.VerilogVariables) {
                    if (scope.Value == null) {
                        continue;
                    }

                    foreach (KeyValuePair<string, VerilogLanguage.VerilogToken.VerilogTokenTypes> symbol in scope.Value) {
                        var run = new SymbolRun();
                        run.Scope = scope.Key;
                        run.Name = symbol.Key;
                        run.TokenType = symbol.Value.ToString();

                        Dictionary<string, string> hoverScope;
                        string hoverText;
                        if (parseData.VerilogVariableHoverText.TryGetValue(scope.Key, out hoverScope) &&
                            hoverScope != null &&
                            hoverScope.TryGetValue(symbol.Key, out hoverText)) {
                            run.HoverText = hoverText;
                        }

                        export.Symbols.Add(run);
                    }
                }
            }
            catch (Exception ex) {
                AddError(export,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Symbol export failed: {0}: {1}",
                        ex.GetType().FullName,
                        ex.Message));
            }
        }

        private static void FillSpanLocationAndText(SnapshotSpan span, ClassificationRun run) {
            ITextSnapshotLine line = span.Snapshot.GetLineFromPosition(span.Start.Position);
            run.Line = line.LineNumber + 1;
            run.Column = span.Start.Position - line.Start.Position + 1;
            run.Text = SafeGetText(span);
        }

        private static void FillSpanLocationAndText(SnapshotSpan span, TagRun run) {
            ITextSnapshotLine line = span.Snapshot.GetLineFromPosition(span.Start.Position);
            run.Line = line.LineNumber + 1;
            run.Column = span.Start.Position - line.Start.Position + 1;
            run.Text = SafeGetText(span);
        }

        private static string SafeGetText(SnapshotSpan span) {
            try {
                return span.GetText();
            }
            catch {
                return string.Empty;
            }
        }

        private static string ComputeTextSha256(ITextSnapshot snapshot) {
            if (snapshot == null) {
                throw new ArgumentNullException("snapshot");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(snapshot.GetText());
            using (SHA256 sha256 = SHA256.Create()) {
                byte[] hash = sha256.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);

                foreach (byte b in hash) {
                    builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static string GetExtensionVersion() {
            Assembly assembly = typeof(SnapshotExporter).Assembly;
            AssemblyInformationalVersionAttribute info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (info != null && !string.IsNullOrWhiteSpace(info.InformationalVersion)) {
                return info.InformationalVersion;
            }

            Version version = assembly.GetName().Version;
            return version != null ? version.ToString() : string.Empty;
        }

        private static void AddError(EditorSnapshotExport export, string message) {
            if (export == null || string.IsNullOrEmpty(message)) {
                return;
            }

            export.Errors.Add(message);
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
