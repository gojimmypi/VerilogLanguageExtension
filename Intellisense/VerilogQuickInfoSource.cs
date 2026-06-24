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
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using VerilogLanguage.VerilogToken;

namespace VerilogLanguage
{
    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [ContentType("verilog")]
    [Name("VerilogQuickInfo")]
    internal sealed class VerilogAsyncQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
    {
        [Import]
        private IBufferTagAggregatorFactoryService aggService = null;

        public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
            if (textBuffer == null || aggService == null) {
                return null;
            }

            // Return a fresh QuickInfo source. The source owns its tag aggregator and
            // disposes it when VS disposes the source. The shared token tagger intentionally
            // does not implement IDisposable, so disposing this temporary aggregator cannot
            // poison the singleton token tagger used by classification and later hovers.
            return new VerilogAsyncQuickInfoSource(
                textBuffer,
                aggService.CreateTagAggregator<VerilogTokenTag>(textBuffer));
        }
    }

    internal sealed class VerilogAsyncQuickInfoSource : IAsyncQuickInfoSource
    {
        private readonly ITagAggregator<VerilogTokenTag> _aggregator;
        private readonly ITextBuffer _buffer;
        private bool _disposed;

        internal static readonly IDictionary<VerilogToken.VerilogTokenTypes, string> VerilogKeywordHoverText = new Dictionary<VerilogToken.VerilogTokenTypes, string>
        {
            // description text thanks: https://www.xilinx.com/support/documentation/sw_manuals/xilinx11/ite_r_verilog_reserved_words.htm
            [VerilogToken.VerilogTokenTypes.Verilog_always] = "An always represents a block of code in a design.",
            [VerilogToken.VerilogTokenTypes.Verilog_assign] = "The assign statement is used to express the first type of procedural continuous assignment. Assign of reg data types is not supported. ",
            [VerilogToken.VerilogTokenTypes.Verilog_automatic] = "The Verilog reserved word automatic is used in task and function declarations to maximize memory space.",
            [VerilogToken.VerilogTokenTypes.Verilog_begin] = "A begin-end block is a means of grouping two or more procedural assignments together so that they act like a single group of sequential statements.",
            [VerilogToken.VerilogTokenTypes.Verilog_case] = "The case reserved word is used in case statements. The case statement is a multiway decision statement that tests whether an expression matches one of a number of other expressions and branches accordingly.",
            [VerilogToken.VerilogTokenTypes.Verilog_casex] = "The casex reserved word is a type of case statement provided to allow handling of don't-care conditions in the case comparisons. Casex treats both high-impedance (x and z) values as don't-cares.",
            [VerilogToken.VerilogTokenTypes.Verilog_casez] = "The casez reserved word is a type of case statement provided to allow handling of don't-care conditions in the case comparisons. Casez treats high-impedance (z) values as don't-cares.",
            [VerilogToken.VerilogTokenTypes.Verilog_cell] = "The term cell indicates a specific library element to use and is used only inside of a configuration statement.",
            [VerilogToken.VerilogTokenTypes.Verilog_config] = "The term config defines a block of code that allows the use of a specific library element for a particular instantiation.",
            [VerilogToken.VerilogTokenTypes.Verilog_deassign] = "The deassign statement is used to end a procedural continuous assignment.",
            [VerilogToken.VerilogTokenTypes.Verilog_default] = "The default statement defines a default branch for a choice case expression. Default assigns an output value, to avoid inferring a latch.",
            [VerilogToken.VerilogTokenTypes.Verilog_defparam] = "The defparam statement is used to override or modify parameters from one module to another. Defparam will allow assignment to parameters using their hierarchical names. The defparam statement is useful for grouping all of the parameter value override assignments together in one module.",
            [VerilogToken.VerilogTokenTypes.Verilog_design] = "The term design indicates which module to use. The term can only be used within a config statement.",
            [VerilogToken.VerilogTokenTypes.Verilog_disable] = "The disable statement terminates the activity associated with currently active procedures. The disable statement terminates a task before it executes all its statements, breaking from a looping statement, or skipping statements in order to continue with another iteration of a looping statement. It is useful for handling exception conditions.",
            [VerilogToken.VerilogTokenTypes.Verilog_edge] = "The edge-control specifiers may be used to control events in timing checks based on specific edge transitions between 0, 1, and x.",
            [VerilogToken.VerilogTokenTypes.Verilog_else] = "Optional clause in an if statement. An else clause specifies alternative statements when the if clause and any else if clauses evaluate false.",
            [VerilogToken.VerilogTokenTypes.Verilog_end] = "Marks the end of a statement, subprogram, or declaration of a library unit.",
            [VerilogToken.VerilogTokenTypes.Verilog_endcase] = "The endcase reserved word is used to close case, casex, and casez statements.",
            [VerilogToken.VerilogTokenTypes.Verilog_endconfig] = "Ends the config block of code.",
            [VerilogToken.VerilogTokenTypes.Verilog_endfunction] = "A function declaration begins with the reserved word function and ends with the reserved word endfunction. The purpose of a function is to return a value that is to be used in an expression.",
            [VerilogToken.VerilogTokenTypes.Verilog_endgenerate] = "Generate blocks are used to create multiple instances of an object within a module. The reserved word endgenerate closes a generate block.",
            [VerilogToken.VerilogTokenTypes.Verilog_endmodule] = "The endmodule reserved word is used to close a module statement.",
            [VerilogToken.VerilogTokenTypes.Verilog_endprimitive] = "The endprimitive reserved word terminates a UDP declaration.",
            [VerilogToken.VerilogTokenTypes.Verilog_endspecify] = "The endspecify reserved word closes a specify block. Specify blocks can be used to describe various paths across the module, assign delays to those paths, and to perform timing checks to ensure that events occurring at the module inputs satisfy the timing constraints of the device described by the module.",
            [VerilogToken.VerilogTokenTypes.Verilog_endtable] = "The reserved word endtable terminates a state table. State tables define the behavior of a UDP.",
            [VerilogToken.VerilogTokenTypes.Verilog_endtask] = "The endtask reserved word closes a task statement. You may define procedures or tasks that enable you to execute the same code from many different places in your description. Tasks are also useful in breaking up large procedures into smaller, more manageable tasks. Tasks may return more than one value and may contain timing controls. You can disable tasks in the same manner as named blocks.",
            [VerilogToken.VerilogTokenTypes.Verilog_event] = "Not Supported in Synthesis: The event reserved word is used to declare a data type. Events can be made to occur from a procedure. This allows you to control the enabling of multiple actions in other procedures.",
            [VerilogToken.VerilogTokenTypes.Verilog_for] = "Controls the execution of associated statements by a three-step process. Executes an assignment normally used to initialize a register that controls the number of loops executed. Evaluates an expression: If the result is a zero, the for-loop exits. If the result is not zero, the for-loop executes its associated statements. Executes an assignment normally used to modify the value of the loop-control register, then repeats step 2.",
            [VerilogToken.VerilogTokenTypes.Verilog_force] = "Not Supported in Synthesis: Force is a form of procedural continuous assignment. A force assignment can be applied to nets and registers. A force statement to a register will override a procedural assignment or procedural continuous assignment that takes place on the register until a release procedural statement is executed on the register.",
            [VerilogToken.VerilogTokenTypes.Verilog_forever] = "The forever looping statement provides a means of controlling the execution of a statement zero, one, or more times. Forever will continuously execute a statement.",
            [VerilogToken.VerilogTokenTypes.Verilog_fork] = "Not Supported in Synthesis: The fork reserved word opens a fork-join block. A fork-join is a means of grouping together two or more procedural assignments so that they act like a single group of concurrent statements.",
            [VerilogToken.VerilogTokenTypes.Verilog_function] = "A function declaration begins with the reserved word function and ends with the reserved word endfunction. The purpose of a function is to return a value that is to be used in an expression.",
            [VerilogToken.VerilogTokenTypes.Verilog_generate] = "Generate blocks are used to create multiple instances of an object within a module. The reserved word generate begins a generate block.",
            [VerilogToken.VerilogTokenTypes.Verilog_genvar] = "The genvar reserved word is used as the index control variable by generate for loops. The genvar variable is restricted to a positive or 0 value. Negative values, X, and Z values cannot be assigned to a genvar variable.",
            [VerilogToken.VerilogTokenTypes.Verilog_if] = "Conditional logic statement. Presents a condition to be evaluated as true or false.",
            [VerilogToken.VerilogTokenTypes.Verilog_ifnone] = "The ifnone reserved word is used to specify a default state-dependent path delay when all other conditions for the path are false. The ifnone condition specifies the same module path source and destination as the state-dependent module paths. The following rules apply: Only simple module paths may be described with the ifnone condition. The state-dependent paths that correspond to the ifnone path may be either simple module paths or edge-sensitive paths. If there are no corresponding state-dependent module paths to the ifnone module path, then the ifnone module path is treated the same as an unconditional module path. It is illegal to specify both an ifnone condition for a module path, and an unconditional simple module path for the same module path.",
            [VerilogToken.VerilogTokenTypes.Verilog_incdir] = "A command to specify what directory a library resides.",
            [VerilogToken.VerilogTokenTypes.Verilog_include] = "A compiler directive pointing to the location of a header file. The include directive can be located anywhere in a Verilog file.",
            [VerilogToken.VerilogTokenTypes.Verilog_initial] = "Not Supported in Synthesis: The initial construct is enabled at the beginning of a simulation and executes only once. Its activity ends when the statement has finished. The is no implied order of execution between initial and always constructs.",
            [VerilogToken.VerilogTokenTypes.Verilog_inout] = "The reserved word inout is a port definition providing a means of interconnecting hardware descriptions consisting of modules, primitives, and macromodules. For example, module X can instantiate module Y, using port connections appropriate to module X. These port names can differ from the names of the internal nets and registers specified in the definition of module Y.",
            [VerilogToken.VerilogTokenTypes.Verilog_input] = "The reserved word input is a port definition providing a means of interconnecting hardware descriptions consisting of modules, primitives, and macromodules. For example, module X can instantiate module Y, using port connections appropriate to module X. These port names can differ from the names of the internal nets and registers specified in the definition of module Y.",
            [VerilogToken.VerilogTokenTypes.Verilog_instance] = "The term instance is used within the config block to pick the specific instantiation on which to apply a library element.",
            [VerilogToken.VerilogTokenTypes.Verilog_join] = "Not Supported in Synthesis: The join reserved word closes a fork-join block. A fork-join is a means of grouping together two or more procedural assignments so that they act like a single group of concurrent statements.",
            [VerilogToken.VerilogTokenTypes.Verilog_liblist] = "The specific library element that applies to the label named in the instance section of the config construct.",
            [VerilogToken.VerilogTokenTypes.Verilog_library] = "A logical collection of design elements. The term library is used only in the config block.",
            [VerilogToken.VerilogTokenTypes.Verilog_localparam] = "Local parameters are identical to parameters, but cannot be directly modified with a defparam statement. A localparam can be assigned the value of parameter constants, and can be indirectly refined from outside the module. A localparam cannot be used within the module port parameter list.",
            [VerilogToken.VerilogTokenTypes.Verilog_macromodule] = "The macromodule reserved word can be used interchangeably with the reserved word module. An implementation can choose to treat module definitions beginning with the macromodule reserved word differently.",
            [VerilogToken.VerilogTokenTypes.Verilog_module] = "The module declaration is the only design unit in Verilog. It describes both a design's interface to other designs in the same environment, and its functional composition.",
            [VerilogToken.VerilogTokenTypes.Verilog_negedge] = "Value changes on nets and registers can be used as events to trigger the execution of a statement. This is known as detecting an implicit event. The event can be based on the direction of the change towards the value 1 (posedge) or towards the value 0 (negedge).",
            [VerilogToken.VerilogTokenTypes.Verilog_noshowcancelled] = "Under certain simulation conditions the trailing edge of a pulse and be scheduled before the leading edge of the pulse. This discrepancy can be noted with the use of the showcanceled in which case the simulation would show an unknown (`X`) during the time. The term noshowcanceled puts the simulator in the default mode by ignoring the condition.",
            [VerilogToken.VerilogTokenTypes.Verilog_output] = "The reserved word output is a port definition providing a means of interconnecting hardware descriptions consisting of modules, primitives, and macromodules. For example, module X can instantiate module Y, using port connections appropriate to module X. These port names can differ from the names of the internal nets and registers specified in the definition of module Y.",
            [VerilogToken.VerilogTokenTypes.Verilog_parameter] = "Parameters are constants that can be modified with the defparam statement or through parameter passing in the module instance statement. Parameters are local to the module in which they have been declared.",
            [VerilogToken.VerilogTokenTypes.Verilog_posedge] = "Value changes on nets and registers can be used as events to trigger the execution of a statement. This is known as detecting an implicit event. The event can be based on the direction of the change towards the value 1 (posedge) or towards the value 0 (negedge).",
            [VerilogToken.VerilogTokenTypes.Verilog_primitive] = "Only gate level primitives are supported. The reserved word primitive begins a UDP definition, and is followed by an identifier the name of the UDP.",
            [VerilogToken.VerilogTokenTypes.Verilog_pulsestyle_ondetect] = "Use for modeling only. Propagates a logic X value to the output as soon as the second input event occurs.",
            [VerilogToken.VerilogTokenTypes.Verilog_pulsestyle_onevent] = "Use for modeling only. Propagates a logic X value to output only between two scheduled events.",
            [VerilogToken.VerilogTokenTypes.Verilog_reg] = "A reg is an abstraction of a data storage element. The reg stores a value from one assignment to the next. An assignment statement in a procedure acts as a trigger that changes the value in the data storage element. Reg data types can only be assigned inside of the procedural, initial, always blocks or when they are declared.",
            [VerilogToken.VerilogTokenTypes.Verilog_release] = "Not Supported in Synthesis: The release reserved word closes a forced procedural continuous assignment. A force assignment can be applied to nets and registers. A force statement to a register will override a procedural assignment or procedural continuous assignment that takes place on the register until a release procedural statement is executed on the register.",
            [VerilogToken.VerilogTokenTypes.Verilog_repeat] = "Executes a statement a fixed number of times. If the expression evaluates to unknown, or high impedance, it will be treated as a zero, and no statement will be executed.",
            [VerilogToken.VerilogTokenTypes.Verilog_scalared] = "Scalared is an optional advisory reserved word used in vector net or reg declaration. If this reserved word is implemented, certain operations on vectors may be restricted. If the reserved word scalared is used, bit and part selects of the object will be allowed, and the PLI will consider the object expanded.",
            [VerilogToken.VerilogTokenTypes.Verilog_showcancelled] = "Use for modeling only. Use in conjunction with pulsestyle_ondetect in a specify block to vary how the logic X propagates to the output for negative pulse detection.",
            [VerilogToken.VerilogTokenTypes.Verilog_signed] = "Declare reg variables and all net data types using the reserved word signed. The signed reserved word can also be placed on module port declarations. When either the date type or the port is declared signed, the other inherits the property of the signed data type or port. Note Signed operations can be performed with vectors of any size.",
            [VerilogToken.VerilogTokenTypes.Verilog_specify] = "The specify reserved word opens a specify block. Specify blocks can be used to describe various paths across the module, assign delays to those paths, and perform timing checks to ensure that events occurring at the module inputs satisfy the timing constraints of the device described by the module.",
            [VerilogToken.VerilogTokenTypes.Verilog_specparam] = "The reserved word specparam declares parameters within specify blocks called specify parameters (specparams), to distinguish them from module parameters.",
            [VerilogToken.VerilogTokenTypes.Verilog_strength] = "The reserved word strength specifies drive strength for a gate instance. You can specify the output drive strengths for both 0 and 1 values when you instantiate a gate. When you declare drive strengths, you must specify both the 1 and 0 strengths unless the instance is a pulldown or pullup gate. When you don't specify strengths, the defaults are strong1, and strong0.",
            [VerilogToken.VerilogTokenTypes.Verilog_table] = "Not Supported in Synthesis: The reserved word table begins a state table. State tables define the behavior of a UDP.",
            [VerilogToken.VerilogTokenTypes.Verilog_task] = "The task reserved word opens a task statement. You may define procedures, or tasks that allow you to execute the same code from many different places in your description. Tasks are also useful in breaking up large procedures into smaller, more manageable blocks. Tasks may return more than one value and may contain timing controls. You can disable tasks in the same manner as named blocks.",
            [VerilogToken.VerilogTokenTypes.Verilog_tri] = "Tri nets connect elements. The net-type tri is identical in syntax and function to the net-type wire. A tri net-type can be used where multiple drivers drive a net.",
            [VerilogToken.VerilogTokenTypes.Verilog_tri0] = "The tri0 model nets with resistive pulldown and resistive pullup devices on them. When no driver drives a tri0 net, its value is 0. When no drive drives a tri1 net, its value is 1. The strength of this value is pull.",
            [VerilogToken.VerilogTokenTypes.Verilog_tri1] = "The tri1 model nets with resistive pulldown and resistive pullup devices on them. When no driver drives a tri0 net, its value is 0. When no drive drives a tri1 net, its value is 1. The strength of this value is pull.",
            [VerilogToken.VerilogTokenTypes.Verilog_triand] = "Triand, trior, wand and wor, are types of wired nets used to model wired logic configurations. Wired nets use truth tables to resolve the conflicts that result when multiple drivers drive the same net. The triand and wand nets create wired and configurations, such that if any driver is 0, the value of the net is also 0. Wand and triand are identical in their syntax and functionality. The trior and wor nets create wired configurations, such that when any of the drivers is 1, the resulting value of the net is also 1. Wor and trior are identical in their syntax and functionality.",
            [VerilogToken.VerilogTokenTypes.Verilog_wand] = "Triand, trior, wand and wor, are types of wired nets used to model wired logic configurations. Wired nets use truth tables to resolve the conflicts that result when multiple drivers drive the same net. The triand and wand nets create wired and configurations, such that if any driver is 0, the value of the net is also 0. Wand and triand are identical in their syntax and functionality. The trior and wor nets create wired configurations, such that when any of the drivers is 1, the resulting value of the net is also 1. Wor and trior are identical in their syntax and functionality.",
            [VerilogToken.VerilogTokenTypes.Verilog_trior] = "Triand, trior, wand and wor, are types of wired nets used to model wired logic configurations. Wired nets use truth tables to resolve the conflicts that result when multiple drivers drive the same net. The triand and wand nets create wired and configurations, such that if any driver is 0, the value of the net is also 0. Wand and triand are identical in their syntax and functionality. The trior and wor nets create wired configurations, such that when any of the drivers is 1, the resulting value of the net is also 1. Wor and trior are identical in their syntax and functionality.",
            [VerilogToken.VerilogTokenTypes.Verilog_wor] = "Triand, trior, wand and wor, are types of wired nets used to model wired logic configurations. Wired nets use truth tables to resolve the conflicts that result when multiple drivers drive the same net. The triand and wand nets create wired and configurations, such that if any driver is 0, the value of the net is also 0. Wand and triand are identical in their syntax and functionality. The trior and wor nets create wired configurations, such that when any of the drivers is 1, the resulting value of the net is also 1. Wor and trior are identical in their syntax and functionality.",
            [VerilogToken.VerilogTokenTypes.Verilog_trireg] = "The trireg net stores a value and is used to model charge storage nodes. A trireg can be one of two states: Driven State: When one or more drivers of a trireg has a value of 1, 0, or x; the value spreads into the trireg and is the driven value of a trireg. The strength of a trireg in the driven state is strong, pull, or weak depending on the strength of the driver. Capacitive State: When all the drivers of a trireg net are at the high-impedance value (z), the trireg net retains its last driven value; the high-impedance value does not spread from the driver to the trireg. The strength of the value on the trireg net in the capacitive state is small, medium, or large, depending on the size specified in the declaration of the trireg.",
            [VerilogToken.VerilogTokenTypes.Verilog_unsigned] = "Declares a signal to be unsigned (this is the default behavior).",
            [VerilogToken.VerilogTokenTypes.Verilog_use] = "The use term tells the compiler which element to use in a library. The term is used within the config block.",
            [VerilogToken.VerilogTokenTypes.Verilog_vectored] = "Vectored is an optional advisory reserved word used in vector net or reg declaration. If this reserved word is implemented, certain operations on vectors may be restricted. If the reserved word vectored is used, bit and part selects and strength specifications may not be allowed, and the PLI may consider the object unexpanded.",
            [VerilogToken.VerilogTokenTypes.Verilog_wait] = "The wait statement evaluates a condition, and if it is false, the procedural statements following the wait statement remain blocked until that condition becomes true before continuing.",
            [VerilogToken.VerilogTokenTypes.Verilog_while] = "The while reserved word executes a statement until an expression becomes false. If the expression starts out false, the statement is not executed at all.",
            [VerilogToken.VerilogTokenTypes.Verilog_wire] = "Wire nets connect elements. The net-type wire is identical in syntax and function to the net-type tri. A wire net can be used for nets that are driven by a single gate or continuous assignment. Logical conflicts from multiple sources on a wire net result in unknown values unless the net is controlled by logic strength.",

            [VerilogToken.VerilogTokenTypes.Verilog_Primitive_and] = "Gate primitive: N-input AND gate",
            [VerilogToken.VerilogTokenTypes.Verilog_Primitive_nand] = "Gate primitive: N-input NAND gate",
            [VerilogToken.VerilogTokenTypes.Verilog_Primitive_or] = "Gate primitive: N - input OR gate",
            [VerilogToken.VerilogTokenTypes.Verilog_Primitive_nor] = "Gate primitive: N-input NOR gate",
            [VerilogToken.VerilogTokenTypes.Verilog_Primitive_xor] = "Gate primitive: N-input XOR gate",
            [VerilogToken.VerilogTokenTypes.Verilog_Primitive_xnor] = "Gate primitive: N-input XNOR gate",
            [VerilogToken.VerilogTokenTypes.Verilog_Primitive_not] = "Gate primitive: NOT gate",

            [VerilogToken.VerilogTokenTypes.Verilog_Directive] = "Verilog directive"
        };

        private readonly IDictionary<VerilogToken.VerilogTokenTypes, string> _verilogKeywordHoverText;

        public VerilogAsyncQuickInfoSource(
            ITextBuffer buffer,
            ITagAggregator<VerilogTokenTag> aggregator) {
            _buffer = buffer;
            _aggregator = aggregator;

            _verilogKeywordHoverText = VerilogKeywordHoverText;
        }

        public Task<QuickInfoItem> GetQuickInfoItemAsync(
            IAsyncQuickInfoSession session,
            CancellationToken cancellationToken) {
            if (_disposed || cancellationToken.IsCancellationRequested || session == null) {
                return Task.FromResult<QuickInfoItem>(null);
            }

            SnapshotPoint? triggerPoint = session.GetTriggerPoint(_buffer.CurrentSnapshot);
            if (!triggerPoint.HasValue) {
                return Task.FromResult<QuickInfoItem>(null);
            }

            SnapshotPoint trigger = triggerPoint.Value;
            ITextSnapshot snapshot = trigger.Snapshot;
            if (snapshot == null || snapshot.Length == 0) {
                return Task.FromResult<QuickInfoItem>(null);
            }

            int probePosition = trigger.Position;
            if (probePosition >= snapshot.Length) {
                probePosition = snapshot.Length - 1;
            }
            if (probePosition < 0) {
                return Task.FromResult<QuickInfoItem>(null);
            }

            SnapshotSpan probeSpan = new SnapshotSpan(snapshot, probePosition, 1);
            List<IMappingTagSpan<VerilogTokenTag>> tags;
            try {
                tags = _aggregator.GetTags(probeSpan).ToList();
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine("VerilogAsyncQuickInfoSource.GetTags failed: " + ex.Message);
                return Task.FromResult<QuickInfoItem>(null);
            }

            foreach (IMappingTagSpan<VerilogTokenTag> curTag in tags) {
                cancellationToken.ThrowIfCancellationRequested();

                // Normalize mapping span to the current snapshot for this buffer.
                var spans = curTag.Span.GetSpans(_buffer.CurrentSnapshot);
                if (spans == null || spans.Count == 0) {
                    continue;
                }

                SnapshotSpan tagSpan = spans[0];

                ITrackingSpan applicableToSpan = _buffer.CurrentSnapshot.CreateTrackingSpan(
                    tagSpan,
                    SpanTrackingMode.EdgeExclusive);

                string hover;
                if (VerilogHoverInfo.TryGetHoverText(curTag.Tag.type, _buffer.CurrentSnapshot, tagSpan, out hover)) {
                    return Task.FromResult(new QuickInfoItem(applicableToSpan, hover));
                }

            }

            return Task.FromResult<QuickInfoItem>(null);
        }

        public void Dispose() {
            if (_disposed) {
                return;
            }

            _disposed = true;

            IDisposable disposableAggregator = _aggregator as IDisposable;
            if (disposableAggregator != null) {
                disposableAggregator.Dispose();
            }
        }
    }

    internal static class VerilogHoverInfo
    {
        internal static bool TryGetHoverText(
            VerilogToken.VerilogTokenTypes tokenType,
            ITextSnapshot snapshot,
            SnapshotSpan tagSpan,
            out string hoverText) {

            hoverText = null;

            string keywordHover;
            if (VerilogAsyncQuickInfoSource.VerilogKeywordHoverText.TryGetValue(tokenType, out keywordHover)) {
                hoverText = keywordHover;
                return true;
            }

            return TryGetVariableHoverText(snapshot, tagSpan, out hoverText);
        }

        internal static bool TryGetVariableHoverText(
            ITextSnapshot snapshot,
            SnapshotSpan tagSpan,
            out string hoverText) {

            hoverText = null;

            string thisHoverKey = tagSpan.GetText();
            if (string.IsNullOrWhiteSpace(thisHoverKey)) {
                return false;
            }

            ITextSnapshot spanSnapshot = snapshot ?? tagSpan.Snapshot;
            if (spanSnapshot == null) {
                return false;
            }

            ITextSnapshotLine lineInfo = spanSnapshot.GetLineFromPosition(tagSpan.Start.Position);

            int thisLine = lineInfo.LineNumber;
            int thisPosition = tagSpan.Start.Position - lineInfo.Extent.Start.Position;

            string thisScopeName = null;
            Dictionary<string, Dictionary<string, string>> hoverDb = null;

            string thisFile = VerilogGlobals.GetDocumentPath(spanSnapshot);
            VerilogGlobals.ParseDataSnapshot parseData;
            if (VerilogGlobals.TryGetParseData(thisFile, spanSnapshot.Version.VersionNumber, true, out parseData)) {
                thisScopeName = parseData.TextModuleName(thisLine, thisPosition);
                hoverDb = parseData.VerilogVariableHoverText;
            }
            else {
                thisScopeName = VerilogGlobals.TextModuleName(thisLine, thisPosition);
                hoverDb = VerilogGlobals.VerilogVariableHoverText;
            }

            if (string.IsNullOrWhiteSpace(thisScopeName)) {
                thisScopeName = VerilogGlobals.SCOPE_CONST;
            }

            if (hoverDb == null) {
                return false;
            }

            Dictionary<string, string> scopeMap;
            string variableHover;
            if (hoverDb.TryGetValue(thisScopeName, out scopeMap) &&
                scopeMap != null &&
                scopeMap.TryGetValue(thisHoverKey, out variableHover)) {
                hoverText = variableHover;
                return true;
            }

            Dictionary<string, string> constMap;
            string constHover;
            if (hoverDb.TryGetValue(VerilogGlobals.SCOPE_CONST, out constMap) &&
                constMap != null &&
                constMap.TryGetValue(thisHoverKey, out constHover)) {
                hoverText = constHover;
                return true;
            }

            string macroHoverKey = thisHoverKey;
            if (macroHoverKey.StartsWith("`", StringComparison.Ordinal)) {
                macroHoverKey = macroHoverKey.Substring(1);
            }

            Dictionary<string, string> macroMap;
            string macroHover;
            if (hoverDb.TryGetValue(VerilogGlobals.SCOPE_MACRO, out macroMap) &&
                macroMap != null &&
                macroMap.TryGetValue(macroHoverKey, out macroHover)) {
                hoverText = macroHover;
                return true;
            }

            return false;
        }
    }
}
