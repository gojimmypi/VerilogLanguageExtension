using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text;

namespace VerilogLanguage.Outlining
{
    // this is code is based on the Walkthrough: Outlining  (Implementing a Tagger Provider)
    // See: https://docs.microsoft.com/en-us/visualstudio/extensibility/walkthrough-outlining?view=vs-2015

    // You must export a tagger provider for your tagger. The tagger provider creates an OutliningTagger 
    // for a buffer of the "verilog" content type, or else returns an OutliningTagger if the buffer already has one.

    // Step #1: Create a class named OutliningTaggerProvider that implements ITaggerProvider, 
    // and export it with the ContentType and TagType attributes.
    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(IOutliningRegionTag))]
    [ContentType("verilog")]
    internal sealed class OutliningTaggerProvider : ITaggerProvider
    {
        // Step #2: Implement the CreateTagger method by adding an OutliningTagger to the properties of the buffer.
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            //create a single tagger for each buffer.
            Func<ITagger<T>> sc = delegate () { return new OutliningTagger(buffer) as ITagger<T>; };
            return buffer.Properties.GetOrCreateSingletonProperty<ITagger<T>>(sc);
        }
    }
}
