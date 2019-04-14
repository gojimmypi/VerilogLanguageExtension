// adapted from https://github.com/madskristensen/ExtensibilityTools

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace VerilogLanguage
{
    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(IOutliningRegionTag))]
    [ContentType("verilog")]
    internal sealed class OutliningTaggerProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            //if (!ExtensibilityToolsPackage.Options.PkgdefEnableOutlining)
            //    return null;

            return buffer.Properties.GetOrCreateSingletonProperty(() => new OutliningTagger(buffer)) as ITagger<T>;
        }
    }
}
