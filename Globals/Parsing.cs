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
        private static Dictionary<string, ParseAttribute> ParseStatus = new Dictionary<string, ParseAttribute> { };

        // see https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/statements#the-lock-statement
        //
        // While a mutual-exclusion lock is held, code executing in the same execution thread can also obtain and 
        // release the lock. However, code executing in other threads is blocked from obtaining the lock until the 
        // lock is released.
        //
        // Locking System.Type objects in order to synchronize access to static data is not recommended.Other code 
        // might lock on the same type, which can result in deadlock.A better approach is to synchronize access to 
        // static data by locking a private static object.
        /// <summary>
        /// _synchronizationParseStatus - synchronize access to static ParseStatus data by locking a private static object
        /// </summary>
        private static readonly object _synchronizationParseStatus = new object();

        /// <summary>
        /// ParseStatusController - thread-safe lock around access to shared ParseStatus dictionary
        /// </summary>
        public static class ParseStatusController
        {
            /// <summary>
            /// Init  - we want to make sure the ParseStatus for this file is freshly initialized (e.g. when opening / re-opening a file)
            /// </summary>
            /// <param name="targetFile"></param>
            public static void Init(string targetFile)
            {
                lock (_synchronizationParseStatus)
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
            /// EnsureExists_ParseStatus - we want to make sure our respective ParseStatus item exists, but NOT refresh it.
            /// </summary>
            /// <param name="targetFile"></param>
            public static void EnsureExists(string targetFile)
            {
                lock (_synchronizationParseStatus)
                {
                    if (!ParseStatus.ContainsKey(targetFile))
                    {
                        Init(targetFile);
                    }
                }
            }

            /// <summary>
            /// ParseStatus_NeedReparse - creates ParseStatus for file as needed and checks if it needs to be raparsed
            /// </summary>
            /// <param name="forFile"></param>
            /// <returns></returns>
            public static bool NeedReparse(string forFile)
            {
                lock (_synchronizationParseStatus)
                {
                    EnsureExists(forFile);
                    return ParseStatus[forFile].NeedReparse;
                }
            }

            public static bool IsReparsing(string forFile)
            {
                lock (_synchronizationParseStatus)
                {
                    EnsureExists(forFile);
                    return ParseStatus[forFile].IsReparsing;
                }

            }

            public static void NeedReparse_SetValue(string forFile, bool toValue)
            {
                lock (_synchronizationParseStatus)
                {
                    EnsureExists(forFile);
                    ParseStatus[forFile].NeedReparse = toValue;
                }
            }


        }


        public static int LastPreparseVersion(string forFile)
        {
            lock (_synchronizationParseStatus)
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
