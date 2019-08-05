using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

// Code based on Walkthrough: Highlighting Text
// See: https://docs.microsoft.com/en-us/visualstudio/extensibility/walkthrough-highlighting-text?view=vs-2015


namespace VerilogLanguage.Highlighting
{
    // Creating a Tagger Provider

    // Create a class named HighlightWordTaggerProvider that implements IViewTaggerProvider, 
    // and export it with a ContentTypeAttribute of "text" and a TagTypeAttribute of TextMarkerTag.
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("verilog")]
    [TagType(typeof(TextMarkerTag))]
    internal class HighlightWordTaggerProvider : IViewTaggerProvider
    {
        // Step 2: You must import two editor services, the ITextSearchService and the ITextStructureNavigatorSelectorService, to instantiate the tagger.
        [Import]
        internal ITextSearchService TextSearchService { get; set; }

        [Import]
        internal ITextStructureNavigatorSelectorService TextStructureNavigatorSelector { get; set; }
        // Step 3: Implement the CreateTagger method to return an instance of HighlightWordTagger.
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            //provide highlighting only on the top buffer   
            if (textView.TextBuffer != buffer)
                return null;

            string thisFile = VerilogLanguage.VerilogGlobals.GetDocumentPath(textView.TextSnapshot);

            // TODO - do we really want to reparse here??
            // this appears to be the only place called when first opening a file. (?)
            VerilogGlobals.NeedReparse = true;
            VerilogGlobals.Reparse(buffer,thisFile); // parse the buffer at file load time

            ITextStructureNavigator textStructureNavigator =
                TextStructureNavigatorSelector.GetTextStructureNavigator(buffer);

            return new HighlightWordTagger(textView, buffer, TextSearchService, textStructureNavigator) as ITagger<T>;
        }
    }
}
