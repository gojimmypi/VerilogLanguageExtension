using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using static VerilogLanguage.VerilogGlobals;

// Code based on Walkthrough: Highlighting Text
// See: https://docs.microsoft.com/en-us/visualstudio/extensibility/walkthrough-highlighting-text?view=vs-2015


namespace VerilogLanguage.Highlighting
{
    // Code based on Walkthrough: Highlighting Text
    // See: https://docs.microsoft.com/en-us/visualstudio/extensibility/walkthrough-highlighting-text?view=vs-2015
    internal class HighlightWordTagger : ITagger<HighlightWordTag>
    {
        //An ITextView, which corresponds to the current text view.
        ITextView View { get; set; }

        //An ITextBuffer, which corresponds to the text buffer that underlies the text view.
        ITextBuffer SourceBuffer { get; set; }

        //An ITextSearchService, which is used to find text.
        ITextSearchService TextSearchService { get; set; }

        //An ITextStructureNavigator, which has methods for navigating within text spans.
        ITextStructureNavigator TextStructureNavigator { get; set; }

        //A NormalizedSnapshotSpanCollection, which contains the set of words to highlight.
        NormalizedSnapshotSpanCollection WordSpans { get; set; }

        //A SnapshotSpan, which corresponds to the current word.
        SnapshotSpan? CurrentWord { get; set; }

        //A SnapshotPoint, which corresponds to the current position of the caret.
        SnapshotPoint RequestedPoint { get; set; }

        //A lock object.
        object updateLock = new object();

        public HighlightWordTagger(ITextView view, 
                                   ITextBuffer sourceBuffer, 
                                   ITextSearchService textSearchService,
                                   ITextStructureNavigator textStructureNavigator)
        {
            this.View = view;
            this.SourceBuffer = sourceBuffer;
            this.TextSearchService = textSearchService;
            this.TextStructureNavigator = textStructureNavigator;
            this.WordSpans = new NormalizedSnapshotSpanCollection();
            this.CurrentWord = null;
            this.View.Caret.PositionChanged += CaretPositionChanged;
            this.View.LayoutChanged += ViewLayoutChanged;
        }

        // Step 4: The event handlers both call the UpdateAtCaretPosition method.
        void ViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            // If a new snapshot wasn't generated, then skip this layout   
            if (e.NewSnapshot != e.OldSnapshot)
            {
                UpdateAtCaretPosition(View.Caret.Position);
                string thisFile = VerilogLanguage.VerilogGlobals.GetDocumentPath(View.TextSnapshot);
                //VerilogGlobals.ParseStatus_EnsureExists(thisFile);
                //VerilogGlobals.ParseStatus[thisFile].NeedReparse = true;
                // VerilogGlobals.ParseStatus_NeedReparse_SetValue(thisFile, true);
                ParseStatusController.NeedReparse_SetValue(thisFile, true);
                //VerilogGlobals.NeedReparse = true;
                VerilogGlobals.Reparse(SourceBuffer, thisFile);
            }
        }

