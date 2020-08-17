
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
        public static List<string> VerilogBinaryChars = new List<string> { "0", "1",   
                                                                           "z", "Z",   
                                                                           "x", "X" 
                                                                          };
        public static List<string> VerilogDecimalChars = new List<string> { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
                                                                            "z", "Z",
                                                                            "x", "X"
                                                                          };
        public static List<string> VerilogHexChars = new List<string> { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "A", "B", "C", "D", "E", "F",
                                                                            "z", "Z",
                                                                            "x", "X"
                                                                          };
        public static List<string> VerilogOctalChars = new List<string> { "0", "1", "2", "3", "4", "5", "6", "7",
                                                                            "z", "Z",
                                                                            "x", "X"
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
            public bool thisCharIsDelimiter; // this includes "special cases", NOT just IsDelimiter(value); see IsDelimiterValue.
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

            // 8                   'h        2A
            // IsBuildingNumber  HasRadix HasConstValue
            public bool IsBuildingNumber; // some numbers could end up being constants with embedded spaces: "8 'h 2A"
            public bool HasRadix; // the middle segment of "8 'h 2A", is there a 'h value? (or other radix values)
            public bool HasConstValue; // the 3rd part: the constant after the radix

            private string _thisChar;
            public string thisChar
            {
                get { return _thisChar; }
                set
                {
                    _thisChar = value;

                    char c = Convert.ToChar(value);
                    bool IsDelimiterValue = IsDelimiter(value);
                    bool IsVerilogBracketValue = IsVerilogBracket(value);
                    bool IsNewItemStart = priorCharIsDelimiter || ((thisItem ?? "").Trim() == "");

                    // check to see if we've found a constant value after the radix
                    if (IsBuildingEmbeddedSpaceItem && HasRadix && !IsDelimiterValue && !HasConstValue)
                    {
                        // after we find a radix value, we don't know if there's actually a constant value until we find one
                        // TODO: check for valid value?
                        HasConstValue = true;
                    }

                    // if we find a delimiter (not including space) during the building of an ebedded space item, we've reached the end
                    if (IsDelimiterValue && IsBuildingEmbeddedSpaceItem && value != " ")
                    {
                        IsBuildingNumber = false;
                        HasConstValue = false;
                        IsBuildingEmbeddedSpaceItem = false;
                    }

                    // the beginning char of a new item might make it interesting
                    if (IsNewItemStart ) {
                        // we are never already building a number for blank items, and not yet processed _thisChar
                        // so set some obvious things:
                        IsBuildingNumber = false;
                        HasConstValue = false;
                        IsBuildingEmbeddedSpaceItem = false;

                        // if we first find a number, we could be bulding a simmple constant like 123 
                        // or possibly something more interesting like the "8" in "8 'h 2A"
                        if (char.IsNumber(c))
                        {
                            IsBuildingNumber = true; // this is  only true when the first digit is numeric (note in the second part, could be hex, z, or x
                            HasConstValue = true; // this could be either the "8" or the "2A"
                            IsBuildingEmbeddedSpaceItem = true; // // this could be either the "8" in "8 'h f_f" 
                        }
                    }

                    // have we found a radix while building a number string?
                    // radix values *must* be adjacent (e.g. "3' b001" is not valid)
                    // looking for the "'b" in "3 'b 001" or "3'b 001" or "3'b 001" or "3'b001"
                    if (IsBuildingNumber && (priorValue == "'") && (VerilogRadixChars.Contains(value)))
                    {
                        // we get here on a radix value immediately following a single quote:  (e.g. the "h" in "32'h ffff_ffff)
                        // IsBuildingEmbeddedSpaceItem = true; // we already assumed IsBuildingEmbeddedSpaceItem = true, above, but we might need to turn it off
                        HasRadix = true;
                        HasConstValue = false; // instead of the "3" being the constant, now we are looking for a value after the radix
                    }

                    // need to set FoundPostRadixValue - when a space, not after a closing squiigly, means end of value!

                    thisCharIsDelimiter = (IsDelimiterValue && !IsBuildingEmbeddedSpaceItem) // the easiest determination

                                            ||

                                          (IsDelimiterValue && IsBuildingEmbeddedSpaceItem && HasRadix && HasConstValue) // when we are building an embedded value, spaces are not delimiters!

                                            ||

                                          // special case for "*" when not a comment delimiter
                                          (value == "*" && priorValue != "/") // if we did not find "/*", then this "*" is a delimiter
                                            ||
                                          (priorValue == "*" && value != "/") // if we did not find "*/", then this "*" is a delimiter

                                            ||
                                          
                                          // special case for "/" when not a comment delimter
                                          (value == "/" && priorValue != "*" && priorValue != "/" ) // if we did not find "*/", then this "/" is a delimiter
                                            ||
                                          (priorValue == "/" && value != "*" && value != "/"); // if we did not find "/*" then this "/" is a delimiter 

                    thisCharIsEndingDelimiter = IsEndingDelimeter(value); // any of the closing brackets
                    priorCharIsDelimiter = IsDelimiter(priorChar);

                    //priorCharIsDelimiter = IsDelimiter(priorChar); // any common delimiter, including space; TODO - we can save this value at end: priorCharIsDelimiter =  thisCharIsDelimiter

                    if (IsBuildingEmbeddedSpaceItem)
                    {
                        // note that spaces ARE allowed when bulding an embedded space item (e.g. the value "32'h ffff_ffff" is just one item!)
                        IsNewDelimitedSegment = IsVerilogBracketValue 
                                                      || 
                                          (     (thisCharIsDelimiter || priorCharIsDelimiter)
                                            && !(     (_thisChar == " ")
                                                  && !(priorChar == " ")
                                                )
                                            && !hasOpenSquigglyBracket // e.g. {4'b 0001, 32'b 0}
                                           );
                    }
                    else
                    {
                        // note  contiguous spaces are a single segment
                        // reminder that variable definitions may contain multiple segments with space delimiters


                        IsNewDelimitedSegment = IsVerilogBracketValue
                                                      || 
                                                (
                                                (thisCharIsDelimiter || priorCharIsDelimiter)
                                                  && !(    (_thisChar == " ")
                                                        && (priorChar == " ")
                                                      )
                                                );

                    }


                    if (IsNewDelimitedSegment)
                    {
                        //HasConstValue = false;
                        //IsBuildingEmbeddedSpaceItem = false;
                        //HasRadix = false;
                        //HasConstValue = false;
                        //IsBuildingNumber = false;
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
                                if (IsBuildingEmbeddedSpaceItem)
                                {
                                    // once we have a constant value, or a constant value after a radix... and a delimiter
                                    // char is found, we've reached the end of this item. Time for a new delimited segment
                                    IsBuildingEmbeddedSpaceItem = !(HasConstValue && HasRadix && thisCharIsDelimiter);

                                    if (!IsBuildingEmbeddedSpaceItem)
                                    {
                                        IsNewDelimitedSegment = true;
                                    }
                                }
                                // IsBuildingEmbeddedSpaceItem = IsBuildingNumber && HasRadix && HasConstValue && thisCharIsDelimiter;  // as soon as we see a non-blank value, we don't need to keep track: the next delimiter is the end of this segment
                                // TODO: "8 'h 2A" is a valid value
                                // "3' b001" is not valid

                            }
                        }
                    }

                    if (IsNewDelimitedSegment)
                    { 

                    }
                    else
                    {
                        // we only append the items here when building an item to add, otherwise the value is carried over after adding a token
                        // see AddToken: new VerilogToken(thisToken.ParseState.thisChar);
                        thisItem += value;
                    }
                    // priorCharIsDelimiter = thisCharIsDelimiter;
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
                IsBuildingNumber = false;
                HasRadix = false;
                HasConstValue = false;
                OpenSquigglyBracketCount = 0;
            }
        }



    }
}
