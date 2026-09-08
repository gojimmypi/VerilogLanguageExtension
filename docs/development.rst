Development and Testing
=======================

This page summarizes the repository workflow for modifying VLE itself.

Development environment
-----------------------

Use a Visual Studio installation with the Visual Studio Extension Development
workload and the SDK/build components required by ``VerilogLanguage.csproj``.
The project targets .NET Framework 4.7.2 and uses Visual Studio SDK packages.

The project file can select Visual Studio 18.0 SDK build targets when they are
installed and otherwise falls back to 17.0 targets.

Build the VSIX
--------------

From a Visual Studio developer command prompt, build the project normally with
MSBuild. The repository also includes:

.. code-block:: text

   scripts\build_vsix.bat

That helper cleans ``bin`` and ``obj``, rebuilds the project, expands the
resulting VSIX, and prints selected packaging artifacts for inspection.

Debug in an Experimental Instance
---------------------------------

Open the extension project in Visual Studio and press **F5**. Visual Studio
launches an Experimental Instance so the extension can be exercised without
replacing the normal installed copy.

Experimental profiles can become stale during repeated extension development.
If a development-only profile must be reset, make sure the directory is the
``Exp`` instance, not the user's normal Visual Studio profile.

Snapshot regression testing
---------------------------

The repository contains wrapper scripts under ``scripts`` and the underlying
snapshot implementation under ``tools/vle-ci``.

A normal local sequence is:

.. code-block:: powershell

   .\scripts\ci-pass.ps1
   .\scripts\ci-baseline.ps1
   .\scripts\ci-check.ps1

``ci-pass.ps1``
    Regenerates the all-testfiles manifest and checks current snapshots against
    expectations without approving new baselines.

``ci-baseline.ps1``
    Updates the approved snapshot baseline after intentional behavior changes.

``ci-check.ps1``
    Performs a fresh comparison against the approved baseline.

Use baseline updates only when the changed classifications/tokens/hover data
are intentional and reviewed.

Single-file testing
-------------------

For focused work:

.. code-block:: powershell

   .\scripts\check-file.ps1 -SourceFile TestFiles\comma.v

The script can also reuse an existing Experimental Instance or leave Visual
Studio open for inspection.

Project template development
----------------------------

The source-of-truth template files live under:

.. code-block:: text

   AddedExtensionProjectTemplates\VerilogProject

The generated VSIX input is:

.. code-block:: text

   ProjectTemplates\CSharp\1033\VerilogProject.zip

Regenerate and validate it with:

.. code-block:: powershell

   .\tools\templates\Build-ProjectTemplates.ps1
   .\tools\templates\Test-ProjectTemplate.ps1

The validation checks for missing files, stale build artifacts, bad project
references, accidental local paths, and other template-packaging problems.

Adding or changing classifications
----------------------------------

The normal path is:

1. Add or update token/parsing logic under ``VerilogToken`` / ``Globals``.
2. Declare the classification in ``Classification/ClassificationType.cs``.
3. Define its editor format in ``Classification/ClassificationFormat.cs``.
4. Update classifier/tagger mapping as needed.
5. Add representative source files under ``TestFiles``.
6. Run the local snapshot tests.
7. Review the snapshot delta before updating a baseline.

Release metadata
----------------

Keep the VSIX identity/version, assembly version metadata, release notes, and
snapshot release metadata aligned. The repository contains release-health
checks under ``tools/vle-ci`` to catch mismatches.

Read the repository's ``RELEASE_NOTES.md`` and ``scripts/README.md`` for the
current release and local tooling details.
