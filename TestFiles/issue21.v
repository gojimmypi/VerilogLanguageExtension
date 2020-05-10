//  from picorv32.v line 531
module demo(
	localparam  [31:0] LATCHED_IRQ5  = 32'hffff_0000; 
)
	reg [31:0] mem_rdata_q;
	wire [31:0] mem_rdata_latched = 32'hffff_ffff ;  
    localparam  [31:0] LATCHED_IRQ5 = 32'hffff_ffff, 
	assign S_prog_out = S_prog_in == 2'b10 ? 2'b01 : S_prog_in == 2'b01 ? 2'b10 : 2'b11;
	case (mem_rdata_latched[15:13])
		3'b110: begin // C.SWSP
			// issue #21 - mem_rdata_latched should be colorized in next line (note space after single quote fixes)
			{mem_rdata_q[31:25], mem_rdata_q[11:7]} <= {4 'b0, mem_rdata_latched[8:7], mem_rdata_latched[12:9], 2'b00};
			mem_rdata_q[14:12] <= 3'b 010;   
		end
	reg a = mem_rdata_latched;

endmodule