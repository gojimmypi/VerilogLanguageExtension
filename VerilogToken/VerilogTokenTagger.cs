// file: VerilogToken/VerilogTokenTagger.cs
//***************************************************************************
//
//  MIT License
//
//  Copyright (c) 2019-2026 gojimmypi
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

// Optional settings:
// #define USE_JTF   /* Enable JoinableTaskFactory instead of SynchronizationContext */
// #define TAG_DEBUG /* Emit some System.Diagnostics.Debug messages */

namespace VerilogLanguage.VerilogToken
{
    using System;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Tagging;
    using CommentHelper;
    using System.Threading;
    using Microsoft.VisualStudio.Threading;
    using System.Linq.Expressions;

    internal sealed class VerilogTokenTagger : ITagger<VerilogTokenTag>, IDisposable
    {

        private Timer _reparseCompletionTimer;
        private readonly object _reparseCompletionTimerLock = new object();
        private string _lastReparseFile = string.Empty;

        private int _initialInvalidateAttempted;
        private volatile bool _disposed;

        private const int ReparseCompletionTimerMaxTicks = 200;
        private int _reparseCompletionTimerTicks;

        private readonly object _blockCommentStateLock = new object();
        private ITextSnapshot _blockCommentStateSnapshot;
        private readonly List<bool> _blockCommentStateAtLineStart = new List<bool>();

        private struct AttributeScanState
        {
            internal bool IsAttributeOpen;
            internal bool IsBlockCommentOpen;
        }

        private readonly object _attributeStateLock = new object();
        private ITextSnapshot _attributeStateSnapshot;
        private readonly List<AttributeScanState> _attributeStateAtLineStart = new List<AttributeScanState>();

        private readonly object _preprocessorStateLock = new object();
        private ITextSnapshot _preprocessorStateSnapshot;
        private List<VerilogPreprocessorEvaluator.LineState> _preprocessorLineStates =
            new List<VerilogPreprocessorEvaluator.LineState>();

        // ITextView View { get; set; }
        private readonly ITextBuffer _buffer;
        private bool _systemVerilogDocumentKnown;
        private bool _isSystemVerilogDocument;

#if USE_JTF
        private readonly JoinableTaskFactory _jtf;
#else
        private readonly SynchronizationContext _uiContext;
#endif
        internal VerilogTokenTagger(ITextBuffer buffer) {
            VerilogGlobals.PerfMon.VerilogTokenTagger_Count++;
            VerilogGlobals.TheBuffer = buffer;
            _buffer = buffer;

#if USE_JTF
            // Prefer VS JTF for UI-thread switches (avoids VSTHRD001).
            _jtf = ThreadHelper.JoinableTaskFactory;
#else
            _uiContext = SynchronizationContext.Current;
#endif

            this._buffer.Changed += BufferChanged;
            VerilogGlobals.ParseDataPublished += ParseDataPublished;

            // Initial parse is required so module/variable tables exist before the first classification pass.
            TriggerReparseAndRefreshAll();

            // Do not rely on ctor-time invalidate; there are often no subscribers yet.
            // We will do a one-shot invalidate when GetTags is first called (subscribers exist then).
        }

        /// <summary>
        /// Trigger an initial reparse and ensure we repaint AFTER threaded parsing completes.
        /// </summary>
        private void TriggerReparseAndRefreshAll() {
            string thisFile = VerilogLanguage.VerilogGlobals.GetDocumentPath(_buffer.CurrentSnapshot);
            if (string.IsNullOrEmpty(thisFile)) {
                return;
            }

            VerilogGlobals.ParseStatusController.NeedReparse_SetValue(thisFile, true);

            // Start parse (may be threaded).
            VerilogGlobals.Reparse(_buffer, thisFile);

            // If threaded, the data will not be ready yet. Refresh again once the thread completes.
            StartOrResetReparseCompletionWatcher(thisFile);
        }

        private void StartOrResetReparseCompletionWatcher(string forFile) {
            if (_disposed || string.IsNullOrEmpty(forFile)) {
                return;
            }

            lock (_reparseCompletionTimerLock) {
                if (_disposed) {
                    return;
                }

                _lastReparseFile = forFile;
                _reparseCompletionTimerTicks = 0;

                if (_reparseCompletionTimer == null) {
                    _reparseCompletionTimer = new Timer(ReparseCompletionTimerCallback, null, 50, 50);
                    return;
                }

                try {
                    _reparseCompletionTimer.Change(50, 50);
                }
                catch (ObjectDisposedException) {
                    // A callback from an older parse can race with a new parse request.
                    // Recreate the watcher instead of letting a disposed Timer break classification.
                    _reparseCompletionTimer = new Timer(ReparseCompletionTimerCallback, null, 50, 50);
                }
            }
        }

        private void ParseDataPublished(object sender, EventArgs e) {
            if (_disposed) {
                return;
            }

            ITextSnapshot snapshot = null;
            try {
                snapshot = _buffer.CurrentSnapshot;
            }
            catch {
                snapshot = null;
            }

            if (snapshot != null) {
                InvalidateAll(snapshot);
            }
        }

        private void ReparseCompletionTimerCallback(object state) {
            if (_disposed) {
                return;
            }

            string forFile = _lastReparseFile;
            if (string.IsNullOrEmpty(forFile)) {
                return;
            }

            // Wait for parse to complete, then invalidate all.
            // Bound the watcher so a stuck parse flag cannot keep a Timer alive forever.
            if (VerilogGlobals.ParseStatusController.IsReparsing(forFile)) {
                _reparseCompletionTimerTicks++;
                if (_reparseCompletionTimerTicks >= ReparseCompletionTimerMaxTicks) {
                    StopReparseCompletionWatcher();
                }
                return;
            }

            // Stop and dispose the timer (one-shot behavior).
            StopReparseCompletionWatcher();

            ITextSnapshot snapshot = null;
            try {
                snapshot = _buffer.CurrentSnapshot;
            }
            catch {
                snapshot = null;
            }

            if (snapshot != null) {
                InvalidateAll(snapshot);
            }
        }

        /// <summary>
        ///   BufferChanged - handle Buffer Changed event. If buffer has a character with possible far-reaching consequences
        ///                   then force a rescan of the enture buffer. See also HighlightWordTaggerProvider
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void BufferChanged(object sender, TextContentChangedEventArgs e) {
            if (_disposed) {
                return;
            }

            bool preprocessorStateChanged = ChangeTouchesPreprocessorDirective(e);

            InvalidateBlockCommentStateCache();
            InvalidateAttributeStateCache();
            InvalidatePreprocessorStateCache();

            // If this isn't the most up-to-date version of the buffer, then ignore it for now (we'll eventually get another change event).
            if (e.After != _buffer.CurrentSnapshot) {
                return;
            }

            if (e.Changes == null || e.Changes.Count < 1) {
                // TODO - how did we get here if there are no changes? (found this after exception during debug. no apparent invoke. )
                return;
            }

            // Always invalidate the affected span so classification refreshes reliably (even when we do not do a full reparse).
            InvalidateChangedSpan(e);

            bool forceReparse = preprocessorStateChanged;

            if (preprocessorStateChanged) {
                // A macro definition or conditional can change highlighting far beyond
                // the edited line, so repaint the complete snapshot immediately.
                InvalidateAll(e.After);
            }

            foreach (ITextChange change in e.Changes) {
                string theNewText = change.NewText;
                string theOldText = change.OldText;

                // we are only interested when the old and new text are different.
                // yes, the event seems to be triggered even with no apparent changes
                //
                if (theNewText != theOldText) {
                    // even if the buffer is different, only certain characters require a full reparse
                    // typically brackets (since we keep track of depth) and comment chars:
                    if (VerilogGlobals.ContainsRefreshChar(theNewText) || VerilogGlobals.ContainsRefreshChar(theOldText)) {
                        forceReparse = true;
                        break;
                    }
                }
                else {
                    System.Diagnostics.Debug.WriteLine("BufferChanged called but new and old text not different!");
                }
            }

            if (forceReparse) {
                string thisFile = VerilogLanguage.VerilogGlobals.GetDocumentPath(_buffer.CurrentSnapshot);
                if (string.IsNullOrEmpty(thisFile)) {
                    return;
                }

                VerilogGlobals.ParseStatusController.NeedReparse_SetValue(thisFile, true);

                // Start parse (may be threaded).
                VerilogGlobals.Reparse(_buffer, thisFile);

                // IMPORTANT: If threaded, InvalidateAll now is too early. Watch for completion and invalidate then.
                StartOrResetReparseCompletionWatcher(thisFile);

                // Still invalidate now to avoid "stuck" visuals; completion watcher will repaint again when ready.
                InvalidateAll(_buffer.CurrentSnapshot);
            }
        }

        private bool IsOpenBlockComment(NormalizedSnapshotSpanCollection sc) {
            VerilogGlobals.PerfMon.VerilogTokenTagger_IsOpenBlockComment_Count++;

            if (sc == null || sc.Count == 0 || sc[0].Snapshot == null || sc[0].Start.Position <= 0) {
                return false;
            }

            ITextSnapshot snapshot = sc[0].Snapshot;
            int startPosition = sc[0].Start.Position;
            if (startPosition > snapshot.Length) {
                startPosition = snapshot.Length;
            }

            ITextSnapshotLine line = snapshot.GetLineFromPosition(startPosition);
            bool isLocalBlockComment = GetBlockCommentStateAtLineStart(snapshot, line.LineNumber);

            int prefixLength = startPosition - line.Start.Position;
            if (prefixLength > 0) {
                string linePrefix = line.GetText().Substring(0, prefixLength);
                CommentHelper commentHelper = new CommentHelper(linePrefix, false, isLocalBlockComment);
                isLocalBlockComment = commentHelper.HasBlockStartComment;
            }

            return isLocalBlockComment;
        }

