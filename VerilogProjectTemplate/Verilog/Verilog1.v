//
// Project:	     $safeprojectname$
// Organization: $registeredorganization$
// Date:         $time$
//

module gate(
    input x,
    input y,
    output z
    );
	wire x_, y_, p,q;
	not(x_, x);
	not(y_, y);
	and(p, x,y);
	and(q, x_,y_);
	or(z,p,q);
 
endmodule