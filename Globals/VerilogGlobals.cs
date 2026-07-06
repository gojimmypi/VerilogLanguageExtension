// file: Globals/VerilogGlobals.cs
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
        public const string SCOPE_CONST = "__CONST__";
        public const string SCOPE_MACRO = "__MACRO__";
        public const string SCOPE_FUNCTION_PREFIX = "__FUNCTION__";
        public const string SCOPE_TASK_PREFIX = "__TASK__";
        public const char RADIX_CHAR = '\'';
        public static ITextBuffer TheBuffer;
        public static ITextView TheView; // assigned in QuickInfoControllerProvider see https://docs.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.text.editor.itextview?redirectedfrom=MSDN&view=visualstudiosdk-2017

        /// <summary>
        /// Warning, includes underscores, Z, X
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static bool IsVerilogNumberChar(char c) {
            return VerilogBinaryChars.Contains(c)
                || VerilogDecimalChars.Contains(c)
                || VerilogHexChars.Contains(c)
                || VerilogOctalChars.Contains(c);
        }

        public static bool IsVerilogNumberStart(char c) {
            // Sized literal: 32'hFF
            if (c >= '0' && c <= '9')
                return true;

            // Unsized literal: 'hFF, 'b0
            if (c == RADIX_CHAR)
                return true;

            return false;
        }

        private static bool IsIdentifier(string s) {
            if (string.IsNullOrEmpty(s)) {
                return false;
            }

            char c0 = s[0];
            if (!(c0 == '_' || (c0 >= 'A' && c0 <= 'Z') || (c0 >= 'a' && c0 <= 'z'))) {
                return false;
            }

            for (int i = 1; i < s.Length; i++) {
                char c = s[i];
                if (!(c == '_'
                        || (c >= 'A' && c <= 'Z')
                        || (c >= 'a' && c <= 'z')
                        || (c >= '0' && c <= '9'))) {
                    return false;
                }
            }

            return true;
        }

        private static bool IsVerilogIdentifierChar(char c) {
            return c == '_'
                || (c >= 'A' && c <= 'Z')
                || (c >= 'a' && c <= 'z')
                || (c >= '0' && c <= '9');
        }

        private static bool IsDeclarationKeywordPrefixBoundary(char c) {
            return char.IsWhiteSpace(c) || c == '(' || c == ',' || c == ';' || c == '#';
        }

        private static bool ContainsDeclarationKeyword(string text, string keyword) {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword)) {
                return false;
            }

            int searchStart = 0;
            while (searchStart < text.Length) {
                int index = text.IndexOf(keyword, searchStart, StringComparison.Ordinal);
                if (index < 0) {
                    return false;
                }

                bool validPrefix = index == 0 || IsDeclarationKeywordPrefixBoundary(text[index - 1]);
                int afterIndex = index + keyword.Length;
                bool validSuffix = afterIndex >= text.Length || !IsVerilogIdentifierChar(text[afterIndex]);

                if (validPrefix && validSuffix) {
                    return true;
                }

                searchStart = index + keyword.Length;
            }

            return false;
        }

        public static bool TryGetDeclarationVariableTypeFromText(string text, out VerilogTokenTypes variableType) {
            variableType = VerilogTokenTypes.Verilog_Variable;

            if (string.IsNullOrEmpty(text)) {
                return false;
            }

            if (ContainsDeclarationKeyword(text, "localparam")) {
                variableType = VerilogTokenTypes.Verilog_Variable_localparam;
                return true;
            }

            if (ContainsDeclarationKeyword(text, "parameter")) {
                variableType = VerilogTokenTypes.Verilog_Variable_parameter;
                return true;
            }

            if (ContainsDeclarationKeyword(text, "reg")
                    || ContainsDeclarationKeyword(text, "integer")
                    || ContainsDeclarationKeyword(text, "logic")
                    || ContainsDeclarationKeyword(text, "bit")) {
                variableType = VerilogTokenTypes.Verilog_Variable_reg;
                return true;
            }

            if (ContainsDeclarationKeyword(text, "wire")
                    || ContainsDeclarationKeyword(text, "tri")
                    || ContainsDeclarationKeyword(text, "tri0")
                    || ContainsDeclarationKeyword(text, "tri1")
                    || ContainsDeclarationKeyword(text, "triand")
                    || ContainsDeclarationKeyword(text, "trior")
                    || ContainsDeclarationKeyword(text, "trireg")
                    || ContainsDeclarationKeyword(text, "wand")
                    || ContainsDeclarationKeyword(text, "wor")) {
                variableType = VerilogTokenTypes.Verilog_Variable_wire;
                return true;
            }

            if (ContainsDeclarationKeyword(text, "inout")) {
                variableType = VerilogTokenTypes.Verilog_Variable_inout;
                return true;
            }

            if (ContainsDeclarationKeyword(text, "output")) {
                variableType = VerilogTokenTypes.Verilog_Variable_output;
                return true;
            }

            if (ContainsDeclarationKeyword(text, "input")) {
                variableType = VerilogTokenTypes.Verilog_Variable_input;
                return true;
            }

            return false;
        }

        /// <summary>
        ///   VerilogVariableHoverText - dictionary collection of keywords and hover text (variable names and definitions)
        /// </summary>
        public static Dictionary<string, Dictionary<string, string>> VerilogVariableHoverText = new Dictionary<string, Dictionary<string, string>>
        {
            {
                "global", // the name of the module, or "global" default
                new Dictionary<string, string>{ }
            }
            // e.g. ["led"] = "An LED."
        };

        /// <summary>
        ///   VerilogVariables - a list of modules, each with variables found in the text that will be have hover text (see VerilogVariableHoverText)
        /// </summary>
        public static IDictionary<string, Dictionary<string, VerilogTokenTypes>> VerilogVariables = new Dictionary<string, Dictionary<string, VerilogTokenTypes>>
        {
            {
               "global", // the name of the module, or "global" default
               new Dictionary<string, VerilogTokenTypes>{ }
            }

            // e.g. ["module name"]["led"] = VerilogTokenTypes.Verilog_Variable,
        };

        public static Dictionary<string, Dictionary<string, VerilogDefinitionLocation>> VerilogDefinitionLocations = new Dictionary<string, Dictionary<string, VerilogDefinitionLocation>>
        {
            {
               "global",
               new Dictionary<string, VerilogDefinitionLocation>{ }
            },
            {
               SCOPE_MACRO,
               new Dictionary<string, VerilogDefinitionLocation>{ }
            }
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

            // Some System Verilog Specific keywords
            ["bit"] = VerilogTokenTypes.Verilog_bit,


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

            ["macro_type"] = VerilogTokenTypes.Verilog_Macro,
            ["macro_definition"] = VerilogTokenTypes.Verilog_MacroDefinition,

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
            ["variable_localparam"] = VerilogTokenTypes.Verilog_Variable_localparam,
            ["variable_parameter"] = VerilogTokenTypes.Verilog_Variable_parameter,
            ["variable_duplicate"] = VerilogTokenTypes.Verilog_Variable_duplicate,
            ["variable_module"] = VerilogTokenTypes.Verilog_Variable_module,

            ["static_string"] = VerilogTokenTypes.Verilog_StaticString,
            ["function_name"] = VerilogTokenTypes.Verilog_FunctionName,

            // primitives
            ["and"] = VerilogTokenTypes.Verilog_Primitive_and,
            ["nand"] = VerilogTokenTypes.Verilog_Primitive_nand,
            ["or"] = VerilogTokenTypes.Verilog_Primitive_or,
            ["nor"] = VerilogTokenTypes.Verilog_Primitive_nor,
            ["xor"] = VerilogTokenTypes.Verilog_Primitive_xor,
            ["xnor"] = VerilogTokenTypes.Verilog_Primitive_xnor,
            ["not"] = VerilogTokenTypes.Verilog_Primitive_not,

            ["value_type"] = VerilogTokenTypes.Verilog_Value,
        };


        // public static bool NeedReparse { get; set; }
        // public static DateTime LastParseTime {get; set; }
        public static DateTime LastKeypressTime { get; set; }
        public static Boolean IsContinuedBlockComment = false;

        private static BuildHoverStates _BuildHoverState = BuildHoverStates.UndefinedState;

        public static string GetDocumentPath(Microsoft.VisualStudio.Text.ITextSnapshot ts) {
            Microsoft.VisualStudio.Text.ITextDocument textDoc;
            bool rc = ts.TextBuffer.Properties.TryGetProperty(
                typeof(Microsoft.VisualStudio.Text.ITextDocument), out textDoc);
            if (rc && textDoc != null)
                return textDoc.FilePath;
            return null;
        }


        /// <summary>
        ///    IsDefinedVerilogVariable
        /// </summary>
        /// <param name="VariableName"></param>
        /// <returns></returns>
        public static bool IsDefinedVerilogVariable(string thisScope, string VariableName) {
            if (VerilogVariables.ContainsKey(thisScope)) {
                // only check if the module dictionary thisScope exists
                return VerilogVariables[thisScope].ContainsKey(VariableName);
            }
            else {
                return false; // if we don't even have a scope, we certainly don't have a variable name in it!
            }

        }

        public static BuildHoverStates BuildHoverState
        {
            get
            {
                return _BuildHoverState;
            }
            set
            {
                _BuildHoverState = value;
                if (value == BuildHoverStates.UndefinedState) {
                    thisHoverName = string.Empty; // we can never have a name, during an unknown state!
                }
            }
        }
        // BuildHoverStates.UndefinedState;

        private static string thisHoverName = string.Empty;
        private static string lastHoverItem = string.Empty;
        private static string lastNonblankHoverItem = string.Empty;
        private static string thisVariableDeclarationText = string.Empty;
        private static string thisModuleName = string.Empty;
        private static string thisFunctionName = string.Empty;
        private static string thisFunctionScope = string.Empty;
        private static string thisModuleDeclarationText = string.Empty;
        private static string thisModuleParameterText = string.Empty;
        private static string thisItemText = string.Empty;
        private static int thisItemLineNumber = -1;
        private static int thisItemLinePosition = -1;
        private static int thisHoverNameLineNumber = -1;
        private static int thisHoverNameLinePosition = -1;
        private static int thisModuleNameLineNumber = -1;
        private static int thisModuleNameLinePosition = -1;
        private static bool IsInsideSquareBracket = false;
        private static bool IsInsideSquigglyBracket = false;
        private static VerilogTokenTypes thisVariableType = VerilogTokenTypes.Verilog_Variable;
        private sealed class ConditionalDefinitionCandidate
        {
            public string Scope { get; set; }
            public string Name { get; set; }
            public int GroupId { get; set; }
            public int BranchId { get; set; }
            public bool HasMacroReference { get; set; }
            public string HoverText { get; set; }
            public string DeclarationText { get; set; }
            public VerilogTokenTypes DeclarationType { get; set; }
        }

        private static readonly List<string> PreprocessorConditionStack = new List<string>();
        private static readonly List<List<string>> PreprocessorBranchTextStack = new List<List<string>>();
        private static readonly List<int> PreprocessorConditionalGroupIdStack = new List<int>();
        private static readonly List<int> PreprocessorConditionalBranchIdStack = new List<int>();
        private static readonly List<ConditionalDefinitionCandidate> PreprocessorConditionalDefinitionCandidates = new List<ConditionalDefinitionCandidate>();
        private static readonly Dictionary<string, bool> PreprocessorConditionalDuplicateSuppressions = new Dictionary<string, bool>();
        private static bool PreprocessorScanBlockCommentOpen = false;
        private static int PreprocessorNextConditionalGroupId = 1;

        /// <summary>
        ///   InitHoverBuilder - prep for another refresh of hover item lookup
        /// </summary>
        public static void InitHoverBuilder() {
            // re-initialize variables to above values
            if (VerilogVariableHoverText != null) {
                System.Diagnostics.Debug.WriteLine("destroying VerilogVariableHoverText!");

            }
            VerilogVariableHoverText = new Dictionary<string, Dictionary<string, string>>
            // e.g. ["led"] = "An LED."
            {
                {
                    "global",
                    new Dictionary<string, string>{ }
                },
                {
                    SCOPE_MACRO,
                    new Dictionary<string, string>{ }
                }
            };

            // if (VerilogVariables == null || VerilogVariables.Count < 1 || VerilogVariables.ContainsKey("globals"))
            // we need to always rebuild this, as how would we otherwise know if there are *duplicates* ?
            // TODO - come up with something better to allow keeping what we already know, but also detect dupes
            {

                VerilogVariables = new Dictionary<string, Dictionary<string, VerilogTokenTypes>>
                // e.g. ["module name"]["led"] = VerilogTokenTypes.Verilog_Variable,
                {
                    {
                        "global",
                        new Dictionary<string, VerilogTokenTypes>{ }
                    },
                    {
                        SCOPE_MACRO,
                        new Dictionary<string, VerilogTokenTypes>{ }
                    }
                };
            }

            VerilogDefinitionLocations = new Dictionary<string, Dictionary<string, VerilogDefinitionLocation>>
            {
                {
                    "global",
                    new Dictionary<string, VerilogDefinitionLocation>{ }
                },
                {
                    SCOPE_MACRO,
                    new Dictionary<string, VerilogDefinitionLocation>{ }
                }
            };

            thisHoverName = string.Empty;

            thisVariableDeclarationText = string.Empty; // this is only variable declaration, even if inside a module declaration

            thisModuleDeclarationText = string.Empty; // this is the full module declaration
            thisModuleParameterText = string.Empty;
            thisModuleName = string.Empty;
            thisFunctionName = string.Empty;
            thisFunctionScope = string.Empty;
            thisItemText = string.Empty;
            thisItemLineNumber = -1;
            thisItemLinePosition = -1;
            thisHoverNameLineNumber = -1;
            thisHoverNameLinePosition = -1;
            thisModuleNameLineNumber = -1;
            thisModuleNameLinePosition = -1;

            lastHoverItem = string.Empty;
            lastNonblankHoverItem = string.Empty;
            IsInsideSquareBracket = false;
            IsInsideSquigglyBracket = false;
            thisVariableType = VerilogTokenTypes.Verilog_Variable;

            PreprocessorConditionStack.Clear();
            PreprocessorBranchTextStack.Clear();
            PreprocessorConditionalGroupIdStack.Clear();
            PreprocessorConditionalBranchIdStack.Clear();
            PreprocessorConditionalDefinitionCandidates.Clear();
            PreprocessorConditionalDuplicateSuppressions.Clear();
            PreprocessorScanBlockCommentOpen = false;
            PreprocessorNextConditionalGroupId = 1;

            BuildHoverState = BuildHoverStates.UndefinedState;
        }

        public static string ValueHoverText(string s) {
            // we start with values like "4", "8 'h  2a"
            string _HoverItem = (s ?? "").Replace(" ", "").Replace("_", "").ToUpper(); // initially remove all spaces from hover text (h)
            string _HoverBase = string.Empty; // the base is the first characters after the single quote "'"
            string[] _HoverPart = _HoverItem.Split(RADIX_CHAR); // // split "8'h2a" into "8" and "h2a" - parts (p)
            string _HoverValue = string.Empty; // the actual value,
            string _HoverValueBinary = string.Empty;
            int _HoverBitLength = 0;
            string _HoverBitlength_Message = string.Empty;
            string _BitLengthWarning = string.Empty;
            if (_HoverPart.Length > 1) {
                _HoverValue = _HoverPart[1];
                if (_HoverValue.Length >= 2) {

                    if (int.TryParse(_HoverPart[0], out _HoverBitLength)) {
                        _HoverBitlength_Message = _HoverPart[0] + " bit";
                    }
                    _HoverBase = _HoverValue.Substring(0, 1);
                    _HoverValue = _HoverValue.Substring(1); // don't include the base character
                }

                switch (_HoverBase.ToUpper()) {
                    case "B":
                        if (_HoverValue.AllCharsIn(VerilogBinaryChars)) {
                            _HoverBase = "Binary";
                        }
                        else {
                            _HoverBase = "Invalid Binary";
                        }
                        break;
                    case "D":
                        if (_HoverValue.AllCharsIn(VerilogDecimalChars)) {
                            _HoverBase = "Decimal";
                        }
                        else {
                            _HoverBase = "Invalid Decimal";
                        }
                        break;
                    case "H":
                        if (_HoverValue.AllCharsIn(VerilogHexChars)) {
                            _HoverBase = "Hex";
                            _HoverValueBinary = _HoverValue.HexStringToBinary();
                            if (_HoverValueBinary.Length > _HoverBitLength) {
                                _BitLengthWarning = " Warning: Found " + _HoverValueBinary.Length.ToString() + " bits for " + _HoverBitLength.ToString() + " value!";
                            }
                        }
                        else {
                            _HoverBase = "Invalid Hex";
                        }
                        break;
                    case "O":
                        if (_HoverValue.AllCharsIn(VerilogOctalChars)) {
                            _HoverBase = "Octal";
                        }
                        else {
                            _HoverBase = "Invalid Octal";
                        }
                        break;
                    default:
                        _HoverBase = "(Not valid)";
                        break;
                }
                // result
            }
            return _HoverItem +
                    " = (" + _HoverBitlength_Message + " " + _HoverBase + ") "
                    + _HoverValue + "; " + _HoverValueBinary
                    + _BitLengthWarning;

        }

        /// <summary>
        ///    AddHoverItem
        /// </summary>
        /// <param name="ItemName"></param>
        /// <param name="HoverText"></param>
        private static void UpdateCurrentDeclarationVariableType(string text) {
            VerilogTokenTypes declarationVariableType;
            if (TryGetDeclarationVariableTypeFromText(text, out declarationVariableType)) {
                thisVariableType = declarationVariableType;
            }
        }

        private static VerilogTokenTypes GetDeclarationVariableTypeForHoverText(string hoverText) {
            VerilogTokenTypes declarationVariableType;
            if (TryGetDeclarationVariableTypeFromText(hoverText, out declarationVariableType)) {
                return declarationVariableType;
            }

            return thisVariableType;
        }

        private static VerilogTokenTypes GetDeclarationVariableTypeForConditionalText(string declarationText) {
            VerilogTokenTypes declarationVariableType;
            if (TryGetDeclarationVariableTypeFromText(declarationText, out declarationVariableType)) {
                return declarationVariableType;
            }

            return VerilogTokenTypes.Verilog_Variable;
        }

        private static bool IsDeclarationStartKeyword(string itemText) {
            switch (itemText) {
                case "input":
                case "output":
                case "inout":
                case "wire":
                case "tri":
                case "tri0":
                case "tri1":
                case "triand":
                case "trior":
                case "trireg":
                case "wand":
                case "wor":
                case "reg":
                case "logic":
                case "bit":
                case "integer":
                case "parameter":
                case "localparam":
                    return true;

                default:
                    return false;
            }
        }

        private static bool IsDeclarationModifierKeyword(string itemText) {
            return IsDeclarationStartKeyword(itemText) || IsVerilogVariableSigner(itemText);
        }

        private static string StripLineCommentForDuplicateScan(string lineText) {
            if (string.IsNullOrEmpty(lineText)) {
                return string.Empty;
            }

            int lineCommentIndex = lineText.IndexOf("//", StringComparison.Ordinal);
            if (lineCommentIndex >= 0) {
                return lineText.Substring(0, lineCommentIndex);
            }

            return lineText;
        }

        private static string NormalizeDeclarationDuplicateScope(string scope) {
            if (string.IsNullOrEmpty(scope)) {
                scope = "global";
            }

            if (VerilogVariables.ContainsKey(scope)) {
                return scope;
            }

            if (scope == "global" && VerilogVariables.ContainsKey(string.Empty)) {
                return string.Empty;
            }

            return scope;
        }

        public static string FunctionLocalScopeName(string moduleScope, string functionName) {
            string normalizedModuleScope = NormalizeDeclarationDuplicateScope(moduleScope);
            if (string.IsNullOrEmpty(functionName)) {
                return normalizedModuleScope;
            }

            return normalizedModuleScope + "::" + SCOPE_FUNCTION_PREFIX + "::" + functionName;
        }

        public static string TaskLocalScopeName(string moduleScope, string taskName) {
            string normalizedModuleScope = NormalizeDeclarationDuplicateScope(moduleScope);
            if (string.IsNullOrEmpty(taskName)) {
                return normalizedModuleScope;
            }

            return normalizedModuleScope + "::" + SCOPE_TASK_PREFIX + "::" + taskName;
        }

        public static string ParentScopeName(string scope) {
            if (string.IsNullOrEmpty(scope)) {
                return NormalizeDeclarationDuplicateScope(scope);
            }

            int functionScopeIndex = scope.IndexOf("::" + SCOPE_FUNCTION_PREFIX + "::", StringComparison.Ordinal);
            if (functionScopeIndex >= 0) {
                return NormalizeDeclarationDuplicateScope(scope.Substring(0, functionScopeIndex));
            }

            int taskScopeIndex = scope.IndexOf("::" + SCOPE_TASK_PREFIX + "::", StringComparison.Ordinal);
            if (taskScopeIndex >= 0) {
                return NormalizeDeclarationDuplicateScope(scope.Substring(0, taskScopeIndex));
            }

            return NormalizeDeclarationDuplicateScope(scope);
        }

        private static int FindStandaloneCodeKeyword(string lineText, string keyword) {
            string codeText = StripLineCommentForDuplicateScan(lineText);
            if (string.IsNullOrEmpty(codeText) || string.IsNullOrEmpty(keyword)) {
                return -1;
            }

            int searchStart = 0;
            while (searchStart < codeText.Length) {
                int index = codeText.IndexOf(keyword, searchStart, StringComparison.Ordinal);
                if (index < 0) {
                    return -1;
                }

                bool validPrefix = index == 0 || !IsVerilogIdentifierChar(codeText[index - 1]);
                int afterIndex = index + keyword.Length;
                bool validSuffix = afterIndex >= codeText.Length || !IsVerilogIdentifierChar(codeText[afterIndex]);

                if (validPrefix && validSuffix) {
                    return index;
                }

                searchStart = index + keyword.Length;
            }

            return -1;
        }

        private static bool CodeLineStartsWithDeclarationKeyword(string lineText) {
            string codeText = StripLineCommentForDuplicateScan(lineText);
            if (string.IsNullOrWhiteSpace(codeText)) {
                return false;
            }

            int index = 0;
            while (index < codeText.Length && char.IsWhiteSpace(codeText[index])) {
                index++;
            }

            int start = index;
            while (index < codeText.Length && IsVerilogIdentifierChar(codeText[index])) {
                index++;
            }

            if (index == start) {
                return false;
            }

            return IsDeclarationStartKeyword(codeText.Substring(start, index - start));
        }

        private static bool TryGetRoutineNameFromLineText(string lineText, string keyword, out string routineName) {
            routineName = string.Empty;

            int keywordIndex = FindStandaloneCodeKeyword(lineText, keyword);
            if (keywordIndex < 0) {
                return false;
            }

            string codeText = StripLineCommentForDuplicateScan(lineText);
            int index = keywordIndex + keyword.Length;
            int squareDepth = 0;

            while (index < codeText.Length) {
                char c = codeText[index];

                if (c == ';') {
                    return false;
                }

                if (c == '[') {
                    squareDepth++;
                    index++;
                    continue;
                }

                if (c == ']') {
                    if (squareDepth > 0) {
                        squareDepth--;
                    }
                    index++;
                    continue;
                }

                if (squareDepth > 0 || !IsVerilogIdentifierChar(c)) {
                    index++;
                    continue;
                }

                int nameStart = index;
                while (index < codeText.Length && IsVerilogIdentifierChar(codeText[index])) {
                    index++;
                }

                string candidate = codeText.Substring(nameStart, index - nameStart);
                if (IsFunctionReturnTypeToken(candidate)) {
                    continue;
                }

                if (IsIdentifier(candidate)) {
                    routineName = candidate;
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetFunctionNameFromLineText(string lineText, out string functionName) {
            return TryGetRoutineNameFromLineText(lineText, "function", out functionName);
        }

        public static bool TryGetTaskNameFromLineText(string lineText, out string taskName) {
            return TryGetRoutineNameFromLineText(lineText, "task", out taskName);
        }

        public static bool IsEndFunctionLineText(string lineText) {
            return FindStandaloneCodeKeyword(lineText, "endfunction") >= 0;
        }

        public static bool IsEndTaskLineText(string lineText) {
            return FindStandaloneCodeKeyword(lineText, "endtask") >= 0;
        }

        private static string ActiveDeclarationScope(string scope) {
            string normalizedScope = NormalizeDeclarationDuplicateScope(scope);
            if (!string.IsNullOrEmpty(thisFunctionScope) &&
                normalizedScope != SCOPE_CONST &&
                normalizedScope != SCOPE_MACRO) {
                return thisFunctionScope;
            }

            return normalizedScope;
        }

        private static void AddDuplicateScanName(
            Dictionary<string, Dictionary<string, int>> countsByScope,
            string scope,
            string name) {
            if (string.IsNullOrEmpty(name) || !IsIdentifier(name)) {
                return;
            }

            if (!countsByScope.ContainsKey(scope)) {
                countsByScope.Add(scope, new Dictionary<string, int>());
            }

            if (!countsByScope[scope].ContainsKey(name)) {
                countsByScope[scope].Add(name, 0);
            }

            countsByScope[scope][name]++;
        }

        private static List<string> CollectDeclarationNamesInLine(string lineText) {
            List<string> names = new List<string>();
            if (!CodeLineStartsWithDeclarationKeyword(lineText)) {
                return names;
            }

            string codeText = StripLineCommentForDuplicateScan(lineText);
            VerilogToken[] lineTokens = VerilogKeywordSplit(codeText, new VerilogToken());
            List<string> items = new List<string>();

            foreach (VerilogToken token in lineTokens) {
                string itemText = (token.Part ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(itemText)) {
                    items.Add(itemText);
                }
            }

            if (items.Count == 0 || !IsDeclarationStartKeyword(items[0])) {
                return names;
            }

            int squareDepth = 0;
            int roundDepth = 0;
            int squigglyDepth = 0;
            bool skippingInitializer = false;

            for (int i = 0; i < items.Count; i++) {
                string itemText = items[i];

                if (itemText == "[") {
                    squareDepth++;
                    continue;
                }

                if (itemText == "]") {
                    if (squareDepth > 0) {
                        squareDepth--;
                    }
                    continue;
                }

                if (itemText == "(") {
                    roundDepth++;
                    continue;
                }

                if (itemText == ")") {
                    if (roundDepth > 0) {
                        roundDepth--;
                    }
                    continue;
                }

                if (itemText == "{") {
                    squigglyDepth++;
                    continue;
                }

                if (itemText == "}") {
                    if (squigglyDepth > 0) {
                        squigglyDepth--;
                    }
                    continue;
                }

                if (squareDepth != 0 || roundDepth != 0 || squigglyDepth != 0) {
                    continue;
                }

                if (itemText == ";") {
                    break;
                }

                if (itemText == ",") {
                    skippingInitializer = false;
                    continue;
                }

                if (itemText == "=") {
                    skippingInitializer = true;
                    continue;
                }

                if (skippingInitializer) {
                    continue;
                }

                if (IsDeclarationModifierKeyword(itemText) || IsDelimiter(itemText) || IsNumeric(itemText) || IsVerilogValue(itemText)) {
                    continue;
                }

                if (IsIdentifier(itemText)) {
                    names.Add(itemText);

                    // A name can be followed by an unpacked dimension, as in:
                    //     wire rbit [7:0];
                    // Do not treat identifiers inside that range as additional declared names.
                    continue;
                }
            }

            return names;
        }

        private static void CountDeclarationNamesInLine(
            Dictionary<string, Dictionary<string, int>> countsByScope,
            string scope,
            string lineText) {
            foreach (string name in CollectDeclarationNamesInLine(lineText)) {
                AddDuplicateScanName(countsByScope, scope, name);
            }
        }

        private static string BackfillDeclarationHoverText(string lineText) {
            string hoverText = TrimLineForHover(StripLineCommentForDuplicateScan(lineText));
            if (string.IsNullOrWhiteSpace(hoverText)) {
                return string.Empty;
            }

            return hoverText.Trim().TrimEnd(',', ';').Trim();
        }

        private static void AddMissingDeclarationSymbol(
            string scope,
            string name,
            string hoverText,
            VerilogTokenTypes variableType) {
            if (string.IsNullOrEmpty(scope) || string.IsNullOrEmpty(name) || !IsIdentifier(name)) {
                return;
            }

            EnsureHoverScope(scope);

            if (!VerilogVariables[scope].ContainsKey(name)) {
                VerilogVariables[scope].Add(name, variableType);
            }

            if (!string.IsNullOrEmpty(hoverText) && !VerilogVariableHoverText[scope].ContainsKey(name)) {
                VerilogVariableHoverText[scope].Add(name, hoverText);
            }
        }

        private static string StripCommentsForPreprocessorLine(string lineText) {
            if (string.IsNullOrEmpty(lineText)) {
                return string.Empty;
            }

            StringBuilder result = new StringBuilder();

            for (int i = 0; i < lineText.Length; i++) {
                char c = lineText[i];
                char next = (i + 1 < lineText.Length) ? lineText[i + 1] : '\0';

                if (PreprocessorScanBlockCommentOpen) {
                    if (c == '*' && next == '/') {
                        PreprocessorScanBlockCommentOpen = false;
                        i++;
                    }
                    continue;
                }

                if (c == '/' && next == '*') {
                    PreprocessorScanBlockCommentOpen = true;
                    i++;
                    continue;
                }

                if (c == '/' && next == '/') {
                    break;
                }

                result.Append(c);
            }

            return result.ToString();
        }

        private static List<string> GetTrimmedLineItems(string codeText) {
            List<string> items = new List<string>();
            if (string.IsNullOrWhiteSpace(codeText)) {
                return items;
            }

            VerilogToken[] lineTokens = VerilogKeywordSplit(codeText, new VerilogToken());
            foreach (VerilogToken token in lineTokens) {
                string itemText = (token.Part ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(itemText)) {
                    items.Add(itemText);
                }
            }

            return items;
        }

        private static string NormalizeMacroName(string itemText) {
            if (string.IsNullOrWhiteSpace(itemText)) {
                return string.Empty;
            }

            string macroName = itemText.Trim();
            if (macroName.StartsWith("`", StringComparison.Ordinal)) {
                macroName = macroName.Substring(1);
            }

            int parenIndex = macroName.IndexOf('(');
            if (parenIndex >= 0) {
                macroName = macroName.Substring(0, parenIndex);
            }

            return IsIdentifier(macroName) ? macroName : string.Empty;
        }

        private static string CurrentPreprocessorConditionText() {
            if (PreprocessorConditionStack.Count == 0) {
                return "(unconditional)";
            }

            return string.Join(Environment.NewLine, PreprocessorConditionStack.ToArray());
        }

        private static string TrimLineForHover(string lineText) {
            return (lineText ?? string.Empty).TrimEnd();
        }

        private static void PushPreprocessorBranchText(string directiveText) {
            PreprocessorBranchTextStack.Add(new List<string> { TrimLineForHover(directiveText) });
        }

        private static void ReplaceCurrentPreprocessorBranchText(string directiveText) {
            if (PreprocessorBranchTextStack.Count == 0) {
                PushPreprocessorBranchText(directiveText);
                return;
            }

            List<string> branchText = PreprocessorBranchTextStack[PreprocessorBranchTextStack.Count - 1];
            branchText.Clear();
            branchText.Add(TrimLineForHover(directiveText));
        }

        private static void PushPreprocessorConditionalGroup() {
            PreprocessorConditionalGroupIdStack.Add(PreprocessorNextConditionalGroupId++);
            PreprocessorConditionalBranchIdStack.Add(0);
        }

        private static void AdvanceCurrentPreprocessorConditionalBranch() {
            if (PreprocessorConditionalBranchIdStack.Count == 0) {
                PushPreprocessorConditionalGroup();
                return;
            }

            int lastIndex = PreprocessorConditionalBranchIdStack.Count - 1;
            PreprocessorConditionalBranchIdStack[lastIndex] = PreprocessorConditionalBranchIdStack[lastIndex] + 1;
        }

        private static void PopPreprocessorBranchText() {
            if (PreprocessorBranchTextStack.Count > 0) {
                PreprocessorBranchTextStack.RemoveAt(PreprocessorBranchTextStack.Count - 1);
            }
        }

        private static void PopPreprocessorConditionalGroup() {
            if (PreprocessorConditionalGroupIdStack.Count > 0) {
                PreprocessorConditionalGroupIdStack.RemoveAt(PreprocessorConditionalGroupIdStack.Count - 1);
            }

            if (PreprocessorConditionalBranchIdStack.Count > 0) {
                PreprocessorConditionalBranchIdStack.RemoveAt(PreprocessorConditionalBranchIdStack.Count - 1);
            }
        }

        private static void AddCurrentPreprocessorBranchTextLine(string lineText) {
            if (PreprocessorBranchTextStack.Count == 0) {
                return;
            }

            string hoverLine = TrimLineForHover(lineText);
            if (string.IsNullOrWhiteSpace(hoverLine)) {
                return;
            }

            PreprocessorBranchTextStack[PreprocessorBranchTextStack.Count - 1].Add(hoverLine);
        }

        private static string CurrentPreprocessorBranchText(string fallbackLineText) {
            if (PreprocessorBranchTextStack.Count == 0) {
                return TrimLineForHover(fallbackLineText);
            }

            List<string> branchLines = new List<string>();
            List<string> branchText = PreprocessorBranchTextStack[PreprocessorBranchTextStack.Count - 1];
            foreach (string branchLine in branchText) {
                if (!string.IsNullOrWhiteSpace(branchLine)) {
                    branchLines.Add(branchLine);
                }
            }

            if (branchLines.Count == 0) {
                return TrimLineForHover(fallbackLineText);
            }

            return string.Join(Environment.NewLine, branchLines.ToArray());
        }

        private static bool HasMacroReferenceInLine(string text) {
            if (string.IsNullOrEmpty(text)) {
                return false;
            }

            for (int i = 0; i < text.Length - 1; i++) {
                if (text[i] != '`') {
                    continue;
                }

                char next = text[i + 1];
                if (next == '_' ||
                    (next >= 'A' && next <= 'Z') ||
                    (next >= 'a' && next <= 'z')) {
                    return true;
                }
            }

            return false;
        }

        private static int CurrentPreprocessorConditionalGroupId() {
            if (PreprocessorConditionalGroupIdStack.Count == 0) {
                return 0;
            }

            return PreprocessorConditionalGroupIdStack[PreprocessorConditionalGroupIdStack.Count - 1];
        }

        private static int CurrentPreprocessorConditionalBranchId() {
            if (PreprocessorConditionalBranchIdStack.Count == 0) {
                return 0;
            }

            return PreprocessorConditionalBranchIdStack[PreprocessorConditionalBranchIdStack.Count - 1];
        }

        private static void EnsureHoverScope(string scope) {
            if (!VerilogVariables.ContainsKey(scope)) {
                VerilogVariables.Add(scope, new Dictionary<string, VerilogTokenTypes>());
            }

            if (!VerilogVariableHoverText.ContainsKey(scope)) {
                VerilogVariableHoverText.Add(scope, new Dictionary<string, string>());
            }

            if (!VerilogDefinitionLocations.ContainsKey(scope)) {
                VerilogDefinitionLocations.Add(scope, new Dictionary<string, VerilogDefinitionLocation>());
            }
        }

        private static bool TryGetCurrentDefinitionLocation(string itemName, out int lineNumber, out int linePosition) {
            lineNumber = -1;
            linePosition = -1;

            if (string.IsNullOrEmpty(itemName)) {
                return false;
            }

            if (itemName == thisHoverName && thisHoverNameLineNumber >= 0 && thisHoverNameLinePosition >= 0) {
                lineNumber = thisHoverNameLineNumber;
                linePosition = thisHoverNameLinePosition;
                return true;
            }

            if (itemName == thisModuleName && thisModuleNameLineNumber >= 0 && thisModuleNameLinePosition >= 0) {
                lineNumber = thisModuleNameLineNumber;
                linePosition = thisModuleNameLinePosition;
                return true;
            }

            if (itemName == thisItemText && thisItemLineNumber >= 0 && thisItemLinePosition >= 0) {
                lineNumber = thisItemLineNumber;
                linePosition = thisItemLinePosition;
                return true;
            }

            return false;
        }

        private static void AddDefinitionLocation(string scope, string itemName, VerilogTokenTypes tokenType, string hoverText) {
            int lineNumber;
            int linePosition;
            if (!TryGetCurrentDefinitionLocation(itemName, out lineNumber, out linePosition)) {
                return;
            }

            EnsureHoverScope(scope);

            if (VerilogDefinitionLocations[scope].ContainsKey(itemName)) {
                return;
            }

            VerilogDefinitionLocations[scope].Add(
                itemName,
                new VerilogDefinitionLocation(
                    scope,
                    itemName,
                    lineNumber,
                    linePosition,
                    itemName.Length,
                    tokenType,
                    hoverText));
        }

        private static void AddOrAppendHoverItem(string scope, string itemName, VerilogTokenTypes tokenType, string hoverText, bool addDefinitionLocation = true) {
            if (string.IsNullOrEmpty(itemName) || string.IsNullOrEmpty(hoverText)) {
                return;
            }

            EnsureHoverScope(scope);
            VerilogVariables[scope][itemName] = tokenType;
            if (addDefinitionLocation) {
                AddDefinitionLocation(scope, itemName, tokenType, hoverText);
            }

            string existingHoverText;
            if (VerilogVariableHoverText[scope].TryGetValue(itemName, out existingHoverText)) {
                if (existingHoverText.IndexOf(hoverText, StringComparison.Ordinal) < 0) {
                    VerilogVariableHoverText[scope][itemName] = existingHoverText + Environment.NewLine + Environment.NewLine + hoverText;
                }
            }
            else {
                VerilogVariableHoverText[scope].Add(itemName, hoverText);
            }
        }

        private static void AddMacroHoverItem(string macroName, string hoverText) {
            AddOrAppendHoverItem(SCOPE_MACRO, macroName, VerilogTokenTypes.Verilog_Macro, hoverText, false);
        }

        private static void AddMacroHoverItem(string macroName, string hoverText, int lineNumber, int linePosition) {
            thisItemText = macroName;
            thisItemLineNumber = lineNumber;
            thisItemLinePosition = linePosition;
            AddOrAppendHoverItem(SCOPE_MACRO, macroName, VerilogTokenTypes.Verilog_Macro, hoverText);
        }

        private static void AddFunctionHoverItem(string scope, string functionName, string hoverText) {
            AddOrAppendHoverItem(NormalizeDeclarationDuplicateScope(scope), functionName, VerilogTokenTypes.Verilog_FunctionName, hoverText);
        }

        private static void AddConditionalDefinitionHoverItem(string scope, string itemName, string hoverText) {
            AddOrAppendHoverItem(scope, itemName, VerilogTokenTypes.Verilog_MacroDefinition, hoverText, false);
        }

        private static void QueueConditionalDefinitionCandidate(
            string scope,
            string itemName,
            int groupId,
            int branchId,
            bool hasMacroReference,
            string hoverText,
            string declarationText,
            VerilogTokenTypes declarationType) {
            if (string.IsNullOrEmpty(scope) || string.IsNullOrEmpty(itemName) || groupId <= 0 || string.IsNullOrEmpty(hoverText)) {
                return;
            }

            PreprocessorConditionalDefinitionCandidates.Add(new ConditionalDefinitionCandidate {
                Scope = scope,
                Name = itemName,
                GroupId = groupId,
                BranchId = branchId,
                HasMacroReference = hasMacroReference,
                HoverText = hoverText,
                DeclarationText = declarationText,
                DeclarationType = declarationType
            });
        }

        private static int MacroNameLinePosition(string lineText, string macroItem, string macroName) {
            if (string.IsNullOrEmpty(lineText)) {
                return -1;
            }

            if (!string.IsNullOrEmpty(macroItem)) {
                int itemPosition = lineText.IndexOf(macroItem, StringComparison.Ordinal);
                if (itemPosition >= 0) {
                    return itemPosition;
                }
            }

            if (!string.IsNullOrEmpty(macroName)) {
                return lineText.IndexOf(macroName, StringComparison.Ordinal);
            }

            return -1;
        }

        public static void ProcessPreprocessorLine(string lineText, int lineNumber = -1) {
            string codeText = StripCommentsForPreprocessorLine(lineText);
            List<string> items = GetTrimmedLineItems(codeText);
            if (items.Count == 0) {
                if (PreprocessorConditionStack.Count > 0) {
                    AddCurrentPreprocessorBranchTextLine(lineText);
                }
                return;
            }

            string firstItem = items[0];
            string trimmedLine = codeText.Trim();

            switch (firstItem) {
                case "`ifdef":
                case "`ifndef":
                    if (items.Count > 1) {
                        string macroName = NormalizeMacroName(items[1]);
                        if (!string.IsNullOrEmpty(macroName)) {
                            AddMacroHoverItem(macroName, "macro condition:" + Environment.NewLine + trimmedLine);
                            PreprocessorConditionStack.Add(firstItem + " " + macroName);
                            PushPreprocessorConditionalGroup();
                            PushPreprocessorBranchText(trimmedLine);
                        }
                    }
                    return;

                case "`elsif":
                    if (items.Count > 1) {
                        string macroName = NormalizeMacroName(items[1]);
                        if (!string.IsNullOrEmpty(macroName)) {
                            AddMacroHoverItem(macroName, "macro condition:" + Environment.NewLine + trimmedLine);
                            if (PreprocessorConditionStack.Count > 0) {
                                string previousCondition = PreprocessorConditionStack[PreprocessorConditionStack.Count - 1];
                                PreprocessorConditionStack[PreprocessorConditionStack.Count - 1] = firstItem + " " + macroName + " (from " + previousCondition + ")";
                                AdvanceCurrentPreprocessorConditionalBranch();
                                ReplaceCurrentPreprocessorBranchText(trimmedLine);
                            }
                            else {
                                PreprocessorConditionStack.Add(firstItem + " " + macroName);
                                PushPreprocessorConditionalGroup();
                                PushPreprocessorBranchText(trimmedLine);
                            }
                        }
                    }
                    return;

                case "`else":
                    if (PreprocessorConditionStack.Count > 0) {
                        string previousCondition = PreprocessorConditionStack[PreprocessorConditionStack.Count - 1];
                        PreprocessorConditionStack[PreprocessorConditionStack.Count - 1] = "`else (from " + previousCondition + ")";
                        AdvanceCurrentPreprocessorConditionalBranch();
                        ReplaceCurrentPreprocessorBranchText(trimmedLine);
                    }
                    else {
                        PreprocessorConditionStack.Add("`else");
                        PushPreprocessorConditionalGroup();
                        PushPreprocessorBranchText(trimmedLine);
                    }
                    return;

                case "`endif":
                    if (PreprocessorConditionStack.Count > 0) {
                        PreprocessorConditionStack.RemoveAt(PreprocessorConditionStack.Count - 1);
                    }
                    PopPreprocessorBranchText();
                    PopPreprocessorConditionalGroup();
                    return;

                case "`define":
                    if (items.Count > 1) {
                        string macroName = NormalizeMacroName(items[1]);
                        if (!string.IsNullOrEmpty(macroName)) {
                            string hoverText = "macro definition:" + Environment.NewLine + trimmedLine + Environment.NewLine + Environment.NewLine +
                                               "condition:" + Environment.NewLine + CurrentPreprocessorConditionText();
                            AddMacroHoverItem(macroName, hoverText, lineNumber, MacroNameLinePosition(lineText, items[1], macroName));
                        }
                    }
                    return;

                case "`undef":
                    if (items.Count > 1) {
                        string macroName = NormalizeMacroName(items[1]);
                        if (!string.IsNullOrEmpty(macroName)) {
                            AddMacroHoverItem(macroName, "macro undef:" + Environment.NewLine + trimmedLine);
                        }
                    }
                    return;

                default:
                    break;
            }

            if (PreprocessorConditionStack.Count == 0) {
                return;
            }

            AddCurrentPreprocessorBranchTextLine(lineText);

            List<string> declarationNames = CollectDeclarationNamesInLine(codeText);
            if (declarationNames.Count == 0) {
                return;
            }

            string scope = ActiveDeclarationScope(thisModuleName);
            string conditionText = CurrentPreprocessorConditionText();
            int groupId = CurrentPreprocessorConditionalGroupId();
            int branchId = CurrentPreprocessorConditionalBranchId();
            bool hasMacroReference = HasMacroReferenceInLine(codeText);
            string hoverTextForLine = "conditional macro-controlled definition:" + Environment.NewLine + CurrentPreprocessorBranchText(trimmedLine) + Environment.NewLine + Environment.NewLine +
                                      "condition:" + Environment.NewLine + conditionText;
            VerilogTokenTypes declarationType = GetDeclarationVariableTypeForConditionalText(trimmedLine);

            foreach (string declarationName in declarationNames) {
                QueueConditionalDefinitionCandidate(scope, declarationName, groupId, branchId, hasMacroReference, hoverTextForLine, trimmedLine, declarationType);
            }
        }

        private static string ConditionalDefinitionKey(string scope, string name) {
            return (scope ?? string.Empty) + "::" + (name ?? string.Empty);
        }

        private static int DeclarationCount(
            Dictionary<string, Dictionary<string, int>> countsByScope,
            string scope,
            string name) {
            Dictionary<string, int> scopeCounts;
            int count;
            if (countsByScope.TryGetValue(scope, out scopeCounts) &&
                scopeCounts.TryGetValue(name, out count)) {
                return count;
            }

            return 0;
        }

        private static bool IsConditionalDuplicateSuppressed(string scope, string name) {
            return PreprocessorConditionalDuplicateSuppressions.ContainsKey(ConditionalDefinitionKey(scope, name));
        }

        private static void ApplyConditionalDefinitionCandidates(Dictionary<string, Dictionary<string, int>> countsByScope) {
            Dictionary<string, List<int>> candidateBranchIds = new Dictionary<string, List<int>>();
            Dictionary<string, Dictionary<int, int>> candidateBranchCounts = new Dictionary<string, Dictionary<int, int>>();
            Dictionary<string, List<string>> candidateHoverText = new Dictionary<string, List<string>>();
            Dictionary<string, bool> candidateHasMacroReference = new Dictionary<string, bool>();
            Dictionary<string, string> candidateScopeByKey = new Dictionary<string, string>();
            Dictionary<string, string> candidateNameByKey = new Dictionary<string, string>();
            Dictionary<string, int> candidateTotalCounts = new Dictionary<string, int>();
            Dictionary<string, string> candidateDeclarationText = new Dictionary<string, string>();
            Dictionary<string, VerilogTokenTypes> candidateDeclarationType = new Dictionary<string, VerilogTokenTypes>();

            foreach (ConditionalDefinitionCandidate candidate in PreprocessorConditionalDefinitionCandidates) {
                if (candidate == null || string.IsNullOrEmpty(candidate.Name) || candidate.GroupId <= 0) {
                    continue;
                }

                string scope = NormalizeDeclarationDuplicateScope(candidate.Scope);
                string key = ConditionalDefinitionKey(scope, candidate.Name) + "::" + candidate.GroupId.ToString();

                if (!candidateBranchIds.ContainsKey(key)) {
                    candidateBranchIds.Add(key, new List<int>());
                    candidateBranchCounts.Add(key, new Dictionary<int, int>());
                    candidateHasMacroReference.Add(key, false);
                    candidateScopeByKey.Add(key, scope);
                    candidateNameByKey.Add(key, candidate.Name);
                    candidateTotalCounts.Add(key, 0);
                    candidateDeclarationText.Add(key, candidate.DeclarationText ?? candidate.Name);
                    candidateDeclarationType.Add(key, candidate.DeclarationType);
                }

                if (!candidateBranchIds[key].Contains(candidate.BranchId)) {
                    candidateBranchIds[key].Add(candidate.BranchId);
                }

                if (!candidateBranchCounts[key].ContainsKey(candidate.BranchId)) {
                    candidateBranchCounts[key].Add(candidate.BranchId, 0);
                }
                candidateBranchCounts[key][candidate.BranchId]++;
                candidateTotalCounts[key]++;

                if (candidate.HasMacroReference) {
                    candidateHasMacroReference[key] = true;
                }

                if (!candidateHoverText.ContainsKey(key)) {
                    candidateHoverText.Add(key, new List<string>());
                }

                if (!string.IsNullOrEmpty(candidate.HoverText) &&
                    !candidateHoverText[key].Contains(candidate.HoverText)) {
                    candidateHoverText[key].Add(candidate.HoverText);
                }
            }

            foreach (KeyValuePair<string, List<int>> branchIds in candidateBranchIds) {
                string key = branchIds.Key;
                string scope = candidateScopeByKey[key];
                string name = candidateNameByKey[key];

                if (branchIds.Value.Count <= 1) {
                    continue;
                }

                int totalDeclarationCount = DeclarationCount(countsByScope, scope, name);
                if (totalDeclarationCount <= 1 || totalDeclarationCount != candidateTotalCounts[key]) {
                    continue;
                }

                bool duplicateInSameBranch = false;
                foreach (KeyValuePair<int, int> branchCount in candidateBranchCounts[key]) {
                    if (branchCount.Value > 1) {
                        duplicateInSameBranch = true;
                        break;
                    }
                }

                if (duplicateInSameBranch) {
                    continue;
                }

                PreprocessorConditionalDuplicateSuppressions[ConditionalDefinitionKey(scope, name)] = true;
                EnsureHoverScope(scope);

                if (candidateHasMacroReference.ContainsKey(key) && candidateHasMacroReference[key]) {
                    VerilogVariables[scope][name] = VerilogTokenTypes.Verilog_MacroDefinition;

                    List<string> hoverLines;
                    if (candidateHoverText.TryGetValue(key, out hoverLines) && hoverLines.Count > 0) {
                        VerilogVariableHoverText[scope][name] = string.Join(Environment.NewLine + Environment.NewLine, hoverLines.ToArray());
                    }
                }
                else {
                    VerilogVariables[scope][name] = candidateDeclarationType[key];
                    VerilogVariableHoverText[scope][name] = candidateDeclarationText[key];
                }
            }
        }

        private static void MarkDuplicateDeclarationsFromSnapshot(ITextSnapshot snapshot) {
            if (snapshot == null || snapshot.Length == 0) {
                return;
            }

            Dictionary<string, Dictionary<string, int>> countsByScope = new Dictionary<string, Dictionary<string, int>>();

            string activeLocalScope = string.Empty;

            foreach (ITextSnapshotLine line in snapshot.Lines) {
                string lineText = line.GetText();
                bool mayContainFunction = lineText.IndexOf("function", StringComparison.Ordinal) >= 0;
                bool mayContainTask = lineText.IndexOf("task", StringComparison.Ordinal) >= 0;
                bool mayContainDeclaration = CodeLineStartsWithDeclarationKeyword(lineText);
                bool mayEndLocalScope = !string.IsNullOrEmpty(activeLocalScope) &&
                    (lineText.IndexOf("endfunction", StringComparison.Ordinal) >= 0 ||
                     lineText.IndexOf("endtask", StringComparison.Ordinal) >= 0);

                if (!mayContainFunction && !mayContainTask && !mayContainDeclaration && !mayEndLocalScope) {
                    continue;
                }

                string moduleScope = string.Empty;
                string functionName;
                string taskName;

                if (mayContainFunction && TryGetFunctionNameFromLineText(lineText, out functionName)) {
                    moduleScope = NormalizeDeclarationDuplicateScope(TextModuleName(line.LineNumber, 0));
                    activeLocalScope = FunctionLocalScopeName(moduleScope, functionName);
                }
                else if (mayContainTask && TryGetTaskNameFromLineText(lineText, out taskName)) {
                    moduleScope = NormalizeDeclarationDuplicateScope(TextModuleName(line.LineNumber, 0));
                    activeLocalScope = TaskLocalScopeName(moduleScope, taskName);
                }

                if (mayContainDeclaration) {
                    string scope = activeLocalScope;
                    if (string.IsNullOrEmpty(scope)) {
                        if (string.IsNullOrEmpty(moduleScope)) {
                            moduleScope = NormalizeDeclarationDuplicateScope(TextModuleName(line.LineNumber, 0));
                        }
                        scope = moduleScope;
                    }

                    List<string> declarationNames = CollectDeclarationNamesInLine(lineText);
                    foreach (string name in declarationNames) {
                        AddDuplicateScanName(countsByScope, scope, name);
                    }

                    VerilogTokenTypes variableType;
                    if (TryGetDeclarationVariableTypeFromText(lineText, out variableType)) {
                        string hoverText = BackfillDeclarationHoverText(lineText);
                        foreach (string name in declarationNames) {
                            AddMissingDeclarationSymbol(scope, name, hoverText, variableType);
                        }
                    }
                }

                if (mayEndLocalScope && (IsEndFunctionLineText(lineText) || IsEndTaskLineText(lineText))) {
                    activeLocalScope = string.Empty;
                }
            }

            ApplyConditionalDefinitionCandidates(countsByScope);

            foreach (KeyValuePair<string, Dictionary<string, int>> scopeCounts in countsByScope) {
                string scope = NormalizeDeclarationDuplicateScope(scopeCounts.Key);

                if (!VerilogVariables.ContainsKey(scope)) {
                    VerilogVariables.Add(scope, new Dictionary<string, VerilogTokenTypes>());
                }

                if (!VerilogVariableHoverText.ContainsKey(scope)) {
                    VerilogVariableHoverText.Add(scope, new Dictionary<string, string>());
                }

                foreach (KeyValuePair<string, int> nameCount in scopeCounts.Value) {
                    if (nameCount.Value <= 1) {
                        continue;
                    }

                    if (VerilogVariables[scope].ContainsKey(nameCount.Key) &&
                        VerilogVariables[scope][nameCount.Key] == VerilogTokenTypes.Verilog_MacroDefinition) {
                        continue;
                    }

                    if (IsConditionalDuplicateSuppressed(scope, nameCount.Key)) {
                        continue;
                    }

                    VerilogVariables[scope][nameCount.Key] = VerilogTokenTypes.Verilog_Variable_duplicate;

                    if (VerilogVariableHoverText[scope].ContainsKey(nameCount.Key)) {
                        if (!VerilogVariableHoverText[scope][nameCount.Key].StartsWith("duplicate? ", StringComparison.Ordinal)) {
                            VerilogVariableHoverText[scope][nameCount.Key] = "duplicate? " + VerilogVariableHoverText[scope][nameCount.Key];
                        }
                    }
                    else {
                        VerilogVariableHoverText[scope].Add(nameCount.Key, "duplicate? " + nameCount.Key);
                    }
                }
            }
        }

        private static void AddHoverItem(string thisScope, string ItemName, string HoverText) {
            thisScope = ActiveDeclarationScope(thisScope);

            if (ItemName == string.Empty || (ItemName.Length == 1) && IsDelimiter(ItemName[0]) ) {
                // never add a blank & never add a delimiter TODO - why would we even try? unresolved declaration naming?
                // sometimes we end up here while typing new declarations
                // string a = "breakpoint"; // we should never end up here TODO do we need to clean up interim values?
                return;
            }
            else {
                // ensure VerilogVariables has a dictionary for [thisScope]
                if (!VerilogVariables.ContainsKey(thisScope)) {
                    VerilogVariables.Add(thisScope, new Dictionary<string, VerilogTokenTypes> { });
                }

                // ensure VerilogVariableHoverText has a dictionary for [thisScope]
                if (!VerilogVariableHoverText.ContainsKey(thisScope)) {
                    VerilogVariableHoverText.Add(thisScope, new Dictionary<string, string> { });
                }

                if (!VerilogDefinitionLocations.ContainsKey(thisScope)) {
                    VerilogDefinitionLocations.Add(thisScope, new Dictionary<string, VerilogDefinitionLocation> { });
                }

                // first add the token type; hover text added below to separate collection
                if (VerilogVariables[thisScope].Keys.Contains(ItemName)) {
                    // edit existing, TODO - new color for dupes?
                    // "var,)" will also get us here
                    if (thisScope == SCOPE_CONST) {
                        // special handling for constant items, such as strings and numbers
                        // we don't detect dupes, so nothign to do here.
                    }
                    else if (VerilogGlobals.VerilogVariables[thisScope][ItemName] == VerilogTokenTypes.Verilog_MacroDefinition) {
                        // Conditional preprocessor branches can define the same declaration name in mutually exclusive text.
                        // Keep the conditional-definition classification instead of marking it as a plain duplicate.
                    }
                    else {
                        // if we already have this item, and it is not a constant, it must be a duplicate declaration
                        VerilogGlobals.VerilogVariables[thisScope][ItemName] = VerilogTokenTypes.Verilog_Variable_duplicate;
                    }
                }
                else {
                    // add new
                    if (thisScope == SCOPE_CONST) {
                        // we are adding the constant value for the first time (and we don't care for dupplicates)
                        VerilogVariables[thisScope].Add(ItemName, VerilogTokenTypes.Verilog_Value); // TODO define new const type; don't reuse config type
                    }
                    else {
                        if (VerilogVariables.ContainsKey(ItemName)) {
                            VerilogVariables[thisScope].Add(ItemName, VerilogTokenTypes.Verilog_Variable_module);
                        }
                        else {
                            VerilogVariables[thisScope].Add(ItemName, GetDeclarationVariableTypeForHoverText(HoverText));
                        }
                    }
                }
                string thisHoverText = HoverText;

                if (thisScope == SCOPE_CONST) {
                    // TODO consider additional values (hex, binary, decimal equivalents)
                    // for now the hover value is the constant value (not very exciting)
                }
                else {
                    // next add the hover text
                    switch (BuildHoverState) {
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
                }

                VerilogTokenTypes thisTokenType;
                if (VerilogGlobals.VerilogVariables[thisScope].TryGetValue(ItemName, out thisTokenType)) {
                    AddDefinitionLocation(thisScope, ItemName, thisTokenType, thisHoverText);
                }

                if (VerilogGlobals.VerilogVariableHoverText[thisScope].ContainsKey(ItemName)) {
                    if (thisScope == SCOPE_CONST) {
                        // it already exists, no need to do anything for constants. (we might have many duplicates)
                    }
                    else if (VerilogGlobals.VerilogVariables[thisScope].ContainsKey(ItemName) &&
                             VerilogGlobals.VerilogVariables[thisScope][ItemName] == VerilogTokenTypes.Verilog_MacroDefinition) {
                        // Keep the conditional hover text collected from the full preprocessor branch.
                    }
                    else {
                        // overwrite an existing variable declaration - duplicate definition?
                        VerilogGlobals.VerilogVariableHoverText[thisScope][ItemName] = "duplicate? " + thisHoverText;
                    }
                }
                else {
                    // add a new variable hover text attribute
                    VerilogGlobals.VerilogVariableHoverText[thisScope].Add(ItemName, thisHoverText);
                }
            } // else was not delimiter or blank item
        } // AddHoverItem

        #region "BuildHoverItems - State Handler"

        private static void SetBracketContentStatus_For(string ItemText) {
            switch (ItemText) {
                case "[":
                    IsInsideSquareBracket = true;
                    break;
                case "]":
                    IsInsideSquareBracket = false;
                    break;
                case "{":
                    IsInsideSquigglyBracket = true;
                    break;
                case "}":
                    IsInsideSquigglyBracket = false;
                    break;

                default:
                    // nothing
                    break;
            }
        }

        private static bool Is_BracketContent_For(string thisScope, string ItemText) {
            return IsInsideSquareBracket && IsDefinedVerilogVariable(thisScope, ItemText);
        }

        /// <summary>
        ///   Process_UndefinedState_For
        /// </summary>
        /// <param name="ItemText"></param>
        private static void Process_UndefinedState_For(string ItemText) {
            switch (ItemText) {
                case "":
                    // ignoring trimmed spaces / blanks
                    break;

                case "module":
                    // we're naming a module
                    BuildHoverState = BuildHoverStates.ModuleStart;
                    thisModuleDeclarationText = ItemText;
                    break;

                case "function":
                    BuildHoverState = BuildHoverStates.FunctionNaming;
                    thisVariableDeclarationText = ItemText;
                    break;

                case "task":
                    BuildHoverState = BuildHoverStates.TaskNaming;
                    thisVariableDeclarationText = ItemText;
                    break;

                case "input":
                case "output":
                case "inout":
                case "wire":
                case "reg":
                case "localparam":
                case "parameter":
                    // the same keywords could be used for module parameters, or variables:
                    switch (BuildHoverState) {
                        case BuildHoverStates.ModuleStart:
                            BuildHoverState = BuildHoverStates.ModuleParameterNaming;
                            break;

                        case BuildHoverStates.VariableMimicNaming: // comma-delimited types have the type copied (mimic) into hover text for each variable
                            BuildHoverState = BuildHoverStates.VariableNaming;
                            thisVariableDeclarationText = ItemText;
                            thisHoverName = string.Empty; // we are no longer using the same type declaration, so reset to blank
                            break;

                        default:
                            BuildHoverState = BuildHoverStates.VariableNaming;
                            thisVariableDeclarationText = ItemText;
                            break;
                    }

                    UpdateCurrentDeclarationVariableType(ItemText);
                    break;

                case "endmodule":
                    // we're naming a module
                    BuildHoverState = BuildHoverStates.UndefinedState;
                    thisFunctionName = string.Empty;
                    thisFunctionScope = string.Empty;
                    // this is likely a syntax error
                    break;

                case "endfunction":
                    BuildHoverState = BuildHoverStates.UndefinedState;
                    thisFunctionName = string.Empty;
                    thisFunctionScope = string.Empty;
                    break;

                case "endtask":
                    BuildHoverState = BuildHoverStates.UndefinedState;
                    thisFunctionName = string.Empty;
                    thisFunctionScope = string.Empty;
                    break;

                default:
                    if (VerilogVariables.ContainsKey(ItemText)) {
                        // a scope-level module name is defined, so treat it like a variable type
                        BuildHoverState = BuildHoverStates.VariableNaming; // actually, we are module naming. TODO different color for modules?

                        // a module instantiation will have the work "module" manually prepended
                        thisVariableDeclarationText = "module " + ItemText;
                    }
                    else {
                        if (IsVerilogValue(ItemText)) {
                            AddHoverItem(SCOPE_CONST, ItemText, ValueHoverText(ItemText));
                        }
                        BuildHoverState = BuildHoverStates.UndefinedState;
                    }
                    break;
            }
        }

        /// <summary>
        ///    Process_ModuleStart_For
        /// </summary>
        /// <param name="ItemText"></param>
        private static void Process_ModuleStart_For(string ItemText) {
            // we've found the "module" keyword, the next word should be the module name
            // TODO - flag syntax error for non-variable names found
            switch (ItemText) {
                case "":
                    // trimming blanks to a single space
                    thisModuleDeclarationText += " ";
                    break;

                default:
                    thisModuleName = ItemText;
                    thisModuleDeclarationText += ItemText;
                    //editingBufferModuleAttributes.Add(new BufferModuleAttribute { LineStart = 0,
                    //                                                               ModuleName = thisModuleName });
                    BuildHoverState = BuildHoverStates.ModuleNamed;
                    break;
            }
        }

        /// <summary>
        ///    Process_ModuleNamed_For
        /// </summary>
        /// <param name="ItemText"></param>
        private static void Process_ModuleNamed_For(string ItemText) {
            switch (ItemText) {
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

        private static void Process_ModuleOpenParen_For(string ItemText) {
            switch (ItemText) {
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
                case "localparam":
                case "parameter":
                    // the same keywords could be used for module parameters, or variables:
                    BuildHoverState = BuildHoverStates.ModuleParameterNaming;
                    thisModuleParameterText = ItemText;
                    UpdateCurrentDeclarationVariableType(ItemText);
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
        private static void Process_ModuleParameterNaming_For(string ItemText) {
            // once we are naming a module parameter, we only end with a closing parenthesis, or a comman
            switch (ItemText) {
                case "":
                    // only append whitespace when not found at beginning
                    if (thisModuleParameterText != string.Empty) {
                        if ((lastHoverItem == string.Empty) || (lastHoverItem == "\t")) {
                            // we'll ignore sequentual tabs, or alternating table-space
                            // only one space will be used
                        }
                        else {
                            thisModuleDeclarationText += " ";
                            thisModuleParameterText += " ";
                        }
                    }
                    break;

                case "\t":
                    if ((lastHoverItem == string.Empty) || (lastHoverItem == "\t")) {
                        // we'll ignore sequentual tabs, or alternating tab-space
                        // only one space will be used
                    }
                    else {
                        thisModuleDeclarationText += " ";
                        thisModuleParameterText += " ";
                    }
                    break;

                case ")":

                    // also add an individual parameter as needed
                    // note all module parameters have test appended: "module [modulename]" + {}  + ")"
                    AddHoverItem(thisModuleName, thisHoverName, thisModuleParameterText);
                    thisModuleParameterText = string.Empty; // upon the colose parenthesis, no more module parameters
                    BuildHoverState = BuildHoverStates.UndefinedState; // and no more module definition

                    // we add the module definition afterwards to avoid any additional, manually added closing ")" that is included for *every( module parameter, but not actually in the text
                    thisModuleDeclarationText += ItemText;
                    if (lastNonblankHoverItem == ",") {
                        // we won't try to add a duplicate if there's a trailing ",)" syntax error
                    }
                    else {
                        AddHoverItem(thisModuleName, thisModuleName, thisModuleDeclarationText);
                        thisHoverName = string.Empty;
                    }
                    break;

                case ",":
                    if (IsInsideSquigglyBracket) {
                        // comma inside concatenation: { a, b }  -> not a parameter separator
                        thisModuleDeclarationText += ItemText;
                        thisModuleParameterText += ItemText;
                        break;
                    }

                    thisModuleDeclarationText += ItemText; // only the module declaration will include the comment
                    thisModuleDeclarationText += System.Environment.NewLine;

                    // add the module parameter
                    AddHoverItem(thisModuleName, thisHoverName, thisModuleParameterText);
                    thisModuleParameterText = thisModuleParameterText.Replace(thisHoverName, ""); // TODO - use a placeholder here, not an empty string
                    thisHoverName = string.Empty;

                    // the next parameter after the comma will use the same definition
                    BuildHoverState = BuildHoverStates.ModuleParameterMimicNaming;
                    break;

                case ";":
                    thisModuleDeclarationText += ItemText; // only the module declaration will include the comment
                    thisModuleDeclarationText += System.Environment.NewLine;

                    // add the module parameter
                    AddHoverItem(thisModuleName, thisHoverName, thisModuleParameterText);
                    thisHoverName = string.Empty;
                    thisModuleParameterText = string.Empty; // we can't use the same parameter def after a semicolon
                    BuildHoverState = BuildHoverStates.ModuleParameterNaming; // certainly not mimic naming after a semi-colon!
                    break;

                case "=":
                    thisModuleParameterText += ItemText;
                    thisModuleDeclarationText += ItemText;
                    UpdateCurrentDeclarationVariableType(thisModuleParameterText);
                    break;

                default:
                    thisModuleParameterText += ItemText;
                    thisModuleDeclarationText += ItemText;
                    UpdateCurrentDeclarationVariableType(thisModuleParameterText);

                    if (thisHoverName == string.Empty && IsIdentifier(ItemText) && !IsVerilogNamerKeyword(ItemText) && !IsVerilogVariableSigner(ItemText)) {
                        thisHoverName = ItemText;
                    }

                    if (IsVerilogNamerKeyword(ItemText) || IsVerilogVariableSigner(ItemText) || IsVerilogBracket(ItemText) || IsNumeric(ItemText) || IsVerilogValue(ItemText) || IsDelimiter(ItemText)) {
                        SetBracketContentStatus_For(ItemText);
                        // nothing at this time; we are still bulding the declaration part
                        // thisModuleParameterText += ItemText;
                        if (IsVerilogValue(ItemText)) {
                            AddHoverItem(SCOPE_CONST, ItemText, ValueHoverText(ItemText));
                        }
                    }
                    else {
                        // thisHoverName = ItemText;
                    }
                    break;
            }
        }

        /// <summary>
        ///   Process_ModuleParameterMimicNaming_For
        /// </summary>
        /// <param name="ItemText"></param>
        private static void Process_ModuleParameterMimicNaming_For(string ItemText) {
            // once we are naming a module parameter, we only end with a closing parenthesis, or a comman
            switch (ItemText) {
                case "":
                    if ((lastHoverItem == string.Empty) || (lastHoverItem == "\t")) {
                        // we'll ignore sequentual tabs, or alternating table-space
                        // only one space will be used
                    }
                    else {
                        thisModuleDeclarationText += " ";
                    }

                    // thisModuleParameterText += " ";
                    break;

                case "\t":
                    if ((lastHoverItem == string.Empty) || (lastHoverItem == "\t")) {
                        // we'll ignore sequentual tabs, or alternating table-space
                        // only one space will be used
                    }
                    else {
                        thisModuleParameterText += " ";
                    }
                    break;

                case ")":
                    thisModuleDeclarationText += ItemText;
                    AddHoverItem(thisModuleName, thisModuleName, thisModuleDeclarationText);

                    // also add an individual parameter as needed
                    AddHoverItem(thisModuleName, thisHoverName, thisModuleParameterText);
                    thisHoverName = string.Empty;
                    thisModuleParameterText = string.Empty; // upon the close parenthesis, no more module parameters. we might try to re-add the last param during syntax errot (e.g. traling comma immediately followed by closing parenthesis
                    BuildHoverState = BuildHoverStates.UndefinedState; // and no more module definition
                    break;

                case ",":
                    if (IsInsideSquigglyBracket) {
                        // comma inside concatenation: { a, b }  -> not a parameter separator
                        thisModuleDeclarationText += ItemText;
                        // thisModuleParameterText += ItemText;
                        break;
                    }

                    thisModuleDeclarationText += ItemText; // only the module declaration will include the comment
                    thisModuleDeclarationText += System.Environment.NewLine;

                    // add the module parameter
                    AddHoverItem(thisModuleName, thisHoverName, thisModuleParameterText);
                    thisModuleParameterText = thisModuleParameterText.Replace(thisHoverName, "");
                    thisHoverName = string.Empty;
                    break;

                case ";":
                    thisModuleDeclarationText += ItemText; // only the module declaration will include the comment
                    thisModuleDeclarationText += System.Environment.NewLine;

                    // add the module parameter
                    AddHoverItem(thisModuleName, thisHoverName, thisModuleParameterText);
                    thisHoverName = string.Empty;
                    thisModuleParameterText = string.Empty;
                    BuildHoverState = BuildHoverStates.ModuleParameterNaming; // certainly not mimic naming after a semi-colon!
                    break;

                default:
                    // thisModuleParameterText += ItemText;
                    thisModuleDeclarationText += ItemText;

                    if (IsVerilogNamerKeyword(ItemText) || IsVerilogVariableSigner(ItemText) || IsVerilogBracket(ItemText) || IsNumeric(ItemText) || IsVerilogValue(ItemText) || IsDelimiter(ItemText)) {
                        SetBracketContentStatus_For(ItemText);

                        // no longer mimic naming
                        BuildHoverState = BuildHoverStates.ModuleParameterNaming;
                        if (IsVerilogValue(ItemText)) {
                            AddHoverItem(SCOPE_CONST, ItemText, ValueHoverText(ItemText));
                        }
                        thisModuleParameterText = ItemText; // start over for the module parameter
                        UpdateCurrentDeclarationVariableType(thisModuleParameterText);
                    }
                    else {
                        if (thisHoverName == string.Empty && IsIdentifier(ItemText) && !IsVerilogNamerKeyword(ItemText) && !IsVerilogVariableSigner(ItemText)) {
                            thisHoverName = ItemText;
                        }
                        thisModuleParameterText += ItemText;
                    }
                    break;
            }
        }

        /// <summary>
        ///    Process_ModuleCloseParen_For
        /// </summary>
        /// <param name="ItemText"></param>
        private static void Process_ModuleCloseParen_For(string ItemText) {
            if (1 == 1) {
                BuildHoverState = BuildHoverStates.UndefinedState;
            }
            else {
                //syntax error
            }
        }

        private static bool IsFunctionReturnTypeToken(string itemText) {
            switch (itemText) {
                case "automatic":
                case "signed":
                case "unsigned":
                case "reg":
                case "logic":
                case "bit":
                case "integer":
                case "time":
                case "real":
                case "realtime":
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        ///    Process_FunctionNaming_For
        /// </summary>
        /// <param name="ItemText"></param>
        private static void Process_FunctionNaming_For(string ItemText) {
            switch (ItemText) {
                case "":
                    if (!string.IsNullOrEmpty(thisVariableDeclarationText) &&
                        !thisVariableDeclarationText.EndsWith(" ", StringComparison.Ordinal)) {
                        thisVariableDeclarationText += " ";
                    }
                    break;

                case ";":
                    thisVariableDeclarationText = string.Empty;
                    BuildHoverState = BuildHoverStates.UndefinedState;
                    break;

                default:
                    bool wasInsideSquareBracket = IsInsideSquareBracket;
                    SetBracketContentStatus_For(ItemText);

                    if (wasInsideSquareBracket || IsInsideSquareBracket || IsVerilogBracket(ItemText) ||
                        IsDelimiter(ItemText) || IsNumeric(ItemText) || IsVerilogValue(ItemText) ||
                        IsFunctionReturnTypeToken(ItemText)) {
                        thisVariableDeclarationText += ItemText;
                        break;
                    }

                    if (IsIdentifier(ItemText)) {
                        thisVariableDeclarationText += ItemText;
                        AddFunctionHoverItem(thisModuleName, ItemText, thisVariableDeclarationText);
                        thisFunctionName = ItemText;
                        thisFunctionScope = FunctionLocalScopeName(thisModuleName, thisFunctionName);
                        thisVariableDeclarationText = string.Empty;
                        BuildHoverState = BuildHoverStates.FunctionDeclarationRemainder;
                        break;
                    }

                    thisVariableDeclarationText += ItemText;
                    break;
            }
        }

        /// <summary>
        ///    Process_FunctionDeclarationRemainder_For
        /// </summary>
        /// <param name="ItemText"></param>
        private static void Process_FunctionDeclarationRemainder_For(string ItemText) {
            if (ItemText == ";") {
                BuildHoverState = BuildHoverStates.UndefinedState;
            }
        }

        /// <summary>
        ///    Process_TaskNaming_For
        /// </summary>
        /// <param name="ItemText"></param>
        private static void Process_TaskNaming_For(string ItemText) {
            switch (ItemText) {
                case "":
                    if (!string.IsNullOrEmpty(thisVariableDeclarationText) &&
                        !thisVariableDeclarationText.EndsWith(" ", StringComparison.Ordinal)) {
                        thisVariableDeclarationText += " ";
                    }
                    break;

                case ";":
                    thisVariableDeclarationText = string.Empty;
                    BuildHoverState = BuildHoverStates.UndefinedState;
                    break;

                default:
                    bool wasInsideSquareBracket = IsInsideSquareBracket;
                    SetBracketContentStatus_For(ItemText);

                    if (wasInsideSquareBracket || IsInsideSquareBracket || IsVerilogBracket(ItemText) ||
                        IsDelimiter(ItemText) || IsNumeric(ItemText) || IsVerilogValue(ItemText) ||
                        IsFunctionReturnTypeToken(ItemText)) {
                        thisVariableDeclarationText += ItemText;
                        break;
                    }

                    if (IsIdentifier(ItemText)) {
                        thisVariableDeclarationText += ItemText;
                        AddFunctionHoverItem(thisModuleName, ItemText, thisVariableDeclarationText);
                        thisFunctionName = ItemText;
                        thisFunctionScope = TaskLocalScopeName(thisModuleName, thisFunctionName);
                        thisVariableDeclarationText = string.Empty;
                        BuildHoverState = BuildHoverStates.TaskDeclarationRemainder;
                        break;
                    }

                    thisVariableDeclarationText += ItemText;
                    break;
            }
        }

        /// <summary>
        ///    Process_TaskDeclarationRemainder_For
        /// </summary>
        /// <param name="ItemText"></param>
        private static void Process_TaskDeclarationRemainder_For(string ItemText) {
            if (ItemText == ";") {
                BuildHoverState = BuildHoverStates.UndefinedState;
            }
        }

        /// <summary>
        ///    Process_VariableNaming_For
        /// </summary>
        /// <param name="ItemText"></param>
        private static void Process_VariableNaming_For(string ItemText) {
            if (thisHoverName == string.Empty) {
                // string a = "breakpoint";
                // TODO - how did we wend up here? (seen during multi-thread)
                //return;
            }
            // once we are naming a module parameter, we only end with a closing parenthesis, or a comman
            switch (ItemText) {
                case "":
                    if ((lastHoverItem == string.Empty) || (lastHoverItem == "\t")) {
                        // we'll ignore sequentual tabs, or alternating table-space
                        // only one space will be used
                    }
                    else {
                        thisVariableDeclarationText += " ";
                    }

                    break;

                case ";":
                    AddHoverItem(thisModuleName, thisHoverName, thisVariableDeclarationText);
                    thisVariableDeclarationText = string.Empty; // reminder we do this manually, as AddHoverItem does not know *what* it is adding
                    BuildHoverState = BuildHoverStates.UndefinedState;
                    break;

                case ",":
                    if (thisHoverName == string.Empty) {
                        // string a = "breakpoint";
                        // no hovername = nothing to do

                        BuildHoverState = BuildHoverStates.VariableMimicNaming; // Mimic naming is the same declaration but comma-delimited (e.g. input a,b // b has the input "mimic'd" )
                    }
                    else {
                        if (IsInsideSquigglyBracket) {
                            thisVariableDeclarationText += ItemText;
                            // BuildHoverState remains variable building
                        }
                        else {
                            AddHoverItem(thisModuleName, thisHoverName, thisVariableDeclarationText);
                            // since we encountered a comma, we will use the same declaration text for a new name, so replace this name with a blank
                            thisVariableDeclarationText = thisVariableDeclarationText.Replace(thisHoverName, "");

                            thisHoverName = string.Empty; // IMPORTANT: next identifier is the next variable name

                            BuildHoverState = BuildHoverStates.VariableMimicNaming; // Mimic naming is the same declaration but comma-delimited (e.g. input a,b // b has the input "mimic'd" )
                        }
                    }
                    break;

                case "input":
                case "output":
                case "inout":
                case "wire":
                case "reg":
                case "localparam":
                case "parameter":
                case "bit":
                case "logic":
                case "integer":
                    thisVariableDeclarationText += ItemText;
                    UpdateCurrentDeclarationVariableType(thisVariableDeclarationText);
                    break;

                case "endmodule":
                    // we're done naming a module
                    AddHoverItem(thisModuleName, thisHoverName, thisModuleParameterText);
                    BuildHoverState = BuildHoverStates.UndefinedState;
                    thisHoverName = string.Empty;
                    thisVariableDeclarationText = string.Empty;
                    thisModuleName = string.Empty;
                    thisFunctionName = string.Empty;
                    thisFunctionScope = string.Empty;
                    thisModuleParameterText = string.Empty;
                    break;

                default:
                    if (thisHoverName == string.Empty && IsIdentifier(ItemText) && !IsVerilogVariableSigner(ItemText)) {
                        if (IsInsideSquareBracket || IsInsideSquigglyBracket) {
                            // Identifier used in a range or concatenation is not a declared name
                            thisVariableDeclarationText += ItemText;
                            break;
                        }
                        else {
                            // IS a declared name
                        }
                        thisHoverName = ItemText;
                        thisVariableDeclarationText += ItemText;
                        break;
                    }

                    // TODO implement IsVerilogAssignment
                    if ((thisHoverName != string.Empty) || (ItemText == "=") || IsVerilogBracket(ItemText) || IsNumeric(ItemText) || IsVerilogValue(ItemText) || Is_BracketContent_For(thisModuleName, ItemText) || IsDelimiter(ItemText) || IsVerilogVariableSigner(ItemText)) {
                        // we continue building the declaration text
                        SetBracketContentStatus_For(ItemText);
                        if (IsVerilogValue(ItemText)) {
                            AddHoverItem(SCOPE_CONST, ItemText, ValueHoverText(ItemText)); // here we are naming a variable with a constant in the definition
                        }
                        // nothing at this time; we are still bulding the declaration part for the given thisHoverName (aka variable name)
                        thisVariableDeclarationText += ItemText;
                    }
                    else {
                        // we found a new hover name to assign declaration text
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
        private static void Process_VariableMimicNaming_For(string ItemText) {
            // once we are naming a module parameter, we only end with a closing parenthesis, or a comman
            switch (ItemText) {
                case "":
                    if ((lastHoverItem == string.Empty) || (lastHoverItem == "\t") || (lastHoverItem == ",")) {
                        // we'll ignore sequentual tabs, or alternating table-space, commas mean we are mimicing prior definition
                        // only one space will be used
                    }
                    else {
                        thisVariableDeclarationText += " ";
                    }
                    break;

                case ",":
                    AddHoverItem(thisModuleName, thisHoverName, thisVariableDeclarationText);
                    if (thisHoverName == string.Empty) {
                        // nothing to do!
                    }
                    else {
                        thisVariableDeclarationText = thisVariableDeclarationText.Replace(thisHoverName, "");
                        thisHoverName = string.Empty;
                    }
                    break;

                case ";":
                    AddHoverItem(thisModuleName, thisHoverName, thisVariableDeclarationText);
                    thisHoverName = string.Empty;
                    thisVariableDeclarationText = string.Empty;
                    BuildHoverState = BuildHoverStates.UndefinedState;
                    break;

                case "endmodule":
                    BuildHoverState = BuildHoverStates.UndefinedState;
                    thisFunctionName = string.Empty;
                    thisFunctionScope = string.Empty;
                    // we're done naming a module
                    //AddHoverItem(thisModuleName, thisHoverName, thisModuleParameterText);
                    //BuildHoverState = BuildHoverStates.ModuleNamed;
                    //thisHoverName = string.Empty;
                    //thisVariableDeclarationText = string.Empty;
                    //thisModuleName = string.Empty;
                    //thisModuleParameterText = string.Empty;
                    break;

                default:
                    // if we encounter a NamerKeyword during a sequence of comma-delimited vars, then this is a new type!
                    // e.g.  input a,b,  // this is input a; input b;
                    //       output c    // this is output c;
                    if (IsVerilogNamerKeyword(ItemText)) {
                        Process_UndefinedState_For(ItemText);
                    }
                    else {
                        if (IsVerilogVariableSigner(ItemText) || IsVerilogBracket(ItemText) || IsNumeric(ItemText) || IsVerilogValue(ItemText) || IsDelimiter(ItemText)) {
                            SetBracketContentStatus_For(ItemText);
                            if (IsVerilogValue(ItemText)) {
                                AddHoverItem(SCOPE_CONST, ItemText, ValueHoverText(ItemText));
                            }
                            // nothing at this time; we are still bulding the declaration part
                            thisVariableDeclarationText += ItemText;
                        }
                        else {
                            if (thisHoverName == string.Empty && IsIdentifier(ItemText) && !IsVerilogVariableSigner(ItemText)) {
                                if (IsInsideSquareBracket || IsInsideSquigglyBracket) {
                                    // Identifier inside is NOT a declared name
                                }
                                else {
                                    // Identifier not used in a range or concatenation IS a declared name
                                    thisHoverName = ItemText;
                                }
                            }
                            thisVariableDeclarationText += ItemText;
                        }
                    }
                    break;
            }
        }

        /// <summary>
        ///    Process_XXX_For - template
        /// </summary>
        /// <param name="ItemText"></param>
        private static void Process_XXX_For(string ItemText) {
            if (1 == 1) {
                BuildHoverState = BuildHoverStates.UndefinedState;
            }
            else {
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
            FunctionNaming,
            FunctionDeclarationRemainder,
            TaskNaming,
            TaskDeclarationRemainder,
        };


        public static void BuildHoverItems(string s, int lineNumber, int linePosition) {
            thisItemLineNumber = lineNumber;
            thisItemLinePosition = linePosition;
            BuildHoverItems(s);
        }

        public static void BuildHoverItems(string s) {
            string thisTrimmedItem = (s == null) ? "" : s.Trim();
            string priorHoverName = thisHoverName;
            string priorModuleName = thisModuleName;
            thisItemText = thisTrimmedItem;

            switch (BuildHoverState) {
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

                case BuildHoverStates.VariableMimicNaming: // comma-delimited types have the type copied (mimic) into hover text for each variable
                    Process_VariableMimicNaming_For(thisTrimmedItem);
                    break;

                case BuildHoverStates.FunctionNaming:
                    Process_FunctionNaming_For(thisTrimmedItem);
                    break;

                case BuildHoverStates.FunctionDeclarationRemainder:
                    Process_FunctionDeclarationRemainder_For(thisTrimmedItem);
                    break;

                case BuildHoverStates.TaskNaming:
                    Process_TaskNaming_For(thisTrimmedItem);
                    break;

                case BuildHoverStates.TaskDeclarationRemainder:
                    Process_TaskDeclarationRemainder_For(thisTrimmedItem);
                    break;

                default:
                    break;
            }
            if (thisHoverName != priorHoverName && thisHoverName == thisTrimmedItem) {
                thisHoverNameLineNumber = thisItemLineNumber;
                thisHoverNameLinePosition = thisItemLinePosition;
            }

            if (thisModuleName != priorModuleName && thisModuleName == thisTrimmedItem) {
                thisModuleNameLineNumber = thisItemLineNumber;
                thisModuleNameLinePosition = thisItemLinePosition;
            }

            lastHoverItem = thisTrimmedItem;
            if (thisTrimmedItem == string.Empty) {
                // not doing anything for whitespace
            }
            else {
                lastNonblankHoverItem = thisTrimmedItem;
            }

        }

        /// <summary>
        ///  BuildHoverItems - builds the Verilog variable hover text. Called in IEnumerable VerilogTokenTagger
        ///                    as each token text string is encountered.
        /// </summary>
        /// <param name="s"></param>

        public static void Dispose() {
            // see https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose
            //
            // there are no  unmanaged resources that need to be released at this time
            //
            // TODO cleanup
        }
    } // class
} // namespace
