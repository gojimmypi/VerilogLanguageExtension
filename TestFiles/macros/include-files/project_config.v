`ifndef VLE_PROJECT_CONFIG_V
`define VLE_PROJECT_CONFIG_V

`define SIM_JTAG_CORE_TB 1
`define INCLUDED_ZERO_VALUE 0
`define NESTED_CONFIG_FILE "nested/nested_config.vh"
`include `NESTED_CONFIG_FILE
`include "cycle_a.vh"

`endif
