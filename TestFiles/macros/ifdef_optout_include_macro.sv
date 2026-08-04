`include "include-files/show_inactive_config.vh"
module ifdef_optout_include_macro;
`ifdef UNDEFINED_FEATURE
    logic normally_colored_despite_inactive_branch;
`else
    logic active_branch;
`endif
endmodule
