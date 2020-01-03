using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Threading;
using VerilogLanguage.VerilogToken;

namespace VerilogLanguage
{
    public static partial class VerilogGlobals
    {
        public class ParseAttribute
        {
            public int LastReparseVersion = 0;
            public bool IsReparsing = false;
        }

        /// <summary>
        /// ParseStatus will keep track of parsing status for all the open files, based on file name for key
        /// </summary>
        public static Dictionary<string, ParseAttribute> ParseStatus = new Dictionary<string, ParseAttribute> { };


        public static int LastPreparseVersion(string forFile)
        {
            if (ParseStatus.ContainsKey(forFile)) {
                return ParseStatus[forFile].LastReparseVersion;
            }
            else
            {
                ParseAttribute newParseAttribute = new ParseAttribute();
                ParseStatus.Add(forFile, newParseAttribute);
                return 0;
            }
        }
    }
}
