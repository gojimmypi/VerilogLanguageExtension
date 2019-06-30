using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using CommentHelper;

namespace VerilogLanguage
{
    public static partial class VerilogGlobals
    {
        public class BufferAttribute : ICloneable
        {
            public bool IsEmpty;
            private int _Start;
            private int _End;
            private int _LineNumber;

            private int _LineStart;
            private int _LineEnd;
            private bool _IsComment;
            private int _SquareBracketDepth;
            private int _RoundBracketDepth;
            private int _SquigglyBracketDepth;

            public int Start
            {
                get
                {
                    return _Start;
                }
                set
                {
                    _Start = value;
                    IsEmpty = false;
                }
            }

            public int End
            {
                get
                {
                    return _End;
                }
                set
                {
                    _End = value;
                    IsEmpty = false;
                }
            }

            public int LineNumber
            {
                get
                {
                    return _LineNumber;
                }
                set
                {
                    _LineNumber = value;
                    IsEmpty = false;
                }
            }

            public int LineStart
            {
                get
                {
                    return _LineStart;
                }
                set
                {
                    _LineStart = value;
                    IsEmpty = false;
                }
            }

            public int LineEnd
            {
                get
                {
                    return _LineEnd;
                }
                set
                {
                    _LineEnd = value;
                    IsEmpty = false;
                }
            }

            public bool IsComment
            {
                get
                {
                    return _IsComment;
                }
                set
                {
                    _IsComment = value;
                    IsEmpty = false;
                }
            }

            public int SquareBracketDepth
            {
                get
                {
                    return _SquareBracketDepth;
                }
                set
                {
                    _SquareBracketDepth = value;
                    IsEmpty = false;
                }
            }

            public int RoundBracketDepth
            {
                get
                {
                    return _RoundBracketDepth;
                }
                set
                {
                    _RoundBracketDepth = value;
                    IsEmpty = false;
                }
            }

            public int SquigglyBracketDepth
            {
                get
                {
                    return _SquigglyBracketDepth;
                }
                set
                {
                    _SquigglyBracketDepth = value;
                    IsEmpty = false;
                }
            }


            public BufferAttribute()
            {
                IsEmpty = true;

                _Start = 0;
                _End = 0;

                _LineNumber = 0;
                _LineStart = 0;
                _LineEnd = 0;
                _IsComment = false;
                _SquareBracketDepth = 0;
                _RoundBracketDepth = 0;
                _SquigglyBracketDepth = 0;
            }

            public object Clone()
            {
                return this.MemberwiseClone();
            }
        };

        public static List<BufferAttribute> BufferAttributes = new List<BufferAttribute>();

