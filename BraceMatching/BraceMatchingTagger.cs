﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

// adapted from https://github.com/madskristensen/ExtensibilityTools
// See also https://docs.microsoft.com/en-us/visualstudio/extensibility/walkthrough-displaying-matching-braces?view=vs-2019

namespace VerilogLanguage.BraceMatching
{
    // Step 3: Define a class BraceMatchingTagger that inherits from ITagger<T> of type TextMarkerTag.
    public class BraceMatchingTagger : ITagger<TextMarkerTag>
    {
        // Step 4: Add properties for the text view, the source buffer, and the current snapshot point, and also a set of brace pairs.
        ITextView View { get; set; }
        ITextBuffer SourceBuffer { get; set; }
        SnapshotPoint? CurrentChar { get; set; }
        private Dictionary<char, char> m_braceList;


        // Step 5: In the tagger constructor, set the properties and subscribe to the view change events 
        // PositionChanged and LayoutChanged. In this example, for illustrative purposes, the 
        // matching pairs are also defined in the constructor.
        internal BraceMatchingTagger(ITextView view, ITextBuffer sourceBuffer)
        {
            //here the keys are the open braces, and the values are the close braces
            m_braceList = new Dictionary<char, char> {
                { '[', ']' },
                { '{', '}' },
                { '(', ')' }
            };
            this.View = view;
            this.SourceBuffer = sourceBuffer;
            this.CurrentChar = null;

            this.View.Caret.PositionChanged += CaretPositionChanged;
            this.View.LayoutChanged += ViewLayoutChanged;
        }

        // Step 7: The event handlers update the current caret position of the CurrentChar property and raise the TagsChanged event.
        void ViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (e.NewSnapshot != e.OldSnapshot) //make sure that there has really been a change
            {
                UpdateAtCaretPosition(View.Caret.Position);
            }
        }

