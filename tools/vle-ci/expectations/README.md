# Expectations

Expectation files are optional targeted invariants. They supplement the complete
snapshot baselines and are not required one-for-one with test files.

- `issue10.expect.json` â€” focused regression checks for issue 10.
- `issue21.expect.json` â€” focused regression checks for issue 21.
- `systemverilog_keywords.expect.json` â€” verifies SystemVerilog keyword guidance, hover text, function/argument classification, scoped user-defined types, and forbidden type-name misclassification.
- `integer_type.expect.json` â€” verifies that `integer` is `Verilog_integer`, while the declaration name remains the parameter/localparam symbol.
