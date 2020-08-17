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

namespace VerilogLanguage.VerilogToken
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.Utilities;
    using CommentHelper;
    using System.Threading;

    internal sealed class VerilogTokenTagger : ITagger<VerilogTokenTag>
    {
        // ITextView View { get; set; }
        ITextBuffer _buffer;

        internal VerilogTokenTagger(ITextBuffer buffer)
        {
           
            VerilogGlobals.PerfMon.VerilogTokenTagger_Count++;
            VerilogGlobals.TheBuffer = buffer;
            _buffer = buffer;

            this._buffer.Changed += BufferChanged;
        }




        //private void Start()
        //{
        //    System.Diagnostics.Debug.WriteLine("1. Call thread task");

        //    StartMyLongRunningTask();

        //    System.Diagnostics.Debug.WriteLine("2. Do something else");
        //}

        //private void StartMyLongRunningTask()
        //{

        //    ThreadStart starter = myLongRunningTask;

        //    starter += () =>
        //    {
        //        myLongRunningTaskDone();
        //    };

        //    Thread _thread = new Thread(starter) { IsBackground = true };
        //    _thread.Start();
        //}

        //private void myLongRunningTaskDone()
        //{
        //    System.Diagnostics.Debug.WriteLine("3. Task callback result");
        //}

        //private void myLongRunningTask()
        //{
        //    string thisFile = VerilogLanguage.VerilogGlobals.GetDocumentPath(_buffer.CurrentSnapshot);
        //    VerilogGlobals.Reparse(_buffer, thisFile); // note that above, we are checking that the e.After is the same as the _buffer
        //}

        /// <summary>
        ///   BufferChanged - handle Buffer Changed event. If buffer has a character with possible far-reaching consequences
        ///                   then force a rescan of the enture buffer. See also HighlightWordTaggerProvider 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void BufferChanged(object sender, TextContentChangedEventArgs e)
        {
            // If this isn't the most up-to-date version of the buffer, then ignore it for now (we'll eventually get another change event).
            if (e.After != _buffer.CurrentSnapshot)
                return;

            if (e.Changes.Count < 1)
            {
                // TODO - how did we get here if there are no changes? (found this after exception during debug. no apparent invoke. )
                return;
            }

            string theNewText = e.Changes[0].NewText;
            string theOldText = e.Changes[0].OldText;

            if (e.Changes.Count > 1)
            {
                // TODO - why exit if there is more than one change (perhaps exit if LESS than 1? no!)
                return;
            }


            // we are only interested when the old and new text are different. 
            // yes, the event seems to be triggered even with no apparent changes
            // 
            if (theNewText != theOldText)
            {
                // even if the buffer is different, only certain characters require a full reparse
                // typically brackets (since we keep track of depth) and comment chars:
                if (VerilogGlobals.IsRefreshChar(theNewText) || VerilogGlobals.IsRefreshChar(theOldText))
                {
                    string thisFile = VerilogLanguage.VerilogGlobals.GetDocumentPath(_buffer.CurrentSnapshot);

                    // VerilogGlobals.ParseStatus_NeedReparse_SetValue(thisFile, true);
                    VerilogGlobals.ParseStatusController.NeedReparse_SetValue(thisFile, true);
                    //VerilogGlobals.ParseStatus_EnsureExists(thisFile);
                    //VerilogGlobals.ParseStatus[thisFile].NeedReparse = true;
                    // VerilogGlobals.NeedReparse = true;

                    //myLongRunningTask();
                    VerilogGlobals.Reparse(_buffer, thisFile); // note that above, we are checking that the e.After is the same as the _buffer
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("BufferChanged called but new and old text not different!");
            }
        }

        //=====================================================================================
        // TODO - do spans always start on new lines? If not, we'll need an IsOpenLineComment
        private bool IsOpenBlockComment(NormalizedSnapshotSpanCollection sc) // are we starting with prior text that is already an opening comment block?
        {

            // return false;

            VerilogGlobals.PerfMon.VerilogTokenTagger_IsOpenBlockComment_Count++; 
            bool isLocalBlockComment = false; // we'll assume there's no open block comment unless otherwise found
            bool isLocalLineComment = false;
            if (sc != null && sc[0].Snapshot != null && sc[0].Start.Position > 0)
            {
                int ToPosition = sc[0].Start.Position - 1; // we are only interested in text priot to our current location
                // SnapshotSpan PriorText = sc[0].Snapshot(0, ToPosition);
                foreach (ITextSnapshotLine thisLine in sc[0].Snapshot.Lines)
                {
                    int pos = thisLine.Start.Position;
                    if (pos > ToPosition)
                    {
                        break; // nothing to do if the starting position is beyond our starting point, as we are only interested in prior open block
                    }
                    CommentHelper commentHelper = new CommentHelper(thisLine.GetText(), isLocalLineComment, isLocalBlockComment); // we are starting at the beginning of a string, so there's of course no prior "//" continued line comment
                    isLocalBlockComment = commentHelper.HasBlockStartComment;
                    isLocalLineComment = false; // we are sending entire lines here, so we are never worried about continued line comments previously starting with "//"
                } // for each thisLine
            } // if sc is not blank
            return isLocalBlockComment;
        }





        /// <summary>
        ///   IEnumerable VerilogTokenTag GetTags
        /// </summary>
        /// <param name="spans"></param>
        /// <returns></returns>
        public IEnumerable<ITagSpan<VerilogTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            //while (VerilogGlobals.IsReparsing)
            {
                // do we really want to do this? (probably not)
                // System.Threading.Thread.Sleep(10);
            }


            //System.Diagnostics.Debug.WriteLine("Starting IEnumerable<ITagSpan<VerilogTokenTag>>");
            // bool EditInProgress = spans.snapshot.TextBuffer.EditInProgress;
            // since we can start mid-text, we don't know if the current span is in the middle of a comment

            // init TODO - we don't really want to call this for every enumeration!
            // VerilogGlobals.InitHoverBuilder();
            VerilogGlobals.IsContinuedBlockComment = IsOpenBlockComment(spans); // TODO - does spans always contain the full document? (appears perhaps not)
            VerilogGlobals.VerilogToken[] tokens = null;
            VerilogGlobals.VerilogToken priorToken = new VerilogGlobals.VerilogToken();

            // look at each span for tokens, comments, etc
            foreach (SnapshotSpan curSpan in spans)
            {
                    if (tokens != null && tokens.Length >= 1)
                {
                    priorToken = tokens[tokens.Length - 1]; // get the token from the prior line
                }
                ITextSnapshotLine containingLine = curSpan.Start.GetContainingLine();
                int curLoc = containingLine.Start.Position;
                int LinePosition = 0;
                string thisTokenString = "";

                tokens = VerilogGlobals.VerilogKeywordSplit(containingLine.GetText(), priorToken);

                Boolean IsContinuedLineComment = false; // comments with "//" are only effective for the current line, but /* can span multiple lines
                foreach (VerilogGlobals.VerilogToken VerilogToken in tokens) // this group of tokens in in a single line
                {
                    // by the time we get here, we might have a tag with adjacent comments:
                    //     assign//
                    //     //assign
                    //     assign//comment
                    //     /*assign*/
                    //     assign/*comment*/
                    thisTokenString = VerilogToken.Part;
                    CommentHelper commentHelper = new CommentHelper(thisTokenString,
                                                                    IsContinuedLineComment,
                                                                    VerilogGlobals.IsContinuedBlockComment);
                    VerilogGlobals.IsContinuedBlockComment = commentHelper.HasBlockStartComment;
                    IsContinuedLineComment = commentHelper.HasOpenLineComment; // we'll use this when processing the VerilogToken item in the commentHelper, above

                    foreach (CommentHelper.CommentItem Item in commentHelper.CommentItems)
                    {
                        bool TestComment = VerilogGlobals.TextIsComment(containingLine.LineNumber, LinePosition);
                        LinePosition += Item.ItemText.Length;

                        var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc, Item.ItemText.Length));
                        if (tokenSpan.IntersectsWith(curSpan))
                        {
                            // is this item a comment? If so, color as appropriate. comments take highest priority: no other condition will change color of a comment
                            if (Item.IsComment)
                            {
                                // System.Diagnostics.Debug.WriteLine("IEnumerable VerilogTokenTag yield comment for item " + Item.ItemText??"");
                                yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                                                                      new VerilogTokenTag(VerilogTokenTypes.Verilog_Comment));
                            }

                            // otherwise when not a comment, check to see if it is a keyword
                            else
                            {
                                // first check to see if any new variables are being defined;

                                // VerilogGlobals.BuildHoverItems(Item.ItemText);


                                // check for standard keyword syntax higlighting
                                if (VerilogGlobals.VerilogTypes.ContainsKey(Item.ItemText))
                                {
                                    System.Diagnostics.Debug.WriteLine("IEnumerable VerilogTokenTag yield " + Item.ItemText);
                                    yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                                                                          new VerilogTokenTag(VerilogGlobals.VerilogTypes[Item.ItemText]));
                                }
                                else if (VerilogGlobals.VerilogVariables.ContainsKey(Item.ItemText))
                                {
                                    // we are instantiation a module; recall VerilogVariables is first a dictionary of scope (aka module), then a dictionary of variables in each module scope
                                    // TODO do we need: if (tokenSpan.IntersectsWith(curSpan))
                                    System.Diagnostics.Debug.WriteLine("IEnumerable VerilogTokenTag yield variable module " + Item.ItemText);
                                    yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                                          new VerilogTokenTag(VerilogGlobals.VerilogTypes["variable_module"]));
                                }
                                else
                                {
                                    // check to see if this is a variable
                                    string thisScope = VerilogGlobals.TextModuleName(containingLine.LineNumber, curLoc - containingLine.Start.Position); // TODO 
                                    if (VerilogGlobals.VerilogVariables.ContainsKey(thisScope))
                                    {
                                        // the current scope (typically a module name) is defined. So do we have a known variable?
                                        if (VerilogGlobals.VerilogVariables[thisScope].ContainsKey(Item.ItemText))
                                        {
                                            // TODO do we need: if (tokenSpan.IntersectsWith(curSpan))
                                            System.Diagnostics.Debug.WriteLine("IEnumerable VerilogTokenTag yield variable " + Item.ItemText);
                                            yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                                                                                  new VerilogTokenTag(VerilogGlobals.VerilogVariables[thisScope][Item.ItemText]));
                                        }

                                        else if (VerilogGlobals.VerilogVariables.ContainsKey(VerilogGlobals.SCOPE_CONST) && VerilogGlobals.VerilogVariables[VerilogGlobals.SCOPE_CONST].ContainsKey(Item.ItemText))
                                        {
                                            yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                                                                                  new VerilogTokenTag(VerilogGlobals.VerilogVariables[VerilogGlobals.SCOPE_CONST][Item.ItemText]));
                                        }

                                        else
                                        {
                                            // no tag colorization for the explicit token, but perhaps based on context:
                                            int thisDelimiterIndex = 0;
                                            int thisDelimiterTotalDepth;
                                            //int thisDelimiterTotalDepth = VerilogToken.SquareBracketDepth +
                                            //                              VerilogToken.RoundBracketDepth +
                                            //                              VerilogToken.SquigglyBracketDepth;
                                            // int testValue = VerilogGlobals.BracketDepth(containingLine.LineNumber, curLoc - containingLine.Start.Position);
                                            switch (VerilogToken.Context)
                                            {
                                                case VerilogGlobals.VerilogTokenContextType.SquareBracketOpen:
                                                case VerilogGlobals.VerilogTokenContextType.SquareBracketClose:
                                                    thisDelimiterTotalDepth = VerilogGlobals.BracketDepth(containingLine.LineNumber, curLoc - containingLine.Start.Position);
                                                    thisDelimiterIndex = (thisDelimiterTotalDepth % 5);
                                                    yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                                                                                          // see _VerilogTypes["bracket_type1"] .. _VerilogTypes["bracket_type5"]
                                                                                          new VerilogTokenTag(VerilogGlobals.VerilogTypes["bracket_type" + (thisDelimiterIndex).ToString()]));
                                                    break;

                                                case VerilogGlobals.VerilogTokenContextType.RoundBracketClose:
                                                case VerilogGlobals.VerilogTokenContextType.RoundBracketOpen:
                                                    thisDelimiterTotalDepth = VerilogGlobals.BracketDepth(containingLine.LineNumber, curLoc - containingLine.Start.Position);
                                                    thisDelimiterIndex = (thisDelimiterTotalDepth % 5);
                                                    yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                                                                                          // see _VerilogTypes["bracket_type1"] .. _VerilogTypes["bracket_type5"]
                                                                                          new VerilogTokenTag(VerilogGlobals.VerilogTypes["bracket_type" + (thisDelimiterIndex).ToString()]));
                                                    break;

                                                case VerilogGlobals.VerilogTokenContextType.SquigglyBracketOpen:
                                                case VerilogGlobals.VerilogTokenContextType.SquigglyBracketClose:
                                                    thisDelimiterTotalDepth = VerilogGlobals.BracketDepth(containingLine.LineNumber, curLoc - containingLine.Start.Position);
                                                    thisDelimiterIndex = (thisDelimiterTotalDepth % 5);
                                                    // see _VerilogTypes["bracket_type1"] .. _VerilogTypes["bracket_type5"]
                                                    yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                                                        new VerilogTokenTag(VerilogGlobals.VerilogTypes["bracket_type" + (thisDelimiterIndex).ToString()]));
                                                    break;

                                                case VerilogGlobals.VerilogTokenContextType.SquareBracketContents:
                                                    yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                                                                                          new VerilogTokenTag(VerilogTokenTypes.Verilog_BracketContent));
                                                    break;

                                                case VerilogGlobals.VerilogTokenContextType.AlwaysAt:
                                                    yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                                                                                          new VerilogTokenTag(VerilogTokenTypes.Verilog_always));
                                                    break;

                                                default:
                                                    // no highlighting
                                                    break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (VerilogGlobals.VerilogVariables.ContainsKey(VerilogGlobals.SCOPE_CONST) && VerilogGlobals.VerilogVariables[VerilogGlobals.SCOPE_CONST].ContainsKey(Item.ItemText))
                                        {
                                            //yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                                            //      new VerilogTokenTag(VerilogGlobals.VerilogTypes["Verilog_Value"]));
                                            yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                                                                                      new VerilogTokenTag(VerilogGlobals.VerilogVariables[VerilogGlobals.SCOPE_CONST][Item.ItemText]));

                                        }
                                        else
                                        {
                                            // TODO - how do we get here when thisScope *is* defined? timing?
                                            // A: we destroy the VerilogVariables when rescanning (otherwise everyuthing is a duplicate) TODO: keep track of where variables are defined. don't rebui;d
                                            System.Diagnostics.Debug.WriteLine("Warning! VerilogGlobals.VerilogVariables.ContainsKey({0}) not defined!", thisScope);
                                        }
                                    }
                                }
                            }

                        }

                        // note that no chars are lost when splitting string with VerilogKeywordSplit, so no adjustment needed in location
                        curLoc += Item.ItemText.Length;
                    }
                }
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        void ViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {            
            if (e.NewSnapshot != e.OldSnapshot) //make sure that there has really been a change
            {
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(_buffer.CurrentSnapshot, 0,
                        _buffer.CurrentSnapshot.Length)));
            }
        }
    }

}
