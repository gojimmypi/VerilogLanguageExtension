/*
 * Copyright (c) 2026 gojimmypi
 * SPDX-License-Identifier: Apache-2.0
 *
 * See ATTRIBUTION.md for third-party sources and credits.
 *
 * file: uart_trng_ascii_core.v
 *
 * Core integration block for the TRNG ASCII design.
 *
 * Purpose:
 * - Connects the minimal UART RX and TX blocks.
 * - In normal mode, connects the ASCII command parser to the TRNG stub.
 * - In FORCE_DEEP_LOOPBACK mode, bypasses the parser/TRNG path and performs an
 *   internal byte echo so RX/TX can be isolated and validated.
 *
 * Why this block matters:
 * - It is the main point where the same functional core can be reused under
 *   both the Tiny Tapeout wrapper and the ULX3S wrapper.
 */
`default_nettype none

`ifdef SIM_JTAG_CORE_TB
    `timescale 1ns / 1ps
`endif

/*
** See build options:
**   `define FORCE_DEEP_LOOPBACK
*/

/* Although the project config is in a parent directory, the Makefile should include
 * a proper directory parameter for yoysys to find it with no path:
 *   `include "project_config.v"
 *
 * But the GH FPGA action: python tt/tt_tool.py --create-user-config $FLOW_ARG
 * does not include the path in the Makefile at this time. So we see an error:
 *   ERROR: Can't open include file `project_config.v'!
 *   2026-05-21 07:40:04,633 - project    - ERROR    - yosys port read failed for [000 : unknown]
 * For example: https://github.com/gojimmypi/ttsky-UART-FSM-TRNG-Lab/actions/runs/26212459101/job/77126453032 
 *
 * Here we assume another file has included the project_config.v, with error checks for zero values.
 */

module uart_trng_ascii_core
#(
    parameter [31:0] CLOCK_HZ  = `PROJECT_CLOCK_HZ,
    parameter [31:0] UART_BAUD = `PROJECT_UART_BAUD
)
(
    input  wire       clk,
    input  wire       rst_n,
    input  wire       uart_rx_i,
    output wire       uart_tx_o,

    output wire [7:0] reg_ctrl_o,
    output wire [7:0] reg_src_o,
    output wire [7:0] reg_div_o,
    output wire [7:0] reg_mode_o,
    output wire [7:0] reg_oscen_o,

    output wire [7:0] reg_status_o,
    output wire [7:0] reg_rawlo_o,
    output wire [7:0] reg_rawhi_o,
    output wire       trng_bit_o
`ifdef SPI_REG_ACCESS
    ,
    input  wire       spi_reg_wr_en,
    input  wire [`SPI_ADDR_MSB:0] spi_reg_addr,
    input  wire [7:0] spi_reg_wdata,
    output wire [7:0] spi_reg_rdata
`endif /* SPI_REG_ACCESS */

`ifdef BIG16_SPI_REG
    ,
    input  wire [7:0] ui_in,
    input  wire [7:0] uo_out,
    input  wire [7:0] uio_in,
    input  wire [7:0] uio_out,
    input  wire [7:0] uio_oe
