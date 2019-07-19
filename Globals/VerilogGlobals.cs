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
        private static bool blnIsVariableExpected = false;
        private static bool IsModuleDeclarationActive = false;
        private static string thisModuleName = "";
        private static string thisModuleDeclarationText = ""; 

        /// <summary>
        ///   InitHoverBuilder - prep for another refresh of hover item lookup
        /// </summary>
        public static void InitHoverBuilder()
        {
            // re-initialize variables to above values
            VerilogVariableHoverText = new Dictionary<string, string> { };
            VerilogVariables = new Dictionary<string, VerilogTokenTypes> { };
            IsContinuedBlockComment = false;
            thisVariableHoverText = ""; // the string we will build the declaration in
            thisHoverName = "";
            IsNextNonblankName = false; // true when the next, non-blank item is the name
            IsLastName = false;
            IsNaming = false; // when we find a naming keyaord (module, input, etc)... we will build hover text, including comments
            lastKeyword = "";
            FoundHoverName = false;
            FoundDeclaration = false;
            thisVariableDeclarationText = "";
            blnIsVariableExpected = false;

            IsModuleDeclarationActive = false;
            thisModuleDeclarationText = "";
            thisModuleName = "";
        }


        /// <summary>
        ///  BuildHoverItems - builds the Verilog variable hover text. Called in IEnumerable VerilogTokenTagger
        ///                    as each token text string is encountered.
        /// </summary>
        /// <param name="s"></param>
        public static void BuildHoverItems(string s)
        {
            void AddHoverItem(string ItemName, string HoverText)
            {
                if (!VerilogVariables.Keys.Contains(ItemName) // we only have something to do if this variable does not exist in the lookup
                     && !IsDelimiter(ItemName) // never add a delimiter TODO - why would we even try? unresolved declaration naming?
                     && ItemName != "" // never add a blank
                   ) // if
                {
                    blnIsVariableExpected = false; // if we were expecting to find one, here it is!

                    VerilogVariables.Add(ItemName, VerilogTokenTypes.Verilog_Variable);

                    string thisHoverText = HoverText;
                    if (IsModuleDeclarationActive)
                    {
                        thisHoverText = "module " + thisModuleName + "(" + thisHoverText + ")";
                        thisModuleDeclarationText += HoverText + System.Environment.NewLine;
                    }

                    if (!VerilogGlobals.VerilogVariableHoverText.ContainsKey(ItemName))
                    {
                        // add a new variable hover text attribute
                        VerilogGlobals.VerilogVariableHoverText.Add(ItemName, thisHoverText);
                    }
                    else
                    {
                        // overwrite an existing variable declaration
                        VerilogGlobals.VerilogVariableHoverText[ItemName] = thisHoverText;
                    }  
                } // if
            }

            string thisTrimmedItem = (s == null) ? "" : s.Trim();

            // blanks are ignored here for everything else, so return
            if (thisTrimmedItem == "")
            {
                if (IsNaming && !FoundDeclaration)
                {
                    // we remove up extra spaces in declaration for cleaner hover text
                    thisVariableHoverText += " ";
                }
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

            // have we reached the end of an existing declaration: 
            //   input wire[1:1] k1, h1
            //

            if (IsNaming || IsModuleDeclarationActive) 
            {
                //  when naming, all text, including blanks are appended to hover text

                // we're here bacause a prior keyword was something like: input, wire, etc...
                // the first non-declaration text indicates we found the base variable declaration
                // string: everything minus the actual name of the variable(s)
                if (IsVerilogNamerKeyword(thisTrimmedItem))
                {
                    // while naming, some verilog keywords can be accumulated in hover text.
                    // for example: input wire...
                    thisVariableHoverText += thisTrimmedItem;
                }
                else
                {
                    if (FoundDeclaration) 
                    {
                        // we're naming a declaration, this is not a keyword, and we have a declaration string
                        // such as "input wire [1:1], thus thisTrimmedItem must be a variable name.
                        if (IsModuleDeclarationActive && (thisModuleDeclarationText == "") && (thisTrimmedItem == "("))
                        {
                            // move the variable declaration into the module declaration
                            // we may have intra-module declarations to follow
                            thisModuleName = thisHoverName;

                            thisModuleDeclarationText = thisVariableDeclarationText + " " + thisHoverName + " (" + System.Environment.NewLine;

                            // clear the variable strings since we were naming a module
                            thisHoverName = "";
                            thisVariableDeclarationText = "";
                            thisVariableHoverText = "";
                            FoundDeclaration = false;
                        }
                        else
                        {
                            if (thisTrimmedItem == ",")
                            {
                                if (IsModuleDeclarationActive)
                                {
                                    blnIsVariableExpected = false;
                                    thisVariableHoverText = "";
                                }
                                else
                                {
                                    blnIsVariableExpected = true; // next variable expected (may be on next line?)
                                }
                            }
                            else if (thisTrimmedItem == ";")
                            {
                                // no more variables in a comma-delimited list with this declaration
                                blnIsVariableExpected = false;
                            }
                            else if (thisTrimmedItem == ")")
                            {
                                // this is likely the end of a module definition (or a syntax error)
                                thisModuleDeclarationText += thisTrimmedItem;
                            }
                            else
                            {
                                thisHoverName = thisTrimmedItem;
                                AddHoverItem(thisHoverName, thisVariableHoverText + " " + thisHoverName);
                            }
                        }
                    }
                    else
                    {
                        // the first non-keyword, non-numeric, non-array, non-equal sign is the end of the declaration and first
                        // non black segment should be the variable
                        if (IsVerilogBracket(thisTrimmedItem) || IsNumeric(thisTrimmedItem) || IsVerilogValue(thisTrimmedItem))
                        {
                            // nothing at this time; we are still bulding the declaration part
                        }
                        else
                        {
                            // we've found the declaration part, such as:
                            //  input wire [1:1]
                            // the following, comma delimited text items later found will be variables
                            // 
                            thisVariableDeclarationText = thisVariableHoverText;
                            FoundDeclaration = true;
                            thisHoverName = thisTrimmedItem;
                        }
                        if (!FoundDeclaration)
                        {
                            thisVariableHoverText += thisTrimmedItem;
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
                    if (IsModuleDeclarationActive && (thisTrimmedItem == ")"))
                    {
                        IsModuleDeclarationActive = false;
                        AddHoverItem(thisModuleName, thisModuleDeclarationText);
                    }
                    else {
                        // variable declaration
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
                            AddHoverItem(thisHoverName, thisVariableHoverText + " " + thisHoverName);

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
                            AddHoverItem(thisHoverName, thisVariableHoverText + " " + thisHoverName);
                        }
                    } // else not IsModuleDeclarationActive

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
                        IsModuleDeclarationActive = true;
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
                        FoundDeclaration = false;
                        blnIsVariableExpected = true;
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