        void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            UpdateAtCaretPosition(e.NewPosition);
        }

        // Step 5: You must also add a TagsChanged event that will be called by the update method.
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        // Step 6: The UpdateAtCaretPosition() method finds every word in the text buffer that is identical 
        // to the word where the cursor is positioned and constructs a list of SnapshotSpan objects that 
        // correspond to the occurrences of the word. It then calls SynchronousUpdate, which raises the TagsChanged event.
        void UpdateAtCaretPosition(CaretPosition caretPosition)
        {
            SnapshotPoint? point = caretPosition.Point.GetPoint(SourceBuffer, caretPosition.Affinity);

            if (!point.HasValue)
                return;

            // If the new caret position is still within the current word (and on the same snapshot), we don't need to check it   
            if (CurrentWord.HasValue
                && CurrentWord.Value.Snapshot == View.TextSnapshot
                && point.Value >= CurrentWord.Value.Start
                && point.Value <= CurrentWord.Value.End)
            {
                return;
            }

            RequestedPoint = point.Value;
            UpdateWordAdornments();
        }

        void UpdateWordAdornments()
        {
            SnapshotPoint currentRequest = RequestedPoint;
            List<SnapshotSpan> wordSpans = new List<SnapshotSpan>();
            //Find all words in the buffer like the one the caret is on  
            TextExtent word = TextStructureNavigator.GetExtentOfWord(currentRequest);
            bool foundWord = true;
            //If we've selected something not worth highlighting, we might have missed a "word" by a little bit  
            if (!WordExtentIsValid(currentRequest, word))
            {
                //Before we retry, make sure it is worthwhile   
                if (word.Span.Start != currentRequest
                     || currentRequest == currentRequest.GetContainingLine().Start
                     || char.IsWhiteSpace((currentRequest - 1).GetChar()))
                {
                    foundWord = false;
                }
                else
                {
                    // Try again, one character previous.    
                    //If the caret is at the end of a word, pick up the word.  
                    word = TextStructureNavigator.GetExtentOfWord(currentRequest - 1);

                    //If the word still isn't valid, we're done   
                    if (!WordExtentIsValid(currentRequest, word))
                        foundWord = false;
                }
            }

            if (!foundWord)
            {
                //If we couldn't find a word, clear out the existing markers  
                SynchronousUpdate(currentRequest, new NormalizedSnapshotSpanCollection(), null);
                return;
            }

            SnapshotSpan currentWord = word.Span;
            //If this is the current word, and the caret moved within a word, we're done.   
            if (CurrentWord.HasValue && currentWord == CurrentWord)
                return;

            //Find the new spans  
            FindData findData = new FindData(currentWord.GetText(), currentWord.Snapshot);
            findData.FindOptions = FindOptions.WholeWord | FindOptions.MatchCase;

            wordSpans.AddRange(TextSearchService.FindAll(findData));

            //If another change hasn't happened, do a real update   
            if (currentRequest == RequestedPoint)
                SynchronousUpdate(currentRequest, new NormalizedSnapshotSpanCollection(wordSpans), currentWord);
        }
        static bool WordExtentIsValid(SnapshotPoint currentRequest, TextExtent word)
        {
            return word.IsSignificant
                && currentRequest.Snapshot.GetText(word.Span).Any(c => char.IsLetter(c));
        }

        // Step 7: The SynchronousUpdate performs a synchronous update on the WordSpans and 
        // CurrentWord properties, and raises the TagsChanged event.
        void SynchronousUpdate(SnapshotPoint currentRequest, NormalizedSnapshotSpanCollection newSpans, SnapshotSpan? newCurrentWord)
        {
            lock (updateLock)
            {
                if (currentRequest != RequestedPoint)
                    return;

                WordSpans = newSpans;
                CurrentWord = newCurrentWord;

                var tempEvent = TagsChanged;
                if (tempEvent != null)
                    tempEvent(this, new SnapshotSpanEventArgs(new SnapshotSpan(SourceBuffer.CurrentSnapshot, 0, SourceBuffer.CurrentSnapshot.Length)));
            }
        }

        // Step 8: You must implement the GetTags method. This method takes a collection of SnapshotSpan objects 
        // and returns an enumeration of tag spans.
        // 
        // In C#, implement this method as a yield iterator, which enables lazy evaluation (that is, evaluation 
        // of the set only when individual items are accessed) of the tags. In Visual Basic, add the tags to a list and return the list.
        //
        // Here the method returns a TagSpan<T> object that has a "blue" TextMarkerTag, which provides a blue background.
        public IEnumerable<ITagSpan<HighlightWordTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (CurrentWord == null)
                yield break;

            // Hold on to a "snapshot" of the word spans and current word, so that we maintain the same  
            // collection throughout  
            SnapshotSpan currentWord = CurrentWord.Value;
            NormalizedSnapshotSpanCollection wordSpans = WordSpans;

            if (spans.Count == 0 || wordSpans.Count == 0)
                yield break;

            // If the requested snapshot isn't the same as the one our words are on, translate our spans to the expected snapshot   
            if (spans[0].Snapshot != wordSpans[0].Snapshot)
            {
                wordSpans = new NormalizedSnapshotSpanCollection(
                    wordSpans.Select(span => span.TranslateTo(spans[0].Snapshot, SpanTrackingMode.EdgeExclusive)));

                currentWord = currentWord.TranslateTo(spans[0].Snapshot, SpanTrackingMode.EdgeExclusive);
            }

            // First, yield back the word the cursor is under (if it overlaps)   
            // Note that we'll yield back the same word again in the wordspans collection;   
            // the duplication here is expected.   
            if (spans.OverlapsWith(new NormalizedSnapshotSpanCollection(currentWord)))
                yield return new TagSpan<HighlightWordTag>(currentWord, new HighlightWordTag());

            // Second, yield all the other words in the file   
            foreach (SnapshotSpan span in NormalizedSnapshotSpanCollection.Overlap(spans, wordSpans))
            {
                yield return new TagSpan<HighlightWordTag>(span, new HighlightWordTag());
            }
        }

    }


}
