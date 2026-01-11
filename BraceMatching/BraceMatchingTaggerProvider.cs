// File: BraceMatchingTaggerProvider.cs
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
    internal sealed class BraceMatchingTaggerProvider : IViewTaggerProvider
    {
        // Step 2: Implement the CreateTagger method to instantiate a BraceMatchingTagger.
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
#if DEBUG
            System.Diagnostics.Debug.WriteLine("Verilog BraceMatching: buffer.ContentType=" + buffer.ContentType.TypeName);
#endif
            if (textView == null) {
                return null;
            }

            // provide highlighting only on the top-level buffer
            if (textView.TextBuffer != buffer) {
                return null;
            }

            // old code for reference:
            // return textView.Properties.GetOrCreateSingletonProperty<ITagger<T>>(
            //     () => new BraceMatchingTagger(textView, buffer) as ITagger<T>);

            // IMPORTANT:
            // Return a single tagger instance per view.
            return textView.Properties.GetOrCreateSingletonProperty(
                () => new BraceMatchingTagger(textView, buffer)) as ITagger<T>;


        }
    }
}
