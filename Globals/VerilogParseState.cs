
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace VerilogLanguage
{
    public static partial class VerilogGlobals
    {
        public static List<string> VerilogRadixChars = new List<string> {  "d", "D",  // for integer (optional)
                                                                           "h", "H",  //for hexadecimal
                                                                           "o", "O",  //for octal
                                                                           "b", "B"   //for bit
                                                                        };
        /// <summary>
        ///   VerilogParseState - while processing each segment, we'll keep track of attributes in a VerilogParseState 
        /// </summary>
        public struct VerilogParseState
        {
            public string thisItem;
            public int thisIndex;
            public string priorChar;
            public string priorValue;
            public string priorDelimiter;
            public bool thisCharIsDelimiter;
            public bool priorCharIsDelimiter;

            public bool thisCharIsEndingDelimiter;
            public bool priorCharIsIsEndingDelimiter;

            public bool hasOpenSquareBracket;  // not implemented
            public bool hasOpenRoundBracket;   // not implemented
            public bool hasOpenSquigglyBracket;

            public byte OpenSquigglyBracketCount;

            public bool hasOpenDoubleQuote;

            public bool IsNewDelimitedSegment;

            public bool IsBuildingEmbeddedSpaceItem; // we are bulding a "special" segment in cases where there's an embedded space: 

            private string _thisChar;
            public string thisChar
            {
                get { return _thisChar; }
                set
                {
                    _thisChar = value;
                    if ((priorValue == "'") && (VerilogRadixChars.Contains(value)))
                    {
                        // e.g. 32'h ffff_ffff
                        IsBuildingEmbeddedSpaceItem = true;
                    }
                    if (value == "{")
                    {
                        OpenSquigglyBracketCount++;
                    }
                    if (value == "}")
                    {
                        OpenSquigglyBracketCount--;
                    }
                    hasOpenSquigglyBracket = (OpenSquigglyBracketCount != 0);

                    thisCharIsDelimiter = IsDelimiter(value);
                    thisCharIsEndingDelimiter = IsEndingDelimeter(value);
                    priorCharIsDelimiter = IsDelimiter(priorChar);

                    if (IsBuildingEmbeddedSpaceItem)
                    {
                        // note that spaces ARE allowed when bulding an embedded space item (e.g. the value "32'h ffff_ffff" is just one item!)
                        IsNewDelimitedSegment = (thisCharIsDelimiter || priorCharIsDelimiter)
                                            && !((_thisChar == " ")
                                            && !(priorChar == " "))
                                            && !hasOpenSquigglyBracket; // e.g. {4'b 0001, 32'b 0}
                    }
                    else
                    {
                        // note  contiguous spaces are a single segment
                        // reminder that variable definitions may contain multiple segments
                        IsNewDelimitedSegment =  (thisCharIsDelimiter || priorCharIsDelimiter)
                                            && !((_thisChar == " ")
                                            && (priorChar == " "));

                    }


                    if (IsNewDelimitedSegment)
                    {

                    }
                    else
                    {
                        // note  contiguous spaces are a single segment
                        if (IsBuildingEmbeddedSpaceItem)
                        {
                            if (hasOpenSquigglyBracket)
                            {
                                // we continue searching
                            }
                            else {
                                // only for closed squiggly, can we end a constant
                                IsBuildingEmbeddedSpaceItem = (value != "'");  // as soon as we see a non-blank value, we don't need to keep track: the next delimiter is the end of this segment
                            }
                        }
                        thisItem += value;
                    }
                    priorValue = value;
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
                priorValue = "";
                priorDelimiter = "";
                thisCharIsDelimiter = false;
                priorCharIsDelimiter = false;
                thisCharIsEndingDelimiter = false;
                priorCharIsIsEndingDelimiter = false;
                hasOpenSquareBracket = false;
                hasOpenRoundBracket = false;
                hasOpenSquigglyBracket = false;
                hasOpenDoubleQuote = false;
                IsBuildingEmbeddedSpaceItem = false;
                OpenSquigglyBracketCount = 0;
            }
        }



    }
}
