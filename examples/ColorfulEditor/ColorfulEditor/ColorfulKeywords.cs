// Copyright © 2018 Transeric Solutions.  All rights reserved.
// Author: Eric David Lynch
// License: https://www.codeproject.com/info/cpol10.aspx
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ColorfulEditor
{
	/// <summary>
	/// The keywords for the "Colorful" language.
	/// </summary>
	internal static class ColorfulKeywords
	{
		#region Private data
		// Adapted from https://en.wikipedia.org/wiki/List_of_colors:_A–F
		private static readonly List<string> keywords = new List<string>
		{
			"Amaranth", "Amber", "Amethyst", "Apricot", "Aquamarine", "Azure", "Beige", "Black",
			"Blue", "Blush", "Bronze", "Brown", "Burgundy", "Byzantium", "Carmine", "Cerise",
			"Cerulean", "Champagne", "Chartreuse", "Chocolate", "Cobalt", "Coffee", "Copper",
			"Coral", "Crimson", "Cyan", "Emerald", "Erin", "Gold", "Gray", "Green", "Harlequin",
			"Indigo", "Ivory", "Jade", "Lavender", "Lemon", "Lilac", "Lime", "Magenta", "Maroon",
			"Mauve", "Navy", "Ocher", "Olive", "Orange", "Orchid", "Peach", "Pear", "Periwinkle",
			"Pink", "Plum", "Puce", "Purple", "Raspberry", "Red", "Rose", "Ruby", "Salmon",
			"Sangria", "Sapphire", "Scarlet", "Silver", "Tan", "Taupe", "Teal", "Turquoise",
			"Violet", "Viridian", "White", "Yellow"
		};

		private static readonly HashSet<string> keywordSet = new HashSet<string>(
			keywords, StringComparer.OrdinalIgnoreCase);
		#endregion // Private data

		#region Properties
		/// <summary>
		/// Gets the list of all keywords (in alphabetic order).
		/// </summary>
		internal static IReadOnlyList<string> All { get; } =
			new ReadOnlyCollection<string>(keywords);
		#endregion // Properties

		#region Internal methods
		/// <summary>
		/// Gets value indicating if the specified word is a keyword.
		/// </summary>
		/// <param name="word">The word to test.</param>
		/// <returns>True, if the word is a keyword; otherwise, false.</returns>
		internal static bool Contains(string word) =>
			keywordSet.Contains(word);
		#endregion // Internal methods
	}
}
