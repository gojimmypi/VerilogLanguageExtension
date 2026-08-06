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

module systemverilog_type_owner ();

typedef logic [7:0] shared_scope_name;

endmodule

module systemverilog_variable_owner ();

wire shared_scope_name;

endmodule

module systemverilog_type_owner_no_ports;

typedef logic [7:0] shared_no_ports_name;

endmodule

module systemverilog_variable_owner_no_ports;

wire shared_no_ports_name;

endmodule

module systemverilog_type_owner_parameterized_no_ports #(
    parameter integer WIDTH = 8
);

typedef logic [WIDTH - 1:0] shared_parameterized_no_ports_name;

endmodule

module systemverilog_variable_owner_parameterized_no_ports #(
    parameter integer WIDTH = 8
);

wire shared_parameterized_no_ports_name;

endmodule

module systemverilog_type_owner_parameterized_ports #(
    parameter integer WIDTH = 8
) ();

typedef logic [WIDTH - 1:0] shared_parameterized_ports_name;

shared_parameterized_ports_name = 3;

endmodule

module systemverilog_variable_owner_parameterized_ports #(
    parameter integer WIDTH = 8
) ();

wire shared_parameterized_ports_name;

shared_parameterized_ports_name = 4;

endmodule
