module my_din(
	// parameter [31:0] MASKED_IRQ = 32'h0000_0000,  
	parameter [31:0] LATCHED_IRQ = 32'h 0001_0000,
	input test 
 )
   din == 0 ;
   parameter [31:0] LATCHED_IRQ2 = 32'h    fff4_ffff;  
   LATCHED_IRQ = 1; 
 endmodule  