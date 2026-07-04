// File: VerilogTokenTagProvider.cs
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
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace VerilogLanguage.VerilogToken
{
    // You must export a tagger provider for your tagger.
    // IMPORTANT: Only ONE tagger instance per buffer.
    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(VerilogTokenTag))]
    [ContentType("verilog")] // see _buffer.ContentType (ITextBuffer.ContentType Property)
    internal sealed class VerilogTokenTagProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {
            if (buffer == null) {
                return null;
            }

            System.Diagnostics.Debug.WriteLine(
                "VerilogTokenTagProvider.CreateTagger: ContentType=" + buffer.ContentType.TypeName);

            // CRITICAL:
            // Returning a new tagger each time causes:
            //   - multiple buffer.Changed handlers
            //   - multiple token scans
            //   - broken / partial classification
            //
            // This MUST be a singleton per buffer.
            // Do not dispose this singleton from ITextDocumentDisposed. Visual Studio Peek
            // can create and close a temporary document view for the same file while the
            // normal editor view is still alive. Disposing the shared tagger at that point
            // leaves the existing classifier/aggregator connected to a dead tagger, which
            // makes all Verilog syntax highlighting disappear until the file is reopened.
            VerilogTokenTagger tagger = buffer.Properties.GetOrCreateSingletonProperty<VerilogTokenTagger>(
                () => new VerilogTokenTagger(buffer));

            return tagger as ITagger<T>;

            // return new VerilogTokenTagger(buffer) as ITagger<T>;
            // TODO which is better? above or below?

            //Func<ITagger<T>> sc = delegate () { return new VerilogTokenTagger(buffer) as ITagger<T>; };
            //return buffer.Properties.GetOrCreateSingletonProperty<ITagger<T>>(sc);
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged
        {
            add { }
            remove { }
        }
    }

}
