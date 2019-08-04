// Listing 1.1
// this is a cxomment
// 

module eq1[] 
   (
    input wire k1, h1, i1, dd; 
    output wire eq
   );

   // signal declaration
   wire p02, p1; 

   // body
   // sum of two product terms
   assign eq = p0 | p1; 
   // product terms
   assign p0 = ~k1 & ~i1;  
   assign p1 = i0 & i1; 

endmodule
wire ne;
  assign shutdown = 0;

  wire    [1:0] S_prog_in;
  this tes();
  reg    [1:0] R_prog_in;  
  wire  [1:0] S_prog_out;
  reg  [7:0] R_spi_miso 
  wire [3] registeedr; ///
  wire  S_oled_csn;
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
  [] [] [] () {} 
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

endmodule// Listing 1.1
module eq1
   // I/O ports
   (
    input wire k1, h1, i1, 
    output wire eq
   );

   // signal declaration
   wire p02, p1; 

   // body
   // sum of two product terms
   assign eq = p0 | p1; 
   // product terms
   assign p0 = ~k1 & ~i1;  
   assign p1 = i0 & i1; 

endmodule


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
  [] [] [] () {} 
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

endmodule// Listing 1.1
module eq1
   // I/O ports
   (
    input wire k1, h1, i1, 
    output wire eq
   );

   // signal declaration
   wire p02, p1; 

   // body
   // sum of two product terms
   assign eq = p0 | p1; 
   // product terms
   assign p0 = ~k1 & ~i1;  
   assign p1 = i0 & i1; 

endmodule


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
  [] [] [] () {} 
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

endmodule// Listing 1.1
module eq1
   // I/O ports
   (
    input wire k1, h1, i1, 
    output wire eq
   );

   // signal declaration
   wire p02, p1; 

   // body
   // sum of two product terms
   assign eq = p0 | p1; 
   // product terms
   assign p0 = ~k1 & ~i1;  
   assign p1 = i0 & i1; 

endmodule


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
  [] [] [] () {} 
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

endmodule// Listing 1.1
module eq1
   // I/O ports
   (
    input wire k1, h1, i1, 
    output wire eq
   );

   // signal declaration
   wire p02, p1; 

   // body
   // sum of two product terms
   assign eq = p0 | p1; 
   // product terms
   assign p0 = ~k1 & ~i1;  
   assign p1 = i0 & i1; 

endmodule


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
  [] [] [] () {} 
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

endmodule// Listing 1.1
module eq1
   // I/O ports
   (
    input wire k1, h1, i1, 
    output wire eq
   );

   // signal declaration
   wire p02, p1; 

   // body
   // sum of two product terms
   assign eq = p0 | p1; 
   // product terms
   assign p0 = ~k1 & ~i1;  
   assign p1 = i0 & i1; 

endmodule


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
  [] [] [] () {} 
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

endmodule// Listing 1.1
module eq1
   // I/O ports
   (
    input wire k1, h1, i1, 
    output wire eq
   );

   // signal declaration
   wire p02, p1; 

   // body
   // sum of two product terms
   assign eq = p0 | p1; 
   // product terms
   assign p0 = ~k1 & ~i1;  
   assign p1 = i0 & i1; 

endmodule


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
  [] [] [] () {} 
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

endmodule// Listing 1.1
module eq1
   // I/O ports
   (
    input wire k1, h1, i1, 
    output wire eq
   );

   // signal declaration
   wire p02, p1; 

   // body
   // sum of two product terms
   assign eq = p0 | p1; 
   // product terms
   assign p0 = ~k1 & ~i1;  
   assign p1 = i0 & i1; 

endmodule


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
  [] [] [] () {} 
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

endmodule// Listing 1.1
module eq1
   // I/O ports
   (
    input wire k1, h1, i1, 
    output wire eq
   );

   // signal declaration
   wire p02, p1; 

   // body
   // sum of two product terms
   assign eq = p0 | p1; 
   // product terms
   assign p0 = ~k1 & ~i1;  
   assign p1 = i0 & i1; 

endmodule


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
  [] [] [] () {} 
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

endmodule// Listing 1.1
module eq1
   // I/O ports
   (
    input wire k1, h1, i1, 
    output wire eq
   );

   // signal declaration
   wire p02, p1; 

   // body
   // sum of two product terms
   assign eq = p0 | p1; 
   // product terms
   assign p0 = ~k1 & ~i1;  
   assign p1 = i0 & i1; 

