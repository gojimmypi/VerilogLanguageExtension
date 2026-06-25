# Tiny Tapeout Verilog Source Files

This is the main project source directory.

## Files

 - `src\config.json` - edit with caution.

 - `src\project_config.v` - project-wide parameter values and macros. Use by source in other directories requires `Makefile` edits (e.g. `tt/tt_tool.py`)

 - `src\project.v` - the main template shim. Keep it simple for portability.
 - `src\tt_um_main.v` - the main project file, which instantiates the JTAG, SPI, UART and TRNG cores.

 - `src\UART\uart_rx_min.v` - a simple UART receiver core, which receives ASCII characters and outputs them as 8-bit values.
 - `src\UART\uart_tx_min.v` - a simple UART transmitter core, which sends 8-bit values as ASCII characters.
 - `src\UART\uart_trng_ascii_core.v` - a simple UART core that receives ASCII characters and outputs them as 8-bit values, and also includes a TRNG core that generates random numbers and sends them over UART when a specific command is received.

 - `src\TRNG\trng_cfg_ascii_core.v` - a simple TRNG core that can be configured via UART commands, and sends random numbers over UART when a specific command is received. This is a more complex version of the `uart_trng_ascii_core` that allows for configuration of the TRNG parameters.
 - `src\TRNG\trng_stub.v` - a stub TRNG core that can be used for testing the UART functionality without the complexity of a real TRNG. It generates pseudo-random numbers based on a simple counter and some bit manipulation, and sends them over UART when a specific command is received. This can be useful for testing the UART communication and command parsing without needing a real TRNG implementation.
 - `src\TRNG\trng_lab_core.v` - an optional TRNG lab core that provides an alternative to the `trng_stub` for experimentation and testing purposes.

## Config Edits

