Program Technical Details
=========================

This page describes the major implementation layers in the repository and how
they interact inside Visual Studio.

High-level architecture
-----------------------

The extension is a classic Visual Studio SDK/MEF extension. The main flow is:

.. code-block:: text

   .v/.vh/.sv/.svh/.verilog file
              |
              v
      Visual Studio content type: "verilog"
              |
              +-----------------------------+
              |                             |
              v                             v
      token/tag parsing               editor services
              |                     (QuickInfo, outline,
              v                      navigation, Peek)
       classification tags                   |
              |                              |
              +--------------+---------------+
                             v
                     Visual Studio editor

The VSIX also contains a Visual Studio package for commands and a project
-template asset.

Build and runtime targets
-------------------------

``VerilogLanguage.csproj`` currently targets:

* ``.NETFramework`` / .NET Framework 4.7.2 for the extension assembly.
* Visual Studio SDK packages from the 17.x SDK line.
* Visual Studio SDK build targets selected from Visual Studio 18.0 when present,
  otherwise 17.0.

The VSIX installation manifest in the supplied build declares Community,
Professional, and Enterprise installation targets beginning at Visual Studio
17.0, with ``amd64`` product architecture and the Core Editor prerequisite.
That model allows one VSIX identity to be applicable to supported Visual Studio
2022 and Visual Studio 2026 installations.

Content type and MEF attachment
-------------------------------

``Classification/VerilogClassifier.cs`` exports the ``verilog`` content type
and maps these filename extensions to it:

``.v``, ``.vh``, ``.verilog``, ``.sv``, and ``.svh``.

Other components use ``[ContentType("verilog")]`` so they are created only for
buffers that Visual Studio has classified as Verilog content.

Tokenization and parsing
------------------------

The token/tag layer lives primarily under ``VerilogToken`` and ``Globals``.
``VerilogTokenTagger`` is responsible for editor token tags and coordinates
reparse/refresh behavior. Important implementation details include:

* Per-buffer lifecycle and explicit disposal.
* Reparse-completion watching so the editor refreshes after asynchronous parse
  publication.
* Block-comment state cached by text snapshot.
* Attribute scanning state cached by text snapshot.
* Preprocessor line state and include-dependency tracking.
* Bounded reparse timer behavior to avoid an indefinitely active watcher.

The global parsing layer stores parse information keyed by file and editor
snapshot version. QuickInfo and navigation code request parse data that matches
the current snapshot rather than intentionally using arbitrary stale global
state.

Preprocessor evaluator
----------------------

``VerilogToken/VerilogPreprocessorEvaluator.cs`` evaluates directives for
syntax-highlighting purposes. It tracks macro values/definitions, conditional
state, and includes in textual source order.

Current implementation details include:

* Maximum include nesting depth: 64.
* Relative include resolution from the including source file's directory.
* Macro expansion for include names with a bounded expansion depth.
* Include dependency records containing path, existence, modification time,
  and length.
* Conditional state for ``ifdef``, ``ifndef``, ``elsif``, ``else``, and
  ``endif``.
* Hover text explaining why a conditional branch is inactive.

This evaluator supports editor presentation. It is not intended to replace a
standards-complete compiler preprocessor.

Classification pipeline
-----------------------

``VerilogTokenTagger`` produces token information and
``VerilogClassifier`` converts those tags into Visual Studio classification
tags. ``Classification/ClassificationType.cs`` declares classification types,
while ``Classification/ClassificationFormat.cs`` defines user-visible editor
formats.

The format layer includes individual keyword classifications and higher-level
formats for constructs such as SystemVerilog guidance, typedef aliases,
attributes, duplicate variables, system tasks/functions, and nested brackets.

QuickInfo
---------

``Intellisense/VerilogQuickInfoSource.cs`` implements Visual Studio async
QuickInfo. It combines several sources of information:

* Static documentation for recognized language keywords.
* Static documentation for supported ``$`` system tasks/functions.
* Parser-produced declaration and scope information.
* Function- and task-local symbol lookup.
* Macro definition information.
* SystemVerilog guidance classifications.

QuickInfo owns its tag aggregator and cleans it up when the source is disposed.

Completion
----------

The repository exports ``VerilogCompletionSourceProvider`` for the ``verilog``
content type. The current completion list is intentionally empty: the source
contains example placeholder entries, but no production autocomplete words are
currently inserted. Do not describe VLE as providing full IntelliSense code
completion until that list and its semantic behavior are implemented.

Navigation
----------

Navigation is split among:

* ``Navigation/VerilogDefinitionResolver.cs``
* ``Navigation/VerilogReferenceFinder.cs``
* ``Navigation/FindAllReferencesCommand.cs``
* ``Peek/PeekDefinitionCommand.cs``
* ``Peek/VerilogPeekDefinitionSource.cs``

The VSCT command table adds **Go To Definition**, **Peek Definition**, and
**Find All References** to the code-window context menu. Package initialization
registers the command handlers needed by the Visual Studio shell.

Outlining
---------

``Outlining/VerilogOutliningTagger.cs`` performs a lightweight line/token scan
and builds folding regions. It maintains stacks for scopes and conditional
directives, plus a small pending-block mechanism used to associate ``always``,
``if``, or ``else`` headers with following ``begin`` blocks.

Snapshot exporter and CI
------------------------

The snapshot subsystem under ``SnapshotExporter`` captures editor state in a
stable JSON model for regression tests. It records parser tokens,
classifications, VLE tag spans, symbols, source hash, and run metadata.

Local CI wrappers under ``scripts`` and implementation tools under
``tools/vle-ci`` compare current snapshots with checked-in baselines. This
allows editor behavior to be regression-tested without relying only on visual
inspection.

VS package and commands
-----------------------

``VerilogLanguagePackage.cs`` derives from ``AsyncPackage`` and is registered
for background loading after the Visual Studio shell is initialized. The
package registers menu resources and initializes commands on the UI thread when
required.

``VerilogLanguagePackage.vsct`` defines:

* **Tools -> Snapshot Export**.
* **Code window -> Go To Definition**.
* **Code window -> Peek Definition**.
* **Code window -> Find All References**.

VSIX repair helper
------------------

``scripts/Repair-VerilogLanguageVSIX.ps1`` is intentionally separate from the
extension runtime. It is a Windows/PowerShell maintenance utility for stale or
ambiguous VSIX registration state, especially on side-by-side Visual Studio
installations. See :doc:`vsix-repair` for the complete design and command
reference.