endmodule


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
  [] [] [] () {} 
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

endmodule// Listing 1.1
module eq1
   // I/O ports
   (
    input wire k1, h1, i1, 
    output wire eq
   );

   // signal declaration
   wire p02, p1; 

   // body
   // sum of two product terms
   assign eq = p0 | p1; 
   // product terms
   assign p0 = ~k1 & ~i1;  
   assign p1 = i0 & i1; 

endmodule


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
  [] [] [] () {} 
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

endmodule// Listing 1.1
module eq1
   // I/O ports
   (
    input wire k1, h1, i1, 
    output wire eq
   );

   // signal declaration
   wire p02, p1; 

   // body
   // sum of two product terms
   assign eq = p0 | p1; 
   // product terms
   assign p0 = ~k1 & ~i1;  
   assign p1 = i0 & i1; 

endmodule


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
  fff
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
  [] [] [] () {} 
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

endmodule// Listing 1.1
module eq1
   // I/O ports
   (
    input wire k1, h1, i1, 
    output wire eq
   );

   // signal declaration
   wire p02, p1; 

   // body
   // sum of two product terms
   assign eq = p0 | p1; 
   // product terms
   assign p0 = ~k1 & ~i1;  
   assign p1 = i0 & i1; 

endmodule


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
  [] [] [] () {} 
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

endmodule// Listing 1.1
module eq1
   // I/O ports
   (
    input wire k1, h1, i1, 
    output wire eq
   );

   // signal declaration
   wire p02, p1; 

   // body
   // sum of two product terms
   assign eq = p0 | p1; 
   // product terms
   assign p0 = ~k1 & ~i1;  
   assign p1 = i0 & i1; 

endmodule


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
  [] [] [] () {} 
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

endmodule// Listing 1.1
module eq1
   // I/O ports
   (
    input wire k1, h1, i1, 
    output wire eq
   );

   // signal declaration
   wire p02, p1; 

   // body
   // sum of two product terms
   assign eq = p0 | p1; 
   // product terms
   assign p0 = ~k1 & ~i1;  
   assign p1 = i0 & i1; 

endmodule


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
  [] [] [] () {} 
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

endmodule// Listing 1.1
module eq1
   // I/O ports
   (
    input wire k1, h1, i1, 
    output wire eq
   );

   // signal declaration
   wire p02, p1; 

   // body
   // sum of two product terms
   assign eq = p0 | p1; 
   // product terms
   assign p0 = ~k1 & ~i1;  
   assign p1 = i0 & i1; 

endmodule


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
  [] [] [] () {} 
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

endmodule// Listing 1.1
module eq1
   // I/O ports
   (
    input wire k1, h1, i1, 
    output wire eq
   );

   // signal declaration
   wire p02, p1; 

   // body
   // sum of two product terms
   assign eq = p0 | p1; 
   // product terms
   assign p0 = ~k1 & ~i1;  
   assign p1 = i0 & i1; 

endmodule


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
  [] [] [] () {} 
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

  assign user_programn =  ~R_progn[(7)]
endmodule// Listing 1.1
module eq1
   // I/O ports
   (
    input wire k1, h1, i1, 
    output wire eq
   );

   // signal declaration
   wire p02, p1; 

   // body
   // sum of two product terms
   assign eq = p0 | p1; 
   // product terms
   assign p0 = ~k1 & ~i1;  
   assign p1 = i0 & i1; 

endmodule


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
  [] [] [] () {} 
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

