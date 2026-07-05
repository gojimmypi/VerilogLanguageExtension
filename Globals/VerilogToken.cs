// file: Globals/VerilogToken.cs
//***************************************************************************
//
//  MIT License
//
//  Copyright (c) 2025-2026 gojimmypi
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

using System.Collections.Generic;

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

            public void SetContext() {
                if (ParseState.hasOpenDoubleQuote) {
                    Context = VerilogTokenContextType.DoubleQuoteOpen;
                }
                else {
                    if (ParseState.hasOpenSquareBracket && !IsDelimiter(ParseState.thisChar)) {
                        Context = VerilogTokenContextType.SquareBracketContents;
                    }
                    else {
                        if (ParseState.hasOpenRoundBracket && !IsDelimiter(ParseState.thisChar)) {
                            Context = VerilogTokenContextType.RoundBracketContents;
                        }
                        else {
                            if (ParseState.hasOpenSquigglyBracket && !IsDelimiter(ParseState.thisChar)) {
                                Context = VerilogTokenContextType.SquigglyBracketContents;
                            }
                            else {
                                Context = VerilogTokenContextFromString(ParseState.thisChar.ToString());
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
            public VerilogToken(string p = "", VerilogTokenContextType c = VerilogTokenContextType.Undetermined) {
                ParseState = new VerilogParseState(0);
                Part = p ?? string.Empty;

                if (c == VerilogTokenContextType.Undetermined && Part.Length > 0) {
                    Context = VerilogTokenContextFromString(Part);
                }
                else {
                    Context = c;
                }
            }
        }

        /// <summary>
        ///    VerilogToken[] given a line of text, split it into tokens (and array of strings, 0 or more chars)
        /// </summary>
        /// <param name="theString"></param>
        /// <returns></returns>
        public static VerilogToken[] VerilogKeywordSplit(string theString, VerilogToken priorToken) {
            List<VerilogToken> tokens = new List<VerilogToken>();
            VerilogToken thisToken = new VerilogToken();
            VerilogParseState thisContinuedParseState = new VerilogParseState(0);

            VerilogParseState BeginLineParseState(VerilogParseState priorState) {
                VerilogParseState state = priorState;

                // Preserve prior-line parse context, but do not carry the prior token
                // text or character/delimiter bookkeeping into this line. If thisItem
                // is carried across, the first token on the new line is duplicated
                // from the prior line, curLoc advances too far, and highlighted spans
                // shift by one character.
                state.thisItem = string.Empty;
                state.thisIndex = 0;
                state.IsNewDelimitedSegment = false;
                state.priorChar = '\0';
                state.priorValue = '\0';
                state.priorDelimiter = '\0';
                state.thisCharIsDelimiter = false;
                state.priorCharIsDelimiter = false;
                state.thisCharIsEndingDelimiter = false;
                state.priorCharIsIsEndingDelimiter = false;
                state.IsBuildingEmbeddedSpaceItem = false;
                state.IsBuildingNumber = false;
                state.HasRadix = false;
                state.HasConstValue = false;
                state.NumberStringValue = string.Empty;

                return state;
            }

            // AddToken - appends the current token part to the array and create a new thisToken to build.
            // reminder that here we are only splitting text into token items.
            // See VerilogTokenTagger for actually setting the context (e.g. color) of  each token item.
            //
            void AddToken() {
                thisToken.Part = thisToken.ParseState.thisItem;
                if (!string.IsNullOrEmpty(thisToken.Part)) {
                    // thisToken.Part = thisToken.Part.Trim();
                    thisContinuedParseState = thisToken.ParseState;
                    tokens.Add(thisToken);

                    thisToken = new VerilogToken(thisToken.ParseState.thisChar.ToString());
                    thisToken.ParseState = thisContinuedParseState;
                }
                thisToken.ParseState.thisItem = (thisToken.ParseState.thisChar == '\0') ? string.Empty : thisToken.ParseState.thisChar.ToString(); // start building a new token with the current, non-delimiter character, will be used to determine context in VerilogTokenContextFromString
            }

            // Start this line with the prior-line context, but without stale
            // token text/delimiter state from the previous line.
            thisToken.ParseState = BeginLineParseState(priorToken.ParseState);

            for (int i = 0; i < theString.Length; i++) {
                thisToken.ParseState.thisIndex = i;
                thisToken.ParseState.thisChar = theString[i]; // note setting this values triggers ParseState attribute assignments

                if (thisToken.ParseState.IsNewDelimitedSegment) {
                    // anytime a delimiter is encountered, we start a new text segment
                    // note the delimiter itself is in a colorizable segment

                    // there's a new delimiter, so add the current item and prep for the next one
                    AddToken();

                    // once the ParseState is configured (above, when assigning thisChar), set the context of the item
                    thisToken.SetContext(); // TODO do we really need this? context is already set
                    // at the end of each loop, set the prior values
                    thisToken.ParseState.SetPriorValues();
                } // end of for loop look at each char
            }

            // if there's anything left, add it is as token (blank token not added)
            AddToken();
            return tokens.ToArray();
        }
    }
}
