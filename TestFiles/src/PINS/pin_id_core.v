/*
 * Copyright (c) 2026 gojimmypi
 * SPDX-License-Identifier: Apache-2.0
 *
 * See ATTRIBUTION.md for third-party sources and credits.
 *
 * file: pin_id_core.v
 *
 * Select-one-pin diagnostic signal generator for Tiny Tapeout-style pins.
 *
 * The host selects one logical TT output-capable pin and one diagnostic mode.
 * This core then generates a clock, pulse-count ID, or ASCII UART stream for
 * that selected pin only. Input-only ui_in pins are intentionally not driven.
 *
 * Pin select values:
 * - 0x00..0x07: uo_out[0..7]
 * - 0x10..0x17: uio[0..7]
 *
 * Modes:
 * - 00: clock/square wave
 * - 01: pulse-count ID, uo[0..7] = 1..8 pulses, uio[0..7] = 9..16 pulses
 * - 10: ASCII UART stream, repeatedly sends "P=xx\r" where xx is pin_sel
 * - 11: reserved, currently same as clock/square wave
 *
 * NOTE: It appears that when this module is enabled with PIN_DIAG, the size
 * exceeds the 1x2 tiles. See job that ran for over an 90 minutes before abort: 
 *
 *   https://github.com/gojimmypi/ttsky-UART-FSM-TRNG-Lab/actions/runs/27152683035
 *
 * and subsequent success for next bigger tile size 2x2:
 *
 *   https://github.com/gojimmypi/ttsky-UART-FSM-TRNG-Lab/actions/runs/27157950274 
 *
 */
`default_nettype none

`ifdef SIM_JTAG_CORE_TB
    `timescale 1ns / 1ps
`endif

