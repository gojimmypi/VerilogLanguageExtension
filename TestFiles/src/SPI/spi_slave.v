/*
 * Copyright (c) 2026 gojimmypi
 * SPDX-License-Identifier: Apache-2.0
 *
 * See ATTRIBUTION.md for third-party sources and credits.
 *
 * file: spi_slave.v
 *
 * SPI register-access slave for Tiny Tapeout experiments.
 *
 * Protocol:
 * - SPI mode 0 (CPOL=0, CPHA=0)
 * - MSB first
 *
 * Modes:
 * - default: SPI register-access slave
 * - SPI_TEST_FIXED: prior transmit-only fixed-byte test path
 *
 * Pin convention: Shared by SPI and JTAG; see shared_spi_jtag_select 
 * - uio[0] = CS_N
 * - uio[1] = MOSI
 * - uio[2] = MISO
 * - uio[3] = SCK
 *
 * Command byte:
 * - bit7    = 1 for read, 0 for write
 * - bits2:0 = register address 0..7   (default)
 * - bits3:0 = register address 0..15  (BIG16_SPI_REG defined)
 * - bits6:0 = register address 0..127 (MAX_SPI_REG defined)
 * - other bits are ignored, depending on default, BIG16_SPI_REG, MAX_SPI_REG
 *
 * Read transaction:
 * - byte 0: command 8'h80 | addr
 * - byte 1: dummy byte clocks out register value
 *
 * Write transaction:
 * - byte 0: command addr
 * - byte 1: data byte written to the selected writable register
 *
 * Register map matches the UART Rn command:
 * - 0 reg_ctrl
 * - 1 reg_src
 * - 2 reg_div
 * - 3 reg_mode
 * - 4 reg_oscen
 * - 5 reg_status, read-only
 *     bit0 trng_enable
 *     bit1 sample_tick
 *     bit2 any oscillator enabled
 *     bit3 health_valid       when TRNG_HEALTH_STATUS is enabled
 *     bit4 activity_seen      when TRNG_HEALTH_STATUS is enabled
 *     bit5 repetition_fail    when TRNG_HEALTH_STATUS is enabled
 *     bit6 stuck_fail         when TRNG_HEALTH_STATUS is enabled
 *     bit7 health_fail        when TRNG_HEALTH_STATUS is enabled
 * - 6 reg_rawlo, read-only
 * - 7 reg_rawhi, read-only
 */

`default_nettype none

`ifdef SIM_JTAG_CORE_TB
    `timescale 1ns / 1ps
`endif

