//***************************************************************************
// 
//    Copyright (c) Microsoft Corporation. All rights reserved.
//    This code is licensed under the Visual Studio SDK license terms.
//    THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
//    ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
//    IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
//    PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
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
        VerilogAssign,  

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
        Verilog_wire

    }
}
