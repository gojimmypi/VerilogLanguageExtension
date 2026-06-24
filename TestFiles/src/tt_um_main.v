/*
 * Copyright (c) 2026 gojimmypi
 * SPDX-License-Identifier: Apache-2.0
 *
 * See ATTRIBUTION.md for third-party sources and credits.
 *
 * file: tt_um_main.v
 *
 * Tiny Tapeout wrapper for the TRNG ASCII core.
 * Included by project.v and requires project_config.v
 *
 * Purpose:
 * - Exposes the project through the standard Tiny Tapeout pin interface.
 * - Adapts one TT input pin to UART RX and one TT output pin to UART TX.
 * - Surfaces a few internal status bits on GPIOs for simple board-level debug.
 *
 * Pin usage in this wrapper:
 * - ui_in[7:5]   : reserved for future use, currently ignored
 * - ui_in[4]     : SPI/JTAG select, 0 = SPI, 1 = JTAG (when JTAG_ENABLED defined)
 * - ui_in[3]     : UART RX input to the core
 * - ui_in[2:1]   : reserved for future use, currently ignored
 * - ui_in[0]     : Optional DEBUG_PAGE_SELECT
 *
 * - uo_out[7:5]  : selected low raw-data bits or health summary when DEBUG_PAGE_SELECT and ui_in[0]=1
 * - uo_out[4]    : UART TX output from the core
 * - uo_out[3:1]  : selected status bits
 * - uo_out[0]    : trng_bit
 *
 * - uio[0]       : SPI CS_N / JTAG TMS when serial debug is enabled
 * - uio[1]       : SPI MOSI / JTAG TDI when serial debug is enabled
 * - uio[2]       : SPI MISO / JTAG TDO when serial debug is enabled
 * - uio[3]       : SPI SCK  / JTAG TCK when serial debug is enabled
 * - uio_out[7:4] : reg_rawhi[7:4] when serial debug is enabled
 * - uio_out[7:0] : reg_rawhi byte when serial debug is disabled
 *
 * - uio_oe[7:0]  : UIO direction control
 *
 * This module contains almost no behavior of its own. It is mostly a pin-map
 * and visibility wrapper around uart_trng_ascii_core.
 */
`default_nettype none

`ifdef SIM_JTAG_CORE_TB
    `timescale 1ns / 1ps
`endif

`include "project_config.v"

module tt_um_main 
#(
    parameter [31:0] CLOCK_HZ  = `PROJECT_CLOCK_HZ,
    parameter [31:0] UART_BAUD = `PROJECT_UART_BAUD
)
(
    /* For Tiny Tapeout, these are the only ports you can use. 
     * See:    https://tinytapeout.com/specs/pinouts/         */
    input  wire [7:0] ui_in,
    output wire [7:0] uo_out,
    input  wire [7:0] uio_in,
    output wire [7:0] uio_out,
    output wire [7:0] uio_oe,
    input  wire       ena,
    input  wire       clk,
    input  wire       rst_n
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
    endgenerate


    /* Internal debug/configuration buses exported by the core. */
    wire [7:0] reg_ctrl;
    wire [7:0] reg_src;
    wire [7:0] reg_div;
    wire [7:0] reg_mode;
    wire [7:0] reg_oscen;
    wire [7:0] reg_status;
    wire [7:0] reg_rawlo;
    wire [7:0] reg_rawhi;
    wire       trng_bit;
    wire       uart_tx;

    wire [7:0] uo_out_normal;
    wire [7:0] uio_out_normal;
    wire [7:0] uio_oe_normal;

`ifdef PIN_DIAG
    wire       pin_id_enable;
    wire [1:0] pin_id_mode;
    wire [4:0] pin_id_sel;
    wire [7:0] pin_id_uo_override;
    wire [7:0] pin_id_uo_override_en;
    wire [7:0] pin_id_uio_override;
    wire [7:0] pin_id_uio_override_oe;
    wire       pin_id_active;
    wire       pin_id_invalid_sel;
`endif /* PIN_DIAG */

    /* don't use raw reset rst_n */
    reg        rst_meta_n;
    reg        rst_sync_n;

    reg        uart_rx_meta;
    reg        uart_rx_sync;

`ifdef JTAG_ENABLED
    reg        debug_sel_meta;
    reg        debug_sel_sync;
