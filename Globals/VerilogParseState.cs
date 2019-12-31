
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

            public bool hasOpenSquareBracket;
            public bool hasOpenRoundBracket;
            public bool hasOpenSquigglyBracket;

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

                    if ((priorValue == "'") && ((value == "h") || (value == "b")))
                    {
                        // e.g. 32'h ffff_ffff
                        IsBuildingEmbeddedSpaceItem = true;
                    }


                    thisCharIsDelimiter = IsDelimiter(value);
                    thisCharIsEndingDelimiter = IsEndingDelimeter(value);
                    priorCharIsDelimiter = IsDelimiter(priorChar);

                    if (IsBuildingEmbeddedSpaceItem)
                    {
                        // note that spaces ARE allowed when bulding an embedded item (e.g. "32'h ffff_ffff" is not two items!)
                        IsNewDelimitedSegment = (thisCharIsDelimiter || priorCharIsDelimiter)
                                            && !((_thisChar == " ")
                                            && !(priorChar == " "));
                    }
                    else
                    {
                        // note  contiguous spaces are a single segment
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
                            IsBuildingEmbeddedSpaceItem = (value != "'");  // as soon as we see a non-blank value, we don't need to keep track: the next delimiter is the end of this segment
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
            }
        }



    }
}
