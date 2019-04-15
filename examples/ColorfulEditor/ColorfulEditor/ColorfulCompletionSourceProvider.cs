// Copyright © 2018 Transeric Solutions.  All rights reserved.
// Author: Eric David Lynch
// License: https://www.codeproject.com/info/cpol10.aspx
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace ColorfulEditor
{
	/// <summary>
	/// A factory for <see cref="ICompletionSource"/> for the "Colorful" language.
	/// </summary>
	/// <remarks>
	/// Adapted from https://docs.microsoft.com/en-us/visualstudio/extensibility/walkthrough-displaying-statement-completion.
	/// </remarks>
	[Export(typeof(ICompletionSourceProvider))]
	[Name(nameof(ColorfulCompletionSourceProvider))]
	[ContentType(Colorful.ContentType)]
	internal class ColorfulCompletionSourceProvider : ICompletionSourceProvider
	{
		#region Managed Extensibility Framework (MEF) Fields
		// Import the service that caches ITextStructureNavigator objects based on content type
		[Import]
		private ITextStructureNavigatorSelectorService navigatorService = null;
		#endregion // Managed Extensibility Framework (MEF) Fields

		#region Public methods
		/// <summary>
		/// Try to create a completion source for the specified text buffer.
		/// </summary>
		/// <param name="textBuffer">The text buffer for which a completion source is created.</param>
		/// <returns>The completion source that was created for the specified text buffer.</returns>
		public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer) =>
			new ColorfulCompletionSource(textBuffer, navigatorService);
		#endregion // Public methods
	}
}
