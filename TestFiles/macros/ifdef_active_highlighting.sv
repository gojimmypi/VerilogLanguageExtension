`define FEATURE_ZERO 0
`define SELECTED_FEATURE 1
`define SCALE(value) ((value) * 2)

module ifdef_active_highlighting;

`ifdef FEATURE_ZERO
    logic active_even_when_macro_value_is_zero;
`else
    wire inactive_feature_zero_else;
`endif

`undef FEATURE_ZERO

`ifdef FEATURE_ZERO
    reg inactive_after_undef;
`elsif SELECTED_FEATURE
    logic active_elsif_branch;
`else
    integer inactive_final_else;
`endif

`ifdef UNKNOWN_OUTER
    `define INNER_ONLY 1
    wire inactive_outer_branch;
`else
    `ifndef INNER_ONLY
        logic active_nested_ifndef;
    `else
        logic inactive_nested_else;
    `endif
`endif

/* `ifdef COMMENTED_OUT_DIRECTIVE */
logic active_after_commented_directive;

endmodule