        private bool GetBlockCommentStateAtLineStart(ITextSnapshot snapshot, int lineNumber) {
            if (snapshot == null || lineNumber <= 0) {
                return false;
            }

            lock (_blockCommentStateLock) {
                if (!object.ReferenceEquals(_blockCommentStateSnapshot, snapshot)) {
                    _blockCommentStateSnapshot = snapshot;
                    _blockCommentStateAtLineStart.Clear();
                    _blockCommentStateAtLineStart.Add(false); // line 0 is never continued from a prior line
                }

                while (_blockCommentStateAtLineStart.Count <= lineNumber) {
                    int previousLineNumber = _blockCommentStateAtLineStart.Count - 1;
                    bool isLocalBlockComment = _blockCommentStateAtLineStart[previousLineNumber];
                    ITextSnapshotLine previousLine = snapshot.GetLineFromLineNumber(previousLineNumber);
                    CommentHelper commentHelper = new CommentHelper(previousLine.GetText(), false, isLocalBlockComment);
                    _blockCommentStateAtLineStart.Add(commentHelper.HasBlockStartComment);
                }

                return _blockCommentStateAtLineStart[lineNumber];
            }
        }

        private void InvalidateBlockCommentStateCache() {
            lock (_blockCommentStateLock) {
                _blockCommentStateSnapshot = null;
                _blockCommentStateAtLineStart.Clear();
            }
        }

        private AttributeScanState GetAttributeStateAtLineStart(ITextSnapshot snapshot, int lineNumber) {
            if (snapshot == null || lineNumber <= 0) {
                return new AttributeScanState();
            }

            lock (_attributeStateLock) {
                if (!object.ReferenceEquals(_attributeStateSnapshot, snapshot)) {
                    _attributeStateSnapshot = snapshot;
                    _attributeStateAtLineStart.Clear();
                    _attributeStateAtLineStart.Add(new AttributeScanState());
                }

                while (_attributeStateAtLineStart.Count <= lineNumber) {
                    int previousLineNumber = _attributeStateAtLineStart.Count - 1;
                    AttributeScanState previousLineState = _attributeStateAtLineStart[previousLineNumber];
                    ITextSnapshotLine previousLine = snapshot.GetLineFromLineNumber(previousLineNumber);
                    AttributeScanState nextLineState;
                    GetAttributeLineSpans(previousLine.GetText(), previousLineState, out nextLineState);
                    _attributeStateAtLineStart.Add(nextLineState);
                }

                return _attributeStateAtLineStart[lineNumber];
            }
        }

        private static List<Span> GetAttributeLineSpans(
            string lineText,
            AttributeScanState lineStartState,
            out AttributeScanState lineEndState) {
            List<Span> attributeSpans = null;
            bool isAttributeOpen = lineStartState.IsAttributeOpen;
            bool isBlockCommentOpen = lineStartState.IsBlockCommentOpen;
            bool isStringOpen = false;
            bool isEscaped = false;
            bool isEscapedIdentifierOpen = false;
            int attributeStart = isAttributeOpen ? 0 : -1;

            if (lineText == null) {
                lineText = string.Empty;
            }

            for (int i = 0; i < lineText.Length; i++) {
                char currentChar = lineText[i];
                char nextChar = (i + 1 < lineText.Length) ? lineText[i + 1] : '\0';

                if (isBlockCommentOpen) {
                    if (currentChar == '*' && nextChar == '/') {
                        isBlockCommentOpen = false;
                        i++;
                    }

                    continue;
                }

                if (isStringOpen) {
                    if (isEscaped) {
                        isEscaped = false;
                    }
                    else if (currentChar == '\\') {
                        isEscaped = true;
                    }
                    else if (currentChar == '"') {
                        isStringOpen = false;
                    }

                    continue;
                }

                if (isEscapedIdentifierOpen) {
                    if (char.IsWhiteSpace(currentChar)) {
                        isEscapedIdentifierOpen = false;
                    }

                    continue;
                }

                if (currentChar == '\\') {
                    isEscapedIdentifierOpen = true;
                    continue;
                }

                if (currentChar == '/' && nextChar == '/') {
                    break;
                }

                if (currentChar == '/' && nextChar == '*') {
                    isBlockCommentOpen = true;
                    i++;
                    continue;
                }

                if (currentChar == '"') {
                    isStringOpen = true;
                    isEscaped = false;
                    continue;
                }

                // Do not confuse a wildcard event control such as @(*) or @(* )
                // with an attribute opener. Both forms begin with the same (* pair.
                bool isWildcardEventControl = IsWildcardEventControlStart(lineText, i);
                if (!isAttributeOpen && currentChar == '(' && nextChar == '*' && !isWildcardEventControl) {
                    isAttributeOpen = true;
                    attributeStart = i;
                    i++;
                    continue;
                }

                if (isAttributeOpen && currentChar == '*' && nextChar == ')') {
                    int attributeEnd = i + 2;
                    AddAttributeLineSpan(ref attributeSpans, attributeStart, attributeEnd);
                    isAttributeOpen = false;
                    attributeStart = -1;
                    i++;
                }
            }

            if (isAttributeOpen && attributeStart >= 0) {
                AddAttributeLineSpan(ref attributeSpans, attributeStart, lineText.Length);
            }

            lineEndState = new AttributeScanState {
                IsAttributeOpen = isAttributeOpen,
                IsBlockCommentOpen = isBlockCommentOpen
            };

            return attributeSpans;
        }

        private static bool IsWildcardEventControlStart(string lineText, int openParenIndex) {
            if (string.IsNullOrEmpty(lineText) ||
                openParenIndex < 0 ||
                openParenIndex + 1 >= lineText.Length ||
                lineText[openParenIndex] != '(' ||
                lineText[openParenIndex + 1] != '*') {
                return false;
            }

            for (int i = openParenIndex - 1; i >= 0; i--) {
                if (!char.IsWhiteSpace(lineText[i])) {
                    return lineText[i] == '@';
                }
            }

            return false;
        }

        private static void AddAttributeLineSpan(ref List<Span> spans, int start, int end) {
            int length = end - start;
            if (start < 0 || length <= 0) {
                return;
            }

            if (spans == null) {
                spans = new List<Span>();
            }

            spans.Add(new Span(start, length));
        }

        private static bool IntersectsLineSpan(
            SnapshotSpan snapshotSpan,
            ITextSnapshotLine containingLine,
            List<Span> lineSpans) {
            if (containingLine == null || lineSpans == null || lineSpans.Count == 0) {
                return false;
            }

            int relativeStart = snapshotSpan.Start.Position - containingLine.Start.Position;
            int relativeEnd = relativeStart + snapshotSpan.Length;

            foreach (Span lineSpan in lineSpans) {
                if (relativeStart < lineSpan.End && lineSpan.Start < relativeEnd) {
                    return true;
                }
            }

            return false;
        }

        private void InvalidateAttributeStateCache() {
            lock (_attributeStateLock) {
                _attributeStateSnapshot = null;
                _attributeStateAtLineStart.Clear();
            }
        }

        private VerilogPreprocessorEvaluator.LineState GetPreprocessorLineState(
            ITextSnapshot snapshot,
            int lineNumber) {

            if (snapshot == null || lineNumber < 0 || lineNumber >= snapshot.LineCount) {
                return new VerilogPreprocessorEvaluator.LineState(true, false);
            }

            lock (_preprocessorStateLock) {
                if (!object.ReferenceEquals(_preprocessorStateSnapshot, snapshot)) {
                    List<string> lines = new List<string>(snapshot.LineCount);
                    for (int currentLine = 0; currentLine < snapshot.LineCount; currentLine++) {
                        lines.Add(snapshot.GetLineFromLineNumber(currentLine).GetText());
                    }

                    VerilogPreprocessorEvaluator.AnalysisResult analysis =
                        VerilogPreprocessorEvaluator.Analyze(lines);

                    _preprocessorStateSnapshot = snapshot;
                    _preprocessorLineStates = analysis.LineStates;
                }

                if (lineNumber >= _preprocessorLineStates.Count) {
                    return new VerilogPreprocessorEvaluator.LineState(true, false);
                }

                return _preprocessorLineStates[lineNumber];
            }
        }

        private void InvalidatePreprocessorStateCache() {
            lock (_preprocessorStateLock) {
                _preprocessorStateSnapshot = null;
                _preprocessorLineStates.Clear();
            }
        }

        private static bool ChangeTouchesPreprocessorDirective(TextContentChangedEventArgs e) {
            if (e == null || e.Changes == null) {
                return false;
            }

            foreach (ITextChange change in e.Changes) {
                if (change == null) {
                    continue;
                }

                if ((!string.IsNullOrEmpty(change.OldText) && change.OldText.IndexOf('`') >= 0) ||
                    (!string.IsNullOrEmpty(change.NewText) && change.NewText.IndexOf('`') >= 0) ||
                    SnapshotRangeContainsPreprocessorMarker(e.Before, change.OldPosition, change.OldLength) ||
                    SnapshotRangeContainsPreprocessorMarker(e.After, change.NewPosition, change.NewLength)) {

                    return true;
                }
            }

            return false;
        }

