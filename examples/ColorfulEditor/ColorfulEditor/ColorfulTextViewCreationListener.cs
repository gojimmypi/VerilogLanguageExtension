// Copyright © 2018 Transeric Solutions.  All rights reserved.
// Author: Eric David Lynch
// License: https://www.codeproject.com/info/cpol10.aspx
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace ColorfulEditor
{
	/// <summary>
	/// A class to listen for the creation of text views for "Colorful" content.
	/// </summary>
	/// <remarks>
	/// Adapted from https://docs.microsoft.com/en-us/visualstudio/extensibility/walkthrough-displaying-statement-completion.
	/// </remarks>
	[Export(typeof(IVsTextViewCreationListener))]
	[Name(nameof(ColorfulTextViewCreationListener))]
	[ContentType(Colorful.ContentType)]
	[TextViewRole(PredefinedTextViewRoles.Editable)]
	public class ColorfulTextViewCreationListener : IVsTextViewCreationListener
	{
		#region Managed Extensibility Framework (MEF) Fields
		// Import an adapter to map between legacy TextManager code and editor code 
		[Import]
		private IVsEditorAdaptersFactoryService adapterService = null;

		// Import the central completion broker responsible for IntelliSense completion
		[Import]
		private ICompletionBroker completionBroker = null;

		// Import the central Visual Studio service provider
		[Import]
		private SVsServiceProvider serviceProvider = null;
		#endregion // Managed Extensibility Framework (MEF) Fields

		#region Public methods
		/// <summary>
		/// Invoked after a <see cref="IVsTextView"/> has been created and initialized.
		/// </summary>
		/// <param name="vsTextView">The <see cref="IVsTextView"/> that was created and initialized.</param>
		public void VsTextViewCreated(IVsTextView vsTextView)
		{
			// Get the WPF text view for the specified text view adapter
			ITextView textView = adapterService.GetWpfTextView(vsTextView);

			// Create a target for OLE commands for the specified text view adapter
			textView.Properties.GetOrCreateSingletonProperty(() =>
				new ColorfulOleCommandTarget(vsTextView, textView,
					completionBroker, serviceProvider));
		}
		#endregion // Public methods
	}
}
