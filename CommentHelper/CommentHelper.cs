using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommentHelper
{
    // we don't fully analyze the string, just check to see if there's a line or block comment
    class CommentHelper
    {
        readonly string thisLine = "";
        private int posLineComment = -1; // position of first line comment
        private int posBlockStartComment = -1;
        private int posBlockEndComment = -1;
        private string thisCommentBlock = "";
        private string thisNonCommentBlock = "";

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

        private bool HasBlockEndComment { get; } = false; // for internal use only

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

        private void AppendBlockChar(string thisChar)
        {
            // depending on the logic above, append the current character 
            if (HasBlockStartComment || HasOpenLineComment)
            {
                thisCommentBlock += thisChar;
            }
            else
            {
                thisNonCommentBlock += thisChar;
            }
        }

        private void AppendCommentListItem(string AdditionalCommentBlock = "")
        {
            thisCommentBlock += AdditionalCommentBlock;
            if (thisCommentBlock != "")
            {
                CommentItems.Add(new CommentItem(thisCommentBlock, true));
                thisCommentBlock = "";
            }
        }

        // init our CommentHelper
        public CommentHelper(string item, bool IsContinuedLineComment, bool IsContinuedBlockComment)
        {
            // item should be a single line of text, with no CR/LF
            thisLine = item;
            CommentItems = new List<CommentItem>();
            this.HasOpenLineComment = IsContinuedLineComment;// we may be string with a string (or tag) on a line after a "//", set to be re-used again
            if (IsContinuedLineComment)
            {
                this.HasBlockEndComment = false;
                this.HasBlockStartComment = false; // we can never have an open block comment when there's an open line comment (e.g. "// comment /* this is still ine comment, not block")
                AppendCommentListItem(item);
                return;
            }

            HasBlockStartComment = IsContinuedBlockComment;

            posLineComment = thisLine.IndexOf("//");
            posBlockStartComment = thisLine.IndexOf("/*");
            posBlockEndComment = thisLine.IndexOf("*/");

            if (IsContinuedBlockComment || (posBlockStartComment > -1) || (posBlockEndComment > -1) || (posLineComment > -1))
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

                if (HasOpenLineComment && (posLineComment > posBlockStartComment))
                {
                    posLineComment = -1; // ignore this line comment for now, being after the opening block
                    HasOpenLineComment = false;
                }

                thisCommentBlock = "";
                thisNonCommentBlock = "";
                string thisChar = "";
                string nextChar = "";
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

                    if (thisTag == "//")
                    {
                        if (HasBlockStartComment)
                        {
                            // nothing to do, this "//" comment is arleady inside a block comment
                            HasOpenLineComment = false; // for completness, we cannot have an active open line comment "//" inside of a block comment "/"
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
                        AppendBlockChar(thisChar); // append this char to the comment or non-comment block as appropriate
                    }

                    // else check for an opening comment block "/*"
                    else if (thisTag == "/*")
                    {
                        if (thisNonCommentBlock != "")
                        {
                            CommentItems.Add(new CommentItem(thisNonCommentBlock, false));
                            thisNonCommentBlock = "";
                        }
                        if (!HasOpenLineComment)
                        {
                            // we can only open a block comment outside of an open line comment
                            HasBlockStartComment = true;
                        }
                        AppendBlockChar(thisChar); // append this char to the comment or non-comment block as appropriate
                    }

                    // else check for closing comment block "*/"
                    else if (thisTag == "*/")
                    {
                        if (HasOpenLineComment)
                        {
                            // nothing to do, closing block after open line comment
                        }
                        else
                        {
                            AppendBlockChar(thisChar); // append this char to the comment or non-comment block as appropriate
                            AppendBlockChar(nextChar); // append the next char to the comment or non-comment block as appropriate
                            i++;
                            if (HasBlockStartComment)
                            {
                                CommentItems.Add(new CommentItem(thisCommentBlock, true));
                                thisCommentBlock = "";
                            }
                            else
                            {
                                // closing block comment found without opening, so it is not a comment
                                CommentItems.Add(new CommentItem(thisNonCommentBlock, false));
                                thisNonCommentBlock = "";
                            }
                            HasBlockStartComment = false;
                            HasBlockEndComment = false; // once we find an end, we cannot have another

                        } // else not HasOpenLineComment: this "*/" is not after "//"
                    } //  else if (thisTag == "*/")

                    // if none of the comment tags are found, continue appaending with whatever state we are in (comment or no-commnet)
                    else
                    {
                        // as this is not a comment state change, append thisChar as appropriate
                        AppendBlockChar(thisChar);
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
            }
            else
            {
                // if we didn't have incoming active comment, and didn't find an opening,
                // then we don't have a comment to consider, so the entire item is not a comment
                CommentItems.Add(new CommentItem(item, false));
            }
        } // CommentHelper class initializer
    } // CommentHelper class

}
