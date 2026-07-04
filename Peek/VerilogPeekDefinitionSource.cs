using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Threading;
using VerilogLanguage.Navigation;

namespace VerilogLanguage.Peek
{
    [Export(typeof(IPeekableItemSourceProvider))]
    [ContentType("verilog")]
    [Name("Verilog Peek Definition Source")]
    [SupportsStandaloneFiles(true)]
    internal sealed class VerilogPeekDefinitionSourceProvider : IPeekableItemSourceProvider
    {
        [Import]
        internal IPeekResultFactory PeekResultFactory { get; set; }

        public IPeekableItemSource TryCreatePeekableItemSource(ITextBuffer textBuffer) {
            if (textBuffer == null) {
                return null;
            }

            return textBuffer.Properties.GetOrCreateSingletonProperty(
                () => new VerilogPeekDefinitionSource(textBuffer, PeekResultFactory));
        }
    }

    internal sealed class VerilogPeekDefinitionSource : IPeekableItemSource
    {
        private readonly ITextBuffer textBuffer;
        private readonly IPeekResultFactory peekResultFactory;

        internal VerilogPeekDefinitionSource(ITextBuffer textBuffer, IPeekResultFactory peekResultFactory) {
            this.textBuffer = textBuffer;
            this.peekResultFactory = peekResultFactory;
        }

        public void AugmentPeekSession(IPeekSession session, IList<IPeekableItem> peekableItems) {
            try {
                if (session == null || peekableItems == null || textBuffer == null || peekResultFactory == null) {
                    return;
                }

                ITextSnapshot snapshot = textBuffer.CurrentSnapshot;
                if (snapshot == null) {
                    return;
                }

                SnapshotPoint? triggerPoint = session.GetTriggerPoint(snapshot);
                if (!triggerPoint.HasValue) {
                    return;
                }

                SnapshotSpan tokenSpan;
                string lookupText;
                if (!VerilogDefinitionResolver.TryGetIdentifierSpanAtPosition(snapshot, triggerPoint.Value.Position, out tokenSpan, out lookupText)) {
                    return;
                }

                VerilogGlobals.VerilogDefinitionLocation definition;
                if (!VerilogDefinitionResolver.TryFindDefinition(tokenSpan, lookupText, out definition)) {
                    return;
                }

                string filePath = definition.FilePath;
                if (string.IsNullOrEmpty(filePath)) {
                    filePath = VerilogGlobals.GetDocumentPath(snapshot);
                }

                if (string.IsNullOrEmpty(filePath)) {
                    return;
                }

                peekableItems.Add(new VerilogDefinitionPeekableItem(lookupText, filePath, definition, peekResultFactory));
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine("Verilog Peek Definition failed to create item: " + ex);
            }
        }

        public void Dispose() {
        }
    }

    internal sealed class VerilogDefinitionPeekableItem : IPeekableItem
    {
        private readonly string lookupText;
        private readonly string filePath;
        private readonly VerilogGlobals.VerilogDefinitionLocation definition;
        private readonly IPeekResultFactory peekResultFactory;

        internal VerilogDefinitionPeekableItem(
            string lookupText,
            string filePath,
            VerilogGlobals.VerilogDefinitionLocation definition,
            IPeekResultFactory peekResultFactory) {

            this.lookupText = lookupText;
            this.filePath = filePath;
            this.definition = definition;
            this.peekResultFactory = peekResultFactory;
        }

        public string DisplayName
        {
            get { return lookupText; }
        }

        public IEnumerable<IPeekRelationship> Relationships
        {
            get { yield return PredefinedPeekRelationships.Definitions; }
        }

        public IPeekResultSource GetOrCreateResultSource(string relationshipName) {
            if (!string.Equals(relationshipName, PredefinedPeekRelationships.Definitions.Name, StringComparison.OrdinalIgnoreCase)) {
                return null;
            }

            return new VerilogDefinitionPeekResultSource(this);
        }

        internal string LookupText
        {
            get { return lookupText; }
        }

        internal string FilePath
        {
            get { return filePath; }
        }

        internal VerilogGlobals.VerilogDefinitionLocation Definition
        {
            get { return definition; }
        }

        internal IPeekResultFactory PeekResultFactory
        {
            get { return peekResultFactory; }
        }
    }

    internal sealed class VerilogDefinitionPeekResultSource : IPeekResultSource
    {
        private readonly VerilogDefinitionPeekableItem peekableItem;

        internal VerilogDefinitionPeekResultSource(VerilogDefinitionPeekableItem peekableItem) {
            this.peekableItem = peekableItem;
        }

        public void FindResults(string relationshipName, IPeekResultCollection resultCollection, CancellationToken cancellationToken, IFindPeekResultsCallback callback) {
            try {
                if (!string.Equals(relationshipName, PredefinedPeekRelationships.Definitions.Name, StringComparison.OrdinalIgnoreCase)) {
                    return;
                }

                if (cancellationToken.IsCancellationRequested || resultCollection == null || peekableItem == null) {
                    return;
                }

                VerilogGlobals.VerilogDefinitionLocation definition = peekableItem.Definition;
                if (definition == null || string.IsNullOrEmpty(peekableItem.FilePath) || definition.LineNumber < 0) {
                    ReportProgress(callback);
                    return;
                }

                int startLine = Math.Max(0, definition.LineNumber);
                int startIndex = Math.Max(0, definition.LinePosition);
                int endLine = startLine;
                int endIndex = startIndex + Math.Max(1, definition.Length);

                string fileName = Path.GetFileName(peekableItem.FilePath);
                string label = string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} - ({1}, {2})",
                    fileName,
                    startLine + 1,
                    startIndex + 1);

                using (var displayInfo = new PeekResultDisplayInfo(label, peekableItem.FilePath, fileName, peekableItem.FilePath)) {
                    IDocumentPeekResult result = peekableItem.PeekResultFactory.Create(
                        displayInfo,
                        peekableItem.FilePath,
                        startLine,
                        startIndex,
                        endLine,
                        endIndex,
                        startLine,
                        startIndex,
                        true);

                    resultCollection.Add(result);
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine("Verilog Peek Definition failed to find results: " + ex);
            }
            finally {
                ReportProgress(callback);
            }
        }

        private static void ReportProgress(IFindPeekResultsCallback callback) {
            if (callback != null) {
                callback.ReportProgress(1);
            }
        }
    }
}
