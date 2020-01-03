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
            public int LastReparseVersion { get; set; } = 0;
            public bool IsReparsing { get; set; } = false;
            public bool NeedReparse { get; set; } = true;
            public DateTime LastParseTime { get; set; }
        }

        /// <summary>
        /// ParseStatus will keep track of parsing status for all the open files, based on file name for key
        /// </summary>
        public static Dictionary<string, ParseAttribute> ParseStatus = new Dictionary<string, ParseAttribute> { };

        /// <summary>
        /// EnsureExists_ParseStatus - we want to make sure our respective ParseStatus item exists, but NOT refresh it.
        /// </summary>
        /// <param name="targetFile"></param>
        public static void ParseStatus_EnsureExists(string targetFile)
        {
            lock(ParseStatus)
            {
                if (!ParseStatus.ContainsKey(targetFile))
                {
                    ParseStatus.Add(targetFile, new ParseAttribute());
                }
            }
        }

        /// <summary>
        /// Init_ParseStatus - we want to make sure the ParseStatus for this file is freshly initialized (e.g. when opening / re-opening a file)
        /// </summary>
        /// <param name="targetFile"></param>
        public static void ParseStatus_Init(string targetFile)
        {
            lock (ParseStatus)
            {
                if (!ParseStatus.ContainsKey(targetFile))
                {
                    ParseStatus.Add(targetFile, new ParseAttribute());
                }
                else
                {
                    ParseStatus[targetFile] = new ParseAttribute();
                }
            }
        }

        /// <summary>
        /// ParseStatus_NeedReparse - creates ParseStatus for file as needed and checks if it needs to be raparsed
        /// </summary>
        /// <param name="forFile"></param>
        /// <returns></returns>
        public static bool ParseStatus_NeedReparse(string forFile)
        {
            lock (ParseStatus)
            {
                ParseStatus_EnsureExists(forFile);
                return ParseStatus[forFile].NeedReparse;
            }
        }

        public static void ParseStatus_NeedReparse_SetValue(string forFile, bool toValue)
        {
            lock (ParseStatus)
            {
                ParseStatus_EnsureExists(forFile);
                ParseStatus[forFile].NeedReparse = toValue;
            }
        } 
        public static int LastPreparseVersion(string forFile)
        {
            lock (ParseStatus)
            {
                if (ParseStatus.ContainsKey(forFile))
                {
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
}