//Before
assign[1:0];
assign   ;;;assign   ;;; // assign   
// this is a sample test; [delim]	[tab] ;;// test
assign;// this is a test 
assign/* test */ wow; assignassign;assign;;;assign;;;assigns; //test  [never] at the ;; end;  noope [ ] */ still a comment /* this too */ yes, even this
assign	assign /* tab dellmited*/ this is not a comment // this is; as is [this]
assign/* test */ wow; assignassign;assign;;;assign;;;assigns; //test  [never] at the ;; end;  noope [ ] */ still a comment /* this too */ yes, even this
assign	assign /* tab dellmited*/ this is not a comment // this is; as is [this]
assign;// this is a testassign;// this is a testassign;// this is a testbracket delimited*/
assign]other bracket /*bracket not a delimiter */
more;options;assign;stuff;always;
more;options assign;stuff always;
more;options;assign stuff;alwaysandevermmore
[//////
`timescale 1 ns / 100 ps
	wire i_clk;  
	     
`ifdef VERILATOR
/* verilator lint_off UNUSED */		
// this is a sample test; [delim]	[tab] ;;// test
module ulx3s_adda (
  input  i_clk, 
  input  reset,
  output [7:0] o_led,
	 output o_AD_CLK,
	 input  [7:0] J2_AD_PORT,
	 output o_DA_CLK,
	 output [7:0] J2_DA_PORT);
/* verilator lint_on UNUSED */

    wire i_clk;
    wire [7:0] o_led;
	
  wire o_AD_CLK;
  wire  [7:0] i_ad_port_value;

  wire o_DA_CLK;
  wire [7:0] o_value;

`else


module top(
  input clk_25mhz,
//	input reset,
  output [7:0] led,

  output J2_AD_CLK,
  input  [7:0] J2_AD_PORT,

  output J2_DA_CLK,
  output [7:0] J2_DA_PORT,

  output wifi_gpio0
);
	wire i_clk;
	assign i_clk = clk_25mhz;

	// Tie GPIO0 high to keep board from rebooting
    assign wifi_gpio0 = 1'b1;

	//wire i_reset;
	//assign i_reset = btn[0];


	// A/D Input Clock
	// "The pipelined architecture of the AD9280 operates on both rising and falling edges of the input clock.
	// The AD9280 is designed to support a conversion rate of 32 MSPS; running the part at slightly faster clock rates may
	// be possible, although at reduced performance levels." (see AD9280 datasheet, page 15)
	wire o_AD_CLK;
	assign J2_AD_CLK = o_AD_CLK; // = i_clk;
	assign o_AD_CLK = clk_25mhz;

	// D/A Outout Clock
	// AD9708 TxDAC: 125 MSPS Update Rate
	// "The DAC output is updated following the rising edge of the clock as shown in Figure 1 and is designed to support a
	// clock rate as high as 125 MSPS."
	// output propagation delay tPD is typically 1ns (see AD9708 datasheet page 3)
	wire o_DA_CLK;
	assign J2_DA_CLK = o_DA_CLK;
	assign o_DA_CLK = clk_25mhz;

	reg [7:0] o_led;
	assign led = o_led;

	reg[7:0] o_value;
	assign J2_DA_PORT = o_value;

	reg [7:0] i_ad_port_value;
	// assign i_ad_port_value[7:0] = J2_AD_PORT[7:0];
`endif	

	
	assign o_AD_CLK = i_clk;
	assign o_DA_CLK = i_clk;
	assign J2_DA_PORT = o_value;

	// on the falling edge of the i_clk we update the D/A output
	// TODO - does this introduce phase shift? (yes, probably)
	//always @(negedge i_clk) begin
	//	 J2_DA_PORT[7:0] <= i_ad_port_value[7:0];
	//end

	localparam ctr_width = 32;
    reg [ctr_width-1:0] ctr = 32'b1111_1111_1111_1111_1111_1111_1111_1111;

	// 14ns after edge, data is stable (we'll use 16ns)
	specify 
		(J2_AD_PORT => i_ad_port_value) = 16;
	endspecify

  always @( posedge) begin
		// 14ns after edge, data is stable
		ctr <= ctr + 1;
		 i_ad_port_value[7:0] <= J2_AD_PORT[7:0];
		// o_value[7:0] <= i_ad_port_value; // J2_AD_PORT[7:0] && ctr[7:0];
		// o_led[5:0] <= o_value[7:0]; // ctr[23:18];
		// works: o_led[6:6] <= ctr[23:23];
		// works: o_led[7:0] <= i_ad_port_value[7:0];
		o_led[7:0] <= i_ad_port_value[7:0];
		// J2_DA_PORT[7:0] <= i_ad_port_value[7:0];
		// o_led[0:0] <=  ctr[23:23];
		// J2_AD_CLK <= ctr[0:0];
		// J2_DA_CLK <=   ctr[0:0];
		o_value[7:0] <= i_ad_port_value[7:0];
		//reset <= i_reset;
	end

endmodule

