// after

module my_din (

	input [7:0] din
);
    localparam [35:0] TRACE_BRANCH = {4'b 0001, 32'b 0};  
	input clk;  
	LATCHED_IRQ = 0; 
	output reg trap,

	input clk2, clk4;  
	output trapdd;    
endmodule  

  // add32.v  verilog 32 bit adder (Verilog 2001 style)
module adder(input  [31:0] a);  // carry-out 
    parameter d=2;
	localparam integer irqregs_offset = ENABLE_REGS_16_31 ? 32 : 16;
	localparam integer regfile_size = (ENABLE_REGS_16_31 ? 32 : 16) + 4*ENABLE_IRQ*ENABLE_IRQ_QREGS;

	localparam [35:0] TRACE_IRQ    = {4'b 1000, 32'b 0};
	input [35:0] TRACE_IRQ3;    = {4'b 1000, 32'b 0};

endmodule // adder  