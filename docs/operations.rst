Operations
==========

This page describes normal user-visible operations and the development-only
snapshot command.

Editing Verilog and SystemVerilog
---------------------------------

Open any supported ``.v``, ``.vh``, ``.verilog``, ``.sv``, or ``.svh`` file.
VLE assigns the ``verilog`` content type and attaches its editor components.
No special solution or project type is required for syntax highlighting.

Hover for information
---------------------

Place the mouse over a recognized token. Depending on the token and available
parse state, QuickInfo may show:

* Keyword documentation.
* System task/function help.
* The declaration of a variable or module-related symbol.
* Function- or task-local declaration information.
* Macro definition information.
* Guidance for selected SystemVerilog constructs.

Navigate to a declaration
-------------------------

Place the caret on an identifier in a Verilog editor, then right-click in the
code window.

**Go To Definition**
    Moves the editor to the resolved declaration when VLE can identify one.

**Peek Definition**
    Opens the Visual Studio Peek experience around the resolved declaration.

**Find All References**
    Searches for references using VLE's symbol/reference logic and presents
    the results through Visual Studio.

If the command cannot resolve the symbol, verify that the relevant files are
available to the parser and that the source is syntactically recognizable.
See :doc:`technical-details` for the resolver model and limitations.

Fold HDL regions
----------------

Use the normal Visual Studio outlining controls in the editor margin. VLE
publishes outlining regions for modules, functions, tasks, case blocks,
begin/end blocks, common procedural blocks, and conditional preprocessor
regions.

Customize classification colors
--------------------------------

Open:

**Tools -> Options -> Environment -> Fonts and Colors**

The VLE display items can be adjusted independently. This is the supported way
to tailor colors; editing the extension binaries or VSIX is not required.

Snapshot Export
---------------

The package registers **Tools -> Snapshot Export**. This command exports a JSON
snapshot of the active Verilog editor to the configured snapshot directory.
With no explicit configuration, the default output directory is:

.. code-block:: text

   %TEMP%\VerilogLanguageSnapshot

The default run name is ``manual``.

Snapshot export can also be enabled automatically when files are opened. The
current implementation recognizes these environment variables:

``VLE_SNAPSHOT_ENABLE``
    Set to ``1``, ``true``, or ``yes`` to enable export-on-open.

``VLE_SNAPSHOT_GATE_FILE``
    If set to the path of an existing file, export-on-open is enabled.

``VLE_SNAPSHOT_OUTPUT_DIR``
    Output directory for snapshot JSON files.

``VLE_SNAPSHOT_RUN_NAME``
    Logical run name stored in exported snapshots.

``VLE_SNAPSHOT_DELAY_MS``
    Delay before export-on-open. The default is 350 ms.

``VLE_REPO_ROOT``
    Repository root used to make exported source paths relative.

``VLE_GIT_COMMIT``
    Commit identifier stored in snapshot metadata.

The exporter also reads ``VerilogLanguage.ExportSnapshots.config`` from
``%TEMP%``. Environment variables take precedence over config-file values.

Install, repair, and remove the VSIX
------------------------------------

For ordinary installation and removal, use Visual Studio's Extension Manager.
When side-by-side Visual Studio instances have inconsistent extension state,
use ``scripts/Repair-VerilogLanguageVSIX.ps1`` as documented in
:doc:`vsix-repair`.

The repair script defaults to Visual Studio 2026 only and does not hard-code an
``instanceId``. It discovers instances with ``vswhere.exe`` and passes the
resulting ID to ``VSIXInstaller.exe``.
