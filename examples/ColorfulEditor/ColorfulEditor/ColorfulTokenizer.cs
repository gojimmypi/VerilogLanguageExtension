// Copyright © 2018 Transeric Solutions.  All rights reserved.
// Author: Eric David Lynch
// License: https://www.codeproject.com/info/cpol10.aspx
using System;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Classification;

namespace ColorfulEditor
{
	/// <summary>
	/// A tokenizer to break apart the different lexical elements of the "Colorful" language.
	/// </summary>
	internal sealed class ColorfulTokenizer
	{
		#region Constructors
		/// <summary>
		/// Create an instance for the specified standard classifications.
		/// </summary>
		/// <param name="classifications">The standard classifications.</param>
		internal ColorfulTokenizer(IStandardClassificationService classifications) =>
			Classifications = classifications;
		#endregion // Constructors

		#region Properties
		/// <summary>
		/// Gets the standard classifications.
		/// </summary>
		internal IStandardClassificationService Classifications { get; }
		#endregion // Properties

		#region Internal methods
		/// <summary>
		/// Advance the index to the next word and get the classification for the current word.
		/// </summary>
		/// <param name="text">The text to process.</param>
		/// <param name="index">The current zero-based index within the text.</param>
		/// <param name="classification">The classification for the current word.</param>
		/// <returns>The index of the next word.</returns>
		internal int AdvanceWord(string text, int index, out IClassificationType classification)
		{
			int length = text.Length;
			if (index >= length)
				throw new ArgumentOutOfRangeException(nameof(index));

			// If we encounter "//" treat the remainder of the text as a comment
			if (index + 1 < length && text[index] == '/' && text[index + 1] == '/')
			{
				classification = Classifications.Comment;
				return length;
			}

			// Skip white space (if we have any)
			int start = index;
			index = AdvanceWhile(text, index, chr => Char.IsWhiteSpace(chr));

			// If we encountered white space, we advanced a word
			if (index > start)
			{
				classification = Classifications.WhiteSpace;
				return index;
			}

			// If its punctuation (comma, period, etc.), classify it as an operator
			if (Char.IsPunctuation(text[index]))
			{
				classification = Classifications.Operator;
				return ++index;
			}

			// Words start with a letter or digit; otherwise, just take a single character
			start = index;
			if (Char.IsLetterOrDigit(text[index]))
				index = AdvanceWhile(text, index, chr => Char.IsLetterOrDigit(chr));
			else
				index++;

			string word = text.Substring(start, index - start);

			if (IsDecimalInteger(word))
				// Really a NumberLiteral, calling it a string to get a different color
				classification = Classifications.StringLiteral;
			else
				classification = ColorfulKeywords.Contains(word) ?
					Classifications.Keyword : Classifications.Other;

			return index;
		}
		#endregion // Internal methods

		#region Private methods
		// A sequence of one or more decimal digits [0..9] is a decimal integer
		private bool IsDecimalInteger(string word)
		{
			foreach (char chr in word)
				if (chr < '0' || chr > '9')
					return false;

			return true;
		}

		// Advance the index while the predicate condition is true and more characters remain
		private int AdvanceWhile(string text, int index, Func<char, bool> predicate)
		{
			for (int length = text.Length; index < length && predicate(text[index]); index++) ;
			return index;
		}
		#endregion // Private methods
	}
}
