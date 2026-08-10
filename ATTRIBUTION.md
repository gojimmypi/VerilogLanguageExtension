# ATTRIBUTION

Verilog Language Extension is an independent Visual Studio extension project. This
file acknowledges public examples, documentation, and reference material that
informed development, packaging, testing, and release work.

This file is not a third-party license manifest and does not replace the project
license. Unless a source file says otherwise, the source code in this repository
is authored for this project. References listed here were consulted for design
patterns, Visual Studio extension behavior, packaging, or feature comparison.
They are not endorsements by the referenced authors or organizations.

This project was inspired by open-source tools, templates, documentation,
and publicly available reference material.

## Microsoft Visual Studio SDK and documentation

This project acknowledges Microsoft Visual Studio SDK documentation, templates,
and examples for Visual Studio extension development, including guidance and
samples related to:

- VSIX package structure and source.extension.vsixmanifest metadata.
- AsyncPackage registration and background loading.
- VSCT command tables, menu commands, command placement, and command visibility.
- Editor integration patterns for language services, classification, tagging,
  QuickInfo, and navigation-style commands.
- Visual Studio experimental-instance development, extension packaging, and
  Marketplace publishing workflow.

References consulted include:

- Microsoft Visual Studio Extensibility Samples
  https://github.com/microsoft/VSSDK-Extensibility-Samples
- Creating an Extension with a Menu Command
  https://learn.microsoft.com/visualstudio/extensibility/creating-an-extension-with-a-menu-command
- Use AsyncPackage to load VSPackages in the background
  https://learn.microsoft.com/visualstudio/extensibility/how-to-use-asyncpackage-to-load-vspackages-in-the-background
- VSCT XML schema reference and Visual Studio extensibility documentation
  https://learn.microsoft.com/visualstudio/extensibility/

The Microsoft VSSDK sample repository is published by Microsoft and identifies
itself as MIT licensed at the repository level. Microsoft documentation and
product names remain the property of Microsoft.

## Mads Kristensen and VSIX community examples

This project also acknowledges Mads Kristensen and related VSIX community sample
projects for practical Visual Studio extension examples and best-practice
patterns. These examples were useful references for understanding extension
structure, command visibility, package loading behavior, options patterns, and
async extension design.

References consulted include:

- Mads Kristensen OptionsSample
  https://github.com/madskristensen/OptionsSample
- Mads Kristensen VisibilityConstraintsSample
  https://github.com/madskristensen/VisibilityConstraintsSample
- Mads Kristensen AsyncToolWindowSample
  https://github.com/madskristensen/AsyncToolWindowSample
- VS Extensibility Essentials
  https://github.com/VsixCommunity/ExtensibilityEssentials
- VSIX Cookbook
  https://www.vsixcookbook.com/
- Mads Kristensen Visual Studio extensibility posts and examples
  https://www.madskristensen.net/

Before copying code directly from any referenced repository, check that repository's
LICENSE file and retain any required copyright, license, and attribution notices.

## Tools and Assistance

Development of this project included the use of automated code analysis and review tools,
including AI-assisted tooling to improve code quality and correctness.

All final code and design decisions were made by the author.

## Verilog, SystemVerilog, and editor feature references

Feature planning and comparisons were also informed by existing Verilog and
SystemVerilog editor tooling, public documentation, and common HDL workflows.
These references helped evaluate expected editor behaviors such as syntax
colorization, declaration lookup, module navigation, hover information,
preprocessor handling, and project/file indexing. No endorsement or direct code
reuse is implied.

References considered include:

- mshr-h Verilog HDL support for VS Code
  https://github.com/mshr-h/vscode-verilog-hdl-support
- Visual Studio Marketplace extension metadata and publishing examples
  https://marketplace.visualstudio.com/
- Verilog/SystemVerilog language and tooling behavior as observed from public
  editor extensions, linters, simulators, and user workflows.

## Individual acknowledgments

The author gratefully acknowledges Barry Nolte, who has helped over the years
with guidance, technical discussions, answers to technical questions, and
encouragement related to Microsoft development tools and Visual Studio extension
work.

- Barry Nolte
  - https://bsky.app/profile/barrynolte.bsky.social
  - https://x.com/BarryNolte

## Trademarks and names

Microsoft, Visual Studio, Visual Studio Code, VSIX, and related marks are
trademarks or registered trademarks of Microsoft Corporation. Other product,
project, and author names are the property of their respective owners.

## Maintenance note

When adding source code, assets, generated files, or substantial snippets based
on third-party material, update this file or add a THIRD-PARTY-NOTICES.md file
with the exact upstream source, copyright notice, license, and file-level
attribution required by that source.
