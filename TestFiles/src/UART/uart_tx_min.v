/*
 * Copyright (c) 2026 gojimmypi
 * SPDX-License-Identifier: Apache-2.0
 *
 * See ATTRIBUTION.md for third-party sources and credits.
 * 
 * The minimal UART RX and TX modules in this repository are original implementations
 * inspired by common FPGA UART examples, including:
 *
 *  https://nandland.com/uart-serial-port-module/
 *
 * file: uart_tx_min.v
 *
 * Minimal UART transmitter.
 *
 * Purpose:
 * - Sends one 8-bit byte over a UART TX line in 8N1 format.
 * - Asserts busy while a transfer is in progress.
 *
 * UART format:
 * - idle line high
 * - 1 start bit low
 * - 8 data bits, LSB first
 * - 1 stop bit high
 *
 * Handshake:
 * - start is sampled in ST_IDLE.
 * - data_in is captured only when a new transfer begins.
 * - busy stays high from start-bit launch until the stop bit completes.
 */
`default_nettype none

`ifdef SIM_JTAG_CORE_TB
    `timescale 1ns / 1ps
`endif

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

module uart_tx_min
#(
    parameter [31:0] CLOCK_HZ  = `PROJECT_CLOCK_HZ,
    parameter [31:0] UART_BAUD = `PROJECT_UART_BAUD
)
(
    input  wire       clk,
    input  wire       rst_n,
    input  wire [7:0] data_in,
    input  wire       start,
`ifdef ADJUSTABLE_BAUD_ENABLED
    input  wire [15:0] baud_div,
`endif
    output reg        tx,
    output reg        busy
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

`ifdef ADJUSTABLE_BAUD_ENABLED
    wire [15:0] clks_per_bit_m1;

    assign clks_per_bit_m1 = baud_div - 16'd1;
`else
    localparam integer CLKS_PER_BIT     = CLOCK_HZ / UART_BAUD;
    localparam integer CLKS_PER_BIT_M1  = CLKS_PER_BIT - 1;
 // localparam [15:0] CLKS_PER_HALF_M1 = (CLKS_PER_BIT >> 1) - 16'd1;
 // localparam [15:0] CLKS_PER_BIT_16    = CLKS_PER_BIT[15:0];
    localparam [15:0] CLKS_PER_BIT_M1_16 = CLKS_PER_BIT_M1[15:0];
 // localparam [15:0] CLKS_PER_HALF_M1_16 = CLKS_PER_HALF_M1[15:0];
`endif

    localparam [1:0] ST_IDLE  = 2'd0;
    localparam [1:0] ST_START = 2'd1;
    localparam [1:0] ST_DATA  = 2'd2;
    localparam [1:0] ST_STOP  = 2'd3;

    reg [1:0]  state;

    /*
     * shift_reg holds remaining bits to transmit.
     * The LSB is always the next data bit sent.
     */
    reg [7:0]  shift_reg;
    reg [3:0]  bit_index;
    reg [15:0] clk_count;

    always @(posedge clk) begin
        if (!rst_n) begin
            state     <= ST_IDLE;
            tx        <= 1'b1;
            busy      <= 1'b0;
            shift_reg <= 8'h00;
            bit_index <= 4'd0;
            clk_count <= 16'd0;
        end else begin
            case (state)
                ST_IDLE: begin
                    /* Line is idle-high when not transmitting. */
                    tx        <= 1'b1;
                    busy      <= 1'b0;
                    clk_count <= 16'd0;
                    bit_index <= 4'd0;

                    if (start) begin
                        /*
                         * Capture the byte and immediately drive the start bit.
                         * The first data bit will be launched after one full bit
                         * period in ST_START.
                         */
                        shift_reg <= data_in;
                        busy      <= 1'b1;
                        tx        <= 1'b0;
                        state     <= ST_START;
                    end
                end

                ST_START: begin
                    busy <= 1'b1;

`ifdef ADJUSTABLE_BAUD_ENABLED
                    if (clk_count == clks_per_bit_m1) begin
`else
                    if (clk_count == CLKS_PER_BIT_M1_16) begin
`endif

                        clk_count <= 16'd0;

                        /* Put the first payload bit on the line. */
                        tx        <= shift_reg[0];
                        shift_reg <= {1'b0, shift_reg[7:1]};
                        bit_index <= 4'd1;
                        state     <= ST_DATA;
                    end else begin
                        clk_count <= clk_count + 1'b1;
                    end
                end

                ST_DATA: begin
                    busy <= 1'b1;

`ifdef ADJUSTABLE_BAUD_ENABLED
                    if (clk_count == clks_per_bit_m1) begin
`else
                    if (clk_count == CLKS_PER_BIT_M1_16) begin
`endif
                        clk_count <= 16'd0;

                        if (bit_index == 4'd8) begin
                            /* All eight data bits have completed; drive the stop bit high. */
                            tx    <= 1'b1;
                            state <= ST_STOP;
                        end else begin
                            /* Continue shifting out remaining data bits. */
                            tx        <= shift_reg[0];
                            shift_reg <= {1'b0, shift_reg[7:1]};
                            bit_index <= bit_index + 1'b1;
                        end
                    end else begin
                        clk_count <= clk_count + 1'b1;
                    end
                end

                ST_STOP: begin
                    busy <= 1'b1;

`ifdef ADJUSTABLE_BAUD_ENABLED
                    if (clk_count == clks_per_bit_m1) begin
`else
                    if (clk_count == CLKS_PER_BIT_M1_16) begin
`endif
                        clk_count <= 16'd0;
                        state     <= ST_IDLE;
                    end else begin
                        clk_count <= clk_count + 1'b1;
                    end
                end

                default: begin
                    state <= ST_IDLE;
                end
            endcase
        end
    end

endmodule

`default_nettype wire
