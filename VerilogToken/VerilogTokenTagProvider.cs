namespace VerilogLanguage.VerilogToken
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.Utilities;
    using CommentHelper;

    // You must export a tagger provider for your tagger. The tagger provider creates an VerilogTokenTag 
    // for a buffer of the "verilog" content type, or else returns an OutliningTagger if the buffer already has one.
    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(VerilogTokenTag))]
    [ContentType("verilog")]
    internal sealed class VerilogTokenTagProvider : ITaggerProvider
    {

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            // return new VerilogTokenTagger(buffer) as ITagger<T>;
            Func<ITagger<T>> sc = delegate () { return new VerilogTokenTagger(buffer) as ITagger<T>; };
            return buffer.Properties.GetOrCreateSingletonProperty<ITagger<T>>(sc);

        }
    }

}
