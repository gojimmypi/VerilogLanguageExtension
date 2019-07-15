
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace VerilogLanguage
{
    public static partial class VerilogGlobals
    {
        public enum VerilogTokenContextType
        {
            Undetermined,
            DoubleQuoteOpen,
            SquareBracketOpen,
            SquareBracketClose,
            SquareBracketContents,
            RoundBracketOpen,
            RoundBracketClose,
            RoundBracketContents,
            SquigglyBracketOpen,
            SquigglyBracketClose,
            SquigglyBracketContents,
            AlwaysAt,
            Comment,
            Text
        }

        /// <summary>
        ///   VerilogTokenContextType - given a string, determine if is this a bracket or regular text
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private static VerilogTokenContextType VerilogTokenContextFromString(string s)
        {
            switch (s)
            {
                case null:
                    return VerilogTokenContextType.Undetermined; // short circuit exit 

                case "":
                    return VerilogTokenContextType.Text; // short circuit exit to avoid string operation

                default:
                    switch (s.Substring(0, 1)) // given the first chart of the string, determine the context
                    {
                        case "[":
                            return VerilogTokenContextType.SquareBracketOpen;

                        case "]":
                            return VerilogTokenContextType.SquareBracketClose;

                        case "(":
                            return VerilogTokenContextType.RoundBracketOpen;

                        case ")":
                            return VerilogTokenContextType.RoundBracketClose;

                        case "{":
                            return VerilogTokenContextType.SquigglyBracketOpen;

                        case "}":
                            return VerilogTokenContextType.SquigglyBracketClose;

                        case "@":
                            return VerilogTokenContextType.AlwaysAt;

                        default:
                            return VerilogTokenContextType.Text;

                    }
            }
        }



    }
}
