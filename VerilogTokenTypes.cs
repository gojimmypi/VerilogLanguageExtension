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


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VerilogLanguage
{
    public enum VerilogTokenTypes
    {
        Verilog_always,
        Verilog_assign,
        Verilog_automatic,
        Verilog_begin,
        Verilog_case,
        Verilog_casex,
        Verilog_casez,
        Verilog_cell,
        Verilog_config,
        Verilog_deassign,
        Verilog_default,
        Verilog_defparam,
        Verilog_design,
        Verilog_disable,
        Verilog_edge,
        Verilog_else,
        Verilog_end,
        Verilog_endcase,
        Verilog_endconfig,
        Verilog_endfunction,
        Verilog_endgenerate,
        Verilog_endmodule,
        Verilog_endprimitive,
        Verilog_endspecify,
        Verilog_endtable,
        Verilog_endtask,
        Verilog_event,
        Verilog_for,
        Verilog_force,
        Verilog_forever,
        Verilog_fork,
        Verilog_function,
        Verilog_generate,
        Verilog_genvar,
        Verilog_if,
        Verilog_ifnone,
        Verilog_incdir,
        Verilog_include,
        Verilog_initial,
        Verilog_inout,
        Verilog_input,
        Verilog_instance,
        Verilog_join,
        Verilog_liblist,
        Verilog_library,
        Verilog_localparam,
        Verilog_macromodule,
        Verilog_module,
        Verilog_negedge,
        Verilog_noshowcancelled,
        Verilog_output,
        Verilog_parameter,
        Verilog_posedge,
        Verilog_primitive,
        Verilog_pulsestyle_ondetect,
        Verilog_pulsestyle_onevent,
        Verilog_reg,
        Verilog_release,
        Verilog_repeat,
        Verilog_scalared,
        Verilog_showcancelled,
        Verilog_signed,
        Verilog_specify,
        Verilog_specparam,
        Verilog_strength,
        Verilog_table,
        Verilog_task,
        Verilog_tri,
        Verilog_tri0,
        Verilog_tri1,
        Verilog_triand,
        Verilog_wand,
        Verilog_trior,
        Verilog_wor,
        Verilog_trireg,
        Verilog_unsigned,
        Verilog_use,
        Verilog_vectored,
        Verilog_wait,
        Verilog_while,
        Verilog_wire,

        Verilog_Directive, // note that all directives are colorized the same

        //
        Verilog_Comment,
        Verilog_Bracket,
        Verilog_BracketContent

    }
}
