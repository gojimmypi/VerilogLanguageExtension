// file: BraceMatching/BraceMatchingTaggerProvider.cs
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
