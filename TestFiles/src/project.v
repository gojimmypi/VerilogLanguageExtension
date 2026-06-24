/*
 * Copyright (c) 2026 gojimmypi
 * SPDX-License-Identifier: Apache-2.0
 *
 * See ATTRIBUTION.md for third-party sources and credits.
 *
 * file: project.v
 *
 * Top-level wrapper for the Tiny Tapeout project.
 *
 * If the ULX3S FPGA is used, see the /ulx3s/top_ulx3s.v wrapper file and define ULX3S when building. 
 *
 *
 *                              ***** NO USER DEFINES IN THIS FILE *****
 *
 *
 * Any defines here are programmatic based on settings. See project_settings.v for all build configurations.
 *
 */
`default_nettype none

`include "target_pdk.v"

/* Project configuration options
 *
 * Higher level wrappers such as ULX3S FPGA test also need to also have project.v included.
 * Otherwise, only needed here for TT project: */
`include "project_config.v"

`ifdef ULX3S
    `timescale 1ns/1ps
`else
    /* Tiny Tapeout doesn't support timescale directives, so we can ignore it. */
`endif /* ULX3S */

/* Conditional TRNG settings */
`ifdef ULX3S
    /* Do not define TRNG_USE_RO when building for ULX3S since
     * the real RO-based TRNG is only available in the Tiny Tapeout environment. */
    `ifdef TRNG_USE_RO
        PROJECT_ULX3S_MUST_NOT_USE_TRNG_USE_RO u_stop ();
    `endif
    `ifdef TRNG_ALLOW_REAL_RO
        PROJECT_ULX3S_MUST_NOT_USE_TRNG_ALLOW_REAL_RO u_stop ();
    `endif
`else
    /* Not ULXS3. Is this a TT PDK? */

    /* TODO: detect test modes? add "is submission" macro? */

    /* HACK ALERT: checking __pnr__,  SCL_gf180mcu_fd_sc_mcu7t5v0, SCL_sky130_fd_sc_hd may not be 100% reliable!! */
    `ifdef SCL_sky130_fd_sc_hd
        /* Less hacky is to detect the presence of a cell that is only available in the real RO-based TRNG for SKY130, 
         * but this also isn't perfect since it could be used in a non-TT context. */
        `define TRNG_USE_RO
        `define TRNG_ALLOW_REAL_RO
        `define FOUND_TT_PDK
        `define FOUND_TT_PDK_SKY130

        /* To confirm only this path is taken, enable the next line. Only GSD GH Action should fail: */
        /* PROJECT_FOUND_TT_PDK_SKY130 u_stop (); */

    `elsif SCL_gf180mcu_fd_sc_mcu7t5v0
        /* Less hacky is to detect the presence of a cell that is only available in the real RO-based TRNG for GF180, 
         * but this also isn't perfect since it could be used in a non-TT context. */
        `define TRNG_USE_RO
        `define TRNG_ALLOW_REAL_RO
        `define FOUND_TT_PDK
        `define FOUND_TT_PDK_GF180

        /* To confirm only this path is taken, enable the next line. Only GSD GH Action should fail: */
        /* PROJECT_FOUND_TT_PDK_GF180 u_stop (); */
        /* See example: https://github.com/gojimmypi/ttgf-UART-FSM-TRNG-Lab/actions/runs/26890757755 */

    `elsif __pnr__
        /* More hacky is __pnr__ and still does not conclusively prove that we are building for Tiny Tapeout, 
         * but it is a strong indicator that we are in an environment where the real RO-based TRNG can be used. */
        `define TRNG_USE_RO
        `define TRNG_ALLOW_REAL_RO
        `define FOUND_TT_PDK

        /* To confirm only this path is taken, enable the next line. Only GSD GH Action should fail: */
        /* PROJECT_FOUND_TT_PDK_UNKNOWN u_stop (); */

        /* End of possible TT ASIC PDK detection.  */
    `else
        /* Assume we are in some other non-ULX3S, non-ASIC environment, probably the TT FPGA Demoboard? */
        `define TT_NON_ASIC_BUILD

        /* some other non ULX3S, non ASIC path. Detect if REAL RO defined externally and abort */
        `ifdef TRNG_USE_RO
            PROJECT_NON_ASIC_MUST_NOT_USE_TRNG_USE_RO u_stop ();
        `endif

        `ifdef TRNG_ALLOW_REAL_RO
            PROJECT_NON_ASIC_MUST_NOT_USE_TRNG_ALLOW_REAL_RO u_stop ();
        `endif

        `ifdef FOUND_TT_PDK
            PROJECT_FOUND_TT_PDK_BUT_NOT_REAL_RO u_stop (); /* We can only "find" the PDF here! */
        `endif
    `endif