        void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            UpdateAtCaretPosition(e.NewPosition);
        }

        void UpdateAtCaretPosition(CaretPosition caretPosition)
        {
            CurrentChar = caretPosition.Point.GetPoint(SourceBuffer, caretPosition.Affinity);

            if (!CurrentChar.HasValue)
                return;

            var tempEvent = TagsChanged;
            if (tempEvent != null)
                tempEvent(this, new SnapshotSpanEventArgs(new SnapshotSpan(SourceBuffer.CurrentSnapshot, 0,
                    SourceBuffer.CurrentSnapshot.Length)));
        }

        // Step 8: Implement the GetTags method to match braces either when the current character 
        // is an open brace or when the previous character is a close brace, as in Visual Studio. 
        // When the match is found, this method instantiates two tags, one for the open brace and 
        // one for the close brace.
        public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)   //there is no content in the buffer
                yield break;

            //don't do anything if the current SnapshotPoint is not initialized or at the end of the buffer
            if (!CurrentChar.HasValue || CurrentChar.Value.Position >= CurrentChar.Value.Snapshot.Length)
                yield break;

            //hold on to a snapshot of the current character
            SnapshotPoint currentChar = CurrentChar.Value;

            //if the requested snapshot isn't the same as the one the brace is on, translate our spans to the expected snapshot
            if (spans[0].Snapshot != currentChar.Snapshot)
            {
                currentChar = currentChar.TranslateTo(spans[0].Snapshot, PointTrackingMode.Positive);
            }

            //get the current char and the previous char
            char currentText;
            try
            {
                // exceptions are expensive, so manually check for nulls
                if (currentChar == null)
                {
                    currentText = '\0';
                }
                else
                {
                    currentText = currentChar.GetChar();
                }
            }
            catch
            {
                currentText = '\0';
            }

            SnapshotPoint lastChar = currentChar == 0 ? currentChar : currentChar - 1; //if currentChar is 0 (beginning of buffer), don't move it back
            if (lastChar == '\0')
            {
                // nothing to do here
            }
            else {
                char lastText = lastChar.GetChar();
                SnapshotSpan pairSpan = new SnapshotSpan();

                if (m_braceList.ContainsKey(currentText))   //the key is the open brace
                {
                    char closeChar;
                    m_braceList.TryGetValue(currentText, out closeChar);
                    if (BraceMatchingTagger.FindMatchingCloseChar(currentChar, currentText, closeChar, View.TextViewLines.Count, out pairSpan) == true)
                    {
                        yield return new TagSpan<TextMarkerTag>(new SnapshotSpan(currentChar, 1), new TextMarkerTag("blue"));
                        yield return new TagSpan<TextMarkerTag>(pairSpan, new TextMarkerTag("blue"));
                    }
                }
                else if (m_braceList.ContainsValue(lastText))    //the value is the close brace, which is the *previous* character 
                {
                    var open = from n in m_braceList
                               where n.Value.Equals(lastText)
                               select n.Key;
                    if (BraceMatchingTagger.FindMatchingOpenChar(lastChar, (char)open.ElementAt<char>(0), lastText, View.TextViewLines.Count, out pairSpan) == true)
                    {
                        yield return new TagSpan<TextMarkerTag>(new SnapshotSpan(lastChar, 1), new TextMarkerTag("blue"));
                        yield return new TagSpan<TextMarkerTag>(pairSpan, new TextMarkerTag("blue"));
                    }
                }
            }
        }

        // Step 9: The following private methods find the matching brace at any level of nesting. The first method finds the close character that matches the open character:
        private static bool FindMatchingCloseChar(SnapshotPoint startPoint, char open, char close, int maxLines, out SnapshotSpan pairSpan)
        {
            pairSpan = new SnapshotSpan(startPoint.Snapshot, 1, 1);
            ITextSnapshotLine line = startPoint.GetContainingLine();
            string lineText = line.GetText();
            int lineNumber = line.LineNumber;
            int offset = startPoint.Position - line.Start.Position + 1;

            int stopLineNumber = startPoint.Snapshot.LineCount - 1;
            if (maxLines > 0)
                stopLineNumber = Math.Min(stopLineNumber, lineNumber + maxLines);

            int openCount = 0;
            while (true)
            {
                //walk the entire line
                while (offset < line.Length)
                {
                    char currentChar = lineText[offset];
                    if (currentChar == close) //found the close character
                    {
                        if (openCount > 0)
                        {
                            openCount--;
                        }
                        else    //found the matching close
                        {
                            pairSpan = new SnapshotSpan(startPoint.Snapshot, line.Start + offset, 1);
                            return true;
                        }
                    }
                    else if (currentChar == open) // this is another open
                    {
                        openCount++;
                    }
                    offset++;
                }

                //move on to the next line
                if (++lineNumber > stopLineNumber)
                    break;

                line = line.Snapshot.GetLineFromLineNumber(lineNumber);
                lineText = line.GetText();
                offset = 0;
            }

            return false;
        }

        // Step 10: The following helper method finds the open character that matches a close character:
        private static bool FindMatchingOpenChar(SnapshotPoint startPoint, char open, char close, int maxLines, out SnapshotSpan pairSpan)
        {
            pairSpan = new SnapshotSpan(startPoint, startPoint);

            ITextSnapshotLine line = startPoint.GetContainingLine();

            int lineNumber = line.LineNumber;
            int offset = startPoint - line.Start - 1; //move the offset to the character before this one

            //if the offset is negative, move to the previous line
            if ((offset < 0) && (lineNumber > 0))
            {
                line =  line.Snapshot.GetLineFromLineNumber(--lineNumber);
                offset = line.Length - 1;
            }

            string lineText = line.GetText();

            int stopLineNumber = 0;
            if (maxLines > 0)
                stopLineNumber = Math.Max(stopLineNumber, lineNumber - maxLines);

            int closeCount = 0;

            while (true)
            {
                // Walk the entire line
                while (offset >= 0)
                {
                    char currentChar = lineText[offset];

                    if (currentChar == open)
                    {
                        if (closeCount > 0)
                        {
                            closeCount--;
                        }
                        else // We've found the open character
                        {
                            pairSpan = new SnapshotSpan(line.Start + offset, 1); //we just want the character itself
                            return true;
                        }
                    }
                    else if (currentChar == close)
                    {
                        closeCount++;
                    }
                    offset--;
                }

                // Move to the previous line
                if (--lineNumber < stopLineNumber)
                    break;

                line = line.Snapshot.GetLineFromLineNumber(lineNumber);
                lineText = line.GetText();
                offset = line.Length - 1;
            }
            return false;
        }

        // Step 6: As part of the ITagger<T> implementation, declare a TagsChanged event.
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}
