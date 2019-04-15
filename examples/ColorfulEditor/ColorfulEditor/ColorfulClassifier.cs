// Copyright © 2018 Transeric Solutions.  All rights reserved.
// Author: Eric David Lynch
// License: https://www.codeproject.com/info/cpol10.aspx
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace ColorfulEditor
{
	/// <summary>
	/// A text classifier for the "Colorful" language.
	/// </summary>
	internal sealed class ColorfulClassifier : IClassifier
	{
		#region Constructors
		/// <summary>
		/// Create a new instance for the specified buffer, standard classifications, and
		/// classification registry.
		/// </summary>
		/// <param name="buffer">The text buffer for this classifier.</param>
		/// <param name="classifications">The standard classifications.</param>
		/// <param name="classificationRegistry">The registry of classification types.</param>
		internal ColorfulClassifier(ITextBuffer buffer,
			IStandardClassificationService classifications,
			IClassificationTypeRegistryService classificationRegistry)
		{
			ClassificationRegistry = classificationRegistry;
			Classifications = classifications;
			Buffer = buffer;

			tokenizer = new ColorfulTokenizer(classifications);
		}
		#endregion // Constructors

		#region Private data
		private readonly ColorfulTokenizer tokenizer;
		#endregion // Private data

		#region Events
		/// <summary>
		/// An event that occurs when the classification of a span of text has changed.
		/// </summary>
		/// <remarks>
		/// This event gets raised if a non-text change would affect the classification in some way,
		/// for example typing /* would cause the classification to change in C# without directly
		/// affecting the span.
		/// </remarks>
#pragma warning disable CS0067
		public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;
#pragma warning restore CS0067
		#endregion // Events

		#region Properties
		/// <summary>
		/// Gets the text buffer for this classifier.
		/// </summary>
		internal ITextBuffer Buffer { get; }

		/// <summary>
		/// Gets the registry of classification types.
		/// </summary>
		internal IClassificationTypeRegistryService ClassificationRegistry { get; }

		/// <summary>
		/// Gets the standard classification types.
		/// </summary>
		internal IStandardClassificationService Classifications { get; }
		#endregion // Properties

		#region Public methods
		/// <summary>
		/// Get all of the classification spans that intersect with the specified text span.
		/// </summary>
		/// <param name="span">The text span to process (usually a line of text).</param>
		/// <returns>The classification spans that intersect with the specified text span.</returns>
		public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
		{
			var list = new List<ClassificationSpan>();

			ITextSnapshot snapshot = span.Snapshot;
			string text = span.GetText();
			int length = span.Length;
			int index = 0;

			while(index < length)
			{
				int start = index;
				index = tokenizer.AdvanceWord(text, start, out IClassificationType type);

				list.Add(new ClassificationSpan(new SnapshotSpan(snapshot,
					new Span(span.Start + start, index - start)), type));
			}

			return list;
		}
		#endregion // Public methods
	}
}
