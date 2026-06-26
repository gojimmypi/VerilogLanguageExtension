/*
 * Copyright (c) 2026 gojimmypi
 * SPDX-License-Identifier: Apache-2.0
 *
 * See ATTRIBUTION.md for third-party sources and credits.
 *
 * file: trng_cfg_ascii_core.v
 *
 * TODO: rename trng_cfg_reg_core.v with SPI functionality
 *
 * ASCII command parser and register front-end for the TRNG experiment.
 *
 * Purpose:
 * - Receives decoded UART bytes from uart_rx_min.
 * - Interprets a very small ASCII command set.
 * - Updates configuration registers or reads them back.
 * - Generates ASCII replies using uart_tx_min.
 *
 * High-level command format:
 * - Single-nibble write commands: Ex, Sx, Ux, Vx, Wx followed by CR
 * - Two-nibble write commands: Dxxy, Mxy, Oxy followed by CR
 * - Register reads: Rx followed by CR, where x is 0..7, or 0..F when BIG16_SPI_REG is enabled
 * - Binary raw stream: Bxy followed by CR, where xy is 01..FF bytes
 * - Conditioned binary stream: Cxy followed by CR, where xy is 01..FF bytes
 *   when TRNG_CONDITIONED_STREAM is enabled
 * - Version query: V followed by CR
 *
 * Example transactions:
 * - E1<CR>     : set enable bit
 * - D10<CR>    : set divider register to 0x10
 * - R5<CR>     : read status/health register, replies R5=hh<CR>
 * - R6<CR>     : read register 6, replies R6=hh<CR>
 * - Bxy<CR>    : stream xy raw bytes, waiting for a fresh TRNG sample before each byte.
 * - Cxy<CR>    : Cxy: stream xy conditioned bytes, waiting for a fresh TRNG sample before each byte.
 * - U3<CR>     : select 921600 UART baud after OK<CR> completes.
 * - V<CR>      : replies Version 1.0.5 6/21/2026<CR>
 * - RD<CR>     : Replies with Build Target ID. 85 == ULX3S, 42 == target GF180
 *
 * Reply format:
 * - Successful write: OK<CR>
 * - Successful read : Rn=HH<CR>
 * - Version query   : Version 1.0.5 6/21/2026<CR>
 * - Parse/error     : ?<CR>
 */
`default_nettype none

`ifdef SIM_JTAG_CORE_TB
    `timescale 1ns / 1ps
`endif

module trng_cfg_ascii_core
(
    input  wire       clk,
    input  wire       rst_n,

    input  wire [7:0] rx_byte,
    input  wire       rx_valid,

    output reg  [7:0] tx_byte,
    output reg        tx_start,
    input  wire       tx_busy,

`ifdef ADJUSTABLE_BAUD_ENABLED
    output reg  [1:0] uart_baud_sel,
`endif 

    output reg  [7:0] reg_ctrl,
    output reg  [7:0] reg_src,
    output reg  [7:0] reg_div,
    output reg  [7:0] reg_mode,
    output reg  [7:0] reg_oscen,

    input  wire [7:0] reg_status,
    input  wire [7:0] reg_rawlo,
    input  wire [7:0] reg_rawhi,

`ifdef BIG16_SPI_REG
    input  wire [7:0] ui_in,
    input  wire [7:0] uo_out,
    input  wire [7:0] uio_in,
    input  wire [7:0] uio_out,
    input  wire [7:0] uio_oe,
`endif

`ifdef TRNG_BINARY_STREAM
    input  wire [7:0] stream_sample_count,
`endif

`ifdef TRNG_CONDITIONED_STREAM
    `ifdef TRNG_CONDITIONED_STREAM_64_XOR
    input  wire [7:0] reg_cond0,
    input  wire [7:0] reg_cond1,
    input  wire [7:0] reg_cond2,
    input  wire [7:0] reg_cond3,
    input  wire [7:0] reg_cond4,
    input  wire [7:0] reg_cond5,
    input  wire [7:0] reg_cond6,
    input  wire [7:0] reg_cond7,
    `else
    input  wire [7:0] reg_condlo,
    input  wire [7:0] reg_condhi,
    `endif /* !TRNG_CONDITIONED_STREAM_64_XOR */
`endif /* TRNG_CONDITIONED_STREAM */

    input  wire       spi_reg_wr_en,
    input  wire [`SPI_ADDR_MSB:0] spi_reg_addr,
    input  wire [7:0] spi_reg_wdata,

`ifdef JTAG_ENABLED
    output reg  [7:0] spi_reg_rdata
`else
    output wire [7:0] spi_reg_rdata
