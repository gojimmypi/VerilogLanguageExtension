`define VLE_SHOW_INACTIVE_CODE
module ifdef_optout_macro;
`ifdef UNDEFINED_FEATURE
    logic normally_colored_despite_inactive_branch;
`else
    logic active_branch;
`endif
endmodule
