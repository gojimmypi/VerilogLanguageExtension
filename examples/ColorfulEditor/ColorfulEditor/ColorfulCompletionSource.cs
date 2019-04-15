// Copyright © 2018 Transeric Solutions.  All rights reserved.
// Author: Eric David Lynch
// License: https://www.codeproject.com/info/cpol10.aspx
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;

namespace ColorfulEditor
{
	/// <summary>
	/// A source of possible completions for IntelliSense.
	/// </summary>
	/// <remarks>
	/// Adapted from https://docs.microsoft.com/en-us/visualstudio/extensibility/walkthrough-displaying-statement-completion.
	/// </remarks>
	internal sealed class ColorfulCompletionSource : ICompletionSource
	{
		#region Constructors
		/// <summary>
		/// Create an instance for the specified provider and text buffer.
		/// </summary>
		/// <param name="provider">The provider that created this completion source.</param>
		/// <param name="buffer">The text buffer serviced by this completion source.</param>
		internal ColorfulCompletionSource(ITextBuffer buffer,
			ITextStructureNavigatorSelectorService navigatorService)
		{
			Navigator = navigatorService.GetTextStructureNavigator(buffer);
			Buffer = buffer;
		}
		#endregion // Constructors

		#region Constants
		/// <summary>
		/// The unique, non-localized identifier for completion set.
		/// </summary>
		internal const string Moniker = "All";

		/// <summary>
		/// The localized name of the completion set.
		/// </summary>
		/// <remarks>
		/// A globalized implementation should handle localization of the moniker.
		/// </remarks>
		internal static readonly string DisplayName = Moniker;
		#endregion // Constants

		#region Private data
		// All of the possible completions for IntelliSense
		private static readonly List<Completion> completions = ColorfulKeywords.All
			.Select(keyword => new Completion(keyword))
			.ToList();
		#endregion // Private data

		#region Properties
		/// <summary>
		/// Gets the text buffer serviced by this completion source.
		/// </summary>
		internal ITextBuffer Buffer { get; }

		/// <summary>
		/// A navigator for the text buffer serviced by this completion source.
		/// </summary>
		internal ITextStructureNavigator Navigator { get; }
		#endregion // Properties

		#region Public methods
		/// <summary>
		/// Augments any pre-existing IntelliSense completion sets with our own completion set.
		/// </summary>
		/// <param name="session">The IntelliSense session for which competion sets are computed.</param>
		/// <param name="completionSets">The pre-existing IntelliSense completion sets that are augmented.</param>
		public void AugmentCompletionSession(ICompletionSession session,
			IList<CompletionSet> completionSets)
		{
			// Get a tracking span for the partial word that the user is typing
			ITrackingSpan wordToComplete = GetTrackingSpanForWordToComplete(session);

			// Create a new completion set for the partial word that triggered IntelliSense
			CompletionSet completionSet = new CompletionSet(Moniker, DisplayName,
				wordToComplete, completions, null);

			// Add this completion set to the pre-existing list of completion sets
			completionSets.Add(completionSet);
		}

		/// <summary>
		/// Dispose of the unmanaged resources associated with this instance.
		/// </summary>
		public void Dispose() { }
		#endregion // Public methods

		#region Private methods
		// Get a tracking span for the word that contains the character immediately previous to
		// the cursor (caret) so we are aware of (track) changes
		private ITrackingSpan GetTrackingSpanForWordToComplete(ICompletionSession session)
		{
			// Get the location of the character immediately previous to the cursor (caret)
			SnapshotPoint currentPoint = session.TextView.Caret.Position.BufferPosition - 1;

			// Get the word that contains the character immediately previous to the cursor (caret)
			TextExtent extent = Navigator.GetExtentOfWord(currentPoint);

			// Return a span that tracks changes to that word
			return currentPoint.Snapshot.CreateTrackingSpan(extent.Span,
				SpanTrackingMode.EdgeInclusive);
		}
		#endregion // Private methods
	}
}
