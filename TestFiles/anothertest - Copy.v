// File ulx3s_v20_passthru_wifi.vhd translated with vhd2vl v3.0 VHDL to Verilog RTL translator
// this is a freshly converted 6a044e8 version from March 2
// see https://github.com/emard/ulx3s-passthru/blob/71ce18953f84ea8ee07bb42d42ddc5a2673623c3/rtl/ulx3s_v20_passthru_wifi.vhd#L1
// 
// vhd2vl settings:
//  * Verilog Module Declaration Style: 2001

// vhd2vl is Free (libre) Software:
//   Copyright (C) 2001 Vincenzo Liguori - Ocean Logic Pty Ltd
//     http://www.ocean-logic.com
//   Modifications Copyright (C) 2006 Mark Gonzales - PMC Sierra Inc
//   Modifications (C) 2010 Shankar Giri
//   Modifications Copyright (C) 2002-2017 Larry Doolittle
//     http://doolittle.icarus.com/~larry/vhd2vl/
//   Modifications (C) 2017 Rodrigo A. Melo
//
//   vhd2vl comes with ABSOLUTELY NO WARRANTY.  Always check the resulting
//   Verilog for correctness, ideally with a formal verification tool.
//
//   You are welcome to redistribute vhd2vl under certain conditions.
//   See the license (GPLv2) file included with the source for details.

// The result of translation follows.  Its copyright status should be
// considered unchanged from the original VHDL.

// (c)EMARD
// License=BSD
// module to bypass user input and usbserial to esp32 wifi
// no timescale needed

module ulx3s_passthru_wifi(
	input  wire clk_25mhz, // -- main clock input from 25MHz clock source must be lowercase

	// UART0 (FTDI USB slave serial)
	output wire ftdi_rxd,
	input  wire ftdi_txd,

	// FTDI additional signaling
	inout  wire ftdi_ndtr,
	// inout  wire ftdi_ndsr, // ERROR: IO 'ftdi_ndsr' is unconstrained in LPF
	inout  wire ftdi_nrts,
	inout  wire ftdi_txden,

	// UART1 (WiFi serial)
	output wire wifi_rxd,
	input  wire wifi_txd,
	inout  wire wifi_en,

	// WiFi additional signaling
	inout  wire wifi_gpio0,
	// inout  wire wifi_gpio2, // ERROR: IO 'wifi_gpio2' is unconstrained in LPF
	inout wire wifi_gpio5,
	inout  wire wifi_gpio16,
	inout  wire wifi_gpio17,

	// Onboard blinky
	output wire [7:0] led,
	input  wire [6:0] btn,
	input  wire [1:4] sw,
	output wire oled_csn,
	output wire oled_clk,
	output wire oled_mosi,
	output wire oled_dc,
	output wire oled_resn,

	// GPIO (some are shared with wifi and adc)
	inout  wire [27:0] gp,
	inout  wire [27:0] gn,

	// SHUTDOWN: logic '1' here will shutdown power on PCB >= v1.7.5
	// 
	output wire shutdown,

	// Audio jack 3.5mm
	inout  wire [3:0] audio_l,
	inout  wire [3:0] audio_r,
	inout  wire [3:0] audio_v,
	// Flash ROM (SPI0)
	output wire flash_holdn,
	output wire flash_wpn,

	// SD card (SPI1)
	inout  wire [3:0] sd_d,
	input  wire sd_cmd,
	input  wire sd_clk,
	input  wire sd_cdn,
	input  wire sd_wp,

	output wire user_programn
);



