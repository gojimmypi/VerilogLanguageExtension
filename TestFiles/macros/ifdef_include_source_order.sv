`default_nettype none

`ifdef SIM_JTAG_CORE_TB
    `timescale 1ns / 1ps
    logic inactive_before_project_config_include;
`endif

`include "include-files/project_config.v"

`ifdef SIM_JTAG_CORE_TB
    logic active_after_project_config_include;
`endif

module ifdef_include_source_order;
endmodule
