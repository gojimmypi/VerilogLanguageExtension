/*
 * Copyright (c) 2026 gojimmypi
 * SPDX-License-Identifier: Apache-2.0
 *
 * See ATTRIBUTION.md for third-party sources and credits.
 *
 * file: src/TRNG/trng_lab_core.v
 *
 * Experimental TRNG lab core.
 *
 * This is intended for education and experimentation.
 * It is not a certified cryptographic random number generator.
 *
 * WARNING: There's a bit of hackery in this file to allow the real RO-based TRNG code 
 * to be included only when explicitly enabled, and only in appropriate build environments.
 * Note the project.v settings for TRNG_USE_RO and TRNG_ALLOW_REAL_RO, and the conditional 
 * code below that checks for these defines. (in particular the `ifdef __pnr__`)
 *
 * There's an additional manual identification of PDK in root-level target_pdk.v included by project.v, 
 * which is used to conditionally instantiate the correct standard cells in the RO code.
 *
 * See GDS_logs.zip\runs\wokwi\06-yosys-synthesis\tt_um_gojimmypi_ttsky_UART_FSM_TRNG_Lab.nl.v
 *   and confirm this exists: module trng_ro_inverter_cell(a, y);
 *   with many instantiated sky130_fd_sc_hd__inv_2 cells inside. 
 * If you see this, the real RO code is included. If you see instead a module trng_ro_inverter_cell 
 * with no internal cells, then the real RO code is not included and the trng_ro module is just a passthrough.
 *
 * ABC: Error: The network is combinational.
 *   This is GOOD for a ring oscillator design.
 *
 * gdslogs/runs/wokwi/58-klayout-streamout/klayout-streamout.log
 *   Should contain the text:
 *   [INFO] All LEF cells have matching GDS cells.
 */
`default_nettype none

`ifdef SIM_JTAG_CORE_TB
    `timescale 1ns / 1ps
`endif

/* this entire file is only for the TRNG lab core, which is an optional alternative to the trng_stub */
`ifdef TRNG_ENABLED

`ifndef LINT_OFF_PINMISSING_POWER_PINS
    `ifdef USE_POWER_PINS
        `define LINT_OFF_PINMISSING_POWER_PINS /* verilator lint_off PINMISSING */
        `define LINT_ON_PINMISSING_POWER_PINS  /* verilator lint_on PINMISSING */
    `else
        `define LINT_OFF_PINMISSING_POWER_PINS /* */
        `define LINT_ON_PINMISSING_POWER_PINS  /* */
    `endif
`endif

/* 
 * --------------------------------------------------------------------------------------------
 * --------------------------------------------------------------------------------------------
 *  TRNG Lab Core - the heart of all things random
 * --------------------------------------------------------------------------------------------
 * --------------------------------------------------------------------------------------------
 */
