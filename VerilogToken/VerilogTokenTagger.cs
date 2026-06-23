//***************************************************************************
//
//  MIT License
//
//  Copyright(c) 2019 gojimmypi
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

    internal sealed class VerilogTokenTagger : ITagger<VerilogTokenTag>
    {

        private Timer _reparseCompletionTimer;
        private readonly object _reparseCompletionTimerLock = new object();
        private string _lastReparseFile = string.Empty;

        private int _initialInvalidateAttempted;

        private const int ReparseCompletionTimerMaxTicks = 200;
        private int _reparseCompletionTimerTicks;

        private readonly object _blockCommentStateLock = new object();
        private ITextSnapshot _blockCommentStateSnapshot;
        private readonly List<bool> _blockCommentStateAtLineStart = new List<bool>();

        // ITextView View { get; set; }
        private readonly ITextBuffer _buffer;

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
            if (string.IsNullOrEmpty(forFile)) {
                return;
            }

            lock (_reparseCompletionTimerLock) {
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

        private void ReparseCompletionTimerCallback(object state) {
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
            InvalidateBlockCommentStateCache();

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

            bool forceReparse = false;

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

        private void StopReparseCompletionWatcher() {
            lock (_reparseCompletionTimerLock) {
                if (_reparseCompletionTimer == null) {
                    return;
                }

                try {
                    _reparseCompletionTimer.Change(Timeout.Infinite, Timeout.Infinite);
                }
                catch (ObjectDisposedException) {
                    _reparseCompletionTimer = null;
                }
            }
        }


        private void RaiseTagsChanged(SnapshotSpan span) {
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

        private bool EnsureParseDataForCurrentBuffer() {
            string thisFile = VerilogLanguage.VerilogGlobals.GetDocumentPath(_buffer.CurrentSnapshot);
            if (string.IsNullOrEmpty(thisFile)) {
                return true;
            }

            if (VerilogGlobals.IsActiveParseData(thisFile, _buffer)) {
                return true;
            }

            if (VerilogGlobals.ParseStatusController.IsReparsing(thisFile)) {
                StartOrResetReparseCompletionWatcher(thisFile);
                return false;
            }

            VerilogGlobals.ParseStatusController.NeedReparse_SetValue(thisFile, true);
            VerilogGlobals.Reparse(_buffer, thisFile);
            StartOrResetReparseCompletionWatcher(thisFile);

            return VerilogGlobals.IsActiveParseData(thisFile, _buffer);
        }

        /// <summary>
        ///   IEnumerable VerilogTokenTag GetTags
        /// </summary>
        /// <param name="spans"></param>
        /// <returns></returns>
        public IEnumerable<ITagSpan<VerilogTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
            //while (VerilogGlobals.IsReparsing)
            {
                // do we really want to do this? (probably not)
                // System.Threading.Thread.Sleep(10);
            }

            if (spans == null || spans.Count == 0) {
                yield break;
            }

            if (!EnsureParseDataForCurrentBuffer()) {
                yield break;
            }

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
                while (line != null && line.Start.Position < endPos) {
                    if (tokens != null && tokens.Length >= 1) {
                        priorToken = tokens[tokens.Length - 1];
                    }

                    string lineText = line.GetText();
                    tokens = VerilogGlobals.VerilogKeywordSplit(lineText, priorToken);

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

                            if (directTokenSpan.IntersectsWith(curSpan)) {
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
                            if (tokenSpan.IntersectsWith(curSpan)) {
                                foreach (ITagSpan<VerilogTokenTag> tag in ProcessTokenSpan(
                                    curSpan,
                                    line,
                                    verilogToken,
                                    tokenSpan,
                                    item,
                                    curLoc)) {

                                    yield return tag;
                                }
                            }
                            // note that no chars are lost when splitting string with VerilogKeywordSplit, so no adjustment needed in location
                            curLoc += len;
                        }
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
            int curLoc) {
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

            string lookupText = item.ItemText;
            int leadingWhitespace = 0;
            int trailingWhitespace = 0;

            if (!string.IsNullOrEmpty(lookupText)) {
                string trimmedStart = lookupText.TrimStart();
                string trimmedBoth = lookupText.Trim();

                leadingWhitespace = lookupText.Length - trimmedStart.Length;
                trailingWhitespace = lookupText.Length - lookupText.TrimEnd().Length;

                lookupText = trimmedBoth;
            }

            if (string.IsNullOrEmpty(lookupText)) {
                yield break; // no highlighting
            }

            SnapshotSpan lookupSpan = tokenSpan;
            if (leadingWhitespace != 0 || trailingWhitespace != 0) {
                int adjustedLoc = curLoc + leadingWhitespace;
                int adjustedLen = lookupText.Length;

                if (adjustedLen > 0) {
                    lookupSpan = new SnapshotSpan(curSpan.Snapshot, new Span(adjustedLoc, adjustedLen));
                }
            }

            foreach (ITagSpan<VerilogTokenTag> tag in ProcessLookupText(containingLine, verilogToken, tokenSpan, lookupSpan, lookupText, curLoc, leadingWhitespace)) {
                yield return tag;
            }

            /* There's an implicit yield break here; do not return Null! */
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
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '$')) {
                    return false;
                }
            }

            return true;
        }

        private IEnumerable<ITagSpan<VerilogTokenTag>> ProcessLookupText(
            ITextSnapshotLine containingLine,
            VerilogGlobals.VerilogToken verilogToken,
            SnapshotSpan tokenSpan,
            SnapshotSpan lookupSpan,
            string lookupText,
            int curLoc,
            int leadingWhitespace) {
            // check for standard keyword syntax higlighting
            if (VerilogGlobals.VerilogTypes.ContainsKey(lookupText)) {
#if TAG_DEBUG
                System.Diagnostics.Debug.WriteLine("IEnumerable VerilogTokenTag yield " + lookupText);
#endif
                yield return new TagSpan<VerilogTokenTag>(
                    lookupSpan,
                    new VerilogTokenTag(VerilogGlobals.VerilogTypes[lookupText]));
                yield break;
            }

            if (IsVerilogValueText(lookupText)) {
                yield return new TagSpan<VerilogTokenTag>(
                    lookupSpan,
                    new VerilogTokenTag(VerilogTokenTypes.Verilog_Value));
                yield break;
            }

            bool lookupTextIsIdentifier = IsVerilogIdentifierText(lookupText);

            if (lookupTextIsIdentifier && VerilogGlobals.VerilogVariables.ContainsKey(lookupText)) {
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

            foreach (ITagSpan<VerilogTokenTag> tag in ProcessScopeLookup(containingLine, verilogToken, tokenSpan, lookupSpan, lookupText, curLoc, leadingWhitespace)) {
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
            int leadingWhitespace) {
            bool lookupTextIsIdentifier = IsVerilogIdentifierText(lookupText);

            // check to see if this is a variable
            string thisScope = VerilogGlobals.TextModuleName(
                containingLine.LineNumber,
                (curLoc + leadingWhitespace) - containingLine.Start.Position); // TODO

            if (!VerilogGlobals.VerilogVariables.ContainsKey(thisScope)) {
                // fallback: some scope resolvers are position-sensitive; column 0 tends to be stable per line
                thisScope = VerilogGlobals.TextModuleName(containingLine.LineNumber, 0);
            }

            if (VerilogGlobals.VerilogVariables.ContainsKey(thisScope)) {
                // the current scope (typically a module name) is defined. So do we have a known variable?
                if (lookupTextIsIdentifier && VerilogGlobals.VerilogVariables[thisScope].ContainsKey(lookupText)) {
                    // TODO do we need: if (tokenSpan.IntersectsWith(curSpan))
#if TAG_DEBUG
                    System.Diagnostics.Debug.WriteLine("IEnumerable VerilogTokenTag yield variable " + lookupText);
#endif
                    yield return new TagSpan<VerilogTokenTag>(
                        lookupSpan,
                        new VerilogTokenTag(VerilogGlobals.VerilogVariables[thisScope][lookupText]));
                    yield break;
                }

                if (lookupTextIsIdentifier &&
                    VerilogGlobals.VerilogVariables.ContainsKey(VerilogGlobals.SCOPE_CONST) &&
                    VerilogGlobals.VerilogVariables[VerilogGlobals.SCOPE_CONST].ContainsKey(lookupText)) {
                    yield return new TagSpan<VerilogTokenTag>(
                        lookupSpan,
                        new VerilogTokenTag(VerilogGlobals.VerilogVariables[VerilogGlobals.SCOPE_CONST][lookupText]));
                    yield break;
                }

                foreach (ITagSpan<VerilogTokenTag> tag in ProcessContextColorization(containingLine, verilogToken, tokenSpan, lookupText, curLoc)) {
                    yield return tag;
                }

                yield break;
            }

            if (lookupTextIsIdentifier &&
                VerilogGlobals.VerilogVariables.ContainsKey(VerilogGlobals.SCOPE_CONST) &&
                VerilogGlobals.VerilogVariables[VerilogGlobals.SCOPE_CONST].ContainsKey(lookupText)) {
                //yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                //      new VerilogTokenTag(VerilogGlobals.VerilogTypes["Verilog_Value"]));
                yield return new TagSpan<VerilogTokenTag>(
                    lookupSpan,
                    new VerilogTokenTag(VerilogGlobals.VerilogVariables[VerilogGlobals.SCOPE_CONST][lookupText]));
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
            int curLoc) {
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
                    thisDelimiterTotalDepth = VerilogGlobals.BracketDepth(containingLine.LineNumber, curLoc - containingLine.Start.Position);
                    thisDelimiterIndex = (thisDelimiterTotalDepth % 5);
                    yield return new TagSpan<VerilogTokenTag>(
                        tokenSpan,
                        // see _VerilogTypes["bracket_type1"] .. _VerilogTypes["bracket_type5"]
                        new VerilogTokenTag(VerilogGlobals.VerilogTypes["bracket_type" + (thisDelimiterIndex).ToString()]));
                    yield break;

                case VerilogGlobals.VerilogTokenContextType.RoundBracketClose:
                case VerilogGlobals.VerilogTokenContextType.RoundBracketOpen:
                    thisDelimiterTotalDepth = VerilogGlobals.BracketDepth(containingLine.LineNumber, curLoc - containingLine.Start.Position);
                    thisDelimiterIndex = (thisDelimiterTotalDepth % 5);
                    yield return new TagSpan<VerilogTokenTag>(
                        tokenSpan,
                        // see _VerilogTypes["bracket_type1"] .. _VerilogTypes["bracket_type5"]
                        new VerilogTokenTag(VerilogGlobals.VerilogTypes["bracket_type" + (thisDelimiterIndex).ToString()]));
                    yield break;

                case VerilogGlobals.VerilogTokenContextType.SquigglyBracketOpen:
                case VerilogGlobals.VerilogTokenContextType.SquigglyBracketClose:
                    thisDelimiterTotalDepth = VerilogGlobals.BracketDepth(containingLine.LineNumber, curLoc - containingLine.Start.Position);
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
