module my_din(
	parameter [31:0] MASKED_IRQ = 32'H 0000_0000,
	parameter [5:5] WOWWOW = 32'o ff,
	parameter [6:6] WOWWOWw = 32'b ff,
	output reg [31:0] mem_addr,
	parameter [31:0] LATCHED_IR,
	wire mywire, 
	input test 
 )
	reg a,b;
	wire go = |{a, b}; 	input clk, resetn,
	output reg trap,
 localparam [35:0] TRACE_BRANCH = {4'b 0001, 32'b 0}; 
	localparam integer irq_timer = 0;
	localparam integer irq_ebreak = 1;
	parameter integer irq_buserror = 2;
   din == 0 ;
   parameter [31:0] LATCHED_IRQ2 = 32'h    fff4_ffff;  
   parameter [31:0] LATCHED_IRQ3 = 32'h    fff4_ffff;  
   LATCHED_IRQ = 1;
 endmodule
 
 module myff_din (
	parameter [31:0] MASKED_IRQz = 32'h0000_0000,
 	parameter [31:0] LATCHED_IRQ = 32'h ffff_ffff;
    input [7:0] din
);
    din[7:0] == 0;
	din =1;   
	LATCHED_IRQ = 1;  
endmodule 
 

 
