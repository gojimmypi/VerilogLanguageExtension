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

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
// using VerilogLanguage.Events;

namespace VerilogLanguage
{
    
    // You must export a tagger provider for your tagger. The tagger provider creates an VerilogTokenTag 
    // for a buffer of the "verilog" content type, or else returns an OutliningTagger if the buffer already has one.
    [Export(typeof(IViewTaggerProvider))]
    [TagType(typeof(VerilogTokenTag))]
    [ContentType("verilog")] // see _buffer.ContentType (ITextBuffer.ContentType Property)
    internal sealed class VerilogTokenTagProvider : IViewTaggerProvider
    {
//#pragma warning disable 649 // "field never assigned to" -- field is set by MEF.
//        [Import]
//        internal IViewTagAggregatorFactoryService ViewTagAggregatorFactoryService;
//        // private IEventAggregator _eventAggregator;

//#pragma warning restore 649

//        [ImportingConstructor]
//        //public VerilogTokenTagProvider(IEventAggregator eventAggregator)
//        //{
//        //    _eventAggregator = eventAggregator;
//        //}
//        public VerilogTokenTagProvider()
//        {
//        }


        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView == null)
                throw new ArgumentNullException("textView");

            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (buffer != textView.TextBuffer)
                return null;

            //ITagAggregator<VerilogTokenTag> tagAggregator =
            //       ViewTagAggregatorFactoryService.CreateTagAggregator<VerilogTokenTag>(textView);

            // old code:
           //  return new VerilogTokenTagger(textView, buffer) as ITagger<T>;

            return textView.Properties.GetOrCreateSingletonProperty(() =>
                new VerilogTokenTagger(textView, buffer) as ITagger<T>);

            // TODO which is better? above or below?

            //Func<ITagger<T>> sc = delegate () { return new VerilogTokenTagger(textView, buffer) as ITagger<T>; };
            //return buffer.Properties.GetOrCreateSingletonProperty<ITagger<T>>(sc);

        }
    }

}
