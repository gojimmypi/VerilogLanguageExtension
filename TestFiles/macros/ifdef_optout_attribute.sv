(* NO_INACTIVE_MACRO_CODE *)
module ifdef_optout_attribute;
`ifdef UNDEFINED_FEATURE
    logic normally_colored_despite_inactive_branch;
`else
    logic active_branch;
`endif
endmodule
