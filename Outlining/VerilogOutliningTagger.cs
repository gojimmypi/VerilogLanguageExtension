// File: VerilogOutliningTagger.cs

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Text.Tagging;

namespace VerilogLanguage.CodeOutlining
{
    internal sealed class VerilogOutliningTagger : ITagger<IOutliningRegionTag>
    {
        private const int PendingBlockMaxLineDistance = 3;
        private const int MaxCollapsedHeaderLength = 96;

        private readonly ITextBuffer _buffer;
        private readonly object _syncLock = new object();
        private int _cachedSnapshotVersion = -1;
        private List<VerilogOutliningRegion> _cachedRegions = new List<VerilogOutliningRegion>();

        internal VerilogOutliningTagger(ITextBuffer buffer) {
            _buffer = buffer;
            _buffer.Changed += OnBufferChanged;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
            if (spans == null || spans.Count == 0) {
                yield break;
            }

            ITextSnapshot snapshot = spans[0].Snapshot;
            List<VerilogOutliningRegion> regions = GetRegions(snapshot);

            foreach (VerilogOutliningRegion region in regions) {
                if (region.Start >= snapshot.Length || region.End > snapshot.Length || region.End <= region.Start) {
                    continue;
                }

                SnapshotSpan regionSpan = new SnapshotSpan(snapshot, Span.FromBounds(region.Start, region.End));
                if (!IntersectsWithRequestedSpans(regionSpan, spans)) {
                    continue;
                }

                yield return new TagSpan<IOutliningRegionTag>(
                    regionSpan,
                    new OutliningRegionTag(
                        false,
                        false,
                        region.CollapsedForm,
                        region.CollapsedForm));
            }
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e) {
            lock (_syncLock) {
                _cachedSnapshotVersion = -1;
                _cachedRegions = new List<VerilogOutliningRegion>();
            }

            if (e.After != null) {
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(e.After, 0, e.After.Length)));
            }
        }

        private List<VerilogOutliningRegion> GetRegions(ITextSnapshot snapshot) {
            int versionNumber = snapshot.Version.VersionNumber;

            lock (_syncLock) {
                if (_cachedSnapshotVersion == versionNumber) {
                    return _cachedRegions;
                }
            }

            List<VerilogOutliningRegion> regions = ParseRegions(snapshot);

            lock (_syncLock) {
                _cachedSnapshotVersion = versionNumber;
                _cachedRegions = regions;
                return _cachedRegions;
            }
        }

        private static List<VerilogOutliningRegion> ParseRegions(ITextSnapshot snapshot) {
            List<VerilogOutliningRegion> regions = new List<VerilogOutliningRegion>();
            List<VerilogOutlineStart> scopeStack = new List<VerilogOutlineStart>();
            List<VerilogOutlineStart> directiveStack = new List<VerilogOutlineStart>();
            PendingBlock pendingBlock = null;
            bool inBlockComment = false;

            for (int lineNumber = 0; lineNumber < snapshot.LineCount; lineNumber++) {
                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineNumber);

                if (pendingBlock != null && lineNumber - pendingBlock.LineNumber > PendingBlockMaxLineDistance) {
                    pendingBlock = null;
                }

                string originalLineText = line.GetText();
                string codeLineText = RemoveCommentsAndStrings(originalLineText, ref inBlockComment);
                List<string> tokens = GetCodeTokens(codeLineText);

                foreach (string token in tokens) {
                    switch (token) {
                        case "`ifdef":
                        case "`ifndef":
                            directiveStack.Add(CreateStart("directive", line, originalLineText));
                            pendingBlock = null;
                            break;

                        case "`endif":
                            CloseScope(directiveStack, regions, snapshot, lineNumber, IsDirectiveScope);
                            pendingBlock = null;
                            break;

                        case "module":
                            scopeStack.Add(CreateStart("module", line, originalLineText));
                            pendingBlock = null;
                            break;

                        case "endmodule":
                            CloseScope(scopeStack, regions, snapshot, lineNumber, IsModuleScope);
                            pendingBlock = null;
                            break;

                        case "function":
                            scopeStack.Add(CreateStart("function", line, originalLineText));
                            pendingBlock = null;
                            break;

                        case "endfunction":
                            CloseScope(scopeStack, regions, snapshot, lineNumber, IsFunctionScope);
                            pendingBlock = null;
                            break;

                        case "task":
                            scopeStack.Add(CreateStart("task", line, originalLineText));
                            pendingBlock = null;
                            break;

                        case "endtask":
                            CloseScope(scopeStack, regions, snapshot, lineNumber, IsTaskScope);
                            pendingBlock = null;
                            break;

                        case "case":
                        case "casex":
                        case "casez":
                            scopeStack.Add(CreateStart("case", line, originalLineText));
                            pendingBlock = null;
                            break;

                        case "endcase":
                            CloseScope(scopeStack, regions, snapshot, lineNumber, IsCaseScope);
                            pendingBlock = null;
                            break;

                        case "always":
                        case "always_ff":
                        case "always_comb":
                        case "always_latch":
                            pendingBlock = CreatePendingBlock("always", line, originalLineText);
                            break;

                        case "if":
                            if (pendingBlock == null || pendingBlock.Kind != "else" || pendingBlock.LineNumber != lineNumber) {
                                pendingBlock = CreatePendingBlock("if", line, originalLineText);
                            }
                            break;

                        case "else":
                            pendingBlock = CreatePendingBlock("else", line, originalLineText);
                            break;

                        case "begin":
                            if (pendingBlock != null) {
                                scopeStack.Add(new VerilogOutlineStart(
                                    pendingBlock.Kind,
                                    pendingBlock.LineNumber,
                                    pendingBlock.Start,
                                    pendingBlock.Header));
                                pendingBlock = null;
                            }
                            else {
                                scopeStack.Add(CreateStart("begin", line, originalLineText));
                            }
                            break;

                        case "end":
                            CloseScope(scopeStack, regions, snapshot, lineNumber, IsBeginScope);
                            pendingBlock = null;
                            break;

                        case ";":
                            pendingBlock = null;
                            break;
                    }
                }
            }

            regions.Sort((left, right) => left.Start == right.Start ? left.End.CompareTo(right.End) : left.Start.CompareTo(right.Start));
            return regions;
        }

        private static VerilogOutlineStart CreateStart(string kind, ITextSnapshotLine line, string originalLineText) {
            return new VerilogOutlineStart(kind, line.LineNumber, line.Start.Position, originalLineText);
        }

        private static PendingBlock CreatePendingBlock(string kind, ITextSnapshotLine line, string originalLineText) {
            return new PendingBlock(kind, line.LineNumber, line.Start.Position, originalLineText);
        }

        private static void CloseScope(
            List<VerilogOutlineStart> stack,
            List<VerilogOutliningRegion> regions,
            ITextSnapshot snapshot,
            int endLineNumber,
            Func<string, bool> isMatchingScope) {

            for (int index = stack.Count - 1; index >= 0; index--) {
                VerilogOutlineStart start = stack[index];
                if (!isMatchingScope(start.Kind)) {
                    continue;
                }

                if (stack.Count > index) {
                    stack.RemoveRange(index, stack.Count - index);
                }

                if (start.Kind != "begin") {
                    AddRegion(regions, snapshot, start, endLineNumber);
                }
                return;
            }
        }

        private static void AddRegion(
            List<VerilogOutliningRegion> regions,
            ITextSnapshot snapshot,
            VerilogOutlineStart start,
            int endLineNumber) {

            if (endLineNumber <= start.LineNumber || start.LineNumber < 0 || endLineNumber >= snapshot.LineCount) {
                return;
            }

            ITextSnapshotLine endLine = snapshot.GetLineFromLineNumber(endLineNumber);
            int startPosition = start.Start;
            int endPosition = endLine.End.Position;

            if (endPosition <= startPosition) {
                return;
            }

            regions.Add(new VerilogOutliningRegion(
                startPosition,
                endPosition,
                BuildCollapsedForm(start.Header)));
        }

        private static bool IntersectsWithRequestedSpans(SnapshotSpan regionSpan, NormalizedSnapshotSpanCollection spans) {
            foreach (SnapshotSpan span in spans) {
                if (span.IntersectsWith(regionSpan)) {
                    return true;
                }
            }

            return false;
        }

        private static string BuildCollapsedForm(string header) {
            string collapsedForm = CompactWhitespace(header);
            if (string.IsNullOrEmpty(collapsedForm)) {
                return "...";
            }

            if (collapsedForm.Length > MaxCollapsedHeaderLength) {
                collapsedForm = collapsedForm.Substring(0, MaxCollapsedHeaderLength - 3) + "...";
            }
            else if (!collapsedForm.EndsWith("...", StringComparison.Ordinal)) {
                collapsedForm += " ...";
            }

            return collapsedForm;
        }

        private static string CompactWhitespace(string text) {
            if (string.IsNullOrWhiteSpace(text)) {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(text.Length);
            bool lastWasWhitespace = false;

            foreach (char ch in text.Trim()) {
                if (char.IsWhiteSpace(ch)) {
                    if (!lastWasWhitespace) {
                        builder.Append(' ');
                    }
                    lastWasWhitespace = true;
                }
                else {
                    builder.Append(ch);
                    lastWasWhitespace = false;
                }
            }

            return builder.ToString();
        }

        private static string RemoveCommentsAndStrings(string text, ref bool inBlockComment) {
            StringBuilder builder = new StringBuilder(text.Length);

            for (int index = 0; index < text.Length; index++) {
                char ch = text[index];

                if (inBlockComment) {
                    if (ch == '*' && index + 1 < text.Length && text[index + 1] == '/') {
                        inBlockComment = false;
                        index++;
                    }
                    continue;
                }

                if (ch == '/' && index + 1 < text.Length && text[index + 1] == '/') {
                    break;
                }

                if (ch == '/' && index + 1 < text.Length && text[index + 1] == '*') {
                    inBlockComment = true;
                    index++;
                    continue;
                }

                if (ch == '"') {
                    index = SkipString(text, index);
                    continue;
                }

                builder.Append(ch);
            }

            return builder.ToString();
        }

        private static int SkipString(string text, int index) {
            bool isEscaped = false;

            for (int next = index + 1; next < text.Length; next++) {
                char ch = text[next];

                if (isEscaped) {
                    isEscaped = false;
                    continue;
                }

                if (ch == '\\') {
                    isEscaped = true;
                    continue;
                }

                if (ch == '"') {
                    return next;
                }
            }

            return text.Length - 1;
        }

        private static List<string> GetCodeTokens(string text) {
            List<string> tokens = new List<string>();

            for (int index = 0; index < text.Length;) {
                char ch = text[index];

                if (ch == '\\') {
                    index = SkipEscapedIdentifier(text, index);
                    continue;
                }

                if (ch == '`') {
                    int end = index + 1;
                    while (end < text.Length && IsIdentifierPart(text[end])) {
                        end++;
                    }

                    if (end > index + 1) {
                        tokens.Add(text.Substring(index, end - index));
                    }

                    index = end;
                    continue;
                }

                if (IsIdentifierStart(ch)) {
                    int end = index + 1;
                    while (end < text.Length && IsIdentifierPart(text[end])) {
                        end++;
                    }

                    tokens.Add(text.Substring(index, end - index));
                    index = end;
                    continue;
                }

                if (ch == ';') {
                    tokens.Add(";");
                }

                index++;
            }

            return tokens;
        }

        private static int SkipEscapedIdentifier(string text, int index) {
            int end = index + 1;
            while (end < text.Length && !char.IsWhiteSpace(text[end])) {
                end++;
            }

            return end;
        }

        private static bool IsIdentifierStart(char ch) {
            return char.IsLetter(ch) || ch == '_';
        }

        private static bool IsIdentifierPart(char ch) {
            return char.IsLetterOrDigit(ch) || ch == '_' || ch == '$';
        }

        private static bool IsDirectiveScope(string kind) {
            return kind == "directive";
        }

        private static bool IsModuleScope(string kind) {
            return kind == "module";
        }

        private static bool IsFunctionScope(string kind) {
            return kind == "function";
        }

        private static bool IsTaskScope(string kind) {
            return kind == "task";
        }

        private static bool IsCaseScope(string kind) {
            return kind == "case";
        }

        private static bool IsBeginScope(string kind) {
            return kind == "begin" || kind == "if" || kind == "else" || kind == "always";
        }

        private sealed class PendingBlock
        {
            internal PendingBlock(string kind, int lineNumber, int start, string header) {
                Kind = kind;
                LineNumber = lineNumber;
                Start = start;
                Header = header;
            }

            internal string Kind { get; private set; }
            internal int LineNumber { get; private set; }
            internal int Start { get; private set; }
            internal string Header { get; private set; }
        }

        private sealed class VerilogOutlineStart
        {
            internal VerilogOutlineStart(string kind, int lineNumber, int start, string header) {
                Kind = kind;
                LineNumber = lineNumber;
                Start = start;
                Header = header;
            }

            internal string Kind { get; private set; }
            internal int LineNumber { get; private set; }
            internal int Start { get; private set; }
            internal string Header { get; private set; }
        }

        private sealed class VerilogOutliningRegion
        {
            internal VerilogOutliningRegion(int start, int end, string collapsedForm) {
                Start = start;
                End = end;
                CollapsedForm = collapsedForm;
            }

            internal int Start { get; private set; }
            internal int End { get; private set; }
            internal string CollapsedForm { get; private set; }
        }
    }
}
