// before
module my_din (
	parameter [31:0] LATCHED_IRQ = 32'h ffff_ffff,
	parameter [31:0] LATCHED_IRQ2 = 32'hffff_ffff,
	localparam  [31:0] LATCHED_IRQ4 = 32'hffff_ffff,


	localparam [35:0] TRACE_BRANCH = {4'b 0001, 32'b 0}; 

    //input [7:0] dinx, 
	input [7:0] din
);
	reg a,b;
	wire go = |{a, b};
	input clk;  
	LATCHED_IRQ = 0;
	input clk2,  
	output  trapdd, 
endmodule   