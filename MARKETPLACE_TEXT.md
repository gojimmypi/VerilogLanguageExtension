# Verilog Language Extension Marketplace Text

Source control tracking of the published text at:

https://marketplace.visualstudio.com/items?itemName=gojimmypi.gojimmypi-verilog-language-extension

## Basic Information

Probably don't want to edit these without looking like an entirely new extension. Assign in [source.extension.vsixmanifest](./source.extension.vsixmanifest) settings.

- Internal Name: `gojimmypi-verilog-language-extension`
- VSIX ID: `CF0DCF14-5B8F-4B42-8386-9D37BB99F98E`
- Publisher: `gojimmypi`


## Overview

```text
Verilog Language Extension adds Verilog/SystemVerilog editor support to Visual Studio 2022 and later.

What it does:

- Adds syntax highlighting for Verilog/SystemVerilog source and header files.
- Supports `.v`, `.vh`, `.verilog`, `.sv`, and `.svh` files.
- Provides individually configurable colors for Verilog keywords and extension-specific display items.
- Highlights line comments, block comments, static strings, macros, variables, functions, duplicate declarations, and nested bracket depth.
- Shows QuickInfo hover text for Verilog keywords, modules, declarations, variables, and missing/unknown symbols.
- Adds code outlining/folding for modules, functions, tasks, begin/end blocks, case blocks, always blocks, if/else blocks, and preprocessor conditional regions.
- Handles paste, delete, multi-change edits, and larger Verilog files with background parsing and refresh support.
- Includes a Snapshot Exporter and local snapshot-regression tooling for extension development and validation.
- Includes a Verilog Project template preview for FPGA-oriented starter projects using external tools such as WSL, yosys, nextpnr, ecppack, and board-specific programmers.

Set custom keyword colors in `Tools - Options - Environment - Fonts and Colors`.

![image.png](image.png)

![Verilog-Tools-Options-Colors.png](Verilog-Tools-Options-Colors.png)

Suggestions and comments welcome; see GitHub issues. Pull Requests are appreciated.
```

## Supported Visual Studio versions

Assign in [source.extension.vsixmanifest](./source.extension.vsixmanifest) settings.

Runs on both Visual Studio 2022 and Visual Studio 2026 or later for all editions (Community/Pro/Enterprise).

Compiled as "Any CPU" but only tested on x64. Likely works on `arm64` as well. The extension is not supported on Visual Studio 2019 or earlier.

```
    <Installation>
        <InstallationTarget Id="Microsoft.VisualStudio.Community" Version="[17.0,)">
            <ProductArchitecture>amd64</ProductArchitecture>
        </InstallationTarget>
        <InstallationTarget Id="Microsoft.VisualStudio.Pro" Version="[17.0,)">
            <ProductArchitecture>amd64</ProductArchitecture>
        </InstallationTarget>
        <InstallationTarget Id="Microsoft.VisualStudio.Enterprise" Version="[17.0,)">
            <ProductArchitecture>amd64</ProductArchitecture>
        </InstallationTarget>
    </Installation>
```