Although the GH Actions were all green, digging into the logs some concerns were observed. See [suggestion](https://discord.com/channels/1009193568256135208/1513299711975489566/1513680659447812276).

From [LibreLane Checking the reports](https://librelane.readthedocs.io/en/stable/additional_material/caravel/macro_first_hardening/index.html#checking-the-reports):

> Increase the slew/cap repair margins using DESIGN_REPAIR_MAX_SLEW_PCT and DESIGN_REPAIR_MAX_CAP_PCT. The default value is 20%. You may increase it as part of your implementation process:

`DESIGN_REPAIR_MAX_SLEW_PCT` - Tell the flow to apply N% slew margin when repairing slew. Instead of only trying to meet the real library max transition limit, it tries to make transitions faster than the limit by about N%.

`GRT_DESIGN_REPAIR_MAX_SLEW_PCT` - Applies the same idea to the post-global-route repair stage. That is important because after global routing, the tool has a better estimate of real wire parasitics.

> Enable post-global routing design optimizations using `RUN_POST_GRT_DESIGN_REPAIR`:

These changes have been applied / added to `src\config.json`:

```
  "RUN_POST_GRT_DESIGN_REPAIR": "true",
  "RUN_POST_GPL_DESIGN_REPAIR": "true",

  "DESIGN_REPAIR_MAX_SLEW_PCT": 30,
  "GRT_DESIGN_REPAIR_MAX_SLEW_PCT": 30,
```

The design is intended for 25 MHz but is intentionally set to `"CLOCK_PERIOD": 20` (50 MHz) over-constraint.

First GDS Post Design Repair log in GF180: [GRT / GPL Design Repair Test #49](https://github.com/gojimmypi/ttgf-UART-FSM-TRNG-Lab/actions/runs/27211329394/job/80340895326)
in [Commit d3155f9](https://github.com/gojimmypi/ttgf-UART-FSM-TRNG-Lab/commit/d3155f9d3418fc884a32badff31be2cce4a5a792).

Second Max Slew Rate 30% Percent log in GF180: [Add REPAIR_MAX_SLEW_PCT 30% #50](https://github.com/gojimmypi/ttgf-UART-FSM-TRNG-Lab/actions/runs/27216708803)
in [commit b7862a0](https://github.com/gojimmypi/ttgf-UART-FSM-TRNG-Lab/commit/b7862a07a85e2058a95b6d6190a75b42c49837e5).


GF180 failure: [Increase slew repair to 40 percent #52](https://github.com/gojimmypi/ttgf-UART-FSM-TRNG-Lab/actions/runs/27223337383)

GF180 was not better at 35/40: [Decrease DESIGN_REPAIR_MAX_SLEW_PCT to 35 #53](https://github.com/gojimmypi/ttgf-UART-FSM-TRNG-Lab/actions/runs/27225823087/job/80392609949)

GF180 mixed result at 30/35: [Decrease Design/GRT to 30/35 #54](https://github.com/gojimmypi/ttgf-UART-FSM-TRNG-Lab/actions/runs/27226761526/job/80395909787)

GF180 mixed result at 32/32: [Test Design/GRT repairs to 32/32 #55](https://github.com/gojimmypi/ttgf-UART-FSM-TRNG-Lab/actions/runs/27228008872/job/80400293219)

```text
30/30 = best balanced GF180 result
32/32 = better slew count, slightly worse setup
30/35 = slightly better slew, worse timing/cap
35/40 = much worse timing
40/40 = flow failure
```

Even with the edits and all the green checks in the GH actions, there are still corner setup and slew violations at 125C.

```
max_ss_125C_3v00: WNS -1.5718 ns, TNS -13.5390 ns, 25 violations
nom_ss_125C_3v00: WNS -1.2557 ns, TNS  -6.4502 ns, 13 violations
min_ss_125C_3v00: WNS -0.9883 ns, TNS  -3.1216 ns,  9 violations
```

As the target test clock is 25 MHz, the design was also tested at `"CLOCK_PERIOD": 40,` See [CLOCK_PERIOD: 40 #87](https://github.com/gojimmypi/ttgf-UART-FSM-TRNG-Lab/actions/runs/27361579885)

The result was _better_ but not perfect.

```
| Metric                 | GDS 86, 20 ns / 50 MHz | GDS 87, 40 ns / 25 MHz | Result                       |
| ---------------------- | ---------------------: | ---------------------: | ---------------------------- |
| Setup WNS              |           `-1.5718 ns` |                 `0 ns` | **fixed**                    |
| Setup TNS              |          `-13.5390 ns` |                 `0 ns` | **fixed**                    |
| Setup violations       |                   `47` |                    `0` | **fixed**                    |
| Hold WNS               |                    `0` |                    `0` | clean                        |
| Hold violations        |                    `0` |                    `0` | clean                        |
| Max cap violations     |                    `0` |                    `0` | clean                        |
| Route DRC errors       |                    `0` |                    `0` | clean                        |
| Magic DRC errors       |                    `0` |                    `0` | clean                        |
| Antenna violating nets |                    `0` |                    `0` | clean                        |
| Instance count         |                 `6081` |                 `6078` | basically same               |
| Wirelength             |                `67069` |                `67070` | basically same               |
| Max slew violations    |                    `7` |                   `13` | worse count, but still small |
| Max fanout violations  |                   `20` |                   `20` | unchanged, clock leaves      |
```

Corner Summary:

```
| Corner             | 86 setup vio | 86 setup WS ns | 86 slew | 86 fanout | 86 cap | 87 setup vio | 87 setup WS ns | 87 slew | 87 fanout | 87 cap |
| ------------------ | -----------: | -------------: | ------: | --------: | -----: | -----------: | -------------: | ------: | --------: | -----: |
| `max_ss_125C_3v00` |           25 |         -1.572 |       7 |        20 |      0 |            0 |          9.127 |      13 |        20 |      0 |
| `nom_ss_125C_3v00` |           13 |         -1.256 |       5 |        20 |      0 |            0 |          9.740 |       0 |        20 |      0 |
| `min_ss_125C_3v00` |            9 |         -0.988 |       0 |        20 |      0 |            0 |         10.250 |       0 |        20 |      0 |
| `max_tt_025C_3v30` |            0 |          9.125 |       0 |        20 |      0 |            0 |         24.569 |       0 |        20 |      0 |
| `nom_tt_025C_3v30` |            0 |          9.296 |       0 |        20 |      0 |            0 |         24.893 |       0 |        20 |      0 |
| `min_tt_025C_3v30` |            0 |          9.441 |       0 |        20 |      0 |            0 |         25.163 |       0 |        20 |      0 |
| `max_ff_n40C_3v60` |            0 |         12.684 |       0 |        20 |      0 |            0 |         28.704 |       0 |        20 |      0 |
| `nom_ff_n40C_3v60` |            0 |         12.734 |       0 |        20 |      0 |            0 |         28.750 |       0 |        20 |      0 |
| `min_ff_n40C_3v60` |            0 |         12.775 |       0 |        20 |      0 |            0 |         28.788 |       0 |        20 |      0 |

```

---------

SKY130 JTAG-enabled build: setup/hold clean at 50 MHz, DRC/LVS/antenna/lint clean, with remaining slow-corner max-slew and CTS clock-leaf fanout violations.

SKY130 success: [Increase slew repair to 40 percent #191](https://github.com/gojimmypi/ttsky-UART-FSM-TRNG-Lab/actions/runs/27223286366)

----------

References:

- [LibreLane Option 1 - Macro-First Hardening strategy](https://librelane.readthedocs.io/en/stable/additional_material/caravel/macro_first_hardening/index.html)
- [Module 3: Routing and Physical Optimization](https://silicon-sprint-auc.readthedocs.io/en/latest/MODULE3.html)
- [GlobalFoundries 180nm MCU 7 track standard cells libraries](https://github.com/google/globalfoundries-pdk-libs-gf180mcu_fd_sc_mcu7t5v0/tree/main/liberty)