        private static bool SnapshotRangeContainsPreprocessorMarker(
            ITextSnapshot snapshot,
            int position,
            int length) {

            if (snapshot == null || snapshot.LineCount == 0) {
                return false;
            }

            if (snapshot.Length == 0) {
                return false;
            }

            int startPosition = position;
            if (startPosition < 0) {
                startPosition = 0;
            }
            if (startPosition >= snapshot.Length) {
                startPosition = snapshot.Length - 1;
            }

            int endPosition = position + length;
            if (endPosition < startPosition) {
                endPosition = startPosition;
            }
            if (endPosition >= snapshot.Length) {
                endPosition = snapshot.Length - 1;
            }

            int startLine = snapshot.GetLineFromPosition(startPosition).LineNumber;
            int endLine = snapshot.GetLineFromPosition(endPosition).LineNumber;
            startLine = Math.Max(0, startLine - 1);
            endLine = Math.Min(snapshot.LineCount - 1, endLine + 1);

            for (int lineNumber = startLine; lineNumber <= endLine; lineNumber++) {
                if (snapshot.GetLineFromLineNumber(lineNumber).GetText().IndexOf('`') >= 0) {
                    return true;
                }
            }

            return false;
        }

        private void StopReparseCompletionWatcher() {
            Timer timerToDispose;

            lock (_reparseCompletionTimerLock) {
                if (_reparseCompletionTimer == null) {
                    return;
                }

                timerToDispose = _reparseCompletionTimer;
                _reparseCompletionTimer = null;
                _lastReparseFile = string.Empty;
                _reparseCompletionTimerTicks = 0;
            }

            try {
                // disables the System.Threading.Timer before disposing it:
                timerToDispose.Change(Timeout.Infinite, Timeout.Infinite);
            }
            catch (ObjectDisposedException) {
                // Already disposed by a racing callback or cleanup path.
            }

            timerToDispose.Dispose();
        }

        public void Dispose() {
            if (_disposed) {
                return;
            }

            _disposed = true;
            _buffer.Changed -= BufferChanged;
            VerilogGlobals.ParseDataPublished -= ParseDataPublished;
            StopReparseCompletionWatcher();
            InvalidateBlockCommentStateCache();
            InvalidateAttributeStateCache();
            InvalidatePreprocessorStateCache();
        }

        private void RaiseTagsChanged(SnapshotSpan span) {
            if (_disposed) {
                return;
            }

            var handler = TagsChanged;
            if (handler == null) {
                return;
            }

#if USE_JTF
            if (!_jtf.Context.IsOnMainThread) {
                _jtf.RunAsync(async () =>
                {
                    await _jtf.SwitchToMainThreadAsync();
                    handler(this, new SnapshotSpanEventArgs(span));
                });
                return;
            }
#else
    #if DEBUG
            if (_uiContext == null) {
                System.Diagnostics.Debug.WriteLine("Warning: _uiContext is null; TagsChanged may be raised off UI thread.");
            }
    #endif
            if (_uiContext != null && SynchronizationContext.Current != _uiContext) {
                // NOTE: Using SynchronizationContext.Post can trigger analyzer warning VSTHRD001.
                // In this extension, enabling the JTF-based path has caused repaint issues, so we keep Post
                // and locally suppress the analyzer at this call site.
    #pragma warning disable VSTHRD001
                _uiContext.Post(_ => handler(this, new SnapshotSpanEventArgs(span)), null); // Post causes warning VSTHRD001: Await JoinableTaskFactory.SwitchToMainThreadAsync() to switch to the UI thread instead of APIs that can deadlock or require specifying a priority (htt
    #pragma warning restore VSTHRD001
                return;
            }

#endif

            handler(this, new SnapshotSpanEventArgs(span));
        }

        private void InvalidateAll(ITextSnapshot snapshot) {
            if (snapshot == null || snapshot.Length == 0) {
                return;
            }

            RaiseTagsChanged(new SnapshotSpan(snapshot, 0, snapshot.Length));
        }

        private void InvalidateChangedSpan(TextContentChangedEventArgs e) {
            ITextSnapshot snapshot = e.After;
            if (snapshot == null || snapshot.Length == 0) {
                return;
            }

            // Compute a conservative span to refresh from the first changed position to the end of the last changed region.
            int start = int.MaxValue;
            int end = 0;

            foreach (ITextChange change in e.Changes) {
                if (change == null) {
                    continue;
                }

                if (change.NewPosition < start) {
                    start = change.NewPosition;
                }

                int changeEnd = change.NewPosition + change.NewLength;
                if (changeEnd > end) {
                    end = changeEnd;
                }
            }

            if (start == int.MaxValue) {
                return;
            }

            if (start < 0) {
                start = 0;
            }

            if (end < start) {
                end = start;
            }

            if (end > snapshot.Length) {
                end = snapshot.Length;
            }

            int length = end - start;
            if (length <= 0) {
                ITextSnapshotLine line = snapshot.GetLineFromPosition(start);
                RaiseTagsChanged(line.Extent);
                return;
            }

            RaiseTagsChanged(new SnapshotSpan(snapshot, start, length));
        }

        private bool TryGetParseDataForCurrentBuffer(out VerilogGlobals.ParseDataSnapshot parseData) {
            parseData = null;

            string thisFile = VerilogLanguage.VerilogGlobals.GetDocumentPath(_buffer.CurrentSnapshot);
            if (string.IsNullOrEmpty(thisFile)) {
                return false;
            }

            bool thisNeedReparse = VerilogGlobals.ParseStatusController.NeedReparse(thisFile);
            bool thisIsReparsing = VerilogGlobals.ParseStatusController.IsReparsing(thisFile);

            // Do not accept cached parse data while the file is marked dirty.
            // The old order returned stale parse data before honoring NeedReparse.
            if (thisNeedReparse) {
                if (thisIsReparsing) {
                    StartOrResetReparseCompletionWatcher(thisFile);
                    return false;
                }

                VerilogGlobals.Reparse(_buffer, thisFile);
                StartOrResetReparseCompletionWatcher(thisFile);

                thisNeedReparse = VerilogGlobals.ParseStatusController.NeedReparse(thisFile);
                thisIsReparsing = VerilogGlobals.ParseStatusController.IsReparsing(thisFile);

                if (thisNeedReparse || thisIsReparsing) {
                    return false;
                }

                return VerilogGlobals.TryGetParseData(thisFile, _buffer, false, out parseData);
            }

            if (VerilogGlobals.TryGetParseData(thisFile, _buffer, false, out parseData)) {
                return true;
            }

            thisIsReparsing = VerilogGlobals.ParseStatusController.IsReparsing(thisFile);
            if (thisIsReparsing) {
                StartOrResetReparseCompletionWatcher(thisFile);
                return false;
            }

            VerilogGlobals.ParseStatusController.NeedReparse_SetValue(thisFile, true);
            VerilogGlobals.Reparse(_buffer, thisFile);
            StartOrResetReparseCompletionWatcher(thisFile);

            thisNeedReparse = VerilogGlobals.ParseStatusController.NeedReparse(thisFile);
            thisIsReparsing = VerilogGlobals.ParseStatusController.IsReparsing(thisFile);

            if (thisNeedReparse || thisIsReparsing) {
                return false;
            }

            return VerilogGlobals.TryGetParseData(thisFile, _buffer, false, out parseData);
        }
        /// <summary>
        ///   IEnumerable VerilogTokenTag GetTags
        /// </summary>
        /// <param name="spans"></param>
        /// <returns></returns>
        public IEnumerable<ITagSpan<VerilogTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
            if (_disposed) {
                yield break;
            }

            //while (VerilogGlobals.IsReparsing)
            {
                // do we really want to do this? (probably not)
                // System.Threading.Thread.Sleep(10);
            }

            if (spans == null || spans.Count == 0) {
                yield break;
            }

            VerilogGlobals.ParseDataSnapshot parseData;
            bool haveParseData = TryGetParseDataForCurrentBuffer(out parseData);

            // This is the reliable place to trigger the initial full repaint:
            // by the time VS asks for tags, the downstream aggregators are subscribed.
            if (Interlocked.Exchange(ref _initialInvalidateAttempted, 1) == 0) {
                InvalidateAll(_buffer.CurrentSnapshot);
            }

            //System.Diagnostics.Debug.WriteLine("Starting IEnumerable<ITagSpan<VerilogTokenTag>>");
            // bool EditInProgress = spans.snapshot.TextBuffer.EditInProgress;
            // since we can start mid-text, we don't know if the current span is in the middle of a comment

            // init TODO - we don't really want to call this for every enumeration!
            // VerilogGlobals.InitHoverBuilder();
            bool isContinuedBlockComment = IsOpenBlockComment(spans); // TODO - does spans always contain the full document? (appears perhaps not)

            VerilogGlobals.VerilogToken[] tokens = null;
            VerilogGlobals.VerilogToken priorToken = new VerilogGlobals.VerilogToken();
            HashSet<Span> yieldedAttributeSpans = new HashSet<Span>();
            HashSet<Span> yieldedInactiveCodeSpans = new HashSet<Span>();

