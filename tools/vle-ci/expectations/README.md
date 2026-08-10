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
- `ifdef_active_highlighting.expect.json` - verifies source-order macro state,
  active branch coloring, inactive whole-line tags, nested conditionals, `elsif`,
  `undef`, zero-valued defined macros, and commented-out directives.
- `ifdef_inactive_hover.expect.json` - verifies branch-specific inactive hover
  guidance, including missing macros, defined macros, selected earlier branches,
  the file-level opt-out guidance, and preservation of the outer inactive reason.
- `ifdef_include_highlighting.expect.json` - verifies macro definitions imported
  from relative, nested, and cyclic include files, zero-valued included macros,
  and suppression of macro definitions from includes in inactive branches.
- `ifdef_include_source_order.expect.json` - verifies that an include affects only
  conditionals that occur after the include in source order.
- `ifdef_optout_attribute.expect.json` - verifies the
  `(* NO_INACTIVE_MACRO_CODE *)` file-level presentation opt-out.
- `ifdef_optout_include_macro.expect.json` - verifies the
  `NO_INACTIVE_MACRO_CODE` opt-out when defined by an active include file.
- `ifdef_optout_macro.expect.json` - verifies the
  `VLE_SHOW_INACTIVE_CODE` macro opt-out.
- `ifdef_optout_pragma.expect.json` - verifies the
  `// VLE: SHOW_INACTIVE_CODE` pragma opt-out.
