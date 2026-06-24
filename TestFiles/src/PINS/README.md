# Pin Diagnostic Core

`pin_id_core.v` is an optional Tiny Tapeout logical-pin diagnostic block. It lets a host select one output-capable logical TT pin and drive a simple identification signal on that pin.

The feature is disabled by default and is included only when `PIN_DIAG` is defined.

Note that with all other features turned on, this one pushes the size requirements to 2x2 tiles, even though there's only ~80% utilization on 1x2.

## Enable the feature

In `src/project_config.v`:

```verilog
`define PIN_DIAG
```

Leave the define commented out for normal builds:

```verilog
// `define PIN_DIAG
```

`src/project.v` includes the core only when `PIN_DIAG` is enabled:

```verilog
`ifdef PIN_DIAG
    `include "PINS/pin_id_core.v"
`endif
```

## What the core does

When armed and enabled, the core overrides exactly one selected logical output pin:

- `uo_out[0]` through `uo_out[7]`
- `uio[0]` through `uio[7]`

`ui_in[7:0]` pins are input-only and are not driven by this core.

For a selected `uo_out[n]`, only that `uo_out` bit is overridden. Other `uo_out` bits keep their normal project behavior.

For a selected `uio[n]`, only that `uio` bit is driven. Other `uio` pins are made high-Z while pin diagnostic mode is active. This reduces contention risk on bidirectional pins.

Reset disables the mode because all controlling registers return to their reset values.

## Control interface

No new ASCII commands and no new SPI register addresses are added. Pin diagnostic mode reuses the existing UART/SPI register bank.

Existing UART commands used:

| Command | Existing register | Pin diagnostic use |
| --- | --- | --- |
| `Dxx` | `reg_div` | `reg_div[4:0]` selects the logical pin |
| `Oxx` | `reg_oscen` | must be `A5` to arm pin diagnostic mode |
| `Mxx` | `reg_mode` | bit 7 enables mode; bits 6:5 select the signal |

Existing SPI register addresses used:

| SPI address | Existing register | Pin diagnostic use |
| --- | --- | --- |
| `2` | `reg_div` | selected logical pin |
| `3` | `reg_mode` | enable and mode |
| `4` | `reg_oscen` | magic arm value |

Pin diagnostic mode is active only when both conditions are true:

```verilog
reg_oscen == 8'hA5
reg_mode[7] == 1'b1
```

## Pin select values

Write the select value to `reg_div` with UART command `Dxx` or SPI register address `2`.

| Select value | Logical pin |
| --- | --- |
| `00`..`07` | `uo_out[0]`..`uo_out[7]` |
| `10`..`17` | `uio[0]`..`uio[7]` |

Other select values are invalid. Invalid selections do not drive any pin.

## Signal modes

Write the mode value to `reg_mode` with UART command `Mxx` or SPI register address `3`.

| `reg_mode[6:5]` | Full enable value | Mode |
| --- | --- | --- |
| `00` | `80` | square-wave clock |
| `01` | `A0` | pulse-count ID |
| `10` | `C0` | ASCII UART stream |
| `11` | `E0` | reserved; currently behaves like square-wave clock |

### Square-wave clock mode

The selected pin outputs a slow square wave. The current divider targets approximately 1 kHz from the configured project clock.

Example use: continuity checks, scope probing, logic analyzer pin search.

### Pulse-count ID mode

The selected pin emits a repeated burst of pulses followed by a gap.

Pulse counts:

| Logical pin | Pulse count |
| --- | --- |
| `uo_out[0]`..`uo_out[7]` | 1..8 pulses |
| `uio[0]`..`uio[7]` | 9..16 pulses |

Example: `uio[3]` uses select value `13`, so it emits 12 pulses, pauses, and repeats.

### ASCII UART stream mode

The selected pin repeatedly transmits a compact ASCII ID string at the project UART baud rate:

```text
P=xx\r
```

`xx` is the two-digit hexadecimal pin select value.

Examples:

| Selected pin | Select value | ASCII stream |
| --- | --- | --- |
| `uo_out[4]` | `04` | `P=04\r` |
| `uio[3]` | `13` | `P=13\r` |

This stream is driven on the selected diagnostic pin, not on the normal project UART TX pin unless the selected pin is also the normal UART TX pin.

## UART examples

The UART command parser expects carriage return. Line feed is ignored, so CRLF terminals also work.

Select `uo_out[4]` and output a square-wave clock:

```text
D04\r
OA5\r
M80\r
```

Select `uio[3]` and output pulse-count ID:

```text
D13\r
OA5\r
MA0\r
```

Select `uio[3]` and output ASCII UART stream:

```text
D13\r
OA5\r
MC0\r
```

Disable pin diagnostic mode:

```text
M00\r
```

A project reset also disables the mode.

## SPI examples

SPI uses the existing register-access protocol.

For `uio[3]`, pulse-count mode:

```text
write addr 2 = 0x13   # reg_div: select uio[3]
write addr 4 = 0xA5   # reg_oscen: magic arm value
write addr 3 = 0xA0   # reg_mode: enable + pulse mode
```

For `uo_out[4]`, square-wave clock mode:

```text
write addr 2 = 0x04   # reg_div: select uo_out[4]
write addr 4 = 0xA5   # reg_oscen: magic arm value
write addr 3 = 0x80   # reg_mode: enable + clock mode
```

For `uio[3]`, ASCII UART stream mode:

```text
write addr 2 = 0x13   # reg_div: select uio[3]
write addr 4 = 0xA5   # reg_oscen: magic arm value
write addr 3 = 0xC0   # reg_mode: enable + ASCII stream mode
```

Disable pin diagnostic mode:

```text
write addr 3 = 0x00   # reg_mode: disable
```

## Conflict review

The implementation intentionally avoids adding parser commands or SPI addresses.

- UART command letters are unchanged: `E`, `S`, `D`, `V`, `W`, `M`, `O`, and `R` keep their existing parser behavior.
- SPI register addresses are unchanged: addresses `0` through `7` still map to the existing register bank.
- Pin diagnostic mode uses only previously existing writable registers: `reg_div`, `reg_mode`, and `reg_oscen`.
- `reg_mode[7:5]` were not used by the TRNG core; the TRNG core only reports or uses the low mode bits.
- The mode requires the explicit arm value `reg_oscen == 8'hA5` and enable bit `reg_mode[7] == 1'b1`, which prevents normal reset/default behavior from entering pin diagnostic mode.

Operational caveats:

- While pin diagnostic mode is active, `reg_div`, `reg_mode`, and `reg_oscen` are being used for diagnostics, so normal TRNG sampling/divider/oscillator behavior should be considered overridden for bring-up purposes.
- Selecting shared `uio` pins can temporarily override SPI/JTAG pins. This is expected for a pin-identification mode, but only enable it when external contention is controlled.
- Selecting the normal UART TX pin, currently `uo_out[4]`, overrides the normal UART TX output with the diagnostic signal until the mode is disabled.

## Board portability

The core identifies Tiny Tapeout logical pins, not physical board header locations. Board wrappers and board-map documentation should translate logical pins such as `uio[3]` or `uo_out[4]` to ULX3S J1/J2 pins, Tiny Tapeout demoboard PMOD pins, or other board-specific headers.
