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
        public static bool FoundDeclaration = false;
        public static string thisVariableDeclarationText = "";

        public static void InitHoverBuilder()
        {
            IsContinuedBlockComment = false;
            thisVariableHoverText = "";
            thisHoverName = "";
            IsNextNonblankName = false; // true when the next, non-blank item is the name
            IsLastName = false;
            IsNaming = false; // when we find a naming keyaord (module, input, etc)... we will build hover text, including comments
            lastKeyword = "";
            FoundHoverName = false;
            FoundDeclaration = false;
            thisVariableDeclarationText = "";
        }


        /// <summary>
        ///  BuildHoverItems - builds the Verilog variable hover text. Called in IEnumerable VerilogTokenTagger
        ///                    as each token text string is encountered.
        /// </summary>
        /// <param name="s"></param>
        public static void BuildHoverItems(string s)
        {
            void AddHoverItem()
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
            }

            string thisTrimmedItem = (s == null) ? "" : s.Trim();

            //  when naming, all text, including blanks are appended to hover text
            if (IsNaming && (thisTrimmedItem != ","))
            {
                if (FoundDeclaration)
                {
                    thisVariableHoverText = thisVariableDeclarationText + " " + s;
                }
                else
                {
                    thisVariableHoverText += s;
                }
            }

            // blanks are ignored here for everything else, so return
            if (thisTrimmedItem == "")
            {
                return;
            }

            // numeric literal values get special attention
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
                // we're here bacause a prior keyword was something like: input, wire, etc...
                // the first non-declaration text indicates we found the base variable declaration
                // string: everything minus the actual name of the variable(s)
                if (!IsVerilogNamerKeyword(thisTrimmedItem)) {
                    if (FoundDeclaration)
                    {
                        AddHoverItem();
                    }
                    else
                    {
                        if (!IsVerilogBracket(thisTrimmedItem) && !IsNumeric(thisTrimmedItem) && !IsVerilogValue(thisTrimmedItem))
                        {
                            // we are still bulding the declaration part
                            thisVariableDeclarationText = thisVariableHoverText;
                            FoundDeclaration = true;
                            thisHoverName = thisTrimmedItem;
                        }
                    }

                }

                if (IsNextNonblankName)
                {
                    // the name is the next, non-blank keyword found (e.g. my
                    IsNextNonblankName = false; // we only do this once
                    thisHoverName = thisTrimmedItem;
                }
 


                // when we are naming a veriable and end counter a semicolon or comma, we're done. add it and reset.
                if ( (thisTrimmedItem == ";") || (thisTrimmedItem == ",") || (thisTrimmedItem == ")") || (thisTrimmedItem == "="))
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
                    
                    if (!IsNaming || (lastKeyword == ","))
                    {
                        AddHoverItem();

                        //if (!VerilogVariables.Keys.Contains(thisHoverName) && thisHoverName != "")
                        //{
                        //    VerilogVariables.Add(thisHoverName, VerilogTokenTypes.Verilog_Variable);
                        //    if (!VerilogGlobals.VerilogVariableHoverText.ContainsKey(thisHoverName))
                        //    {
                        //        VerilogGlobals.VerilogVariableHoverText.Add(thisHoverName, thisVariableHoverText);
                        //    }
                        //    else
                        //    {
                        //        VerilogGlobals.VerilogVariableHoverText[thisHoverName] = thisVariableHoverText;
                        //    }
                        //}
                        // thisHoverName is the variable keyword and VerilogVariableHoverText is the definition text
                        if (thisTrimmedItem == ",")
                        {
                            IsNaming = true;
                            FoundHoverName = false;
                        }
                        else
                        {
                            thisVariableHoverText = "";
                        }
                        thisHoverName = "";
                        IsNextNonblankName = false;
                        thisTrimmedItem = ""; // once detected, we won't use it here
                    }
                    else
                    {
                        AddHoverItem();
                    }

                } // end if (thisTrimmedItem == ";")

            } // end if (IsNaming)

            // we're not renaming at the moment, but perhaps if this token is a variable namer (module, input, etc)
            else
            {
                switch (thisTrimmedItem)
                {
                    case "module":
                        IsNaming = true;
                        IsNextNonblankName = true;
                        thisVariableHoverText = thisTrimmedItem;
                        break;

                    case "input":
                    case "output":
                    case "inout":
                    case "wire":
                    case "reg":
                    case "parameter":
                        IsNaming = true;
                        IsLastName = true;
                        FoundHoverName = false;
                        thisVariableHoverText = thisTrimmedItem;
                        break;

                    default:
                        IsNaming = false;
                        break;
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
