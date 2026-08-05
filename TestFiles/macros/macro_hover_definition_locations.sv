// Macro hover definition-location validation.
`ifdef NEVER_DEFINED_MACRO
`endif

`define LOCAL_DEFINED_MACRO 1
`ifdef LOCAL_DEFINED_MACRO
`endif
localparam int local_macro_value = `LOCAL_DEFINED_MACRO;
localparam int missing_macro_value = `MISSING_REFERENCE_MACRO;

`include "include-files/macro_hover_config.vh"
`ifdef INCLUDED_DEFINED_MACRO
`endif
localparam int included_macro_value = `INCLUDED_DEFINED_MACRO;

`define REMOVED_MACRO 3
`undef REMOVED_MACRO
localparam int removed_macro_value = `REMOVED_MACRO;
