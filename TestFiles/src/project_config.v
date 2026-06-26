/*
 * Copyright (c) 2026 gojimmypi
 * SPDX-License-Identifier: Apache-2.0
 *
 * See ATTRIBUTION.md for third-party sources and credits.
 *
 * file: project_config.v
 *
 * Project-wide configuration settings for the Tiny Tapeout project
 *
 * See the [project]/scripts/show_effective_defines.sh to view summary of
 * active project macros and optionally generate an equivalent C header.
 *
 */
`default_nettype none

/* We only want to include this file once, but it may be referenced BOTH by:
 *   - project.v
 *   - top_ulx3s.v
 *   - other wrappers
 */
`ifndef PROJECT_CONFIG_V
    `define PROJECT_CONFIG_V
    /* There's <1% (~ 21 cells) increase in the number of cells when using long strings.
     * Currently only the version string is implemented. */
    `define USE_LONG_STRINGS

    `ifdef USE_LONG_STRINGS
        `define VERSION_STRING_LEN 23 /* 123456789012345678901234 */   
        `define VERSION_STRING          "Version 1.0.5 6/21/2026"
        /* GF26a deadline: June 22, 1:00PM PDT */
    `else
        /* no long strings */
    `endif

    /* For TT ASIC, command are only upper case, with both of the following
     * case insensitive options NOT ENABLED due to decreases slew and setup. */

    /* Add some logic to make UART interactive commands case insensitive */
    // `define CASE_INSENSITIVE

    /* Add additional alternative compares for case insensitive command chars */
    // `define CASE_INSENSITIVE_ALT


    /* Optionally Perform a blinky test on led[0] to confirm we have a working clock */
    // `define ULX3S_CLOCK_TEST

    /* The 50 MHz clock on gn12 is only available when using HDMI on the ULX3S */
    // `define ULX3S_USE_GN12_50MHZ

    `ifdef ULX3S_USE_GN12_50MHZ
        `define PROJECT_CLOCK_HZ 32'd50_000_000
    `endif

    `ifndef PROJECT_CLOCK_HZ
       `define PROJECT_CLOCK_HZ 32'd25_000_000
    `endif

    `ifndef PROJECT_UART_BAUD
        `define PROJECT_UART_BAUD 32'd115_200
    `endif

    `define ADJUSTABLE_BAUD_ENABLED

    /* Some project features, typically only changed during development and debugging: */

    // `define ANALOG_ENABLED
    `define UART_ENABLED

    /*
     * --------------------------------------------------------------------------------------------
     * SPI Config. See SPI/spi_slave.v
     * --------------------------------------------------------------------------------------------
     */
    `define SPI_ENABLED
    `define SPI_REG_ACCESS

    /* Default SPI register size is 3 bits. (0..7) 
     * Optionally expand to:
     *   4 bits: 0..15 with BIG16_SPI_REG 
     *   7 bits: 0..127 with MAX_SPI_REG  */
    // `define MAX_SPI_REG

    /* See tt_uart_test.py for manual edit: BIG16_SPI_REG=True */
    `define BIG16_SPI_REG

    `define TRNG_ENABLED
    `define TRNG_BINARY_STREAM

    /* Lightweight TRNG health status. Reuses R5 status bits rather than adding
     * new UART/SPI registers. Disable only if final area/timing needs the space. */
    `define TRNG_HEALTH_STATUS

    /* Use ui_in[0] to select alternate health/debug signals on uo_out[7:5].
     *
     * Manually set TRNG_HEALTH_STATUS_DEBUG_PAGE_SELECT in /test/test.py as needed. */
    // `define DEBUG_PAGE_SELECT
    
    /* 
     * --------------------------------------------------------------------------------------------
     * See trng_lab_core.v for various conditioning options
     *
     * With unlimited cell space (or an FPGA!) one could test each of the conditioning options
     * with a runtime selection. This is not implemented at this time here.
     *

     * --------------------------------------------------------------------------------------------
     */
    `define TRNG_CONDITIONED_STREAM

    /* 
     * --------------------------------------------------------------------------------------------
     * Optional 64 bit XOR stream whitening conditioner: TRNG_CONDITIONED_STREAM_64_XOR
     * --------------------------------------------------------------------------------------------
     * Enabling TRNG_CONDITIONED_STREAM_64_XOR on sky130, repair 20/20
     * increases 1x2 cell utilization from 71% to 88% 
     * See #206 https://github.com/gojimmypi/ttsky-UART-FSM-TRNG-Lab/actions/runs/27421859298
     *  vs
     * #205: https://github.com/gojimmypi/ttsky-UART-FSM-TRNG-Lab/actions/runs/27393236624
     * --------------------------------------------------------------------------------------------
     */
    // `define TRNG_CONDITIONED_STREAM_64_XOR

    /* 
     * --------------------------------------------------------------------------------------------
     * Optional 16 bit CRC whitening conditioner: TRNG_CONDITIONED_STREAM_CRC
     * --------------------------------------------------------------------------------------------
     * See #206 https://github.com/gojimmypi/ttsky-UART-FSM-TRNG-Lab/actions/runs/27421859298 reference 71.200%
     *    vs
     * Update: #207 https://github.com/gojimmypi/ttsky-UART-FSM-TRNG-Lab/actions/runs/27429304557 (in place, not enabled: 71.200%)
     *    vs
     * Enable: #208 https://github.com/gojimmypi/ttsky-UART-FSM-TRNG-Lab/actions/runs/27431458941 (smaller!, 66.358%, but fails NIST Rank)
     * --------------------------------------------------------------------------------------------
     */
    // `define TRNG_CONDITIONED_STREAM_CRC

    /* 
     * --------------------------------------------------------------------------------------------
     * Optional 32 bit Galois whitening conditioner: TRNG_CONDITIONED_STREAM_GALOIS
     * --------------------------------------------------------------------------------------------
     * Baseline default: 71.4% in GDS #212: https://github.com/gojimmypi/ttsky-UART-FSM-TRNG-Lab/actions/runs/27436129291
     *    vs
     * Enable: 73.4% in GDS #213 https://github.com/gojimmypi/ttsky-UART-FSM-TRNG-Lab/actions/runs/27437193873
     *    vs
     * Galois V2: 75.708% in GDS #216 https://github.com/gojimmypi/ttsky-UART-FSM-TRNG-Lab/actions/runs/27443295824 
     *    vs
     * Galois V3: 84.278% in GDS #217 https://github.com/gojimmypi/ttsky-UART-FSM-TRNG-Lab/actions/runs/27444743537
     * --------------------------------------------------------------------------------------------
     * config.json fails:
     *   "DESIGN_REPAIR_MAX_SLEW_PCT": 40,
     *   "GRT_DESIGN_REPAIR_MAX_SLEW_PCT": 40,
     * 
     * Current (default): 20/20
     * last run #220: 72.978% https://github.com/gojimmypi/ttsky-UART-FSM-TRNG-Lab/actions/runs/27447050050
     */
     `define TRNG_CONDITIONED_STREAM_GALOIS

    /* 
     * --------------------------------------------------------------------------------------------
     * Optional selected_bit clean or not: TRNG_RAW_CLEAN_MIX
     * --------------------------------------------------------------------------------------------
     * When enabled:     selected_bit = rox_sample_sync;
     * when not enabled: selected_bit = rox_sample_sync ^ lfsr[0] ^ lfsr[5] ^ sample_shift[3];
     */
     `define TRNG_RAW_CLEAN_MIX

    /*
     * --------------------------------------------------------------------------------------------
     * Optional 64 bit Galois whitening conditioner: TRNG_CONDITIONED_STREAM_GALOIS_64
     * --------------------------------------------------------------------------------------------
     * Too large for 1x2. See #218 https://github.com/gojimmypi/ttsky-UART-FSM-TRNG-Lab/actions/runs/27445643164
     */
    // `define TRNG_CONDITIONED_STREAM_GALOIS_64

    /* TODO: */
    // `define TRNG_CONDITIONED_STREAM_VON_NEUMANN

    /* 
     * --------------------------------------------------------------------------------------------
     *  With all the above features enabled, there's not enough room on 1x2 SKY130 to enable JTAG 
     * --------------------------------------------------------------------------------------------
     */
    `ifdef PDK_TARGET_SKY130
        /* no JTAG at this time */
    `else
        `define JTAG_ENABLED
    `endif

    /* FPGA-only: ignore reg_oscen and expose raw deterministic LFSR taps.
     * Normally leave disabled so the FPGA surrogate respects oscillator enables. */
    // `define FPGA_BASIC_LFSR_RO_TAPS

    /* Note that with all UART_ENABLED, SPI_ENABLED, SPI_REG_ACCESS, TRNG_ENABLED, JTAG_ENABLED
     * also enabling PIN_DIAG pushes design over 80% of 1x2 tiles. GDS aborted after 90 minute run. */
    `ifdef ULX3S
        // `define PIN_DIAG

        /* FPGA only: A practical lightweight candidate is a xoshiro-style 128-bit PRNG. 
         * It is not cryptographic, but it is much more likely to pass STS than the current 16-bit LFSR tap source */
        //`define FPGA_NIST_PRNG_SOURCE

        `define ULX3S_SPI_ENABLED

    `elsif IS_MY_IVERILOG_SIMULATION 
        /* This is used by the [project]/test/my_test.sh simulation test script */
        // `define PIN_DIAG

    `else
        /* Typically the TT Demoboard iCE40 */

        /* The PIN diag not implemented in 1x2 tile setting for TT at this time. */

        /* The NIST PRNG is not implemented */
    `endif

    /* SPI_TEST_BYTE is only used when SPI_TEST_FIXED is enabled. */
    // `define SPI_TEST_BYTE 8'hD2

    /* Pick zero or one of these SPI tests. Leave both disabled for register access. */
    // `define SPI_TEST_FIXED
    // `define SPI_TEST_ECHO

    /* Final combinatorial macros */
    `ifdef TRNG_HEALTH_STATUS
        `ifdef DEBUG_PAGE_SELECT
            /* TRNG_HEALTH_STATUS && DEBUG_PAGE_SELECT */
            `define TRNG_HEALTH_STATUS_DEBUG_PAGE_SELECT
        `endif
    `endif

    `ifdef SPI_REG_ACCESS
        `ifdef MAX_SPI_REG
            `define SPI_ADDR_MSB 6
            `define MAX
        `elsif BIG16_SPI_REG
            `define SPI_ADDR_MSB 3
        `else
            `define SPI_ADDR_MSB 2
        `endif
        `define SPI_ADDR_WIDTH  (`SPI_ADDR_MSB + 1)
    `endif

    `ifdef JTAG_ENABLED
        `define JTAG_ADDR_MSB `SPI_ADDR_MSB
        `define JTAG_ADDR_WIDTH (`JTAG_ADDR_MSB + 1)
    `endif

    /* Some final config sanity checks */
    `ifdef TRNG_CONDITIONED_STREAM
        `ifndef TRNG_BINARY_STREAM
            PROJECT_TRNG_CONDITIONED_STREAM_REQUIRES_BINARY_STREAM u_stop (); /* TRNG_CONDITIONED_STREAM requires TRNG_BINARY_STREAM */
        `endif
    `endif

    `ifdef TRNG_CONDITIONED_STREAM_64_XOR
        `ifndef TRNG_CONDITIONED_STREAM
            PROJECT_TRNG_CONDITIONED_STREAM_64_XOR_REQUIRES_CONDITIONED_STREAM u_stop (); /* TRNG_CONDITIONED_STREAM_64_XOR requires TRNG_CONDITIONED_STREAM */
        `endif
    `endif

    `ifdef TRNG_CONDITIONED_STREAM_CRC
        `ifndef TRNG_CONDITIONED_STREAM
            PROJECT_TRNG_CONDITIONED_STREAM_CRC_REQUIRES_CONDITIONED_STREAM u_stop (); /* TRNG_CONDITIONED_STREAM_CRC requires TRNG_CONDITIONED_STREAM */
        `endif
    `endif

    `ifdef TRNG_CONDITIONED_STREAM_GALOIS
        `ifndef TRNG_CONDITIONED_STREAM
            PROJECT_TRNG_CONDITIONED_STREAM_GALOIS_REQUIRES_CONDITIONED_STREAM u_stop (); /* TRNG_CONDITIONED_STREAM_GALOIS requires TRNG_CONDITIONED_STREAM */
        `endif
    `endif

    `ifdef TRNG_CONDITIONED_STREAM_64_XOR
        `ifdef TRNG_CONDITIONED_STREAM_CRC
            PROJECT_TRNG_CONDITIONED_STREAM_64_XOR_AND_TRNG_CONDITIONED_STREAM_CRC u_stop ();  /* both TRNG_CONDITIONED_STREAM_64_XOR and TRNG_CONDITIONED_STREAM_CRC enabled. pick one. */
        `endif
    `endif

    `ifdef TRNG_CONDITIONED_STREAM_64_XOR
        `ifdef TRNG_CONDITIONED_STREAM_GALOIS
            PROJECT_TRNG_CONDITIONED_STREAM_64_XOR_AND_TRNG_CONDITIONED_STREAM_GALOIS u_stop ();  /* both TRNG_CONDITIONED_STREAM_64_XOR and TRNG_CONDITIONED_STREAM_GALOIS enabled. pick one. */
        `endif
    `endif

    `ifdef TRNG_CONDITIONED_STREAM_CRC
        `ifdef TRNG_CONDITIONED_STREAM_GALOIS
            PROJECT_TRNG_CONDITIONED_STREAM_CRC_AND_TRNG_CONDITIONED_STREAM_GALOIS u_stop ();  /* both TRNG_CONDITIONED_STREAM_CRC and TRNG_CONDITIONED_STREAM_GALOIS enabled. pick one. */
        `endif
    `endif

    `ifdef CASE_INSENSITIVE
        `ifdef CASE_INSENSITIVE_ALT
            PROJECT_MUST_PICK_ZERO_OR_ONE_CASE_INSENSITIVE_ALT u_stop (); /* Cannot use both CASE_INSENSITIVE and CASE_INSENSITIVE_ALT */
        `endif
    `endif

    `ifdef CASE_INSENSITIVE_ALT
        `ifdef CASE_INSENSITIVE
            PROJECT_MUST_PICK_ZERO_OR_ONE_CASE_INSENSITIVE u_stop ();  /* Cannot use both CASE_INSENSITIVE and CASE_INSENSITIVE_ALT */
        `endif
    `endif

    `ifdef FPGA_BASIC_LSFR_RO_TAPS
         PROJECT_LSFR_NOT_A_VALID_OPTION u_stop ();  /* It is LFSR not LSFR */
    `endif

    `ifdef SPI_REG_ACCESS
        `ifndef SPI_ENABLED
            PROJECT_SPI_REG_ACCESS_REQUIRES_SPI_ENABLED u_stop ();
        `endif
    `endif

    `ifdef BIG16_SPI_REG
        `ifdef MAX_SPI_REG
            PROJECT_SPI_BIG16_AND_MAX_PICK_ONE u_stop (); /* Define none or only one SPI reg size */
        `endif
    `endif

    `ifdef MAX_SPI_REG
        `ifdef BIG16_SPI_REG
            PROJECT_SPI_MAX_AND_BIG16_PICK_ONE u_stop (); /* Define none or only one SPI reg size */
        `endif
    `endif

`endif /* PROJECT_CONFIG_V */

`default_nettype wire
