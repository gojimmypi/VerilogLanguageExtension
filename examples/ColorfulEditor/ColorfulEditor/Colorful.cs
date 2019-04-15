// Copyright © 2018 Transeric Solutions.  All rights reserved.
// Author: Eric David Lynch
// License: https://www.codeproject.com/info/cpol10.aspx
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;

namespace ColorfulEditor
{
	/// <summary>
	/// A class to associate the ".colorful" file extension with the content type definition
	/// for the "Colorful" language.
	/// </summary>
	internal static class Colorful
	{
		#region Constants
		/// <summary>
		/// The name of the content type for the "Colorful" language.
		/// </summary>
		internal const string ContentType = nameof(Colorful);

		/// <summary>
		/// The file extension for files containing the "Colorful" language.
		/// </summary>
		internal const string FileExtension = ".colorful";
		#endregion // Constants

		#region Managed Extensibility Framework (MEF) Fields
		/// <summary>
		/// The content type definition for the "Colorful" language, which is based on
		/// the pre-defined Visual Studio content type "code".
		/// </summary>
		[Export]
		[Name(ContentType)]
		[BaseDefinition("code")]
		internal static ContentTypeDefinition ContentTypeDefinition = null;

		/// <summary>
		/// The mapping of the ".colorful" file extension to the content type definition for the "Colorful" language.
		/// </summary>
		[Export]
		[Name(ContentType + nameof(FileExtensionToContentTypeDefinition))]
		[ContentType(ContentType)]
		[FileExtension(FileExtension)]
		internal static FileExtensionToContentTypeDefinition FileExtensionToContentTypeDefinition = null;
		#endregion // Managed Extensibility Framework (MEF) Fields
	}
}
