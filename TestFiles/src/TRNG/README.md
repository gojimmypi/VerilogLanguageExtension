# TRNG

This is the core True Random Number Generator (TRNG) code.

Implements a [Ring Oscillator](https://en.wikipedia.org/wiki/Ring_oscillator) using foundry-specific inverters.

## FPGA

&#x26A0;  The `TRNG_LAB_USE_REAL_RO` macro is *not* automatically defined for FPGA builds (internal use only), ans should *not* be manually defined by any external build process.

The "RO" bits are deterministic LFSR-derived signals, not physical entropy sources. This path is for functional testing only.

## GF180 PDF

The `TRNG_LAB_USE_REAL_RO` macro *is* automatically defined (internal use only). Both `TRNG_USE_RO` and `TRNG_ALLOW_REAL_RO` should be defined in the `project_config.v`.

There are several possible inverters to use in GF180. See

   https://github.com/google/globalfoundries-pdk-libs-gf180mcu_fd_sc_mcu7t5v0/tree/main/cells/inv

| Cell    | Drive |        Area | Input cap | Min-load delay, falling output | Min-load delay, rising output |
| ------- | ----: | ----------: | --------: | -----------------------------: | ----------------------------: |
| `inv_1` |    1X |  8.7808 um2 | 0.0047 pF |                      0.0377 ns |                     0.0499 ns |
| `inv_2` |    2X | 13.1712 um2 | 0.0093 pF |                      0.0308 ns |                     0.0387 ns |
| `inv_3` |    3X | 17.5616 um2 | 0.0140 pF |                      0.0309 ns |                     0.0388 ns |
| `inv_4` |    4X | 21.9520 um2 | 0.0185 pF |                      0.0282 ns |                     0.0345 ns |
| `inv_8` |    8X | 39.5136 um2 | 0.0373 pF |                      0.0282 ns |                     0.0344 ns |

>  "The key observation: _inv_2 gives a meaningful speed/drive improvement over _inv_1, but _inv_3 gives almost no 
   further min-load delay improvement over _inv_2. _inv_4 and _inv_8 are only modestly faster in the min-load table, 
    while costing much more input capacitance and area."

For reference:

Option 1: `gf180mcu_fd_sc_mcu7t5v0__inv_1`

   - https://github.com/google/globalfoundries-pdk-libs-gf180mcu_fd_sc_mcu7t5v0/blob/main/cells/inv/gf180mcu_fd_sc_mcu7t5v0__inv_1.functional.v
   - https://github.com/google/globalfoundries-pdk-libs-gf180mcu_fd_sc_mcu7t5v0/blob/main/cells/inv/gf180mcu_fd_sc_mcu7t5v0__inv_1.rst

Option 2: `gf180mcu_fd_sc_mcu7t5v0__inv_2`

   - https://github.com/google/globalfoundries-pdk-libs-gf180mcu_fd_sc_mcu7t5v0/blob/main/cells/inv/gf180mcu_fd_sc_mcu7t5v0__inv_2.functional.v
   - https://github.com/google/globalfoundries-pdk-libs-gf180mcu_fd_sc_mcu7t5v0/blob/main/cells/inv/gf180mcu_fd_sc_mcu7t5v0__inv_2.rst
