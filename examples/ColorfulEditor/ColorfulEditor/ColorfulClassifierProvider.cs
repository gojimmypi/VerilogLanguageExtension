// Copyright © 2018 Transeric Solutions.  All rights reserved.
// Author: Eric David Lynch
// License: https://www.codeproject.com/info/cpol10.aspx
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace ColorfulEditor
{
	/// <summary>
	/// A factory for <see cref="IClassifier"/> for the content type <see cref="Colorful.ContentType"/>.
	/// </summary>
	[Export(typeof(IClassifierProvider))]
	[Name(nameof(ColorfulClassifierProvider))]
	[ContentType(Colorful.ContentType)]
	internal sealed class ColorfulClassifierProvider : IClassifierProvider
	{
		#region Managed Extensibility Framework (MEF) Fields
		// Import a registry of classifications types
		[Import]
		private IClassificationTypeRegistryService classificationRegistry = null;

		// Import the standard classification types
		[Import]
		private IStandardClassificationService classifications = null;
		#endregion // Managed Extensibility Framework (MEF) Fields

		#region Public methods
		/// <summary>
		/// Gets a classifier for the specified text buffer.
		/// </summary>
		/// <param name="buffer">The <see cref="ITextBuffer"/> to classify.</param>
		/// <returns>A classifier for the text buffer, or null if the provider cannot do so in its current state.</returns>
		public IClassifier GetClassifier(ITextBuffer buffer) =>
			buffer.Properties.GetOrCreateSingletonProperty(() =>
				new ColorfulClassifier(buffer, classifications, classificationRegistry));
		#endregion // Public methods
	}
}
