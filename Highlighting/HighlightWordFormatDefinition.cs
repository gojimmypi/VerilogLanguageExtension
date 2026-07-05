// file: Highlighting/HighlightWordFormatDefinition.cs
//***************************************************************************
//
//  MIT License
//
//  Copyright (c) 2025-2026 gojimmypi
//
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in all
//  copies or substantial portions of the Software.
//
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//  SOFTWARE.
//
//***************************************************************************

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.Windows.Media;

// Code based on Walkthrough: Highlighting Text
// See: https://docs.microsoft.com/en-us/visualstudio/extensibility/walkthrough-highlighting-text?view=vs-2015

namespace VerilogLanguage.Highlighting
{
    // Step #5: Create a second class that inherits from MarkerFormatDefinition,
    // and name it HighlightWordFormatDefinition. In order to use this format definition for your tag,
    // you must export it with the following attributes:
    //
    // NameAttribute: tags use this to reference this format
    // UserVisibleAttribute: this causes the format to appear in the UI

    [Export(typeof(EditorFormatDefinition))]
    [Name("MarkerFormatDefinition/HighlightWordFormatDefinition")]
    [UserVisible(true)]
    internal class HighlightWordFormatDefinition : MarkerFormatDefinition
    {
        // the single word higlight that occurs when typing. called once at Visual Studio launch time.
        // this controls the color of all selected words in the document.
        // currently commented out to use the default Visual Studio colorization
        public HighlightWordFormatDefinition() {
            // this.ForegroundColor = Colors.DarkBlue;
            // this.DisplayName = "Highlight Word";
            // this.ZOrder = 5;
        }
    }

}
