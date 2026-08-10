`default_nettype none

`include "include-files/project_config.v"

`ifdef SIM_JTAG_CORE_TB
    `timescale 1ns / 1ps
    logic active_from_project_config;
`else
    logic inactive_project_config_else;
`endif

`ifdef INCLUDED_ZERO_VALUE
    logic active_included_zero_value;
`endif

`ifdef NESTED_CONFIG_ACTIVE
    logic active_from_nested_include;
`endif

`ifdef CYCLE_A_REACHED
    logic active_after_include_cycle_guard;
`endif

`ifdef CYCLE_B_REACHED
    logic active_from_second_cycle_file;
`endif

`ifdef UNKNOWN_BRANCH
    `include "include-files/inactive_config.vh"
`endif

`ifdef INACTIVE_INCLUDE_MUST_NOT_LEAK
    logic inactive_include_leaked_macro;
`else
    logic active_inactive_include_did_not_leak;
`endif

module ifdef_include_highlighting;
endmodule
