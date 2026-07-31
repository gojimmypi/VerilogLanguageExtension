# Expectations

4 files for special cases, leave them here

- `issue10.expect.json` — focused regression checks for issue 10.
- `issue21.expect.json` — focused regression checks for issue 21.
- `systemverilog_keywords.expect.json` — verifies SystemVerilog keyword support, hover text, function/argument classification, and explicitly forbids type names such as sample_t from being classified as variables or functions.
- `integer_type.expect.json` — verifies that integer is Verilog_integer, while the declaration name remains the parameter/localparam symbol.
