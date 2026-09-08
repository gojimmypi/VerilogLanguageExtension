Troubleshooting
===============

Extension is installed but no syntax highlighting appears
----------------------------------------------------------

Check all of the following:

* The file uses ``.v``, ``.vh``, ``.verilog``, ``.sv``, or ``.svh``.
* VLE appears under **Extensions -> Manage Extensions -> Installed -> All**.
* The extension is enabled.
* Restart Visual Studio after a new installation or update.
* Test with a small known-good HDL file to rule out a file-association issue.

If only one Visual Studio major version is affected on a side-by-side machine,
verify the extension state inside that exact Visual Studio instance.

"Already installed to all applicable products" but VLE is missing
------------------------------------------------------------------

This can indicate stale per-user VSIX registration state. It is especially
confusing when Visual Studio 2022 and Visual Studio 2026 coexist because a
working registration in one instance does not guarantee a healthy registration
in the other.

Use :doc:`vsix-repair`. The safest recovery is usually:

1. Close all Visual Studio instances.
2. Run ``Repair-VerilogLanguageVSIX.ps1 -UninstallOnly`` with the downloaded
   VSIX path.
3. Start the affected Visual Studio instance.
4. Install VLE from **Extensions -> Manage Extensions -> Browse**.

Do not hard-code an ``instanceId`` copied from another computer.

VSIX is not applicable to the selected Visual Studio installation
------------------------------------------------------------------

Inspect the VSIX manifest installation targets and the actual Visual Studio
product/version/architecture. The current VLE build is intended for 64-bit
Community, Professional, and Enterprise installations beginning with Visual
Studio 17.0.

If a repair log contains ``NoApplicableSKUsException``, confirm that the VSIX
is the expected VLE package and that the selected instance is a supported
Visual Studio product.

Navigation does not find a symbol
---------------------------------

VLE navigation depends on the parser's current symbol information. Common
causes of incomplete results include:

* Source that the lightweight parser cannot fully recognize.
* A declaration outside the files/state currently available to the parser.
* Unsaved or rapidly changing text before parse publication catches up.
* Complex preprocessing or generate constructs beyond the resolver's current
  model.

Try saving the file, waiting for the editor to reparse, and testing a simpler
local declaration. Snapshot export can help developers see what symbols and
classifications VLE produced.

Outlining is missing or unusual
-------------------------------

The outlining tagger intentionally performs a lightweight structural scan.
Highly unusual formatting, incomplete blocks, or complex macro-generated
syntax can prevent a region from being inferred.

QuickInfo appears stale after editing
-------------------------------------

The current implementation keys parse data to the editor snapshot and refreshes
classification after parse publication. If stale UI remains after a large
edit, save the document or reopen the file to force a clean editor state. For a
repeatable bug, add the case to ``TestFiles`` and capture a snapshot regression.

Visual Studio Experimental Instance problems
--------------------------------------------

Development profiles can become stale independently of the normal Visual
Studio profile. Only remove directories that are clearly Experimental Instance
profiles (commonly names ending in ``Exp``). Do not delete the user's normal
profile as a routine development workaround.

Where are VSIX repair logs?
---------------------------

``Repair-VerilogLanguageVSIX.ps1`` writes unique logs under ``%TEMP%``. The
script prints each exact path. Search the tail of the install log for
applicability, access, or package-registration errors.

Where are snapshot exports?
---------------------------

Unless overridden by environment/config settings:

.. code-block:: text

   %TEMP%\VerilogLanguageSnapshot

See :doc:`operations` for the snapshot environment variables.

Last-resort manual extension cleanup
------------------------------------

Do not begin by deleting Visual Studio profile files. Follow the text-only
manual fallback in :doc:`vsix-repair`, which requires positively identifying
the extension directory by its ``extension.vsixmanifest`` identity before any
manual deletion.
