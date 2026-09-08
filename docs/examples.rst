Examples
========

These examples focus on editor behavior rather than simulation or synthesis.

Basic Verilog editing
---------------------

Open a ``counter.v`` file:

.. code-block:: verilog

   module counter (
       input  wire       clk,
       input  wire       reset,
       output reg  [7:0] count
   );

       always @(posedge clk) begin
           if (reset) begin
               count <= 8'h00;
           end
           else begin
               count <= count + 1'b1;
           end
       end

   endmodule

Expected editor behavior includes keyword classification, nested bracket
classification, variable/declaration information, and outlining for the module
and procedural blocks.

SystemVerilog types and typedef aliases
---------------------------------------

.. code-block:: verilog

   typedef logic [31:0] word_t;

   module register_file (
       input  logic  clk,
       input  word_t write_data
   );

       word_t latched_data;

       always_ff @(posedge clk) begin
           latched_data <= write_data;
       end

   endmodule

VLE has dedicated SystemVerilog classifications and can classify resolved
``typedef`` aliases separately from built-in data-type keywords.

Macro-controlled source
-----------------------

.. code-block:: verilog

   `define USE_FAST_PATH

   module example;

   `ifdef USE_FAST_PATH
       wire fast_path_enabled = 1'b1;
   `else
       wire fast_path_enabled = 1'b0;
   `endif

   endmodule

The preprocessor evaluator tracks macro definitions in source order and can
mark conditional branches according to the active macro state used for editor
highlighting.

Include-driven macro state
--------------------------

Suppose ``config.vh`` contains:

.. code-block:: verilog

   `define BOARD_ULX3S

and the source contains:

.. code-block:: verilog

   `include "config.vh"

   `ifdef BOARD_ULX3S
       localparam integer CLOCK_HZ = 50000000;
   `else
       localparam integer CLOCK_HZ = 25000000;
   `endif

VLE processes supported includes in textual order for preprocessor-highlighting
purposes. The include resolver uses the source file location when resolving a
relative include path.

Show inactive macro code
------------------------

A file can opt out of inactive-code suppression/highlighting behavior with one
of the recognized markers. For example:

.. code-block:: verilog

   // VLE: SHOW_INACTIVE_CODE

or:

.. code-block:: verilog

   `define VLE_SHOW_INACTIVE_CODE

These markers affect editor presentation only. They are not intended as a
replacement for the HDL toolchain's own preprocessor behavior.

Go To Definition
----------------

Given:

.. code-block:: verilog

   module example;
       reg [7:0] counter_value;

       always @(*) begin
           counter_value = 8'h55;
       end
   endmodule

Place the caret on ``counter_value`` in the assignment, right-click, and select
**Go To Definition**. If the declaration is resolved, VLE moves the editor to
``reg [7:0] counter_value``.

Peek Definition
---------------

Use **Peek Definition** on the same identifier when you want to inspect the
declaration without leaving the current editor context.

Find All References
-------------------

Use **Find All References** on a parsed identifier to locate occurrences that
VLE's reference finder associates with that symbol.

Export a regression snapshot
----------------------------

For extension development, open a Verilog file and select:

**Tools -> Snapshot Export**

The command reports the path to a ``.snapshot.json`` file. With default
settings the file is written below:

.. code-block:: text

   %TEMP%\VerilogLanguageSnapshot

The JSON is designed for automated comparison of classifications, token tags,
parser state, and symbol information.

Repair a stale Visual Studio 2026 registration
----------------------------------------------

If Visual Studio 2026 reports that VLE is already installed even though it is
missing from **Installed -> All**, run the repair helper from the repository
root:

.. code-block:: powershell

   .\scripts\Repair-VerilogLanguageVSIX.ps1 `
       -VsixPath "$env:USERPROFILE\Downloads\VerilogLanguage_0.5.1.67.vsix" `
       -UninstallOnly

Then start Visual Studio 2026 and install VLE from:

**Extensions -> Manage Extensions -> Browse**

This is the preferred recovery sequence for the stale-registration case. See
:doc:`vsix-repair` for the full command reference.
