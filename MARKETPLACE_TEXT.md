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
Verilog Extension for Visual Studio. Basic keyword highlighting for Verilog editing in Visual Studio (files with ".v" or ".verilog" extension). Free and Open Source. This is an initial version and my first experience at developing extensions for Visual Studio.  Supported items include individual colorization for each Verilog keyword, Directive keywords are all currently highlighted the same color. Comment text in green, plus some code outlining with round, square, and squiggly brackets. Autocomplete still needs work. Install in Visual Studio via Tools - Extensions and Updates.

This latest release includes colorization for keywords, variables, and bracket depth, along with hover text.



Project PREVIEW: Synthesis directly in Visual Studio for iCEBreaker, Orange Crab, tinyFPGA and ULX3S (requires WSL and FPGA toolchain)

Fixed in 0.3.5.4 Fixed Ctrl-C in a multiple pane Window such as the Git Diff, this "value cannot be null" error would occur.

CHANGED in 0.3.5.3 Added support for ONLY VS 2022 and VS 2026. See GitHub for older versions of Visual Studio.

Fixed in 0.3.3 is more syntax highlighting, particularly variable, write initialization hover text, initial load, etc.

Fixed in 0.3.1 is syntax highlighting for VS2015 that stopped working when multiple different file extensions were added.

Fixed in 0.2.1 is processing for large files. Files larger than 8K are now processed in the background. Upon completion of background processing, the viewport is _not_ refreshed at this time. Mouse hovers and key presses can help nudge updates. Stay tuned for future releases coming soon.

![image.png](image.png)

Set custom keyword colors in `Tools - Options - Colors`:

![Verilog-Tools-Options-Colors.png](Verilog-Tools-Options-Colors.png)

Suggestions and comments welcome; see GitHub issues, Pull Requests appreciated.
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
