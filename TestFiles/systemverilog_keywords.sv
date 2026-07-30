module systemverilog_keywords (
    input  logic clk,
    input  logic d,
    output logic q
);

bit enabled;

always_ff @(posedge clk) begin
    q <= d;
end

always_comb begin
    enabled = d;
end

// synthesis translate_off
final $display("enabled=%b", enabled);
// synthesis translate_on

endmodule
