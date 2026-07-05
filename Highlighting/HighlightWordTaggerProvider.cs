// file: Highlighting/HighlightWordTaggerProvider.cs
//***************************************************************************
//
//  MIT License
//
//  Copyright (c) 2025-2026 gojimmypi
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

// File: HighlightWordTaggerProvider.cs
// Code based on Walkthrough: Highlighting Text
// See: https://docs.microsoft.com/en-us/visualstudio/extensibility/walkthrough-highlighting-text?view=vs-2015

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
    // and export it with a ContentTypeAttribute of "verilog" and a TagTypeAttribute of TextMarkerTag.
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("verilog")]
    [TagType(typeof(TextMarkerTag))]
    internal sealed class HighlightWordTaggerProvider : IViewTaggerProvider
    {
        // Step 2: You must import two editor services, the ITextSearchService and the ITextStructureNavigatorSelectorService, to instantiate the tagger.
        [Import]
        internal ITextSearchService TextSearchService { get; set; }

        [Import]
        internal ITextStructureNavigatorSelectorService TextStructureNavigatorSelector { get; set; }
        // Step 3: Implement the CreateTagger method to return an instance of HighlightWordTagger.
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
#if DEBUG
            System.Diagnostics.Debug.WriteLine("Highlighting.CreateTagger: ContentType=" + buffer.ContentType.TypeName);
#endif
            if (textView == null) {
                return null;
            }

            // provide highlighting only on the top-level buffer
            if (textView.TextBuffer != buffer) {
                return null;
            }

            ITextStructureNavigator textStructureNavigator =
                TextStructureNavigatorSelector.GetTextStructureNavigator(buffer);

            // IMPORTANT:
            // Return a single tagger instance per view.
            return textView.Properties.GetOrCreateSingletonProperty(
                () => new HighlightWordTagger(textView, buffer, TextSearchService, textStructureNavigator)) as ITagger<T>;
        }
    }
}