module trng_lab_core
(
    input  wire       clk,
    input  wire       rst_n,

    input  wire [7:0] reg_ctrl,
    input  wire [7:0] reg_src,
    input  wire [7:0] reg_div,
    input  wire [7:0] reg_mode,
    input  wire [7:0] reg_oscen,

    output reg  [7:0] reg_status,
    output reg  [7:0] reg_rawlo,
    output reg  [7:0] reg_rawhi,

`ifdef TRNG_BINARY_STREAM
    output reg  [7:0] stream_sample_count,
`endif

`ifdef TRNG_CONDITIONED_STREAM
    `ifdef TRNG_CONDITIONED_STREAM_64_XOR
    output wire [7:0] reg_cond0,
    output wire [7:0] reg_cond1,
    output wire [7:0] reg_cond2,
    output wire [7:0] reg_cond3,
    output wire [7:0] reg_cond4,
    output wire [7:0] reg_cond5,
    output wire [7:0] reg_cond6,
    output wire [7:0] reg_cond7,
    `else
    output wire [7:0] reg_condlo,
    output wire [7:0] reg_condhi,
    `endif
`endif

    output wire       trng_bit
);
/* -------------------------------------------------------------------------------------------- */
    localparam [1:0] SRC_LFSR = 2'b00;
    localparam [1:0] SRC_RO0  = 2'b01;
    localparam [1:0] SRC_ROX  = 2'b10;
    localparam [1:0] SRC_MIX  = 2'b11;

    reg  [8:0] sample_ctr; /* 9 bit register, allow for 8 bit overflow */
    reg  [15:0] lfsr;
    reg  [15:0] sample_shift;

`ifdef TRNG_CONDITIONED_STREAM
    `ifdef TRNG_CONDITIONED_STREAM_64_XOR
    reg  [63:0] stream_mix;
    `elsif TRNG_CONDITIONED_STREAM_CRC
    reg  [15:0] stream_mix;
    `elsif TRNG_CONDITIONED_STREAM_GALOIS_64
    reg  [63:0] stream_mix;
    reg  [7:0]  condlo_mix;
    reg  [7:0]  condhi_mix;
    `elsif TRNG_CONDITIONED_STREAM_GALOIS
    reg  [31:0] stream_mix;
    `else
    reg  [31:0] stream_mix;
    `endif
`endif

    reg         ro0_sample_meta;
    reg         ro0_sample_sync;
    reg         rox_sample_meta;
    reg         rox_sample_sync;
    reg         selected_bit;

    reg         trng_step_d;
    reg         sample_tick_q;
    reg         do_sample_q;

    wire        trng_step;
    wire        trng_step_pulse;

    wire        trng_enable;
    wire        sample_tick;
    wire [1:0]  source_select;

    (* keep, dont_touch *) 
    wire [7:0]  ro_raw;

    wire        ro_xor;
    wire        lfsr_next_bit;

`ifdef TRNG_CONDITIONED_STREAM
    `ifdef TRNG_CONDITIONED_STREAM_CRC
    wire        cond_in_bit;
    wire        feedback;
    `elsif TRNG_CONDITIONED_STREAM_GALOIS_64
    wire        cond_in_bit;
    wire        stream_feedback;
    wire [63:0] galois_mix_next;
    wire [63:0] galois_mix_scrambled;
    wire [7:0]  galois_foldlo;
    wire [7:0]  galois_foldhi;
    wire [7:0]  condlo_mix_next;
    wire [7:0]  condhi_mix_next;
    `elsif TRNG_CONDITIONED_STREAM_GALOIS
    wire        cond_in_bit;
    wire        stream_feedback;
    wire [31:0] galois_mix_next;
    wire [7:0]  ro_sync_mix;
    `else
    wire        stream_feedback;
    `endif
    `ifdef TRNG_CONDITIONED_STREAM_64_XOR
    wire [63:0] stream_scrambled;
    `endif
`endif

    wire        unused_reg_ctrl;
    wire        unused_reg_src;
    wire        unused_reg_mode;
    wire        unused_sample_shift;

    wire        trng_reset;

`ifdef TRNG_HEALTH_STATUS
    /* Health monitor only drives R5[7:3]. Legacy R5[2:0] is preserved. */
    wire [7:3]  health_status;
    reg         health_sample_valid_q;
    reg         health_sample_bit_q;
    reg         health_ro_activity_q;
    reg         health_any_osc_en_q;
`endif

    assign trng_reset = reg_ctrl[2];

    /* reg_ctrl[1], written by V1/V0, is a deterministic single-step request. */
    assign trng_step = reg_ctrl[1];
    assign trng_step_pulse = trng_step && !trng_step_d;

    assign trng_enable = reg_ctrl[0];
    assign source_select = reg_src[1:0];

    assign sample_tick = sample_ctr >= {1'b0, reg_div};

    assign lfsr_next_bit = lfsr[15] ^ lfsr[13] ^ lfsr[12] ^ lfsr[10];

    assign ro_xor = ro_raw[0] ^ ro_raw[1] ^ ro_raw[2] ^ ro_raw[3] ^
                    ro_raw[4] ^ ro_raw[5] ^ ro_raw[6] ^ ro_raw[7];

    assign trng_bit = sample_shift[0];

`ifdef TRNG_CONDITIONED_STREAM
`ifdef TRNG_CONDITIONED_STREAM_64_XOR
    assign stream_feedback =
        stream_mix[63] ^
        stream_mix[62] ^
        stream_mix[60] ^
        stream_mix[59] ^
        selected_bit   ^
        ro0_sample_sync ^
        rox_sample_sync ^
        lfsr[0]        ^
        lfsr[7]        ^
        lfsr[15]       ^
        sample_shift[3] ^
        sample_shift[11];

    assign stream_scrambled =
        stream_mix ^
        {7'h00, stream_mix[63:7]} ^
        {17'h00000, stream_mix[63:17]} ^
        {31'h00000000, stream_mix[63:31]};

    assign reg_cond0 = stream_scrambled[7:0]   ^ stream_scrambled[39:32] ^ stream_scrambled[63:56];
    assign reg_cond1 = stream_scrambled[15:8]  ^ stream_scrambled[47:40] ^ stream_scrambled[31:24];
    assign reg_cond2 = stream_scrambled[23:16] ^ stream_scrambled[55:48] ^ stream_scrambled[7:0];
    assign reg_cond3 = stream_scrambled[31:24] ^ stream_scrambled[63:56] ^ stream_scrambled[15:8];
    assign reg_cond4 = stream_scrambled[39:32] ^ stream_scrambled[7:0]   ^ stream_scrambled[23:16];
    assign reg_cond5 = stream_scrambled[47:40] ^ stream_scrambled[15:8]  ^ stream_scrambled[31:24];
    assign reg_cond6 = stream_scrambled[55:48] ^ stream_scrambled[23:16] ^ stream_scrambled[39:32];
    assign reg_cond7 = stream_scrambled[63:56] ^ stream_scrambled[31:24] ^ stream_scrambled[47:40];

`elsif TRNG_CONDITIONED_STREAM_CRC
    assign cond_in_bit =
        selected_bit ^
        ro0_sample_sync ^
        rox_sample_sync ^
        lfsr[0] ^
        sample_shift[3];

    assign feedback = stream_mix[15] ^ cond_in_bit;

    assign reg_condlo = stream_mix[7:0];
    assign reg_condhi = stream_mix[15:8];

`elsif TRNG_CONDITIONED_STREAM_GALOIS_64
    assign cond_in_bit =
        selected_bit ^
        ro0_sample_sync ^
        rox_sample_sync ^
        ro_raw[0] ^
        ro_raw[2] ^
        ro_raw[5] ^
        ro_raw[7] ^
        lfsr[0] ^
        lfsr[5] ^
        lfsr[9] ^
        lfsr[15] ^
        sample_shift[1] ^
        sample_shift[6] ^
        sample_shift[10] ^
        sample_shift[13];

    assign stream_feedback =
        stream_mix[63] ^
        stream_mix[62] ^
        stream_mix[60] ^
        stream_mix[59] ^
        cond_in_bit;

    /* 64-bit Galois-style conditioner.  The wider state and nonlinear
     * output fold are intended to break the repeated-template structure
     * that showed up in NIST NonOverlappingTemplate runs.  This is still
     * not a cryptographic DRBG. */
    assign galois_mix_next = {
        stream_mix[62] ^ stream_feedback,
        stream_mix[61],
        stream_mix[60] ^ stream_feedback,
        stream_mix[59] ^ stream_feedback,
        stream_mix[58:0],
        stream_feedback
    };

    assign galois_mix_scrambled =
        stream_mix ^
        {stream_mix[18:0], stream_mix[63:19]} ^
        {stream_mix[40:0], stream_mix[63:41]} ^
        {stream_mix[54:0], stream_mix[63:55]};

    assign galois_foldlo =
        galois_mix_scrambled[7:0] ^
        galois_mix_scrambled[23:16] ^
        galois_mix_scrambled[39:32] ^
        galois_mix_scrambled[55:48] ^
        (galois_mix_scrambled[15:8] & galois_mix_scrambled[47:40]) ^
        (galois_mix_scrambled[31:24] | galois_mix_scrambled[63:56]) ^
        ro_raw ^
        sample_shift[7:0] ^
        lfsr[7:0];

    assign galois_foldhi =
        galois_mix_scrambled[15:8] ^
        galois_mix_scrambled[31:24] ^
        galois_mix_scrambled[47:40] ^
        galois_mix_scrambled[63:56] ^
        (galois_mix_scrambled[7:0] & galois_mix_scrambled[39:32]) ^
        (galois_mix_scrambled[23:16] | galois_mix_scrambled[55:48]) ^
        {ro_raw[3:0], ro_raw[7:4]} ^
        sample_shift[15:8] ^
        lfsr[15:8];

    assign condlo_mix_next =
        galois_foldlo ^
        condhi_mix ^
        {condlo_mix[6:0], condlo_mix[7]} ^
        ((condlo_mix & galois_foldhi) ^ (condhi_mix | {7'b0000000, selected_bit}));

    assign condhi_mix_next =
        galois_foldhi ^
        condlo_mix ^
        {condhi_mix[6:0], condhi_mix[7]} ^
        ((condhi_mix & galois_foldlo) ^ (condlo_mix | {7'b0000000, rox_sample_sync}));

    assign reg_condlo = condlo_mix;
    assign reg_condhi = condhi_mix;

`elsif TRNG_CONDITIONED_STREAM_GALOIS
    assign ro_sync_mix = {8{rox_sample_sync}} ^ {7'b0000000, ro0_sample_sync};

    assign cond_in_bit =
        selected_bit ^
        ro0_sample_sync ^
        rox_sample_sync ^
        lfsr[0] ^
        lfsr[5] ^
        lfsr[9] ^
        lfsr[15] ^
        sample_shift[3] ^
        sample_shift[11];

    assign stream_feedback = stream_mix[31] ^ cond_in_bit;

    /* 32-bit Galois-style conditioner using taps matching
     * x^32 + x^22 + x^2 + x + 1. This is still a linear conditioner,
     * not a cryptographic DRBG, but it avoids exposing a tiny 16-bit state. */
    assign galois_mix_next = {
        stream_mix[30],
        stream_mix[29],
        stream_mix[28],
        stream_mix[27],
        stream_mix[26],
        stream_mix[25],
        stream_mix[24],
        stream_mix[23],
        stream_mix[22],
        stream_mix[21] ^ stream_feedback,
        stream_mix[20],
        stream_mix[19],
        stream_mix[18],
        stream_mix[17],
        stream_mix[16],
        stream_mix[15],
        stream_mix[14],
        stream_mix[13],
        stream_mix[12],
        stream_mix[11],
        stream_mix[10],
        stream_mix[9],
        stream_mix[8],
        stream_mix[7],
        stream_mix[6],
        stream_mix[5],
        stream_mix[4],
        stream_mix[3],
        stream_mix[2],
        stream_mix[1] ^ stream_feedback,
        stream_mix[0] ^ stream_feedback,
        stream_feedback
    };

`ifdef FPGA_NIST_PRNG_SOURCE
    /* FPGA-only NIST plumbing mode. This exposes PRNG output bytes directly.
     * This does not validate the ASIC RO entropy source. */
    assign reg_condlo = fpga_prng_half_sel ? fpga_prng_out[23:16] : fpga_prng_out[7:0];
    assign reg_condhi = fpga_prng_half_sel ? fpga_prng_out[31:24] : fpga_prng_out[15:8];
`else
    assign reg_condlo = stream_mix[7:0]  ^ stream_mix[23:16] ^ {stream_mix[3:0],  stream_mix[31:28]};
    assign reg_condhi = stream_mix[15:8] ^ stream_mix[31:24] ^ {stream_mix[11:8], stream_mix[27:24]};
`endif /* ! FPGA_NIST_PRNG_SOURCE */
    /* end TRNG_CONDITIONED_STREAM_GALOIS */
`else
    assign stream_feedback =
        stream_mix[31] ^
        stream_mix[21] ^
        stream_mix[1]  ^
        stream_mix[0]  ^
        selected_bit   ^
        lfsr[0]        ^
        lfsr[7]        ^
        lfsr[15];

    assign reg_condlo = stream_mix[7:0]  ^ stream_mix[31:24];
    assign reg_condhi = stream_mix[15:8] ^ stream_mix[23:16];
`endif /* !TRNG_CONDITIONED_STREAM_64_XOR */
`endif /* TRNG_CONDITIONED_STREAM */

    assign unused_reg_ctrl = &reg_ctrl[7:3];
    assign unused_reg_src  = &reg_src[7:2];

`ifdef TRNG_HEALTH_STATUS
    assign unused_reg_mode = &reg_mode;
`else
    assign unused_reg_mode = &reg_mode[7:3];
`endif

    assign unused_sample_shift = sample_shift[15];

/*
 * The real RO path instantiates SKY130 standard cells.
 * Keep it disabled for normal simulation and FPGA builds.
 * Enable it only for an explicit ASIC RO experiment by defining both:
 * TRNG_USE_RO and TRNG_ALLOW_REAL_RO.
 */
`ifdef TRNG_USE_RO
    `ifdef FPGA
        /* TRNG_USE_RO is not supported for FPGA builds. Please remove TRNG_USE_RO definition. */
        FPGA_TRNG_USE_RO_NOT_SUPPORTED u_stop ();
    `endif

    // TODO: why did we have this:
    //`ifdef SYNTH
    //    /* TRNG_USE_RO is not supported for FPGA builds. Please remove TRNG_USE_RO definition. */
    //    SYNTH_TRNG_USE_RO_NOT_SUPPORTED u_stop ();
    //`endif

    `ifdef TRNG_ALLOW_REAL_RO
        /* TRNG_LAB_USE_REAL_RO is used internally to conditionally include the real RO code. 
         * Do not define externally as there are multiple build paths during testing (e.g. FPGA) */
        `define TRNG_LAB_USE_REAL_RO
    `endif
`endif


/* 
 * --------------------------------------------------------------------------------------------
 * --------------------------------------------------------------------------------------------
 * Hardware specific ring oscillators
 *
 * TRNG_LAB_USE_REAL_RO should be defined only programmatically:
 *                      only in this file, when TRNG_USE_RO.
 *
 * TRNG_ALLOW_REAL_RO   should be defined only programmatically:
 *                      see project.v;
 *
 * TRNG_USE_RO          should be defined only programmatically:
 *                      see project.v; when SKY130, GF180 or other ASIC PDK (__pnr__) detected
 *
 * --------------------------------------------------------------------------------------------
 * --------------------------------------------------------------------------------------------
 */
`ifdef TRNG_LAB_USE_REAL_RO
    /* 
     * --------------------------------------------------------------------------------------------
     * --------------------------------------------------------------------------------------------
     * Real ring oscillator path. Requires explicit TRNG_ALLOW_REAL_RO.
     * --------------------------------------------------------------------------------------------
     * --------------------------------------------------------------------------------------------
     *
     *   f_ro is approximately:  1 / (2 * loop_delay)
     *
     * Where:
     *
     *   loop_delay is approximately: N * inverter_delay + enable_gate_delay + routing_delay
     *
     * 8 separate trng_ro modules, each with a different odd number of inverter stages.
     * Each has its own enable bit, 
     *   e.g. reg_oscen[X] -> u_roN
     *
     * Each one produces one raw oscillator output bit:
     *   u_roX -> ro_raw[N] 
     *
     */
`ifdef BASIC_RO_SET
    /* Legacy/basic RO bank starts at 3 stages.
     *
     *   reg_oscen[0] -> 3-inverter RO  -> ro_raw[0]
     *   reg_oscen[1] -> 5-inverter RO  -> ro_raw[1]
     *   reg_oscen[2] -> 7-inverter RO  -> ro_raw[2]
     *   reg_oscen[3] -> 9-inverter RO  -> ro_raw[3]
     *   reg_oscen[4] -> 11-inverter RO -> ro_raw[4]
     *   reg_oscen[5] -> 13-inverter RO -> ro_raw[5]
     *   reg_oscen[6] -> 15-inverter RO -> ro_raw[6]
     *   reg_oscen[7] -> 17-inverter RO -> ro_raw[7]
     *
     * Size 73.102% in sky #221: https://github.com/gojimmypi/ttsky-UART-FSM-TRNG-Lab/actions/runs/27450807079
     *
     * See trng_ro implementation for keep attributes, do not add them here.
    */
    trng_ro #(.STAGES(3))  u_ro0 (.enable(reg_oscen[0]), .ro_out(ro_raw[0]));
    trng_ro #(.STAGES(5))  u_ro1 (.enable(reg_oscen[1]), .ro_out(ro_raw[1]));
    trng_ro #(.STAGES(7))  u_ro2 (.enable(reg_oscen[2]), .ro_out(ro_raw[2]));
    trng_ro #(.STAGES(9))  u_ro3 (.enable(reg_oscen[3]), .ro_out(ro_raw[3]));
    trng_ro #(.STAGES(11)) u_ro4 (.enable(reg_oscen[4]), .ro_out(ro_raw[4]));
    trng_ro #(.STAGES(13)) u_ro5 (.enable(reg_oscen[5]), .ro_out(ro_raw[5]));
    trng_ro #(.STAGES(15)) u_ro6 (.enable(reg_oscen[6]), .ro_out(ro_raw[6]));
    trng_ro #(.STAGES(17)) u_ro7 (.enable(reg_oscen[7]), .ro_out(ro_raw[7]));
`else
    /* Default RO bank starts at 7 stages to avoid very short 3/5-stage rings.
     * Size 73.381% in GDS #223: https://github.com/gojimmypi/ttsky-UART-FSM-TRNG-Lab/actions/runs/27452625395 
     *
     * See trng_ro implementation for keep attributes, do not add them here.
     */ 
    trng_ro #(.STAGES(7))  u_ro0 (.enable(reg_oscen[0]), .ro_out(ro_raw[0]));
    trng_ro #(.STAGES(9))  u_ro1 (.enable(reg_oscen[1]), .ro_out(ro_raw[1]));
    trng_ro #(.STAGES(11)) u_ro2 (.enable(reg_oscen[2]), .ro_out(ro_raw[2]));
    trng_ro #(.STAGES(13)) u_ro3 (.enable(reg_oscen[3]), .ro_out(ro_raw[3]));
    trng_ro #(.STAGES(15)) u_ro4 (.enable(reg_oscen[4]), .ro_out(ro_raw[4]));
    trng_ro #(.STAGES(17)) u_ro5 (.enable(reg_oscen[5]), .ro_out(ro_raw[5]));
    trng_ro #(.STAGES(19)) u_ro6 (.enable(reg_oscen[6]), .ro_out(ro_raw[6]));
    trng_ro #(.STAGES(21)) u_ro7 (.enable(reg_oscen[7]), .ro_out(ro_raw[7]));
`endif /* ~BASIC_RO_SET */

`else
    /* 
     * --------------------------------------------------------------------------------------------
     * --------------------------------------------------------------------------------------------
     * Typical simulation or FPGA path.
     *
     * The "RO" bits are deterministic LFSR-derived signals, not physical
     * entropy sources. This path is for functional testing only.
     * --------------------------------------------------------------------------------------------
     * --------------------------------------------------------------------------------------------
     */

`ifdef FPGA_NIST_PRNG_SOURCE
    reg [31:0] fpga_s0;
    reg [31:0] fpga_s1;
    reg [31:0] fpga_s2;
    reg [31:0] fpga_s3;
    reg        fpga_prng_half_sel;

    function [31:0] rotl32;
        input [31:0] x;
        input [4:0] k;
        begin
            rotl32 = (x << k) | (x >> (32 - k));
        end
    endfunction

    wire [31:0] fpga_prng_out = rotl32(fpga_s0 + fpga_s3, 5'd7) + fpga_s0;

    wire [31:0] fpga_t   = fpga_s1 << 9;
    wire [31:0] fpga_s2a = fpga_s2 ^ fpga_s0;
    wire [31:0] fpga_s3a = fpga_s3 ^ fpga_s1;
    wire [31:0] fpga_s1n = fpga_s1 ^ fpga_s2a;
    wire [31:0] fpga_s0n = fpga_s0 ^ fpga_s3a;
    wire [31:0] fpga_s2n = fpga_s2a ^ fpga_t;
    wire [31:0] fpga_s3n = rotl32(fpga_s3a, 5'd11);

    /* Keep ro_raw deterministic for register/source functional behavior.
     * The NIST stream path uses fpga_prng_out directly through reg_condlo/reg_condhi. */
    assign ro_raw[0] = reg_oscen[0] & lfsr[0];
    assign ro_raw[1] = reg_oscen[1] & lfsr[3];
    assign ro_raw[2] = reg_oscen[2] & lfsr[5];
    assign ro_raw[3] = reg_oscen[3] & lfsr[7];
    assign ro_raw[4] = reg_oscen[4] & lfsr[9];
    assign ro_raw[5] = reg_oscen[5] & lfsr[11];
    assign ro_raw[6] = reg_oscen[6] & lfsr[13];
    assign ro_raw[7] = reg_oscen[7] & lfsr[15];

    always @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            fpga_s0 <= 32'h676f_6a69;
            fpga_s1 <= 32'h6d6d_7970;
            fpga_s2 <= 32'h695f_5454;
            fpga_s3 <= 32'h4650_4741;
            fpga_prng_half_sel <= 1'b0;
        end else if (do_sample_q) begin
            fpga_prng_half_sel <= !fpga_prng_half_sel;

            if (fpga_prng_half_sel) begin
                fpga_s0 <= fpga_s0n;
                fpga_s1 <= fpga_s1n;
                fpga_s2 <= fpga_s2n;
                fpga_s3 <= fpga_s3n;
            end
        end
    end
`elsif FPGA_BASIC_LFSR_RO_TAPS
    /* The "RO" bits are just taps from the LFSR. */
    assign ro_raw[0] = lfsr[0];
    assign ro_raw[1] = lfsr[3];
    assign ro_raw[2] = lfsr[5];
    assign ro_raw[3] = lfsr[7];
    assign ro_raw[4] = lfsr[9];
    assign ro_raw[5] = lfsr[11];
    assign ro_raw[6] = lfsr[13];
    assign ro_raw[7] = lfsr[15];
`else
    /* Default FPGA surrogate respects reg_oscen, but is still deterministic. */
    assign ro_raw[0] = reg_oscen[0] & lfsr[0];
    assign ro_raw[1] = reg_oscen[1] & lfsr[3];
    assign ro_raw[2] = reg_oscen[2] & lfsr[5];
    assign ro_raw[3] = reg_oscen[3] & lfsr[7];
    assign ro_raw[4] = reg_oscen[4] & lfsr[9];
    assign ro_raw[5] = reg_oscen[5] & lfsr[11];
    assign ro_raw[6] = reg_oscen[6] & lfsr[13];
    assign ro_raw[7] = reg_oscen[7] & lfsr[15];
`endif /* !FPGA_BASIC_LFSR_RO_TAPS */

`endif /* !TRNG_LAB_USE_REAL_RO */

    always @(*) begin
        case (source_select)
            SRC_LFSR: begin
                selected_bit = lfsr[0];
            end

            SRC_RO0: begin
                selected_bit = ro0_sample_sync;
            end

            SRC_ROX: begin
                selected_bit = rox_sample_sync;
            end

            SRC_MIX: begin
            `ifdef TRNG_RAW_CLEAN_MIX
                selected_bit = rox_sample_sync;
            `else
                selected_bit = rox_sample_sync ^ lfsr[0] ^ lfsr[5] ^ sample_shift[3];
            `endif
            end

            default: begin
                selected_bit = lfsr[0];
            end
        endcase
    end

`ifdef TRNG_HEALTH_STATUS
    trng_health_core u_health
    (
        .clk(clk),
        .rst_n(rst_n),
        .clear_i(trng_reset),
        .sample_valid_i(health_sample_valid_q),
        .sample_bit_i(health_sample_bit_q),
        .ro_activity_bit_i(health_ro_activity_q),
        .any_osc_en_i(health_any_osc_en_q),
        .health_status_o(health_status)
    );
`endif

    always @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            trng_step_d     <= 1'b0;
            sample_tick_q   <= 1'b0;
            do_sample_q     <= 1'b0;

            sample_ctr      <= 9'd0;
            lfsr            <= 16'h1ACE;
            sample_shift    <= 16'h0000;

`ifdef TRNG_CONDITIONED_STREAM
    `ifdef TRNG_CONDITIONED_STREAM_64_XOR
            /* Nonzero fixed conditioner seed. This is not a secret key and does not
             * provide entropy. It only prevents the conditioner from starting at zero.
             * ASCII "gojimmy!" makes the value traceable and intentional. */
            stream_mix <= 64'h676F_6A69_6D6D_7921; // "gojimmy!"
    `elsif TRNG_CONDITIONED_STREAM_CRC
            stream_mix      <= 16'hA5C3;
    `elsif TRNG_CONDITIONED_STREAM_GALOIS_64
            stream_mix      <= 64'h676F_6A69_6D6D_7921; // "gojimmy!"
            condlo_mix      <= 8'hA5;
            condhi_mix      <= 8'h3C;
    `elsif TRNG_CONDITIONED_STREAM_GALOIS
            stream_mix      <= 32'h676F_6A69; // "goji"
    `else
            stream_mix      <= 32'hA5C3_1F2D;
    `endif
`endif /* TRNG_CONDITIONED_STREAM */

            ro0_sample_meta <= 1'b0;
            ro0_sample_sync <= 1'b0;
            rox_sample_meta <= 1'b0;
            rox_sample_sync <= 1'b0;
            reg_status      <= 8'h00;
            reg_rawlo       <= 8'h00;
            reg_rawhi       <= 8'h00;
`ifdef TRNG_HEALTH_STATUS
            health_sample_valid_q <= 1'b0;
            health_sample_bit_q   <= 1'b0;
            health_ro_activity_q  <= 1'b0;
            health_any_osc_en_q   <= 1'b0;
`endif
`ifdef TRNG_BINARY_STREAM
            stream_sample_count <= 8'h00;
`endif
            /* End hardware reset */

        end else if (trng_reset) begin
            trng_step_d     <= 1'b0;
            sample_tick_q   <= 1'b0;
            do_sample_q     <= 1'b0;

            sample_ctr      <= 9'd0;
            lfsr            <= 16'h1ACE;
            sample_shift    <= 16'h0000;

`ifdef TRNG_CONDITIONED_STREAM
    `ifdef TRNG_CONDITIONED_STREAM_64_XOR
            /* Nonzero fixed conditioner seed. This is not a secret key and does not
             * provide entropy. It only prevents the conditioner from starting at zero.
             * ASCII "gojimmy!" makes the value traceable and intentional. */
            stream_mix <= 64'h676F_6A69_6D6D_7921; // "gojimmy!"
    `elsif TRNG_CONDITIONED_STREAM_CRC
            stream_mix      <= 16'hA5C3;
    `elsif TRNG_CONDITIONED_STREAM_GALOIS_64
            stream_mix      <= 64'h676F_6A69_6D6D_7921; // "gojimmy!"
            condlo_mix      <= 8'hA5;
            condhi_mix      <= 8'h3C;
    `elsif TRNG_CONDITIONED_STREAM_GALOIS
            stream_mix      <= 32'h676F_6A69; // "goji"
    `else
            stream_mix      <= 32'hA5C3_1F2D;
    `endif
`endif /* TRNG_CONDITIONED_STREAM */

            ro0_sample_meta <= 1'b0;
            ro0_sample_sync <= 1'b0;
            rox_sample_meta <= 1'b0;
            rox_sample_sync <= 1'b0;
            reg_status      <= 8'h00;
            reg_rawlo       <= 8'h00;
            reg_rawhi       <= 8'h00;
`ifdef TRNG_HEALTH_STATUS
            health_sample_valid_q <= 1'b0;
            health_sample_bit_q   <= 1'b0;
            health_ro_activity_q  <= 1'b0;
            health_any_osc_en_q   <= 1'b0;
`endif
`ifdef TRNG_BINARY_STREAM
            stream_sample_count <= 8'h00;
`endif
            /* End software TRNG reset */

        end else begin
            /* Main always block */
            trng_step_d     <= trng_step;
            sample_tick_q   <= sample_tick;
            do_sample_q     <= (trng_enable && sample_tick) || trng_step_pulse;

            ro0_sample_meta <= ro_raw[0];
            ro0_sample_sync <= ro0_sample_meta;

            rox_sample_meta <= ro_xor;
            rox_sample_sync <= rox_sample_meta;

            reg_status[0]   <= trng_enable;
            reg_status[1]   <= sample_tick_q;
            reg_status[2]   <= |reg_oscen;
`ifdef TRNG_HEALTH_STATUS
            health_sample_valid_q <= trng_enable && do_sample_q;
            health_sample_bit_q   <= selected_bit;
            health_ro_activity_q  <= rox_sample_sync;
            health_any_osc_en_q   <= |reg_oscen;

            /* Preserve legacy R5[2:0]; use health monitor only for R5[7:3] */
            reg_status[3]   <= health_status[3]; /* health_valid */
            reg_status[4]   <= health_status[4]; /* activity_seen */
            reg_status[5]   <= health_status[5]; /* repetition_fail */
            reg_status[6]   <= health_status[6]; /* stuck_fail */
            reg_status[7]   <= health_status[7]; /* health_fail */
`else
            reg_status[4:3] <= source_select;
            reg_status[7:5] <= reg_mode[2:0];
`endif

            if (do_sample_q) begin
                sample_ctr   <= 9'd0;
                lfsr         <= {lfsr[14:0], lfsr_next_bit};
                sample_shift <= {sample_shift[14:0], selected_bit};
                reg_rawlo    <= {sample_shift[6:0], selected_bit};
                reg_rawhi    <= sample_shift[14:7];
        `ifdef TRNG_BINARY_STREAM
                stream_sample_count <= stream_sample_count + 8'h01;
        `endif

`ifdef TRNG_CONDITIONED_STREAM
`ifdef TRNG_CONDITIONED_STREAM_64_XOR
                stream_mix   <= {
                    stream_mix[62:0],
                    stream_feedback
                } ^ {
                    reg_rawhi,
                    reg_rawlo,
                    sample_shift[15:8],
                    sample_shift[7:0],
                    lfsr[15:8],
                    lfsr[7:0],
                    ro_raw,
                    {7'b0000000, selected_bit}
                };
`elsif TRNG_CONDITIONED_STREAM_CRC
                stream_mix   <= {
                    stream_mix[14] ^ feedback,
                    stream_mix[13],
                    stream_mix[12],
                    stream_mix[11],
                    stream_mix[10],
                    stream_mix[9],
                    stream_mix[8],
                    stream_mix[7],
                    stream_mix[6],
                    stream_mix[5],
                    stream_mix[4],
                    stream_mix[3],
                    stream_mix[2],
                    stream_mix[1] ^ feedback,
                    stream_mix[0],
                    feedback
                };
`elsif TRNG_CONDITIONED_STREAM_GALOIS_64
                stream_mix   <= galois_mix_next ^ {
                    reg_rawhi ^ lfsr[15:8],
                    reg_rawlo ^ lfsr[7:0],
                    sample_shift[15:8] ^ ro_raw,
                    sample_shift[7:0] ^ {7'b0000000, selected_bit},
                    {ro_raw[3:0], ro_raw[7:4]} ^ sample_shift[11:4],
                    ro_raw ^ sample_shift[15:8],
                    lfsr[15:8] ^ {7'b0000000, ro0_sample_sync},
                    lfsr[7:0] ^ {7'b0000000, rox_sample_sync}
                };
                condlo_mix   <= condlo_mix_next;
                condhi_mix   <= condhi_mix_next;
`elsif TRNG_CONDITIONED_STREAM_GALOIS
                stream_mix   <= galois_mix_next ^ {
                    reg_rawhi ^ lfsr[15:8],
                    reg_rawlo ^ lfsr[7:0],
                    sample_shift[15:8] ^ ro_sync_mix,
                    sample_shift[7:0] ^ {7'b0000000, selected_bit}
                };
`else
                stream_mix   <= {
                    stream_mix[30:0],
                    stream_feedback
                } ^ {
                    reg_rawhi,
                    reg_rawlo,
                    sample_shift[15:8],
                    sample_shift[7:0]
                };
`endif /* !TRNG_CONDITIONED_STREAM_64_XOR */
`endif /* TRNG_CONDITIONED_STREAM */

            end else if (trng_enable) begin
                sample_ctr <= sample_ctr + 9'd1;

            end else begin
                sample_ctr <= 9'd0;
            end
        end
    end

endmodule /* trng_lab_core */


`ifdef TRNG_HEALTH_STATUS
/*
 * --------------------------------------------------------------------------------------------
 * --------------------------------------------------------------------------------------------
 * Lightweight TRNG health monitor
 * --------------------------------------------------------------------------------------------
 * --------------------------------------------------------------------------------------------
 * This monitor is intentionally small. It is not a full entropy estimator and
 * does not replace off-chip NIST or 90B analysis. It only catches obvious
 * failures such as a stuck selected source or a long repeated-bit run.
 *
 * health_status_o bit map:
 *   [3] health_valid: at least one sample window completed
 *   [4] activity_seen: RO activity bit toggled since the last clear/reset
 *   [5] repetition_fail: 16 identical sampled bits in a row
 *   [6] stuck_fail: no RO activity-bit transition in a 64-sample window
 *   [7] health_fail: fatal health failure, currently same as stuck_fail
 */
module trng_health_core
(
    input  wire       clk,
    input  wire       rst_n,
    input  wire       clear_i,
    input  wire       sample_valid_i,
    input  wire       sample_bit_i,
    input  wire       ro_activity_bit_i,
    input  wire       any_osc_en_i,
    output wire [7:3] health_status_o /*  Preserves legacy R5[2:0] */
);
/* -------------------------------------------------------------------------------------------- */
    localparam [5:0] HEALTH_WINDOW_MASK = 6'h3F; /* 64 sampled bits */
    localparam [3:0] HEALTH_REPEAT_MAX  = 4'hF;  /* 16 identical sampled bits */

    reg       sample_seen;
    reg       last_sample;
    reg       last_ro_activity;
    reg       window_transition_seen;
    reg       activity_seen;
    reg       health_valid;
    reg       repetition_fail;
    reg       stuck_fail;
    reg [3:0] repeat_count;
    reg [5:0] window_count;

    wire      sample_toggled;
    wire      ro_activity_toggled;
    wire      window_done;
    wire      next_window_transition_seen;

    assign sample_toggled = sample_seen && (sample_bit_i != last_sample);
    assign ro_activity_toggled = sample_seen && (ro_activity_bit_i != last_ro_activity);
    assign window_done = (window_count == HEALTH_WINDOW_MASK);
    assign next_window_transition_seen = window_transition_seen || ro_activity_toggled;

    wire health_fail = stuck_fail;

    assign health_status_o = {
        health_fail,     /* [7] fatal health failure, currently same as stuck_fail */
        stuck_fail,      /* [6] no monitored RO transition seen during a completed health window */
        repetition_fail, /* [5] diagnostic: 16 identical sampled bits in a row. Not impossible, but unlikely */
        activity_seen,   /* [4] sticky: at least one monitored RO transition seen since clear */
        health_valid     /* [3] at least one 64-sample health window completed; just a completed data sample, window_done, nothing more */
    };

    always @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            sample_seen            <= 1'b0;
            last_sample            <= 1'b0;
            last_ro_activity       <= 1'b0;
            window_transition_seen <= 1'b0;
            activity_seen          <= 1'b0;
            health_valid           <= 1'b0;
            repetition_fail        <= 1'b0;
            stuck_fail             <= 1'b0;
            repeat_count           <= 4'h0;
            window_count           <= 6'h00;
        end else if (clear_i) begin
            sample_seen            <= 1'b0;
            last_sample            <= 1'b0;
            last_ro_activity       <= 1'b0;
            window_transition_seen <= 1'b0;
            activity_seen          <= 1'b0;
            health_valid           <= 1'b0;
            repetition_fail        <= 1'b0;
            stuck_fail             <= 1'b0;
            repeat_count           <= 4'h0;
            window_count           <= 6'h00;
        end else if (sample_valid_i) begin
            if (!sample_seen) begin
                sample_seen            <= 1'b1;
                last_sample            <= sample_bit_i;
                last_ro_activity       <= ro_activity_bit_i;
                window_transition_seen <= 1'b0;
                repeat_count           <= 4'h0;
                window_count           <= 6'h01;
            end else begin
                last_sample      <= sample_bit_i;
                last_ro_activity <= ro_activity_bit_i;

                if (ro_activity_toggled) begin
                    window_transition_seen <= 1'b1;
                    activity_seen          <= 1'b1;
                end

                if (sample_toggled) begin
                    repeat_count <= 4'h0;
                end else begin
                    if (repeat_count != HEALTH_REPEAT_MAX) begin
                        repeat_count <= repeat_count + 4'h1;
                    end

                    if (repeat_count >= (HEALTH_REPEAT_MAX - 4'h1)) begin
                        repetition_fail <= 1'b1;
                    end
                end

                if (window_done) begin
                    health_valid <= 1'b1;

                    if (any_osc_en_i && !next_window_transition_seen) begin
                        stuck_fail <= 1'b1;
                    end

                    window_transition_seen <= 1'b0;
                    window_count           <= 6'h00;
                end else begin
                    window_count <= window_count + 6'h01;
                end
            end
        end
    end

endmodule /* trng_health_core */

`endif /* TRNG_HEALTH_STATUS */


/*
 * --------------------------------------------------------------------------------------------
 * --------------------------------------------------------------------------------------------
 * Hardware specific inverter cell for ring oscillator
 * --------------------------------------------------------------------------------------------
 * --------------------------------------------------------------------------------------------
 * Keep:
 * 
 *   > The keep attribute on cells and wires is used to mark objects that should never be removed 
 *     by the optimizer. This is used for example for cells that have hidden connections that are 
 *     not part of the netlist, such as IO pads. Setting the keep attribute on a module has the 
 *     same effect as setting it on all instances of the module.
 *
 *   > The keep_hierarchy attribute on cells and modules keeps the flatten command from flattening 
 *     the indicated cells and modules.
 *
 * See: https://yosyshq.readthedocs.io/projects/yosys/en/latest/using_yosys/verilog.html
 *
 * keep_hierarchy:
 *   This tells Yosys not to flatten cells/modules marked with keep_hierarchy. Yosys documents
 *   that keep_hierarchy on cells and modules prevents the flatten command from flattening them.
 *
 * See: https://yosyshq.readthedocs.io/projects/yosys/en/latest/cmd/index_passes_hierarchy.html#cmd-keep_hierarchy
 *
 * dont_touch:
 *   This one is less clean in an open-source flow than in vendor FPGA tools.
 *
 *   Yosys clearly documents keep and keep_hierarchy; it does not give dont_touch the same central 
 *   documented meaning in the Yosys Verilog-support page. In practice, tools may carry the attribute 
 *   along, and some passes may honor it, but for Yosys the load-bearing attribute is keep. 
 *
 *   OpenROAD has a separate Tcl command, set_dont_touch, which prevents resizer commands 
 *   from modifying instances or nets.
 *
 * See: https://openroad.readthedocs.io/en/latest/main/src/rsz/README.html#set-dont-touch
 */
`ifdef TRNG_LAB_USE_REAL_RO

(* keep_hierarchy, keep, dont_touch *)
module trng_ro_inverter_cell
(
    input  wire a,
    output wire y
);

    `LINT_OFF_PINMISSING_POWER_PINS

    /* See target_pdk.v included at the top-level project.v for the PDK selection. 
     * The cells instantiated here must match the selected PDK.
     *
     * sky130_fd_sc_hd__inv_1: Minimum / weaker inverter, Slower, lower drive, likely lower dynamic power
     * sky130_fd_sc_hd__inv_2: Stronger inverter, roughly 2x drive class, Faster edges, can drive more capacitance, likely more dynamic power
     */ 
    `ifdef PDK_TARGET_SKY130
        /* See https://sky130-unofficial.readthedocs.io/en/latest/contents/libraries/sky130_fd_sc_hd/cells/inv/README.html */
        (* keep_hierarchy, keep, dont_touch *) sky130_fd_sc_hd__inv_2 u_inv
        (
            .A(a),
            .Y(y)
        );

        /* SKY130 hard stop breadcrumb. 
         * Example: see https://github.com/gojimmypi/ttsky-UART-FSM-TRNG-Lab/actions/runs/27464130872
         * Uncomment to confirm SKY130 build failure: */
        // PROJECT_ASIC_SKY130_BREADCRUMB_FAULT u_stop (); 

    `elsif PDK_TARGET_GF180
        /* not a valid GF detector: https://github.com/gojimmypi/ttgf-UART-FSM-TRNG-Lab/actions/runs/26855846226/job/79198383591 */
        // `ifdef gf180mcu_fd_sc_mcu7t5v0
        //    /* if a macro, we found it, success! for GF180 detection*/
        //    PROJECT_FOUND_PDK u_stop ();
        //`endif

        /* 
         * There are several possible inverters to use in GF180. See
         *    https://github.com/google/globalfoundries-pdk-libs-gf180mcu_fd_sc_mcu7t5v0/tree/main/cells/inv
         *
         * Asked ChatGPT to summarize differences. This is the response:
         *
         * | Cell    | Drive |        Area | Input cap | Min-load delay, falling output | Min-load delay, rising output |
         * | ------- | ----: | ----------: | --------: | -----------------------------: | ----------------------------: |
         * | `inv_1` |    1X |  8.7808 um2 | 0.0047 pF |                      0.0377 ns |                     0.0499 ns |
         * | `inv_2` |    2X | 13.1712 um2 | 0.0093 pF |                      0.0308 ns |                     0.0387 ns |
         * | `inv_3` |    3X | 17.5616 um2 | 0.0140 pF |                      0.0309 ns |                     0.0388 ns |
         * | `inv_4` |    4X | 21.9520 um2 | 0.0185 pF |                      0.0282 ns |                     0.0345 ns |
         * | `inv_8` |    8X | 39.5136 um2 | 0.0373 pF |                      0.0282 ns |                     0.0344 ns |
         *
         *   "The key observation: _inv_2 gives a meaningful speed/drive improvement over _inv_1, but _inv_3 gives almost no 
         *    further min-load delay improvement over _inv_2. _inv_4 and _inv_8 are only modestly faster in the min-load table, 
         *     while costing much more input capacitance and area."
         *
         * For reference:
         *
         * Option 1: gf180mcu_fd_sc_mcu7t5v0__inv_1
         *    https://github.com/google/globalfoundries-pdk-libs-gf180mcu_fd_sc_mcu7t5v0/blob/main/cells/inv/gf180mcu_fd_sc_mcu7t5v0__inv_1.functional.v
         *    https://github.com/google/globalfoundries-pdk-libs-gf180mcu_fd_sc_mcu7t5v0/blob/main/cells/inv/gf180mcu_fd_sc_mcu7t5v0__inv_1.rst
         *
         * Option 2: gf180mcu_fd_sc_mcu7t5v0__inv_2
         *    https://github.com/google/globalfoundries-pdk-libs-gf180mcu_fd_sc_mcu7t5v0/blob/main/cells/inv/gf180mcu_fd_sc_mcu7t5v0__inv_2.functional.v
         *    https://github.com/google/globalfoundries-pdk-libs-gf180mcu_fd_sc_mcu7t5v0/blob/main/cells/inv/gf180mcu_fd_sc_mcu7t5v0__inv_2.rst
         */

        (* keep_hierarchy, keep, dont_touch *) gf180mcu_fd_sc_mcu7t5v0__inv_2 u_inv
        (
            .I(a),
            .ZN(y)
        );

        /* GF180 hard stop breadcrumb. 
         * Example: see TBD
         * Uncomment to confirm GF180 build failure: */
        // PROJECT_ASIC_GF180_BREADCRUMB_FAULT u_stop (); 

    `else
        PROJECT_ASIC_SKY130_OR_GF180_ONLY u_stop (); /* Only SKY130 and GF180 supported at this time */
    `endif

    `LINT_ON_PINMISSING_POWER_PINS

endmodule /* trng_ro_inverter_cell */


/*
 * --------------------------------------------------------------------------------------------
 * --------------------------------------------------------------------------------------------
 * Build a gated ring oscillator out of [STAGES] inverter cells.
 * --------------------------------------------------------------------------------------------
 * --------------------------------------------------------------------------------------------
 * Do not place any keep hierarchy decorators here. See: 
 *   https://github.com/gojimmypi/ttsky-UART-FSM-TRNG-Lab/actions/runs/27505094813
 *   https://github.com/gojimmypi/ttgf-UART-FSM-TRNG-Lab/actions/runs/27505061710
 * Revert only that attribute for success:
 *   https://github.com/gojimmypi/ttsky-UART-FSM-TRNG-Lab/actions/runs/27505702100
 *   https://github.com/gojimmypi/ttgf-UART-FSM-TRNG-Lab/actions/runs/27506081181
 */
module trng_ro
#(
    parameter integer STAGES = 3
)
(
    input  wire enable,
    output wire ro_out
);

/* We need at least 3 stages, hard stop otherwise */
generate
    if ((STAGES < 3) || ((STAGES % 2) == 0)) begin : gen_bad_ro_stage_count
        TRNG_RO_STAGE_COUNT_MUST_BE_ODD_AND_AT_LEAST_3 u_stop ();
    end
endgenerate

    (* keep, dont_touch *) wire [STAGES-1:0] inv_in;
    (* keep, dont_touch *) wire [STAGES-1:0] inv_out;

    assign inv_in[STAGES-1:1] = inv_out[STAGES-2:0];
    assign inv_in[0] = inv_out[STAGES-1] & enable;

    /* connect output to last stage of the inverter sequence */
    assign ro_out = inv_out[STAGES-1];

    /* inv_array is an array of inverter cell instances forming one ring */
    (* keep_hierarchy, keep, dont_touch *) trng_ro_inverter_cell inv_array [STAGES-1:0]
    (
        .a(inv_in),
        .y(inv_out)
    );

endmodule /* trng_ro */

`endif /* TRNG_LAB_USE_REAL_RO */

/* 
 * --------------------------------------------------------------------------------------------
 * --------------------------------------------------------------------------------------------
 * Sanity checks
 * --------------------------------------------------------------------------------------------
 * --------------------------------------------------------------------------------------------
 */
`ifdef TRNG_LAB_USE_REAL_RO
    /* Some options prohibited if real ring oscillator detected */
    `ifdef FPGA_NIST_PRNG_SOURCE
        PROJECT_TRNG_LAB_USE_REAL_RO_PROHIBIT_FPGA_NIST_PRNG_SOURCE_NOT_A_VALID_OPTION u_stop ();  /* FPGA_NIST_PRNG_SOURCE only for FPGA */
    `endif

    `ifdef FPGA_NIST_PRNG_SOURCE
         PROJECT_TRNG_LAB_USE_REAL_RO_PROHIBIT_ULX3S u_stop ();  /* ULX3S only for FPGA */
    `endif

    `ifdef ULX3S_USE_GN12_50MHZ
         PROJECT_TRNG_LAB_USE_REAL_RO_PROHIBIT_ULX3S_USE_GN12_50MHZ u_stop ();  /* ULX3S_USE_GN12_50MHZ only for FPGA */
    `endif

    `ifdef IS_MY_IVERILOG_SIMULATION
        PROJECT_TRNG_LAB_USE_REAL_RO_PROHIBIT_IS_MY_IVERILOG_SIMULATION u_stop ();  /* IS_MY_IVERILOG_SIMULATION not allowed for real RO */
    `endif
`endif

/* 
 * --------------------------------------------------------------------------------------------
 * --------------------------------------------------------------------------------------------
 *  Clean up macros created and used only in this file
 * --------------------------------------------------------------------------------------------
 * --------------------------------------------------------------------------------------------
 */
`ifdef TRNG_LAB_USE_REAL_RO
    `undef TRNG_LAB_USE_REAL_RO
`endif

`ifdef LINT_OFF_PINMISSING_POWER_PINS
    `undef LINT_OFF_PINMISSING_POWER_PINS
`endif

`ifdef LINT_ON_PINMISSING_POWER_PINS
    `undef LINT_ON_PINMISSING_POWER_PINS
`endif

`endif /* TRNG_ENABLED */

`default_nettype wire
