using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerilogLanguage 
{
    public static class StringExtensions
    {
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
                if ((value == null) || (value == "") || value.Length < 3 || !value.Contains("'"))
                {
                    return res;
                }

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
    }
}
