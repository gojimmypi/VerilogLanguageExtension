Getting Started
===============

This page covers the normal installation path and the minimum steps needed to
confirm that VLE is active.

Requirements
------------

The current VSIX manifest declares support for:

* Microsoft Visual Studio Community, Professional, or Enterprise.
* Visual Studio 2022 (17.x) and Visual Studio 2026 (18.x).
* 64-bit Visual Studio (``amd64``).
* The Visual Studio Core Editor component.
* Microsoft .NET Framework support required by the VSIX package.

Use a Windows version supported by the Visual Studio release you are running.

Recommended installation
------------------------

The preferred installation method is from inside the Visual Studio instance
that you want to use:

1. Start Visual Studio.
2. Select **Extensions -> Manage Extensions**.
3. Select **Browse**.
4. Search for ``Verilog Language Extension``, ``VerilogLanguage``, or ``FPGA``.
5. Choose the extension published by ``gojimmypi``.
6. Install it and restart Visual Studio if requested.
7. Return to **Extensions -> Manage Extensions -> Installed -> All** and
   confirm that **Verilog Language Extension** is listed and enabled.

Installing from inside Visual Studio is especially useful on computers that
have Visual Studio 2022 and Visual Studio 2026 installed side by side because
the active Visual Studio instance selects its own extension registration.

Manual VSIX installation
------------------------

A VSIX can also be downloaded from the Visual Studio Marketplace. For normal
manual installation, use the Microsoft Visual Studio Version Selector
(``VSLauncher.exe``) rather than guessing the path to a particular
``VSIXInstaller.exe``.

A typical command is:

.. code-block:: powershell

   & "${env:ProgramFiles(x86)}\Common Files\Microsoft Shared\MSEnv\VSLauncher.exe" `
       "$env:USERPROFILE\Downloads\VerilogLanguage_0.5.1.67.vsix"

If the launcher or installer reports that the extension is already installed
but the extension is missing from Visual Studio, do not repeatedly install over
the stale state. Use the documented repair procedure in :doc:`vsix-repair`.

Open your first HDL file
------------------------

VLE activates for these file extensions:

* ``.v``
* ``.vh``
* ``.verilog``
* ``.sv``
* ``.svh``

Open a file such as:

.. code-block:: verilog

   module counter (
       input  wire       clk,
       input  wire       reset,
       output reg  [7:0] count
   );

       always @(posedge clk) begin
           if (reset)
               count <= 8'h00;
           else
               count <= count + 1'b1;
       end

   endmodule

You should immediately see Verilog classifications and theme-aware colors.
Hover over a recognized keyword, declaration, macro, or supported system task
to see QuickInfo.

Confirm the content type
------------------------

If a supported file opens as plain text, first verify that the extension is
enabled under **Extensions -> Manage Extensions -> Installed -> All**. Also
confirm that the filename uses one of the supported extensions above.

The extension exports a Visual Studio content type named ``verilog`` and maps
each supported file extension to that content type. Most editor features are
MEF components that are enabled only for that content type.

Customize the editor
--------------------

Open:

**Tools -> Options -> Environment -> Fonts and Colors**

Search the display items for entries beginning with ``Verilog`` or
``SystemVerilog``. VLE defines separate classifications for many keywords,
comments, strings, declarations, variables, nested bracket levels, attributes,
system tasks/functions, and other HDL constructs.

See :doc:`customization` for details.

Next steps
----------

* :doc:`features` - what VLE recognizes and displays.
* :doc:`operations` - commands and normal editor workflows.
* :doc:`examples` - concrete Verilog/SystemVerilog examples.
* :doc:`technical-details` - how the extension is implemented.
* :doc:`vsix-repair` - repair a stale Visual Studio extension registration.
