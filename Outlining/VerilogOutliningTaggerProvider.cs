// File: VerilogOutliningTaggerProvider.cs

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace VerilogLanguage.CodeOutlining
{
    [Export(typeof(ITaggerProvider))]
    [ContentType("verilog")]
    [TagType(typeof(OutliningRegionTag))]
    internal sealed class VerilogOutliningTaggerProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {
            if (buffer == null) {
                return null;
            }

            return buffer.Properties.GetOrCreateSingletonProperty<ITagger<T>>(
                () => new VerilogOutliningTagger(buffer) as ITagger<T>);
        }
    }
}
