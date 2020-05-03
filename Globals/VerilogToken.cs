
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace VerilogLanguage
{
    public static partial class VerilogGlobals
    {

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
                    if (ParseState.hasOpenSquareBracket && !IsDelimiter(ParseState.thisChar))
                    {
                        Context = VerilogTokenContextType.SquareBracketContents;
                    }
                    else
                    {
                        if (ParseState.hasOpenRoundBracket && !IsDelimiter(ParseState.thisChar))
                        {
                            Context = VerilogTokenContextType.RoundBracketContents;
                        }
                        else
                        {
                            if (ParseState.hasOpenSquigglyBracket && !IsDelimiter(ParseState.thisChar))
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
        ///    VerilogToken[] given a line of text, split it into tokens (and array of strings, 0 or more chars)
        /// </summary>
        /// <param name="theString"></param>
        /// <returns></returns>
        public static VerilogToken[] VerilogKeywordSplit(string theString, VerilogToken priorToken)
        {
            List<VerilogToken> tokens = new List<VerilogToken>();
            VerilogToken thisToken = new VerilogToken();
            VerilogParseState thisContinuedParseState = new VerilogParseState(0);

            // AddToken - appends the current token part to the array and create a new thisToken to build.
            // reminder that here we are only splitting text into token items. 
            // See VerilogTokenTagger for actually setting the context (e.g. color) of  each token item.
            // 
            void AddToken()
            {
                //string thisItem = thisToken.ParseState.thisItem;
                //if (thisItem != "") // && thisItem != "")
                //{
                    thisToken.Part = thisToken.ParseState.thisItem;
                    if (thisToken.Part != null)
                    {
                        thisContinuedParseState = thisToken.ParseState;
                        tokens.Add(thisToken);

                        thisToken = new VerilogToken(thisToken.ParseState.thisChar);
                        thisToken.ParseState = thisContinuedParseState;
                    }
                    thisToken.ParseState.thisItem = thisToken.ParseState.thisChar; // start building a new token with the current, non-delimiter character, will be used to determine context in VerilogTokenContextFromString
                //}
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

                    //VerilogToken old = thisToken;

                    // once the ParseState is configured (above, when assigning thisChar), set the context of the item
                    thisToken.SetContext(); // TODO do we really need this? context is already set
                    // at the end of each loop, set the prior values

                    //if (old.Context != thisToken.Context)
                    //{
                    //    old.Context = thisToken.Context;
                    //}
                    thisToken.ParseState.SetPriorValues();
                } // end of for loop look at each char
            }

            // if there's anything left, add it is as token (blank token not added)
            AddToken();
            return tokens.ToArray();
        }
    }
}
