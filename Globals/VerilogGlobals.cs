using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VerilogLanguage.VerilogToken;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Utilities;

namespace VerilogLanguage
{
    public static partial class VerilogGlobals  
    {

        public static ITextBuffer TheBuffer;
        public static ITextView TheView; // assigned in QuickInfoControllerProvider see https://docs.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.text.editor.itextview?redirectedfrom=MSDN&view=visualstudiosdk-2017


        /// <summary>
        ///   VerilogVariableHoverText - dictionary collection of keywords and hover text (variable names and definitions)
        /// </summary>
        public static Dictionary<string, string> VerilogVariableHoverText = new Dictionary<string, string>
        {
            // e.g. ["led"] = "An LED."
        };

        /// <summary>
        ///   VerilogVariables - a list of variables found in the text that will be have  hover text (see VerilogVariableHoverText)
        /// </summary>
        public static IDictionary<string, VerilogTokenTypes> VerilogVariables = new Dictionary<string, VerilogTokenTypes>
        {
            // e.g. ["led"] = VerilogTokenTypes.Verilog_Variable,
        };


        public static Boolean IsContinuedBlockComment = false;
        public static string thisVariableHoverText = "";
        public static string thisHoverName = "";
        public static bool IsNextNonblankName = false; // true when the next, non-blank item is the name
        public static bool IsLastName = false;
        public static bool IsNaming = false; // when we find a naming keyaord (module, input, etc)... we will build hover text, including comments
        public static string lastKeyword = "";
        public static bool FoundHoverName = false;

        private static bool IsVerilogNamerKeyword(string theKeyword)
        {
            return ((theKeyword == "reg") || 
                    (theKeyword == "wire") ||
                    (theKeyword == "input") ||
                    (theKeyword == "inout") ||
                    (theKeyword == "output") ||
                    (theKeyword == "parameter") ||
                    (theKeyword == "module")
                   );
        }

        private static bool IsVerilogValue(string theKeyword)
        {
            return theKeyword.Contains("'b") || theKeyword.Contains("'h");
        }

        public static Boolean IsNumeric(String input)
        {
            double temp;
            return double.TryParse(input, out temp); ;
        }

        public static void BuildHoverItems(string s)
        {
            string thisTrimmedItem = (s == null) ? "" : s.Trim();

            //  when naming, all text, including blanks are appended to hover text
            if (IsNaming)
            {
                thisVariableHoverText += s;
            }

            // blanks are ignored here for everything else, so return
            if (thisTrimmedItem == "")
            {
                return;
            }

            if (IsNumeric(s) || IsVerilogValue(s) )
            {
                if (!VerilogVariables.Keys.Contains(s))
                {
                    VerilogVariables.Add(s, VerilogTokenTypes.Verilog_Value);
                    if (!IsNaming)
                    {
                        return;
                    }
                }
            }

            if (IsNaming)
            {
                if (IsNextNonblankName)
                {
                    // the name is the next, non-blank keyword found (e.g. my
                    IsNextNonblankName = false;
                    thisHoverName = thisTrimmedItem;
                }



                // when we are naming a veriable and end counter a semicolon or comma, we're done. add it and reset.
                if ((thisTrimmedItem == ";") || (thisTrimmedItem == ",") || (thisTrimmedItem == ")") || (thisTrimmedItem == "="))
                {
                    IsNaming = (thisTrimmedItem == "="); // set to false when we are done. all naming ends upon semi-colon.
                                                         // an equals-sign needs the next value included. (e.g. reg val = 1'b0; )

                     if (IsLastName && !FoundHoverName)
                    {
                        // the name is the last, non-blank value before the semicolon. (e.g. "J2_AD_PORT" from "input [7:0] J2_AS_PORT;")
                        IsLastName = (thisTrimmedItem != ","); // commas allow for multiple variables
                        thisHoverName = lastKeyword;
                        FoundHoverName = true;
                    }
                    
                    if (!IsNaming)
                    {
                        if (!VerilogVariables.Keys.Contains(thisHoverName) && thisHoverName != "")
                        {
                            VerilogVariables.Add(thisHoverName, VerilogTokenTypes.Verilog_Variable);
                            if (!VerilogGlobals.VerilogVariableHoverText.ContainsKey(thisHoverName))
                            {
                                VerilogGlobals.VerilogVariableHoverText.Add(thisHoverName, thisVariableHoverText);
                            }
                            else
                            {
                                VerilogGlobals.VerilogVariableHoverText[thisHoverName] = thisVariableHoverText;
                            }
                        }
                        // thisHoverName is the variable keyword and VerilogVariableHoverText is the definition text
                        thisVariableHoverText = "";
                        thisHoverName = "";
                        IsNextNonblankName = false;
                        thisTrimmedItem = ""; // once detected, we won't use it here
                    }

                } // end if (thisTrimmedItem == ";")

            } // end if (IsNaming)

            // we're not renaming at the moment, but perhaps if this token is a variable namer (module, input, etc)
            else
            {
                IsNaming = IsVerilogNamerKeyword(thisTrimmedItem); // we are not currently naming, but perhaps we'll turn it on with a keyword?'
                if (IsNaming)
                {
                    switch (thisTrimmedItem)
                    {
                        case "module":
                            IsNextNonblankName = true;
                            thisVariableHoverText = thisTrimmedItem;
                            break;

                        case "input":
                        case "output":
                        case "inout":
                        case "wire":
                        case "reg":
                        case "parameter":
                            IsLastName = true;
                            FoundHoverName = false;
                            thisVariableHoverText = thisTrimmedItem;
                            break;

                        default:
                            break;
                    }
                }
            } // end else naming

            lastKeyword = thisTrimmedItem;

        }  // end void BuildHoverItems

        public static void Dispose()
        {
            // see https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose
            //
            // there are no  unmanaged resources that need to be released at this time
            //
            // TODO cleanup
        }
    } // class
} // namespace
