// file: Globals/VerilogContext.cs
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
        private static VerilogTokenContextType VerilogTokenContextFromString(string s) {
            switch (s) {
                case null:
                    return VerilogTokenContextType.Undetermined; // short circuit exit

                case "":    // nothing to do for empty string
                case "\t":  // nor tabs
                case " ":   // nor spaces
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

    } // partial class VerilogGlobals
}
