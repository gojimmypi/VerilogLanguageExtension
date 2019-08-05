// see https://twitter.com/jangray/status/1158094604681396225?s=20

logic q = 0;
always_ff @(posedge clk) q <= ~q; // 0 1 0 1 0 1 ...
wire inv_q = ~q; // 1 0 1 0 1 0 ...
logic oops = ~q; // 1 1 1 1 1 1 ...

It's new to SV. V doesn't have 'logic'.



Requires semantic analysis.
logic ok = 0; // OK constant 0
logic also_ok = ~ok; // OK constant 1

logic ok2 = 0; // OK, initially 0
always ... ok2 = ...; // subsequently changes
logic oops = ~ok2; // surprise, it's a constant 1, highlight this
