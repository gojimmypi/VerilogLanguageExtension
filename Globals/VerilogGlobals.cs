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

        /// <summary>
        ///   VerilogTypes
        /// </summary>
        // see also VerilogClassifier that has Dictionary<VerilogTokenTypes, IClassificationType>
        public static IDictionary<string, VerilogTokenTypes> VerilogTypes = new Dictionary<string, VerilogTokenTypes>
        {
            ["always"] = VerilogTokenTypes.Verilog_always,
            ["assign"] = VerilogTokenTypes.Verilog_assign,
            ["automatic"] = VerilogTokenTypes.Verilog_automatic,
            ["begin"] = VerilogTokenTypes.Verilog_begin,
            ["case"] = VerilogTokenTypes.Verilog_case,
            ["casex"] = VerilogTokenTypes.Verilog_casex,
            ["casez"] = VerilogTokenTypes.Verilog_casez,
            ["cell"] = VerilogTokenTypes.Verilog_cell,
            ["config"] = VerilogTokenTypes.Verilog_config,
            ["deassign"] = VerilogTokenTypes.Verilog_deassign,
            ["default"] = VerilogTokenTypes.Verilog_default,
            ["defparam"] = VerilogTokenTypes.Verilog_defparam,
            ["design"] = VerilogTokenTypes.Verilog_design,
            ["disable"] = VerilogTokenTypes.Verilog_disable,
            ["edge"] = VerilogTokenTypes.Verilog_edge,
            ["else"] = VerilogTokenTypes.Verilog_else,
            ["end"] = VerilogTokenTypes.Verilog_end,
            ["endcase"] = VerilogTokenTypes.Verilog_endcase,
            ["endconfig"] = VerilogTokenTypes.Verilog_endconfig,
            ["endfunction"] = VerilogTokenTypes.Verilog_endfunction,
            ["endgenerate"] = VerilogTokenTypes.Verilog_endgenerate,
            ["endmodule"] = VerilogTokenTypes.Verilog_endmodule,
            ["endprimitive"] = VerilogTokenTypes.Verilog_endprimitive,
            ["endspecify"] = VerilogTokenTypes.Verilog_endspecify,
            ["endtable"] = VerilogTokenTypes.Verilog_endtable,
            ["endtask"] = VerilogTokenTypes.Verilog_endtask,
            ["event"] = VerilogTokenTypes.Verilog_event,
            ["for"] = VerilogTokenTypes.Verilog_for,
            ["force"] = VerilogTokenTypes.Verilog_force,
            ["forever"] = VerilogTokenTypes.Verilog_forever,
            ["fork"] = VerilogTokenTypes.Verilog_fork,
            ["function"] = VerilogTokenTypes.Verilog_function,
            ["generate"] = VerilogTokenTypes.Verilog_generate,
            ["genvar"] = VerilogTokenTypes.Verilog_genvar,
            ["if"] = VerilogTokenTypes.Verilog_if,
            ["ifnone"] = VerilogTokenTypes.Verilog_ifnone,
            ["incdir"] = VerilogTokenTypes.Verilog_incdir,
            ["include"] = VerilogTokenTypes.Verilog_include,
            ["initial"] = VerilogTokenTypes.Verilog_initial,
            ["inout"] = VerilogTokenTypes.Verilog_inout,
            ["input"] = VerilogTokenTypes.Verilog_input,
            ["instance"] = VerilogTokenTypes.Verilog_instance,
            ["join"] = VerilogTokenTypes.Verilog_join,
            ["liblist"] = VerilogTokenTypes.Verilog_liblist,
            ["library"] = VerilogTokenTypes.Verilog_library,
            ["localparam"] = VerilogTokenTypes.Verilog_localparam,
            ["macromodule"] = VerilogTokenTypes.Verilog_macromodule,
            ["module"] = VerilogTokenTypes.Verilog_module,
            ["negedge"] = VerilogTokenTypes.Verilog_negedge,
            ["noshowcancelled"] = VerilogTokenTypes.Verilog_noshowcancelled,
            ["output"] = VerilogTokenTypes.Verilog_output,
            ["parameter"] = VerilogTokenTypes.Verilog_parameter,
            ["posedge"] = VerilogTokenTypes.Verilog_posedge,
            ["primitive"] = VerilogTokenTypes.Verilog_primitive,
            ["pulsestyle_ondetect"] = VerilogTokenTypes.Verilog_pulsestyle_ondetect,
            ["pulsestyle_onevent"] = VerilogTokenTypes.Verilog_pulsestyle_onevent,
            ["reg"] = VerilogTokenTypes.Verilog_reg,
            ["release"] = VerilogTokenTypes.Verilog_release,
            ["repeat"] = VerilogTokenTypes.Verilog_repeat,
            ["scalared"] = VerilogTokenTypes.Verilog_scalared,
            ["showcancelled"] = VerilogTokenTypes.Verilog_showcancelled,
            ["signed"] = VerilogTokenTypes.Verilog_signed,
            ["specify"] = VerilogTokenTypes.Verilog_specify,
            ["specparam"] = VerilogTokenTypes.Verilog_specparam,
            ["strength"] = VerilogTokenTypes.Verilog_strength,
            ["table"] = VerilogTokenTypes.Verilog_table,
            ["task"] = VerilogTokenTypes.Verilog_task,
            ["tri"] = VerilogTokenTypes.Verilog_tri,
            ["tri0"] = VerilogTokenTypes.Verilog_tri0,
            ["tri1"] = VerilogTokenTypes.Verilog_tri1,
            ["triand"] = VerilogTokenTypes.Verilog_triand,
            ["wand"] = VerilogTokenTypes.Verilog_wand,
            ["trior"] = VerilogTokenTypes.Verilog_trior,
            ["wor"] = VerilogTokenTypes.Verilog_wor,
            ["trireg"] = VerilogTokenTypes.Verilog_trireg,
            ["unsigned"] = VerilogTokenTypes.Verilog_unsigned,
            ["use"] = VerilogTokenTypes.Verilog_use,
            ["vectored"] = VerilogTokenTypes.Verilog_vectored,
            ["wait"] = VerilogTokenTypes.Verilog_wait,
            ["while"] = VerilogTokenTypes.Verilog_while,
            ["wire"] = VerilogTokenTypes.Verilog_wire,

            // all of the Verilog directives are the same color
            ["`celldefine"] = VerilogTokenTypes.Verilog_Directive,
            ["`endcelldefine"] = VerilogTokenTypes.Verilog_Directive,
            ["`default_nettype"] = VerilogTokenTypes.Verilog_Directive,
            ["`define"] = VerilogTokenTypes.Verilog_Directive,
            ["`undef"] = VerilogTokenTypes.Verilog_Directive,
            ["`ifdef"] = VerilogTokenTypes.Verilog_Directive,
            ["`ifndef"] = VerilogTokenTypes.Verilog_Directive,
            ["`elsif"] = VerilogTokenTypes.Verilog_Directive,
            ["`else"] = VerilogTokenTypes.Verilog_Directive,
            ["`endif"] = VerilogTokenTypes.Verilog_Directive,
            ["`include"] = VerilogTokenTypes.Verilog_Directive,
            ["`resetall"] = VerilogTokenTypes.Verilog_Directive,
            ["`line"] = VerilogTokenTypes.Verilog_Directive,
            ["`timescale"] = VerilogTokenTypes.Verilog_Directive,
            ["`unconnected_drive"] = VerilogTokenTypes.Verilog_Directive,
            ["`nounconnected_driv"] = VerilogTokenTypes.Verilog_Directive,

            ["comment_type"] = VerilogTokenTypes.Verilog_Comment,

            ["bracket_type"] = VerilogTokenTypes.Verilog_Bracket,
            ["bracket_type0"] = VerilogTokenTypes.Verilog_Bracket0,
            ["bracket_type1"] = VerilogTokenTypes.Verilog_Bracket1,
            ["bracket_type2"] = VerilogTokenTypes.Verilog_Bracket2,
            ["bracket_type3"] = VerilogTokenTypes.Verilog_Bracket3,
            ["bracket_type4"] = VerilogTokenTypes.Verilog_Bracket4,
            ["bracket_type5"] = VerilogTokenTypes.Verilog_Bracket5,
            ["bracket_content"] = VerilogTokenTypes.Verilog_BracketContent,

            // generic variable type
            ["variable_type"] = VerilogTokenTypes.Verilog_Variable,

            // specific variable name types
            ["variable_input"] = VerilogTokenTypes.Verilog_Variable_input,
            ["variable_output"] = VerilogTokenTypes.Verilog_Variable_output,
            ["variable_inout"] = VerilogTokenTypes.Verilog_Variable_inout,
            ["variable_wire"] = VerilogTokenTypes.Verilog_Variable_wire,
            ["variable_reg"] = VerilogTokenTypes.Verilog_Variable_reg,
            ["variable_parameter"] = VerilogTokenTypes.Verilog_Variable_parameter,
            ["variable_duplicate"] = VerilogTokenTypes.Verilog_Variable_duplicate,

            ["value_type"] = VerilogTokenTypes.Verilog_Value,
        };


        public static bool NeedReparse { get; set; }
        public static Boolean IsContinuedBlockComment = false;

        private static BuildHoverStates _BuildHoverState = BuildHoverStates.UndefinedState;

        /// <summary>
        ///    IsDefinedVerilogVariable
        /// </summary>
        /// <param name="VariableName"></param>
        /// <returns></returns>
        public static bool IsDefinedVerilogVariable(string VariableName)
        {
            return VerilogVariables.ContainsKey(VariableName);
        }

        public static BuildHoverStates BuildHoverState {
            get
            {
                return _BuildHoverState;
            }
            set
            {
                _BuildHoverState = value;
                if (value == BuildHoverStates.UndefinedState)
                {
                    thisHoverName = ""; // we can never have a name, during an unknown state!
                }
            }
        }
        // BuildHoverStates.UndefinedState;

        private static string thisHoverName = "";
        private static string lastHoverItem = "";
        private static string thisVariableDeclarationText = "";
        private static string thisModuleName = "";
        private static string thisModuleDeclarationText = "";
        private static string thisModuleParameterText = "";
        private static bool IsInsideSquareBracket = false;
        private static VerilogTokenTypes thisVariableType = VerilogTokenTypes.Verilog_Variable;

        /// <summary>
        ///   InitHoverBuilder - prep for another refresh of hover item lookup
        /// </summary>
        public static void InitHoverBuilder()
        {
            // re-initialize variables to above values
            VerilogVariableHoverText = new Dictionary<string, string> { };
            VerilogVariables = new Dictionary<string, VerilogTokenTypes> { };
            IsContinuedBlockComment = false;
            thisHoverName = "";

            thisVariableDeclarationText = ""; // this is only variable declaration, even if inside a module declaration

            thisModuleDeclarationText = ""; // this is the full module declaration
            thisModuleParameterText = "";
            thisModuleName = "";

            BuildHoverState = BuildHoverStates.UndefinedState;
        }

        /// <summary>
        ///    AddHoverItem
        /// </summary>
        /// <param name="ItemName"></param>
        /// <param name="HoverText"></param>
        private static void AddHoverItem(string ItemName, string HoverText)
        {
            if (IsDelimiter(ItemName) || ItemName == "")
            {
                // never add a blank & never add a delimiter TODO - why would we even try? unresolved declaration naming?
            }
            else
            {
                // first add the token type; hover text added below to separate collection
                if (VerilogVariables.Keys.Contains(ItemName))
                {
                    // edit existing, TODO - new color for dupes?
                    VerilogGlobals.VerilogVariables[ItemName] = VerilogTokenTypes.Verilog_Variable_duplicate;
                }
                else
                {
                    // add new
                    VerilogVariables.Add(ItemName, thisVariableType);
                }
                string thisHoverText = HoverText;


                // next add the hover text
                switch (BuildHoverState)
                {
                    // in the case of module parameters, we'll add the keyword "module" and module name to the hover text:
                    // e.g. "module myModule( thisHoverText )"
                    case BuildHoverStates.ModuleParameterNaming:
                    case BuildHoverStates.ModuleParameterMimicNaming:
                        thisHoverText = "module " + thisModuleName + "( .. " + thisHoverText + " .. )";
                        // there may be more parameters, so we're not adding it how
                        break;

                    // otherwise no special processing needed for the hover text, we'll use it as-is
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
            } // else
        } // AddHoverItem

        #region "BuildHoverItems - State Handler"

        private static void SetBracketContentStatus_For(string ItemText)
        {
            switch (ItemText)
            {
                case "[":
                    IsInsideSquareBracket = true;
                    break;
                case "]":
                    IsInsideSquareBracket = false;
                    break;
                default:
                    // nothing
                    break;
            }
        }

        private static bool Is_BracketContent_For(string ItemText)
        {
            return IsInsideSquareBracket && IsDefinedVerilogVariable(ItemText);
        }

        /// <summary>
        ///   Process_UndefinedState_For
        /// </summary>
        /// <param name="ItemText"></param>
        private static void Process_UndefinedState_For(string ItemText)
        {
            switch (ItemText)
            {
                case "":
                    // ignoring trimmed spaces / blanks
                    break;

                case "module":
                    // we're naming a module
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

                    thisVariableType = VerilogGlobals.VerilogTypes["variable_" + ItemText];
                    break;

                default:
                    BuildHoverState = BuildHoverStates.UndefinedState;
                    break;
            }
        }

        /// <summary>
        ///    Process_ModuleStart_For
        /// </summary>
        /// <param name="ItemText"></param>
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

        /// <summary>
        ///    Process_ModuleNamed_For
        /// </summary>
        /// <param name="ItemText"></param>
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

        /// <summary>
        ///    Process_ModuleParameterNaming_For
        /// </summary>
        /// <param name="ItemText"></param>
        private static void Process_ModuleParameterNaming_For(string ItemText)
        {
            // once we are naming a module parameter, we only end with a closing parenthesis, or a comman
            switch (ItemText)
            {
                case "":
                    // only append whitespace when not found at beginning
                    if (thisModuleParameterText != "")
                    {
                        if ((lastHoverItem == "") || (lastHoverItem == "\t"))
                        {
                            // we'll ignore sequentual tabs, or alternating table-space
                            // only one space will be used
                        }
                        else
                        {
                            thisModuleDeclarationText += " ";
                            thisModuleParameterText += " ";
                        }
                    }
                    break;

                case "\t":
                    if ((lastHoverItem == "") || (lastHoverItem == "\t")) {
                        // we'll ignore sequentual tabs, or alternating table-space
                        // only one space will be used
                    }
                    else
                    {
                        thisModuleDeclarationText += " ";
                        thisModuleParameterText += " ";
                    }
                    break;

                case ")":

                    // also add an indivisual parameter as needed
                    // note all module parameters have test appended: "module [modulename]" + {}  + ")"
                    AddHoverItem(thisHoverName, thisModuleParameterText);
                    thisModuleParameterText = ""; // upon the colose parenthesis, no more module parameters
                    BuildHoverState = BuildHoverStates.UndefinedState; // and no more module definition

                    // we add the module definition afterwards to avoid any additional, manually added closing ")" that is included for *every( module parameter, but not actually in the text
                    thisModuleDeclarationText += ItemText;
                    AddHoverItem(thisModuleName, thisModuleDeclarationText);
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
                        SetBracketContentStatus_For(ItemText);
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

        /// <summary>
        ///   Process_ModuleParameterMimicNaming_For
        /// </summary>
        /// <param name="ItemText"></param>
        private static void Process_ModuleParameterMimicNaming_For(string ItemText)
        {
            // once we are naming a module parameter, we only end with a closing parenthesis, or a comman
            switch (ItemText)
            {
                case "":
                    if ((lastHoverItem == "") || (lastHoverItem == "\t"))
                    {
                        // we'll ignore sequentual tabs, or alternating table-space
                        // only one space will be used
                    }
                    else
                    {
                        thisModuleDeclarationText += " ";
                    }

                    // thisModuleParameterText += " ";
                    break;

                case "\t":
                    if ((lastHoverItem == "") || (lastHoverItem == "\t"))
                    {
                        // we'll ignore sequentual tabs, or alternating table-space
                        // only one space will be used
                    }
                    else
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
                        SetBracketContentStatus_For(ItemText);

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

        /// <summary>
        ///    Process_ModuleCloseParen_For
        /// </summary>
        /// <param name="ItemText"></param>
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

        /// <summary>
        ///    Process_VariableNaming_For
        /// </summary>
        /// <param name="ItemText"></param>
        private static void Process_VariableNaming_For(string ItemText)
        {
            // once we are naming a module parameter, we only end with a closing parenthesis, or a comman
            switch (ItemText)
            {
                case "":
                    if ((lastHoverItem == "") || (lastHoverItem == "\t"))
                    {
                        // we'll ignore sequentual tabs, or alternating table-space
                        // only one space will be used
                    }
                    else
                    {
                        thisVariableDeclarationText += " ";
                    }

                    break;

                case ";":
                    AddHoverItem(thisHoverName, thisVariableDeclarationText);
                    thisVariableDeclarationText = ""; // reminder we do this manually, as AddHoverItem does not know *what* it is adding
                    BuildHoverState = BuildHoverStates.UndefinedState;
                    break;

                case ",":
                    AddHoverItem(thisHoverName, thisVariableDeclarationText);
                    thisVariableDeclarationText = thisVariableDeclarationText.Replace(thisHoverName, "");

                    BuildHoverState = BuildHoverStates.VariableMimicNaming;
                    break;

                default:
                    // TODO implement IsVerilogAssignment
                    if ((thisHoverName != "") || (ItemText == "=") || IsVerilogBracket(ItemText) || IsNumeric(ItemText) || IsVerilogValue(ItemText) || Is_BracketContent_For(ItemText) || IsDelimiter(ItemText))
                    {
                        SetBracketContentStatus_For(ItemText);
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

        /// <summary>
        ///    Process_VariableMimicNaming_For
        /// </summary>
        /// <param name="ItemText"></param>
        private static void Process_VariableMimicNaming_For(string ItemText)
        {
            // once we are naming a module parameter, we only end with a closing parenthesis, or a comman
            switch (ItemText)
            {
                case "":
                    if ((lastHoverItem == "") || (lastHoverItem == "\t") || (lastHoverItem == ","))
                    {
                        // we'll ignore sequentual tabs, or alternating table-space, commas mean we are mimicing prior definition
                        // only one space will be used
                    }
                    else
                    {
                        thisVariableDeclarationText += " ";
                    }
                    break;

                case ",":
                    AddHoverItem(thisHoverName, thisVariableDeclarationText);
                    thisVariableDeclarationText = thisVariableDeclarationText.Replace(thisHoverName, "");
                    break;

                case ";":
                    AddHoverItem(thisHoverName, thisVariableDeclarationText);
                    thisVariableDeclarationText = "";
                    BuildHoverState = BuildHoverStates.UndefinedState;
                    break;

                default:
                    if (IsVerilogBracket(ItemText) || IsNumeric(ItemText) || IsVerilogValue(ItemText) || IsDelimiter(ItemText))
                    {
                        SetBracketContentStatus_For(ItemText);
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

        /// <summary>
        ///    Process_XXX_For - template
        /// </summary>
        /// <param name="ItemText"></param>
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
        #endregion



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
            lastHoverItem = thisTrimmedItem;


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