`ifdef PIN_DIAG

module pin_id_core
#(
    parameter [31:0] CLOCK_HZ  = `PROJECT_CLOCK_HZ,
    parameter [31:0] UART_BAUD = `PROJECT_UART_BAUD
)
(
    input  wire       clk,
    input  wire       rst_n,

    input  wire       enable,
    input  wire [1:0] mode,
    input  wire [4:0] pin_sel,

    output wire [7:0] uo_override,
    output wire [7:0] uo_override_en,
    output wire [7:0] uio_override,
    output wire [7:0] uio_override_oe,
    output wire       active,
    output wire       invalid_sel
);
    localparam [31:0] CLOCK_TOGGLE_DIV = (CLOCK_HZ / 32'd2000) == 32'd0 ? 32'd1 : (CLOCK_HZ / 32'd2000);
    localparam [31:0] PULSE_TOGGLE_DIV = (CLOCK_HZ / 32'd200)  == 32'd0 ? 32'd1 : (CLOCK_HZ / 32'd200);

//  localparam [1:0] MODE_CLOCK = 2'b00;
    localparam [1:0] MODE_PULSE = 2'b01;
    localparam [1:0] MODE_ASCII = 2'b10;

    wire       sel_uo;
    wire       sel_uio;
    wire [2:0] sel_bit;
    wire [4:0] pulse_count_target;

    reg [31:0] div_count;
    reg        clock_signal;
    reg        pulse_signal;
    reg [4:0]  pulse_count;
    reg [3:0]  pulse_gap_count;
    reg        pulse_high_phase;
    reg        pulse_gap_phase;

    wire [7:0] ascii_tx_byte;
    wire       ascii_tx_start;
    wire       ascii_tx_busy;
    wire       ascii_tx_raw;
    wire       ascii_signal;

    reg [2:0]  ascii_state;
    reg [7:0]  ascii_tx_byte_r;
    reg        ascii_tx_start_r;

    function [7:0] to_hex_ascii;
        input [3:0] nib;
        begin
            if (nib < 4'd10) begin
                to_hex_ascii = {4'b0011, nib};
            end else begin
                to_hex_ascii = {4'b0011, nib} + 8'd7;
            end
        end
    endfunction

    assign sel_uo      = pin_sel[4:3] == 2'b00;
    assign sel_uio     = pin_sel[4:3] == 2'b10;
    assign sel_bit     = pin_sel[2:0];
    assign invalid_sel = enable && !(sel_uo || sel_uio);
    assign active      = enable && !invalid_sel;

    assign pulse_count_target = sel_uio ? ({2'b00, sel_bit} + 5'd9) :
                                ({2'b00, sel_bit} + 5'd1);

    assign ascii_tx_byte  = ascii_tx_byte_r;
    assign ascii_tx_start = ascii_tx_start_r;
    assign ascii_signal   = (~ascii_tx_busy) | ascii_tx_raw;

    uart_tx_min
    #(
        .CLOCK_HZ(CLOCK_HZ),
        .UART_BAUD(UART_BAUD)
    )
    u_pin_id_tx
    (
        .clk(clk),
        .rst_n(rst_n),
        .data_in(ascii_tx_byte),
        .start(ascii_tx_start),
        .tx(ascii_tx_raw),
        .busy(ascii_tx_busy)
    );

    always @(posedge clk) begin
        if (!rst_n) begin
            div_count        <= 32'd0;
            clock_signal     <= 1'b0;
            pulse_signal     <= 1'b0;
            pulse_count      <= 5'd0;
            pulse_gap_count  <= 4'd0;
            pulse_high_phase <= 1'b0;
            pulse_gap_phase  <= 1'b0;
            ascii_state      <= 3'd0;
            ascii_tx_byte_r  <= 8'h00;
            ascii_tx_start_r <= 1'b0;
        end else begin
            ascii_tx_start_r <= 1'b0;

            if (!active) begin
                div_count        <= 32'd0;
                clock_signal     <= 1'b0;
                pulse_signal     <= 1'b0;
                pulse_count      <= 5'd0;
                pulse_gap_count  <= 4'd0;
                pulse_high_phase <= 1'b0;
                pulse_gap_phase  <= 1'b0;
                ascii_state      <= 3'd0;
            end else if (mode == MODE_PULSE) begin
                if (div_count == (PULSE_TOGGLE_DIV - 1'b1)) begin
                    div_count <= 32'd0;

                    if (pulse_gap_phase) begin
                        pulse_signal <= 1'b0;
                        pulse_count  <= 5'd0;

                        if (pulse_gap_count == 4'd8) begin
                            pulse_gap_count <= 4'd0;
                            pulse_gap_phase <= 1'b0;
                        end else begin
                            pulse_gap_count <= pulse_gap_count + 1'b1;
                        end
                    end else if (pulse_high_phase) begin
                        pulse_signal     <= 1'b0;
                        pulse_high_phase <= 1'b0;

                        if (pulse_count == (pulse_count_target - 1'b1)) begin
                            pulse_count     <= 5'd0;
                            pulse_gap_phase <= 1'b1;
                        end else begin
                            pulse_count <= pulse_count + 1'b1;
                        end
                    end else begin
                        pulse_signal     <= 1'b1;
                        pulse_high_phase <= 1'b1;
                    end
                end else begin
                    div_count <= div_count + 1'b1;
                end
            end else begin
                pulse_signal     <= 1'b0;
                pulse_count      <= 5'd0;
                pulse_gap_count  <= 4'd0;
                pulse_high_phase <= 1'b0;
                pulse_gap_phase  <= 1'b0;

                if (div_count == (CLOCK_TOGGLE_DIV - 1'b1)) begin
                    div_count    <= 32'd0;
                    clock_signal <= ~clock_signal;
                end else begin
                    div_count <= div_count + 1'b1;
                end
            end

            if (active && (mode == MODE_ASCII) && !ascii_tx_busy && !ascii_tx_start_r) begin
                case (ascii_state)
                    3'd0: begin
                        ascii_tx_byte_r  <= "P";
                        ascii_tx_start_r <= 1'b1;
                        ascii_state      <= 3'd1;
                    end

                    3'd1: begin
                        ascii_tx_byte_r  <= "=";
                        ascii_tx_start_r <= 1'b1;
                        ascii_state      <= 3'd2;
                    end

                    3'd2: begin
                        ascii_tx_byte_r  <= to_hex_ascii({3'b000, pin_sel[4]});
                        ascii_tx_start_r <= 1'b1;
                        ascii_state      <= 3'd3;
                    end

                    3'd3: begin
                        ascii_tx_byte_r  <= to_hex_ascii(pin_sel[3:0]);
                        ascii_tx_start_r <= 1'b1;
                        ascii_state      <= 3'd4;
                    end

                    3'd4: begin
                        ascii_tx_byte_r  <= 8'h0D;
                        ascii_tx_start_r <= 1'b1;
                        ascii_state      <= 3'd0;
                    end

                    default: begin
                        ascii_state <= 3'd0;
                    end
                endcase
            end else if (!active || (mode != MODE_ASCII)) begin
                ascii_state <= 3'd0;
            end
        end
    end

    wire pin_id_signal;

    assign pin_id_signal = (mode == MODE_ASCII) ? ascii_signal :
                           (mode == MODE_PULSE) ? pulse_signal :
                           clock_signal;

    assign uo_override_en = (active && sel_uo)  ? (8'h01 << sel_bit) : 8'h00;
    assign uio_override_oe = (active && sel_uio) ? (8'h01 << sel_bit) : 8'h00;

    assign uo_override  = uo_override_en  & {8{pin_id_signal}};
    assign uio_override = uio_override_oe & {8{pin_id_signal}};

endmodule

`endif /* PIN_DIAG */

`default_nettype wire
