# Expectations

Expectation files are optional targeted invariants. They supplement the complete
snapshot baselines and are not required one-for-one with test files.

- `issue10.expect.json` - focused regression checks for issue 10.
- `issue21.expect.json` - focused regression checks for issue 21.
- `systemverilog_keywords.expect.json` - verifies SystemVerilog keyword guidance,
  hover text, function and argument classification, module-scope handling, and
  forbidden user-defined-type misclassification.
- `integer_type.expect.json` - verifies that `integer` is `Verilog_integer`, while
  the declaration name remains the parameter or localparam symbol.
- `z386.expect.json` - verifies that commas inside declaration initializers and
  function calls do not create false duplicate variable declarations, and that
  sibling SystemVerilog block-local declarations remain separate scopes.
