using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Threading;
using VerilogLanguage.VerilogToken;

namespace VerilogLanguage
{
    public static partial class VerilogGlobals
    {
        private static bool threadActive = false; // true to spawn a new thread when reparsing
        private static ITextBuffer threadbuffer;  // we'll copy the intended buffer to process to this threadBuffer; it will be placed back upon completion
        private static string threadFile = "";    // there may be multiple files open. we'll keep track of them here.
        private const int THREAD_TRIGGER_SIZE = 8192;
        private static DateTime ProfileStart;     // we'll keep track of performance; this is the starting time marker

        // some helpers to keep track of text attributes in our buffer (comments, module names, etc)
        private static bool BufferFirstParseComplete = false;

        public static Dictionary<byte, string> ModuleNames = new Dictionary<byte, string> { // we can have up to 255 modules in a buffer
                                                                                            { 0, "global" } // key = 0 implies global naming
                                                                                        };
        public static Dictionary<string, byte> ModuleKeys = new Dictionary<string, byte> { // we can have up to 255 modules in a buffer
                                                                                            {"global", 0 } // key = 0 implies global naming
                                                                                        };

        public static List<BufferAttribute> BufferAttributes = new List<BufferAttribute>(); // this is the buffer actually used
        private static List<BufferAttribute> editingBufferAttributes = new List<BufferAttribute>(); // buffer being built in a separate thread

        public class BufferAttribute : ICloneable
        {
            public bool IsEmpty;
            private int _Start;
            private int _End;
            private int _LineNumber;

            private int _LineStart;
            private int _LineEnd;
            private bool _IsComment;
            private int _SquareBracketDepth; // TODO limit to byte size
            private int _RoundBracketDepth; // TODO limit to byte size
            private int _SquigglyBracketDepth; // TODO limit to byte size
            private byte _ModuleNameKey; // the ModuleNames byte key value

            #region "Property Implementation"
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

            public byte ModuleNameKey
            {
                get
                {
                    return _ModuleNameKey;
                }
                set
                {
                    _ModuleNameKey = value;
                    IsEmpty = false;
                }
            }
            #endregion


            public BufferAttribute()
            {
                IsEmpty = true;

                _Start = 0;
                _End = 0;

                _LineNumber = 0;
                _LineStart = -1;
                _LineEnd = -1;
                _IsComment = false;
                _SquareBracketDepth = 0;
                _RoundBracketDepth = 0;
                _SquigglyBracketDepth = 0;
                _ModuleNameKey = 0; // 0 = "global"
            }

            public object Clone()
            {
                return this.MemberwiseClone();
            }
        };

        // private static Boolean IsContinuedLineComment = false; // comments with "//" are only effective for the current line, but /* can span multiple lines
        private static VerilogGlobals.VerilogToken[] tokens = null;
        private static VerilogGlobals.VerilogToken priorToken = new VerilogGlobals.VerilogToken();


        /// <summary>
        ///   LineParse
        /// </summary>
        /// <param name="theLine"></param>
        private static void LineParse(string theLine, int theLineNumber)
        {
            // first, parse the words and tokens
            string thisTokenString = "";
            int LinePosition = 0;

            tokens = VerilogGlobals.VerilogKeywordSplit(theLine, priorToken);
            Boolean IsContinuedLineComment = false; // new lines never have a continued line comment; comments with "//" are only effective for the current line, but /* can span multiple lines
            foreach (VerilogGlobals.VerilogToken VerilogToken in tokens) // this group of tokens in in a single line
            {
                // by the time we get here, we might have a tag with adjacent comments:
                //     assign//
                //     //assign
                //     assign//comment
                //     /*assign*/
                //     assign/*comment*/
                thisTokenString = VerilogToken.Part;
                CommentHelper.CommentHelper commentHelper = new CommentHelper.CommentHelper(thisTokenString,
                                                                IsContinuedLineComment,
                                                                VerilogGlobals.IsContinuedBlockComment);
                VerilogGlobals.IsContinuedBlockComment = commentHelper.HasBlockStartComment;
                IsContinuedLineComment = commentHelper.HasOpenLineComment; // we'll use this when processing the VerilogToken item in the commentHelper, above
                foreach (CommentHelper.CommentHelper.CommentItem Item in commentHelper.CommentItems)
                {
                    // TODO - are we actually doing anything with TestComment, or is this just for testing VerilogGlobals.TextIsComment() ??
                    bool TestComment = VerilogGlobals.TextIsComment(theLineNumber, LinePosition);
                    LinePosition += Item.ItemText.Length;

                    // is this item a comment? If so, color as appropriate. comments take highest priority: no other condition will change color of a comment
                    if (Item.IsComment)
                    {
                        // nothing
                    }

                    // otherwise when not a comment, check to see if it is a keyword
                    else
                    {
                        // first check to see if any new variables are being defined;

                        double duration10 = (DateTime.Now - ProfileStart).TotalMilliseconds;
                        VerilogGlobals.BuildHoverItems(Item.ItemText);
                        double duration11 = (DateTime.Now - ProfileStart).TotalMilliseconds;
                    }
                }
            }
        }

