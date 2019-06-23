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
            _VerilogTypes = new Dictionary<VerilogTokenTypes, IClassificationType>
            {
                [VerilogTokenTypes.Verilog_always] = typeService.GetClassificationType("always"),
                [VerilogTokenTypes.Verilog_assign] = typeService.GetClassificationType("assign"),

                [VerilogTokenTypes.Verilog_begin] = typeService.GetClassificationType("begin"),
                [VerilogTokenTypes.Verilog_case] = typeService.GetClassificationType("case"),
                [VerilogTokenTypes.Verilog_casex] = typeService.GetClassificationType("casex"),
                [VerilogTokenTypes.Verilog_casez] = typeService.GetClassificationType("casez"),
                [VerilogTokenTypes.Verilog_cell] = typeService.GetClassificationType("cell"),
                [VerilogTokenTypes.Verilog_config] = typeService.GetClassificationType("config"),
                [VerilogTokenTypes.Verilog_deassign] = typeService.GetClassificationType("deassign"),
                [VerilogTokenTypes.Verilog_default] = typeService.GetClassificationType("default"),
                [VerilogTokenTypes.Verilog_defparam] = typeService.GetClassificationType("defparam"),
                [VerilogTokenTypes.Verilog_design] = typeService.GetClassificationType("design"),
                [VerilogTokenTypes.Verilog_disable] = typeService.GetClassificationType("disable"),
                [VerilogTokenTypes.Verilog_edge] = typeService.GetClassificationType("edge"),
                [VerilogTokenTypes.Verilog_else] = typeService.GetClassificationType("else"),
                [VerilogTokenTypes.Verilog_end] = typeService.GetClassificationType("end"),
                [VerilogTokenTypes.Verilog_endcase] = typeService.GetClassificationType("endcase"),
                [VerilogTokenTypes.Verilog_endconfig] = typeService.GetClassificationType("endconfig"),
                [VerilogTokenTypes.Verilog_endfunction] = typeService.GetClassificationType("endfunction"),
                [VerilogTokenTypes.Verilog_endgenerate] = typeService.GetClassificationType("endgenerate"),
                [VerilogTokenTypes.Verilog_endmodule] = typeService.GetClassificationType("endmodule"),
                [VerilogTokenTypes.Verilog_endprimitive] = typeService.GetClassificationType("endprimitive"),
                [VerilogTokenTypes.Verilog_endspecify] = typeService.GetClassificationType("endspecify"),
                [VerilogTokenTypes.Verilog_endtable] = typeService.GetClassificationType("endtable"),
                [VerilogTokenTypes.Verilog_endtask] = typeService.GetClassificationType("endtask"),
                [VerilogTokenTypes.Verilog_event] = typeService.GetClassificationType("event"),
                [VerilogTokenTypes.Verilog_for] = typeService.GetClassificationType("for"),
                [VerilogTokenTypes.Verilog_force] = typeService.GetClassificationType("force"),
                [VerilogTokenTypes.Verilog_forever] = typeService.GetClassificationType("forever"),
                [VerilogTokenTypes.Verilog_fork] = typeService.GetClassificationType("fork"),
                [VerilogTokenTypes.Verilog_function] = typeService.GetClassificationType("function"),
                [VerilogTokenTypes.Verilog_generate] = typeService.GetClassificationType("generate"),
                [VerilogTokenTypes.Verilog_genvar] = typeService.GetClassificationType("genvar"),
                [VerilogTokenTypes.Verilog_if] = typeService.GetClassificationType("if"),
                [VerilogTokenTypes.Verilog_ifnone] = typeService.GetClassificationType("ifnone"),
                [VerilogTokenTypes.Verilog_incdir] = typeService.GetClassificationType("incdir"),
                [VerilogTokenTypes.Verilog_include] = typeService.GetClassificationType("include"),
                [VerilogTokenTypes.Verilog_initial] = typeService.GetClassificationType("initial"),
                [VerilogTokenTypes.Verilog_inout] = typeService.GetClassificationType("inout"),
                [VerilogTokenTypes.Verilog_input] = typeService.GetClassificationType("input"),
                [VerilogTokenTypes.Verilog_instance] = typeService.GetClassificationType("instance"),
                [VerilogTokenTypes.Verilog_join] = typeService.GetClassificationType("join"),
                [VerilogTokenTypes.Verilog_liblist] = typeService.GetClassificationType("liblist"),
                [VerilogTokenTypes.Verilog_library] = typeService.GetClassificationType("library"),
                [VerilogTokenTypes.Verilog_localparam] = typeService.GetClassificationType("localparam"),
                [VerilogTokenTypes.Verilog_macromodule] = typeService.GetClassificationType("macromodule"),
                [VerilogTokenTypes.Verilog_module] = typeService.GetClassificationType("module"),
                [VerilogTokenTypes.Verilog_negedge] = typeService.GetClassificationType("negedge"),
                [VerilogTokenTypes.Verilog_noshowcancelled] = typeService.GetClassificationType("noshowcancelled"),
                [VerilogTokenTypes.Verilog_output] = typeService.GetClassificationType("output"),
                [VerilogTokenTypes.Verilog_parameter] = typeService.GetClassificationType("parameter"),
                [VerilogTokenTypes.Verilog_posedge] = typeService.GetClassificationType("posedge"),
                [VerilogTokenTypes.Verilog_primitive] = typeService.GetClassificationType("primitive"),
                [VerilogTokenTypes.Verilog_pulsestyle_ondetect] = typeService.GetClassificationType("pulsestyle_ondetect"),
                [VerilogTokenTypes.Verilog_pulsestyle_onevent] = typeService.GetClassificationType("pulsestyle_onevent"),
                [VerilogTokenTypes.Verilog_reg] = typeService.GetClassificationType("reg"),
                [VerilogTokenTypes.Verilog_release] = typeService.GetClassificationType("release"),
                [VerilogTokenTypes.Verilog_repeat] = typeService.GetClassificationType("repeat"),
                [VerilogTokenTypes.Verilog_scalared] = typeService.GetClassificationType("scalared"),
                [VerilogTokenTypes.Verilog_showcancelled] = typeService.GetClassificationType("showcancelled"),
                [VerilogTokenTypes.Verilog_signed] = typeService.GetClassificationType("signed"),
                [VerilogTokenTypes.Verilog_specify] = typeService.GetClassificationType("specify"),
                [VerilogTokenTypes.Verilog_specparam] = typeService.GetClassificationType("specparam"),
                [VerilogTokenTypes.Verilog_strength] = typeService.GetClassificationType("strength"),
                [VerilogTokenTypes.Verilog_table] = typeService.GetClassificationType("table"),
                [VerilogTokenTypes.Verilog_task] = typeService.GetClassificationType("task"),
                [VerilogTokenTypes.Verilog_tri] = typeService.GetClassificationType("tri"),
                [VerilogTokenTypes.Verilog_tri0] = typeService.GetClassificationType("tri0"),
                [VerilogTokenTypes.Verilog_tri1] = typeService.GetClassificationType("tri1"),
                [VerilogTokenTypes.Verilog_triand] = typeService.GetClassificationType("triand"),
                [VerilogTokenTypes.Verilog_wand] = typeService.GetClassificationType("wand"),
                [VerilogTokenTypes.Verilog_trior] = typeService.GetClassificationType("trior"),
                [VerilogTokenTypes.Verilog_wor] = typeService.GetClassificationType("wor"),
                [VerilogTokenTypes.Verilog_trireg] = typeService.GetClassificationType("trireg"),
                [VerilogTokenTypes.Verilog_unsigned] = typeService.GetClassificationType("unsigned"),
                [VerilogTokenTypes.Verilog_use] = typeService.GetClassificationType("use"),
                [VerilogTokenTypes.Verilog_vectored] = typeService.GetClassificationType("vectored"),
                [VerilogTokenTypes.Verilog_wait] = typeService.GetClassificationType("wait"),
                [VerilogTokenTypes.Verilog_while] = typeService.GetClassificationType("while"),
                [VerilogTokenTypes.Verilog_wire] = typeService.GetClassificationType("wire"),

                [VerilogTokenTypes.Verilog_Directive] = typeService.GetClassificationType("directive"), // type must be one of VerilogTokenTagger 
                [VerilogTokenTypes.Verilog_Comment] = typeService.GetClassificationType("Comment"), // GetClassificationType string must be defined in ClassificationType.cs
                [VerilogTokenTypes.Verilog_Bracket] = typeService.GetClassificationType("Bracket"),
                [VerilogTokenTypes.Verilog_BracketContent] = typeService.GetClassificationType("BracketContent"),
            };
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