`endif
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

    /* UART receive side: decoded byte plus one-cycle valid pulse. */
    wire [7:0] rx_byte;
    wire       rx_valid;

    /* UART transmit side: byte, launch pulse, busy indication, and raw TX line. */
    wire [7:0] tx_byte;
    wire       tx_start;
    wire       tx_busy;
    wire       uart_tx_raw;

`ifdef ADJUSTABLE_BAUD_ENABLED
    wire [1:0] uart_baud_sel;

    localparam integer UART_DIV_115200 = CLOCK_HZ / 32'd115_200;
    localparam integer UART_DIV_230400 = CLOCK_HZ / 32'd230_400;
    localparam integer UART_DIV_460800 = CLOCK_HZ / 32'd460_800;
    localparam integer UART_DIV_921600 = CLOCK_HZ / 32'd921_600;

    /* Boilerplate parameter checking */
    generate
        if (UART_DIV_115200 == 32'd0) begin : gen_bad_uart_div_115200
            PROJECT_MUST_NOT_USE_ZERO_UART_DIV_115200 u_stop ();
        end
        if (UART_DIV_230400 == 32'd0) begin : gen_bad_uart_div_230400
            PROJECT_MUST_NOT_USE_ZERO_UART_DIV_230400 u_stop ();
        end
        if (UART_DIV_460800 == 32'd0) begin : gen_bad_uart_div_460800
            PROJECT_MUST_NOT_USE_ZERO_UART_DIV_460800 u_stop ();
        end
        if (UART_DIV_921600 == 32'd0) begin : gen_bad_uart_div_921600
            PROJECT_MUST_NOT_USE_ZERO_UART_DIV_921600 u_stop ();
        end

    endgenerate

    localparam [15:0] UART_DIV_115200_16 = UART_DIV_115200[15:0];
    localparam [15:0] UART_DIV_230400_16 = UART_DIV_230400[15:0];
    localparam [15:0] UART_DIV_460800_16 = UART_DIV_460800[15:0];
    localparam [15:0] UART_DIV_921600_16 = UART_DIV_921600[15:0];

    wire [15:0] uart_baud_div;

    assign uart_baud_div = (uart_baud_sel == 2'd3) ? UART_DIV_921600_16 :
                           (uart_baud_sel == 2'd2) ? UART_DIV_460800_16 :
                           (uart_baud_sel == 2'd1) ? UART_DIV_230400_16 :
                                                       UART_DIV_115200_16;
`endif /* ADJUSTABLE_BAUD_ENABLED */

    /*
     * Locally replicate the incoming reset inside this integration block.
     *
     * The outer TT wrapper already synchronizes the external reset. These local
     * reset flops split the high-fanout reset tree so the physical tools do not
     * need to drive every UART/config/TRNG flop from one large reset net.
     */
    (* keep = "true" *) reg rst_uart_meta_n;
    (* keep = "true" *) reg rst_uart_sync_n;
    (* keep = "true" *) reg rst_cfg_meta_n;
    (* keep = "true" *) reg rst_cfg_sync_n;
    (* keep = "true" *) reg rst_trng_meta_n;
    (* keep = "true" *) reg rst_trng_sync_n;

    always @(posedge clk) begin
        if (!rst_n) begin
            rst_uart_meta_n <= 1'b0;
            rst_uart_sync_n <= 1'b0;
            rst_cfg_meta_n  <= 1'b0;
            rst_cfg_sync_n  <= 1'b0;
            rst_trng_meta_n <= 1'b0;
            rst_trng_sync_n <= 1'b0;
        end else begin
            rst_uart_meta_n <= 1'b1;
            rst_uart_sync_n <= rst_uart_meta_n;
            rst_cfg_meta_n  <= 1'b1;
            rst_cfg_sync_n  <= rst_cfg_meta_n;
            rst_trng_meta_n <= 1'b1;
            rst_trng_sync_n <= rst_trng_meta_n;
        end
    end

    /*
     * The external UART TX line must idle high whenever the transmitter is not busy.
     * Writing this as OR logic also masks gate-level simulation X values on the
     * raw TX flop while the transmitter is idle. In normal 2-state hardware logic,
     * this is equivalent to: tx_busy ? uart_tx_raw : 1'b1.
     *
     * Truth table:
     *     tx_busy  uart_tx_raw  uart_tx_o
     *        0        0            1
     *        0        1            1
     *        0        X            1
     *        1        0            0
     *        1        1            1
     *        1        X            X
     */
    assign uart_tx_o = (~tx_busy) | uart_tx_raw;

    uart_rx_min
    #(
        .CLOCK_HZ(CLOCK_HZ),
        .UART_BAUD(UART_BAUD)
    )
    u_rx
    (
        .clk(clk),
        .rst_n(rst_uart_sync_n),
        .rx(uart_rx_i),
`ifdef ADJUSTABLE_BAUD_ENABLED
        .baud_div(uart_baud_div),
`endif
        .data_out(rx_byte),
        .data_valid(rx_valid)
    );

    uart_tx_min
    #(
        .CLOCK_HZ(CLOCK_HZ),
        .UART_BAUD(UART_BAUD)
    )
    u_tx
    (
        .clk(clk),
        .rst_n(rst_uart_sync_n),
        .data_in(tx_byte),
        .start(tx_start),
`ifdef ADJUSTABLE_BAUD_ENABLED
        .baud_div(uart_baud_div),
`endif
        .tx(uart_tx_raw),
        .busy(tx_busy)
    );

`ifdef FORCE_DEEP_LOOPBACK
    /*
     * Deep internal loopback mode:
     * - Meant to validate uart_rx_min and uart_tx_min in isolation.
     * - A received byte is sent straight back out when TX is idle.
     * - The register outputs become simple debug/status placeholders.
     *
     * This should not be combined with top-level FORCE_LOOPBACK, because then
     * the observed behavior would no longer reflect the internal echo path.
     */
    `ifdef FORCE_LOOPBACK
        MODULE_FORCE_LOOPBACK_MUST_NOT_BE_ENABLED_WITH_FORCE_DEEP_LOOPBACK u_stop ();
    `endif

    reg  [7:0] tx_byte_r;
    reg        tx_start_r;
    reg        rx_valid_d;

    reg  [7:0] pending_byte_r;
    reg        pending_valid_r;
    reg        overflow_r;

    reg  [7:0] reg_status_r;
    reg  [7:0] reg_rawlo_r;
    reg  [7:0] reg_rawhi_r;
    reg        trng_bit_r;

    /* Pulse detect so a received byte is echoed exactly once. */
    wire       rx_valid_pulse;

    /* Placeholder config outputs in loopback mode. */
    wire [7:0] reg_ctrl;
    wire [7:0] reg_src;
    wire [7:0] reg_div;
    wire [7:0] reg_mode;
    wire [7:0] reg_oscen;

    /* Debug/status outputs in loopback mode. */
    wire [7:0] reg_status;
    wire [7:0] reg_rawlo;
    wire [7:0] reg_rawhi;

`ifdef TRNG_BINARY_STREAM
    wire [7:0] stream_sample_count;
`endif

`ifdef TRNG_CONDITIONED_STREAM
`ifdef TRNG_CONDITIONED_STREAM_64_XOR
    wire [7:0] reg_cond0;
    wire [7:0] reg_cond1;
    wire [7:0] reg_cond2;
    wire [7:0] reg_cond3;
    wire [7:0] reg_cond4;
    wire [7:0] reg_cond5;
    wire [7:0] reg_cond6;
    wire [7:0] reg_cond7;
`else
    wire [7:0] reg_condlo;
    wire [7:0] reg_condhi;
`endif /* ! TRNG_CONDITIONED_STREAM_64_XOR */
`endif /* TRNG_CONDITIONED_STREAM */ 

    wire       trng_bit;

    assign rx_valid_pulse = rx_valid && !rx_valid_d;

`ifdef ADJUSTABLE_BAUD_ENABLED
    assign uart_baud_sel = 2'd0;
`endif

    assign tx_byte  = tx_byte_r;
    assign tx_start = tx_start_r;

    assign reg_ctrl  = 8'h00;
    assign reg_src   = 8'h00;
    assign reg_div   = 8'h10;
    assign reg_mode  = 8'h00;
    assign reg_oscen = 8'h01;

    assign reg_status = reg_status_r;
    assign reg_rawlo  = reg_rawlo_r;
    assign reg_rawhi  = reg_rawhi_r;
    assign trng_bit   = trng_bit_r;
`ifdef SPI_REG_ACCESS
    assign spi_reg_rdata = 8'h00;

    wire _unused_spi_loopback = &{spi_reg_wr_en, spi_reg_addr, spi_reg_wdata};
`endif

    always @(posedge clk) begin
        if (!rst_cfg_sync_n) begin
            rx_valid_d      <= 1'b0;
            tx_byte_r       <= 8'h00;
            tx_start_r      <= 1'b0;
            pending_byte_r  <= 8'h00;
            pending_valid_r <= 1'b0;
            overflow_r      <= 1'b0;
            reg_status_r    <= 8'h00;
            reg_rawlo_r     <= 8'h00;
            reg_rawhi_r     <= 8'h00;
            trng_bit_r      <= 1'b0;
        end else begin
            rx_valid_d <= rx_valid;
            tx_start_r <= 1'b0;

            /*
             * Pack a few useful live debug indicators:
             * bit0 = raw UART RX input level at this clock
             * bit1 = decoded receive-byte pulse
             * bit2 = local TX start pulse
             * bit3 = TX busy
             * bit4 = pending byte waiting for TX
             * bit5 = loopback overflow/drop occurred
             */
            reg_status_r[0]   <= uart_rx_i;
            reg_status_r[1]   <= rx_valid;
            reg_status_r[2]   <= tx_start_r;
            reg_status_r[3]   <= tx_busy;
            reg_status_r[4]   <= pending_valid_r;
            reg_status_r[5]   <= overflow_r;
            reg_status_r[7:6] <= 2'b00;

            if (rx_valid_pulse) begin
                if (!pending_valid_r) begin
                    pending_byte_r  <= rx_byte;
                    pending_valid_r <= 1'b1;
                    reg_rawlo_r     <= rx_byte;
                    reg_rawhi_r     <= rx_byte;
                    trng_bit_r      <= rx_byte[0];
                end else begin
                    overflow_r <= 1'b1;
                end
            end

            if (!tx_busy && pending_valid_r) begin
                tx_byte_r       <= pending_byte_r;
                tx_start_r      <= 1'b1;
                pending_valid_r <= 1'b0;
            end
        end
    end
    /* End of FORCE_DEEP_LOOPBACK mode. */
`else

    /*
     * Normal system mode:
     * - trng_cfg_ascii_core interprets UART command bytes.
     *
     * - trng_stub supplies readable status and sample bytes.
     *     ** or (see project.v TRNG_ENABLED) **
     * - trng_lab_core or trng_stub supplies readable status and sample bytes.
     */
    wire [7:0] reg_ctrl;
    wire [7:0] reg_src;
    wire [7:0] reg_div;
    wire [7:0] reg_mode;
    wire [7:0] reg_oscen;

    wire [7:0] reg_status;
    wire [7:0] reg_rawlo;
    wire [7:0] reg_rawhi;

`ifdef TRNG_BINARY_STREAM
    wire [7:0] stream_sample_count;
`endif

`ifdef TRNG_CONDITIONED_STREAM
`ifdef TRNG_CONDITIONED_STREAM_64_XOR
    wire [7:0] reg_cond0;
    wire [7:0] reg_cond1;
    wire [7:0] reg_cond2;
    wire [7:0] reg_cond3;
    wire [7:0] reg_cond4;
    wire [7:0] reg_cond5;
    wire [7:0] reg_cond6;
    wire [7:0] reg_cond7;
`else
    wire [7:0] reg_condlo;
    wire [7:0] reg_condhi;
`endif /* ! TRNG_CONDITIONED_STREAM_64_XOR */
`endif /* TRNG_CONDITIONED_STREAM */

    wire       trng_bit;

    /*
     * Register the config boundary before the TRNG block.
     *
     * This keeps UART/SPI-visible config registers immediate at this wrapper
     * boundary, but gives the TRNG core local one-cycle-delayed copies. The
     * extra register stage breaks long config/reset/control paths from the
     * ASCII/SPI config block into TRNG sample counter, LFSR, and raw-register
     * update logic.
     */
    reg  [7:0] trng_reg_ctrl;
    reg  [7:0] trng_reg_src;
    reg  [7:0] trng_reg_div;
    reg  [7:0] trng_reg_mode;
    reg  [7:0] trng_reg_oscen;

    always @(posedge clk or negedge rst_trng_sync_n) begin
        if (!rst_trng_sync_n) begin
            trng_reg_ctrl  <= 8'h00;
            trng_reg_src   <= 8'h00;
            trng_reg_div   <= 8'h10;
            trng_reg_mode  <= 8'h00;
            trng_reg_oscen <= 8'h01;
        end else begin
            trng_reg_ctrl  <= reg_ctrl;
            trng_reg_src   <= reg_src;
            trng_reg_div   <= reg_div;
            trng_reg_mode  <= reg_mode;
            trng_reg_oscen <= reg_oscen;
        end
    end

`ifndef SPI_REG_ACCESS
    wire [7:0] unused_spi_reg_rdata;
    wire       unused_spi_reg_rdata_ok;

    assign unused_spi_reg_rdata_ok = &{1'b0, unused_spi_reg_rdata};
`endif

    trng_cfg_ascii_core u_cfg
    (
        .clk(clk),
        .rst_n(rst_cfg_sync_n),

        .rx_byte(rx_byte),
        .rx_valid(rx_valid),

        .tx_byte(tx_byte),
        .tx_start(tx_start),
        .tx_busy(tx_busy),

`ifdef ADJUSTABLE_BAUD_ENABLED
        .uart_baud_sel(uart_baud_sel),
`endif

        .reg_ctrl(reg_ctrl),
        .reg_src(reg_src),
        .reg_div(reg_div),
        .reg_mode(reg_mode),
        .reg_oscen(reg_oscen),

        .reg_status(reg_status),
        .reg_rawlo(reg_rawlo),
        .reg_rawhi(reg_rawhi),

`ifdef BIG16_SPI_REG
        .ui_in(ui_in),
        .uo_out(uo_out),
        .uio_in(uio_in),
        .uio_out(uio_out),
        .uio_oe(uio_oe),
`endif

`ifdef TRNG_BINARY_STREAM
        .stream_sample_count(stream_sample_count),
`endif

`ifdef TRNG_CONDITIONED_STREAM
`ifdef TRNG_CONDITIONED_STREAM_64_XOR
        .reg_cond0(reg_cond0),
        .reg_cond1(reg_cond1),
        .reg_cond2(reg_cond2),
        .reg_cond3(reg_cond3),
        .reg_cond4(reg_cond4),
        .reg_cond5(reg_cond5),
        .reg_cond6(reg_cond6),
        .reg_cond7(reg_cond7),
`else
        .reg_condlo(reg_condlo),
        .reg_condhi(reg_condhi),
`endif
`endif

`ifdef SPI_REG_ACCESS
        .spi_reg_wr_en(spi_reg_wr_en),
        .spi_reg_addr(spi_reg_addr),
        .spi_reg_wdata(spi_reg_wdata),
        .spi_reg_rdata(spi_reg_rdata)
`else
        .spi_reg_wr_en(1'b0),
        .spi_reg_addr({`SPI_ADDR_WIDTH{1'b0}}),
        .spi_reg_wdata(8'h00),
        .spi_reg_rdata(unused_spi_reg_rdata)
`endif
    );

`ifdef TRNG_ENABLED
    trng_lab_core u_trng
    (
        .clk(clk),
        .rst_n(rst_trng_sync_n),
        .reg_ctrl(trng_reg_ctrl),
        .reg_src(trng_reg_src),
        .reg_div(trng_reg_div),
        .reg_mode(trng_reg_mode),
        .reg_oscen(trng_reg_oscen),
        .reg_status(reg_status),
        .reg_rawlo(reg_rawlo),
        .reg_rawhi(reg_rawhi),

`ifdef TRNG_BINARY_STREAM
        .stream_sample_count(stream_sample_count),
`endif

`ifdef TRNG_CONDITIONED_STREAM
`ifdef TRNG_CONDITIONED_STREAM_64_XOR
        .reg_cond0(reg_cond0),
        .reg_cond1(reg_cond1),
        .reg_cond2(reg_cond2),
        .reg_cond3(reg_cond3),
        .reg_cond4(reg_cond4),
        .reg_cond5(reg_cond5),
        .reg_cond6(reg_cond6),
        .reg_cond7(reg_cond7),
`else
        .reg_condlo(reg_condlo),
        .reg_condhi(reg_condhi),
`endif
`endif
        .trng_bit(trng_bit)
    );
`else
`ifdef TRNG_CONDITIONED_STREAM
`ifdef TRNG_CONDITIONED_STREAM_64_XOR
    assign reg_cond0 = reg_rawlo;
    assign reg_cond1 = reg_rawhi;
    assign reg_cond2 = reg_rawlo;
    assign reg_cond3 = reg_rawhi;
    assign reg_cond4 = reg_rawlo;
    assign reg_cond5 = reg_rawhi;
    assign reg_cond6 = reg_rawlo;
    assign reg_cond7 = reg_rawhi;
`else
    assign reg_condlo = reg_rawlo;
    assign reg_condhi = reg_rawhi;
`endif
`endif
    /* use only the stub when TRNG is not enabled, so we can still test the ASCII parser and UART path */
    trng_stub u_trng
    (
        .clk(clk),
        .rst_n(rst_trng_sync_n),
        .reg_ctrl(trng_reg_ctrl),
        .reg_src(trng_reg_src),
        .reg_div(trng_reg_div),
        .reg_mode(trng_reg_mode),
        .reg_oscen(trng_reg_oscen[0]),
        .reg_status(reg_status),
        .reg_rawlo(reg_rawlo),
        .reg_rawhi(reg_rawhi),
    `ifdef TRNG_BINARY_STREAM
        .stream_sample_count(stream_sample_count),
    `endif
        .trng_bit(trng_bit)
    );
`endif /* End of TRNG_ENABLED conditional. */
`endif /* End of FORCE_DEEP_LOOPBACK conditional. */

    /* Re-export selected internals to the outer wrappers for debug/visibility. */
    assign reg_ctrl_o   = reg_ctrl;
    assign reg_src_o    = reg_src;
    assign reg_div_o    = reg_div;
    assign reg_mode_o   = reg_mode;
    assign reg_oscen_o  = reg_oscen;

    assign reg_status_o = reg_status;
    assign reg_rawlo_o  = reg_rawlo;
    assign reg_rawhi_o  = reg_rawhi;
    assign trng_bit_o   = trng_bit;

endmodule

`default_nettype wire
