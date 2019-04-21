`timescale 1 ns / 100 ps
	wire i_clk;

`ifdef VERILATOR
/* verilator lint_off UNUSED */		
// this is a sample
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

  always @(posedge i_clk) begin
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

