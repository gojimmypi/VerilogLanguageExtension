module wow( 
           input wire a    
         );

    localparam ctr_width1 = 24;
    localparam ctr_max = 2**(ctr_width) - 1; // TODO these are not actually valid in module declaration
	a = a + 1;
endmodule

// BCD (Binary Coded Decimal) counter
module bcd8_increment (
	input [7:0] din,
	output reg [7:0] unsigned dout
);
    localparam ctr_width = 24;
	parameter d = 45;
	d = d + 1;
	always @* begin
		case (1'b1)
			din[7:0] == 8'h 99:
				dout = 0;
			din[3:0] == 4'h 9:
				dout = {din[7:4] + 4'd 1, 4'h 0};
			default:
				dout = {din[7:4], din[3:0] + 4'd 1};
		endcase
	end
endmodule
