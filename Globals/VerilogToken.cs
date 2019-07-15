
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace VerilogLanguage
{
    public static partial class VerilogGlobals
    {
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
                   (theString == "~") ||
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


        /// <summary>
        ///   VerilogParseState - while processing each segment, we'll keep track of attributes in a VerilogParseState 
        /// </summary>
        public struct VerilogParseState
        {
            public string thisItem;
            public int thisIndex;
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
                    // note  contiguous spaces are a single segment
                    IsNewDelimitedSegment = (thisCharIsDelimiter || priorCharIsDelimiter) && !((_thisChar == " ") && (priorChar == " "));

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
        public static VerilogToken[] VerilogKeywordSplit(string theString, VerilogToken priorToken)
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
    }
}
