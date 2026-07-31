module systemverilog_keywords (
    input  logic clk,
    input  logic d,
    output logic q
);

typedef logic [7:0] sample_t;

bit enabled;

function automatic sample_t sample_identity(
    input sample_t value
);
    sample_identity = value;
endfunction

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