        public static void Reparse(ITextBuffer buffer)
        {
            ITextSnapshot newSnapshot = buffer.CurrentSnapshot;
            //List<Region> newRegions = new List<Region>();

            ////keep the current (deepest) partial region, which will have
            //// references to any parent partial regions.
            //PartialRegion currentRegion = null;
            string thisChar = "";
            string lastChar = "";
            string thisLine = "";
            bool AttributesChanged = false;
            bool IsActiveLineComment = false;
            bool IsActiveBlockComment = false;
            //int thisLineStart = 0;
            int thisLineNumber = 0;

            BufferAttributes = new List<BufferAttribute>(); // re-initialize the global BufferAttributes
            BufferAttribute bufferAttribute = new BufferAttribute();

            void AppendBufferAttribute()
            {
                bufferAttribute.LineNumber = thisLineNumber;
                BufferAttributes.Add(bufferAttribute);
                // bufferAttribute = new BufferAttribute();
                // bufferAttribute = (VerilogGlobals.BufferAttribute)BufferAttributes[BufferAttributes.Count - 1].Clone();
                bufferAttribute = new BufferAttribute();
                // set rollover params
                bufferAttribute.RoundBracketDepth = BufferAttributes[BufferAttributes.Count - 1].RoundBracketDepth;
                bufferAttribute.SquareBracketDepth = BufferAttributes[BufferAttributes.Count - 1].SquareBracketDepth;
                bufferAttribute.SquigglyBracketDepth = BufferAttributes[BufferAttributes.Count - 1].SquigglyBracketDepth;
                bufferAttribute.IsComment = IsActiveBlockComment;
            }

            // reminder bufferAttribute is pointing to the contents of the last item in BufferAttributes
            foreach (var line in newSnapshot.Lines)
            {
                thisLine = line.GetText();
                thisLineNumber = line.LineNumber;

                for (int i = 0; i < thisLine.Length; i++)
                {
                    AttributesChanged = true;

                    thisChar = thisLine.Substring(i, 1);
                    switch (thisChar)
                    {
                        case "[":
                            if (IsActiveLineComment || IsActiveBlockComment)
                            {
                                AttributesChanged = false; // if there's an active line comment - nothing changes!
                            }
                            else
                            {
                                bufferAttribute.SquareBracketDepth++;
                                bufferAttribute.LineStart = i; // brackets are only 1 char long, starting
                                bufferAttribute.LineEnd = i;   // and ending at the same positions
                                AppendBufferAttribute();
                            }
                            break;

                        case "]":
                            if (IsActiveLineComment || IsActiveBlockComment)
                            {
                                AttributesChanged = false; // if there's an active line comment - nothing changes!
                            }
                            else
                            {
                                bufferAttribute.LineStart = i; // brackets are only 1 char long, starting
                                bufferAttribute.LineEnd = i;   // and ending at the same positions
                                AppendBufferAttribute();
                                bufferAttribute.SquareBracketDepth = (bufferAttribute.SquareBracketDepth > 0) ? (--bufferAttribute.SquareBracketDepth) : 0;
                            }
                            break;

                        case "(":
                            if (IsActiveLineComment || IsActiveBlockComment)
                            {
                                AttributesChanged = false; // if there's an active line comment - nothing changes!
                            }
                            else
                            {
                                bufferAttribute.RoundBracketDepth++;
                                bufferAttribute.LineStart = i; // brackets are only 1 char long, starting
                                bufferAttribute.LineEnd = i;   // and ending at the same positions
                                AppendBufferAttribute();
                            }
                            break;

                        case ")":
                            if (IsActiveLineComment || IsActiveBlockComment)
                            {
                                AttributesChanged = false; // if there's an active line comment - nothing changes!
                            }
                            else
                            {
                                bufferAttribute.LineStart = i; // brackets are only 1 char long, starting
                                bufferAttribute.LineEnd = i;   // and ending at the same positions
                                AppendBufferAttribute();
                                bufferAttribute.RoundBracketDepth = (bufferAttribute.RoundBracketDepth > 0) ? (--bufferAttribute.RoundBracketDepth) : 0;
                            }
                            break;

                        case "{":
                            if (IsActiveLineComment || IsActiveBlockComment)
                            {
                                AttributesChanged = false; // if there's an active line comment - nothing changes!
                            }
                            else
                            {
                                bufferAttribute.SquigglyBracketDepth++;
                                bufferAttribute.LineStart = i; // brackets are only 1 char long, starting
                                bufferAttribute.LineEnd = i;   // and ending at the same positions
                                AppendBufferAttribute();
                            }
                            break;

                        case "}":
                            if (IsActiveLineComment || IsActiveBlockComment)
                            {
                                AttributesChanged = false; // if there's an active line comment - nothing changes!
                            }
                            else
                            {
                                bufferAttribute.LineStart = i; // brackets are only 1 char long, starting
                                bufferAttribute.LineEnd = i;   // and ending at the same positions
                                AppendBufferAttribute();
                                bufferAttribute.SquigglyBracketDepth = (bufferAttribute.SquigglyBracketDepth > 0) ? (--bufferAttribute.SquigglyBracketDepth) : 0;
                            }
                            break;

                        case "*":
                            // encountered "/*"
                            if (lastChar == "/")
                            {
                                //bool changing = TheBuffer.CheckEditAccess();

                                if (IsActiveLineComment || IsActiveBlockComment)
                                {
                                    AttributesChanged = false; // if there's an active line comment - nothing changes!
                                }
                                else
                                {
                                    bufferAttribute.LineStart = i - 1; // started on prior char
                                    // bufferAttribute.LineEnd TBD
                                    IsActiveBlockComment = true;
                                    bufferAttribute.IsComment = true;
                                }
                            }
                            else
                            {
                                AttributesChanged = false;
                            }
                            break;

                        case "/":
                            // check for block comment end "*/"
                            if (lastChar == "*")
                            {
                                if (!IsActiveLineComment)
                                {
                                    IsActiveBlockComment = false;
                                    bufferAttribute.LineEnd = i; //
                                    bufferAttribute.IsComment = false;
                                    AppendBufferAttribute();
                                }
                                else
                                {
                                    AttributesChanged = false;
                                }
                            }
                            else
                            {
                                // detect line comments "//"
                                if (lastChar == "/" && !IsActiveLineComment) // encountered first "//" on a line
                                {
                                    IsActiveLineComment = true;
                                    bufferAttribute.IsComment = true;
                                    bufferAttribute.LineStart = i - 1; // comment actually starts on prior char
                                    bufferAttribute.Start = i - 1; // we actually start the comment on the prior char
                                    AttributesChanged = (i > 1); // the attribute of the line will not change if the first char starts a comment
                                }
                                else
                                {
                                    AttributesChanged = false;
                                }
                            }
                            break;

                        default:
                            AttributesChanged = false;
                            break;
                    }
                    if (AttributesChanged)
                    {
                        // the last item currently in the list has end ending line position set to this line starting position
                        // BufferAttributes[BufferAttributes.Count - 1].LineEnd = bufferAttribute.LineStart;
                        // BufferAttributes.Add(bufferAttribute);
                        // bufferAttribute = new BufferAttribute();
                        // bufferAttribute = (VerilogGlobals.BufferAttribute)BufferAttributes[BufferAttributes.Count - 1].Clone();
                    }


                    // bufferAttribute.Start++;

                    lastChar = thisChar;
                } // end of for loop looking at each char in line

                lastChar = "";  // the lastChar is irrelevant when spanning multiple lines, as we are only using it for comment detection
                if (!bufferAttribute.IsEmpty)
                {
                    AppendBufferAttribute();
                }

                if (BufferAttributes.Count > 0)
                {
                    if (BufferAttributes[BufferAttributes.Count - 1].LineEnd == 0)
                    {
                        // BufferAttributes[BufferAttributes.Count - 1].LineEnd = thisLine.Length - 1;
                    }

                    IsActiveLineComment = false;

                    // BufferAttributes[BufferAttributes.Count - 1].End = bufferAttribute.Start;
                }
                //bufferAttribute.IsComment = IsActiveLineComment || IsActiveBlockComment;
            }
        }

