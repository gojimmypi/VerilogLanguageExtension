//***************************************************************************
// 
//    Copyright (c) Microsoft Corporation. All rights reserved.
//    This code is licensed under the Visual Studio SDK license terms.
//    THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
//    ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
//    IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
//    PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//***************************************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using VerilogLanguage.VerilogToken;
namespace VerilogLanguage
{
    /// <summary>
    /// Factory for quick info sources
    /// </summary>
    [Export(typeof(IQuickInfoSourceProvider))]
    [ContentType("verilog")]
    [Name("VerilogQuickInfo")]
    class VerilogQuickInfoSourceProvider : IQuickInfoSourceProvider
    {

        [Import]
        IBufferTagAggregatorFactoryService aggService = null;

        public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            return new VerilogQuickInfoSource(textBuffer, aggService.CreateTagAggregator<VerilogToken.VerilogTokenTag>(textBuffer));
        }
    }

    /// <summary>
    /// Provides QuickInfo information to be displayed in a text buffer
    /// </summary>
    class VerilogQuickInfoSource : IQuickInfoSource
    {
        private ITagAggregator<VerilogToken.VerilogTokenTag> _aggregator;
        private ITextBuffer _buffer;
        private bool _disposed = false;
        IDictionary<VerilogToken.VerilogTokenTypes, string> _VerilogKeywordHoverText;
        //IDictionary<string, string> _VerilogVariableHoverText;


        public VerilogQuickInfoSource(ITextBuffer buffer, ITagAggregator<VerilogToken.VerilogTokenTag> aggregator)
        {
            _aggregator = aggregator;
            _buffer = buffer;

            // TODO - do we really want to call reparse here?
            VerilogGlobals.Reparse(buffer); // parse the buffer at file load time

            //_VerilogVariableHoverText = new Dictionary<string, string>
            //{
            //    ["led"] = "An LED.",
            //    // description text thanks: https://www.xilinx.com/support/documentation/sw_manuals/xilinx11/ite_r_verilog_reserved_words.htm
            //};

            _VerilogKeywordHoverText = new Dictionary<VerilogToken.VerilogTokenTypes, string>
            {
                // description text thanks: https://www.xilinx.com/support/documentation/sw_manuals/xilinx11/ite_r_verilog_reserved_words.htm
                [VerilogTokenTypes.Verilog_always] = "An always represents a block of code in a design.",
                [VerilogTokenTypes.Verilog_assign] = "The assign statement is used to express the first type of procedural continuous assignment. ",
                [VerilogTokenTypes.Verilog_automatic] = "The Verilog reserved word automatic is used in task and function declarations to maximize memory space.",
                [VerilogTokenTypes.Verilog_begin] = "A begin-end block is a means of grouping two or more procedural assignments together so that they act like a single group of sequential statements.",
                [VerilogTokenTypes.Verilog_case] = "The case reserved word is used in case statements. The case statement is a multiway decision statement that tests whether an expression matches one of a number of other expressions and branches accordingly.",
                [VerilogTokenTypes.Verilog_casex] = "The casex reserved word is a type of case statement provided to allow handling of don’t-care conditions in the case comparisons. Casex treats both high-impedance (x and z) values as don’t-cares.",
                [VerilogTokenTypes.Verilog_casez] = "The casez reserved word is a type of case statement provided to allow handling of don’t-care conditions in the case comparisons. Casez treats high-impedance (z) values as don’t-cares.",
                [VerilogTokenTypes.Verilog_cell] = "The term cell indicates a specific library element to use and is used only inside of a configuration statement.",
                [VerilogTokenTypes.Verilog_config] = "The term config defines a block of code that allows the use of a specific library element for a particular instantiation.",
                [VerilogTokenTypes.Verilog_deassign] = "The deassign statement is used to end a procedural continuous assignment.",
                [VerilogTokenTypes.Verilog_default] = "The default statement defines a default branch for a choice case expression. Default assigns an output value, to avoid inferring a latch.",
                [VerilogTokenTypes.Verilog_defparam] = "The defparam statement is used to override or modify parameters from one module to another. Defparam will allow assignment to parameters using their hierarchical names. The defparam statement is useful for grouping all of the parameter value override assignments together in one module.",
                [VerilogTokenTypes.Verilog_design] = "The term design indicates which module to use. The term can only be used within a config statement.",
                [VerilogTokenTypes.Verilog_disable] = "The disable statement terminates the activity associated with currently active procedures. The disable statement terminates a task before it executes all its statements, breaking from a looping statement, or skipping statements in order to continue with another iteration of a looping statement. It is useful for handling exception conditions.",
                [VerilogTokenTypes.Verilog_edge] = "The edge-control specifiers may be used to control events in timing checks based on specific edge transitions between 0, 1, and x.",
                [VerilogTokenTypes.Verilog_else] = "Optional clause in an if statement. An else clause specifies alternative statements when the if clause and any else if clauses evaluate false.",
                [VerilogTokenTypes.Verilog_end] = "Marks the end of a statement, subprogram, or declaration of a library unit.",
                [VerilogTokenTypes.Verilog_endcase] = "The endcase reserved word is used to close case, casex, and casez statements.",
                [VerilogTokenTypes.Verilog_endconfig] = "Ends the config block of code.",
                [VerilogTokenTypes.Verilog_endfunction] = "A function declaration begins with the reserved word function and ends with the reserved word endfunction. The purpose of a function is to return a value that is to be used in an expression.",
                [VerilogTokenTypes.Verilog_endgenerate] = "Generate blocks are used to create multiple instances of an object within a module. The reserved word endgenerate closes a generate block.",
                [VerilogTokenTypes.Verilog_endmodule] = "The endmodule reserved word is used to close a module statement.",
                [VerilogTokenTypes.Verilog_endprimitive] = "The endprimitive reserved word terminates a UDP declaration.",
                [VerilogTokenTypes.Verilog_endspecify] = "The endspecify reserved word closes a specify block. Specify blocks can be used to describe various paths across the module, assign delays to those paths, and to perform timing checks to ensure that events occurring at the module inputs satisfy the timing constraints of the device described by the module.",
                [VerilogTokenTypes.Verilog_endtable] = "The reserved word endtable terminates a state table. State tables define the behavior of a UDP.",
                [VerilogTokenTypes.Verilog_endtask] = "The endtask reserved word closes a task statement. You may define procedures or tasks that enable you to execute the same code from many different places in your description. Tasks are also useful in breaking up large procedures into smaller, more manageable tasks. Tasks may return more than one value and may contain timing controls. You can disable tasks in the same manner as named blocks.",
                [VerilogTokenTypes.Verilog_event] = "The event reserved word is used to declare a data type. Events can be made to occur from a procedure. This allows you to control the enabling of multiple actions in other procedures.",
                [VerilogTokenTypes.Verilog_for] = "Controls the execution of associated statements by a three-step process. Executes an assignment normally used to initialize a register that controls the number of loops executed. Evaluates an expression: If the result is a zero, the for-loop exits. If the result is not zero, the for-loop executes its associated statements. Executes an assignment normally used to modify the value of the loop-control register, then repeats step 2.",
                [VerilogTokenTypes.Verilog_force] = "Force is a form of procedural continuous assignment. A force assignment can be applied to nets and registers. A force statement to a register will override a procedural assignment or procedural continuous assignment that takes place on the register until a release procedural statement is executed on the register.",
                [VerilogTokenTypes.Verilog_forever] = "The forever looping statement provides a means of controlling the execution of a statement zero, one, or more times. Forever will continuously execute a statement.",
                [VerilogTokenTypes.Verilog_fork] = "The fork reserved word opens a fork-join block. A fork-join is a means of grouping together two or more procedural assignments so that they act like a single group of concurrent statements.",
                [VerilogTokenTypes.Verilog_function] = "A function declaration begins with the reserved word function and ends with the reserved word endfunction. The purpose of a function is to return a value that is to be used in an expression.",
                [VerilogTokenTypes.Verilog_generate] = "Generate blocks are used to create multiple instances of an object within a module. The reserved word generate begins a generate block.",
                [VerilogTokenTypes.Verilog_genvar] = "The genvar reserved word is used as the index control variable by generate for loops. The genvar variable is restricted to a positive or 0 value. Negative values, X, and Z values cannot be assigned to a genvar variable.",
                [VerilogTokenTypes.Verilog_if] = "Conditional logic statement. Presents a condition to be evaluated as true or false.",
                [VerilogTokenTypes.Verilog_ifnone] = "The ifnone reserved word is used to specify a default state-dependent path delay when all other conditions for the path are false. The ifnone condition specifies the same module path source and destination as the state-dependent module paths. The following rules apply: Only simple module paths may be described with the ifnone condition. The state-dependent paths that correspond to the ifnone path may be either simple module paths or edge-sensitive paths. If there are no corresponding state-dependent module paths to the ifnone module path, then the ifnone module path is treated the same as an unconditional module path. It is illegal to specify both an ifnone condition for a module path, and an unconditional simple module path for the same module path.",
                [VerilogTokenTypes.Verilog_incdir] = "A command to specify what directory a library resides.",
                [VerilogTokenTypes.Verilog_include] = "A compiler directive pointing to the location of a header file. The include directive can be located anywhere in a Verilog file.",
                [VerilogTokenTypes.Verilog_initial] = "The initial construct is enabled at the beginning of a simulation and executes only once. Its activity ends when the statement has finished. The is no implied order of execution between initial and always constructs.",
                [VerilogTokenTypes.Verilog_inout] = "The reserved word inout is a port definition providing a means of interconnecting hardware descriptions consisting of modules, primitives, and macromodules. For example, module X can instantiate module Y, using port connections appropriate to module X. These port names can differ from the names of the internal nets and registers specified in the definition of module Y.",
                [VerilogTokenTypes.Verilog_input] = "The reserved word input is a port definition providing a means of interconnecting hardware descriptions consisting of modules, primitives, and macromodules. For example, module X can instantiate module Y, using port connections appropriate to module X. These port names can differ from the names of the internal nets and registers specified in the definition of module Y.",
                [VerilogTokenTypes.Verilog_instance] = "The term instance is used within the config block to pick the specific instantiation on which to apply a library element.",
                [VerilogTokenTypes.Verilog_join] = "The join reserved word closes a fork-join block. A fork-join is a means of grouping together two or more procedural assignments so that they act like a single group of concurrent statements.",
                [VerilogTokenTypes.Verilog_liblist] = "The specific library element that applies to the label named in the instance section of the config construct.",
                [VerilogTokenTypes.Verilog_library] = "A logical collection of design elements. The term library is used only in the config block.",
                [VerilogTokenTypes.Verilog_localparam] = "Local parameters are identical to parameters, but cannot be directly modified with a defparam statement. A localparam can be assigned the value of parameter constants, and can be indirectly refined from outside the module. A localparam cannot be used within the module port parameter list.",
                [VerilogTokenTypes.Verilog_macromodule] = "The macromodule reserved word can be used interchangeably with the reserved word module. An implementation can choose to treat module definitions beginning with the macromodule reserved word differently.",
                [VerilogTokenTypes.Verilog_module] = "The module declaration is the only design unit in Verilog. It describes both a design’s interface to other designs in the same environment, and its functional composition.",
                [VerilogTokenTypes.Verilog_negedge] = "Value changes on nets and registers can be used as events to trigger the execution of a statement. This is known as detecting an implicit event. The event can be based on the direction of the change towards the value 1 (posedge) or towards the value 0 (negedge).",
                [VerilogTokenTypes.Verilog_noshowcancelled] = "Under certain simulation conditions the trailing edge of a pulse and be scheduled before the leading edge of the pulse. This discrepancy can be noted with the use of the showcanceled in which case the simulation would show an unknown (’X’) during the time. The term noshowcanceled puts the simulator in the default mode by ignoring the condition.",
                [VerilogTokenTypes.Verilog_output] = "The reserved word output is a port definition providing a means of interconnecting hardware descriptions consisting of modules, primitives, and macromodules. For example, module X can instantiate module Y, using port connections appropriate to module X. These port names can differ from the names of the internal nets and registers specified in the definition of module Y.",
                [VerilogTokenTypes.Verilog_parameter] = "Parameters are constants that can be modified with the defparam statement or through parameter passing in the module instance statement. Parameters are local to the module in which they have been declared.",
                [VerilogTokenTypes.Verilog_posedge] = "Value changes on nets and registers can be used as events to trigger the execution of a statement. This is known as detecting an implicit event. The event can be based on the direction of the change towards the value 1 (posedge) or towards the value 0 (negedge).",
                [VerilogTokenTypes.Verilog_primitive] = "The reserved word primitive begins a UDP definition, and is followed by an identifier the name of the UDP.",
                [VerilogTokenTypes.Verilog_pulsestyle_ondetect] = "Use for modeling only. Propagates a logic X value to the output as soon as the second input event occurs.",
                [VerilogTokenTypes.Verilog_pulsestyle_onevent] = "Use for modeling only. Propagates a logic X value to output only between two scheduled events.",
                [VerilogTokenTypes.Verilog_reg] = "A reg is an abstraction of a data storage element. The reg stores a value from one assignment to the next. An assignment statement in a procedure acts as a trigger that changes the value in the data storage element. Reg data types can only be assigned inside of the procedural, initial, always blocks or when they are declared.",
                [VerilogTokenTypes.Verilog_release] = "The release reserved word closes a forced procedural continuous assignment. A force assignment can be applied to nets and registers. A force statement to a register will override a procedural assignment or procedural continuous assignment that takes place on the register until a release procedural statement is executed on the register.",
                [VerilogTokenTypes.Verilog_repeat] = "Executes a statement a fixed number of times. If the expression evaluates to unknown, or high impedance, it will be treated as a zero, and no statement will be executed.",
                [VerilogTokenTypes.Verilog_scalared] = "Scalared is an optional advisory reserved word used in vector net or reg declaration. If this reserved word is implemented, certain operations on vectors may be restricted. If the reserved word scalared is used, bit and part selects of the object will be allowed, and the PLI will consider the object expanded.",
                [VerilogTokenTypes.Verilog_showcancelled] = "Use for modeling only. Use in conjunction with pulsestyle_ondetect in a specify block to vary how the logic X propagates to the output for negative pulse detection.",
                [VerilogTokenTypes.Verilog_signed] = "Declare reg variables and all net data types using the reserved word signed. The signed reserved word can also be placed on module port declarations. When either the date type or the port is declared signed, the other inherits the property of the signed data type or port. Note Signed operations can be performed with vectors of any size.",
                [VerilogTokenTypes.Verilog_specify] = "The specify reserved word opens a specify block. Specify blocks can be used to describe various paths across the module, assign delays to those paths, and perform timing checks to ensure that events occurring at the module inputs satisfy the timing constraints of the device described by the module.",
                [VerilogTokenTypes.Verilog_specparam] = "The reserved word specparam declares parameters within specify blocks called specify parameters (specparams), to distinguish them from module parameters.",
                [VerilogTokenTypes.Verilog_strength] = "The reserved word strength specifies drive strength for a gate instance. You can specify the output drive strengths for both 0 and 1 values when you instantiate a gate. When you declare drive strengths, you must specify both the 1 and 0 strengths unless the instance is a pulldown or pullup gate. When you don’t specify strengths, the defaults are strong1, and strong0.",
                [VerilogTokenTypes.Verilog_table] = "The reserved word table begins a state table. State tables define the behavior of a UDP.",
                [VerilogTokenTypes.Verilog_task] = "The task reserved word opens a task statement. You may define procedures, or tasks that allow you to execute the same code from many different places in your description. Tasks are also useful in breaking up large procedures into smaller, more manageable blocks. Tasks may return more than one value and may contain timing controls. You can disable tasks in the same manner as named blocks.",
                [VerilogTokenTypes.Verilog_tri] = "Tri nets connect elements. The net-type tri is identical in syntax and function to the net-type wire. A tri net-type can be used where multiple drivers drive a net.",
                [VerilogTokenTypes.Verilog_tri0] = "The tri0 model nets with resistive pulldown and resistive pullup devices on them. When no driver drives a tri0 net, its value is 0. When no drive drives a tri1 net, its value is 1. The strength of this value is pull.",
                [VerilogTokenTypes.Verilog_tri1] = "The tri1 model nets with resistive pulldown and resistive pullup devices on them. When no driver drives a tri0 net, its value is 0. When no drive drives a tri1 net, its value is 1. The strength of this value is pull.",
                [VerilogTokenTypes.Verilog_triand] = "Triand, trior, wand and wor, are types of wired nets used to model wired logic configurations. Wired nets use truth tables to resolve the conflicts that result when multiple drivers drive the same net. The triand and wand nets create wired and configurations, such that if any driver is 0, the value of the net is also 0. Wand and triand are identical in their syntax and functionality. The trior and wor nets create wired configurations, such that when any of the drivers is 1, the resulting value of the net is also 1. Wor and trior are identical in their syntax and functionality.",
                [VerilogTokenTypes.Verilog_wand] = "Triand, trior, wand and wor, are types of wired nets used to model wired logic configurations. Wired nets use truth tables to resolve the conflicts that result when multiple drivers drive the same net. The triand and wand nets create wired and configurations, such that if any driver is 0, the value of the net is also 0. Wand and triand are identical in their syntax and functionality. The trior and wor nets create wired configurations, such that when any of the drivers is 1, the resulting value of the net is also 1. Wor and trior are identical in their syntax and functionality.",
                [VerilogTokenTypes.Verilog_trior] = "Triand, trior, wand and wor, are types of wired nets used to model wired logic configurations. Wired nets use truth tables to resolve the conflicts that result when multiple drivers drive the same net. The triand and wand nets create wired and configurations, such that if any driver is 0, the value of the net is also 0. Wand and triand are identical in their syntax and functionality. The trior and wor nets create wired configurations, such that when any of the drivers is 1, the resulting value of the net is also 1. Wor and trior are identical in their syntax and functionality.",
                [VerilogTokenTypes.Verilog_wor] = "Triand, trior, wand and wor, are types of wired nets used to model wired logic configurations. Wired nets use truth tables to resolve the conflicts that result when multiple drivers drive the same net. The triand and wand nets create wired and configurations, such that if any driver is 0, the value of the net is also 0. Wand and triand are identical in their syntax and functionality. The trior and wor nets create wired configurations, such that when any of the drivers is 1, the resulting value of the net is also 1. Wor and trior are identical in their syntax and functionality.",
                [VerilogTokenTypes.Verilog_trireg] = "The trireg net stores a value and is used to model charge storage nodes. A trireg can be one of two states: Driven State: When one or more drivers of a trireg has a value of 1, 0, or x; the value spreads into the trireg and is the driven value of a trireg. The strength of a trireg in the driven state is strong, pull, or weak depending on the strength of the driver. Capacitive State: When all the drivers of a trireg net are at the high-impedance value (z), the trireg net retains its last driven value; the high-impedance value does not spread from the driver to the trireg. The strength of the value on the trireg net in the capacitive state is small, medium, or large, depending on the size specified in the declaration of the trireg.",
                [VerilogTokenTypes.Verilog_unsigned] = "Declares a signal to be unsigned (this is the default behavior).",
                [VerilogTokenTypes.Verilog_use] = "The use term tells the compiler which element to use in a library. The term is used within the config block.",
                [VerilogTokenTypes.Verilog_vectored] = "Vectored is an optional advisory reserved word used in vector net or reg declaration. If this reserved word is implemented, certain operations on vectors may be restricted. If the reserved word vectored is used, bit and part selects and strength specifications may not be allowed, and the PLI may consider the object unexpanded.",
                [VerilogTokenTypes.Verilog_wait] = "The wait statement evaluates a condition, and if it is false, the procedural statements following the wait statement remain blocked until that condition becomes true before continuing.",
                [VerilogTokenTypes.Verilog_while] = "The while reserved word executes a statement until an expression becomes false. If the expression starts out false, the statement is not executed at all.",
                [VerilogTokenTypes.Verilog_wire] = "Wire nets connect elements. The net-type wire is identical in syntax and function to the net-type tri. A wire net can be used for nets that are driven by a single gate or continuous assignment. Logical conflicts from multiple sources on a wire net result in unknown values unless the net is controlled by logic strength.",


                [VerilogTokenTypes.Verilog_Directive] = ""
            };
        }

        /// <summary>
        /// Determine which pieces of Quickinfo content should be displayed
        /// </summary>
        public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan)
        {
            applicableToSpan = null;

            if (_disposed)
                throw new ObjectDisposedException("TestQuickInfoSource");

            var triggerPoint = (SnapshotPoint) session.GetTriggerPoint(_buffer.CurrentSnapshot);

            if (triggerPoint == null)
                return;



            foreach (IMappingTagSpan<VerilogToken.VerilogTokenTag> curTag in _aggregator.GetTags(new SnapshotSpan(triggerPoint, triggerPoint)))
            {
                // here we add hover text at runtime 
                if (_VerilogKeywordHoverText.Keys.Contains(curTag.Tag.type)) {
                    var tagSpan = curTag.Span.GetSpans(_buffer).First();
                    applicableToSpan = _buffer.CurrentSnapshot.CreateTrackingSpan(tagSpan, SpanTrackingMode.EdgeExclusive);
                    quickInfoContent.Add(_VerilogKeywordHoverText[curTag.Tag.type]);
                }
                else
                { // 
                    var thisTag = curTag.ToString(); //TODO is this hwere to add verilog variables?
                    var tagSpan = curTag.Span.GetSpans(_buffer).First();
                    string thisHoverKey = tagSpan.GetText();

                    if (VerilogGlobals.VerilogVariableHoverText.Keys.Contains(thisHoverKey))
                    {
                        applicableToSpan = _buffer.CurrentSnapshot.CreateTrackingSpan(tagSpan, SpanTrackingMode.EdgeExclusive);
                        quickInfoContent.Add(VerilogGlobals.VerilogVariableHoverText[thisHoverKey]);
                    }
                }

                //if (curTag.Tag.type == VerilogTokenTypes.Verilog_always)
                //{
                //    var tagSpan = curTag.Span.GetSpans(_buffer).First();
                //    applicableToSpan = _buffer.CurrentSnapshot.CreateTrackingSpan(tagSpan, SpanTrackingMode.EdgeExclusive);
                //    quickInfoContent.Add(_VerilogKeywordHoverText[VerilogTokenTypes.Verilog_always]);
                //}
                //else if (curTag.Tag.type == VerilogTokenTypes.Verilog_begin)
                //{
                //    var tagSpan = curTag.Span.GetSpans(_buffer).First();
                //    applicableToSpan = _buffer.CurrentSnapshot.CreateTrackingSpan(tagSpan, SpanTrackingMode.EdgeExclusive);
                //    quickInfoContent.Add("Question Begin?");
                //}
            }
        }
        
        public void Dispose()
        {
            _disposed = true;
        }
    }
}

