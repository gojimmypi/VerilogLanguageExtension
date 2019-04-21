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

namespace VerilogLanguage
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.Utilities;

    [Export(typeof(ITaggerProvider))]
    [ContentType("verilog")]
    [TagType(typeof(VerilogTokenTag))]
    internal sealed class VerilogTokenTagProvider : ITaggerProvider
    {

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            return new VerilogTokenTagger(buffer) as ITagger<T>;
        }
    }

    public class VerilogTokenTag : ITag 
    {
        public VerilogTokenTypes type { get; private set; }

        public VerilogTokenTag(VerilogTokenTypes type)
        {
            this.type = type;
        }
    }

    internal sealed class VerilogTokenTagger : ITagger<VerilogTokenTag>
    {

        ITextBuffer _buffer;
        IDictionary<string, VerilogTokenTypes> _VerilogTypes;

        internal VerilogTokenTagger(ITextBuffer buffer)
        {
            _buffer = buffer;
            _VerilogTypes = new Dictionary<string, VerilogTokenTypes>();
            _VerilogTypes["always"] = VerilogTokenTypes.Verilog_always;
            _VerilogTypes["assign"] = VerilogTokenTypes.Verilog_assign;
            _VerilogTypes["automatic"] = VerilogTokenTypes.Verilog_automatic;
            _VerilogTypes["begin"] = VerilogTokenTypes.Verilog_begin;
            _VerilogTypes["case"] = VerilogTokenTypes.Verilog_case;
            _VerilogTypes["casex"] = VerilogTokenTypes.Verilog_casex;
            _VerilogTypes["casez"] = VerilogTokenTypes.Verilog_casez;
            _VerilogTypes["cell"] = VerilogTokenTypes.Verilog_cell;
            _VerilogTypes["config"] = VerilogTokenTypes.Verilog_config;
            _VerilogTypes["deassign"] = VerilogTokenTypes.Verilog_deassign;
            _VerilogTypes["default"] = VerilogTokenTypes.Verilog_default;
            _VerilogTypes["defparam"] = VerilogTokenTypes.Verilog_defparam;
            _VerilogTypes["design"] = VerilogTokenTypes.Verilog_design;
            _VerilogTypes["disable"] = VerilogTokenTypes.Verilog_disable;
            _VerilogTypes["edge"] = VerilogTokenTypes.Verilog_edge;
            _VerilogTypes["else"] = VerilogTokenTypes.Verilog_else;
            _VerilogTypes["end"] = VerilogTokenTypes.Verilog_end;
            _VerilogTypes["endcase"] = VerilogTokenTypes.Verilog_endcase;
            _VerilogTypes["endconfig"] = VerilogTokenTypes.Verilog_endconfig;
            _VerilogTypes["endfunction"] = VerilogTokenTypes.Verilog_endfunction;
            _VerilogTypes["endgenerate"] = VerilogTokenTypes.Verilog_endgenerate;
            _VerilogTypes["endmodule"] = VerilogTokenTypes.Verilog_endmodule;
            _VerilogTypes["endprimitive"] = VerilogTokenTypes.Verilog_endprimitive;
            _VerilogTypes["endspecify"] = VerilogTokenTypes.Verilog_endspecify;
            _VerilogTypes["endtable"] = VerilogTokenTypes.Verilog_endtable;
            _VerilogTypes["endtask"] = VerilogTokenTypes.Verilog_endtask;
            _VerilogTypes["event"] = VerilogTokenTypes.Verilog_event;
            _VerilogTypes["for"] = VerilogTokenTypes.Verilog_for;
            _VerilogTypes["force"] = VerilogTokenTypes.Verilog_force;
            _VerilogTypes["forever"] = VerilogTokenTypes.Verilog_forever;
            _VerilogTypes["fork"] = VerilogTokenTypes.Verilog_fork;
            _VerilogTypes["function"] = VerilogTokenTypes.Verilog_function;
            _VerilogTypes["generate"] = VerilogTokenTypes.Verilog_generate;
            _VerilogTypes["genvar"] = VerilogTokenTypes.Verilog_genvar;
            _VerilogTypes["if"] = VerilogTokenTypes.Verilog_if;
            _VerilogTypes["ifnone"] = VerilogTokenTypes.Verilog_ifnone;
            _VerilogTypes["incdir"] = VerilogTokenTypes.Verilog_incdir;
            _VerilogTypes["include"] = VerilogTokenTypes.Verilog_include;
            _VerilogTypes["initial"] = VerilogTokenTypes.Verilog_initial;
            _VerilogTypes["inout"] = VerilogTokenTypes.Verilog_inout;
            _VerilogTypes["input"] = VerilogTokenTypes.Verilog_input;
            _VerilogTypes["instance"] = VerilogTokenTypes.Verilog_instance;
            _VerilogTypes["join"] = VerilogTokenTypes.Verilog_join;
            _VerilogTypes["liblist"] = VerilogTokenTypes.Verilog_liblist;
            _VerilogTypes["library"] = VerilogTokenTypes.Verilog_library;
            _VerilogTypes["localparam"] = VerilogTokenTypes.Verilog_localparam;
            _VerilogTypes["macromodule"] = VerilogTokenTypes.Verilog_macromodule;
            _VerilogTypes["module"] = VerilogTokenTypes.Verilog_module;
            _VerilogTypes["negedge"] = VerilogTokenTypes.Verilog_negedge;
            _VerilogTypes["noshowcancelled"] = VerilogTokenTypes.Verilog_noshowcancelled;
            _VerilogTypes["output"] = VerilogTokenTypes.Verilog_output;
            _VerilogTypes["parameter"] = VerilogTokenTypes.Verilog_parameter;
            _VerilogTypes["posedge"] = VerilogTokenTypes.Verilog_posedge;
            _VerilogTypes["primitive"] = VerilogTokenTypes.Verilog_primitive;
            _VerilogTypes["pulsestyle_ondetect"] = VerilogTokenTypes.Verilog_pulsestyle_ondetect;
            _VerilogTypes["pulsestyle_onevent"] = VerilogTokenTypes.Verilog_pulsestyle_onevent;
            _VerilogTypes["reg"] = VerilogTokenTypes.Verilog_reg;
            _VerilogTypes["release"] = VerilogTokenTypes.Verilog_release;
            _VerilogTypes["repeat"] = VerilogTokenTypes.Verilog_repeat;
            _VerilogTypes["scalared"] = VerilogTokenTypes.Verilog_scalared;
            _VerilogTypes["showcancelled"] = VerilogTokenTypes.Verilog_showcancelled;
            _VerilogTypes["signed"] = VerilogTokenTypes.Verilog_signed;
            _VerilogTypes["specify"] = VerilogTokenTypes.Verilog_specify;
            _VerilogTypes["specparam"] = VerilogTokenTypes.Verilog_specparam;
            _VerilogTypes["strength"] = VerilogTokenTypes.Verilog_strength;
            _VerilogTypes["table"] = VerilogTokenTypes.Verilog_table;
            _VerilogTypes["task"] = VerilogTokenTypes.Verilog_task;
            _VerilogTypes["tri"] = VerilogTokenTypes.Verilog_tri;
            _VerilogTypes["tri0"] = VerilogTokenTypes.Verilog_tri0;
            _VerilogTypes["tri1"] = VerilogTokenTypes.Verilog_tri1;
            _VerilogTypes["triand"] = VerilogTokenTypes.Verilog_triand;
            _VerilogTypes["wand"] = VerilogTokenTypes.Verilog_wand;
            _VerilogTypes["trior"] = VerilogTokenTypes.Verilog_trior;
            _VerilogTypes["wor"] = VerilogTokenTypes.Verilog_wor;
            _VerilogTypes["trireg"] = VerilogTokenTypes.Verilog_trireg;
            _VerilogTypes["unsigned"] = VerilogTokenTypes.Verilog_unsigned;
            _VerilogTypes["use"] = VerilogTokenTypes.Verilog_use;
            _VerilogTypes["vectored"] = VerilogTokenTypes.Verilog_vectored;
            _VerilogTypes["wait"] = VerilogTokenTypes.Verilog_wait;
            _VerilogTypes["while"] = VerilogTokenTypes.Verilog_while;
            _VerilogTypes["wire"] = VerilogTokenTypes.Verilog_wire;

            // all of the Verilog directives are the same color
            _VerilogTypes["`celldefine"] = VerilogTokenTypes.Verilog_Directive;
            _VerilogTypes["`endcelldefine"] = VerilogTokenTypes.Verilog_Directive;
            _VerilogTypes["`default_nettype"] = VerilogTokenTypes.Verilog_Directive;
            _VerilogTypes["`define"] = VerilogTokenTypes.Verilog_Directive;
            _VerilogTypes["`undef"] = VerilogTokenTypes.Verilog_Directive;
            _VerilogTypes["`ifdef"] = VerilogTokenTypes.Verilog_Directive;
            _VerilogTypes["`ifndef"] = VerilogTokenTypes.Verilog_Directive;
            _VerilogTypes["`elsif"] = VerilogTokenTypes.Verilog_Directive;
            _VerilogTypes["`else"] = VerilogTokenTypes.Verilog_Directive;
            _VerilogTypes["`endif"] = VerilogTokenTypes.Verilog_Directive;
            _VerilogTypes["`include"] = VerilogTokenTypes.Verilog_Directive;
            _VerilogTypes["`resetall"] = VerilogTokenTypes.Verilog_Directive;
            _VerilogTypes["`line"] = VerilogTokenTypes.Verilog_Directive;
            _VerilogTypes["`timescale"] = VerilogTokenTypes.Verilog_Directive;
            _VerilogTypes["`unconnected_drive"] = VerilogTokenTypes.Verilog_Directive;
            _VerilogTypes["`nounconnected_driv"] = VerilogTokenTypes.Verilog_Directive;

            _VerilogTypes["//"] = VerilogTokenTypes.Verilog_Comment;

        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged
        {
            add { }
            remove { }
        }

        // we don't fully analyze the string, just check to see if there's a line or block comment
        class CommentHelper
        {
            readonly string thisLine = "";
            private readonly int posLineComment = -1; // position of first line comment
            private readonly int posBlockStartComment = -1;
            private readonly int posBlockEndComment = -1;

            public bool IsMinimumSize  
            {
                get
                {
                    // strings need to be at least 2 characters long to be /*, //, or */
                    return (thisLine != null) && (thisLine.Length >= 2);
                }
            }

            // public bool HasLineComment { get; } = false;

            public bool HasBlockStartComment { get; } = false;

            public bool HasBlockEndComment { get; } = false;

            public bool HasOpenLineComment { get; } = false;

            // public int NonCommentLength { get; } = -1;

            public class CommentItem
            {
                public string ItemText { get; }
                public bool IsComment { get; }
                public CommentItem(string itemtext, bool iscomment)
                {
                    this.ItemText = itemtext;
                    this.IsComment = iscomment;
                }
            }

            public List<CommentItem> CommentItems { get; }

            // init our CommentHelper
            public CommentHelper(string item, bool IsContinuedLineComment, bool IsContinuedBlockComment)
            {
                // item should be a single line of text, with no CR/LF
                thisLine = item;
                CommentItems = new List<CommentItem>();
                this.HasOpenLineComment = IsContinuedLineComment;// we may be string with a string (or tag) on a line after a "//", set to be re-used again
                if (IsContinuedLineComment)
                {
                    CommentItems.Add(new CommentItem(item, true)); // if we start knowing that this is a continuation of a line comment, everthing is still part of that comment
                    return;
                }

                posLineComment = thisLine.IndexOf("//");
                // NonCommentLength = thisLine.Length; // we'll consider the entire string, unless line comment tag found
                posBlockStartComment = thisLine.IndexOf("/*");
                posBlockEndComment = thisLine.IndexOf("*/");
                HasBlockStartComment = (posBlockStartComment > -1) || IsContinuedBlockComment;
                HasBlockEndComment = (posBlockEndComment > -1);
                HasOpenLineComment = IsContinuedLineComment || (posLineComment > -1);

                // there's only something to do when we find starting or ending block comment tags
                if (HasBlockStartComment || HasBlockEndComment )
                {
                    if (HasOpenLineComment && (posBlockStartComment > posLineComment))
                    {
                        posBlockStartComment = -1; // we are not interested in any starting block comments after a line comment tag
                        HasBlockStartComment = false;
                    }

                    if (HasOpenLineComment && (posBlockEndComment > posLineComment))
                    {
                        posBlockEndComment = -1; // we are not interested in any ending block comments after a line comment tag
                        HasBlockEndComment = false;
                    }

                    string thisCommentBlock = "";
                    string thisNonCommentBlock = "";
                    string previousChar = "";
                    string thisChar = "";
                    string nextChar = "";
                    if (this.IsMinimumSize && (this.HasBlockStartComment || this.HasOpenLineComment))
                    {
                        string thisTag = "";
                        for (int i = 0; i <= this.thisLine.Length - 1; i++)
                        {
                            thisTag = "";
                            nextChar = "";
                            thisChar = thisLine.Substring(i, 1);
                            if (i < this.thisLine.Length - 1)
                            {
                                nextChar = thisLine.Substring(i + 1, 1);
                            }
                            thisTag = thisChar + nextChar;
                            //if (i <= this.thisLine.Length - 2)
                            //{
                            //    thisTag = thisLine.Substring(i, 2);
                            //}

                            if (thisTag == "//")
                            {
                                if (HasBlockStartComment)
                                {
                                    // nothing to do, this "//" is inside a block comment
                                }
                                else
                                {
                                    HasOpenLineComment = true;
                                    if (thisNonCommentBlock != "")
                                    {
                                        CommentItems.Add(new CommentItem(thisNonCommentBlock, false));
                                        thisNonCommentBlock = "";
                                    }
                                }
                                //thisCommentBlock += thisTag;
                                //i++;
                            }
                            else if (thisTag == "/*")
                            {
                                if (thisNonCommentBlock != "") 
                                {
                                    CommentItems.Add(new CommentItem(thisNonCommentBlock, false));
                                    thisNonCommentBlock = "";
                                }
                                if (!HasOpenLineComment)
                                {
                                    HasBlockStartComment = true;
                                }
                            }
                            else if (thisTag == "*/")
                            {
                                if (HasBlockStartComment && !HasOpenLineComment)
                                {
                                    thisCommentBlock += thisChar; i++;
                                    thisCommentBlock += nextChar; i++; 
                                    CommentItems.Add(new CommentItem(thisCommentBlock, true));
                                    thisCommentBlock = "";
                                    HasBlockStartComment = false;
                                }
                                else
                                {
                                    // closing block comment found without opening, so it is not a comment
                                }
                                HasBlockStartComment = false;
                            }

                            // we may have incremented above, so ensure we are still inside the string
                            if (i < this.thisLine.Length)
                            {
                                if (HasBlockStartComment || HasOpenLineComment)
                                {
                                    thisCommentBlock += thisLine.Substring(i, 1);
                                }
                                else
                                {
                                    thisNonCommentBlock += thisLine.Substring(i, 1);
                                }
                            }
                        } // end of for loop checking each char

                        // add any outstanding comment text to our list
                        if (thisCommentBlock != "")
                        {
                            CommentItems.Add(new CommentItem(thisCommentBlock, true));
                            thisCommentBlock = "";
                        }

                        // add any outstanding regaular, non-comment text to our list
                        if (thisNonCommentBlock != "")
                        {
                            CommentItems.Add(new CommentItem(thisNonCommentBlock, false));
                            thisNonCommentBlock = "";
                        }
                    } // end if we had a comment block start or open line comment
                }
                else
                {
                    // if we didn't have incoming active comment, and didn't find an opening,
                    // then we don't have a comment to consider, so the entire item is not a comment
                    CommentItems.Add(new CommentItem( item, false ));
                }
            } // CommentHelper class initializer
        } // CommentHelper class


        //=====================================================================================
        // TODO - do spans always start on new lines? If not, we'll need an IsOpenLineComment
        private bool IsOpenBlockComment(NormalizedSnapshotSpanCollection sc) // are we starting with prior text that is already an opening comment block?
        {
            bool isLocalBlockComment = false; // we'll assume there's no open block comment unless otherwise found
            bool isLocalLineComment = false;
            if (sc != null && sc[0].Snapshot != null && sc[0].Start.Position > 0)
            {
                int ToPosition = sc[0].Start.Position - 1; // we are only interesdted in text priot to our current location
                // SnapshotSpan PriorText = sc[0].Snapshot(0, ToPosition);
                foreach (ITextSnapshotLine thisLine in sc[0].Snapshot.Lines){
                    int pos = thisLine.Start.Position;
                    if (pos > ToPosition)
                    {
                        break; // nothing to do if the starting position is beyond our starting point, as we are only interested in prior open block
                    }
                    CommentHelper commentHelper = new CommentHelper(thisLine.GetText(), isLocalLineComment,  isLocalBlockComment); // we are starting at the beginning of a string, so there's of course no prior "//" continued line comment
                    isLocalBlockComment = commentHelper.HasBlockStartComment;
                    isLocalLineComment = false; // we are sending entire lines here, so we are neverworried about continued line comments previously starting with "//"

                    #region OldCode
                    //// quit as early as possible for performance.  nothing to do if the line is less than 2 chars long
                    //if ((thisLine != null) && (thisLine.GetText().Length > 2) && (pos < ToPosition))
                    //{
                    //    int posBlockComment = thisLine.GetText().IndexOf("/*");
                    //    int posBlockEndComment = thisLine.GetText().IndexOf("*/");
                    //    bool hasBlockComment = (posBlockComment > -1);
                    //    bool hasBlockEndComment = (posBlockEndComment > -1);

                    //    // there's only something to do when we find starting or ending block comment tags
                    //    if (hasBlockComment || hasBlockEndComment)
                    //    {
                    //        // block comment tag text found after line comments are not considered
                    //        int posLineComment = thisLine.GetText().IndexOf("//");

                    //        int maxLen = thisLine.GetText().Length; // we'll consider the entire string, unless line comment tag found
                    //        if (posLineComment > -1)
                    //        {
                    //            maxLen = posLineComment; // we've never interested in chars beyond line comment tag
                    //        }
                    //        bool hasLineComment = (posLineComment > -1);
                    //        if (hasLineComment && (posBlockComment > posLineComment))
                    //        {
                    //            posBlockComment = -1; // we are not interested in any starting block comments after a line comment tag
                    //            hasBlockComment = false;
                    //        }
                    //        if (hasLineComment && (posBlockEndComment > posLineComment))
                    //        {
                    //            posBlockEndComment = -1; // we are not interested in any ending block comments after a line comment tag
                    //            hasBlockEndComment = false;
                    //        }


                    //        // we'll still need to determine effective comment blocks, but 
                    //        // let'ssee if we even have an interesting starting or ending tag
                    //        //bool hasBlockComment = (!hasLineComment && (posBlockComment > -1)) // no line comment, and beging block comment found
                    //        //                             ||
                    //        //                       (hasLineComment && (posBlockComment > -1) && (posBlockComment < posLineComment)); // has line comment, block comment found before it

                    //        //bool hasBlockEndComment = (!hasLineComment && (posBlockEndComment > -1))
                    //        //                             ||
                    //        //                          (hasLineComment && (posBlockEndComment > -1) && (posBlockEndComment < posLineComment));

                    //        // re-check if we still have interesting block comment tags after considering those that may have been found after line comment tag
                    //        if (hasBlockComment || hasBlockEndComment) // we only have something to do here if starting or ending block comment tags are found
                    //        {
                    //            for (int i = 0; i < maxLen -2; i++)
                    //            {
                    //                string thisTag = thisLine.GetText().Substring(i, 2);
                    //                if (thisTag == "/*") {
                    //                    isLocalBlockComment = true;
                    //                }
                    //                if (thisTag == "*/")
                    //                {
                    //                    isLocalBlockComment = false;
                    //                }
                    //            }
                    //        }

                    //    }

                    //}
                    #endregion
                }
            }
            return isLocalBlockComment;
        }

        public IEnumerable<ITagSpan<VerilogTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {

            // since we can start mid-text, we don't know if the current span is in the middle of a comment
            Boolean IsContinuedBlockComment = IsOpenBlockComment(spans);

            foreach (SnapshotSpan curSpan in spans)
            {
                ITextSnapshotLine containingLine = curSpan.Start.GetContainingLine();
                int curLoc = containingLine.Start.Position;
                string[] tokens = containingLine.GetText().ToLower().Split(separator: new char[] { ' ', '\t', '[', ';' }, 
                                                                             options: StringSplitOptions.None);

                Boolean IsContinuedLineComment = false; // comments with "//" are only effective forthe currentl line
                foreach (string VerilogToken in tokens)
                {
                    // by the time we get here, we might have a tag with adjacent comments:
                    //     assign//
                    //     //assign
                    //     assign//comment
                    //     /*assign*/
                    //     assign/*comment*/
                    CommentHelper commentHelper = new CommentHelper(VerilogToken, IsContinuedLineComment, IsContinuedBlockComment);
                    IsContinuedBlockComment = commentHelper.HasBlockStartComment;
                    foreach (CommentHelper.CommentItem Item in commentHelper.CommentItems)
                    {
                        var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc, Item.ItemText.Length));
                        if (Item.IsComment)
                        {
                            if (tokenSpan.IntersectsWith(curSpan))
                                yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                                                                      new VerilogTokenTag(_VerilogTypes["//"]));
                        }
                        else
                        {
                            if (_VerilogTypes.ContainsKey(Item.ItemText))
                            {
                                if (tokenSpan.IntersectsWith(curSpan))
                                    yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                                                                          new VerilogTokenTag(_VerilogTypes[Item.ItemText]));
                            }
                            else
                            {
                                // no tag colorization
                            }
                        }
                        curLoc += Item.ItemText.Length;

                    }


                    #region old code
                    //if ((VerilogToken != null) && (VerilogToken.Length >= 2))
                    //{
                    //    int posBlockComment = VerilogToken.IndexOf("/*");
                    //    int posBlockEndComment = VerilogToken.IndexOf("*/");
                    //    bool hasBlockComment = (posBlockComment > -1);
                    //    bool hasBlockEndComment = (posBlockEndComment > -1);
                    //
                    //}
                    //
                    //if (VerilogToken.Length >= 2)
                    //{
                    //    if (VerilogToken.Substring(0, 2) == "//")
                    //    {
                    //        IsContinuedLineComment = true;
                    //    }
                    //
                    //    if (!IsContinuedLineComment && VerilogToken.Substring(0, 2) == "/*")
                    //    {
                    //        IsContinuedBlockComment = true;
                    //    }
                    //}
                    #endregion

                    //if (IsContinuedLineComment || IsContinuedBlockComment)
                    //{
                    //    var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc, VerilogToken.Length));
                    //    if (tokenSpan.IntersectsWith(curSpan))
                    //        yield return new TagSpan<VerilogTokenTag>(tokenSpan,
                    //                                              new VerilogTokenTag(_VerilogTypes["//"]));
                    //}

                    //else // no start comment delimiter, colorize as usual
                    //{
                    //}

                    //if ( (VerilogToken.Length >=2) && VerilogToken.Substring(VerilogToken.Length-2,2) == "*/")
                    //{
                    //    // when we find a "*/" this is the end of the block comment, 
                    //    // the next toekn will not be considered part of comments
                    //    IsContinuedBlockComment = false;
                    //}

                    //add an extra char location because of the delimiter
                    curLoc +=  + 1;
                }
            }
            
        }
    }
}
