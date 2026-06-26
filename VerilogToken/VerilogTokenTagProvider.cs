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
        [Import]
        internal ITextDocumentFactoryService TextDocumentFactoryService { get; set; }

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
            // This MUST be a singleton per buffer. The tagger owns an event
            // subscription on the buffer, so register a matching cleanup path
            // when the backing text document is disposed.
            VerilogTokenTagger tagger = buffer.Properties.GetOrCreateSingletonProperty<VerilogTokenTagger>(
                () => {
                    VerilogTokenTagger newTagger = new VerilogTokenTagger(buffer);
                    RegisterDisposeOnTextDocumentDisposed(buffer, newTagger);
                    return newTagger;
                });

            return tagger as ITagger<T>;

            // return new VerilogTokenTagger(buffer) as ITagger<T>;
            // TODO which is better? above or below?

            //Func<ITagger<T>> sc = delegate () { return new VerilogTokenTagger(buffer) as ITagger<T>; };
            //return buffer.Properties.GetOrCreateSingletonProperty<ITagger<T>>(sc);
        }

        private void RegisterDisposeOnTextDocumentDisposed(ITextBuffer buffer, VerilogTokenTagger tagger) {
            if (TextDocumentFactoryService == null) {
                return;
            }

            ITextDocument textDocument;
            if (!TextDocumentFactoryService.TryGetTextDocument(buffer, out textDocument)) {
                return;
            }

            EventHandler<TextDocumentEventArgs> disposedHandler = null;
            disposedHandler = (sender, e) => {
                if (e == null || !object.ReferenceEquals(e.TextDocument, textDocument)) {
                    return;
                }

                TextDocumentFactoryService.TextDocumentDisposed -= disposedHandler;
                tagger.Dispose();
                buffer.Properties.RemoveProperty(typeof(VerilogTokenTagger));
            };

            TextDocumentFactoryService.TextDocumentDisposed += disposedHandler;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged
        {
            add { }
            remove { }
        }
    }

}