        public static int LastReparseVersion = 0;
        public static bool IsReparsing = false;

        public class ThreadReparse
        {
            public static void DoWork()
            {
                if (IsReparsing)
                {
                    // TODO what is this for? does it help with threading? (probably not)
                    Thread.Sleep(50);
                }
                else
                {
                    IsReparsing = true;
                    VerilogGlobals.ReparseWork(threadbuffer, threadFile);
                    // TODO once reparsing is done in a thread, we need to tell the viewport to redraww the screen
                    Thread.Sleep(10);
                }
            }
        }
        /// <summary>
        ///   Reparse
        /// </summary>
        /// <param name="buffer"></param>
        public static void Reparse(ITextBuffer buffer, string forFile = "")
        {
            if (NeedReparse)
            {
                threadbuffer = buffer;
                threadFile = forFile;
                threadActive = (buffer.CurrentSnapshot.Length > THREAD_TRIGGER_SIZE);
                if (threadActive)
                {
                    // Do reparse work as a separate thread
                    Thread thread1 = new Thread(ThreadReparse.DoWork);
                    thread1.Start();
                }
                else
                {
                    // Do blocking reparse work when the files are relatively small
                    ThreadReparse.DoWork();
                }
            }
            // ThreadReparse.DoWork();
        }
        public static void ReparseWork(ITextBuffer buffer, string targetFile)
        {
            System.Diagnostics.Debug.WriteLine("Starting ReparseWork...");
            IsReparsing = true;
            if (buffer == null)
            {
                IsReparsing = false;
                return;
            }
            if (buffer.EditInProgress)
            {
                IsReparsing = false;
                return;
            }
            
            int thisBufferVersion = 0;
            try
            {
                thisBufferVersion = buffer.CurrentSnapshot.Version.VersionNumber;
            }
            catch
            {
                thisBufferVersion = 0;
            }

            // if we could not determine a version ( = 0), or if the last time we reparsed was for this same buffer, then exit
            if ((thisBufferVersion == 0) || (LastReparseVersion == thisBufferVersion))
            {
                IsReparsing = false;
                return;
            }
            //if ((DateTime.Now - ProfileStart).TotalMilliseconds < 1000)
            //{
            //    ProfileStart = DateTime.Now;
            //    return; // never reparse more than once a second
            //}
            ProfileStart = DateTime.Now;
            ITextSnapshot newSnapshot = buffer.CurrentSnapshot;
            string thisChar = "";
            string lastChar = "";
            string thisLine = "";
            bool IsActiveLineComment = false;
            bool IsActiveBlockComment = false;

            int thisLineNumber = 0;
            double duration2;
            double duration3;
            editingBufferAttributes = new List<BufferAttribute>(); // re-initialize the global editingBufferAttributes used for editing
            lock(editingBufferAttributes)
            {
                BufferAttribute bufferAttribute = new BufferAttribute();
                //
                // Reparse AppendBufferAttribute
                // 
                void AppendBufferAttribute()
                {
                    duration2 = (DateTime.Now - ProfileStart).TotalMilliseconds;
                    bufferAttribute.LineNumber = thisLineNumber;

                    // TODO move this to separate function
                    if (thisModuleName != "")
                    {
                        if (!ModuleNames.ContainsValue(thisModuleName))
                        {
                            byte thisNewKey;
                            if (ModuleNames.Count < 256)
                            {
                                thisNewKey = (byte)ModuleNames.Count;
                            }
                            else
                            {
                                // TODO this is actually an error! do something here (popup warning?)
                                thisNewKey = 0; // we'll (incorrectly) assume global space if there are more than 255 modules 
                            }
                            ModuleNames.Add(thisNewKey, thisModuleName);
                            ModuleKeys.Add(thisModuleName, thisNewKey); // build two dictionaries for runtime performance
                            
                            // create placeholder for variables
                            if (!VerilogVariables.ContainsKey(thisModuleName))
                            {
                                VerilogVariables.Add(thisModuleName, new Dictionary<string, VerilogTokenTypes> { });
                            }

                            // ensure VerilogVariableHoverText has a dictionary for [thisModuleName]
                            if (!VerilogVariableHoverText.ContainsKey(thisModuleName))
                            {
                                VerilogVariableHoverText.Add(thisModuleName, new Dictionary<string, string> { });
                            }
                        }
                        bufferAttribute.ModuleNameKey = ModuleKeys[thisModuleName]; // ModuleNames.FirstOrDefault(x => x.Value == thisModuleName).Key; // thanks stackoverflow https://stackoverflow.com/questions/2444033/get-dictionary-key-by-value
                    }

                    editingBufferAttributes.Add(bufferAttribute);
                    bufferAttribute = new BufferAttribute();

                    // set rollover params
                    bufferAttribute.RoundBracketDepth = editingBufferAttributes[editingBufferAttributes.Count - 1].RoundBracketDepth;
                    bufferAttribute.SquareBracketDepth = editingBufferAttributes[editingBufferAttributes.Count - 1].SquareBracketDepth;
                    bufferAttribute.SquigglyBracketDepth = editingBufferAttributes[editingBufferAttributes.Count - 1].SquigglyBracketDepth;
                    bufferAttribute.IsComment = IsActiveBlockComment;
                    bufferAttribute.IsEmpty = true; // although we may have carried over some values, at this point it is still "empty"
                    duration3 = (DateTime.Now - ProfileStart).TotalMilliseconds;
                }

                void CharParse()
                {
                    for (int i = 0; i < thisLine.Length; i++)
                    {
                        thisChar = thisLine.Substring(i, 1);
                        switch (thisChar)
                        {
                            case "[":
                                if (IsActiveLineComment || IsActiveBlockComment)
                                {
                                    // AttributesChanged = false; // if there's an active line comment - nothing changes!
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
                                    // AttributesChanged = false; // if there's an active line comment - nothing changes!
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
                                    // AttributesChanged = false; // if there's an active line comment - nothing changes!
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
                                    // AttributesChanged = false; // if there's an active line comment - nothing changes!
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
                                    // AttributesChanged = false; // if there's an active line comment - nothing changes!
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
                                    // AttributesChanged = false; // if there's an active line comment - nothing changes!
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
                                    if (IsActiveLineComment || IsActiveBlockComment)
                                    {
                                        // AttributesChanged = false; // if there's an active line comment - nothing changes!
                                    }
                                    else
                                    {
                                        bufferAttribute.LineStart = i - 1; // started on prior char
                                                                           // bufferAttribute.LineEnd TBD
                                        IsActiveBlockComment = true;
                                        bufferAttribute.IsComment = true;
                                        AppendBufferAttribute();
                                    }
                                }
                                else
                                {
                                    // AttributesChanged = false;
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
                                        // AttributesChanged = false;
                                    }
                                }
                                else
                                {
                                    // detect line comments "//"
                                    if (lastChar == "/" && !IsActiveLineComment) // encountered first "//" on a line, can only be ended by new line
                                    {
                                        IsActiveLineComment = true;
                                        bufferAttribute.IsComment = true;
                                        bufferAttribute.LineStart = i - 1; // comment actually starts on prior char
                                        bufferAttribute.LineEnd = -1; // a value of -1 means the entire line, regardless of actual length.
                                                                      // AttributesChanged = (i > 1); // the attribute of the line will not change if the first char starts a comment
                                        AppendBufferAttribute();
                                    }
                                    else
                                    {
                                        // AttributesChanged = false;
                                    }
                                }
                                break;

                            default:
                                // we'll keep track of ending string segment that may need to be added below; note if something interesting is found, we'll overwrite these bufferAttribute values, above
                                if (bufferAttribute.LineStart < 0)
                                {
                                    bufferAttribute.LineStart = i; // the first time we end up here, is the start of the string that does not match one of the above special cases
                                }
                                bufferAttribute.LineEnd = i; // keep track of the end.
                                break;
                        }
                        lastChar = thisChar;
                    } // end of for loop looking at each char in line

                }


                VerilogGlobals.InitHoverBuilder();

                double duration4 = (DateTime.Now - ProfileStart).TotalMilliseconds;
                // reminder bufferAttribute is pointing to the contents of the last item in editingBufferAttributes
                foreach (var line in newSnapshot.Lines)
                {
                    //Thread.Sleep(10);
                    thisLine = line.GetText();
                    thisLineNumber = line.LineNumber; // zero-based line numbers

                    // parse the entire line for tokens
                    double duration6 = (DateTime.Now - ProfileStart).TotalMilliseconds;
                    LineParse(thisLine, thisLineNumber);
                    double duration7 = (DateTime.Now - ProfileStart).TotalMilliseconds;

                    // some things, like bracket depth, require us to look at each character...
                    // we'll build a helper table to be able to lookup bracket depth at 
                    // arbitrary points
                    CharParse();
                    double duration8 = (DateTime.Now - ProfileStart).TotalMilliseconds;
                    lastChar = "";  // the lastChar is irrelevant when spanning multiple lines, as we are only using it for comment detection
                    if (!bufferAttribute.IsEmpty)
                    {
                        AppendBufferAttribute();
                    }

                    if (editingBufferAttributes.Count > 0)
                    {
                        // when we reach the end of the line, we reach the end of the line comment!
                        IsActiveLineComment = false;
                    }
                    double duration9 = (DateTime.Now - ProfileStart).TotalMilliseconds;
                    if (!BufferFirstParseComplete)
                    {
                        // TODO - this was supposed to help intial file load of large files, but does not seem to help.
                        BufferAttributes = editingBufferAttributes;
                    }
                } // foreach line

            } // lock editingBufferAttributes

