module ifdef_inactive_hover;

`ifdef SIM_JTAG_CORE_TB
    logic inactive_missing_sim_macro;
`else
    logic active_without_sim_macro;
`endif

`define PRESENT_FEATURE 1
`ifndef PRESENT_FEATURE
    logic inactive_because_macro_is_defined;
`endif

`ifdef PRESENT_FEATURE
    logic active_first_branch;
`elsif SECOND_FEATURE
    logic inactive_elsif_after_selected_branch;
`else
    logic inactive_else_after_selected_branch;
`endif

`ifdef MISSING_OUTER
    `ifdef PRESENT_FEATURE
        logic nested_inactive_uses_outer_reason;
    `endif
`endif

endmodule
