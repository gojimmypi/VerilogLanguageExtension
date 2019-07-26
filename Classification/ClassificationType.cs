//***************************************************************************
// 
//  MIT License
//
//  Copyright(c) 2019 gojimmypi
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


using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace VerilogLanguage
{
    internal static class OrdinaryClassificationDefinition
    {
        #region Type definition

        /// <summary>
        /// Defines the "Verilog_always" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("always")]
        internal static ClassificationTypeDefinition Verilog_always = null;


        /// <summary>
        /// Defines the "Verilog_assign" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("assign")]
        internal static ClassificationTypeDefinition Verilog_assign = null;


        /// <summary>
        /// Defines the "Verilog_automatic" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("automatic")]
        internal static ClassificationTypeDefinition Verilog_automatic = null;


        /// <summary>
        /// Defines the "Verilog_begin" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("begin")]
        internal static ClassificationTypeDefinition Verilog_begin = null;


        /// <summary>
        /// Defines the "Verilog_case" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("case")]
        internal static ClassificationTypeDefinition Verilog_case = null;


        /// <summary>
        /// Defines the "Verilog_casex" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("casex")]
        internal static ClassificationTypeDefinition Verilog_casex = null;


        /// <summary>
        /// Defines the "Verilog_casez" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("casez")]
        internal static ClassificationTypeDefinition Verilog_casez = null;


        /// <summary>
        /// Defines the "Verilog_cell" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("cell")]
        internal static ClassificationTypeDefinition Verilog_cell = null;


        /// <summary>
        /// Defines the "Verilog_config" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("config")]
        internal static ClassificationTypeDefinition Verilog_config = null;


        /// <summary>
        /// Defines the "Verilog_deassign" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("deassign")]
        internal static ClassificationTypeDefinition Verilog_deassign = null;


        /// <summary>
        /// Defines the "Verilog_default" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("default")]
        internal static ClassificationTypeDefinition Verilog_default = null;


        /// <summary>
        /// Defines the "Verilog_defparam" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("defparam")]
        internal static ClassificationTypeDefinition Verilog_defparam = null;


        /// <summary>
        /// Defines the "Verilog_design" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("design")]
        internal static ClassificationTypeDefinition Verilog_design = null;


        /// <summary>
        /// Defines the "Verilog_disable" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("disable")]
        internal static ClassificationTypeDefinition Verilog_disable = null;


        /// <summary>
        /// Defines the "Verilog_edge" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("edge")]
        internal static ClassificationTypeDefinition Verilog_edge = null;


        /// <summary>
        /// Defines the "Verilog_else" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("else")]
        internal static ClassificationTypeDefinition Verilog_else = null;


        /// <summary>
        /// Defines the "Verilog_end" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("end")]
        internal static ClassificationTypeDefinition Verilog_end = null;


        /// <summary>
        /// Defines the "Verilog_endcase" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("endcase")]
        internal static ClassificationTypeDefinition Verilog_endcase = null;


        /// <summary>
        /// Defines the "Verilog_endconfig" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("endconfig")]
        internal static ClassificationTypeDefinition Verilog_endconfig = null;


        /// <summary>
        /// Defines the "Verilog_endfunction" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("endfunction")]
        internal static ClassificationTypeDefinition Verilog_endfunction = null;


        /// <summary>
        /// Defines the "Verilog_endgenerate" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("endgenerate")]
        internal static ClassificationTypeDefinition Verilog_endgenerate = null;


        /// <summary>
        /// Defines the "Verilog_endmodule" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("endmodule")]
        internal static ClassificationTypeDefinition Verilog_endmodule = null;


        /// <summary>
        /// Defines the "Verilog_endprimitive" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("endprimitive")]
        internal static ClassificationTypeDefinition Verilog_endprimitive = null;


        /// <summary>
        /// Defines the "Verilog_endspecify" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("endspecify")]
        internal static ClassificationTypeDefinition Verilog_endspecify = null;


        /// <summary>
        /// Defines the "Verilog_endtable" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("endtable")]
        internal static ClassificationTypeDefinition Verilog_endtable = null;


        /// <summary>
        /// Defines the "Verilog_endtask" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("endtask")]
        internal static ClassificationTypeDefinition Verilog_endtask = null;


        /// <summary>
        /// Defines the "Verilog_event" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("event")]
        internal static ClassificationTypeDefinition Verilog_event = null;


        /// <summary>
        /// Defines the "Verilog_for" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("for")]
        internal static ClassificationTypeDefinition Verilog_for = null;


        /// <summary>
        /// Defines the "Verilog_force" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("force")]
        internal static ClassificationTypeDefinition Verilog_force = null;


        /// <summary>
        /// Defines the "Verilog_forever" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("forever")]
        internal static ClassificationTypeDefinition Verilog_forever = null;


        /// <summary>
        /// Defines the "Verilog_fork" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("fork")]
        internal static ClassificationTypeDefinition Verilog_fork = null;


        /// <summary>
        /// Defines the "Verilog_function" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("function")]
        internal static ClassificationTypeDefinition Verilog_function = null;


        /// <summary>
        /// Defines the "Verilog_generate" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("generate")]
        internal static ClassificationTypeDefinition Verilog_generate = null;


        /// <summary>
        /// Defines the "Verilog_genvar" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("genvar")]
        internal static ClassificationTypeDefinition Verilog_genvar = null;


        /// <summary>
        /// Defines the "Verilog_if" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("if")]
        internal static ClassificationTypeDefinition Verilog_if = null;


        /// <summary>
        /// Defines the "Verilog_ifnone" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("ifnone")]
        internal static ClassificationTypeDefinition Verilog_ifnone = null;


        /// <summary>
        /// Defines the "Verilog_incdir" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("incdir")]
        internal static ClassificationTypeDefinition Verilog_incdir = null;


        /// <summary>
        /// Defines the "Verilog_include" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("include")]
        internal static ClassificationTypeDefinition Verilog_include = null;


        /// <summary>
        /// Defines the "Verilog_initial" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("initial")]
        internal static ClassificationTypeDefinition Verilog_initial = null;


        /// <summary>
        /// Defines the "Verilog_inout" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("inout")]
        internal static ClassificationTypeDefinition Verilog_inout = null;


        /// <summary>
        /// Defines the "Verilog_input" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("input")]
        internal static ClassificationTypeDefinition Verilog_input = null;


        /// <summary>
        /// Defines the "Verilog_instance" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("instance")]
        internal static ClassificationTypeDefinition Verilog_instance = null;


        /// <summary>
        /// Defines the "Verilog_join" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("join")]
        internal static ClassificationTypeDefinition Verilog_join = null;


        /// <summary>
        /// Defines the "Verilog_liblist" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("liblist")]
        internal static ClassificationTypeDefinition Verilog_liblist = null;


        /// <summary>
        /// Defines the "Verilog_library" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("library")]
        internal static ClassificationTypeDefinition Verilog_library = null;


        /// <summary>
        /// Defines the "Verilog_localparam" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("localparam")]
        internal static ClassificationTypeDefinition Verilog_localparam = null;


        /// <summary>
        /// Defines the "Verilog_macromodule" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("macromodule")]
        internal static ClassificationTypeDefinition Verilog_macromodule = null;


        /// <summary>
        /// Defines the "Verilog_module" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("module")]
        internal static ClassificationTypeDefinition Verilog_module = null;


        /// <summary>
        /// Defines the "Verilog_negedge" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("negedge")]
        internal static ClassificationTypeDefinition Verilog_negedge = null;


        /// <summary>
        /// Defines the "Verilog_noshowcancelled" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("noshowcancelled")]
        internal static ClassificationTypeDefinition Verilog_noshowcancelled = null;


        /// <summary>
        /// Defines the "Verilog_output" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("output")]
        internal static ClassificationTypeDefinition Verilog_output = null;


        /// <summary>
        /// Defines the "Verilog_parameter" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("parameter")]
        internal static ClassificationTypeDefinition Verilog_parameter = null;


        /// <summary>
        /// Defines the "Verilog_posedge" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("posedge")]
        internal static ClassificationTypeDefinition Verilog_posedge = null;


        /// <summary>
        /// Defines the "Verilog_primitive" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("primitive")]
        internal static ClassificationTypeDefinition Verilog_primitive = null;


        /// <summary>
        /// Defines the "Verilog_pulsestyle_ondetect" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("pulsestyle_ondetect")]
        internal static ClassificationTypeDefinition Verilog_pulsestyle_ondetect = null;


        /// <summary>
        /// Defines the "Verilog_pulsestyle_onevent" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("pulsestyle_onevent")]
        internal static ClassificationTypeDefinition Verilog_pulsestyle_onevent = null;


        /// <summary>
        /// Defines the "Verilog_reg" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("reg")]
        internal static ClassificationTypeDefinition Verilog_reg = null;


        /// <summary>
        /// Defines the "Verilog_release" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("release")]
        internal static ClassificationTypeDefinition Verilog_release = null;


        /// <summary>
        /// Defines the "Verilog_repeat" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("repeat")]
        internal static ClassificationTypeDefinition Verilog_repeat = null;


        /// <summary>
        /// Defines the "Verilog_scalared" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("scalared")]
        internal static ClassificationTypeDefinition Verilog_scalared = null;


        /// <summary>
        /// Defines the "Verilog_showcancelled" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("showcancelled")]
        internal static ClassificationTypeDefinition Verilog_showcancelled = null;


        /// <summary>
        /// Defines the "Verilog_signed" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("signed")]
        internal static ClassificationTypeDefinition Verilog_signed = null;


        /// <summary>
        /// Defines the "Verilog_specify" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("specify")]
        internal static ClassificationTypeDefinition Verilog_specify = null;


        /// <summary>
        /// Defines the "Verilog_specparam" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("specparam")]
        internal static ClassificationTypeDefinition Verilog_specparam = null;


        /// <summary>
        /// Defines the "Verilog_strength" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("strength")]
        internal static ClassificationTypeDefinition Verilog_strength = null;


        /// <summary>
        /// Defines the "Verilog_table" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("table")]
        internal static ClassificationTypeDefinition Verilog_table = null;


        /// <summary>
        /// Defines the "Verilog_task" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("task")]
        internal static ClassificationTypeDefinition Verilog_task = null;


        /// <summary>
        /// Defines the "Verilog_tri" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("tri")]
        internal static ClassificationTypeDefinition Verilog_tri = null;


        /// <summary>
        /// Defines the "Verilog_tri0" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("tri0")]
        internal static ClassificationTypeDefinition Verilog_tri0 = null;


        /// <summary>
        /// Defines the "Verilog_tri1" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("tri1")]
        internal static ClassificationTypeDefinition Verilog_tri1 = null;


        /// <summary>
        /// Defines the "Verilog_triand" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("triand")]
        internal static ClassificationTypeDefinition Verilog_triand = null;


        /// <summary>
        /// Defines the "Verilog_wand" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("wand")]
        internal static ClassificationTypeDefinition Verilog_wand = null;


        /// <summary>
        /// Defines the "Verilog_trior" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("trior")]
        internal static ClassificationTypeDefinition Verilog_trior = null;


        /// <summary>
        /// Defines the "Verilog_wor" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("wor")]
        internal static ClassificationTypeDefinition Verilog_wor = null;


        /// <summary>
        /// Defines the "Verilog_trireg" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("trireg")]
        internal static ClassificationTypeDefinition Verilog_trireg = null;


        /// <summary>
        /// Defines the "Verilog_unsigned" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("unsigned")]
        internal static ClassificationTypeDefinition Verilog_unsigned = null;


        /// <summary>
        /// Defines the "Verilog_use" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("use")]
        internal static ClassificationTypeDefinition Verilog_use = null;


        /// <summary>
        /// Defines the "Verilog_vectored" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("vectored")]
        internal static ClassificationTypeDefinition Verilog_vectored = null;


        /// <summary>
        /// Defines the "Verilog_wait" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("wait")]
        internal static ClassificationTypeDefinition Verilog_wait = null;


        /// <summary>
        /// Defines the "Verilog_while" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("while")]
        internal static ClassificationTypeDefinition Verilog_while = null;


        /// <summary>
        /// Defines the "Verilog_wire" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("wire")]
        internal static ClassificationTypeDefinition Verilog_wire = null;


        #endregion

        #region directives
        /// <summary>
        /// Defines the "Verilog_Directive" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("Directive")]
        internal static ClassificationTypeDefinition Verilog_Directive = null;

        #endregion

        #region comments
        /// <summary>
        /// Defines the "Verilog_Comment" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("Comment")]
        internal static ClassificationTypeDefinition Verilog_Comment = null;
        #endregion

        #region Bracket
        /// <summary>
        /// Defines the "Verilog_BracketContent" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("Bracket")]
        internal static ClassificationTypeDefinition Verilog_Bracket = null;
        #endregion

        #region Bracket0
        /// <summary>
        /// Defines the "Verilog_BracketContent" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("Bracket0")]
        internal static ClassificationTypeDefinition Verilog_Bracket0 = null;
        #endregion

        #region Bracket1
        /// <summary>
        /// Defines the "Verilog_BracketContent" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("Bracket1")]
        internal static ClassificationTypeDefinition Verilog_Bracket1 = null;
        #endregion

        #region Bracket2
        /// <summary>
        /// Defines the "Verilog_BracketContent" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("Bracket2")]
        internal static ClassificationTypeDefinition Verilog_Bracket2 = null;
        #endregion

        #region Bracket3
        /// <summary>
        /// Defines the "Verilog_BracketContent" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("Bracket3")]
        internal static ClassificationTypeDefinition Verilog_Bracket3 = null;
        #endregion

        #region Bracket4
        /// <summary>
        /// Defines the "Verilog_BracketContent" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("Bracket4")]
        internal static ClassificationTypeDefinition Verilog_Bracket4 = null;
        #endregion

        #region Bracket5
        /// <summary>
        /// Defines the "Verilog_BracketContent" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("Bracket5")]
        internal static ClassificationTypeDefinition Verilog_Bracket5 = null;
        #endregion

        #region BracketContent
        /// <summary>
        /// Defines the "Verilog_BracketContent" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("BracketContent")]
        internal static ClassificationTypeDefinition Verilog_BracketContent = null;
        #endregion

        #region variable
        /// <summary>
        /// Defines the "Verilog_Variable" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("Variable")]
        internal static ClassificationTypeDefinition Verilog_Variable = null;

        /// <summary>
        /// Defines the "Verilog_Variable_input" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("Variable_input")]
        internal static ClassificationTypeDefinition Verilog_Variable_input = null;

        /// <summary>
        /// Defines the "Verilog_Variable_output" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("Variable_output")]
        internal static ClassificationTypeDefinition Verilog_Variable_output = null;

        /// <summary>
        /// Defines the "Verilog_Variable_inout" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("Variable_inout")]
        internal static ClassificationTypeDefinition Verilog_Variable_inout = null;

        /// <summary>
        /// Defines the "Verilog_Variable_wire" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("Variable_wire")]
        internal static ClassificationTypeDefinition Verilog_Variable_wire = null;

        /// <summary>
        /// Defines the "Verilog_Variable_reg" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("Variable_reg")]
        internal static ClassificationTypeDefinition Verilog_Variable_reg = null;

        /// <summary>
        /// Defines the "Verilog_Variable_parameter" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("Variable_parameter")]
        internal static ClassificationTypeDefinition Verilog_Variable_parameter = null;

        /// <summary>
        /// Defines the "Verilog_Variable_duplicate" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("Variable_duplicate")]
        internal static ClassificationTypeDefinition Verilog_Variable_duplicate = null;


        #endregion

        #region value
        /// <summary>
        /// Defines the "Verilog_Comment" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("Value")]
        internal static ClassificationTypeDefinition Verilog_Value = null;
        #endregion
    }
}
