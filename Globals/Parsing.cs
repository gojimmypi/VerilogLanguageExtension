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
        private static readonly object _synchronizationActiveParseData = new object();
        private static readonly Dictionary<string, ParseDataSnapshot> ParseDataByFile = new Dictionary<string, ParseDataSnapshot>(StringComparer.OrdinalIgnoreCase);
        private static string _activeParseFile = string.Empty;
        private static int _activeParseVersion = 0;

        public static event EventHandler ParseDataPublished;

        public sealed class VerilogDefinitionLocation
        {
            public string FilePath { get; private set; }
            public string Scope { get; private set; }
            public string Name { get; private set; }
            public int LineNumber { get; private set; }
            public int LinePosition { get; private set; }
            public int Length { get; private set; }
            public VerilogTokenTypes TokenType { get; private set; }
            public string HoverText { get; private set; }

            public VerilogDefinitionLocation(
                string scope,
                string name,
                int lineNumber,
                int linePosition,
                int length,
                VerilogTokenTypes tokenType,
                string hoverText)
                : this(string.Empty, scope, name, lineNumber, linePosition, length, tokenType, hoverText) {
            }

            public VerilogDefinitionLocation(
                string filePath,
                string scope,
                string name,
                int lineNumber,
                int linePosition,
                int length,
                VerilogTokenTypes tokenType,
                string hoverText) {

                FilePath = filePath ?? string.Empty;
                Scope = scope ?? string.Empty;
                Name = name ?? string.Empty;
                LineNumber = lineNumber;
                LinePosition = linePosition;
                Length = length;
                TokenType = tokenType;
                HoverText = hoverText ?? string.Empty;
            }

            public VerilogDefinitionLocation Clone() {
                return new VerilogDefinitionLocation(FilePath, Scope, Name, LineNumber, LinePosition, Length, TokenType, HoverText);
            }

            public VerilogDefinitionLocation CloneWithFilePath(string filePath) {
                return new VerilogDefinitionLocation(filePath, Scope, Name, LineNumber, LinePosition, Length, TokenType, HoverText);
            }
        }

        public sealed class ParseDataSnapshot
        {
            public string TargetFile { get; private set; }
            public int SnapshotVersion { get; private set; }
            public Dictionary<byte, string> ModuleNames { get; private set; }
            public Dictionary<string, byte> ModuleKeys { get; private set; }
            public List<BufferAttribute> BufferAttributes { get; private set; }
            public int[] BufferAttributeAtLineNumber { get; private set; }
            public Dictionary<string, Dictionary<string, VerilogTokenTypes>> VerilogVariables { get; private set; }
            public Dictionary<string, Dictionary<string, string>> VerilogVariableHoverText { get; private set; }
            public Dictionary<string, Dictionary<string, VerilogDefinitionLocation>> VerilogDefinitionLocations { get; private set; }

            public ParseDataSnapshot(
                string targetFile,
                int snapshotVersion,
                Dictionary<byte, string> moduleNames,
                Dictionary<string, byte> moduleKeys,
                List<BufferAttribute> bufferAttributes,
                int[] bufferAttributeAtLineNumber,
                Dictionary<string, Dictionary<string, VerilogTokenTypes>> verilogVariables,
                Dictionary<string, Dictionary<string, string>> verilogVariableHoverText,
                Dictionary<string, Dictionary<string, VerilogDefinitionLocation>> verilogDefinitionLocations) {

                TargetFile = targetFile;
                SnapshotVersion = snapshotVersion;
                ModuleNames = moduleNames ?? new Dictionary<byte, string> { { 0, "global" } };
                ModuleKeys = moduleKeys ?? new Dictionary<string, byte> { { "global", 0 } };
                BufferAttributes = bufferAttributes ?? new List<BufferAttribute>();
                BufferAttributeAtLineNumber = bufferAttributeAtLineNumber ?? new int[0];
                VerilogVariables = verilogVariables ?? new Dictionary<string, Dictionary<string, VerilogTokenTypes>>();
                VerilogVariableHoverText = verilogVariableHoverText ?? new Dictionary<string, Dictionary<string, string>>();
                VerilogDefinitionLocations = verilogDefinitionLocations ?? new Dictionary<string, Dictionary<string, VerilogDefinitionLocation>>();
            }

            private int GetBufferHint(int forLineNumber) {
                int thisHint = 0;
                if ((BufferAttributeAtLineNumber != null) && (BufferAttributeAtLineNumber.Length > forLineNumber) && (forLineNumber >= 0)) {
                    thisHint = BufferAttributeAtLineNumber[forLineNumber];
                    if (thisHint < 0 || thisHint >= BufferAttributes.Count) {
                        thisHint = 0;
                    }
                }
                return thisHint;
            }

            public string TextModuleName(int AtLine, int AtPosition) {
                string res = "global";

                int hint = GetBufferHint(AtLine);
                for (int i = hint; i < BufferAttributes.Count; i++) {
                    BufferAttribute thisBufferAttribute = BufferAttributes[i];
                    if (thisBufferAttribute.LineNumber == AtLine) {
                        byte thisModuleNameKey = thisBufferAttribute.ModuleNameKey;
                        ModuleNames.TryGetValue(thisModuleNameKey, out res);
                        break;
                    }

                    if (thisBufferAttribute.LineNumber > AtLine) {
                        break;
                    }
                }

                return string.IsNullOrEmpty(res) ? "global" : res;
            }

            public int BracketDepth(int AtLine, int AtPosition) {
                int res = 0;
                bool found = false;

                int starting_hint = 0;
                if ((BufferAttributeAtLineNumber != null) && (BufferAttributeAtLineNumber.Length > AtLine) && (AtLine >= 0)) {
                    starting_hint = BufferAttributeAtLineNumber[AtLine];
                    if (starting_hint > BufferAttributes.Count - 1) {
                        starting_hint = 0;
                    }
                }

                if (BufferAttributes != null && BufferAttributes.Count > 0) {
                    if (BufferAttributes[BufferAttributes.Count - 1] != null && BufferAttributes[BufferAttributes.Count - 1].LineNumber >= AtLine) {
                        for (int i = starting_hint; i < BufferAttributes.Count; i++) {
                            if (BufferAttributes[i] != null && BufferAttributes[i].LineNumber == AtLine) {
                                if (BufferAttributes[i].LineStart == AtPosition) {
                                    res = BufferAttributes[i].RoundBracketDepth +
                                          BufferAttributes[i].SquareBracketDepth +
                                          BufferAttributes[i].SquigglyBracketDepth;
                                    found = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (!found) {
                        int LastID = BufferAttributes.Count - 1;
                        res = BufferAttributes[LastID].RoundBracketDepth +
                              BufferAttributes[LastID].SquareBracketDepth +
                              BufferAttributes[LastID].SquigglyBracketDepth;
                    }
                }

                return res;
            }
        }

        private static Dictionary<string, Dictionary<string, VerilogTokenTypes>> CloneVariableMap(IDictionary<string, Dictionary<string, VerilogTokenTypes>> source) {
            Dictionary<string, Dictionary<string, VerilogTokenTypes>> result = new Dictionary<string, Dictionary<string, VerilogTokenTypes>>();
            if (source == null) {
                return result;
            }

            foreach (KeyValuePair<string, Dictionary<string, VerilogTokenTypes>> scope in source) {
                result[scope.Key] = scope.Value == null
                    ? new Dictionary<string, VerilogTokenTypes>()
                    : new Dictionary<string, VerilogTokenTypes>(scope.Value);
            }

            return result;
        }

        private static Dictionary<string, Dictionary<string, string>> CloneHoverMap(Dictionary<string, Dictionary<string, string>> source) {
            Dictionary<string, Dictionary<string, string>> result = new Dictionary<string, Dictionary<string, string>>();
            if (source == null) {
                return result;
            }

            foreach (KeyValuePair<string, Dictionary<string, string>> scope in source) {
                result[scope.Key] = scope.Value == null
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>(scope.Value);
            }

            return result;
        }

        private static Dictionary<string, Dictionary<string, VerilogDefinitionLocation>> CloneDefinitionMap(Dictionary<string, Dictionary<string, VerilogDefinitionLocation>> source, string filePath) {
            Dictionary<string, Dictionary<string, VerilogDefinitionLocation>> result = new Dictionary<string, Dictionary<string, VerilogDefinitionLocation>>();
            if (source == null) {
                return result;
            }

            foreach (KeyValuePair<string, Dictionary<string, VerilogDefinitionLocation>> scope in source) {
                Dictionary<string, VerilogDefinitionLocation> clonedScope = new Dictionary<string, VerilogDefinitionLocation>();
                if (scope.Value != null) {
                    foreach (KeyValuePair<string, VerilogDefinitionLocation> item in scope.Value) {
                        clonedScope[item.Key] = item.Value == null ? null : item.Value.CloneWithFilePath(filePath);
                    }
                }

                result[scope.Key] = clonedScope;
            }

            return result;
        }

        private static List<BufferAttribute> CloneBufferAttributes(List<BufferAttribute> source) {
            List<BufferAttribute> result = new List<BufferAttribute>();
            if (source == null) {
                return result;
            }

            foreach (BufferAttribute item in source) {
                if (item != null) {
                    result.Add((BufferAttribute)item.Clone());
                }
            }

            return result;
        }

        private static void ApplyParseDataSnapshot(ParseDataSnapshot parseData) {
            if (parseData == null) {
                return;
            }

            ModuleNames = parseData.ModuleNames;
            ModuleKeys = parseData.ModuleKeys;
            BufferAttributes = parseData.BufferAttributes;
            BufferAttribute_at_LineNumber = parseData.BufferAttributeAtLineNumber;
            VerilogVariables = parseData.VerilogVariables;
            VerilogVariableHoverText = parseData.VerilogVariableHoverText;
            VerilogDefinitionLocations = parseData.VerilogDefinitionLocations;
            BufferFirstParseComplete = true;
        }

        public static void PublishParseData(string targetFile, int snapshotVersion) {
            if (string.IsNullOrEmpty(targetFile) || snapshotVersion == 0) {
                return;
            }

            ParseDataSnapshot parseData = new ParseDataSnapshot(
                targetFile,
                snapshotVersion,
                ModuleNames == null ? null : new Dictionary<byte, string>(ModuleNames),
                ModuleKeys == null ? null : new Dictionary<string, byte>(ModuleKeys),
                CloneBufferAttributes(BufferAttributes),
                BufferAttribute_at_LineNumber == null ? null : (int[])BufferAttribute_at_LineNumber.Clone(),
                CloneVariableMap(VerilogVariables),
                CloneHoverMap(VerilogVariableHoverText),
                CloneDefinitionMap(VerilogDefinitionLocations, targetFile));

            lock (_synchronizationActiveParseData) {
                ParseDataByFile[targetFile] = parseData;
                ApplyParseDataSnapshot(parseData);
                _activeParseFile = targetFile;
                _activeParseVersion = snapshotVersion;
            }

            EventHandler handler = ParseDataPublished;
            if (handler != null) {
                handler(null, EventArgs.Empty);
            }
        }

        public static bool TryGetParseData(string targetFile, ITextBuffer buffer, bool allowStale, out ParseDataSnapshot parseData) {
            parseData = null;
            if (string.IsNullOrEmpty(targetFile) || buffer == null) {
                return false;
            }

            int snapshotVersion;
            try {
                snapshotVersion = buffer.CurrentSnapshot.Version.VersionNumber;
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine("TryGetParseData failed to get buffer.CurrentSnapshot.Version.VersionNumber", ex.Message);
                return false;
            }

            return TryGetParseData(targetFile, snapshotVersion, allowStale, out parseData);
        }

        public static bool TryGetParseData(string targetFile, int snapshotVersion, bool allowStale, out ParseDataSnapshot parseData) {
            parseData = null;
            if (string.IsNullOrEmpty(targetFile)) {
                return false;
            }

            lock (_synchronizationActiveParseData) {
                if (!ParseDataByFile.TryGetValue(targetFile, out parseData) || parseData == null) {
                    return false;
                }

                if (allowStale || snapshotVersion == 0 || parseData.SnapshotVersion == snapshotVersion) {
                    return true;
                }

                parseData = null;
                return false;
            }
        }

        public static bool TryGetModuleDefinitionFromParsedFiles(string moduleName, out VerilogDefinitionLocation definition) {
            definition = null;

            if (string.IsNullOrEmpty(moduleName)) {
                return false;
            }

            lock (_synchronizationActiveParseData) {
                foreach (ParseDataSnapshot parseData in ParseDataByFile.Values) {
                    if (parseData == null || parseData.VerilogDefinitionLocations == null) {
                        continue;
                    }

                    Dictionary<string, VerilogDefinitionLocation> moduleDefinitions;
                    if (!parseData.VerilogDefinitionLocations.TryGetValue(moduleName, out moduleDefinitions) || moduleDefinitions == null) {
                        continue;
                    }

                    VerilogDefinitionLocation candidate;
                    if (moduleDefinitions.TryGetValue(moduleName, out candidate) && candidate != null) {
                        definition = CloneDefinitionCandidate(parseData, candidate);
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool TryGetDefinitionFromParsedFiles(
            string lookupText,
            string currentFile,
            out VerilogDefinitionLocation definition) {

            definition = null;

            if (string.IsNullOrEmpty(lookupText)) {
                return false;
            }

            string macroName = lookupText.StartsWith("`", StringComparison.Ordinal)
                ? lookupText.Substring(1)
                : string.Empty;

            lock (_synchronizationActiveParseData) {
                foreach (ParseDataSnapshot parseData in ParseDataByFile.Values) {
                    if (parseData == null || parseData.VerilogDefinitionLocations == null ||
                        IsSameParsedFile(parseData.TargetFile, currentFile)) {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(macroName) &&
                        TryGetDefinitionFromParsedScope(parseData, SCOPE_MACRO, macroName, out definition)) {
                        return true;
                    }

                    if (TryGetDefinitionFromParsedScope(parseData, SCOPE_MACRO, lookupText, out definition)) {
                        return true;
                    }

                    if (TryGetDefinitionFromParsedScope(parseData, SCOPE_CONST, lookupText, out definition)) {
                        return true;
                    }
                }

                if (!IsCrossFileDefinitionLookupCandidate(lookupText)) {
                    return false;
                }

                foreach (ParseDataSnapshot parseData in ParseDataByFile.Values) {
                    if (parseData == null || parseData.VerilogDefinitionLocations == null ||
                        IsSameParsedFile(parseData.TargetFile, currentFile)) {
                        continue;
                    }

                    if (TryGetAnyDefinitionFromParsedFile(parseData, lookupText, out definition)) {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryGetDefinitionFromParsedScope(
            ParseDataSnapshot parseData,
            string scope,
            string lookupText,
            out VerilogDefinitionLocation definition) {

            definition = null;

            if (parseData == null || parseData.VerilogDefinitionLocations == null ||
                string.IsNullOrEmpty(scope) || string.IsNullOrEmpty(lookupText)) {
                return false;
            }

            Dictionary<string, VerilogDefinitionLocation> scopeDefinitions;
            if (!parseData.VerilogDefinitionLocations.TryGetValue(scope, out scopeDefinitions) || scopeDefinitions == null) {
                return false;
            }

            VerilogDefinitionLocation candidate;
            if (!scopeDefinitions.TryGetValue(lookupText, out candidate) || candidate == null) {
                return false;
            }

            definition = CloneDefinitionCandidate(parseData, candidate);
            return true;
        }

        private static bool TryGetAnyDefinitionFromParsedFile(
            ParseDataSnapshot parseData,
            string lookupText,
            out VerilogDefinitionLocation definition) {

            definition = null;

            if (parseData == null || parseData.VerilogDefinitionLocations == null || string.IsNullOrEmpty(lookupText)) {
                return false;
            }

            foreach (Dictionary<string, VerilogDefinitionLocation> scopeDefinitions in parseData.VerilogDefinitionLocations.Values) {
                if (scopeDefinitions == null) {
                    continue;
                }

                VerilogDefinitionLocation candidate;
                if (scopeDefinitions.TryGetValue(lookupText, out candidate) && candidate != null) {
                    definition = CloneDefinitionCandidate(parseData, candidate);
                    return true;
                }
            }

            return false;
        }

        private static VerilogDefinitionLocation CloneDefinitionCandidate(ParseDataSnapshot parseData, VerilogDefinitionLocation candidate) {
            if (candidate == null) {
                return null;
            }

            if (string.IsNullOrEmpty(candidate.FilePath) && parseData != null) {
                return candidate.CloneWithFilePath(parseData.TargetFile);
            }

            return candidate.Clone();
        }

        private static bool IsCrossFileDefinitionLookupCandidate(string lookupText) {
            if (string.IsNullOrEmpty(lookupText)) {
                return false;
            }

            string identifier = lookupText.StartsWith("`", StringComparison.Ordinal)
                ? lookupText.Substring(1)
                : lookupText;

            if (identifier.Length == 0) {
                return false;
            }

            bool hasLetter = false;
            for (int i = 0; i < identifier.Length; i++) {
                char c = identifier[i];
                if (c >= 'a' && c <= 'z') {
                    return false;
                }

                if (c >= 'A' && c <= 'Z') {
                    hasLetter = true;
                    continue;
                }

                if ((c >= '0' && c <= '9') || c == '_' || c == '$') {
                    continue;
                }

                return false;
            }

            return hasLetter;
        }

        private static bool IsSameParsedFile(string firstPath, string secondPath) {
            if (string.IsNullOrEmpty(firstPath) || string.IsNullOrEmpty(secondPath)) {
                return false;
            }

            try {
                firstPath = System.IO.Path.GetFullPath(firstPath);
                secondPath = System.IO.Path.GetFullPath(secondPath);
            }
            catch (ArgumentException) {
            }
            catch (NotSupportedException) {
            }
            catch (System.IO.PathTooLongException) {
            }

            return string.Equals(firstPath, secondPath, StringComparison.OrdinalIgnoreCase);
        }


        public static bool IsActiveParseData(string targetFile, ITextBuffer buffer) {
            ParseDataSnapshot parseData;
            return TryGetParseData(targetFile, buffer, false, out parseData);
        }

        public static void SetActiveParseData(string targetFile, int snapshotVersion) {
            if (string.IsNullOrEmpty(targetFile) || snapshotVersion == 0) {
                return;
            }

            lock (_synchronizationActiveParseData) {
                ParseDataSnapshot parseData;
                if (ParseDataByFile.TryGetValue(targetFile, out parseData) && parseData != null && parseData.SnapshotVersion == snapshotVersion) {
                    ApplyParseDataSnapshot(parseData);
                }

                _activeParseFile = targetFile;
                _activeParseVersion = snapshotVersion;
            }
        }

        /// <summary>
        /// ParseStatusController - thread-safe lock around access to shared ParseStatus dictionary
        /// </summary>
        public static class ParseStatusController
        {
            /// <summary>
            /// Init  - we want to make sure the ParseStatus for this file is freshly initialized (e.g. when opening / re-opening a file)
            /// </summary>
            /// <param name="targetFile"></param>
            public static void Init(string targetFile) {
                lock (_synchronizationParseStatus) {
                    if (!ParseStatus.ContainsKey(targetFile)) {
                        ParseStatus.Add(targetFile, new ParseAttribute());
                    }
                    else {
                        ParseStatus[targetFile] = new ParseAttribute();
                    }
                }
            }

            /// <summary>
            /// EnsureExists_ParseStatus - we want to make sure our respective ParseStatus item exists, but NOT refresh it.
            /// </summary>
            /// <param name="targetFile"></param>
            public static void EnsureExists(string targetFile) {
                if (string.IsNullOrEmpty(targetFile)) {
                    return;
                }

                lock (_synchronizationParseStatus) {
                    if (!ParseStatus.TryGetValue(targetFile, out _)) {
                        Init(targetFile);
                    }
                }
            }

            /// <summary>
            /// ParseStatus_NeedReparse - creates ParseStatus for file as needed and checks if it needs to be raparsed
            /// </summary>
            /// <param name="forFile"></param>
            /// <returns></returns>
            public static bool NeedReparse(string forFile) {
                if (string.IsNullOrEmpty(forFile)) {
                    return false;
                }

                lock (_synchronizationParseStatus) {
                    EnsureExists(forFile);
                    return ParseStatus[forFile].NeedReparse;
                }
            }

            public static bool IsReparsing(string forFile) {
                if (string.IsNullOrEmpty(forFile)) {
                    return false;
                }

                lock (_synchronizationParseStatus) {
                    EnsureExists(forFile);
                    return ParseStatus[forFile].IsReparsing;
                }

            }

            public static void NeedReparse_SetValue(string forFile, bool toValue) {
                if (string.IsNullOrEmpty(forFile)) {
                    return;
                }
                lock (_synchronizationParseStatus) {
                    EnsureExists(forFile);
                    ParseStatus[forFile].NeedReparse = toValue;
                }
            }


        }


        public static int LastPreparseVersion(string forFile) {
            lock (_synchronizationParseStatus) {
                if (ParseStatus.ContainsKey(forFile)) {
                    return ParseStatus[forFile].LastReparseVersion;
                }
                else {
                    ParseAttribute newParseAttribute = new ParseAttribute();
                    ParseStatus.Add(forFile, newParseAttribute);
                    return 0;
                }
            }
        }
    }
}