module tt_spi_slave
(
    input  wire       clk,
    input  wire       rst_n,

    input  wire       spi_sck,
    input  wire       spi_cs_n,
    input  wire       spi_mosi,

    output reg        spi_miso,

    output reg        reg_wr_en,
    output reg  [`SPI_ADDR_MSB:0] reg_addr,
    output reg  [7:0] reg_wdata,
    input  wire [7:0] reg_rdata
);

`ifdef SPI_TEST_FIXED

    /* Prior transmit-only SPI test path. */
`ifdef SPI_TEST_BYTE
    /* see project.v for how to set this value; default is 0x42 */
    localparam [7:0] SPI_TEST_BYTE_VAL = `SPI_TEST_BYTE;
`else
    localparam [7:0] SPI_TEST_BYTE_VAL = 8'h42;
`endif

    localparam SPI_IDLE_MISO = 1'b1;

    reg [2:0] spi_sck_sync;
    reg [2:0] spi_cs_sync;
    reg [7:0] spi_tx_shift;

    wire spi_sck_fall;
    wire spi_cs_start;
    wire spi_cs_active;

    /* Keep currently-unused inputs referenced in fixed test mode. */
    wire unused_ok;
    assign unused_ok = &{1'b0, spi_mosi, reg_rdata};

    assign spi_sck_fall  = spi_sck_sync[2:1] == 2'b10;
    assign spi_cs_start  = spi_cs_sync[2:1] == 2'b10;
    assign spi_cs_active = !spi_cs_sync[2];

    always @(posedge clk) begin
        if (!rst_n) begin
            spi_sck_sync <= 3'b000;
            spi_cs_sync  <= 3'b111;
            spi_tx_shift <= SPI_TEST_BYTE_VAL;
            spi_miso     <= SPI_IDLE_MISO;
            reg_wr_en    <= 1'b0;
            reg_addr     <= {`SPI_ADDR_WIDTH{1'b0}};
            reg_wdata    <= 8'h00;
        end else begin
            spi_sck_sync <= {spi_sck_sync[1:0], spi_sck};
            spi_cs_sync  <= {spi_cs_sync[1:0], spi_cs_n};
            reg_wr_en    <= 1'b0;
            reg_addr     <= {`SPI_ADDR_WIDTH{1'b0}};
            reg_wdata    <= 8'h00;

            /* SPI slave, mode 0 (CPOL=0, CPHA=0), MSB-first */
            /*
             * CS_N falls  : preload first MISO bit
             * SCK rises   : ESP32 samples valid bit
             * SCK falls   : TT prepares next bit
             */
            if (spi_cs_start) begin
                /*
                 * Preload shift register so bit6 is ready after
                 * the first falling edge.
                 */
                spi_tx_shift <= {SPI_TEST_BYTE_VAL[6:0], 1'b0};

                /*
                 * Present bit7 immediately so the master samples
                 * valid data on the first rising edge.
                 */
                spi_miso <= SPI_TEST_BYTE_VAL[7];
            end else if (spi_cs_active && spi_sck_fall) begin
                /* Present next bit. */
                spi_miso <= spi_tx_shift[7];

                /* Shift for following bit. */
                spi_tx_shift <= {spi_tx_shift[6:0], 1'b0};
            end else if (!spi_cs_active) begin
                spi_miso <= SPI_IDLE_MISO;
            end
        end
    end

`else

    /*
     * SPI register-access protocol:
     *
     * Read:
     * - byte 0: 8'h80 | addr
     * - byte 1: dummy byte clocks out register value
     *
     * Write:
     * - byte 0: addr
     * - byte 1: data byte written to the selected writable register
     */

    localparam SPI_IDLE_MISO = 1'b1;

    localparam [1:0] ST_CMD  = 2'd0;
    localparam [1:0] ST_DATA = 2'd1;

    reg [2:0] spi_sck_sync;
    reg [2:0] spi_cs_sync;

    reg [7:0] rx_shift;
    reg [7:0] tx_shift;

    reg [2:0] bit_count;
    reg [1:0] state;
    reg       cmd_read;
    reg       load_read_data;

    wire spi_sck_rise;
    wire spi_sck_fall;
    wire spi_cs_start;
    wire spi_cs_active;

    wire [7:0] rx_next;
    wire       rx_shift_msb_drop; /* introduced only for clean linting; functionally the same as rx_next[6:0] */
    wire byte_done;

    assign spi_sck_rise  = spi_sck_sync[2:1] == 2'b01;
    assign spi_sck_fall  = spi_sck_sync[2:1] == 2'b10;
    assign spi_cs_start  = spi_cs_sync[2:1] == 2'b10;
    assign spi_cs_active = !spi_cs_sync[2];

    assign rx_shift_msb_drop = rx_shift[7];
    assign rx_next       = {rx_shift[6:0], spi_mosi} |
                           {8{rx_shift_msb_drop & 1'b0}};
    assign byte_done     = bit_count == 3'd7;

    always @(posedge clk) begin
        if (!rst_n) begin
            spi_sck_sync   <= 3'b000;
            spi_cs_sync    <= 3'b111;
            rx_shift       <= 8'h00;
            tx_shift       <= 8'h00;
            bit_count      <= 3'd0;
            state          <= ST_CMD;
            cmd_read       <= 1'b0;
            load_read_data <= 1'b0;
            spi_miso       <= SPI_IDLE_MISO;
            reg_wr_en      <= 1'b0;
            reg_addr       <= {`SPI_ADDR_WIDTH{1'b0}};
            reg_wdata      <= 8'h00;
        end else begin
            spi_sck_sync <= {spi_sck_sync[1:0], spi_sck};
            spi_cs_sync  <= {spi_cs_sync[1:0], spi_cs_n};
            reg_wr_en    <= 1'b0;

            if (spi_cs_start) begin
                rx_shift       <= 8'h00;
                tx_shift       <= 8'h00;
                bit_count      <= 3'd0;
                state          <= ST_CMD;
                cmd_read       <= 1'b0;
                load_read_data <= 1'b0;
                spi_miso       <= 1'b0;
            end else if (!spi_cs_active) begin
                spi_miso <= SPI_IDLE_MISO;

            end else begin
                if (spi_sck_rise) begin
                    rx_shift <= rx_next;

                    if (byte_done) begin
                        bit_count <= 3'd0;

                        case (state)
                            ST_CMD: begin
                                /* rx_next[7] == 0 write; 1 read */
                                reg_addr <= rx_next[`SPI_ADDR_MSB:0];
                                cmd_read <= rx_next[7];
                                state    <= ST_DATA;

                                if (rx_next[7]) begin
                                    load_read_data <= 1'b1;
                                end else begin
                                    tx_shift <= 8'h00;
                                    spi_miso <= 1'b0;
                                end
                            end /* ST_CMD */

                            ST_DATA: begin
                                if (!cmd_read) begin
                                    reg_wdata <= rx_next;
                                    reg_wr_en <= 1'b1;
                                end

                                state <= ST_DATA;
                            end /* ST_DATA */

                            default: begin
                                state <= ST_CMD;
                            end /* default state */

                        endcase /* state */

                    end else begin
                        /* ! byte_done */
                        bit_count <= bit_count + 1'b1;
                    end
                end /* spi_sck_rise */

                if (spi_sck_fall) begin
                    if (load_read_data) begin
                        spi_miso       <= reg_rdata[7];
                        tx_shift       <= {reg_rdata[6:0], 1'b0};
                        load_read_data <= 1'b0;
                    end else begin
                        spi_miso <= tx_shift[7];
                        tx_shift <= {tx_shift[6:0], 1'b0};
                    end
                end /* spi_sck_fall */
            end /* else spi_cs_active and !spi_cs_start */
        end /* ! rst_n */
    end /* always */

`endif

endmodule /* tt_spi_slave */

`default_nettype wire