`endif

/*
 * Build Environment Configuration
 *
 * The codebase is designed to be portable across different FPGA platforms and simulation environments.
 * Conditional compilation directives are used to include or exclude code based on the target environment.
 * This allows for a single codebase that can be built for both the ULX3S FPGA and the Tiny Tapeout platform,
 * while still supporting environment-specific features and optimizations.
 *
 * Key points:
 * - The `ULX3S` macro is defined when building for the ULX3S FPGA, enabling ULX3S-specific code paths.
 * - When `ULX3S` is not defined, it is assumed that the build target is Tiny Tapeout, and Tiny Tapeout-specific code paths are enabled.
 * - This structure allows for clean separation of environment-specific code while maintaining a shared core logic.
 */

`ifndef PROJECT_CONFIG_V
    MODULE_MISSING_PROJECT_CONFIG u_stop (); /* Error if the project_config.v was not included */
`endif

`ifdef ULX3S
    /* The ./ulx3s/Makefile includes references to needed files */

    /* Beware of a potentially generated file: "[project]/src/_tt_fpga_top.v" */

`elsif IS_MY_IVERILOG_SIMULATION 
    /* We want iverilog to be more like the TT build without the target_pdk.v included in wildcard */

    /* Note the module is named tt_um_example for this path */

`else
    /* Tiny Tapeout needs to include all the files explicitly since `tt_fpga.py harden` doesn't use Makefiles.
     *  (see https://github.com/TinyTapeout/tt-support-tools.git )
     *    ** OR **
     *  list them in /info.yaml file (pick one, don't mix) 
     * 
     * Note the ice40 TT Demoboard build uses this path with `tt_fpga.py harden` */
    `include "tt_um_main.v"
    `include "JTAG/jtag_core.v"
    `include "SPI/spi_slave.v"
    `include "UART/uart_rx_min.v"
    `include "UART/uart_tx_min.v"
    `include "UART/uart_trng_ascii_core.v"
    `include "TRNG/trng_cfg_ascii_core.v"
    `ifdef PIN_DIAG
        `include "PINS/pin_id_core.v"
    `endif
    `ifdef TRNG_ENABLED
        `include "TRNG/trng_lab_core.v"
    `else
        `include "TRNG/trng_stub.v"
    `endif /* TRNG_ENABLED */
`endif /* ULX3S */

/* Some analog sanity checks */
`ifdef ANALOG_ENABLED
    `ifdef PDK_TARGET_GF180
        MODULE_ANALOG_NOT_SUPPORTED_IN_GF180 u_stop (); /* Error as there's no analog features here. See SKY130 */
    `endif
`endif

/* See companion project: SKY130 (ChipFoundry) tt_um_gojimmypi_ttsky_UART_FSM_TRNG_Lab */

/* Assume TT needs this file to be called project.v 
 * but the module is called tt_um_gojimmypi_ttgf_UART_FSM_TRNG_Lab - so disable warning: */

 /* Define a unique name for the module based on the target PDK. 
  * This allows the same project.v file to be used across different PDK targets without modification, 
  * while still adhering to any naming requirements imposed by the Tiny Tapeout platform. 
  * 
  * There's no Makefile to extract name from info.yaml, so the module name is hardcoded here: */
`ifdef PDK_TARGET_SKY130
/* verilator lint_off DECLFILENAME */
module tt_um_gojimmypi_ttsky_UART_FSM_TRNG_Lab
/* verilator lint_on DECLFILENAME */

`elsif PDK_TARGET_GF180
/* verilator lint_off DECLFILENAME */
module tt_um_gojimmypi_ttgf_UART_FSM_TRNG_Lab
/* verilator lint_on DECLFILENAME */