            // look at each span for tokens, comments, etc
            foreach (SnapshotSpan curSpan in spans) {
                if (curSpan.IsEmpty) {
                    System.Diagnostics.Debug.WriteLine("VerilogTokenTagger.GetTags: curSpan.IsEmpty");
                    /* just means this span covers no characters, but
                     *   SnapshotSpan = (Snapshot + Start + Length)
                     *   Snapshot represents the entire text buffer,
                     * So, no "contonue" here. curSpan also never Null */
                }

                ITextSnapshot snapshot = curSpan.Snapshot;
                if (snapshot is null) {
                    /* Nothing to do */
                    System.Diagnostics.Debug.WriteLine("VerilogTokenTagger.GetTags: snapshot is null");
                    continue;
                }

                if (snapshot.Length == 0) {
                    /* Nothing to do; this span covers no characters */
                    System.Diagnostics.Debug.WriteLine("VerilogTokenTagger.GetTags: snapshot.Length == 0");
                    continue;
                }

                int startPos = curSpan.Start.Position;
                int endPos = curSpan.End.Position;

                if (startPos < 0) {
                    startPos = 0;
                }
                if (endPos > snapshot.Length) {
                    endPos = snapshot.Length;
                }
                if (endPos < startPos) {
                    /* How did this happen? */
                    System.Diagnostics.Debug.WriteLine("Oddity in VerilogTokenTagger.GetTags: startPos > endPos");
                    continue;
                }

                ITextSnapshotLine line = snapshot.GetLineFromPosition(startPos);
                AttributeScanState attributeState = GetAttributeStateAtLineStart(snapshot, line.LineNumber);
                string activeLocalScope = string.Empty;
                if (haveParseData && parseData != null) {
                    TryFindActiveLocalScope(line.Snapshot, line.LineNumber, parseData, out activeLocalScope);
                }

                while (line != null && line.Start.Position < endPos) {
                    if (tokens != null && tokens.Length >= 1) {
                        priorToken = tokens[tokens.Length - 1];
                    }

                    string lineText = line.GetText();
                    VerilogPreprocessorEvaluator.LineState preprocessorLineState =
                        GetPreprocessorLineState(snapshot, line.LineNumber);

                    List<Span> attributeLineSpans = GetAttributeLineSpans(lineText, attributeState, out attributeState);
                    tokens = VerilogGlobals.VerilogKeywordSplit(lineText, priorToken);

                    if (!preprocessorLineState.IsActive && !preprocessorLineState.IsDirective) {
                        // Keep lexical continuation state correct across inactive lines,
                        // but suppress all normal syntax classifications for their text.
                        CommentHelper inactiveCommentHelper =
                            new CommentHelper(lineText, false, isContinuedBlockComment);
                        isContinuedBlockComment = inactiveCommentHelper.HasBlockStartComment;

                        SnapshotSpan inactiveCodeSpan = line.Extent;
                        Span inactiveSpan = inactiveCodeSpan.Span;
                        if (inactiveCodeSpan.Length > 0 &&
                            inactiveCodeSpan.IntersectsWith(curSpan) &&
                            yieldedInactiveCodeSpans.Add(inactiveSpan)) {

                            yield return new TagSpan<VerilogTokenTag>(
                                inactiveCodeSpan,
                                new VerilogTokenTag(VerilogTokenTypes.Verilog_InactiveCode));
                        }

                        if (line.LineBreakLength == 0) {
                            break;
                        }

                        int inactiveNextLineStart = line.EndIncludingLineBreak.Position;
                        if (inactiveNextLineStart >= snapshot.Length) {
                            break;
                        }

                        line = snapshot.GetLineFromPosition(inactiveNextLineStart);
                        continue;
                    }

                    if (haveParseData && parseData != null) {
                        UpdateActiveLocalScopeForLineStart(line, parseData, ref activeLocalScope);
                    }

                    List<Span> staticStringLineSpans = GetStaticStringLineSpans(lineText);

                    if (attributeLineSpans != null) {
                        foreach (Span lineSpan in attributeLineSpans) {
                            Span absoluteSpan = new Span(line.Start.Position + lineSpan.Start, lineSpan.Length);
                            SnapshotSpan attributeSnapshotSpan = new SnapshotSpan(line.Snapshot, absoluteSpan);
                            if (attributeSnapshotSpan.IntersectsWith(curSpan) && yieldedAttributeSpans.Add(absoluteSpan)) {
                                yield return new TagSpan<VerilogTokenTag>(
                                    attributeSnapshotSpan,
                                    new VerilogTokenTag(VerilogTokenTypes.Verilog_Attribute));
                            }
                        }
                    }

                    int curLoc = line.Start.Position;
                    bool isContinuedLineComment = false; // comments with "//" are only effective for the current line, but /* can span multiple lines
                    foreach (VerilogGlobals.VerilogToken verilogToken in tokens) { // this group of tokens in in a single line
                        string tokenText = verilogToken.Part ?? string.Empty;
                        int tokenLength = tokenText.Length;
                        if (tokenLength <= 0) {
                            continue;
                        }

                        ITextSnapshot snap = line.Snapshot;
                        if (curLoc < 0 || curLoc >= snap.Length) {
                            curLoc += tokenLength;
                            continue; // at EOF (or invalid), cannot make a non-empty span
                        }

                        if (curLoc + tokenLength > snap.Length) {
                            tokenLength = snap.Length - curLoc;
                            if (tokenLength <= 0) {
                                continue;
                            }
                        }

                        if (verilogToken.Context == VerilogGlobals.VerilogTokenContextType.DoubleQuoteOpen) {
                            SnapshotSpan directTokenSpan;
                            try {
                                directTokenSpan = new SnapshotSpan(line.Snapshot, new Span(curLoc, tokenLength));
                            }
                            catch (Exception ex) {
                                Console.WriteLine($"Error in VerilogTokenTagger.GetTags: {ex.Message}");
                                curLoc += tokenLength;
                                continue;
                            }

                            if (directTokenSpan.IntersectsWith(curSpan) &&
                                !IntersectsLineSpan(directTokenSpan, line, attributeLineSpans)) {
                                yield return new TagSpan<VerilogTokenTag>(
                                    directTokenSpan,
                                    new VerilogTokenTag(VerilogTokenTypes.Verilog_Value));
                            }

                            curLoc += tokenLength;
                            continue;
                        }

                        // by the time we get here, we might have a tag with adjacent comments:
                        //     assign//
                        //     //assign
                        //     assign//comment
                        //     /*assign*/
                        //     assign/*comment*/
                        CommentHelper commentHelper;
                        CreateCommentHelper(
                            tokenText,
                            isContinuedLineComment,
                            isContinuedBlockComment,
                            out commentHelper,
                            out isContinuedLineComment,
                            out isContinuedBlockComment);

                        foreach (CommentHelper.CommentItem item in commentHelper.CommentItems) {
                            /* This next length section is typically for not processing EOF, but perhaps will occur elsewhere */
                            int len = item.ItemText.Length;

                            if (len <= 0) {
                                continue;
                            }

                            if (curLoc < 0 || curLoc >= snap.Length) {
                                curLoc += len;
                                continue; // at EOF (or invalid), cannot make a non-empty span
                            }

                            if (curLoc + len > snap.Length) {
                                len = snap.Length - curLoc; // clamp (or just continue)
                                if (len <= 0) {
                                    continue;
                                }
                            }

                            SnapshotSpan tokenSpan;
                            try {
                                tokenSpan = new SnapshotSpan(line.Snapshot, new Span(curLoc, len));
                            }
                            catch (Exception ex) {
                                /* Highly unlikely we ended up here, but just in case: */
                                tokenSpan = new SnapshotSpan();
                                Console.WriteLine($"Error in VerilogTokenTagger.GetTags: {ex.Message}");
                                curLoc += len;
                                continue;
                            }
                            if (tokenSpan.IntersectsWith(curSpan) &&
                                !IntersectsLineSpan(tokenSpan, line, attributeLineSpans)) {
                                foreach (ITagSpan<VerilogTokenTag> tag in ProcessTokenSpan(
                                    curSpan,
                                    line,
                                    verilogToken,
                                    tokenSpan,
                                    item,
                                    curLoc,
                                    haveParseData ? parseData : null,
                                    activeLocalScope,
                                    staticStringLineSpans)) {

                                    yield return tag;
                                }
                            }
                            // note that no chars are lost when splitting string with VerilogKeywordSplit, so no adjustment needed in location
                            curLoc += len;
                        }
                    }

                    if (haveParseData && parseData != null) {
                        ClearActiveLocalScopeForLineEnd(line, ref activeLocalScope);
                    }

                    if (line.LineBreakLength == 0) {
                        break;
                    }

                    int nextLineStart = line.EndIncludingLineBreak.Position;
                    if (nextLineStart >= snapshot.Length) {
                        break;
                    }

                    line = snapshot.GetLineFromPosition(nextLineStart);
                }
            } /* foreach (SnapshotSpan curSpan in spans) */

            yield break;
        } /* IEnumerable<ITagSpan<VerilogTokenTag>> GetTags */

        private static void CreateCommentHelper(
            string tokenText,
            bool isContinuedLineComment,
            bool isContinuedBlockComment,
            out CommentHelper commentHelper,
            out bool newIsContinuedLineComment,
            out bool newIsContinuedBlockComment) {
            commentHelper = new CommentHelper(tokenText, isContinuedLineComment, isContinuedBlockComment);

            newIsContinuedBlockComment = commentHelper.HasBlockStartComment;
            newIsContinuedLineComment = commentHelper.HasOpenLineComment; // we'll use this when processing the VerilogToken item in the commentHelper, above
        }

