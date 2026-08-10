# Verilog Project Template for Visual Studio

The Verilog Project Template for Visual Studio assumes that the [Windows Subsystem For Linux](https://en.wikipedia.org/wiki/Windows_Subsystem_for_Linux) (aka "WSL") is
already installed, as well as several key components such as [verilator](https://en.wikipedia.org/wiki/Verilator),
[yosys](http://www.clifford.at/yosys/), [nextpnr](https://github.com/YosysHQ/nextpnr), the [ecppack](https://github.com/SymbiFlow/prjtrellis/blob/master/libtrellis/tools/ecppack.cpp)
utility in [Project Trellis](https://github.com/SymbiFlow/prjtrellis), and the iCE40 tools from Project IceStorm for iCE40-based boards.

To actually upload the synthesized bitstream onto an FPGA, a board-specific programmer is needed. For the ULX3S and ULX4M,
[fujprog](https://github.com/kost/fujprog/issues) can be used. Other boards use their usual programmers, such as `iceprog`,
`tinyprog`, or `dfu-util`, depending on the selected board and makefile target.

One option is to install everything for the complete [ULX3S Toolchain](https://github.com/ulx3s/ulx3s-toolchain).
This is probably best if you want to keep up with the latest versions and have full control. Although tailored for the
ULX3S with ESP32 development, this toolchain is easily adapted to other FPGA devices with minimal changes.

Builds ("synthesis" in FPGA lingo) are accomplished by selecting a configuration and platform, then right-clicking and selecting `Build` in the project.

Board example sources and board-specific support files live under [boards](./boards/). The project root is reserved for
the Visual Studio project files, documentation, shared configuration, and helper folders. Do not put board-specific example
source files or generated synthesis artifacts in the project root.

The board example files are organized by board directory:

- [boards/ulx3s](./boards/ulx3s/) contains the ULX3S example source, scripts, and constraints used by the ULX3S 12K, 25K, 45K, and 85K platforms.
- [boards/ulx4m](./boards/ulx4m/) contains the ULX4M-LS and ULX4M-LD example source, makefiles, and minimal LPF constraints.
- [boards/icebreaker](./boards/icebreaker/) contains the iCEBreaker example source, makefile, and PCF constraints.
- [boards/orangecrab](./boards/orangecrab/) contains the OrangeCrab example source, makefile, and PCF constraints.
- [boards/tinyfpga_bx](./boards/tinyfpga_bx/) contains the tinyFPGA BX example source, makefile, and PCF constraints.
- [boards/icestick](./boards/icestick/) is reserved for iCEStick-related notes and support files.

Generated synthesis artifacts are written under `build/`, for example `build/ulx3s-85k/ulx3s.bit`,
`build/ulx4m-ld-85k/ulx4m.bit`, `build/icebreaker/top_icebreaker.bin`, `build/orangecrab/blink.dfu`,
and `build/tinyfpga-bx/TinyFPGA_B.bin`, instead of the project root.

For reference: Visual Studio invokes the custom FPGA "build", "upload", "verify", and "clean" steps from MSBuild targets in the C#-style project file.
For the Verilog project template, the active generated project file is [VerilogProject.csproj](./VerilogProject.csproj), which is named by
[Verilog.vstemplate](./Verilog.vstemplate) and becomes `$safeprojectname$.csproj` when Visual Studio creates a new project.
The imported [ProjectPlatform](./ProjectPlatform/) files contain board/platform-specific support. Yes, it is a bit wonky to have this be a C# app; if anyone
knows how to change that to something better, please submit a PR.

The Windows helper scripts live in the [scripts](./scripts/) directory. The `build/` directory is reserved for generated FPGA artifacts.
Those scripts figure out which WSL path to use, then call Linux `make` with a board-local makefile, for example
[boards/ulx3s/Makefile-ULX3S-85F.mk](./boards/ulx3s/Makefile-ULX3S-85F.mk),
[boards/ulx4m/Makefile-ULX4M-LD-85F.mk](./boards/ulx4m/Makefile-ULX4M-LD-85F.mk),
[boards/icebreaker/main.mk](./boards/icebreaker/main.mk),
[boards/orangecrab/Makefile](./boards/orangecrab/Makefile), or
[boards/tinyfpga_bx/Makefile](./boards/tinyfpga_bx/Makefile).

## Template source files not generated into projects

These files are useful in the template source tree, template packaging, or local development, but should not be treated as
board source files for generated Verilog projects:

- `Verilog.vstemplate`
- `__PreviewImage.png`
- `VerilogProjectTemplate.csproj`
- `VerilogProjectTemplate.csproj.new`
- `VerilogProjectTemplate.csproj.user`
- `VerilogProject.csproj.user`
- `VerilogProject.sln`

If `ProjectTemplate.csproj`, `ProjectTemplate.csproj.new`, or `ProjectTemplate.csproj.bak` exists in this VerilogProject folder, it is legacy compatibility debris.
The active Verilog generated project file is `VerilogProject.csproj`.

Root-level board example or generated files such as `top.v`, `top.ys`, `top_icebreaker.v`, `ulx3s.ys`, `ulx3s_empty.config`, and `blinky.json`
should be moved into the appropriate board directory or removed if obsolete.

## Installing WSL

This template was originally developed on Ubuntu 18.04.4 LTS for WSL. Current WSL Ubuntu releases should also work if the required FPGA tools are installed.
See the [Microsoft instructions](https://docs.microsoft.com/en-us/windows/wsl/install-win10).
Check the version with `lsb_release -a`. If you need to update:

```bash
sudo apt-get upgrade
sudo apt dist-upgrade -y
sudo do-release-upgrade
```

## Installing Verilator

```bash
sudo apt-get install verilator
```

## Installing yosys / nextpnr / ecppack / icestorm

There are several options available to install the toolchain:

Recommended (but perhaps challenging): see each repo and follow the instructions there.

These individual scripts are also available:

- [verilator](https://github.com/ulx3s/ulx3s-toolchain/blob/master/install_verilator.sh)
- [yosys](https://github.com/ulx3s/ulx3s-toolchain/blob/master/install_yosys.sh)
- [nextpnr](https://github.com/ulx3s/ulx3s-toolchain/blob/master/install_nextpnr.sh)
- [ecppack - Project Trellis](https://github.com/ulx3s/ulx3s-toolchain/blob/master/install_prjtrellis.sh)
- [icestorm](https://github.com/ulx3s/ulx3s-toolchain/blob/master/install_icestorm.sh)

Just fetch the repo and run each individual script:

```bash
export WORKSPACE=/mnt/s/workspace
export THIS_ULX3S_DEVICE=LFE5U-85F
export ULX3S_COM=/dev/ttyS8

sudo apt-get update
mkdir -p "$WORKSPACE"

git clone https://github.com/ulx3s/ulx3s-toolchain.git
cd ulx3s-toolchain

## Ensure all scripts are executable.
chmod +x install_set_permissions.sh

./install_set_permissions.sh
./install_verilator.sh
./install_yosys.sh
./install_prjtrellis.sh
./install_icestorm.sh
./install_nextpnr.sh
```

Kost has precompiled binary [releases](https://github.com/alpin3/ulx3s/releases).

For information on updating these project files, see the [README](../README.md).

Readme version 1.2
