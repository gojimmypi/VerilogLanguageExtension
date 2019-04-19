using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text;

namespace VerilogLanguage.Outlining
{
    // this is code is based on the Walkthrough: Outlining
    // See: https://docs.microsoft.com/en-us/visualstudio/extensibility/walkthrough-outlining?view=vs-2015


    // Step #3: Create a class named OutliningTagger, and have it implement ITagger<T>:
    internal sealed class OutliningTagger : ITagger<IOutliningRegionTag>
    {
        // Step #4: Add some fields to track the text buffer and snapshot and to accumulate the sets of lines 
        // that should be tagged as outlining regions. This code includes a list of Region objects (to be defined later) 
        // that represent the outlining regions.

        string startHide = "(";     //the characters that start the outlining region
        string endHide = ")";       //the characters that end the outlining region
        string ellipsis = "...";    //the characters that are displayed when the region is collapsed
        string hoverText = "hover text"; //the contents of the tooltip for the collapsed span
        ITextBuffer buffer;
        ITextSnapshot snapshot;
        List<Region> regions;

        // Step #5: Add a tagger constructor that initializes the fields, parses the buffer, and adds an event handler to the Changed event.
        public OutliningTagger(ITextBuffer buffer)
        {
            this.buffer = buffer;
            this.snapshot = buffer.CurrentSnapshot;
            this.regions = new List<Region>();
            this.ReParse();
            this.buffer.Changed += BufferChanged;
        }

        // Step #6: Implement the GetTags method, which instantiates the tag spans. This example assumes that the spans in the 
        // NormalizedSpanCollection passed in to the method are contiguous, although this may not always be the case. This method 
        // instantiates a new tag span for each of the outlining regions.
        public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
                yield break;
            List<Region> currentRegions = this.regions;
            ITextSnapshot currentSnapshot = this.snapshot;
            SnapshotSpan entire = new SnapshotSpan(spans[0].Start, spans[spans.Count - 1].End).TranslateTo(currentSnapshot, SpanTrackingMode.EdgeExclusive);
            int startLineNumber = entire.Start.GetContainingLine().LineNumber;
            int endLineNumber = entire.End.GetContainingLine().LineNumber;
            foreach (var region in currentRegions)
            {
                if (region.StartLine <= endLineNumber &&
                    region.EndLine >= startLineNumber)
                {
                    var startLine = currentSnapshot.GetLineFromLineNumber(region.StartLine);
                    var endLine = currentSnapshot.GetLineFromLineNumber(region.EndLine);

                    //the region starts at the beginning of the "[", and goes until the *end* of the line that contains the "]".
                    yield return new TagSpan<IOutliningRegionTag>(
                        new SnapshotSpan(startLine.Start + region.StartOffset,
                        endLine.End),
                        new OutliningRegionTag(false, false, ellipsis, hoverText));
                }
            }
        }

        // Step #7: Declare a TagsChanged event handler.
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        // Step #8: Add a BufferChanged event handler that responds to Changed events by parsing the text buffer.
        void BufferChanged(object sender, TextContentChangedEventArgs e)
        {
            // If this isn't the most up-to-date version of the buffer, then ignore it for now (we'll eventually get another change event).
            if (e.After != buffer.CurrentSnapshot)
                return;
            this.ReParse();
        }

        // Step #9: Add a method that parses the buffer. The example given here is for illustration only. 
        // It synchronously parses the buffer into nested outlining regions.

