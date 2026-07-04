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
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace VerilogLanguage.VerilogToken
{
    // One shared tokenizing core per text buffer, but one disposable lease per VS tag aggregator.
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

            VerilogTokenTagger tagger = buffer.Properties.GetOrCreateSingletonProperty<VerilogTokenTagger>(
                () => new VerilogTokenTagger(buffer));

            return new VerilogTokenTaggerLease(tagger) as ITagger<T>;
        }
    }

    internal sealed class VerilogTokenTaggerLease : ITagger<VerilogTokenTag>, IDisposable
    {
        private readonly object eventLock = new object();
        private VerilogTokenTagger tagger;
        private EventHandler<SnapshotSpanEventArgs> tagsChanged;

        internal VerilogTokenTaggerLease(VerilogTokenTagger tagger) {
            this.tagger = tagger ?? throw new ArgumentNullException(nameof(tagger));
            this.tagger.TagsChanged += CoreTagsChanged;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged
        {
            add
            {
                lock (eventLock) {
                    tagsChanged += value;
                }
            }

            remove
            {
                lock (eventLock) {
                    tagsChanged -= value;
                }
            }
        }

        public IEnumerable<ITagSpan<VerilogTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
            VerilogTokenTagger currentTagger = tagger;
            if (currentTagger == null) {
                yield break;
            }

            foreach (ITagSpan<VerilogTokenTag> tag in currentTagger.GetTags(spans)) {
                yield return tag;
            }
        }

        public void Dispose() {
            VerilogTokenTagger currentTagger = tagger;
            if (currentTagger == null) {
                return;
            }

            tagger = null;
            currentTagger.TagsChanged -= CoreTagsChanged;

            lock (eventLock) {
                tagsChanged = null;
            }
        }

        private void CoreTagsChanged(object sender, SnapshotSpanEventArgs e) {
            EventHandler<SnapshotSpanEventArgs> handler;

            lock (eventLock) {
                handler = tagsChanged;
            }

            if (handler != null) {
                handler(this, e);
            }
        }
    }
}
