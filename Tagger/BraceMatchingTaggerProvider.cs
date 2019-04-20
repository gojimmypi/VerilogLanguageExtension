// adapted from https://github.com/madskristensen/ExtensibilityTools

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace VerilogLanguage.BraceMatching
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("verilog")]
    [TagType(typeof(TextMarkerTag))]
    class BraceMatchingTaggerProvider : IViewTaggerProvider
    {
        // Step 2: Implement the CreateTagger method to instantiate a BraceMatchingTagger.
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView == null)
                return null;

            //provide highlighting only on the top-level buffer
            if (textView.TextBuffer != buffer)
                return null;

            return new BraceMatchingTagger(textView, buffer) as ITagger<T>;
        }
    }
}
