using Microsoft.VisualStudio.Text.Tagging;

// Code based on Walkthrough: Highlighting Text
// See: https://docs.microsoft.com/en-us/visualstudio/extensibility/walkthrough-highlighting-text?view=vs-2015

namespace VerilogLanguage.Highlighting
{
    // Step #4: Create a class that inherits from TextMarkerTag and name it HighlightWordTag
    internal class HighlightWordTag : TextMarkerTag
    {
        public HighlightWordTag() : base("MarkerFormatDefinition/HighlightWordFormatDefinition") {
            // 
        }
    }


}