        private IEnumerable<ITagSpan<VerilogTokenTag>> ProcessTokenSpan(
            SnapshotSpan curSpan,
            ITextSnapshotLine containingLine,
            VerilogGlobals.VerilogToken verilogToken,
            SnapshotSpan tokenSpan,
            CommentHelper.CommentItem item,
            int curLoc,
            VerilogGlobals.ParseDataSnapshot parseData,
            string activeLocalScope,
            List<Span> staticStringLineSpans) {
            // is this item a comment? If so, color as appropriate. comments take highest priority: no other condition will change color of a comment
            if (item.IsComment) {
#if TAG_DEBUG
                System.Diagnostics.Debug.WriteLine("IEnumerable VerilogTokenTag yield comment for item " + Item.ItemText??"");
#endif
                yield return new TagSpan<VerilogTokenTag>(
                    tokenSpan,
                    new VerilogTokenTag(VerilogTokenTypes.Verilog_Comment));

                yield break;
            }

            SnapshotSpan staticStringSpan;
            bool staticStringStartsInToken;
            if (TryGetStaticStringSpanForToken(containingLine, tokenSpan, staticStringLineSpans, out staticStringSpan, out staticStringStartsInToken)) {
                if (staticStringStartsInToken) {
                    yield return new TagSpan<VerilogTokenTag>(
                        staticStringSpan,
                        new VerilogTokenTag(VerilogTokenTypes.Verilog_StaticString));
                }

                yield break;
            }

            string lookupText = item.ItemText;
            int leadingTrim = 0;
            int trailingTrim = 0;

            if (!TryGetClassifiableLookupText(item.ItemText, out lookupText, out leadingTrim, out trailingTrim)) {
                yield break; // no highlighting
            }

            SnapshotSpan lookupSpan = tokenSpan;
            if (leadingTrim != 0 || trailingTrim != 0) {
                int adjustedLoc = curLoc + leadingTrim;
                int adjustedLen = lookupText.Length;

                if (adjustedLen > 0) {
                    lookupSpan = new SnapshotSpan(curSpan.Snapshot, new Span(adjustedLoc, adjustedLen));
                }
            }

            foreach (ITagSpan<VerilogTokenTag> tag in ProcessLookupText(containingLine, verilogToken, tokenSpan, lookupSpan, lookupText, curLoc, leadingTrim, parseData, activeLocalScope)) {
                yield return tag;
            }

            /* There's an implicit yield break here; do not return Null! */
        }

        private static List<Span> GetStaticStringLineSpans(string lineText) {
            if (string.IsNullOrEmpty(lineText) || lineText.IndexOf('"') < 0) {
                return null;
            }

            List<Span> spans = new List<Span>();
            bool inString = false;
            bool escaped = false;
            int stringStart = -1;

            for (int i = 0; i < lineText.Length; i++) {
                char c = lineText[i];

                if (!inString) {
                    if (c == '"') {
                        inString = true;
                        escaped = false;
                        stringStart = i;
                    }
                    continue;
                }

                if (c == '"' && !escaped) {
                    spans.Add(new Span(stringStart, i + 1 - stringStart));
                    inString = false;
                    escaped = false;
                    stringStart = -1;
                    continue;
                }

                if (c == '\\' && !escaped) {
                    escaped = true;
                }
                else {
                    escaped = false;
                }
            }

            return spans;
        }

        private static bool TryGetStaticStringSpanForToken(
            ITextSnapshotLine containingLine,
            SnapshotSpan tokenSpan,
            List<Span> staticStringLineSpans,
            out SnapshotSpan staticStringSpan,
            out bool staticStringStartsInToken) {
            staticStringSpan = new SnapshotSpan();
            staticStringStartsInToken = false;

            if (containingLine == null || tokenSpan.Length <= 0 || staticStringLineSpans == null || staticStringLineSpans.Count == 0) {
                return false;
            }

            int tokenStart = tokenSpan.Start.Position - containingLine.Start.Position;
            int tokenEnd = tokenStart + tokenSpan.Length;
            if (tokenStart < 0 || tokenEnd <= tokenStart) {
                return false;
            }

            foreach (Span lineSpan in staticStringLineSpans) {
                int stringStart = lineSpan.Start;
                int stringEnd = lineSpan.End;
                if (stringStart < tokenEnd && stringEnd > tokenStart) {
                    staticStringStartsInToken = stringStart >= tokenStart && stringStart < tokenEnd;
                    staticStringSpan = new SnapshotSpan(
                        containingLine.Snapshot,
                        new Span(containingLine.Start.Position + stringStart, lineSpan.Length));
                    return true;
                }
            }

            return false;
        }

        private static bool IsStaticStringText(string text) {
            if (string.IsNullOrEmpty(text)) {
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.Length < 2 || trimmed[0] != '"') {
                return false;
            }

            bool escaped = false;
            for (int i = 1; i < trimmed.Length; i++) {
                char c = trimmed[i];
                if (c == '"' && !escaped) {
                    return i == trimmed.Length - 1;
                }

                if (c == '\\' && !escaped) {
                    escaped = true;
                }
                else {
                    escaped = false;
                }
            }

            return false;
        }

        private static bool IsFunctionReturnTypeText(string text) {
            switch (text) {
                case "automatic":
                case "signed":
                case "unsigned":
                case "reg":
                case "logic":
                case "bit":
                case "integer":
                case "time":
                case "real":
                case "realtime":
                    return true;

                default:
                    return false;
            }
        }

        private static List<string> GetSimpleCodeItems(string text) {
            List<string> items = new List<string>();
            if (string.IsNullOrEmpty(text)) {
                return items;
            }

            string current = string.Empty;
            for (int i = 0; i < text.Length; i++) {
                char c = text[i];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '$' || c == '\\') {
                    current += c;
                    continue;
                }

                if (!string.IsNullOrEmpty(current)) {
                    items.Add(current);
                    current = string.Empty;
                }

                if (!char.IsWhiteSpace(c)) {
                    items.Add(c.ToString());
                }
            }

            if (!string.IsNullOrEmpty(current)) {
                items.Add(current);
            }

