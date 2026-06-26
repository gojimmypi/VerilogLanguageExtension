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
 * file: uart_rx_min.v
 *
 * Minimal UART receiver.
 *
 * Purpose:
 * - Samples an asynchronous RX line.
 * - Produces one-cycle data_valid when a full 8N1 byte has been received.
 *
 * UART format assumed:
 * - 1 start bit (low)
 * - 8 data bits, LSB first
 * - 1 stop bit (high)
 * - No parity
 *
 * Timing:
 * - CLKS_PER_BIT is the system-clock count for one UART bit period.
 * - The receiver first waits half a bit after detecting a falling edge so it
 *   can validate the center of the start bit.
 * - It then samples once per bit period for each data bit and stop bit.
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

module uart_rx_min
#(
    parameter [31:0] CLOCK_HZ  = `PROJECT_CLOCK_HZ,
    parameter [31:0] UART_BAUD = `PROJECT_UART_BAUD
)
(
    input  wire       clk,
    input  wire       rst_n,
    input  wire       rx,
`ifdef ADJUSTABLE_BAUD_ENABLED
    input  wire [15:0] baud_div,
`endif
    output reg [7:0]  data_out,
    output reg        data_valid
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
    wire [15:0] clks_per_half_m1;

    assign clks_per_bit_m1  = baud_div - 16'd1;
    assign clks_per_half_m1 = {1'b0, baud_div[15:1]} - 16'd1;
`else
    localparam integer CLKS_PER_BIT     = CLOCK_HZ / UART_BAUD;
    localparam integer CLKS_PER_BIT_M1  = CLKS_PER_BIT - 1;
    localparam integer CLKS_PER_HALF_M1 = (CLKS_PER_BIT >> 1) - 1;
 // localparam [15:0] CLKS_PER_BIT_16    = CLKS_PER_BIT[15:0];
    localparam [15:0] CLKS_PER_BIT_M1_16 = CLKS_PER_BIT_M1[15:0];
    localparam [15:0] CLKS_PER_HALF_M1_16 = CLKS_PER_HALF_M1[15:0];
`endif

    localparam [1:0] ST_IDLE  = 2'd0;
    localparam [1:0] ST_START = 2'd1;
    localparam [1:0] ST_DATA  = 2'd2;
    localparam [1:0] ST_STOP  = 2'd3;

    reg [1:0]  state;

    /*
     * Two-flop synchronizer for the asynchronous UART input.
     * This reduces metastability risk before the state machine samples bits.
     */
    reg        rx_meta;
    reg        rx_sync;

    /* Bit-period counter and current-bit index. */
    reg [15:0] clk_count;
    reg [2:0]  bit_index;

    /* Shift register receives the byte LSB first. */
    reg [7:0]  shift_reg;

    always @(posedge clk) begin
        if (!rst_n) begin
            rx_meta <= 1'b1;
            rx_sync <= 1'b1;
        end else begin
            rx_meta <= rx;
            rx_sync <= rx_meta;
        end
    end

    always @(posedge clk) begin
        if (!rst_n) begin
            state      <= ST_IDLE;
            clk_count  <= 16'd0;
            bit_index  <= 3'd0;
            shift_reg  <= 8'h00;
            data_out   <= 8'h00;
            data_valid <= 1'b0;
        end else begin
            /* data_valid is a pulse, not a level. */
            data_valid <= 1'b0;

            case (state)
                ST_IDLE: begin
                    /*
                     * Wait for the line to go low, which may indicate a start
                     * bit. Reset counters so the next state starts cleanly.
                     */
                    clk_count <= 16'd0;
                    bit_index <= 3'd0;

                    if (rx_sync == 1'b0) begin
                        state     <= ST_START;
                        clk_count <= 16'd0;
                    end
                end

                ST_START: begin
                    /*
                     * Sample in the middle of the start bit. If the line has
                     * returned high, it was likely just noise or a glitch.
                     */
`ifdef ADJUSTABLE_BAUD_ENABLED
                    if (clk_count == clks_per_half_m1) begin
`else
                    if (clk_count == CLKS_PER_HALF_M1_16) begin
`endif

                        if (rx_sync == 1'b0) begin
                            clk_count <= 16'd0;
                            bit_index <= 3'd0;
                            state     <= ST_DATA;
                        end else begin
                            state <= ST_IDLE;
                        end
                    end else begin
                        clk_count <= clk_count + 1'b1;
                    end
                end

                ST_DATA: begin
                    /*
                     * Sample one data bit every full bit period.
                     * Because UART is LSB-first, bit_index maps directly to the
                     * destination bit position.
                     */
`ifdef ADJUSTABLE_BAUD_ENABLED
                    if (clk_count == clks_per_bit_m1) begin
`else
                    if (clk_count == CLKS_PER_BIT_M1_16) begin
`endif
                        clk_count <= 16'd0;
                        shift_reg[bit_index] <= rx_sync;

                        if (bit_index == 3'd7) begin
                            state <= ST_STOP;
                        end else begin
                            bit_index <= bit_index + 1'b1;
                        end
                    end else begin
                        clk_count <= clk_count + 1'b1;
                    end
                end

                ST_STOP: begin
                    /*
                     * Check for a valid stop bit. Only then is the received byte
                     * presented and data_valid pulsed for one clock.
                     */
`ifdef ADJUSTABLE_BAUD_ENABLED
                    if (clk_count == clks_per_bit_m1) begin
`else
                    if (clk_count == CLKS_PER_BIT_M1_16) begin
`endif
                        clk_count <= 16'd0;

                        if (rx_sync == 1'b1) begin
                            data_out   <= shift_reg;
                            data_valid <= 1'b1;
                        end

                        state <= ST_IDLE;
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
