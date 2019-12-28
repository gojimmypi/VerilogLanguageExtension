
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace VerilogLanguage
{
    public static partial class VerilogGlobals
    {
        /// <summary>
        ///  IsRefreshChar - some characters, when encountered will need to have a full refresh, as they can have far-reaching consequences. 
        /// </summary>
        /// <param name="theString"></param>
        /// <returns></returns>
        static public bool IsRefreshChar(string theString)
        {
            return (theString.Contains("/")) ||
                   (theString.Contains("*")) ||
                   (theString.Contains("[")) ||
                   (theString.Contains("]")) ||
                   (theString.Contains("}")) ||
                   (theString.Contains("{")) ||
                   (theString.Contains("(")) ||
                   (theString.Contains(")"));
        }


        static public bool IsDelimiter(string theString)
        {
            return (theString == " ") ||
                   (theString == ":") ||
                   (theString == "~") ||
                   (theString == "[") ||
                   (theString == "]") ||
                   (theString == "}") ||
                   (theString == "{") ||
                   (theString == "(") ||
                   (theString == ")") ||
                   (theString == ";") ||
                   (theString == ",") ||
                   (theString == "@") ||
                   (theString == "\"") ||
                   (theString == "\t");
        }


        static public bool IsEndingDelimeter(string theString)
        {
            return (theString == "]") ||
                   (theString == "}") ||
                   (theString == ")");
        }


        /// <summary>
        ///  IsVerilogBracket is the string a single character that is an opening or closing bracket?
        /// </summary>
        /// <param name="theKeyword"></param>
        /// <returns></returns>
        private static bool IsVerilogBracket(string theKeyword)
        {
            switch (theKeyword)
            {
                case "[":
                case "(":
                case "{":
                case "}":
                case ")":
                case "]":
                    return true;
                default:
                    return false;
            }

        }


        /// <summary>
        ///  IsVerilogNamerKeyword - is the keyword one that names something? (e.g. variable, module, etc)
        /// </summary>
        /// <param name="theKeyword"></param>
        /// <returns></returns>
        private static bool IsVerilogNamerKeyword(string theKeyword)
        {
            return ((theKeyword == "reg") ||
                    (theKeyword == "wire") ||
                    (theKeyword == "input") ||
                    (theKeyword == "inout") ||
                    (theKeyword == "output") ||
                    (theKeyword == "parameter") ||
                    (theKeyword == "module")
                   );
        }

        private static bool IsVerilogVariableSigner(string theKeyword)
        {
            return ((theKeyword == "signed") ||
                    (theKeyword == "unsigned")
                   );
        }
        /// <summary>
        ///   IsVerilogValue - true if theKeyword is something like "1'b1",  "4'hFF",  "1:1", etc.
        ///                    note the latest version pre-splits the colon values, even though this
        ///                    function still supports the embedded colon
        /// </summary>
        /// <param name="theKeyword"></param>
        /// <returns></returns>
        private static bool IsVerilogValue(string theKeyword)
        {
            bool NumericParts = false; // we'll only have numeric parts, if there are parts to look at! (e.g. "1:1")
            string[] KeywordParts = theKeyword.Split(':');
            if (KeywordParts.Count() > 1)
            {
                foreach (string part in KeywordParts)
                {
                    // recursively call self here, if perhas we have a value like [1'b1:2'b2], or a prevopiusly defined parameter
                    if (!IsNumeric(part) && !IsVerilogValue(part) && !Is_BracketContent_For(part))
                    {
                        return false;
                    }
                    else
                    {
                        NumericParts = true;
                    }
                }
            }
            return theKeyword.Contains("'b") || theKeyword.Contains("'h") || NumericParts;
        }

        /// <summary>
        ///   IsNumeric - can the value be parsed as a valid number?
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static Boolean IsNumeric(String input)
        {
            return double.TryParse(input, out _); ;
        }

    }
}
