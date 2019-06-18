//***************************************************************************
// 
//  MIT License
//
//  Copyright(c) 2019 gojimmypi
//
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in all
//  copies or substantial portions of the Software.
//
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//  SOFTWARE.
//
//***************************************************************************

namespace VerilogLanguage
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.Utilities;

    [Export(typeof(ITaggerProvider))]
    [ContentType("verilog")]
    [TagType(typeof(ClassificationTag))]
    internal sealed class VerilogClassifierProvider : ITaggerProvider
    {

        [Export]
        [Name("verilog")]
        [BaseDefinition("code")]
        [BaseDefinition("projection")]
        internal static ContentTypeDefinition VerilogContentType = null;

        [Export]
        [FileExtension(".v")]
        [ContentType("verilog")]
        [BaseDefinition("code")]
        [BaseDefinition("projection")]
        internal static FileExtensionToContentTypeDefinition VerilogFileType = null;

        [Import]
        internal IClassificationTypeRegistryService ClassificationTypeRegistry = null;

        [Import]
        internal IBufferTagAggregatorFactoryService aggregatorFactory = null;

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {

            ITagAggregator<VerilogTokenTag> VerilogTagAggregator =
                                            aggregatorFactory.CreateTagAggregator<VerilogTokenTag>(buffer);

            return new VerilogClassifier(buffer, VerilogTagAggregator, ClassificationTypeRegistry) as ITagger<T>;
        }
    }

    internal sealed class VerilogClassifier : ITagger<ClassificationTag>
    {
        ITextBuffer _buffer;
        ITagAggregator<VerilogTokenTag> _aggregator;
        IDictionary<VerilogTokenTypes, IClassificationType> _VerilogTypes;

        /// <summary>
        /// Construct the classifier and define search tokens
        /// </summary>
        internal VerilogClassifier(ITextBuffer buffer,
                               ITagAggregator<VerilogTokenTag> VerilogTagAggregator,
                               IClassificationTypeRegistryService typeService)
        {
            _buffer = buffer;
            _aggregator = VerilogTagAggregator;
            _VerilogTypes = new Dictionary<VerilogTokenTypes, IClassificationType>();
            _VerilogTypes[VerilogTokenTypes.Verilog_always] = typeService.GetClassificationType("always");
            _VerilogTypes[VerilogTokenTypes.Verilog_assign] = typeService.GetClassificationType("assign");

            _VerilogTypes[VerilogTokenTypes.Verilog_begin] = typeService.GetClassificationType("begin");
            _VerilogTypes[VerilogTokenTypes.Verilog_case] = typeService.GetClassificationType("case");
            _VerilogTypes[VerilogTokenTypes.Verilog_casex] = typeService.GetClassificationType("casex");
            _VerilogTypes[VerilogTokenTypes.Verilog_casez] = typeService.GetClassificationType("casez");
            _VerilogTypes[VerilogTokenTypes.Verilog_cell] = typeService.GetClassificationType("cell");
            _VerilogTypes[VerilogTokenTypes.Verilog_config] = typeService.GetClassificationType("config");
            _VerilogTypes[VerilogTokenTypes.Verilog_deassign] = typeService.GetClassificationType("deassign");
            _VerilogTypes[VerilogTokenTypes.Verilog_default] = typeService.GetClassificationType("default");
            _VerilogTypes[VerilogTokenTypes.Verilog_defparam] = typeService.GetClassificationType("defparam");
            _VerilogTypes[VerilogTokenTypes.Verilog_design] = typeService.GetClassificationType("design");
            _VerilogTypes[VerilogTokenTypes.Verilog_disable] = typeService.GetClassificationType("disable");
            _VerilogTypes[VerilogTokenTypes.Verilog_edge] = typeService.GetClassificationType("edge");
            _VerilogTypes[VerilogTokenTypes.Verilog_else] = typeService.GetClassificationType("else");
            _VerilogTypes[VerilogTokenTypes.Verilog_end] = typeService.GetClassificationType("end");
            _VerilogTypes[VerilogTokenTypes.Verilog_endcase] = typeService.GetClassificationType("endcase");
            _VerilogTypes[VerilogTokenTypes.Verilog_endconfig] = typeService.GetClassificationType("endconfig");
            _VerilogTypes[VerilogTokenTypes.Verilog_endfunction] = typeService.GetClassificationType("endfunction");
            _VerilogTypes[VerilogTokenTypes.Verilog_endgenerate] = typeService.GetClassificationType("endgenerate");
            _VerilogTypes[VerilogTokenTypes.Verilog_endmodule] = typeService.GetClassificationType("endmodule");
            _VerilogTypes[VerilogTokenTypes.Verilog_endprimitive] = typeService.GetClassificationType("endprimitive");
            _VerilogTypes[VerilogTokenTypes.Verilog_endspecify] = typeService.GetClassificationType("endspecify");
            _VerilogTypes[VerilogTokenTypes.Verilog_endtable] = typeService.GetClassificationType("endtable");
            _VerilogTypes[VerilogTokenTypes.Verilog_endtask] = typeService.GetClassificationType("endtask");
            _VerilogTypes[VerilogTokenTypes.Verilog_event] = typeService.GetClassificationType("event");
            _VerilogTypes[VerilogTokenTypes.Verilog_for] = typeService.GetClassificationType("for");
            _VerilogTypes[VerilogTokenTypes.Verilog_force] = typeService.GetClassificationType("force");
            _VerilogTypes[VerilogTokenTypes.Verilog_forever] = typeService.GetClassificationType("forever");
            _VerilogTypes[VerilogTokenTypes.Verilog_fork] = typeService.GetClassificationType("fork");
            _VerilogTypes[VerilogTokenTypes.Verilog_function] = typeService.GetClassificationType("function");
            _VerilogTypes[VerilogTokenTypes.Verilog_generate] = typeService.GetClassificationType("generate");
            _VerilogTypes[VerilogTokenTypes.Verilog_genvar] = typeService.GetClassificationType("genvar");
            _VerilogTypes[VerilogTokenTypes.Verilog_if] = typeService.GetClassificationType("if");
            _VerilogTypes[VerilogTokenTypes.Verilog_ifnone] = typeService.GetClassificationType("ifnone");
            _VerilogTypes[VerilogTokenTypes.Verilog_incdir] = typeService.GetClassificationType("incdir");
            _VerilogTypes[VerilogTokenTypes.Verilog_include] = typeService.GetClassificationType("include");
            _VerilogTypes[VerilogTokenTypes.Verilog_initial] = typeService.GetClassificationType("initial");
            _VerilogTypes[VerilogTokenTypes.Verilog_inout] = typeService.GetClassificationType("inout");
            _VerilogTypes[VerilogTokenTypes.Verilog_input] = typeService.GetClassificationType("input");
            _VerilogTypes[VerilogTokenTypes.Verilog_instance] = typeService.GetClassificationType("instance");
            _VerilogTypes[VerilogTokenTypes.Verilog_join] = typeService.GetClassificationType("join");
            _VerilogTypes[VerilogTokenTypes.Verilog_liblist] = typeService.GetClassificationType("liblist");
            _VerilogTypes[VerilogTokenTypes.Verilog_library] = typeService.GetClassificationType("library");
            _VerilogTypes[VerilogTokenTypes.Verilog_localparam] = typeService.GetClassificationType("localparam");
            _VerilogTypes[VerilogTokenTypes.Verilog_macromodule] = typeService.GetClassificationType("macromodule");
            _VerilogTypes[VerilogTokenTypes.Verilog_module] = typeService.GetClassificationType("module");
            _VerilogTypes[VerilogTokenTypes.Verilog_negedge] = typeService.GetClassificationType("negedge");
            _VerilogTypes[VerilogTokenTypes.Verilog_noshowcancelled] = typeService.GetClassificationType("noshowcancelled");
            _VerilogTypes[VerilogTokenTypes.Verilog_output] = typeService.GetClassificationType("output");
            _VerilogTypes[VerilogTokenTypes.Verilog_parameter] = typeService.GetClassificationType("parameter");
            _VerilogTypes[VerilogTokenTypes.Verilog_posedge] = typeService.GetClassificationType("posedge");
            _VerilogTypes[VerilogTokenTypes.Verilog_primitive] = typeService.GetClassificationType("primitive");
            _VerilogTypes[VerilogTokenTypes.Verilog_pulsestyle_ondetect] = typeService.GetClassificationType("pulsestyle_ondetect");
            _VerilogTypes[VerilogTokenTypes.Verilog_pulsestyle_onevent] = typeService.GetClassificationType("pulsestyle_onevent");
            _VerilogTypes[VerilogTokenTypes.Verilog_reg] = typeService.GetClassificationType("reg");
            _VerilogTypes[VerilogTokenTypes.Verilog_release] = typeService.GetClassificationType("release");
            _VerilogTypes[VerilogTokenTypes.Verilog_repeat] = typeService.GetClassificationType("repeat");
            _VerilogTypes[VerilogTokenTypes.Verilog_scalared] = typeService.GetClassificationType("scalared");
            _VerilogTypes[VerilogTokenTypes.Verilog_showcancelled] = typeService.GetClassificationType("showcancelled");
            _VerilogTypes[VerilogTokenTypes.Verilog_signed] = typeService.GetClassificationType("signed");
            _VerilogTypes[VerilogTokenTypes.Verilog_specify] = typeService.GetClassificationType("specify");
            _VerilogTypes[VerilogTokenTypes.Verilog_specparam] = typeService.GetClassificationType("specparam");
            _VerilogTypes[VerilogTokenTypes.Verilog_strength] = typeService.GetClassificationType("strength");
            _VerilogTypes[VerilogTokenTypes.Verilog_table] = typeService.GetClassificationType("table");
            _VerilogTypes[VerilogTokenTypes.Verilog_task] = typeService.GetClassificationType("task");
            _VerilogTypes[VerilogTokenTypes.Verilog_tri] = typeService.GetClassificationType("tri");
            _VerilogTypes[VerilogTokenTypes.Verilog_tri0] = typeService.GetClassificationType("tri0");
            _VerilogTypes[VerilogTokenTypes.Verilog_tri1] = typeService.GetClassificationType("tri1");
            _VerilogTypes[VerilogTokenTypes.Verilog_triand] = typeService.GetClassificationType("triand");
            _VerilogTypes[VerilogTokenTypes.Verilog_wand] = typeService.GetClassificationType("wand");
            _VerilogTypes[VerilogTokenTypes.Verilog_trior] = typeService.GetClassificationType("trior");
            _VerilogTypes[VerilogTokenTypes.Verilog_wor] = typeService.GetClassificationType("wor");
            _VerilogTypes[VerilogTokenTypes.Verilog_trireg] = typeService.GetClassificationType("trireg");
            _VerilogTypes[VerilogTokenTypes.Verilog_unsigned] = typeService.GetClassificationType("unsigned");
            _VerilogTypes[VerilogTokenTypes.Verilog_use] = typeService.GetClassificationType("use");
            _VerilogTypes[VerilogTokenTypes.Verilog_vectored] = typeService.GetClassificationType("vectored");
            _VerilogTypes[VerilogTokenTypes.Verilog_wait] = typeService.GetClassificationType("wait");
            _VerilogTypes[VerilogTokenTypes.Verilog_while] = typeService.GetClassificationType("while");
            _VerilogTypes[VerilogTokenTypes.Verilog_wire] = typeService.GetClassificationType("wire");

            _VerilogTypes[VerilogTokenTypes.Verilog_Directive] = typeService.GetClassificationType("directive"); // type must be one of VerilogTokenTagger 
            _VerilogTypes[VerilogTokenTypes.Verilog_Comment] = typeService.GetClassificationType("Comment"); // GetClassificationType string must be defined in ClassificationType.cs
            _VerilogTypes[VerilogTokenTypes.Verilog_Bracket] = typeService.GetClassificationType("Bracket");
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged
        {
            add { }
            remove { }
        }

        /// <summary>
        /// Search the given span for any instances of classified tags
        /// </summary>
        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            foreach (var tagSpan in _aggregator.GetTags(spans))
            {
                var tagSpans = tagSpan.Span.GetSpans(spans[0].Snapshot);
                // each of the text values found for tagSpan.Tag.type must be defined above in VerilogClassifier
                yield return
                    new TagSpan<ClassificationTag>(tagSpans[0],
                                                   new ClassificationTag(_VerilogTypes[tagSpan.Tag.type]));
            }
        }
    }
}