            double duration5 = (DateTime.Now - ProfileStart).TotalMilliseconds;
            // TODO - do we need a final, end-of-file bufferAttribute (probably not)

            // in case we got here from someplace that set NeedReparse to true - reset to indicate completion:
            VerilogGlobals.NeedReparse = false;
            VerilogGlobals.LastParseTime = DateTime.Now;
            VerilogGlobals.LastReparseVersion = thisBufferVersion;
            double duration = (DateTime.Now - ProfileStart).TotalMilliseconds;
            BufferAttributes = editingBufferAttributes;
            BufferFirstParseComplete = true;
            IsReparsing = false;
        } // Reparse

        /// <summary>
        /// TextModuleName return the Verilog module name for the text located AtLine and AtPosition
        /// </summary>
        /// <param name="AtLine"></param>
        /// <param name="AtPosition"></param>
        /// <returns></returns>
        public static string TextModuleName(int AtLine, int AtPosition)
        {
            string res = "global";
            foreach (var thisBufferAttribute in BufferAttributes)
            {
                if ((thisBufferAttribute.LineNumber == AtLine)
//                      && (thisBufferAttribute.LineStart <= AtPosition)
//                     && ((AtPosition <= thisBufferAttribute.LineEnd) || (thisBufferAttribute.LineEnd == -1))
                   )
                {
                    byte thisModuleNameKey = thisBufferAttribute.ModuleNameKey;
                    ModuleNames.TryGetValue(thisModuleNameKey, out res);
                    break; // no need to continue searching on foreach once we have an answer
                }
            }
            return res;
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
            lock(BufferAttributes)
            {
                foreach (var thisBufferAttribute in BufferAttributes)
                {
                    if ((thisBufferAttribute.LineNumber == AtLine)
                          && (thisBufferAttribute.LineStart <= AtPosition)
                          && ((AtPosition <= thisBufferAttribute.LineEnd) || (thisBufferAttribute.LineEnd == -1))
                       )
                    {
                        IsComment = thisBufferAttribute.IsComment;
                        break; // no need to continue searching on foreach once we have an answer
                    }
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
            bool found = false;

            if (BufferAttributes.Count > 0)
            {
                if (BufferAttributes[BufferAttributes.Count - 1].LineNumber >= AtLine)
                {
                    for (int i = 0; i < BufferAttributes.Count - 1; i++)
                    {
                        if (BufferAttributes[i].LineNumber == AtLine)
                        {
                            if (BufferAttributes[i].LineStart == AtPosition)
                            {
                                res = BufferAttributes[i].RoundBracketDepth +
                                      BufferAttributes[i].SquareBracketDepth +
                                      BufferAttributes[i].SquigglyBracketDepth;
                                found = true;
                                break;
                            }
                        }
                    } // for
                } // if target AtLine is less than or equal to the last line number in BufferAttributes
                else
                {
                    // the line number is not even in the BufferAttributes, so don't bother even looking
                }

                // if we didn't find a depth at the explicit line, the depth is at the last known line; 
                // without this, the ending bracket depth is unknown
                if (!found)
                {
                    int LastID = BufferAttributes.Count - 1;
                    res = BufferAttributes[LastID].RoundBracketDepth +
                          BufferAttributes[LastID].SquareBracketDepth +
                          BufferAttributes[LastID].SquigglyBracketDepth;
                } // if !found
            } // BufferAttributes.Count > 0
            else
            {
                // no BufferAttributes, so nothing to do!
            }

            return res;
        }
    }
}
