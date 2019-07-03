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

    internal sealed class VerilogTokenTagger : ITagger<VerilogTokenTag>
    {

        ITextBuffer _buffer;
        IDictionary<string, VerilogTokenTypes> _VerilogTypes;

        internal VerilogTokenTagger(ITextBuffer buffer)
        {
            VerilogGlobals.PerfMon.VerilogTokenTagger_Count++;

            VerilogGlobals.TheBuffer = buffer;
            _buffer = buffer;

            // see also VerilogClassifier that has Dictionary<VerilogTokenTypes, IClassificationType>
            _VerilogTypes = new Dictionary<string, VerilogTokenTypes>
            {
                ["always"] = VerilogTokenTypes.Verilog_always,
                ["assign"] = VerilogTokenTypes.Verilog_assign,
                ["automatic"] = VerilogTokenTypes.Verilog_automatic,
                ["begin"] = VerilogTokenTypes.Verilog_begin,
                ["case"] = VerilogTokenTypes.Verilog_case,
                ["casex"] = VerilogTokenTypes.Verilog_casex,
                ["casez"] = VerilogTokenTypes.Verilog_casez,
                ["cell"] = VerilogTokenTypes.Verilog_cell,
                ["config"] = VerilogTokenTypes.Verilog_config,
                ["deassign"] = VerilogTokenTypes.Verilog_deassign,
                ["default"] = VerilogTokenTypes.Verilog_default,
                ["defparam"] = VerilogTokenTypes.Verilog_defparam,
                ["design"] = VerilogTokenTypes.Verilog_design,
                ["disable"] = VerilogTokenTypes.Verilog_disable,
                ["edge"] = VerilogTokenTypes.Verilog_edge,
                ["else"] = VerilogTokenTypes.Verilog_else,
                ["end"] = VerilogTokenTypes.Verilog_end,
                ["endcase"] = VerilogTokenTypes.Verilog_endcase,
                ["endconfig"] = VerilogTokenTypes.Verilog_endconfig,
                ["endfunction"] = VerilogTokenTypes.Verilog_endfunction,
                ["endgenerate"] = VerilogTokenTypes.Verilog_endgenerate,
                ["endmodule"] = VerilogTokenTypes.Verilog_endmodule,
                ["endprimitive"] = VerilogTokenTypes.Verilog_endprimitive,
                ["endspecify"] = VerilogTokenTypes.Verilog_endspecify,
                ["endtable"] = VerilogTokenTypes.Verilog_endtable,
                ["endtask"] = VerilogTokenTypes.Verilog_endtask,
                ["event"] = VerilogTokenTypes.Verilog_event,
                ["for"] = VerilogTokenTypes.Verilog_for,
                ["force"] = VerilogTokenTypes.Verilog_force,
                ["forever"] = VerilogTokenTypes.Verilog_forever,
                ["fork"] = VerilogTokenTypes.Verilog_fork,
                ["function"] = VerilogTokenTypes.Verilog_function,
                ["generate"] = VerilogTokenTypes.Verilog_generate,
                ["genvar"] = VerilogTokenTypes.Verilog_genvar,
                ["if"] = VerilogTokenTypes.Verilog_if,
                ["ifnone"] = VerilogTokenTypes.Verilog_ifnone,
                ["incdir"] = VerilogTokenTypes.Verilog_incdir,
                ["include"] = VerilogTokenTypes.Verilog_include,
                ["initial"] = VerilogTokenTypes.Verilog_initial,
                ["inout"] = VerilogTokenTypes.Verilog_inout,
                ["input"] = VerilogTokenTypes.Verilog_input,
                ["instance"] = VerilogTokenTypes.Verilog_instance,
                ["join"] = VerilogTokenTypes.Verilog_join,
                ["liblist"] = VerilogTokenTypes.Verilog_liblist,
                ["library"] = VerilogTokenTypes.Verilog_library,
                ["localparam"] = VerilogTokenTypes.Verilog_localparam,
                ["macromodule"] = VerilogTokenTypes.Verilog_macromodule,
                ["module"] = VerilogTokenTypes.Verilog_module,
                ["negedge"] = VerilogTokenTypes.Verilog_negedge,
                ["noshowcancelled"] = VerilogTokenTypes.Verilog_noshowcancelled,
                ["output"] = VerilogTokenTypes.Verilog_output,
                ["parameter"] = VerilogTokenTypes.Verilog_parameter,
                ["posedge"] = VerilogTokenTypes.Verilog_posedge,
                ["primitive"] = VerilogTokenTypes.Verilog_primitive,
                ["pulsestyle_ondetect"] = VerilogTokenTypes.Verilog_pulsestyle_ondetect,
                ["pulsestyle_onevent"] = VerilogTokenTypes.Verilog_pulsestyle_onevent,
                ["reg"] = VerilogTokenTypes.Verilog_reg,
                ["release"] = VerilogTokenTypes.Verilog_release,
                ["repeat"] = VerilogTokenTypes.Verilog_repeat,
                ["scalared"] = VerilogTokenTypes.Verilog_scalared,
                ["showcancelled"] = VerilogTokenTypes.Verilog_showcancelled,
                ["signed"] = VerilogTokenTypes.Verilog_signed,
                ["specify"] = VerilogTokenTypes.Verilog_specify,
                ["specparam"] = VerilogTokenTypes.Verilog_specparam,
                ["strength"] = VerilogTokenTypes.Verilog_strength,
                ["table"] = VerilogTokenTypes.Verilog_table,
                ["task"] = VerilogTokenTypes.Verilog_task,
                ["tri"] = VerilogTokenTypes.Verilog_tri,
                ["tri0"] = VerilogTokenTypes.Verilog_tri0,
                ["tri1"] = VerilogTokenTypes.Verilog_tri1,
                ["triand"] = VerilogTokenTypes.Verilog_triand,
                ["wand"] = VerilogTokenTypes.Verilog_wand,
                ["trior"] = VerilogTokenTypes.Verilog_trior,
                ["wor"] = VerilogTokenTypes.Verilog_wor,
                ["trireg"] = VerilogTokenTypes.Verilog_trireg,
                ["unsigned"] = VerilogTokenTypes.Verilog_unsigned,
                ["use"] = VerilogTokenTypes.Verilog_use,
                ["vectored"] = VerilogTokenTypes.Verilog_vectored,
                ["wait"] = VerilogTokenTypes.Verilog_wait,
                ["while"] = VerilogTokenTypes.Verilog_while,
                ["wire"] = VerilogTokenTypes.Verilog_wire,

                // all of the Verilog directives are the same color
                ["`celldefine"] = VerilogTokenTypes.Verilog_Directive,
                ["`endcelldefine"] = VerilogTokenTypes.Verilog_Directive,
                ["`default_nettype"] = VerilogTokenTypes.Verilog_Directive,
                ["`define"] = VerilogTokenTypes.Verilog_Directive,
                ["`undef"] = VerilogTokenTypes.Verilog_Directive,
                ["`ifdef"] = VerilogTokenTypes.Verilog_Directive,
                ["`ifndef"] = VerilogTokenTypes.Verilog_Directive,
                ["`elsif"] = VerilogTokenTypes.Verilog_Directive,
                ["`else"] = VerilogTokenTypes.Verilog_Directive,
                ["`endif"] = VerilogTokenTypes.Verilog_Directive,
                ["`include"] = VerilogTokenTypes.Verilog_Directive,
                ["`resetall"] = VerilogTokenTypes.Verilog_Directive,
                ["`line"] = VerilogTokenTypes.Verilog_Directive,
                ["`timescale"] = VerilogTokenTypes.Verilog_Directive,
                ["`unconnected_drive"] = VerilogTokenTypes.Verilog_Directive,
                ["`nounconnected_driv"] = VerilogTokenTypes.Verilog_Directive,

                ["comment_type"] = VerilogTokenTypes.Verilog_Comment,

                ["bracket_type"] = VerilogTokenTypes.Verilog_Bracket,
                ["bracket_type0"] = VerilogTokenTypes.Verilog_Bracket0,
                ["bracket_type1"] = VerilogTokenTypes.Verilog_Bracket1,
                ["bracket_type2"] = VerilogTokenTypes.Verilog_Bracket2,
                ["bracket_type3"] = VerilogTokenTypes.Verilog_Bracket3,
                ["bracket_type4"] = VerilogTokenTypes.Verilog_Bracket4,
                ["bracket_type5"] = VerilogTokenTypes.Verilog_Bracket5,
                ["bracket_content"] = VerilogTokenTypes.Verilog_BracketContent,

                ["variable_type"] = VerilogTokenTypes.Verilog_Variable
            };

            this._buffer.Changed += BufferChanged;
        }

        /// <summary>
        ///   BufferChanged - handle Buffer Changed event. If buffer has character with possible far-reaching consequences
        ///                   then force a rescan of the enture buffer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void BufferChanged(object sender, TextContentChangedEventArgs e)
        {
            // If this isn't the most up-to-date version of the buffer, then ignore it for now (we'll eventually get another change event).
            if (e.After != _buffer.CurrentSnapshot)
                return;
            
            string theNewText = e.Changes[0].NewText;
            string theOldText = e.Changes[0].OldText;

            if (e.Changes.Count > 1)
            {
                return;
            }


            // we are only interested when the old and new text are different. 
            // yes, the event seems to be triggered even with no apparent changes
            // 
            if (theNewText != theOldText)
            {
                if (IsRefreshChar(theNewText) || IsRefreshChar(theOldText))
                {
                    VerilogGlobals.TheNewPosition = VerilogGlobals.TheView.Caret.Position.BufferPosition.Position;

                    if (VerilogGlobals.TheNewPosition <= VerilogGlobals.TheBuffer.CurrentSnapshot.Length)
                    {

                    SnapshotPoint bp = new SnapshotPoint(VerilogGlobals.TheBuffer.CurrentSnapshot, VerilogGlobals.TheNewPosition);
                    var point = VerilogGlobals.TheView.Caret.Position.BufferPosition;
                    VerilogGlobals.PriorVerticalDistance = VerilogGlobals.TheView.GetTextViewLineContainingBufferPosition(point).TextTop - VerilogGlobals.TheView.ViewportTop;

                    VerilogGlobals.Reparse(_buffer); // note that above, we are checking that the e.After is the same as the _buffer

                        // wouldn't it be great if we could invoke an evn to recan the entire document?
                        // note that if this ever works, attention would be needed to the cursor repositioning in CompletionController.
                        // although the event fires, the fulle document is *not* updated with new tage (e.g. when a new, open "/*" is introduced)
                        // 
                        // var tempEvent = TagsChanged;
                        // if (tempEvent != null)
                        //    tempEvent(this, 
                        //              new SnapshotSpanEventArgs(
                        //                  new SnapshotSpan(_buffer.CurrentSnapshot,
                        //                  0,
                        //                  _buffer.CurrentSnapshot.Length)));

                        // Instead, we revert to the brute-force method of forcing a refresh
                        // note we pass the lenght of the newly inserted (typed or pasted) text to adjust cursor posotion in CompletionController
                        // Note the most graceful, but ivoking event did not cooperate and did not recan entire document as needed
                        //VerilogGlobals.ForceRefresh(theNewText.Length);

                        if (!VerilogGlobals.ForceRefreshInProgress)
                        {
                            VerilogGlobals.ForceRefresh(theNewText.Length); // without appending the new length, the cursor does not move when typing! TODO: but what about paste!
                        }

                        // mensure the cursor stays in the same place.
                        // see https://stackoverflow.com/questions/42712164/replacing-text-in-document-while-preserving-the-caret
                        // VerilogGlobals.TheNewPosition += theNewText.Length;
                    }
                    else
                    {
                        string m = "out of range?";
                    }

                }
            }
        }

        // some characters, when encountered will need to have a full refresh, as they can have far-reaching consequences.
        static public bool IsRefreshChar(string theString)
        {
            return (theString.Contains("/")) ||
                   (theString.Contains("*")) ||
                   (theString.Contains("[")) ||
                   (theString.Contains("]")) ||
                   (theString.Contains("}")) ||
                   (theString.Contains("{")) ||
                   (theString.Contains("(")) ||
                   (theString.Contains(")"));  
        }

        static public bool IsDelimeter(string theString)
        {
            return (theString == " ") ||
                   (theString == "[") ||
                   (theString == "]") ||
                   (theString == "}") ||
                   (theString == "{") ||
                   (theString == "(") ||
                   (theString == ")") ||
                   (theString == ";") ||
                   (theString == ",") ||
                   (theString == "@") ||
                   (theString == "\"") ||
                   (theString == "\t");
        }

        static public bool IsEndingDelimeter(string theString)
        {
            return (theString == "]") ||
                   (theString == "}") ||
                   (theString == ")");
        }


        //=====================================================================================
        // TODO - do spans always start on new lines? If not, we'll need an IsOpenLineComment
        private bool IsOpenBlockComment(NormalizedSnapshotSpanCollection sc) // are we starting with prior text that is already an opening comment block?
        {
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
                    isLocalLineComment = false; // we are sending entire lines here, so we are neverworried about continued line comments previously starting with "//"
                } // for each thisLine
            } // if sc is not blank
            return isLocalBlockComment;
        }

        public enum VerilogTokenContextType
        {
            Undetermined,
            DoubleQuoteOpen,
            SquareBracketOpen,
            SquareBracketClose,
            SquareBracketContents,
            RoundBracketOpen,
            RoundBracketClose,
            RoundBracketContents,
            SquigglyBracketOpen,
            SquigglyBracketClose,
            SquigglyBracketContents,
            AlwaysAt,
            Comment,
            Text
        }


        private static VerilogTokenContextType VerilogTokenContextFromString(string s)
        {
            switch (s)
            {
                case null:
                    return VerilogTokenContextType.Undetermined;

                case "":
                    return VerilogTokenContextType.Text;

                default:
                    switch (s.Substring(0, 1))
                    {
                        case "[":
                            return VerilogTokenContextType.SquareBracketOpen;

                        case "]":
                            return VerilogTokenContextType.SquareBracketClose;

                        case "(":
                            return VerilogTokenContextType.RoundBracketOpen;

                        case ")":
                            return VerilogTokenContextType.RoundBracketClose;

                        case "{":
                            return VerilogTokenContextType.SquigglyBracketOpen;

                        case "}":
                            return VerilogTokenContextType.SquigglyBracketClose;

                        case "@":
                            return VerilogTokenContextType.AlwaysAt;

                        default:
                            return VerilogTokenContextType.Text;

                    }
            }
        }

        /// <summary>
        ///   VerilogParseState - while processing each segment, we'll keep track of attributes in a VerilogParseState 
        /// </summary>
        public struct VerilogParseState
        {
            public string thisItem;
            public int thisIndex;
            //public int thisSquareBracketDepth;
            //public int thisRoundBracketDepth;
            //public int thisSquigglyBracketDepth;

            public string priorChar;
            public string priorDelimiter;
            public bool thisCharIsDelimiter;
            public bool priorCharIsDelimiter;

            public bool thisCharIsEndingDelimiter;
            public bool priorCharIsIsEndingDelimiter;

            public bool hasOpenSquareBracket;
            public bool hasOpenRoundBracket;
            public bool hasOpenSquigglyBracket;

            public bool hasOpenDoubleQuote;

            public bool IsNewDelimitedSegment;

            private string _thisChar;
            public string thisChar
            {
                get { return _thisChar; }
                set
                {
                    _thisChar = value;
                    thisCharIsDelimiter = IsDelimeter(value);
                    thisCharIsEndingDelimiter = IsEndingDelimeter(value);
                    priorCharIsDelimiter = IsDelimeter(priorChar);
                    IsNewDelimitedSegment = thisCharIsDelimiter || priorCharIsDelimiter;

                    if (IsNewDelimitedSegment)
                    {

                    }
                    else
                    {
                        thisItem += thisChar;
                    }
                }
            }

            public void SetPriorValues()
            {
                // return;
                priorCharIsDelimiter = thisCharIsDelimiter;
                priorCharIsIsEndingDelimiter = thisCharIsEndingDelimiter;

                priorChar = thisChar;
                if (thisCharIsDelimiter)
                {
                    priorDelimiter = thisChar;
                }
            }


            // initialize this VerilogParseState at creation time
            public VerilogParseState(int i)
            {
                _thisChar = "";
                thisIndex = 0;
                thisItem = "";
                IsNewDelimitedSegment = false;
                priorChar = "";
                priorDelimiter = "";
                thisCharIsDelimiter = false;
                priorCharIsDelimiter = false;
                thisCharIsEndingDelimiter = false;
                priorCharIsIsEndingDelimiter = false;
                hasOpenSquareBracket = false;
                hasOpenRoundBracket = false;
                hasOpenSquigglyBracket = false;
                hasOpenDoubleQuote = false;
            }
        }


        /// <summary>
        ///   VerilogToken
        /// </summary>
        public struct VerilogToken
        {
            public VerilogParseState ParseState;
            public string Part;
            public VerilogTokenContextType Context;

            public void SetContext()
            {
                if (ParseState.hasOpenDoubleQuote)
                {
                    Context = VerilogTokenContextType.DoubleQuoteOpen;
                }
                else
                {
                    if (ParseState.hasOpenSquareBracket && !IsDelimeter(ParseState.thisChar))
                    {
                        Context = VerilogTokenContextType.SquareBracketContents;
                    }
                    else
                    {
                        if (ParseState.hasOpenRoundBracket && !IsDelimeter(ParseState.thisChar))
                        {
                            Context = VerilogTokenContextType.RoundBracketContents;
                        }
                        else
                        {
                            if (ParseState.hasOpenRoundBracket && !IsDelimeter(ParseState.thisChar))
                            {
                                Context = VerilogTokenContextType.SquigglyBracketContents;
                            }
                            else
                            {
                                Context = VerilogTokenContextFromString(ParseState.thisChar);
                            }
                        }
                    }
                }


            }


            /// <summary>
            ///   Verilog Token Initializer
            /// </summary>
            /// <param name="p"></param>
            /// <param name="c"></param>
            public VerilogToken(string p = "", VerilogTokenContextType c = VerilogTokenContextType.Undetermined)
            {
                ParseState = new VerilogParseState(0);
                //SquareBracketDepth = 0;
                //RoundBracketDepth = 0;
                //SquigglyBracketDepth = 0; // TODO, pass prior token in parameters and set this fromthose prior values

                Part = p ?? ""; // ensure Part is never null (empty string if p is null)

                if (c == VerilogTokenContextType.Undetermined && Part.Length > 0)
                {
                    Context = VerilogTokenContextFromString(p); // we'll figure out the context from the first character
                }
                else
                {
                    Context = c; // unless otherwise specified
                }
            }
        }

        /// <summary>
        ///    VerilogToken[] 
        /// </summary>
        /// <param name="theString"></param>
        /// <returns></returns>
        public VerilogToken[] VerilogKeywordSplit(string theString, VerilogToken priorToken)
        {
            List<VerilogToken> tokens = new List<VerilogToken>();
            VerilogToken thisToken = new VerilogToken();
            VerilogParseState thisContinuedParseState = new VerilogParseState(0);

            // AddToken - appends the current token part to the array and create a new thisToken to build
            void AddToken()
            {
                string thisItem = thisToken.ParseState.thisItem;
                if (thisItem != "")
                {
                    thisToken.Part = thisToken.ParseState.thisItem;
                    thisContinuedParseState = thisToken.ParseState;
                    tokens.Add(thisToken);
                    thisToken = new VerilogToken(thisToken.ParseState.thisChar);
                    thisToken.ParseState = thisContinuedParseState;
                    thisToken.ParseState.thisItem = thisToken.ParseState.thisChar; // start building a new token with the current, non-delimiter character, will be used to determine context in VerilogTokenContextFromString
                }
            }

            thisToken.ParseState = priorToken.ParseState; // when starting, use the priorToken parseState that wouldhave come from the prior line in the span

            for (int i = 0; i < theString.Length; i++)
            {
                thisToken.ParseState.thisIndex = i;
                thisToken.ParseState.thisChar = theString.Substring(i, 1); // note setting this values triggers ParseState attribute assignments

                if (thisToken.ParseState.IsNewDelimitedSegment)
                {
                    // anytime a delimiter is encountered, we start a new text segment 
                    // note the delimiter itself is in a colorizable segment

                    // there's a new delimiter, so add the current item and prep for the next one
                    AddToken();

                    // once the ParseState is configured (above, when assigning thisChar), set the context of the item
                    thisToken.SetContext(); // TOFO do we really need this? context is alreasy set
                    // at the end of each loop, set the prior values
                    thisToken.ParseState.SetPriorValues();
                } // end of for loop look at each char
            }

            // if there's anythin left, add it is as token (blank token not added)
            AddToken();
            return tokens.ToArray();
        }


        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
        //public event EventHandler<SnapshotSpanEventArgs> TagsChanged
        //{
        //    add { }
        //    remove { }
        //}

        public IEnumerable<ITagSpan<VerilogTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            // bool EditInProgress = spans.snapshot.TextBuffer.EditInProgress;
            // since we can start mid-text, we don't know if the current span is in the middle of a comment
            VerilogGlobals.IsContinuedBlockComment = IsOpenBlockComment(spans); // TODO - does spans always contain the full document? (appears perhaps not)
            VerilogToken[] tokens = null;
            VerilogToken priorToken = new VerilogToken();
            //Boolean HasPriorBrackets = false; // GetPriorBracketCounts(spans, ref priorToken);

            

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

                tokens = VerilogKeywordSplit(containingLine.GetText(), priorToken);

                Boolean IsContinuedLineComment = false; // comments with "//" are only effective for the current line, but /* can span multiple lines
                foreach (VerilogToken VerilogToken in tokens) // this group of tokens in in a single line
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
                        // is this item a comment? If so, color as appropriate. comments take highest priority: no other condition will change color of a comment
                        if (Item.IsComment)
                        {
                            if (tokenSpan.IntersectsWith(curSpan))
                                yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                                                                      new VerilogTokenTag(VerilogTokenTypes.Verilog_Comment));
                        }

                        // otherwise when not a comment, check to see if it is a keyword
                        else
                        {
                            // first check to see if any new variables are being defined;

                            VerilogGlobals.BuildHoverItems(Item.ItemText);


                            // check for standard keyword syntax higlighting
                            if (_VerilogTypes.ContainsKey(Item.ItemText))
                            {
                                if (tokenSpan.IntersectsWith(curSpan))
                                    yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                                                                          new VerilogTokenTag(_VerilogTypes[Item.ItemText]));
                            }

                            else
                            {
                                // check to see if this is a variable
                                if (VerilogGlobals.VerilogVariables.ContainsKey(Item.ItemText))
                                {
                                    yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                                                                          new VerilogTokenTag(VerilogGlobals.VerilogVariables[Item.ItemText]));
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
                                        case VerilogTokenContextType.SquareBracketOpen:
                                        case VerilogTokenContextType.SquareBracketClose:
                                            thisDelimiterTotalDepth = VerilogGlobals.BracketDepth(containingLine.LineNumber, curLoc - containingLine.Start.Position);
                                            thisDelimiterIndex = (thisDelimiterTotalDepth % 5);
                                            if (tokenSpan.IntersectsWith(curSpan))
                                                yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                                                                                      // see _VerilogTypes["bracket_type1"] .. _VerilogTypes["bracket_type5"]
                                                                                      new VerilogTokenTag(_VerilogTypes["bracket_type" + (thisDelimiterIndex).ToString()]));
                                            break;

                                        case VerilogTokenContextType.RoundBracketClose:
                                        case VerilogTokenContextType.RoundBracketOpen:
                                            thisDelimiterTotalDepth = VerilogGlobals.BracketDepth(containingLine.LineNumber, curLoc - containingLine.Start.Position);
                                            thisDelimiterIndex = (thisDelimiterTotalDepth % 5);
                                            if (tokenSpan.IntersectsWith(curSpan))
                                                yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                                                                                      // see _VerilogTypes["bracket_type1"] .. _VerilogTypes["bracket_type5"]
                                                                                      new VerilogTokenTag(_VerilogTypes["bracket_type" + (thisDelimiterIndex).ToString()]));
                                            break;

                                        case VerilogTokenContextType.SquigglyBracketOpen:
                                        case VerilogTokenContextType.SquigglyBracketClose:
                                            thisDelimiterTotalDepth = VerilogGlobals.BracketDepth(containingLine.LineNumber, curLoc - containingLine.Start.Position);
                                            thisDelimiterIndex = (thisDelimiterTotalDepth % 5);
                                            if (tokenSpan.IntersectsWith(curSpan))
                                                yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                                                                                      // see _VerilogTypes["bracket_type1"] .. _VerilogTypes["bracket_type5"]
                                                                                      new VerilogTokenTag(_VerilogTypes["bracket_type" + (thisDelimiterIndex).ToString()]));
                                            break;

                                        case VerilogTokenContextType.SquareBracketContents:
                                            if (tokenSpan.IntersectsWith(curSpan))
                                                yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                                                                                      new VerilogTokenTag(VerilogTokenTypes.Verilog_BracketContent));
                                            break;

                                        case VerilogTokenContextType.AlwaysAt:
                                            if (tokenSpan.IntersectsWith(curSpan))
                                                yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                                                                                      new VerilogTokenTag(VerilogTokenTypes.Verilog_always));
                                            break;

                                        default:
                                            // no highlighting
                                            break;
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

        // public event EventHandler<SnapshotSpanEventArgs> TagsChanged;


        //private void OnTagsChanged(object sender, TagsChangedEventArgs e)
        //{
        //    //var snapshotSpan = e.Span.GetSnapshotSpan();
        //    SnapshotSpan entire = new SnapshotSpan(spans[0].Start, spans[spans.Count - 1].End).TranslateTo(currentSnapshot, SpanTrackingMode.EdgeExclusive);
        //    InvokeTagsChanged(sender, entire);

        //}

        //private void InvokeTagsChanged(object sender, SnapshotSpanEventArgs args)
        //{
        //    TagsChanged?.Invoke(sender, args);
        //}

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
