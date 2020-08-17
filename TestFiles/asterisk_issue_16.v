module picorv32 #(
	parameter [ 0:0] ENABLE_IRQ = 0
)
 
localparam rf = 4/ENABLE_IRQ /* */ 
wire a = rf*4;

// see also file: issue16.v


