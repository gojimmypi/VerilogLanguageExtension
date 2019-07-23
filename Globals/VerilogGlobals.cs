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
        private static bool IsExpectingModuleName = false;
        private static string thisModuleParameterText = "";
        public static BuildHoverStates BuildHoverState = BuildHoverStates.UndefinedState;

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

            thisVariableDeclarationText = ""; // this is only variable declaration, even if inside a module declaration

            thisModuleDeclarationText = ""; // this is the full module declaration
            thisModuleParameterText = "";
            thisModuleName = "";
            IsExpectingModuleName = false;

            BuildHoverState = BuildHoverStates.UndefinedState;
        }

        private static void AddHoverItem(string ItemName, string HoverText)
        {
            if (!VerilogVariables.Keys.Contains(ItemName) // we only have something to do if this variable does not exist in the lookup
                 && !IsDelimiter(ItemName) // never add a delimiter TODO - why would we even try? unresolved declaration naming?
                 && ItemName != "" // never add a blank
               ) // if
            {
                blnIsVariableExpected = false; // if we were expecting to find one, here it is!

                VerilogVariables.Add(ItemName, VerilogTokenTypes.Verilog_Variable);
                string thisHoverText = HoverText;


                switch (BuildHoverState)
                {
                    // in the case of module parameters, we'll add the keyword "module" and module name to the hover text:
                    // e.g. "module myModule( thisHoverText )"
                    case BuildHoverStates.ModuleParameterNaming:
                    case BuildHoverStates.ModuleParameterMimicNaming:
                        thisHoverText = "module " + thisModuleName + "(" + thisHoverText + ")";
                        // there may be more parameters, so we're not adding it how
                        break;

                    default:
                        break;
                }

                if (!VerilogGlobals.VerilogVariableHoverText.ContainsKey(ItemName))
                {
                    // add a new variable hover text attribute
                    VerilogGlobals.VerilogVariableHoverText.Add(ItemName, thisHoverText);
                }
                else
                {
                    // overwrite an existing variable declaration - duplicate definition?
                    VerilogGlobals.VerilogVariableHoverText[ItemName] = "duplicate? " + thisHoverText;
                }
            } // if
        }


        private static void Process_UndefinedState_For(string ItemText)
        {
            switch (ItemText)
            {
                case "":
                    // ignoring trimmed spaces / blanks
                    break;

                case "module":
                    BuildHoverState = BuildHoverStates.ModuleStart;
                    thisModuleDeclarationText = ItemText;
                    break;

                case "input":
                case "output":
                case "inout":
                case "wire":
                case "reg":
                case "parameter":
                    // the same keywords could be used for module parameters, or variables:
                    switch (BuildHoverState) {
                        case BuildHoverStates.ModuleStart:
                            BuildHoverState = BuildHoverStates.ModuleParameterNaming;
                            break;

                        default:
                            BuildHoverState = BuildHoverStates.VariableNaming;
                            thisVariableDeclarationText = ItemText;
                            break;
                    }
                    break;

                default:
                    BuildHoverState = BuildHoverStates.UndefinedState;
                    break;
            }
        }

        private static void Process_ModuleStart_For(string ItemText)
        {
            // we've found the "module" keyword, the next word should be the module name
            // TODO - flag syntax error for non-variable names found
            switch (ItemText)
            {
                case "":
                    // trimming blanks to a single space
                    thisModuleDeclarationText += " ";
                    break;

                default:
                    thisModuleName = ItemText;
                    thisModuleDeclarationText += ItemText;
                    BuildHoverState = BuildHoverStates.ModuleNamed;
                    break;
            }
        }

        private static void Process_ModuleNamed_For(string ItemText)
        {
            switch (ItemText)
            {
                case "":
                    // trimming blanks to a single space
                    thisModuleDeclarationText += " ";
                    break;

                case "(":
                    thisModuleDeclarationText += ItemText;
                    BuildHoverState = BuildHoverStates.ModuleOpenParen;
                    break;

                default:
                    thisModuleDeclarationText += ItemText;
                    // BuildHoverState = BuildHoverStates.ModuleNamed; no state change
                    break;
            }
        }

        private static void Process_ModuleOpenParen_For(string ItemText)
        {
            switch (ItemText)
            {
                case "":
                    // ignoring trimmed spaces / blanks
                    thisModuleDeclarationText += " ";
                    break;

                case ")":
                    BuildHoverState = BuildHoverStates.ModuleCloseParen;
                    break;

                case "input":
                case "output":
                case "inout":
                case "wire":
                case "reg":
                case "parameter":
                    // the same keywords could be used for module parameters, or variables:
                    BuildHoverState = BuildHoverStates.ModuleParameterNaming;
                    thisModuleParameterText = ItemText;
                    break;

                default:
                    BuildHoverState = BuildHoverStates.UndefinedState;
                    break;
            }

        }

        private static void Process_ModuleParameterNaming_For(string ItemText)
        {
            // once we are naming a module parameter, we only end with a closing parenthesis, or a comman
            switch (ItemText)
            {
                case "":
                    thisModuleDeclarationText += " ";

                    // only append whitespace when not found at beginning
                    if (thisModuleParameterText != "")
                    {
                        thisModuleParameterText += " ";
                    }
                    break;

                case ")":
                    thisModuleDeclarationText += ItemText;
                    AddHoverItem(thisModuleName, thisModuleDeclarationText);

                    // also add an indivisual parameter as needed
                    AddHoverItem(thisHoverName, thisModuleParameterText);
                    thisModuleParameterText = ""; // upon the colose parenthesis, no more module parameters
                    BuildHoverState = BuildHoverStates.UndefinedState; // and no more module definition
                    break;

                case ",":
                    thisModuleDeclarationText += ItemText; // only the module declaration will include the comment
                    thisModuleDeclarationText += System.Environment.NewLine;

                    // add the module parameter
                    AddHoverItem(thisHoverName, thisModuleParameterText);
                    thisModuleParameterText = thisModuleParameterText.Replace(thisHoverName, "");

                    // the next parameter after the comma will use the same definition
                    BuildHoverState = BuildHoverStates.ModuleParameterMimicNaming;
                    break;

                case ";":
                    thisModuleDeclarationText += ItemText; // only the module declaration will include the comment
                    thisModuleDeclarationText += System.Environment.NewLine;

                    // add the module parameter
                    AddHoverItem(thisHoverName, thisModuleParameterText);
                    thisModuleParameterText = ""; // we can't use the same parameter def after a semicolon
                    BuildHoverState = BuildHoverStates.ModuleParameterNaming; // certainly not mimic naming after a semi-colon!
                    break;

                default:
                    thisModuleParameterText += ItemText;
                    thisModuleDeclarationText += ItemText;

                    if (IsVerilogNamerKeyword(ItemText) || IsVerilogBracket(ItemText) || IsNumeric(ItemText) || IsVerilogValue(ItemText) || IsDelimiter(ItemText))
                    {
                        // nothing at this time; we are still bulding the declaration part
                        // thisModuleParameterText += ItemText;
                    }
                    else
                    {
                        thisHoverName = ItemText;
                    }
                    break;
            }
        }

        private static void Process_ModuleParameterMimicNaming_For(string ItemText)
        {
            // once we are naming a module parameter, we only end with a closing parenthesis, or a comman
            switch (ItemText)
            {
                case "":
                    thisModuleDeclarationText += " ";
                    // thisModuleParameterText += " ";
                    break;

                case ")":
                    thisModuleDeclarationText += ItemText;
                    AddHoverItem(thisModuleName, thisModuleDeclarationText);

                    // also add an indivisual parameter as needed
                    AddHoverItem(thisHoverName, thisModuleParameterText);
                    thisModuleParameterText = ""; // upon the colose parenthesis, no more module parameters
                    BuildHoverState = BuildHoverStates.UndefinedState; // and no more module definition
                    break;

                case ",":
                    thisModuleDeclarationText += ItemText; // only the module declaration will include the comment
                    thisModuleDeclarationText += System.Environment.NewLine;

                    // add the module parameter
                    AddHoverItem(thisHoverName, thisModuleParameterText);
                    thisModuleParameterText = thisModuleParameterText.Replace(thisHoverName, "");
                    break;

                case ";":
                    thisModuleDeclarationText += ItemText; // only the module declaration will include the comment
                    thisModuleDeclarationText += System.Environment.NewLine;

                    // add the module parameter
                    AddHoverItem(thisHoverName, thisModuleParameterText);
                    thisModuleParameterText = "";
                    BuildHoverState = BuildHoverStates.ModuleParameterNaming; // certainly not mimic naming after a semi-colon!
                    break;

                default:
                    // thisModuleParameterText += ItemText;
                    thisModuleDeclarationText += ItemText;

                    if (IsVerilogNamerKeyword(ItemText) ||  IsVerilogBracket(ItemText) || IsNumeric(ItemText) || IsVerilogValue(ItemText) || IsDelimiter(ItemText))
                    {
                        // no longer mimic naming
                        BuildHoverState = BuildHoverStates.ModuleParameterNaming;
                        thisModuleParameterText = ItemText; // start over for the module parameter
                    }
                    else
                    {
                        thisHoverName = ItemText;
                        thisModuleParameterText += ItemText;
                    }
                    break;
            }
        }
        

        private static void Process_ModuleCloseParen_For(string ItemText)
        {
            if (1 == 1)
            {
                BuildHoverState = BuildHoverStates.UndefinedState;
            }
            else
            {
                //syntax error
            }
        }

        private static void Process_VariableNaming_For(string ItemText)
        {
            // once we are naming a module parameter, we only end with a closing parenthesis, or a comman
            switch (ItemText)
            {
                case "":
                    thisVariableDeclarationText += " ";
                    break;

                case ";":
                    AddHoverItem(thisHoverName, thisVariableDeclarationText);
                    thisVariableDeclarationText = "";
                    BuildHoverState = BuildHoverStates.UndefinedState;
                    break;

                case ",":
                    BuildHoverState = BuildHoverStates.VariableMimicNaming;
                    break;

                default:
                    if (IsVerilogBracket(ItemText) || IsNumeric(ItemText) || IsVerilogValue(ItemText) || IsDelimiter(ItemText))
                    {
                        // nothing at this time; we are still bulding the declaration part
                        thisVariableDeclarationText += ItemText;
                    }
                    else
                    {
                        thisHoverName = ItemText;
                        thisVariableDeclarationText += ItemText;
                    }
                    break;
            }
        }


        private static void Process_VariableMimicNaming_For(string ItemText)
        {
            // once we are naming a module parameter, we only end with a closing parenthesis, or a comman
            switch (ItemText)
            {
                case "":
                    thisVariableDeclarationText += " ";
                    break;

                case ";":
                    AddHoverItem(thisHoverName, thisVariableDeclarationText);
                    thisVariableDeclarationText = "";
                    BuildHoverState = BuildHoverStates.UndefinedState;
                    break;

                default:
                    if (IsVerilogBracket(ItemText) || IsNumeric(ItemText) || IsVerilogValue(ItemText) || IsDelimiter(ItemText))
                    {
                        // nothing at this time; we are still bulding the declaration part
                        thisVariableDeclarationText += ItemText;
                    }
                    else
                    {
                        thisHoverName = ItemText;
                        thisVariableDeclarationText += ItemText;
                    }
                    break;
            }
        }
        private static void Process_XXX_For(string ItemText)
        {
            if (1 == 1)
            {
                BuildHoverState = BuildHoverStates.UndefinedState;
            }
            else
            {
                //syntax error
            }
        }

 

        public enum BuildHoverStates
        {
            UndefinedState,        // typically the beginnging, or text we are not processing
            ModuleStart,           // we found the ,pdule keyword, so we are naming a module
            ModuleNamed,           // we found Module + ModuleName, specting parenthesis
            ModuleOpenParen,       // we found the module open paranthesis
            ModuleParameterNaming, // while naming a module, we found a parameter
            ModuleParameterMimicNaming, // while naming a module, we found a parameter after a comma with the same definition
            ModuleCloseParen,      // we found the module open paranthesis
            VariableNaming,        // we found a keyword to initiate or continue variable decaration
            VariableMimicNaming,
        };


        public static void BuildHoverItems(string s)
        {
            string thisTrimmedItem = (s == null) ? "" : s.Trim();

            switch (BuildHoverState)
            {
                case BuildHoverStates.UndefinedState:
                    Process_UndefinedState_For(thisTrimmedItem);
                    break;

                case BuildHoverStates.ModuleStart:
                    Process_ModuleStart_For(thisTrimmedItem);
                    break;

                case BuildHoverStates.ModuleNamed:
                    Process_ModuleNamed_For(thisTrimmedItem);
                    break;

                case BuildHoverStates.ModuleOpenParen:
                    Process_ModuleOpenParen_For(thisTrimmedItem);
                    break;

                case BuildHoverStates.ModuleParameterNaming:
                    Process_ModuleParameterNaming_For(thisTrimmedItem);
                    break;

                case BuildHoverStates.ModuleParameterMimicNaming:
                    Process_ModuleParameterMimicNaming_For(thisTrimmedItem);
                    break;

                case BuildHoverStates.ModuleCloseParen:
                    Process_ModuleCloseParen_For(thisTrimmedItem);
                    break;

                case BuildHoverStates.VariableNaming:
                    Process_VariableNaming_For(thisTrimmedItem);
                    break;

                case BuildHoverStates.VariableMimicNaming:
                    Process_VariableMimicNaming_For(thisTrimmedItem);
                    break;

                default:
                    break;
            }



        }

        /// <summary>
        ///  BuildHoverItems - builds the Verilog variable hover text. Called in IEnumerable VerilogTokenTagger
        ///                    as each token text string is encountered.
        /// </summary>
        /// <param name="s"></param>

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