        /// <summary>
        ///     TextIsComment - is the text on line [AtLine] starting at position [AtPosition] a comment?
        /// </summary>
        /// <param name="AtLine"></param>
        /// <param name="AtPosition"></param>
        /// <returns></returns>
        public static bool TextIsComment(int AtLine, int AtPosition)
        {
            bool IsComment = false;
            //BufferAttribute LastBufferAttribute;
            foreach (var thisBufferAttribute in BufferAttributes)
            {
                if (thisBufferAttribute.LineNumber == AtLine)
                {

                }
            }
            return IsComment;
        }

        /// <summary>
        ///   BracketDepth - parse though the BufferAttributes and find the total bracket depth on line [AtLine], column [AtPosition]. zero based
        /// </summary>
        /// <param name="AtLine"></param>
        /// <param name="AtPosition"></param>
        /// <returns></returns>
        public static int BracketDepth(int AtLine, int AtPosition)
        {
            int res = 0;
            for (int i = 0; i < BufferAttributes.Count - 1; i++)
            {
                if (BufferAttributes[i].LineNumber == AtLine)
                {
                    if (BufferAttributes[i].LineStart == AtPosition)
                    {
                        res = BufferAttributes[i].RoundBracketDepth +
                              BufferAttributes[i].SquareBracketDepth +
                              BufferAttributes[i].SquigglyBracketDepth;
                        break;
                    }
                }
            }
            return res;
        }
    }
}