`endif

`ifdef SPI_REG_ACCESS
    wire       spi_reg_wr_en;
    wire [`SPI_ADDR_MSB:0] spi_reg_addr;
    wire [7:0] spi_reg_wdata;
    wire [7:0] spi_reg_rdata;

    `ifdef SPI_ENABLED
        wire       spi_slave_reg_wr_en;
        wire [`SPI_ADDR_MSB:0] spi_slave_reg_addr;
        wire [7:0] spi_slave_reg_wdata;
    `endif /* SPI_ENABLED */

    `ifdef JTAG_ENABLED
        wire       jtag_reg_wr_en;
        wire [`JTAG_ADDR_MSB:0] jtag_reg_addr;
        wire [7:0] jtag_reg_wdata;
        wire       spi_reg_wr_en_mux;
        wire [`SPI_ADDR_MSB:0] spi_reg_addr_mux;
        wire [7:0] spi_reg_wdata_mux;
        reg        spi_reg_wr_en_r;
        reg  [`SPI_ADDR_MSB:0] spi_reg_addr_r;
        reg  [7:0] spi_reg_wdata_r;
        // wire _unused_jtag_reg_addr = &{1'b0, jtag_reg_addr[7:(`SPI_ADDR_MSB + 1)]};
    `endif /* JTAG_ENABLED */
`endif /* SPI_REG_ACCESS */

`ifdef SPI_ENABLED
    wire spi_sck;
    wire spi_mosi;
    wire spi_cs_n;
    wire spi_miso;
    `ifndef SPI_REG_ACCESS
        wire       spi_unused_reg_wr_en;
        wire [7:0] spi_unused_reg_wdata;
        wire [`SPI_ADDR_MSB:0] spi_unused_reg_addr;
    `endif /* SPI_REG_ACCESS */
`endif /* SPI_ENABLED */

`ifdef JTAG_ENABLED
    wire jtag_tck;
    wire jtag_tms;
    wire jtag_tdi;
    wire jtag_tdo;
    wire debug_is_jtag; /* note special cases of ULX3S vs TT Demoboard */
`endif

`ifdef TRNG_HEALTH_STATUS_DEBUG_PAGE_SELECT
    /* ui_in[0] is used for debug page select. */
    wire unused_or_used_ui_in0 = 1'b0;
`else
    /* ui_in[0] is unused in this build, so mark it consumed. */
    wire unused_or_used_ui_in0 = ui_in[0];
`endif

`ifdef JTAG_ENABLED
    /* ui_in[4] is used as the SPI/JTAG select when JTAG is present. */
    wire unused_or_used_ui_in4 = 1'b0;
`else
    wire unused_or_used_ui_in4 = ui_in[4];
`endif

    /* TODO check unused wires when SPI and/or UART not enabled */
`ifdef SPI_ENABLED
    wire _unused_ui_in = &{
        ui_in[7:5],
        unused_or_used_ui_in4,
        ui_in[2:1],
        unused_or_used_ui_in0
    };
`else
    /* not SPI_ENABLED */
    wire _unused_ui_in = &{
        ui_in[7:5],
        unused_or_used_ui_in4,
        uio_in[2],
        ui_in[2:1],
        unused_or_used_ui_in0
    };
`endif /* !SPI_ENABLED */

    wire _unused_debug_regs = &{
        reg_ctrl,
        reg_src,
`ifdef PIN_DIAG
        reg_div[7:5],
        reg_mode[4:0],
        reg_oscen,
        pin_id_active,
        pin_id_invalid_sel,
`else
        reg_div,
        reg_mode,
        reg_oscen,
`endif
        reg_status[7:3],
        reg_rawlo[7:3], 
        reg_rawhi[3:0]
    }; /* _unused_debug_regs */

    /*
     * Keep unused TT inputs referenced so synthesis does not warn.
     * ena is mandatory in the TT interface but not functionally used here.
     * uio_in is reserved for future use.
     */
    wire unused_ok;
`ifdef SPI_ENABLED
    assign unused_ok = &{ena, uio_in[7:4], uio_in[2], spi_mosi};
`else
    `ifdef JTAG_ENABLED
        assign unused_ok = &{ena, uio_in[7:4], jtag_tdi};
    `else
        assign unused_ok = &{ena, uio_in};
    `endif
`endif
    
    /*
     * Synchronize global reset, rst_n wire to rst_sync_n reg.
     */
    always @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            rst_meta_n <= 1'b0;
            rst_sync_n <= 1'b0;
        end else begin
            rst_meta_n <= 1'b1;
            rst_sync_n <= rst_meta_n;
        end
    end /* reset sync */


    /* 
     * Synchronize asynchronous UART RX input to the local clock domain.
     *
     * The external UART RX pin (ui_in[3]) is asynchronous to clk and can
     * violate setup/hold timing if sampled directly by synchronous logic.
     *
     * A two-stage synchronizer reduces metastability risk and prevents
     * X propagation/glitches observed during GF180 gate-level simulation.
     */
    always @(posedge clk) begin
        if (!rst_sync_n) begin
            uart_rx_meta <= 1'b1;
            uart_rx_sync <= 1'b1;
        end else begin
            uart_rx_meta <= ui_in[3];
            uart_rx_sync <= uart_rx_meta;
        end
    end

`ifdef JTAG_ENABLED
    /*
     * Synchronize asynchronous SPI/JTAG select input to the local clock domain.
     *
     * ui_in[4] selects who owns uio[3:0]:
     * - 0 = SPI
     * - 1 = JTAG
     *
     * The external mode-select pin is asynchronous to clk and should not
     * directly control the JTAG enable or SPI/JTAG register mux.
     */
    always @(posedge clk) begin
        if (!rst_sync_n) begin
        `ifdef ULX3S
            /* The default, unconnected gp4 on the ULX3S is high, 
             * reset sets debug mode to 1 for SPI */
            debug_sel_meta <= 1'b1;
            debug_sel_sync <= 1'b1;
        `else
            /* The TT Demoboard is 0 with SW4 in "up" position for SPI default,
             * set to zero at reset time for SPI */
            debug_sel_meta <= 1'b0;
            debug_sel_sync <= 1'b0;
        `endif
        end else begin
            /* Note debug_is_jtag = debug_sel_sync
             * conditionally inverted depending on platform! */
            debug_sel_meta <= ui_in[4]; /* SPI = 0 (default, TT SW4 dip switch "up" = 0), 1 = JTAG */
            debug_sel_sync <= debug_sel_meta;
        end
    end
`endif

`ifdef SPI_REG_ACCESS
    `ifdef JTAG_ENABLED
    /*
     * Register the selected SPI/JTAG register access bus.
     *
     * This breaks the long same-cycle path from debug_sel_sync through
     * the SPI/JTAG mux and into the configuration register write decode.
     * Register/debug writes gain one clk cycle of latency.
     */
    always @(posedge clk) begin
        if (!rst_sync_n) begin
            spi_reg_wr_en_r <= 1'b0;
            spi_reg_addr_r  <= {`SPI_ADDR_WIDTH{1'b0}};
            spi_reg_wdata_r <= 8'h00;
        end else begin
            spi_reg_wr_en_r <= spi_reg_wr_en_mux;
            spi_reg_addr_r  <= spi_reg_addr_mux;
            spi_reg_wdata_r <= spi_reg_wdata_mux;
        end
    end
    `endif
`endif

`ifdef PIN_DIAG
    /*
     * Pin-ID diagnostic control reuses the existing UART/SPI register bank:
     * - reg_oscen must be 0xA5 as a magic arm value
     * - reg_div[4:0] selects the logical TT pin
     * - reg_mode[7] enables pin-ID mode
     * - reg_mode[6:5] selects clock, pulse-count, or ASCII UART stream
     */
    assign pin_id_enable = (reg_oscen == 8'hA5) && reg_mode[7];
    assign pin_id_mode   = reg_mode[6:5];
    assign pin_id_sel    = reg_div[4:0];
`endif /* PIN_DIAG */

/*
 *******************************************************************************
 *******************************************************************************
 * Instantiate the UART Core
 *******************************************************************************
 *******************************************************************************
 */
    uart_trng_ascii_core
    #(
        .CLOCK_HZ(CLOCK_HZ),
        .UART_BAUD(UART_BAUD)
    )
    u_core
    (
        .clk(clk),
        .rst_n(rst_sync_n),
        .uart_rx_i(uart_rx_sync),
        .uart_tx_o(uart_tx),
        .reg_ctrl_o(reg_ctrl),
        .reg_src_o(reg_src),
        .reg_div_o(reg_div),
        .reg_mode_o(reg_mode),
        .reg_oscen_o(reg_oscen),
        .reg_status_o(reg_status),
        .reg_rawlo_o(reg_rawlo),
        .reg_rawhi_o(reg_rawhi),
        .trng_bit_o(trng_bit)
`ifdef SPI_REG_ACCESS
        ,
        .spi_reg_wr_en(spi_reg_wr_en),
        .spi_reg_addr(spi_reg_addr),
        .spi_reg_wdata(spi_reg_wdata),
        .spi_reg_rdata(spi_reg_rdata)
`endif

`ifdef BIG16_SPI_REG
        ,
        .ui_in(ui_in),
        .uo_out(uo_out),
        .uio_in(uio_in),
        .uio_out(uio_out),
        .uio_oe(uio_oe)
`endif    
);

/*
 *******************************************************************************
 *******************************************************************************
 * Optionally Instantiate the PIN Diagnostics Core
 *******************************************************************************
 *******************************************************************************
 */
`ifdef PIN_DIAG
    pin_id_core
    #(
        .CLOCK_HZ(CLOCK_HZ),
        .UART_BAUD(UART_BAUD)
    )
    u_pin_id_core
    (
        .clk(clk),
        .rst_n(rst_sync_n),
        .enable(pin_id_enable),
        .mode(pin_id_mode),
        .pin_sel(pin_id_sel),
        .uo_override(pin_id_uo_override),
        .uo_override_en(pin_id_uo_override_en),
        .uio_override(pin_id_uio_override),
        .uio_override_oe(pin_id_uio_override_oe),
        .active(pin_id_active),
        .invalid_sel(pin_id_invalid_sel)
    );
`endif /* PIN_DIAG */

    /*
     * Export one UART pin plus a few convenient status/data bits.
     * This is handy during bring-up because it gives visual/logic-analyzer
     * access to internal state without changing the core.
     */
    assign uo_out_normal[0] = trng_bit;
    assign uo_out_normal[1] = reg_status[0];
    assign uo_out_normal[2] = reg_status[1];
    assign uo_out_normal[3] = reg_status[2];

    assign uo_out_normal[4] = uart_tx;

`ifdef TRNG_HEALTH_STATUS_DEBUG_PAGE_SELECT
    /* TRNG_HEALTH_STATUS && DEBUG_PAGE_SELECT; See project_config.v */
    wire debug_page_health;

    assign debug_page_health = ui_in[0];

    assign uo_out_normal[5] = debug_page_health ? reg_status[3] : reg_rawlo[0];
    assign uo_out_normal[6] = debug_page_health ? reg_status[4] : reg_rawlo[1];
    assign uo_out_normal[7] = debug_page_health ? reg_status[7] : reg_rawlo[2];
`else
    assign uo_out_normal[5] = reg_rawlo[0];
    assign uo_out_normal[6] = reg_rawlo[1];
    assign uo_out_normal[7] = reg_rawlo[2];
`endif

`ifdef JTAG_ENABLED
    `ifdef ULX3S
        /* ui_in[4] = 1: ESP32 SPI owns uio[3:0] (default, unconnected = 1: PULLMODE=UP IO_TYPE=LVCMOS33 DRIVE=4;)
         * ui_in[4] = 0: external JTAG header owns uio[3:0], need to manually pull GP4 low */
        assign debug_is_jtag = ~debug_sel_sync; /* invert logic since pull-up default on ULX3S wrapper means unconnected = SPI (not JTAG)  */
    `else
        /* ui_in[4] = 0: ESP32 SPI owns uio[3:0] (TT default, INPUT Switch SW4 "up" = 0)
         * ui_in[4] = 1: external JTAG header owns uio[3:0], need to manually set TT INPUT SW4 "down" */
        assign debug_is_jtag = debug_sel_sync; /* NOT inverted logic since pull-up default on TT board INPUT dip switch default "up" = 0 (SPI), down=on JTAG  */
    `endif

    /* TODO: what happens with unconnected TT pin? */

    assign jtag_tms = uio_in[0];
    assign jtag_tdi = uio_in[1];
    assign jtag_tck = uio_in[3];

/*
 *******************************************************************************
 *******************************************************************************
 * Optionally Instantiate the JTAG Core
 *******************************************************************************
 *******************************************************************************
 */
    jtag_core u_jtag_core
    (
        .clk(clk),
        .rst_n(rst_sync_n),
        .ena(ena & debug_is_jtag),
        .tck_i(jtag_tck),
        .tms_i(jtag_tms),
        .tdi_i(jtag_tdi),
        .tdo_o(jtag_tdo),

    `ifdef SPI_REG_ACCESS
        .reg_addr_o(jtag_reg_addr),
        .reg_wr_o(jtag_reg_wr_en),
        .reg_wdata_o(jtag_reg_wdata),
        .reg_rdata_i(spi_reg_rdata)
    `else
        .reg_addr_o(),
        .reg_wr_o(),
        .reg_wdata_o(),
        .reg_rdata_i(8'h00)
    `endif
    );
`else
    /* No JTAG */
    /* assign debug_is_jtag = 1'b0; */
`endif


`ifdef SPI_REG_ACCESS
    `ifdef JTAG_ENABLED
        assign spi_reg_wr_en_mux = debug_is_jtag ? jtag_reg_wr_en : spi_slave_reg_wr_en;
        assign spi_reg_addr_mux  = debug_is_jtag ? jtag_reg_addr  : spi_slave_reg_addr;
        assign spi_reg_wdata_mux = debug_is_jtag ? jtag_reg_wdata : spi_slave_reg_wdata;

        assign spi_reg_wr_en = spi_reg_wr_en_r;
        assign spi_reg_addr  = spi_reg_addr_r;
        assign spi_reg_wdata = spi_reg_wdata_r;
    `else
        assign spi_reg_wr_en = spi_slave_reg_wr_en;
        assign spi_reg_addr  = spi_slave_reg_addr;
        assign spi_reg_wdata = spi_slave_reg_wdata;
    `endif
`endif

/*
 *******************************************************************************
 *******************************************************************************
 * Optionally Instantiate the SPI Core
 *******************************************************************************
 *******************************************************************************
 */
`ifdef SPI_ENABLED
    assign spi_cs_n = uio_in[0];
    assign spi_mosi = uio_in[1];
    assign spi_sck  = uio_in[3];

    tt_spi_slave u_spi_slave
    (
        .clk(clk),
        .rst_n(rst_sync_n),
        .spi_sck(spi_sck),
        .spi_cs_n(spi_cs_n),
        .spi_mosi(spi_mosi),
        .spi_miso(spi_miso),

    `ifdef SPI_REG_ACCESS
        .reg_wr_en(spi_slave_reg_wr_en),
        .reg_addr(spi_slave_reg_addr),
        .reg_wdata(spi_slave_reg_wdata),
        .reg_rdata(spi_reg_rdata)
    `else
        .reg_wr_en(spi_unused_reg_wr_en),
        .reg_addr(spi_unused_reg_addr),
        .reg_wdata(spi_unused_reg_wdata),
        .reg_rdata(8'h00)
    `endif
    );

    /* There's an optional PIN_DIAG mode that may override I/O, otherwise it is the [io wire name]_normal */
    assign uio_out_normal[0]   = 1'b0;
    assign uio_out_normal[1]   = 1'b0;

    `ifdef JTAG_ENABLED
        assign uio_out_normal[2]   = debug_is_jtag ? jtag_tdo : spi_miso;
    `else
        assign uio_out_normal[2]   = spi_miso;
    `endif

    assign uio_out_normal[3]   = 1'b0;

    assign uio_out_normal[7:4] = reg_rawhi[7:4];

    assign uio_oe_normal = 8'hF4;

    `ifndef SPI_REG_ACCESS
        wire _unused_spi_reg_outputs = &{
            1'b0,
            spi_unused_reg_wr_en,
            spi_unused_reg_addr,
            spi_unused_reg_wdata
        };
    `endif
    /* end SPI_ENABLED */
`else
    /* not SPI_ENABLED */
    `ifdef JTAG_ENABLED
        assign uio_out_normal[0]   = 1'b0;
        assign uio_out_normal[1]   = 1'b0;
        assign uio_out_normal[2]   = jtag_tdo;
        assign uio_out_normal[3]   = 1'b0;
        assign uio_out_normal[7:4] = reg_rawhi[7:4];

        assign uio_oe_normal = 8'hF4;
    `else
        assign uio_out_normal = reg_rawhi;
        assign uio_oe_normal  = 8'hFF;
    `endif
`endif /* not SPI_ENABLED */

`ifdef PIN_DIAG
    assign uo_out = pin_id_active ?
                    ((uo_out_normal & ~pin_id_uo_override_en) | pin_id_uo_override) :
                    uo_out_normal;

    assign uio_out = pin_id_active ?
                     pin_id_uio_override :
                     uio_out_normal;

    assign uio_oe = pin_id_active ?
                    pin_id_uio_override_oe :
                    uio_oe_normal;
`else
    assign uo_out  = uo_out_normal;
    assign uio_out = uio_out_normal;
    assign uio_oe  = uio_oe_normal;
`endif /* PIN_DIAG */

endmodule /* tt_um_main */

`default_nettype wire
