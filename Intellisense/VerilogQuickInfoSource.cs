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
            return new VerilogQuickInfoSource(textBuffer, aggService.CreateTagAggregator<VerilogTokenTag>(textBuffer));
        }
    }

    /// <summary>
    /// Provides QuickInfo information to be displayed in a text buffer
    /// </summary>
    class VerilogQuickInfoSource : IQuickInfoSource
    {
        private ITagAggregator<VerilogTokenTag> _aggregator;
        private ITextBuffer _buffer;
        private bool _disposed = false;
        IDictionary<VerilogTokenTypes, string> _VerilogKeywordHoverText;


        public VerilogQuickInfoSource(ITextBuffer buffer, ITagAggregator<VerilogTokenTag> aggregator)
        {
            _aggregator = aggregator;
            _buffer = buffer;
            _VerilogKeywordHoverText = new Dictionary<VerilogTokenTypes, string>
            {
                // description text thanks: https://www.xilinx.com/support/documentation/sw_manuals/xilinx11/ite_r_verilog_reserved_words.htm
                [VerilogTokenTypes.Verilog_always] = "An always represents a block of code in a design.",
                [VerilogTokenTypes.Verilog_assign] = "The assign statement is used to express the first type of procedural continuous assignment. ",
                [VerilogTokenTypes.Verilog_automatic] = "The Verilog reserved word automatic is used in task and function declarations to maximize memory space.",
                [VerilogTokenTypes.Verilog_begin] = "A begin-end block is a means of grouping two or more procedural assignments together so that they act like a single group of sequential statements.",
                [VerilogTokenTypes.Verilog_case] = "The case reserved word is used in case statements. The case statement is a multiway decision statement that tests whether an expression matches one of a number of other expressions and branches accordingly.",
                [VerilogTokenTypes.Verilog_casex] = "The casex reserved word is a type of case statement provided to allow handling of don’t-care conditions in the case comparisons. Casex treats both high-impedance (x and z) values as don’t-cares.",
                [VerilogTokenTypes.Verilog_casez] = "The casez reserved word is a type of case statement provided to allow handling of don’t-care conditions in the case comparisons. Casez treats high-impedance (z) values as don’t-cares.",
                [VerilogTokenTypes.Verilog_cell] = "",
                [VerilogTokenTypes.Verilog_config] = "",
                [VerilogTokenTypes.Verilog_deassign] = "",
                [VerilogTokenTypes.Verilog_default] = "",
                [VerilogTokenTypes.Verilog_defparam] = "",
                [VerilogTokenTypes.Verilog_design] = "",
                [VerilogTokenTypes.Verilog_disable] = "",
                [VerilogTokenTypes.Verilog_edge] = "",
                [VerilogTokenTypes.Verilog_else] = "",
                [VerilogTokenTypes.Verilog_end] = "",
                [VerilogTokenTypes.Verilog_endcase] = "",
                [VerilogTokenTypes.Verilog_endconfig] = "",
                [VerilogTokenTypes.Verilog_endfunction] = "",
                [VerilogTokenTypes.Verilog_endgenerate] = "",
                [VerilogTokenTypes.Verilog_endmodule] = "",
                [VerilogTokenTypes.Verilog_endprimitive] = "",
                [VerilogTokenTypes.Verilog_endspecify] = "",
                [VerilogTokenTypes.Verilog_endtable] = "",
                [VerilogTokenTypes.Verilog_endtask] = "",
                [VerilogTokenTypes.Verilog_event] = "",
                [VerilogTokenTypes.Verilog_for] = "",
                [VerilogTokenTypes.Verilog_force] = "",
                [VerilogTokenTypes.Verilog_forever] = "",
                [VerilogTokenTypes.Verilog_fork] = "",
                [VerilogTokenTypes.Verilog_function] = "",
                [VerilogTokenTypes.Verilog_generate] = "",
                [VerilogTokenTypes.Verilog_genvar] = "",
                [VerilogTokenTypes.Verilog_if] = "",
                [VerilogTokenTypes.Verilog_ifnone] = "",
                [VerilogTokenTypes.Verilog_incdir] = "",
                [VerilogTokenTypes.Verilog_include] = "",
                [VerilogTokenTypes.Verilog_initial] = "",
                [VerilogTokenTypes.Verilog_inout] = "",
                [VerilogTokenTypes.Verilog_input] = "",
                [VerilogTokenTypes.Verilog_instance] = "",
                [VerilogTokenTypes.Verilog_join] = "",
                [VerilogTokenTypes.Verilog_liblist] = "",
                [VerilogTokenTypes.Verilog_library] = "",
                [VerilogTokenTypes.Verilog_localparam] = "",
                [VerilogTokenTypes.Verilog_macromodule] = "",
                [VerilogTokenTypes.Verilog_module] = "",
                [VerilogTokenTypes.Verilog_negedge] = "",
                [VerilogTokenTypes.Verilog_noshowcancelled] = "",
                [VerilogTokenTypes.Verilog_output] = "",
                [VerilogTokenTypes.Verilog_parameter] = "",
                [VerilogTokenTypes.Verilog_posedge] = "",
                [VerilogTokenTypes.Verilog_primitive] = "",
                [VerilogTokenTypes.Verilog_pulsestyle_ondetect] = "",
                [VerilogTokenTypes.Verilog_pulsestyle_onevent] = "",
                [VerilogTokenTypes.Verilog_reg] = "",
                [VerilogTokenTypes.Verilog_release] = "",
                [VerilogTokenTypes.Verilog_repeat] = "",
                [VerilogTokenTypes.Verilog_scalared] = "",
                [VerilogTokenTypes.Verilog_showcancelled] = "",
                [VerilogTokenTypes.Verilog_signed] = "",
                [VerilogTokenTypes.Verilog_specify] = "",
                [VerilogTokenTypes.Verilog_specparam] = "",
                [VerilogTokenTypes.Verilog_strength] = "",
                [VerilogTokenTypes.Verilog_table] = "",
                [VerilogTokenTypes.Verilog_task] = "",
                [VerilogTokenTypes.Verilog_tri] = "",
                [VerilogTokenTypes.Verilog_tri0] = "",
                [VerilogTokenTypes.Verilog_tri1] = "",
                [VerilogTokenTypes.Verilog_triand] = "",
                [VerilogTokenTypes.Verilog_wand] = "",
                [VerilogTokenTypes.Verilog_trior] = "",
                [VerilogTokenTypes.Verilog_wor] = "",
                [VerilogTokenTypes.Verilog_trireg] = "",
                [VerilogTokenTypes.Verilog_unsigned] = "",
                [VerilogTokenTypes.Verilog_use] = "",
                [VerilogTokenTypes.Verilog_vectored] = "",
                [VerilogTokenTypes.Verilog_wait] = "",
                [VerilogTokenTypes.Verilog_while] = "",
                [VerilogTokenTypes.Verilog_wire] = "",

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



            foreach (IMappingTagSpan<VerilogTokenTag> curTag in _aggregator.GetTags(new SnapshotSpan(triggerPoint, triggerPoint)))
            {
                if (_VerilogKeywordHoverText.Keys.Contains(curTag.Tag.type)) {
                    var tagSpan = curTag.Span.GetSpans(_buffer).First();
                    applicableToSpan = _buffer.CurrentSnapshot.CreateTrackingSpan(tagSpan, SpanTrackingMode.EdgeExclusive);
                    quickInfoContent.Add(_VerilogKeywordHoverText[curTag.Tag.type]);
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