// main clock input from 25MHz clock source must be lowercase
// UART0 (FTDI USB slave serial)
// FTDI additional signaling
// UART1 (WiFi serial)
// WiFi additional signaling
// '0' will disable wifi by default
// Onboard blinky
// GPIO (some are shared with wifi and adc)
// SHUTDOWN: logic '1' here will shutdown power on PCB >= v1.7.5
// Audio jack 3.5mm
// Digital Video (differential outputs)
//gpdi_dp, gpdi_dn: out std_logic_vector(2 downto 0);
//gpdi_clkp, gpdi_clkn: out std_logic;
// Flash ROM (SPI0)
//flash_miso   : in      std_logic;
//flash_mosi   : out     std_logic;
//flash_clk    : out     std_logic;
//flash_csn    : out     std_logic;
// SD card (SPI1)
// wifi_gpio 13,12,4,2
// wifi_gpio15
// wifi_gpio14
// setting this low will skip to next multiboot image


  assign shutdown = 0;

  wire [1:0] S_prog_in;
  reg  [1:0] R_prog_in; 
  wire [1:0] S_prog_out;
  reg  [7:0] R_spi_miso;
  wire S_oled_csn;
  parameter C_prog_release_timeout = 17;  // default 17 2^n * 25MHz timeout for initialization phase
  reg [C_prog_release_timeout:0] R_prog_release = 1'b1;  // timeout that holds lines for reliable entering programming mode
  reg [7:0] R_progn = 1'b0;
  
  // TX/RX passthru
  assign ftdi_rxd = wifi_txd;
  assign wifi_rxd = ftdi_txd;

  // Programming logic
  // SERIAL  ->  ESP32
  // DTR RTS -> EN IO0
  //  1   1     1   1
  //  0   0     1   1
  //  1   0     0   1
  //  0   1     1   0
  assign S_prog_in[1] = ftdi_ndtr;
  assign S_prog_in[0] = ftdi_nrts;
  assign S_prog_out = S_prog_in == 2'b10 ? 2'b01 : S_prog_in == 2'b01 ? 2'b10 : 2'b11;

  assign wifi_en = S_prog_out[1];
  assign wifi_gpio0 = S_prog_out[0] & btn[0];

  // holding BTN0 will hold gpio0 LOW, signal for ESP32 to take control
  //sd_d(0) <= '0' when wifi_gpio0 = '0' else 'Z'; -- gpio2 together with gpio0 to 0
  //sd_d(2) <= '0' when wifi_gpio0 = '0' else 'Z'; -- wifi gpio12
  assign sd_d[0] = R_prog_release[(C_prog_release_timeout)] == 1'b0 ? S_prog_out[0] : S_oled_csn == 1'b0 ? R_spi_miso[0] : 1'bZ;
  // gpio2 to 0 during programming init
	// assign sd_d[2] = (S_prog_in[0] ^ S_prog_in[1]) == 1'b1 ? 1'b0 : 1'bZ; // sd_d(2) <= '0' when (S_prog_in(0) xor S_prog_in(1)) = '1' else 'Z'; -- wifi gpio12
	// assign sd_d[2] = R_prog_release[(C_prog_release_timeout)] == 1'b0 ? 1'b0 : 1'bZ; // sd_d(2) <= '0' when R_prog_release(R_prog_release'high) = '0' else 'Z'; -- wifi gpio12
	// assign sd_d[3] = R_prog_release[(C_prog_release_timeout)] == 1'b0 ? 1'b1 : 1'bZ; // sd_d(3) <= '1' when R_prog_release(R_prog_release'high) = '0' else 'Z';
	// assign sd_clk = R_prog_release[(C_prog_release_timeout)] == 1'b0 ? clk_25mhz : 1'bZ; // sd_clk <= clk_25mhz when R_prog_release(R_prog_release'high) = '0' else 'Z';
  // permanent flashing mode
	// assign wifi_en = ftdi_nrts; // wifi_en <= ftdi_nrts;
	// assign wifi_gpio0 = ftdi_ndtr; // wifi_gpio0 <= ftdi_ndtr;
  assign S_oled_csn = wifi_gpio17;
  assign oled_csn = S_oled_csn;
  assign oled_clk = sd_clk;  // wifi_gpio14
  assign oled_mosi = sd_cmd;  // wifi_gpio15
  assign oled_dc = wifi_gpio16;
  assign oled_resn = gp[11]; // wifi_gpio25

  // show OLED signals on the LEDs
  // show SD signals on the LEDs
	// assign led[7:0] = {S_oled_csn,R_spi_miso[0],sd_clk,sd_d[2],sd_d[3],sd_cmd,sd_d[0],sd_d[1]}; // led(7 downto 0) <= S_oled_csn & R_spi_miso(0) & sd_clk & sd_d(2) & sd_d(3) & sd_cmd & sd_d(0) & sd_d(1); -- beautiful but makes core unreliable

	//assign led[7] =  ~R_prog_release[(C_prog_release_timeout)];

  assign led[7] = wifi_gpio5; // for boards without D22 soldered
  assign led[6] = S_prog_out[1];  // green LED indicates ESP32 disabled
  assign led[5] =  ~R_prog_release[(C_prog_release_timeout)]; // ESP32 programming start: blinks too short to be visible
  
	// green LED indicates ESP32 disabled
	// assign led[3] = sd_d[3]; //led(3) <= sd_d(3); -- sd_d(3) is sd_cs, pullup=NONE in constraints otherwise SD card will prevents esp32 from entering programming mode...
	// assign led[2] = sd_d[2]; //led(2) <= sd_d(2);
	// assign led[1] = sd_d[1]; //led(1) <= sd_d(1);
	// assign led[0] = sd_d[0]; //led(0) <= sd_d(0);

  // programming release counter
  always @(posedge clk_25mhz) begin
    R_prog_in <= S_prog_in;
    if(S_prog_out == 2'b01 && R_prog_in == 2'b11) begin
      R_prog_release <= {(((C_prog_release_timeout))-((0))+1){1'b0}};
    end
    else begin
      if(R_prog_release[(C_prog_release_timeout)] == 1'b0) begin
        R_prog_release <= R_prog_release + 1;
      end
    end
  end

  always @(posedge sd_clk, posedge wifi_gpio17) begin : P1 // gpio17 is OLED CSn

    if(wifi_gpio17 == 1'b1) begin
      R_spi_miso <= {1'b0,btn}; // sample button state during csn=1
    end else begin
      R_spi_miso <= {R_spi_miso[((7)) - 1:0],R_spi_miso[(7)]}; // shift to the left
    end
  end
`
  // if user presses BTN0 and BTN1 then pull down PROGRAMN for multiboot
  always @(posedge clk_25mhz) begin
    if(btn[0] == 1'b0 && btn[1] == 1'b1) begin
      R_progn <= R_progn + 1;
      // BTN0 BTN1 are pressed
    end
    else begin
      R_progn <= {8{1'b0}};
      // BTN0 BTN1 are not pressed
    end
  end

  assign user_programn =  ~R_progn[(7)];

endmodule