`elsif IS_MY_IVERILOG_SIMULATION
module tt_um_example

`else
/* Only SKY130 and GF180 supported at this time. See target_pdk.v 
 * There will likely be an error later with this name and the need for real RO */
module UART_FSM_TRNG_Lab
`endif

#(
    /* Get project-wide params from project_config.v */
    parameter [31:0] CLOCK_HZ  = `PROJECT_CLOCK_HZ,    /* default clock is 25 MHz */
    parameter [31:0] UART_BAUD = `PROJECT_UART_BAUD    /* default UART is 115200 baud */
)
(
`ifdef ANALOG_ENABLED
    // Optional Analog
    //    input  wire       VGND,
    //    input  wire       VDPWR,    // 1.8v power supply
    //    input  wire       VAPWR,    // 3.3v power supply
`endif

    input  wire [7:0] ui_in,    // Dedicated inputs
    output wire [7:0] uo_out,   // Dedicated outputs
    input  wire [7:0] uio_in,   // IOs: Input path
    output wire [7:0] uio_out,  // IOs: Output path
    output wire [7:0] uio_oe,   // IOs: Enable path (active high: 0=input, 1=output)

    //    inout  wire [7:0] ua,       // Analog pins, only ua[5:0] can be used

    input  wire       ena,      // always 1 when the design is powered, so you can ignore it
    input  wire       clk,      // clock
    input  wire       rst_n     // reset_n - low to reset
);
     /* Boilerplate parameter checking */
    generate
        if (CLOCK_HZ == 32'd0) begin : gen_bad_clock_hz
            PROJECT_MUST_NOT_USE_ZERO_CLOCK u_stop ();
        end

        if (UART_BAUD == 32'd0) begin : gen_bad_uart_baud
            PROJECT_MUST_NOT_USE_ZERO_UART_BAUD u_stop ();
        end

        if ((CLOCK_HZ / UART_BAUD) == 32'd0) begin : gen_bad_uart_divider
            PROJECT_UART_DIVIDER_MUST_NOT_BE_ZERO u_stop ();
        end

`ifdef SPI_REG_ACCESS
    `ifdef JTAG_ENABLED
        /* Only equal-length JTAG and SPI registers supported at this time, check MSB: */
        if (`SPI_ADDR_MSB != `JTAG_ADDR_MSB) begin : gen_spi_jtag_msb_mismatch
            PROJECT_SPI_ADDR_MSB_VS_JTAG_ADDR_MSB_LENGTH_MISMATCH  u_stop ();
        end

        /* Only equal-length JTAG and SPI registers supported at this time, check lengvth: */
        if (`SPI_ADDR_WIDTH != `JTAG_ADDR_WIDTH) begin : gen_spi_jtag_width_mismatch
            PROJECT_SPI_ADDR_MSB_VS_JTAG_ADDR_MSB_LENGTH_MISMATCH  u_stop ();
        end
    `endif /* JTAG_ENABLED */
`endif /* SPI_REG_ACCESS */
    endgenerate

    wire unused_ok;

    tt_um_main
    #(
        .CLOCK_HZ(CLOCK_HZ),
        .UART_BAUD(UART_BAUD)
    )
    u_core
    (
        .ui_in(ui_in),
        .uo_out(uo_out),
        .uio_in(uio_in),
        .uio_out(uio_out),
        .uio_oe(uio_oe),
        .ena(ena),
        .clk(clk),
        .rst_n(rst_n)
    );

`ifdef ANALOG_ENABLED
    // Optional Analog
    // assign unused_ok = &{VGND, VDPWR, ena, clk, rst_n, uio_in, ua};
`endif

    assign unused_ok = &{ena, clk, rst_n, uio_in};

    `ifdef ULX3S
        always @(posedge clk) begin
            if (rst_n) begin
                $display("t=%0t ui_in=%h uio_in=%h uo_out=%h",
                         $time, ui_in, uio_in, uo_out);
            end
        end
    `else
        /* FORCE_LOOPBACK not supported outside of ULX3S since it relies on specific pin mappings 
         *  and test features that may not be present in other environments. */
        `ifdef FORCE_LOOPBACK
            MODULE_FORCE_LOOPBACK_MUST_NOT_BE_ENABLED u_stop ();
        `endif
    `endif /* ULX3S */

endmodule /* Conditional module name based on PDK target. See above. */


`default_nettype wire
