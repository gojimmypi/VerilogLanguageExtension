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
    [TagType(typeof(VerilogTokenTag))]
    internal sealed class VerilogTokenTagProvider : ITaggerProvider
    {

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            return new VerilogTokenTagger(buffer) as ITagger<T>;
        }
    }

    public class VerilogTokenTag : ITag 
    {
        public VerilogTokenTypes type { get; private set; }

        public VerilogTokenTag(VerilogTokenTypes type)
        {
            this.type = type;
        }
    }

    internal sealed class VerilogTokenTagger : ITagger<VerilogTokenTag>
    {

        ITextBuffer _buffer;
        IDictionary<string, VerilogTokenTypes> _VerilogTypes;

        internal VerilogTokenTagger(ITextBuffer buffer)
        {
            _buffer = buffer;
            _VerilogTypes = new Dictionary<string, VerilogTokenTypes>();
            _VerilogTypes["always"] = VerilogTokenTypes.Verilog_always;
            _VerilogTypes["assign"] = VerilogTokenTypes.Verilog_assign;
            _VerilogTypes["automatic"] = VerilogTokenTypes.Verilog_automatic;
            _VerilogTypes["begin"] = VerilogTokenTypes.Verilog_begin;
            _VerilogTypes["case"] = VerilogTokenTypes.Verilog_case;
            _VerilogTypes["casex"] = VerilogTokenTypes.Verilog_casex;
            _VerilogTypes["casez"] = VerilogTokenTypes.Verilog_casez;
            _VerilogTypes["cell"] = VerilogTokenTypes.Verilog_cell;
            _VerilogTypes["config"] = VerilogTokenTypes.Verilog_config;
            _VerilogTypes["deassign"] = VerilogTokenTypes.Verilog_deassign;
            _VerilogTypes["default"] = VerilogTokenTypes.Verilog_default;
            _VerilogTypes["defparam"] = VerilogTokenTypes.Verilog_defparam;
            _VerilogTypes["design"] = VerilogTokenTypes.Verilog_design;
            _VerilogTypes["disable"] = VerilogTokenTypes.Verilog_disable;
            _VerilogTypes["edge"] = VerilogTokenTypes.Verilog_edge;
            _VerilogTypes["else"] = VerilogTokenTypes.Verilog_else;
            _VerilogTypes["end"] = VerilogTokenTypes.Verilog_end;
            _VerilogTypes["endcase"] = VerilogTokenTypes.Verilog_endcase;
            _VerilogTypes["endconfig"] = VerilogTokenTypes.Verilog_endconfig;
            _VerilogTypes["endfunction"] = VerilogTokenTypes.Verilog_endfunction;
            _VerilogTypes["endgenerate"] = VerilogTokenTypes.Verilog_endgenerate;
            _VerilogTypes["endmodule"] = VerilogTokenTypes.Verilog_endmodule;
            _VerilogTypes["endprimitive"] = VerilogTokenTypes.Verilog_endprimitive;
            _VerilogTypes["endspecify"] = VerilogTokenTypes.Verilog_endspecify;
            _VerilogTypes["endtable"] = VerilogTokenTypes.Verilog_endtable;
            _VerilogTypes["endtask"] = VerilogTokenTypes.Verilog_endtask;
            _VerilogTypes["event"] = VerilogTokenTypes.Verilog_event;
            _VerilogTypes["for"] = VerilogTokenTypes.Verilog_for;
            _VerilogTypes["force"] = VerilogTokenTypes.Verilog_force;
            _VerilogTypes["forever"] = VerilogTokenTypes.Verilog_forever;
            _VerilogTypes["fork"] = VerilogTokenTypes.Verilog_fork;
            _VerilogTypes["function"] = VerilogTokenTypes.Verilog_function;
            _VerilogTypes["generate"] = VerilogTokenTypes.Verilog_generate;
            _VerilogTypes["genvar"] = VerilogTokenTypes.Verilog_genvar;
            _VerilogTypes["if"] = VerilogTokenTypes.Verilog_if;
            _VerilogTypes["ifnone"] = VerilogTokenTypes.Verilog_ifnone;
            _VerilogTypes["incdir"] = VerilogTokenTypes.Verilog_incdir;
            _VerilogTypes["include"] = VerilogTokenTypes.Verilog_include;
            _VerilogTypes["initial"] = VerilogTokenTypes.Verilog_initial;
            _VerilogTypes["inout"] = VerilogTokenTypes.Verilog_inout;
            _VerilogTypes["input"] = VerilogTokenTypes.Verilog_input;
            _VerilogTypes["instance"] = VerilogTokenTypes.Verilog_instance;
            _VerilogTypes["join"] = VerilogTokenTypes.Verilog_join;
            _VerilogTypes["liblist"] = VerilogTokenTypes.Verilog_liblist;
            _VerilogTypes["library"] = VerilogTokenTypes.Verilog_library;
            _VerilogTypes["localparam"] = VerilogTokenTypes.Verilog_localparam;
            _VerilogTypes["macromodule"] = VerilogTokenTypes.Verilog_macromodule;
            _VerilogTypes["module"] = VerilogTokenTypes.Verilog_module;
            _VerilogTypes["negedge"] = VerilogTokenTypes.Verilog_negedge;
            _VerilogTypes["noshowcancelled"] = VerilogTokenTypes.Verilog_noshowcancelled;
            _VerilogTypes["output"] = VerilogTokenTypes.Verilog_output;
            _VerilogTypes["parameter"] = VerilogTokenTypes.Verilog_parameter;
            _VerilogTypes["posedge"] = VerilogTokenTypes.Verilog_posedge;
            _VerilogTypes["primitive"] = VerilogTokenTypes.Verilog_primitive;
            _VerilogTypes["pulsestyle_ondetect"] = VerilogTokenTypes.Verilog_pulsestyle_ondetect;
            _VerilogTypes["pulsestyle_onevent"] = VerilogTokenTypes.Verilog_pulsestyle_onevent;
            _VerilogTypes["reg"] = VerilogTokenTypes.Verilog_reg;
            _VerilogTypes["release"] = VerilogTokenTypes.Verilog_release;
            _VerilogTypes["repeat"] = VerilogTokenTypes.Verilog_repeat;
            _VerilogTypes["scalared"] = VerilogTokenTypes.Verilog_scalared;
            _VerilogTypes["showcancelled"] = VerilogTokenTypes.Verilog_showcancelled;
            _VerilogTypes["signed"] = VerilogTokenTypes.Verilog_signed;
            _VerilogTypes["specify"] = VerilogTokenTypes.Verilog_specify;
            _VerilogTypes["specparam"] = VerilogTokenTypes.Verilog_specparam;
            _VerilogTypes["strength"] = VerilogTokenTypes.Verilog_strength;
            _VerilogTypes["table"] = VerilogTokenTypes.Verilog_table;
            _VerilogTypes["task"] = VerilogTokenTypes.Verilog_task;
            _VerilogTypes["tri"] = VerilogTokenTypes.Verilog_tri;
            _VerilogTypes["tri0"] = VerilogTokenTypes.Verilog_tri0;
            _VerilogTypes["tri1"] = VerilogTokenTypes.Verilog_tri1;
            _VerilogTypes["triand"] = VerilogTokenTypes.Verilog_triand;
            _VerilogTypes["wand"] = VerilogTokenTypes.Verilog_wand;
            _VerilogTypes["trior"] = VerilogTokenTypes.Verilog_trior;
            _VerilogTypes["wor"] = VerilogTokenTypes.Verilog_wor;
            _VerilogTypes["trireg"] = VerilogTokenTypes.Verilog_trireg;
            _VerilogTypes["unsigned"] = VerilogTokenTypes.Verilog_unsigned;
            _VerilogTypes["use"] = VerilogTokenTypes.Verilog_use;
            _VerilogTypes["vectored"] = VerilogTokenTypes.Verilog_vectored;
            _VerilogTypes["wait"] = VerilogTokenTypes.Verilog_wait;
            _VerilogTypes["while"] = VerilogTokenTypes.Verilog_while;
            _VerilogTypes["wire"] = VerilogTokenTypes.Verilog_wire;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged
        {
            add { }
            remove { }
        }

        public IEnumerable<ITagSpan<VerilogTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {

            foreach (SnapshotSpan curSpan in spans)
            {
                ITextSnapshotLine containingLine = curSpan.Start.GetContainingLine();
                int curLoc = containingLine.Start.Position;
                string[] tokens = containingLine.GetText().ToLower().Split(new char[] { ' ', '\t' }, StringSplitOptions.None);

                foreach (string VerilogToken in tokens)
                {
                    if (_VerilogTypes.ContainsKey(VerilogToken))
                    {
                        var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc, VerilogToken.Length));
                        if( tokenSpan.IntersectsWith(curSpan) ) 
                            yield return new TagSpan<VerilogTokenTag>(tokenSpan, 
                                                                  new VerilogTokenTag(_VerilogTypes[VerilogToken]));
                    }

                    //add an extra char location because of the space
                    curLoc += VerilogToken.Length + 1;
                }
            }
            
        }
    }
}