`endif
);

    /*
     * Parser / reply state machine.
     * Separate states are used for command collection and for multi-character
     * response generation so the logic can serialize through one UART TX path.
     */
    localparam [4:0] ST_IDLE       = 5'd0;
    localparam [4:0] ST_ARG1       = 5'd1;
    localparam [4:0] ST_ARG2       = 5'd2;
    localparam [4:0] ST_WAIT_CR    = 5'd3;
    localparam [4:0] ST_Q_R        = 5'd4;
    localparam [4:0] ST_Q_N        = 5'd5;
    localparam [4:0] ST_Q_EQ       = 5'd6;
    localparam [4:0] ST_Q_HI       = 5'd7;
    localparam [4:0] ST_Q_LO       = 5'd8;
    localparam [4:0] ST_Q_CR       = 5'd9;
    localparam [4:0] ST_Q_O        = 5'd10;
    localparam [4:0] ST_Q_K        = 5'd11;
    localparam [4:0] ST_Q_ERR      = 5'd12;
    localparam [4:0] ST_WAIT_SEND  = 5'd13;

    localparam [`SPI_ADDR_MSB:0] SPI_REG_ADDR_CTRL   = 0;
    localparam [`SPI_ADDR_MSB:0] SPI_REG_ADDR_SRC    = 1;
    localparam [`SPI_ADDR_MSB:0] SPI_REG_ADDR_DIV    = 2;
    localparam [`SPI_ADDR_MSB:0] SPI_REG_ADDR_MODE   = 3;
    localparam [`SPI_ADDR_MSB:0] SPI_REG_ADDR_OSCEN  = 4;
    localparam [`SPI_ADDR_MSB:0] SPI_REG_ADDR_STATUS = 5;
    localparam [`SPI_ADDR_MSB:0] SPI_REG_ADDR_RAWLO  = 6;
    localparam [`SPI_ADDR_MSB:0] SPI_REG_ADDR_RAWHI  = 7;


`ifdef BIG16_SPI_REG
    localparam [`SPI_ADDR_MSB:0] SPI_REG_ADDR_UI_IN   = 8;
    localparam [`SPI_ADDR_MSB:0] SPI_REG_ADDR_UO_OUT  = 9;
    localparam [`SPI_ADDR_MSB:0] SPI_REG_ADDR_UIO_IN  = 10;
    localparam [`SPI_ADDR_MSB:0] SPI_REG_ADDR_UIO_OUT = 11;
    localparam [`SPI_ADDR_MSB:0] SPI_REG_ADDR_UIO_OE  = 12;
    localparam [`SPI_ADDR_MSB:0] SPI_REG_ADDR_BUILD   = 13;

    /*
     * Build Target ID:
     *
     * 0x00: unknown, legacy build, or no target macro matched
     *
     * 0x4n: ASIC / PDK-class builds
     * - 41: ASIC SKY130 detected by project wrapper
     * - 42: ASIC GF180 detected by project wrapper
     * - 43: ASIC/PDK detected by project wrapper, unknown PDK flavor
     * - 44: SKY130 PDK fallback only
     * - 45: GF180 PDK fallback only
     * - 46: manual ASIC SKY130 target define
     * - 47: manual ASIC GF180 target define
     *
     * 0x8n: FPGA / non-ASIC builds
     * - 85: ULX3S build using GF180 project PDK context
     * - 86: ULX3S build using SKY130 project PDK context
     * - 87: ULX3S build with no recognized PDK context
     * - 88: explicit FPGA ULX3S 12K target define
     * - 89: explicit FPGA ULX3S 85F target define
     * - 8A: non-ASIC build, assumed TT FPGA demoboard
     * - 8E: generic FPGA target define
     *
     * 0xFn: simulation/test
     * - F0: simulation/test build
     */

    /* 0x4[n] for ASIC */
    `ifdef FOUND_TT_PDK_SKY130
        /* SKY130 PDK Detected for ASIC Build */
        localparam [7:0] BUILD_TARGET_ID = 8'h41;
    `elsif FOUND_TT_PDK_GF180
        /* GF180 PDK Detected for ASIC Build */
        localparam [7:0] BUILD_TARGET_ID = 8'h42;
    `elsif FOUND_TT_PDK
        /* PDH but not SKY130, not GF130 ? Probably not good */
        localparam [7:0] BUILD_TARGET_ID = 8'h43;
    /* Other manual TT ASIC definitions (not used?)  */
    `elsif TT_TARGET_ASIC_SKY130
        localparam [7:0] BUILD_TARGET_ID = 8'h46;
    `elsif TT_TARGET_ASIC_GF180
        localparam [7:0] BUILD_TARGET_ID = 8'h47;

    /* 0x8[n] for FPGA */
    `elsif ULX3S
        `ifdef PDK_TARGET_GF180
            localparam [7:0] BUILD_TARGET_ID = 8'h85;
        `elsif PDK_TARGET_SKY130
            localparam [7:0] BUILD_TARGET_ID = 8'h86;
        `else
            localparam [7:0] BUILD_TARGET_ID = 8'h87;
        `endif
    `elsif TT_TARGET_FPGA_ULX3S_12K
        localparam [7:0] BUILD_TARGET_ID = 8'h88;
    `elsif TT_TARGET_FPGA_ULX3S_85F
        localparam [7:0] BUILD_TARGET_ID = 8'h89;

    /* Other generic FPGA */
    `elsif TT_TARGET_FPGA
        localparam [7:0] BUILD_TARGET_ID = 8'h8E;

    /* We don't really know how to detect TT Demoboard */
    `elsif TT_NON_ASIC_BUILD
        /* We'll assume this is the FPGA Demoboard. See project.v  */
        localparam [7:0] BUILD_TARGET_ID = 8'h8A;


    /* Simulation */
    `elsif TT_TARGET_SIM
        localparam [7:0] BUILD_TARGET_ID = 8'hF0;

    /* From our target_pdk.v include, after none of the above  */
    `elsif PDK_TARGET_SKY130
        localparam [7:0] BUILD_TARGET_ID = 8'h44;
    `elsif PDK_TARGET_GF180
        localparam [7:0] BUILD_TARGET_ID = 8'h45;
    `else
        localparam [7:0] BUILD_TARGET_ID = 8'h00;
    `endif
`endif

`ifdef USE_LONG_STRINGS
    localparam [4:0] ST_Q_STR      = 5'd14;
    localparam [4:0] VERSION_LEN   = `VERSION_STRING_LEN;
`else
    /* Version string not implemented */
`endif

`ifdef TRNG_BINARY_STREAM
    localparam [4:0] ST_Q_BIN      = 5'd15;
    localparam [4:0] ST_Q_BIN_WAIT = 5'd16;
`endif

    reg [4:0] state;
    reg [4:0] next_state_after_send;

    /*
     * cmd records the operation letter.
     * hex1/hex2 hold parsed ASCII hex nibbles until CR arrives.
     */
    reg [7:0] cmd;
    reg [3:0] hex1;
    reg [3:0] hex2;
    reg       need_two_digits;

    localparam UART_READ_ADDR_MSB = (`SPI_ADDR_MSB < 3) ? 3 : `SPI_ADDR_MSB;
    localparam UART_READ_ADDR_WIDTH = UART_READ_ADDR_MSB + 1;
    localparam [7:0] UART_READ_ADDR_LIMIT = (8'd1 << (`SPI_ADDR_MSB + 1));

    reg [UART_READ_ADDR_MSB:0] read_addr;
    wire [`SPI_ADDR_MSB:0] read_reg_addr = read_addr[`SPI_ADDR_MSB:0];

    /* ASCII to_hex_ascii() nibble helper: */
    wire [3:0] read_addr_nib = read_addr[3:0];
    reg [7:0] reply_value;

`ifdef ADJUSTABLE_BAUD_ENABLED
    reg       pending_baud_valid;
    reg [1:0] pending_baud_sel;
`endif 

`ifdef TRNG_BINARY_STREAM
    reg [7:0] stream_count;
    reg       stream_hi;
    reg [7:0] stream_seen_sample_count;
    reg [7:0] stream_byte;
`ifdef TRNG_CONDITIONED_STREAM
    reg       stream_conditioned;
    `ifdef TRNG_CONDITIONED_STREAM_64_XOR
    reg [2:0] stream_cond_index;
    `endif
`endif
`endif

    wire [3:0] decoded_hex;

    /* Optionally make UART command letters case-insensitive. */
    wire [7:0] rx_cmd;
`ifdef CASE_INSENSITIVE
    assign rx_cmd = ((rx_byte >= "a") && (rx_byte <= "z")) ? {rx_byte[7:6], 1'b0, rx_byte[4:0]} : rx_byte;
`else
    assign rx_cmd = rx_byte;
`endif
    assign decoded_hex = hex_value(rx_byte);

    /*
     * One-byte transmit queue.
     * The parser loads queued_tx_byte, and the front-end launches it only when
     * the downstream UART TX is idle.
     */
    reg [7:0] queued_tx_byte;
    reg       queued_tx_valid;

`ifdef USE_LONG_STRINGS
    /*
     * Version reply support.
     * str_index walks through version_char() one byte at a time.
     */
    reg [4:0] str_index;
`else
    /* no long strings */
`endif

    function is_hex;
        input [7:0] c;
        begin
            if ((c >= "0") && (c <= "9")) begin
                is_hex = 1'b1;
            end else if ((c >= "A") && (c <= "F")) begin
                is_hex = 1'b1;
`ifdef CASE_INSENSITIVE
            end else if ((c >= "a") && (c <= "f")) begin
                is_hex = 1'b1;
`endif
`ifdef CASE_INSENSITIVE_ALT
            end else if ((c >= "a") && (c <= "f")) begin
                is_hex = 1'b1;
`endif
            end else begin
                is_hex = 1'b0;
            end
        end
    endfunction

    function [3:0] hex_value;
        input [7:0] c;
        begin
            if ((c >= "0") && (c <= "9")) begin
                /* hex_value = c - "0"; 2 each 8-bit literals to avoid Verilog treating the result as a full byte instead of a nibble. */
                // hex_value = (c - 8'd48) & 8'h0F;  // "0" = 48
                hex_value = c[3:0];
            end else if ((c >= "A") && (c <= "F")) begin
                // hex_value = c - "A" + 4'd10;
                // hex_value = (c - 8'd65 + 4'd10) & 8'h0F;  // "A" = 65
                hex_value = c[3:0] + 4'd9;
`ifdef CASE_INSENSITIVE
            end else if ((c >= "a") && (c <= "f")) begin
                hex_value = c[3:0] + 4'd9;
`endif
`ifdef CASE_INSENSITIVE_ALT
            end else if ((c >= "a") && (c <= "f")) begin
                hex_value = c[3:0] + 4'd9;
`endif
            end else begin
                hex_value = 4'h0;
            end
        end
    endfunction

    /*
     * Address map used by the Rn read command.
     * 0..4 are writable configuration registers.
     * 5..7 are read-only status/data registers coming back from the TRNG side.
     * With TRNG_HEALTH_STATUS enabled, R5 bits 7..3 are health flags.
     */
    function [7:0] read_reg;
        input [`SPI_ADDR_MSB:0] addr;

        begin
            case (addr)
                /* Standard minimal REGS 0..7 */
                SPI_REG_ADDR_CTRL:   read_reg = reg_ctrl;
                SPI_REG_ADDR_SRC:    read_reg = reg_src;
                SPI_REG_ADDR_DIV:    read_reg = reg_div;
                SPI_REG_ADDR_MODE:   read_reg = reg_mode;
                SPI_REG_ADDR_OSCEN:  read_reg = reg_oscen;
                SPI_REG_ADDR_STATUS: read_reg = reg_status;
                SPI_REG_ADDR_RAWLO:  read_reg = reg_rawlo;
                SPI_REG_ADDR_RAWHI:  read_reg = reg_rawhi;

`ifdef BIG16_SPI_REG
                /* REGS 8..15 */
                SPI_REG_ADDR_UI_IN:   read_reg = ui_in;
                SPI_REG_ADDR_UO_OUT:  read_reg = uo_out;
                SPI_REG_ADDR_UIO_IN:  read_reg = uio_in;
                SPI_REG_ADDR_UIO_OUT: read_reg = uio_out;
                SPI_REG_ADDR_UIO_OE:  read_reg = uio_oe;
                SPI_REG_ADDR_BUILD:   read_reg = BUILD_TARGET_ID;
                /* 14 unused */
                /* 15 unused */
`endif

                default:              read_reg = 8'h00;
            endcase
        end
    endfunction

`ifdef TRNG_CONDITIONED_STREAM
`ifdef TRNG_CONDITIONED_STREAM_64_XOR
    function [7:0] read_cond_byte;
        input [2:0] index;
        begin
            case (index)
                3'd0: read_cond_byte = reg_cond0;
                3'd1: read_cond_byte = reg_cond1;
                3'd2: read_cond_byte = reg_cond2;
                3'd3: read_cond_byte = reg_cond3;
                3'd4: read_cond_byte = reg_cond4;
                3'd5: read_cond_byte = reg_cond5;
                3'd6: read_cond_byte = reg_cond6;
                3'd7: read_cond_byte = reg_cond7;
                default: read_cond_byte = reg_cond0;
            endcase
        end
    endfunction
`endif /* TRNG_CONDITIONED_STREAM_64_XOR */
`endif

`ifdef JTAG_ENABLED
    always @(*) begin
        case (spi_reg_addr)
            SPI_REG_ADDR_CTRL:   spi_reg_rdata = reg_ctrl;
            SPI_REG_ADDR_SRC:    spi_reg_rdata = reg_src;
            SPI_REG_ADDR_DIV:    spi_reg_rdata = reg_div;
            SPI_REG_ADDR_MODE:   spi_reg_rdata = reg_mode;
            SPI_REG_ADDR_OSCEN:  spi_reg_rdata = reg_oscen;
            SPI_REG_ADDR_STATUS: spi_reg_rdata = reg_status;
            SPI_REG_ADDR_RAWLO:  spi_reg_rdata = reg_rawlo;
            SPI_REG_ADDR_RAWHI:  spi_reg_rdata = reg_rawhi;

        `ifdef BIG16_SPI_REG
            SPI_REG_ADDR_UI_IN:   spi_reg_rdata = ui_in;
            SPI_REG_ADDR_UO_OUT:  spi_reg_rdata = uo_out;
            SPI_REG_ADDR_UIO_IN:  spi_reg_rdata = uio_in;
            SPI_REG_ADDR_UIO_OUT: spi_reg_rdata = uio_out;
            SPI_REG_ADDR_UIO_OE:  spi_reg_rdata = uio_oe;
            SPI_REG_ADDR_BUILD:   spi_reg_rdata = BUILD_TARGET_ID;
        `endif

            default:             spi_reg_rdata = 8'h00;
        endcase
    end
`else
    assign spi_reg_rdata = read_reg(spi_reg_addr);
`endif

    /* Convert a nibble to ASCII hex for readback replies. */
    function [7:0] to_hex_ascii;
        input [3:0] nib;
        begin
            if (nib < 4'd10) begin
                //           =  8'd48  + nib;           // '0' + nib
                to_hex_ascii = {4'b0011, nib};          // '0'..'9'
            end else begin
                //           =  8'd55  + nib;           // 'A' - 10 + nib  (65 - 10 = 55)
                to_hex_ascii = {4'b0011, nib} + 8'd7;   // 'A'..'F'
            end
        end
    endfunction

`ifdef USE_LONG_STRINGS
    /*
     * Version reply ROM.
     * VERSION_STRING and VERSION_STRING_LEN are defined in project_config.v.
     *
     * Verilog strings are packed with the first character in the most
     * significant byte, so idx 0 selects the top byte.
     */
    localparam [(`VERSION_STRING_LEN * 8) - 1:0] VERSION_STRING_ROM = `VERSION_STRING;
    localparam [7:0] VERSION_LAST_BIT_OFFSET = ((`VERSION_STRING_LEN - 1) * 8);

    function [7:0] version_char;
        input [4:0] idx;
        reg [7:0] bit_offset;
        begin
            bit_offset = VERSION_LAST_BIT_OFFSET - {idx, 3'b000};
            version_char = VERSION_STRING_ROM[bit_offset +: 8];
        end
    endfunction
`else
    /* no long strings */
`endif

    /*
     * Write decoded values into specific register fields.
     * Some commands write only selected bits while others write a full byte.
     */
    task do_write;
        input [7:0] c;
        input [7:0] value;
        begin
            case (c)
                "E": reg_ctrl[0]   <= value[0];
                "S": reg_src[1:0]  <= value[1:0];
                "D": reg_div       <= value;
                "V": reg_ctrl[1]   <= value[0];
                "W": reg_ctrl[2]   <= value[0];
                "M": reg_mode      <= value;
                "O": reg_oscen     <= value;
`ifdef CASE_INSENSITIVE_ALT
                "e": reg_ctrl[0]   <= value[0];
                "s": reg_src[1:0]  <= value[1:0];
                "d": reg_div       <= value;
                "v": reg_ctrl[1]   <= value[0];
                "w": reg_ctrl[2]   <= value[0];
                "m": reg_mode      <= value;
                "o": reg_oscen     <= value;
`endif
                default: begin end
            endcase
        end
    endtask

    task do_spi_write;
        input [`SPI_ADDR_MSB:0] addr;
        input [7:0] value;
        begin
            case (addr)
                SPI_REG_ADDR_CTRL:  reg_ctrl  <= value;
                SPI_REG_ADDR_SRC:   reg_src   <= value;
                SPI_REG_ADDR_DIV:   reg_div   <= value;
                SPI_REG_ADDR_MODE:  reg_mode  <= value;
                SPI_REG_ADDR_OSCEN: reg_oscen <= value;
                default: begin end
            endcase
        end
    endtask

    /* Queue one reply character. */
    task queue_tx;
        input [7:0] c;
        begin
            queued_tx_byte  <= c;
            queued_tx_valid <= 1'b1;
        end
    endtask

`ifdef USE_LONG_STRINGS
    /* Start sending the hard-coded version string. */
    task start_version;
        begin
            str_index <= 5'd0;
            state     <= ST_Q_STR;
        end
    endtask
`else
    /* no long strings */
`endif

    always @(posedge clk) begin
        if (!rst_n) begin
            state                 <= ST_IDLE;
            next_state_after_send <= ST_IDLE;
            cmd                   <= 8'h00;
            hex1                  <= 4'h0;
            hex2                  <= 4'h0;
            need_two_digits       <= 1'b0;
            read_addr             <= {UART_READ_ADDR_WIDTH{1'b0}};
            reply_value           <= 8'h00;
`ifdef ADJUSTABLE_BAUD_ENABLED
            pending_baud_valid    <= 1'b0;
            pending_baud_sel      <= 2'd0;
`endif 

            queued_tx_byte        <= 8'h00;
            queued_tx_valid       <= 1'b0;
            tx_byte               <= 8'h00;
            tx_start              <= 1'b0;

        `ifdef TRNG_BINARY_STREAM
            stream_seen_sample_count <= 8'h00;
            stream_byte              <= 8'h00;
            
            stream_count          <= 8'h00;
            stream_hi             <= 1'b0;
            `ifdef TRNG_CONDITIONED_STREAM
            stream_conditioned    <= 1'b0;
            `ifdef TRNG_CONDITIONED_STREAM_64_XOR
                stream_cond_index     <= 3'd0;
            `endif /* TRNG_CONDITIONED_STREAM_64_XOR */
            `endif /* TRNG_CONDITIONED_STREAM */
        `endif /* TRNG_BINARY_STREAM */

        `ifdef USE_LONG_STRINGS
            str_index             <= 5'd0;
        `else
            /* no long strings */
        `endif

`ifdef ADJUSTABLE_BAUD_ENABLED
            /* Default power-on register values for bring-up. */
            uart_baud_sel         <= 2'd0;
`endif 

            reg_ctrl              <= 8'h00;
            reg_src               <= 8'h00;
            reg_div               <= 8'h10;
            reg_mode              <= 8'h00;
            reg_oscen             <= 8'h01;
        end else begin
            /* tx_start is a one-clock pulse into uart_tx_min. */
            tx_start <= 1'b0;

            if (spi_reg_wr_en) begin
                do_spi_write(spi_reg_addr, spi_reg_wdata);
            end

            /*
             * Launch a queued reply byte only when the UART transmitter is free.
             * This decouples parser sequencing from the transmit bit timing.
             */
            if (queued_tx_valid && !tx_busy) begin
                tx_byte         <= queued_tx_byte;
                tx_start        <= 1'b1;
                queued_tx_valid <= 1'b0;
            end

            case (state)
                ST_IDLE: begin
                    if (rx_valid) begin
                        /* Ignore LF so terminals sending CRLF still work. */
                        if (rx_byte == 8'h0A) begin
                            state <= ST_IDLE;
`ifdef CASE_INSENSITIVE_ALT
                        end else if ((rx_cmd == "E") || (rx_cmd == "e") ||
                                     (rx_cmd == "S") || (rx_cmd == "s") ||
                            `ifdef ADJUSTABLE_BAUD_ENABLED
                                     (rx_cmd == "U") || (rx_cmd == "u") ||
                            `endif 
                                     (rx_cmd == "V") || (rx_cmd == "v") ||
                                     (rx_cmd == "W") || (rx_cmd == "w")) begin
                            cmd             <= rx_cmd;
                            need_two_digits <= 1'b0;
                            state           <= ST_ARG1;
                        end else if ((rx_cmd == "D") || (rx_cmd == "d") ||
                            `ifdef TRNG_BINARY_STREAM
                                     (rx_cmd == "B") || (rx_cmd == "b") ||
                                `ifdef TRNG_CONDITIONED_STREAM
                                     (rx_cmd == "C") || (rx_cmd == "c") ||
                                `endif
                            `endif
                                     (rx_cmd == "M") || (rx_cmd == "m") ||
                                     (rx_cmd == "O") || (rx_cmd == "o")) begin
                            cmd             <= rx_cmd;
                            need_two_digits <= 1'b1;
                            state           <= ST_ARG1;
                        end else if ((rx_cmd == "R") || (rx_cmd == "r")) begin
                            cmd             <= rx_cmd;
                            need_two_digits <= 1'b0;
                            state           <= ST_ARG1;
`else
                        end else if ((rx_cmd == "E") ||
                                     (rx_cmd == "S") ||
                            `ifdef ADJUSTABLE_BAUD_ENABLED
                                     (rx_cmd == "U") ||
                            `endif 
                                     (rx_cmd == "V") ||
                                     (rx_cmd == "W")) begin
                            cmd             <= rx_cmd;
                            need_two_digits <= 1'b0;
                            state           <= ST_ARG1;
                        end else if ((rx_cmd == "D") ||
                            `ifdef TRNG_BINARY_STREAM
                                     (rx_cmd == "B") ||
                                `ifdef TRNG_CONDITIONED_STREAM
                                     (rx_cmd == "C") ||
                                `endif
                            `endif                                     
                                     (rx_cmd == "M") ||
                                     (rx_cmd == "O")) begin
                            cmd             <= rx_cmd;
                            need_two_digits <= 1'b1;
                            state           <= ST_ARG1;
                        end else if (rx_cmd == "R") begin
                            cmd             <= rx_cmd;
                            need_two_digits <= 1'b0;
                            state           <= ST_ARG1;
`endif
                        end else begin
                            state <= ST_Q_ERR;
                        end
                    end
                end

                ST_ARG1: begin
                    if (rx_valid) begin
                        /*
                         * Bare V<CR> is treated as a version query.
                         * Vx<CR> still retains its original single-nibble write
                         * behavior for reg_ctrl[1].
                         */
                        if ((cmd == "V") && (rx_byte == 8'h0A)) begin
                            state <= ST_ARG1;

`ifdef CASE_INSENSITIVE_ALT
                        end else if ((cmd == "v") && (rx_byte == 8'h0A)) begin
                            state <= ST_ARG1;

                        end else if ((cmd == "v") && (rx_byte == 8'h0D)) begin
                            `ifdef USE_LONG_STRINGS
                                start_version();
                            `else
                                state <= ST_Q_ERR;/* */
                            `endif
`endif
                        end else if ((cmd == "V") && (rx_byte == 8'h0D)) begin
                            `ifdef USE_LONG_STRINGS
                                start_version();
                            `else
                                state <= ST_Q_ERR;/* */
                            `endif

                        end else if (is_hex(rx_byte)) begin
                            hex1 <= hex_value(rx_byte);

`ifdef CASE_INSENSITIVE_ALT
                            if ((cmd == "R") || (cmd == "r")) begin
`else
                            if (cmd == "R") begin
`endif
                                /* UART R accepts one hex digit; valid range follows SPI address width. */
                                if ({4'h0, decoded_hex} < UART_READ_ADDR_LIMIT) begin
                                    read_addr <= decoded_hex;
                                    
                                    state <= ST_WAIT_CR;
                                end else begin
                                    state <= ST_Q_ERR;
                                end
                            end else if (need_two_digits) begin
                                state <= ST_ARG2;
                            end else begin
                                state <= ST_WAIT_CR;
                            end
                        end else begin
                            state <= ST_Q_ERR;
                        end
                    end
                end

                ST_ARG2: begin
                    if (rx_valid) begin
                        if (is_hex(rx_byte)) begin
                            hex2  <= hex_value(rx_byte);
                            state <= ST_WAIT_CR;
                        end else begin
                            state <= ST_Q_ERR;
                        end
                    end
                end

                ST_WAIT_CR: begin
                    if (rx_valid) begin
                        /* Again ignore LF so CRLF is accepted. */
                        if (rx_byte == 8'h0A) begin
                            state <= ST_WAIT_CR;
                        end else if (rx_byte == 8'h0D) begin
`ifdef CASE_INSENSITIVE_ALT
                            if ((cmd == "R") || (cmd == "r")) begin
`else
                            if (cmd == "R") begin
`endif
                                reply_value <= read_reg(read_reg_addr);
                                state <= ST_Q_R;

`ifdef TRNG_BINARY_STREAM

    `ifdef TRNG_CONDITIONED_STREAM
        `ifdef CASE_INSENSITIVE_ALT
                            end else if ((cmd == "B") || (cmd == "b") ||
                                         (cmd == "C") || (cmd == "c")) begin
        `else
                            end else if ((cmd == "B") || (cmd == "C")) begin
        `endif /* CASE_INSENSITIVE_ALT */
    `else
        `ifdef CASE_INSENSITIVE_ALT
                            end else if ((cmd == "B") || (cmd == "b")) begin
        `else
                            end else if (cmd == "B") begin
        `endif /* CASE_INSENSITIVE_ALT */
    `endif /* TRNG_CONDITIONED_STREAM */

                                if ({hex1, hex2} != 8'h00) begin
                                    stream_count <= {hex1, hex2};
                                    stream_hi    <= 1'b0;
    `ifdef TRNG_CONDITIONED_STREAM
                                `ifdef TRNG_CONDITIONED_STREAM_64_XOR
                                    stream_cond_index <= 3'd0;
                                `endif                
        `ifdef CASE_INSENSITIVE_ALT
                                    stream_conditioned <= (cmd == "C") || (cmd == "c");
        `else
                                    stream_conditioned <= (cmd == "C");
        `endif /* CASE_INSENSITIVE_ALT */
    `endif /* TRNG_CONDITIONED_STREAM */
                                    stream_seen_sample_count <= stream_sample_count;
                                    state                    <= ST_Q_BIN_WAIT;
                                end else begin
                                    state <= ST_Q_ERR;
                                end
`endif /* TRNG_BINARY_STREAM */

`ifdef ADJUSTABLE_BAUD_ENABLED

    `ifdef CASE_INSENSITIVE_ALT
                            end else if ((cmd == "U") || (cmd == "u")) begin
    `else
                            end else if (cmd == "U") begin
    `endif /* CASE_INSENSITIVE_ALT */
                                if (hex1 < 4'd4) begin
                                    pending_baud_valid <= 1'b1;
                                    pending_baud_sel   <= hex1[1:0];
                                    state              <= ST_Q_O;
                                end else begin
                                    state <= ST_Q_ERR;
                                end
`endif /* ADJUSTABLE_BAUD_ENABLED */

                            end else begin
                                if (need_two_digits) begin
                                    do_write(cmd, {hex1, hex2});
                                end else begin
                                    do_write(cmd, {4'h0, hex1});
                                end
                                state <= ST_Q_O;
                            end
                        end else begin
                            state <= ST_Q_ERR;
                        end
                    end
                end

                /* Read reply is serialized as: R n = H H CR */
                ST_Q_R: begin
                    if (!queued_tx_valid) begin
                        queue_tx("R");
                        next_state_after_send <= ST_Q_N;
                        state <= ST_WAIT_SEND;
                    end
                end

                ST_Q_N: begin
                    if (!queued_tx_valid) begin
                        queue_tx(to_hex_ascii(read_addr_nib));
                        next_state_after_send <= ST_Q_EQ;
                        state <= ST_WAIT_SEND;
                    end
                end

                ST_Q_EQ: begin
                    if (!queued_tx_valid) begin
                        queue_tx("=");
                        next_state_after_send <= ST_Q_HI;
                        state <= ST_WAIT_SEND;
                    end
                end

                ST_Q_HI: begin
                    if (!queued_tx_valid) begin
                        queue_tx(to_hex_ascii(reply_value[7:4]));
                        next_state_after_send <= ST_Q_LO;
                        state <= ST_WAIT_SEND;
                    end
                end

                ST_Q_LO: begin
                    if (!queued_tx_valid) begin
                        queue_tx(to_hex_ascii(reply_value[3:0]));
                        next_state_after_send <= ST_Q_CR;
                        state <= ST_WAIT_SEND;
                    end
                end

                ST_Q_CR: begin
                    if (!queued_tx_valid) begin
                        queue_tx(8'h0D);
                        next_state_after_send <= ST_IDLE;
                        state <= ST_WAIT_SEND;
                    end
                end

                /* Write reply is serialized as: O K CR */
                ST_Q_O: begin
                    if (!queued_tx_valid) begin
                        queue_tx("O");
                        next_state_after_send <= ST_Q_K;
                        state <= ST_WAIT_SEND;
                    end
                end

                ST_Q_K: begin
                    if (!queued_tx_valid) begin
                        queue_tx("K");
                        next_state_after_send <= ST_Q_CR;
                        state <= ST_WAIT_SEND;
                    end
                end

                /* Generic parser error reply. */
                ST_Q_ERR: begin
                    if (!queued_tx_valid) begin
                        queue_tx("?");
                        next_state_after_send <= ST_Q_CR;
                        state <= ST_WAIT_SEND;
                    end
                end

            `ifdef USE_LONG_STRINGS
                /*
                 * Version string sender.
                 * Characters are emitted one at a time through the normal queue
                 * and ST_WAIT_SEND handshake path.
                 */
                ST_Q_STR: begin
                    if (!queued_tx_valid) begin
                        if (str_index < VERSION_LEN) begin
                            queue_tx(version_char(str_index));
                            str_index <= str_index + 1'b1;
                            next_state_after_send <= ST_Q_STR;
                            state <= ST_WAIT_SEND;
                        end else begin
                            queue_tx(8'h0D);
                            next_state_after_send <= ST_IDLE;
                            state <= ST_WAIT_SEND;
                        end
                    end
                end
            `else
                /* no long strings */
            `endif

            /* Optional TRNG Binary Stream feature for ST_Q_BIN state */
            `ifdef TRNG_BINARY_STREAM
                ST_Q_BIN_WAIT: begin
                    if (stream_sample_count != stream_seen_sample_count) begin
                        stream_seen_sample_count <= stream_sample_count;

`ifdef TRNG_CONDITIONED_STREAM
    `ifdef TRNG_CONDITIONED_STREAM_64_XOR
                        if (stream_conditioned) begin
                            stream_byte       <= read_cond_byte(stream_cond_index);
                            stream_cond_index <= stream_cond_index + 3'd1;
                        end else begin
                            if (stream_hi) begin
                                stream_byte <= reg_rawhi;
                                stream_hi <= 1'b0;
                            end else begin
                                stream_byte <= reg_rawlo;
                                stream_hi <= 1'b1;
                            end
                        end
    `else
                        if (stream_hi) begin
                            if (stream_conditioned) begin
                                stream_byte <= reg_condhi;
                            end else begin
                                stream_byte <= reg_rawhi;
                            end
                            stream_hi <= 1'b0;
                        end else begin
                            if (stream_conditioned) begin
                                stream_byte <= reg_condlo;
                            end else begin
                                stream_byte <= reg_rawlo;
                            end
                            stream_hi <= 1'b1;
                        end
    `endif /* ! TRNG_CONDITIONED_STREAM_64_XOR */
`else
                        if (stream_hi) begin
                            stream_byte <= reg_rawhi;
                            stream_hi <= 1'b0;
                        end else begin
                            stream_byte <= reg_rawlo;
                            stream_hi <= 1'b1;
                        end
`endif /* ! TRNG_CONDITIONED_STREAM */

                        state <= ST_Q_BIN;
                    end
                end

                ST_Q_BIN: begin
                    if (!queued_tx_valid && !tx_busy) begin
                        queue_tx(stream_byte);

                        if (stream_count == 8'h01) begin
                            stream_count <= 8'h00;
                            state        <= ST_IDLE;
                        end else begin
                            stream_count <= stream_count - 8'd1;
                            state        <= ST_Q_BIN_WAIT;
                        end
                    end
                end
            `endif /* TRNG_BINARY_STREAM */

                /*
                 * Stay here until the queued byte has been accepted and the UART
                 * transmitter is no longer busy. Then continue with the next
                 * response character.
                 */
                ST_WAIT_SEND: begin
                    if (!queued_tx_valid && !tx_busy) begin

            `ifdef ADJUSTABLE_BAUD_ENABLED
                        if (next_state_after_send == ST_IDLE) begin
                            if (pending_baud_valid) begin
                                uart_baud_sel      <= pending_baud_sel;
                                pending_baud_valid <= 1'b0;
                            end
                        end
            `endif /* ADJUSTABLE_BAUD_ENABLED */

                        state <= next_state_after_send;
                    end
                end

                default: begin
                    state <= ST_IDLE;
                end
            endcase
        end
    end

endmodule /* trng_cfg_ascii_core */

`default_nettype wire