            return items;
        }

        private static bool LineTextMayContainItem(string lineText, string itemText) {
            return !string.IsNullOrEmpty(lineText) && lineText.IndexOf(itemText, StringComparison.Ordinal) >= 0;
        }

        private static int FindStandaloneCodeItem(string lineText, string itemText) {
            if (string.IsNullOrEmpty(lineText) || string.IsNullOrEmpty(itemText)) {
                return -1;
            }

            int searchStart = 0;
            while (searchStart < lineText.Length) {
                int index = lineText.IndexOf(itemText, searchStart, StringComparison.Ordinal);
                if (index < 0) {
                    return -1;
                }

                bool validPrefix = index == 0 || IsVerilogIdentifierBoundary(lineText[index - 1]);
                int afterIndex = index + itemText.Length;
                bool validSuffix = afterIndex >= lineText.Length || IsVerilogIdentifierBoundary(lineText[afterIndex]);
                if (validPrefix && validSuffix) {
                    return index;
                }

                searchStart = index + itemText.Length;
            }

            return -1;
        }

        private static bool IsRoutineDeclarationNameContext(
            ITextSnapshotLine containingLine,
            int lookupColumn,
            string lookupText,
            string routineKeyword) {
            if (containingLine == null || !IsVerilogIdentifierText(lookupText)) {
                return false;
            }

            string lineText = containingLine.GetText();
            if (lookupColumn < 0 || lookupColumn + lookupText.Length > lineText.Length) {
                return false;
            }

            string routineName;
            bool foundRoutine = routineKeyword == "function"
                ? VerilogGlobals.TryGetFunctionNameFromLineText(lineText, out routineName)
                : VerilogGlobals.TryGetTaskNameFromLineText(lineText, out routineName);
            if (!foundRoutine || !string.Equals(routineName, lookupText, StringComparison.Ordinal)) {
                return false;
            }

            int keywordIndex = FindStandaloneCodeItem(lineText, routineKeyword);
            if (keywordIndex < 0 || lookupColumn <= keywordIndex) {
                return false;
            }

            int declarationEnd = lineText.IndexOf('(', keywordIndex + routineKeyword.Length);
            if (declarationEnd < 0) {
                declarationEnd = lineText.IndexOf(';', keywordIndex + routineKeyword.Length);
            }
            if (declarationEnd < 0) {
                declarationEnd = lineText.Length;
            }

            if (lookupColumn >= declarationEnd) {
                return false;
            }

            int lastNameIndex = lineText.LastIndexOf(
                routineName,
                declarationEnd - 1,
                declarationEnd - keywordIndex,
                StringComparison.Ordinal);
            return lastNameIndex == lookupColumn;
        }

        private static bool IsFunctionDeclarationNameContext(ITextSnapshotLine containingLine, int lookupColumn, string lookupText) {
            return IsRoutineDeclarationNameContext(containingLine, lookupColumn, lookupText, "function");
        }

        private static bool IsTaskDeclarationNameContext(ITextSnapshotLine containingLine, int lookupColumn, string lookupText) {
            return IsRoutineDeclarationNameContext(containingLine, lookupColumn, lookupText, "task");
        }

        private static string ResolveVariableScope(
            ITextSnapshotLine containingLine,
            int lookupColumn,
            VerilogGlobals.ParseDataSnapshot parseData) {
            if (parseData == null || containingLine == null) {
                return string.Empty;
            }

            string thisScope = parseData.TextModuleName(containingLine.LineNumber, lookupColumn);

            if (!parseData.VerilogVariables.ContainsKey(thisScope)) {
                // Fallback: some scope resolvers are position-sensitive; column 0 tends to be stable per line.
                thisScope = parseData.TextModuleName(containingLine.LineNumber, 0);
            }

            if (!parseData.VerilogVariables.ContainsKey(thisScope) && thisScope == "global" && parseData.VerilogVariables.ContainsKey(string.Empty)) {
                // Older parse data used an empty string for file-scope declarations.
                thisScope = string.Empty;
            }

            return thisScope;
        }

        private static bool TryFindActiveLocalScope(
            ITextSnapshot snapshot,
            int lineNumber,
            VerilogGlobals.ParseDataSnapshot parseData,
            out string activeLocalScope) {
            activeLocalScope = string.Empty;

            if (snapshot == null || parseData == null || lineNumber < 0) {
                return false;
            }

            int lastLine = Math.Min(lineNumber, snapshot.LineCount - 1);
            for (int i = lastLine; i >= 0; i--) {
                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(i);
                string lineText = line.GetText();

                bool mayContainEndFunction = LineTextMayContainItem(lineText, "endfunction");
                bool mayContainEndTask = LineTextMayContainItem(lineText, "endtask");
                if ((mayContainEndFunction && VerilogGlobals.IsEndFunctionLineText(lineText)) ||
                    (mayContainEndTask && VerilogGlobals.IsEndTaskLineText(lineText))) {
                    return false;
                }

                bool mayContainFunction = LineTextMayContainItem(lineText, "function");
                bool mayContainTask = LineTextMayContainItem(lineText, "task");
                if (!mayContainFunction && !mayContainTask) {
                    continue;
                }

                string functionName;
                if (mayContainFunction && VerilogGlobals.TryGetFunctionNameFromLineText(lineText, out functionName)) {
                    string moduleScope = ResolveVariableScope(line, 0, parseData);
                    activeLocalScope = VerilogGlobals.FunctionLocalScopeName(moduleScope, functionName);
                    return true;
                }

                string taskName;
                if (mayContainTask && VerilogGlobals.TryGetTaskNameFromLineText(lineText, out taskName)) {
                    string moduleScope = ResolveVariableScope(line, 0, parseData);
                    activeLocalScope = VerilogGlobals.TaskLocalScopeName(moduleScope, taskName);
                    return true;
                }
            }

            return false;
        }

        private static void UpdateActiveLocalScopeForLineStart(
            ITextSnapshotLine line,
            VerilogGlobals.ParseDataSnapshot parseData,
            ref string activeLocalScope) {
            if (line == null || parseData == null) {
                return;
            }

            string lineText = line.GetText();
            bool mayContainFunction = LineTextMayContainItem(lineText, "function");
            bool mayContainTask = LineTextMayContainItem(lineText, "task");
            if (!mayContainFunction && !mayContainTask) {
                return;
            }

            string functionName;
            if (mayContainFunction && VerilogGlobals.TryGetFunctionNameFromLineText(lineText, out functionName)) {
                string moduleScope = ResolveVariableScope(line, 0, parseData);
                activeLocalScope = VerilogGlobals.FunctionLocalScopeName(moduleScope, functionName);
                return;
            }

            string taskName;
            if (mayContainTask && VerilogGlobals.TryGetTaskNameFromLineText(lineText, out taskName)) {
                string moduleScope = ResolveVariableScope(line, 0, parseData);
                activeLocalScope = VerilogGlobals.TaskLocalScopeName(moduleScope, taskName);
            }
        }

        private static void ClearActiveLocalScopeForLineEnd(ITextSnapshotLine line, ref string activeLocalScope) {
            if (line == null || string.IsNullOrEmpty(activeLocalScope)) {
                return;
            }

            string lineText = line.GetText();
            if ((LineTextMayContainItem(lineText, "endfunction") && VerilogGlobals.IsEndFunctionLineText(lineText)) ||
                (LineTextMayContainItem(lineText, "endtask") && VerilogGlobals.IsEndTaskLineText(lineText))) {
                activeLocalScope = string.Empty;
            }
        }

        private static bool IsVerilogValueText(string text) {
            if (string.IsNullOrWhiteSpace(text)) {
                return false;
            }

            string compact = text.Trim().Replace(" ", string.Empty).Replace("\t", string.Empty);
            if (compact.Length == 0) {
                return false;
            }

            // Sized or unsized based literal: 8'hff, 3'b010, 'hdead, 8'sd-1.
            int quoteIndex = compact.IndexOf(VerilogGlobals.RADIX_CHAR);
            if (quoteIndex >= 0) {
                int radixIndex = quoteIndex + 1;
                if (radixIndex < compact.Length && (compact[radixIndex] == 's' || compact[radixIndex] == 'S')) {
                    radixIndex++;
                }

                if (radixIndex < compact.Length) {
                    char radix = compact[radixIndex];
                    if (VerilogGlobals.VerilogRadixChars.Contains(radix)) {
                        return compact.Length > radixIndex + 1;
                    }
                }
            }

            double numericValue;
            return double.TryParse(compact, out numericValue);
        }

        private static bool IsVerilogIdentifierText(string text) {
            if (string.IsNullOrEmpty(text)) {
                return false;
            }

            if (text[0] == '\\') {
                return text.Length > 1;
            }

            if (!(char.IsLetter(text[0]) || text[0] == '_')) {
                return false;
            }

            for (int i = 1; i < text.Length; i++) {
                char c = text[i];
                if (!IsVerilogIdentifierContinuation(c)) {
                    return false;
                }
            }

            return true;
        }

        private static bool IsKnownModuleName(string lookupText, VerilogGlobals.ParseDataSnapshot parseData) {
            if (string.IsNullOrEmpty(lookupText)) {
                return false;
            }

            if (parseData != null && parseData.VerilogVariables.ContainsKey(lookupText)) {
                return true;
            }

            VerilogGlobals.VerilogDefinitionLocation definition;
            return VerilogGlobals.TryGetModuleDefinitionFromParsedFiles(lookupText, out definition);
        }

        private static bool TryGetMacroNameFromLookupText(string lookupText, out string macroName) {
            macroName = string.Empty;
            if (string.IsNullOrEmpty(lookupText) || lookupText[0] != '`') {
                return false;
            }

            string candidate = lookupText.Substring(1);
            if (!IsVerilogIdentifierText(candidate)) {
                return false;
            }

            switch (lookupText) {
                case "`celldefine":
                case "`endcelldefine":
                case "`default_nettype":
                case "`define":
                case "`undef":
                case "`ifdef":
                case "`ifndef":
                case "`elsif":
                case "`else":
                case "`endif":
                case "`include":
                case "`resetall":
                case "`line":
                case "`timescale":
                case "`unconnected_drive":
                case "`nounconnected_driv":
                    return false;

                default:
                    macroName = candidate;
                    return true;
            }
        }

        private static bool IsPreprocessorMacroContext(ITextSnapshotLine containingLine, int lookupColumn, string lookupText) {
            if (containingLine == null || string.IsNullOrEmpty(lookupText)) {
                return false;
            }

            string lineText = containingLine.GetText();
            if (lookupColumn < 0 || lookupColumn > lineText.Length) {
                return false;
            }

            string prefixText = lineText.Substring(0, lookupColumn);
            string trimmedPrefix = prefixText.Trim();

            if (trimmedPrefix.EndsWith("`define", StringComparison.Ordinal) ||
                trimmedPrefix.EndsWith("`ifdef", StringComparison.Ordinal) ||
                trimmedPrefix.EndsWith("`ifndef", StringComparison.Ordinal) ||
                trimmedPrefix.EndsWith("`elsif", StringComparison.Ordinal) ||
                trimmedPrefix.EndsWith("`undef", StringComparison.Ordinal)) {
                return IsVerilogIdentifierText(lookupText);
            }

            return false;
        }

        private static bool IsVerilogIdentifierContinuation(char c) {
            return char.IsLetterOrDigit(c) || c == '_' || c == '$';
        }

        private static bool IsVerilogIdentifierBoundary(char c) {
            return !IsVerilogIdentifierContinuation(c);
        }

        private static bool IsClassifiableTrailingDelimiter(char c) {
            return c == ',' || c == ';';
        }

        private static bool TryGetClassifiableLookupText(
            string sourceText,
            out string lookupText,
            out int leadingTrim,
            out int trailingTrim) {
            lookupText = string.Empty;
            leadingTrim = 0;
            trailingTrim = 0;

            if (string.IsNullOrEmpty(sourceText)) {
                return false;
            }

            int start = 0;
            int end = sourceText.Length;

            while (start < end && char.IsWhiteSpace(sourceText[start])) {
                start++;
            }

            while (end > start && char.IsWhiteSpace(sourceText[end - 1])) {
                end--;
            }

            // ANSI port and declaration lists can arrive here as a single token like "reg_cond0,".
            // Classify only the identifier/value part and leave the delimiter untagged.
            if (start < end && sourceText[start] != '\\') {
                while (end > start && IsClassifiableTrailingDelimiter(sourceText[end - 1])) {
                    end--;
                }
            }

            if (end <= start) {
                return false;
            }

            leadingTrim = start;
            trailingTrim = sourceText.Length - end;
            lookupText = sourceText.Substring(start, end - start);

            return true;
        }

        private static bool HasAssignmentInCurrentDeclarationItem(string prefixText) {
            if (string.IsNullOrEmpty(prefixText)) {
                return false;
            }

            bool hasAssignment = false;
            int squareDepth = 0;
            int roundDepth = 0;
            int squigglyDepth = 0;

            for (int i = 0; i < prefixText.Length; i++) {
                char c = prefixText[i];

                if (c == '[') {
                    squareDepth++;
                    continue;
                }

                if (c == ']') {
                    if (squareDepth > 0) {
                        squareDepth--;
                    }
                    continue;
                }

                if (c == '(') {
                    roundDepth++;
                    continue;
                }

                if (c == ')') {
                    if (roundDepth > 0) {
                        roundDepth--;
                    }
                    continue;
                }

                if (c == '{') {
                    squigglyDepth++;
                    continue;
                }

                if (c == '}') {
                    if (squigglyDepth > 0) {
                        squigglyDepth--;
                    }
                    continue;
                }

                if (squareDepth != 0 || roundDepth != 0 || squigglyDepth != 0) {
                    continue;
                }

                if (c == ',' || c == ';') {
                    hasAssignment = false;
                    continue;
                }

                if (c == '=') {
                    hasAssignment = true;
                }
            }

            return hasAssignment;
        }

        private static string CodeBeforeLineComment(string lineText) {
            if (string.IsNullOrEmpty(lineText)) {
                return string.Empty;
            }

            int commentIndex = lineText.IndexOf("//", StringComparison.Ordinal);
            return commentIndex >= 0 ? lineText.Substring(0, commentIndex) : lineText;
        }

        private static bool IsTypedefAliasIdentifierContext(
            ITextSnapshotLine containingLine,
            int lookupColumn,
            string lookupText) {
            if (containingLine == null || !IsVerilogIdentifierText(lookupText)) {
                return false;
            }

            string lineText = CodeBeforeLineComment(containingLine.GetText());
            int typedefIndex = FindStandaloneCodeItem(lineText, "typedef");
            int semicolonIndex = lineText.LastIndexOf(';');
            if (typedefIndex < 0 || semicolonIndex <= typedefIndex || lookupColumn >= semicolonIndex) {
                return false;
            }

            int aliasEnd = semicolonIndex;
            while (aliasEnd > typedefIndex && char.IsWhiteSpace(lineText[aliasEnd - 1])) {
                aliasEnd--;
            }

            int aliasStart = aliasEnd;
            while (aliasStart > typedefIndex && IsVerilogIdentifierContinuation(lineText[aliasStart - 1])) {
                aliasStart--;
            }

            return aliasStart == lookupColumn &&
                   aliasEnd - aliasStart == lookupText.Length &&
                   string.CompareOrdinal(lineText, aliasStart, lookupText, 0, lookupText.Length) == 0;
        }

        private static bool HasLaterDeclaratorIdentifier(
            ITextSnapshotLine containingLine,
            int lookupColumn,
            string lookupText) {
            if (containingLine == null || !IsVerilogIdentifierText(lookupText)) {
                return false;
            }

            string lineText = CodeBeforeLineComment(containingLine.GetText());
            int tokenEnd = lookupColumn + lookupText.Length;
            if (lookupColumn < 0 || tokenEnd > lineText.Length) {
                return false;
            }

            int squareDepth = 0;
            foreach (string item in GetSimpleCodeItems(lineText.Substring(0, lookupColumn))) {
                if (item == "[") {
                    squareDepth++;
                }
                else if (item == "]" && squareDepth > 0) {
                    squareDepth--;
                }
            }

            if (squareDepth > 0) {
                return true;
            }

            squareDepth = 0;
            int roundDepth = 0;
            int squigglyDepth = 0;
            foreach (string item in GetSimpleCodeItems(lineText.Substring(tokenEnd))) {
                bool atTopLevel = squareDepth == 0 && roundDepth == 0 && squigglyDepth == 0;
                if (atTopLevel && (item == "," || item == ";" || item == "=" || item == ")")) {
                    break;
                }

                switch (item) {
                    case "[":
                        squareDepth++;
                        continue;
                    case "]":
                        if (squareDepth > 0) {
                            squareDepth--;
                        }
                        continue;
                    case "(":
                        roundDepth++;
                        continue;
                    case ")":
                        if (roundDepth > 0) {
                            roundDepth--;
                        }
                        continue;
                    case "{":
                        squigglyDepth++;
                        continue;
                    case "}":
                        if (squigglyDepth > 0) {
                            squigglyDepth--;
                        }
                        continue;
                }

                if (squareDepth == 0 && roundDepth == 0 && squigglyDepth == 0 &&
                    IsVerilogIdentifierText(item)) {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetDeclarationVariableTypeForLookup(
            ITextSnapshotLine containingLine,
            int column,
            string lookupText,
            out VerilogTokenTypes variableType) {
            variableType = VerilogTokenTypes.Verilog_Variable;

            if (IsTypedefAliasIdentifierContext(containingLine, column, lookupText) ||
                HasLaterDeclaratorIdentifier(containingLine, column, lookupText)) {
                return false;
            }

            return TryGetDeclarationVariableType(containingLine, column, out variableType);
        }

        private bool TryGetDeclarationVariableType(
            ITextSnapshotLine containingLine,
            int column,
            out VerilogTokenTypes variableType) {
            variableType = VerilogTokenTypes.Verilog_Variable;

            if (containingLine == null || column <= 0) {
                return false;
            }

            string lineText = containingLine.GetText();
            if (string.IsNullOrEmpty(lineText)) {
                return false;
            }

            if (column > lineText.Length) {
                column = lineText.Length;
            }

            string prefixText = lineText.Substring(0, column);

            if (HasAssignmentInCurrentDeclarationItem(prefixText)) {
                return false;
            }

            return VerilogGlobals.TryGetDeclarationVariableTypeFromText(
                prefixText,
                IsSystemVerilogDocument(),
                out variableType);
        }

        private bool IsSystemVerilogDocument() {
            if (_systemVerilogDocumentKnown) {
                return _isSystemVerilogDocument;
            }

            string documentPath = VerilogGlobals.GetDocumentPath(_buffer.CurrentSnapshot);
            if (string.IsNullOrEmpty(documentPath)) {
                return false;
            }

            string extension = System.IO.Path.GetExtension(documentPath);
            _isSystemVerilogDocument =
                string.Equals(extension, ".sv", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".svh", StringComparison.OrdinalIgnoreCase);
            _systemVerilogDocumentKnown = true;
            return _isSystemVerilogDocument;
        }

        private static bool IsSystemVerilogOnlyKeywordType(VerilogTokenTypes tokenType) {
            return tokenType == VerilogTokenTypes.Verilog_SystemVerilogYosysSupported ||
                   tokenType == VerilogTokenTypes.Verilog_SystemVerilogYosysUnsupported;
        }

        private IEnumerable<ITagSpan<VerilogTokenTag>> ProcessLookupText(
            ITextSnapshotLine containingLine,
            VerilogGlobals.VerilogToken verilogToken,
            SnapshotSpan tokenSpan,
            SnapshotSpan lookupSpan,
            string lookupText,
            int curLoc,
            int leadingWhitespace,
            VerilogGlobals.ParseDataSnapshot parseData,
            string activeLocalScope) {
            if (IsStaticStringText(lookupText)) {
                yield return new TagSpan<VerilogTokenTag>(
                    lookupSpan,
                    new VerilogTokenTag(VerilogTokenTypes.Verilog_StaticString));
                yield break;
            }

            // Check for standard keyword syntax highlighting. The two shared
            // SystemVerilog-only classifications apply only to .sv and .svh files;
            // legacy Verilog files may legally use those words as identifiers.
            VerilogTokenTypes keywordType;
            if (VerilogGlobals.VerilogTypes.TryGetValue(lookupText, out keywordType) &&
                (!IsSystemVerilogOnlyKeywordType(keywordType) || IsSystemVerilogDocument())) {
#if TAG_DEBUG
                System.Diagnostics.Debug.WriteLine("IEnumerable VerilogTokenTag yield " + lookupText);
#endif
                yield return new TagSpan<VerilogTokenTag>(
                    lookupSpan,
                    new VerilogTokenTag(keywordType));
                yield break;
            }

            if (IsVerilogValueText(lookupText)) {
                yield return new TagSpan<VerilogTokenTag>(
                    lookupSpan,
                    new VerilogTokenTag(VerilogTokenTypes.Verilog_Value));
                yield break;
            }

            string macroName;
            if (TryGetMacroNameFromLookupText(lookupText, out macroName)) {
                yield return new TagSpan<VerilogTokenTag>(
                    lookupSpan,
                    new VerilogTokenTag(VerilogTokenTypes.Verilog_Macro));
                yield break;
            }

            bool lookupTextIsIdentifier = IsVerilogIdentifierText(lookupText);

            if (lookupTextIsIdentifier &&
                IsPreprocessorMacroContext(
                    containingLine,
                    (curLoc + leadingWhitespace) - containingLine.Start.Position,
                    lookupText)) {

                yield return new TagSpan<VerilogTokenTag>(
                    lookupSpan,
                    new VerilogTokenTag(VerilogTokenTypes.Verilog_Macro));
                yield break;
            }

            if (lookupTextIsIdentifier &&
                (IsFunctionDeclarationNameContext(
                    containingLine,
                    (curLoc + leadingWhitespace) - containingLine.Start.Position,
                    lookupText) ||
                 IsTaskDeclarationNameContext(
                    containingLine,
                    (curLoc + leadingWhitespace) - containingLine.Start.Position,
                    lookupText))) {

                yield return new TagSpan<VerilogTokenTag>(
                    lookupSpan,
                    new VerilogTokenTag(VerilogTokenTypes.Verilog_FunctionName));
                yield break;
            }

            if (lookupTextIsIdentifier && IsKnownModuleName(lookupText, parseData)) {
                // we are instantiation a module; recall VerilogVariables is first a dictionary of scope (aka module), then a dictionary of variables in each module scope
                // TODO do we need: if (tokenSpan.IntersectsWith(curSpan))
#if TAG_DEBUG
                System.Diagnostics.Debug.WriteLine("IEnumerable VerilogTokenTag yield variable module " + lookupText);
#endif
                yield return new TagSpan<VerilogTokenTag>(
                    lookupSpan,
                    new VerilogTokenTag(VerilogGlobals.VerilogTypes["variable_module"]));
                yield break;
            }

            foreach (ITagSpan<VerilogTokenTag> tag in ProcessScopeLookup(containingLine, verilogToken, tokenSpan, lookupSpan, lookupText, curLoc, leadingWhitespace, parseData, activeLocalScope)) {
                yield return tag;
            }

            /* There's an implicit yield break here; do not return Null! */
        }

        private IEnumerable<ITagSpan<VerilogTokenTag>> ProcessScopeLookup(
            ITextSnapshotLine containingLine,
            VerilogGlobals.VerilogToken verilogToken,
            SnapshotSpan tokenSpan,
            SnapshotSpan lookupSpan,
            string lookupText,
            int curLoc,
            int leadingWhitespace,
            VerilogGlobals.ParseDataSnapshot parseData,
            string activeLocalScope) {
            bool lookupTextIsIdentifier = IsVerilogIdentifierText(lookupText);

            if (parseData == null) {
                VerilogTokenTypes declarationVariableType;
                if (lookupTextIsIdentifier &&
                    TryGetDeclarationVariableTypeForLookup(
                        containingLine,
                        (curLoc + leadingWhitespace) - containingLine.Start.Position,
                        lookupText,
                        out declarationVariableType)) {

                    yield return new TagSpan<VerilogTokenTag>(
                        lookupSpan,
                        new VerilogTokenTag(declarationVariableType));
                    yield break;
                }

                foreach (ITagSpan<VerilogTokenTag> tag in ProcessContextColorization(containingLine, verilogToken, tokenSpan, lookupText, curLoc, parseData)) {
                    yield return tag;
                }

                yield break;
            }

            // check to see if this is a variable
            string thisScope = ResolveVariableScope(
                containingLine,
                (curLoc + leadingWhitespace) - containingLine.Start.Position,
                parseData);

            if (lookupTextIsIdentifier &&
                !string.IsNullOrEmpty(activeLocalScope) &&
                parseData.VerilogVariables.ContainsKey(activeLocalScope) &&
                parseData.VerilogVariables[activeLocalScope].ContainsKey(lookupText)) {
                yield return new TagSpan<VerilogTokenTag>(
                    lookupSpan,
                    new VerilogTokenTag(parseData.VerilogVariables[activeLocalScope][lookupText]));
                yield break;
            }

            if (parseData.VerilogVariables.ContainsKey(thisScope)) {
                // the current scope (typically a module name) is defined. So do we have a known variable?
                if (lookupTextIsIdentifier && parseData.VerilogVariables[thisScope].ContainsKey(lookupText)) {
                    // TODO do we need: if (tokenSpan.IntersectsWith(curSpan))
#if TAG_DEBUG
                    System.Diagnostics.Debug.WriteLine("IEnumerable VerilogTokenTag yield variable " + lookupText);
#endif
                    yield return new TagSpan<VerilogTokenTag>(
                        lookupSpan,
                        new VerilogTokenTag(parseData.VerilogVariables[thisScope][lookupText]));
                    yield break;
                }

                if (lookupTextIsIdentifier &&
                    parseData.VerilogVariables.ContainsKey(VerilogGlobals.SCOPE_CONST) &&
                    parseData.VerilogVariables[VerilogGlobals.SCOPE_CONST].ContainsKey(lookupText)) {
                    yield return new TagSpan<VerilogTokenTag>(
                        lookupSpan,
                        new VerilogTokenTag(parseData.VerilogVariables[VerilogGlobals.SCOPE_CONST][lookupText]));
                    yield break;
                }

                VerilogTokenTypes declarationVariableType;
                if (lookupTextIsIdentifier &&
                    TryGetDeclarationVariableTypeForLookup(
                        containingLine,
                        (curLoc + leadingWhitespace) - containingLine.Start.Position,
                        lookupText,
                        out declarationVariableType)) {

                    yield return new TagSpan<VerilogTokenTag>(
                        lookupSpan,
                        new VerilogTokenTag(declarationVariableType));
                    yield break;
                }

                foreach (ITagSpan<VerilogTokenTag> tag in ProcessContextColorization(containingLine, verilogToken, tokenSpan, lookupText, curLoc, parseData)) {
                    yield return tag;
                }

                yield break;
            }

            if (lookupTextIsIdentifier &&
                parseData.VerilogVariables.ContainsKey(VerilogGlobals.SCOPE_CONST) &&
                parseData.VerilogVariables[VerilogGlobals.SCOPE_CONST].ContainsKey(lookupText)) {
                //yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                //      new VerilogTokenTag(VerilogGlobals.VerilogTypes["Verilog_Value"]));
                yield return new TagSpan<VerilogTokenTag>(
                    lookupSpan,
                    new VerilogTokenTag(parseData.VerilogVariables[VerilogGlobals.SCOPE_CONST][lookupText]));
                yield break;
            }

            VerilogTokenTypes fallbackDeclarationVariableType;
            if (lookupTextIsIdentifier &&
                TryGetDeclarationVariableTypeForLookup(
                    containingLine,
                    (curLoc + leadingWhitespace) - containingLine.Start.Position,
                    lookupText,
                    out fallbackDeclarationVariableType)) {

                yield return new TagSpan<VerilogTokenTag>(
                    lookupSpan,
                    new VerilogTokenTag(fallbackDeclarationVariableType));
                yield break;
            }

            // TODO - how do we get here when thisScope *is* defined? timing?
            // A: we destroy the VerilogVariables when rescanning (otherwise everyuthing is a duplicate) TODO: keep track of where variables are defined. don't rebui;d
            System.Diagnostics.Debug.WriteLine("Warning! VerilogGlobals.VerilogVariables.ContainsKey({0}) not defined!", thisScope);
        }

        private IEnumerable<ITagSpan<VerilogTokenTag>> ProcessContextColorization(
            ITextSnapshotLine containingLine,
            VerilogGlobals.VerilogToken verilogToken,
            SnapshotSpan tokenSpan,
            string lookupText,
            int curLoc,
            VerilogGlobals.ParseDataSnapshot parseData) {
            // no tag colorization for the explicit token, but perhaps based on context:
            int thisDelimiterIndex = 0;
            int thisDelimiterTotalDepth;

            //int thisDelimiterTotalDepth = VerilogToken.SquareBracketDepth +
            //                              VerilogToken.RoundBracketDepth +
            //                              VerilogToken.SquigglyBracketDepth;
            // int testValue = VerilogGlobals.BracketDepth(containingLine.LineNumber, curLoc - containingLine.Start.Position);
            switch (verilogToken.Context) {
                case VerilogGlobals.VerilogTokenContextType.SquareBracketOpen:
                case VerilogGlobals.VerilogTokenContextType.SquareBracketClose:
                    thisDelimiterTotalDepth = parseData == null ? 0 : parseData.BracketDepth(containingLine.LineNumber, curLoc - containingLine.Start.Position);
                    thisDelimiterIndex = (thisDelimiterTotalDepth % 5);
                    yield return new TagSpan<VerilogTokenTag>(
                        tokenSpan,
                        // see _VerilogTypes["bracket_type1"] .. _VerilogTypes["bracket_type5"]
                        new VerilogTokenTag(VerilogGlobals.VerilogTypes["bracket_type" + (thisDelimiterIndex).ToString()]));
                    yield break;

                case VerilogGlobals.VerilogTokenContextType.RoundBracketClose:
                case VerilogGlobals.VerilogTokenContextType.RoundBracketOpen:
                    thisDelimiterTotalDepth = parseData == null ? 0 : parseData.BracketDepth(containingLine.LineNumber, curLoc - containingLine.Start.Position);
                    thisDelimiterIndex = (thisDelimiterTotalDepth % 5);
                    yield return new TagSpan<VerilogTokenTag>(
                        tokenSpan,
                        // see _VerilogTypes["bracket_type1"] .. _VerilogTypes["bracket_type5"]
                        new VerilogTokenTag(VerilogGlobals.VerilogTypes["bracket_type" + (thisDelimiterIndex).ToString()]));
                    yield break;

                case VerilogGlobals.VerilogTokenContextType.SquigglyBracketOpen:
                case VerilogGlobals.VerilogTokenContextType.SquigglyBracketClose:
                    thisDelimiterTotalDepth = parseData == null ? 0 : parseData.BracketDepth(containingLine.LineNumber, curLoc - containingLine.Start.Position);
                    thisDelimiterIndex = (thisDelimiterTotalDepth % 5);
                    // see _VerilogTypes["bracket_type1"] .. _VerilogTypes["bracket_type5"]
                    yield return new TagSpan<VerilogTokenTag>(
                        tokenSpan,
                        new VerilogTokenTag(VerilogGlobals.VerilogTypes["bracket_type" + (thisDelimiterIndex).ToString()]));
                    yield break;

                case VerilogGlobals.VerilogTokenContextType.SquareBracketContents:
                    yield return new TagSpan<VerilogTokenTag>(
                        tokenSpan,
                        new VerilogTokenTag(VerilogTokenTypes.Verilog_BracketContent));
                    yield break;

                case VerilogGlobals.VerilogTokenContextType.AlwaysAt:
                    // The '@' in an event control, such as "always @(posedge clk)",
                    // is a delimiter/operator. Do not tag it as the "always" keyword.
                    // The actual "always" text is highlighted by the keyword lookup.
                    yield break;

                default:
                    // no highlighting
                    yield break;
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        // TODO: confirm we really want to remove this ViewLayoutChanged
        //void ViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e) {
        //    if (e.NewSnapshot != e.OldSnapshot) //make sure that there has really been a change
        //    {
        //        TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(_buffer.CurrentSnapshot, 0,
        //                _buffer.CurrentSnapshot.Length)));
        //    }
        //}
    }

}