        void ReParse()
        {
            ITextSnapshot newSnapshot = buffer.CurrentSnapshot;
            List<Region> newRegions = new List<Region>();

            //keep the current (deepest) partial region, which will have
            // references to any parent partial regions.
            PartialRegion currentRegion = null;

            foreach (var line in newSnapshot.Lines)
            {
                int regionStart = -1;
                string text = line.GetText();

                //lines that contain a "[" denote the start of a new region.
                if ((regionStart = text.IndexOf(startHide, StringComparison.Ordinal)) != -1)
                {
                    int currentLevel = (currentRegion != null) ? currentRegion.Level : 1;
                    int newLevel;
                    if (!TryGetLevel(text, regionStart, out newLevel))
                        newLevel = currentLevel + 1;

                    //levels are the same and we have an existing region;
                    //end the current region and start the next
                    if (currentLevel == newLevel && currentRegion != null)
                    {
                        newRegions.Add(new Region()
                        {
                            Level = currentRegion.Level,
                            StartLine = currentRegion.StartLine,
                            StartOffset = currentRegion.StartOffset,
                            EndLine = line.LineNumber
                        });

                        currentRegion = new PartialRegion()
                        {
                            Level = newLevel,
                            StartLine = line.LineNumber,
                            StartOffset = regionStart,
                            PartialParent = currentRegion.PartialParent
                        };
                    }
                    //this is a new (sub)region
                    else
                    {
                        currentRegion = new PartialRegion()
                        {
                            Level = newLevel,
                            StartLine = line.LineNumber,
                            StartOffset = regionStart,
                            PartialParent = currentRegion
                        };
                    }
                }
                //lines that contain "]" denote the end of a region
                else if ((regionStart = text.IndexOf(endHide, StringComparison.Ordinal)) != -1)
                {
                    int currentLevel = (currentRegion != null) ? currentRegion.Level : 1;
                    int closingLevel;
                    if (!TryGetLevel(text, regionStart, out closingLevel))
                        closingLevel = currentLevel;

                    //the regions match
                    if (currentRegion != null &&
                        currentLevel == closingLevel)
                    {
                        newRegions.Add(new Region()
                        {
                            Level = currentLevel,
                            StartLine = currentRegion.StartLine,
                            StartOffset = currentRegion.StartOffset,
                            EndLine = line.LineNumber
                        });

                        currentRegion = currentRegion.PartialParent;
                    }
                }
            }

            //determine the changed span, and send a changed event with the new spans
            List<Span> oldSpans =
                new List<Span>(this.regions.Select(r => AsSnapshotSpan(r, this.snapshot)
                    .TranslateTo(newSnapshot, SpanTrackingMode.EdgeExclusive)
                    .Span));
            List<Span> newSpans =
                    new List<Span>(newRegions.Select(r => AsSnapshotSpan(r, newSnapshot).Span));

            NormalizedSpanCollection oldSpanCollection = new NormalizedSpanCollection(oldSpans);
            NormalizedSpanCollection newSpanCollection = new NormalizedSpanCollection(newSpans);

            //the changed regions are regions that appear in one set or the other, but not both.
            NormalizedSpanCollection removed =
            NormalizedSpanCollection.Difference(oldSpanCollection, newSpanCollection);

            int changeStart = int.MaxValue;
            int changeEnd = -1;

            if (removed.Count > 0)
            {
                changeStart = removed[0].Start;
                changeEnd = removed[removed.Count - 1].End;
            }

            if (newSpans.Count > 0)
            {
                changeStart = Math.Min(changeStart, newSpans[0].Start);
                changeEnd = Math.Max(changeEnd, newSpans[newSpans.Count - 1].End);
            }

            this.snapshot = newSnapshot;
            this.regions = newRegions;

            if (changeStart <= changeEnd)
            {
                ITextSnapshot snap = this.snapshot;
                if (this.TagsChanged != null)
                    this.TagsChanged(this, new SnapshotSpanEventArgs(
                        new SnapshotSpan(this.snapshot, Span.FromBounds(changeStart, changeEnd))));
            }
        }


        // Step #10: The following helper method gets an integer that represents the level of the outlining, 
        // such that 1 is the leftmost brace pair.
        static bool TryGetLevel(string text, int startIndex, out int level)
        {
            level = -1;
            if (text.Length > startIndex + 3)
            {
                if (int.TryParse(text.Substring(startIndex + 1), out level))
                    return true;
            }

            return false;
        }

        // Step #11: The following helper method translates a Region (defined later in this topic) into a SnapshotSpan.
        static SnapshotSpan AsSnapshotSpan(Region region, ITextSnapshot snapshot)
        {
            var startLine = snapshot.GetLineFromLineNumber(region.StartLine);
            var endLine = (region.StartLine == region.EndLine) ? startLine
                 : snapshot.GetLineFromLineNumber(region.EndLine);
            return new SnapshotSpan(startLine.Start + region.StartOffset, endLine.End);
        }

        // Step #12: The following code is for illustration only. 
        // It defines a PartialRegion class that contains 
        // the line number and offset of the start of an outlining region, and also a reference to the parent 
        // region (if any). This enables the parser to set up nested outlining regions. A derived Region class 
        // contains a reference to the line number of the end of an outlining region.
        class PartialRegion
        {
            public int StartLine { get; set; }
            public int StartOffset { get; set; }
            public int Level { get; set; }
            public PartialRegion PartialParent { get; set; }
        }

        class Region : PartialRegion
        {
            public int EndLine { get; set; }
        }



    }
}
