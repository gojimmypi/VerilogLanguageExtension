module top_ulx4m(
    input clk_25mhz,
    output [3:0] led
);

    reg [25:0] counter = 0;

    always @(posedge clk_25mhz) begin
        counter <= counter + 1;
    end

    assign led = counter[25:22];

endmodule
