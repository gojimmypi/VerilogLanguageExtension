/*
 * Copyright (c) 2026 gojimmypi
 * SPDX-License-Identifier: Apache-2.0
 *
 * See ATTRIBUTION.md for third-party sources and credits.
 *
 * Thanks Julia Desmazes, see:
 *
 * https://essenceia.github.io/projects/two_weeks_until_tapeout/#jtag-tap-design
 *
 * file: jtag_core.v
 *
 * Minimal JTAG TAP for project debug access.
 *
 * Supported instructions:
 *     4'h1 IDCODE     32-bit read-only IDCODE
 *     4'h2 REG_ADDR   decoded register address update (variable length, see project_config.v)
 *     4'h3 REG_READ   8-bit register read
 *     4'h4 REG_WRITE  8-bit register write
 *     4'hF BYPASS     1-bit bypass register
 *
 * Shift order is LSB first, as expected for JTAG scan registers.
 */
`default_nettype none

`ifdef SIM_JTAG_CORE_TB
    `timescale 1ns / 1ps
`endif

module jtag_core #(
    /* ID code TTJ1 */
    parameter [31:0] IDCODE_VALUE = 32'h54544A31
) (
    input wire        clk,
    input wire        rst_n,
    input wire        ena,

    input wire        tck_i,
    input wire        tms_i,
    input wire        tdi_i,
    output wire       tdo_o,

    output reg [`JTAG_ADDR_MSB:0]  reg_addr_o,
    output reg        reg_wr_o,
    output reg [7:0]  reg_wdata_o,
    input wire [7:0]  reg_rdata_i
);

localparam [3:0] IR_IDCODE    = 4'h1;
localparam [3:0] IR_REG_ADDR  = 4'h2;
localparam [3:0] IR_REG_READ  = 4'h3;
localparam [3:0] IR_REG_WRITE = 4'h4;
localparam [3:0] IR_BYPASS    = 4'hF;

localparam [3:0] TAP_RESET      = 4'd0;
localparam [3:0] TAP_IDLE       = 4'd1;
localparam [3:0] TAP_DR_SELECT  = 4'd2;
localparam [3:0] TAP_DR_CAPTURE = 4'd3;
localparam [3:0] TAP_DR_SHIFT   = 4'd4;
localparam [3:0] TAP_DR_EXIT_1  = 4'd5;
localparam [3:0] TAP_DR_PAUSE   = 4'd6;
localparam [3:0] TAP_DR_EXIT_2  = 4'd7;
localparam [3:0] TAP_DR_UPDATE  = 4'd8;
localparam [3:0] TAP_IR_SELECT  = 4'd9;
localparam [3:0] TAP_IR_CAPTURE = 4'd10;
localparam [3:0] TAP_IR_SHIFT   = 4'd11;
localparam [3:0] TAP_IR_EXIT_1  = 4'd12;
localparam [3:0] TAP_IR_PAUSE   = 4'd13;
localparam [3:0] TAP_IR_EXIT_2  = 4'd14;
localparam [3:0] TAP_IR_UPDATE  = 4'd15;

reg [3:0] tap_state_q;
reg [3:0] ir_shift_q;
reg [3:0] ir_q;
reg [31:0] dr_shift_q;
reg bypass_q;
reg tdo_q;
reg tck_meta_q;
reg tck_sync_q;
reg tck_prev_q;
reg tms_meta_q;
reg tms_sync_q;
reg tdi_meta_q;
reg tdi_sync_q;

wire tck_rise;
wire tck_fall;

assign tdo_o = tdo_q;
assign tck_rise = tck_sync_q & ~tck_prev_q;
assign tck_fall = ~tck_sync_q & tck_prev_q;

always @(posedge clk) begin
    if (!rst_n) begin
        tck_meta_q <= 1'b0;
        tck_sync_q <= 1'b0;
        tck_prev_q <= 1'b0;
        tms_meta_q <= 1'b1;
        tms_sync_q <= 1'b1;
        tdi_meta_q <= 1'b0;
        tdi_sync_q <= 1'b0;
    end else begin
        tck_meta_q <= tck_i;
        tck_sync_q <= tck_meta_q;
        tck_prev_q <= tck_sync_q;
        tms_meta_q <= tms_i;
        tms_sync_q <= tms_meta_q;
        tdi_meta_q <= tdi_i;
        tdi_sync_q <= tdi_meta_q;
    end
end

always @(posedge clk) begin
    if (!rst_n) begin
        tap_state_q <= TAP_RESET;
    end else if (!ena) begin
        tap_state_q <= TAP_RESET;
    end else if (tck_rise) begin
        case (tap_state_q)
            TAP_RESET:      tap_state_q <= tms_sync_q ? TAP_RESET     : TAP_IDLE;
            TAP_IDLE:       tap_state_q <= tms_sync_q ? TAP_DR_SELECT : TAP_IDLE;
            TAP_DR_SELECT:  tap_state_q <= tms_sync_q ? TAP_IR_SELECT : TAP_DR_CAPTURE;
            TAP_DR_CAPTURE: tap_state_q <= tms_sync_q ? TAP_DR_EXIT_1 : TAP_DR_SHIFT;
            TAP_DR_SHIFT:   tap_state_q <= tms_sync_q ? TAP_DR_EXIT_1 : TAP_DR_SHIFT;
            TAP_DR_EXIT_1:  tap_state_q <= tms_sync_q ? TAP_DR_UPDATE : TAP_DR_PAUSE;
            TAP_DR_PAUSE:   tap_state_q <= tms_sync_q ? TAP_DR_EXIT_2 : TAP_DR_PAUSE;
            TAP_DR_EXIT_2:  tap_state_q <= tms_sync_q ? TAP_DR_UPDATE : TAP_DR_SHIFT;
            TAP_DR_UPDATE:  tap_state_q <= tms_sync_q ? TAP_DR_SELECT : TAP_IDLE;
            TAP_IR_SELECT:  tap_state_q <= tms_sync_q ? TAP_RESET     : TAP_IR_CAPTURE;
            TAP_IR_CAPTURE: tap_state_q <= tms_sync_q ? TAP_IR_EXIT_1 : TAP_IR_SHIFT;
            TAP_IR_SHIFT:   tap_state_q <= tms_sync_q ? TAP_IR_EXIT_1 : TAP_IR_SHIFT;
            TAP_IR_EXIT_1:  tap_state_q <= tms_sync_q ? TAP_IR_UPDATE : TAP_IR_PAUSE;
            TAP_IR_PAUSE:   tap_state_q <= tms_sync_q ? TAP_IR_EXIT_2 : TAP_IR_PAUSE;
            TAP_IR_EXIT_2:  tap_state_q <= tms_sync_q ? TAP_IR_UPDATE : TAP_IR_SHIFT;
            TAP_IR_UPDATE:  tap_state_q <= tms_sync_q ? TAP_DR_SELECT : TAP_IDLE;
            default:        tap_state_q <= TAP_RESET;
        endcase
    end
end

always @(posedge clk) begin
    if (!rst_n) begin
        ir_shift_q <= IR_IDCODE;
        ir_q <= IR_IDCODE;
    end else if (!ena) begin
        ir_shift_q <= IR_IDCODE;
        ir_q <= IR_IDCODE;
    end else if (tck_rise) begin
        if (tap_state_q == TAP_IR_CAPTURE) begin
            ir_shift_q <= 4'b0001;
        end else if (tap_state_q == TAP_IR_SHIFT) begin
            ir_shift_q <= {tdi_sync_q, ir_shift_q[3:1]};
        end else if (tap_state_q == TAP_IR_UPDATE) begin
            ir_q <= ir_shift_q;
        end
    end
end

always @(posedge clk) begin
    if (!rst_n) begin
        dr_shift_q <= IDCODE_VALUE;
        bypass_q <= 1'b0;
        reg_addr_o <=  {`JTAG_ADDR_WIDTH{1'b0}};
        reg_wr_o <= 1'b0;
        reg_wdata_o <= 8'h00;
    end else if (!ena) begin
        dr_shift_q <= IDCODE_VALUE;
        bypass_q <= 1'b0;
        reg_addr_o <= {`JTAG_ADDR_WIDTH{1'b0}};
        reg_wr_o <= 1'b0;
        reg_wdata_o <= 8'h00;
    end else begin
        reg_wr_o <= 1'b0;

        if (tck_rise) begin
            if (tap_state_q == TAP_DR_CAPTURE) begin
                case (ir_q)
                    IR_IDCODE: begin
                        dr_shift_q <= IDCODE_VALUE;
                    end

                    IR_REG_ADDR: begin
                        // dr_shift_q <= {24'h000000, reg_addr_o};
                        dr_shift_q <= {{(32 - `JTAG_ADDR_WIDTH){1'b0}}, reg_addr_o};
                    end

                    IR_REG_READ: begin
                        dr_shift_q <= {24'h000000, reg_rdata_i};
                    end

                    IR_REG_WRITE: begin
                        dr_shift_q <= 32'h00000000;
                    end

                    default: begin
                        dr_shift_q <= 32'h00000000;
                        bypass_q <= 1'b0;
                    end
                endcase
            end else if (tap_state_q == TAP_DR_SHIFT) begin
                case (ir_q)
                    IR_IDCODE: begin
                        dr_shift_q <= {tdi_sync_q, dr_shift_q[31:1]};
                    end

                    IR_REG_ADDR,
                    IR_REG_READ,
                    IR_REG_WRITE: begin
                        dr_shift_q <= {tdi_sync_q, dr_shift_q[31:1]};
                    end

                    default: begin
                        bypass_q <= tdi_sync_q;
                    end
                endcase
            end else if (tap_state_q == TAP_DR_UPDATE) begin
                case (ir_q)
                    IR_REG_ADDR: begin
                        /* Variable length registers */
                        reg_addr_o <= dr_shift_q[`JTAG_ADDR_MSB:0];
                    end

                    IR_REG_WRITE: begin
                        /* Fixed length data */
                        reg_wdata_o <= dr_shift_q[7:0];
                        reg_wr_o <= 1'b1;
                    end

                    default: begin
                        reg_wr_o <= 1'b0;
                    end
                endcase
            end
        end
    end
end

always @(posedge clk) begin
    if (!rst_n) begin
        tdo_q <= 1'b0;
    end else if (!ena) begin
        tdo_q <= 1'b0;
    end else if (tck_fall) begin
        if (tap_state_q == TAP_IR_SHIFT) begin
            tdo_q <= ir_shift_q[0];
        end else if (tap_state_q == TAP_DR_SHIFT) begin
            if (ir_q == IR_BYPASS) begin
                tdo_q <= bypass_q;
            end else begin
                tdo_q <= dr_shift_q[0];
            end
        end else begin
            tdo_q <= 1'b0;
        end
    end
end

endmodule

`default_nettype wire
