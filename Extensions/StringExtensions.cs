using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerilogLanguage 
{
    public static class StringExtensions
    {

        // although there are clever solutions using a variety of techjniques, thanks https://stackoverflow.com/questions/6617284/c-sharp-how-convert-large-hex-string-to-binary
        // for the reminder that sometimes simplest is fastest
        public static readonly Dictionary<char, string> hexCharacterToBinary = new Dictionary<char, string> {
                                                                        { '0', "0000" },
                                                                        { '1', "0001" },
                                                                        { '2', "0010" },
                                                                        { '3', "0011" },
                                                                        { '4', "0100" },
                                                                        { '5', "0101" },
                                                                        { '6', "0110" },
                                                                        { '7', "0111" },
                                                                        { '8', "1000" },
                                                                        { '9', "1001" },
                                                                        { 'A', "1010" },
                                                                        { 'B', "1011" },
                                                                        { 'C', "1100" },
                                                                        { 'D', "1101" },
                                                                        { 'E', "1110" },
                                                                        { 'F', "1111" },
                                                                        { 'a', "1010" },
                                                                        { 'b', "1011" },
                                                                        { 'c', "1100" },
                                                                        { 'd', "1101" },
                                                                        { 'e', "1110" },
                                                                        { 'f', "1111" }
                                                                    };

        // thank you https://stackoverflow.com/questions/7574606/left-function-in-c-sharp/7574645
        public static string Left(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            maxLength = Math.Abs(maxLength);

            return (value.Length <= maxLength
                   ? value
                   : value.Substring(0, maxLength)
                   );
        }

        /// <summary>
        /// Get substring of specified number of characters on the right.
        /// </summary>
        public static string Right(this string value, int length)
        {
            if (String.IsNullOrEmpty(value)) return string.Empty;

            return value.Length <= length ? value : value.Substring(value.Length - length);
        }

        public static string FirstRadixValue(this string value)
        {
            string res = "";
 
            try
            {
                // check for a blank string, if found return an empty string immediately
                // also check if there's no single quote; if not, there's certainly no Radix
                // the shortes string would be 3 characters: 3'2
                if ((value == null) || (value == "") || value.Length < 3 || !value.Contains("'"))
                {
                    return res;
                }

                // search for  'h, 'b, etc
                foreach (string item in VerilogGlobals.VerilogRadixChars)
                {
                    string searchRadix = "'" + item;
                    if (value.Contains(searchRadix))
                    {
                        res = searchRadix;
                        break;
                    }
                }

            }
            catch (Exception ex)
            {

                string a = ex.Message;
                res = "";
            }
            return res;
        }
        /// <summary>
        /// returns true if all the characters in the string are in the AcceptableValues list
        /// </summary>
        /// <param name="value"></param>
        /// <param name="AcceptableValues"></param>
        /// <returns></returns>
        public static bool AllCharsIn(this string value, List<string> AcceptableValues)
        {
            bool res = true; // we'll assume the string is valid until proven otherwise
            if (AcceptableValues != null && AcceptableValues.Count > 0 && value != null && value.Length > 0)
            {
                for (int i = 0; i < value.Length - 1; i++)
                {
                    if (!AcceptableValues.Contains(value.Substring(i, 1)))
                    {
                        // as soon as we find a character not in the list, we don't need to continue searching
                        return false;
                    }
                }
            }
            else
            {
                res = false;
            }
            return res;
        }

        public static string HexStringToBinary(this string value)
        {
            StringBuilder result = new StringBuilder();
            foreach (char c in value)
            {
                // This will crash for non-hex characters. You might want to handle that differently.
                if (hexCharacterToBinary.ContainsKey(c))
                {
                    result.Append(hexCharacterToBinary[char.ToUpper(c)]);
                }
            }
            return result.ToString();
        }
    }
}
