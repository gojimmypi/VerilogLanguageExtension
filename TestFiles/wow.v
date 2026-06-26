
module picorv32 (
	parameter [31:0] P1 = 32'h 0000,
	parameter [31:0] P2 = 32'h 0000_0000
)
	reg [1:0] mem_state;

if (mem_state == 2 || mem_state == 3)




