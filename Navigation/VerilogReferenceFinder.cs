using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.IO;
using VerilogLanguage.VerilogToken;

namespace VerilogLanguage.Navigation
{
    internal static class VerilogReferenceFinder
    {
        private const int MaxReferenceSearchFiles = 600;
        private const long MaxReferenceSearchFileBytes = 1024 * 1024;

        internal enum VerilogReferenceKind
        {
            Declaration,
            Reference,
            Read,
            Write
        }

        internal sealed class VerilogReferenceLocation
        {
            public string FilePath { get; private set; }
            public int LineNumber { get; private set; }
            public int LinePosition { get; private set; }
            public int Length { get; private set; }
            public string ContainingType { get; private set; }
            public string ContainingMember { get; private set; }
            public VerilogReferenceKind Kind { get; private set; }
            public string LineText { get; private set; }

            public VerilogReferenceLocation(
                string filePath,
                int lineNumber,
                int linePosition,
                int length,
                string containingType,
                string containingMember,
                VerilogReferenceKind kind,
                string lineText) {

                FilePath = filePath ?? string.Empty;
                LineNumber = lineNumber;
                LinePosition = linePosition;
                Length = length;
                ContainingType = containingType ?? string.Empty;
                ContainingMember = containingMember ?? string.Empty;
                Kind = kind;
                LineText = lineText ?? string.Empty;
            }
        }

        private sealed class IdentifierOccurrence
        {
            public int LinePosition { get; private set; }
            public int Length { get; private set; }

            public IdentifierOccurrence(int linePosition, int length) {
                LinePosition = linePosition;
                Length = length;
            }
        }

        internal static bool TryFindReferences(
            SnapshotSpan tokenSpan,
            string lookupText,
            out VerilogGlobals.VerilogDefinitionLocation definition,
            out List<VerilogReferenceLocation> references,
            out string failureMessage) {

            definition = null;
            references = new List<VerilogReferenceLocation>();
            failureMessage = string.Empty;

            if (tokenSpan.Snapshot == null || string.IsNullOrEmpty(lookupText)) {
                failureMessage = "Place the caret on a Verilog identifier first.";
                return false;
            }

            if (!VerilogDefinitionResolver.TryFindDefinition(tokenSpan, lookupText, out definition) || definition == null) {
                failureMessage = "No definition was found for '" + lookupText + "'.";
                return false;
            }

            ITextSnapshot snapshot = tokenSpan.Snapshot;
            string currentFile = VerilogGlobals.GetDocumentPath(snapshot);
            if (string.IsNullOrEmpty(currentFile)) {
                failureMessage = "The active Verilog editor is not backed by a file.";
                return false;
            }

            VerilogGlobals.ParseDataSnapshot currentParseData;
            if (!TryGetCurrentParseData(snapshot, currentFile, out currentParseData)) {
                failureMessage = "VLE parse data was not available for the active Verilog file.";
                return false;
            }

            string normalizedLookupText = NormalizeLookupText(lookupText, definition);
            if (string.IsNullOrEmpty(normalizedLookupText)) {
                failureMessage = "The selected text is not a supported Verilog identifier.";
                return false;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool globalSearch = IsGlobalSearch(definition, lookupText);

            foreach (string filePath in CandidateReferenceFiles(currentFile, definition, globalSearch)) {
                VerilogGlobals.ParseDataSnapshot parseData = IsSameFilePath(filePath, currentFile)
                    ? currentParseData
                    : null;

                string[] lines;
                if (IsSameFilePath(filePath, currentFile)) {
                    lines = SnapshotLines(snapshot);
                }
                else if (!TryReadSmallTextFile(filePath, out lines)) {
                    continue;
                }

                FindReferencesInLines(filePath, lines, parseData, definition, normalizedLookupText, lookupText, seen, references);
            }

            references.Sort(CompareReferences);
            return true;
        }

        private static bool TryGetCurrentParseData(
            ITextSnapshot snapshot,
            string currentFile,
            out VerilogGlobals.ParseDataSnapshot parseData) {

            parseData = null;
            if (snapshot == null || string.IsNullOrEmpty(currentFile)) {
                return false;
            }

            if (VerilogGlobals.TryGetParseData(currentFile, snapshot.Version.VersionNumber, false, out parseData)) {
                return true;
            }

            VerilogGlobals.ParseStatusController.NeedReparse_SetValue(currentFile, true);
            VerilogGlobals.Reparse(snapshot.TextBuffer, currentFile);
            return VerilogGlobals.TryGetParseData(currentFile, snapshot.Version.VersionNumber, false, out parseData);
        }

        private static string NormalizeLookupText(string lookupText, VerilogGlobals.VerilogDefinitionLocation definition) {
            if (definition != null && !string.IsNullOrEmpty(definition.Name)) {
                return definition.Name;
            }

            if (string.IsNullOrEmpty(lookupText)) {
                return string.Empty;
            }

            return lookupText.StartsWith("`", StringComparison.Ordinal)
                ? lookupText.Substring(1)
                : lookupText;
        }

        private static string[] SnapshotLines(ITextSnapshot snapshot) {
            string[] lines = new string[snapshot.LineCount];
            for (int i = 0; i < snapshot.LineCount; i++) {
                lines[i] = snapshot.GetLineFromLineNumber(i).GetText();
            }

            return lines;
        }

        private static void FindReferencesInLines(
            string filePath,
            string[] lines,
            VerilogGlobals.ParseDataSnapshot parseData,
            VerilogGlobals.VerilogDefinitionLocation definition,
            string lookupName,
            string originalLookupText,
            HashSet<string> seen,
            List<VerilogReferenceLocation> references) {

            if (lines == null || string.IsNullOrEmpty(lookupName)) {
                return;
            }

            bool isMacroLookup = IsMacroLookup(definition, originalLookupText);
            bool inBlockComment = false;

            for (int lineNumber = 0; lineNumber < lines.Length; lineNumber++) {
                string lineText = lines[lineNumber] ?? string.Empty;
                List<IdentifierOccurrence> occurrences = FindIdentifierOccurrencesInCodeLine(lineText, lookupName, isMacroLookup, ref inBlockComment);
                if (occurrences.Count == 0) {
                    continue;
                }

                foreach (IdentifierOccurrence occurrence in occurrences) {
                    if (!IsReferenceToResolvedSymbol(filePath, lines, lineNumber, occurrence.LinePosition, parseData, definition, originalLookupText)) {
                        continue;
                    }

                    string key = filePath + ":" + lineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + occurrence.LinePosition.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (!seen.Add(key)) {
                        continue;
                    }

                    string containingType;
                    string containingMember;
                    GetContainingScope(parseData, lines, lineNumber, occurrence.LinePosition, out containingType, out containingMember);

                    VerilogReferenceKind kind = ClassifyReferenceKind(lineText, occurrence.LinePosition, occurrence.Length, definition, filePath, lineNumber);
                    references.Add(new VerilogReferenceLocation(
                        filePath,
                        lineNumber,
                        occurrence.LinePosition,
                        occurrence.Length,
                        containingType,
                        containingMember,
                        kind,
                        lineText.Trim()));
                }
            }
        }

        private static List<IdentifierOccurrence> FindIdentifierOccurrencesInCodeLine(
            string lineText,
            string lookupName,
            bool isMacroLookup,
            ref bool inBlockComment) {

            List<IdentifierOccurrence> occurrences = new List<IdentifierOccurrence>();
            if (string.IsNullOrEmpty(lineText) || string.IsNullOrEmpty(lookupName)) {
                return occurrences;
            }

            int index = 0;
            while (index < lineText.Length) {
                if (inBlockComment) {
                    int blockEnd = lineText.IndexOf("*/", index, StringComparison.Ordinal);
                    if (blockEnd < 0) {
                        return occurrences;
                    }

                    inBlockComment = false;
                    index = blockEnd + 2;
                    continue;
                }

                if (index + 1 < lineText.Length && lineText[index] == '/' && lineText[index + 1] == '/') {
                    return occurrences;
                }

                if (index + 1 < lineText.Length && lineText[index] == '/' && lineText[index + 1] == '*') {
                    inBlockComment = true;
                    index += 2;
                    continue;
                }

                if (lineText[index] == '"') {
                    index = SkipString(lineText, index + 1);
                    continue;
                }

                if (isMacroLookup && lineText[index] == '`') {
                    int macroStart = index + 1;
                    if (StartsWithIdentifierAt(lineText, macroStart, lookupName)) {
                        occurrences.Add(new IdentifierOccurrence(macroStart, lookupName.Length));
                        index = macroStart + lookupName.Length;
                        continue;
                    }
                }

                if (StartsWithIdentifierAt(lineText, index, lookupName)) {
                    if (isMacroLookup) {
                        if (LooksLikeMacroDirectiveName(lineText, index)) {
                            occurrences.Add(new IdentifierOccurrence(index, lookupName.Length));
                        }
                    }
                    else if (!IsDotPrefixedPortName(lineText, index)) {
                        occurrences.Add(new IdentifierOccurrence(index, lookupName.Length));
                    }

                    index += lookupName.Length;
                    continue;
                }

                index++;
            }

            return occurrences;
        }

        private static bool LooksLikeMacroDirectiveName(string lineText, int index) {
            if (string.IsNullOrEmpty(lineText) || index <= 0 || index > lineText.Length) {
                return false;
            }

            string beforeName = lineText.Substring(0, index).TrimEnd();
            return beforeName.EndsWith("`define", StringComparison.Ordinal) ||
                beforeName.EndsWith("`ifdef", StringComparison.Ordinal) ||
                beforeName.EndsWith("`ifndef", StringComparison.Ordinal) ||
                beforeName.EndsWith("`elsif", StringComparison.Ordinal) ||
                beforeName.EndsWith("`undef", StringComparison.Ordinal);
        }

        private static int SkipString(string lineText, int index) {
            while (index < lineText.Length) {
                if (lineText[index] == '\\') {
                    index += 2;
                    continue;
                }

                if (lineText[index] == '"') {
                    return index + 1;
                }

                index++;
            }

            return lineText.Length;
        }

        private static bool StartsWithIdentifierAt(string text, int index, string identifier) {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(identifier) || index < 0 || index + identifier.Length > text.Length) {
                return false;
            }

            if (!string.Equals(text.Substring(index, identifier.Length), identifier, StringComparison.Ordinal)) {
                return false;
            }

            int beforeIndex = index - 1;
            int afterIndex = index + identifier.Length;
            bool beforeOk = beforeIndex < 0 || !IsIdentifierCharacter(text[beforeIndex]);
            bool afterOk = afterIndex >= text.Length || !IsIdentifierCharacter(text[afterIndex]);
            return beforeOk && afterOk;
        }

        private static bool IsDotPrefixedPortName(string lineText, int index) {
            int before = index - 1;
            while (before >= 0 && char.IsWhiteSpace(lineText[before])) {
                before--;
            }

            return before >= 0 && lineText[before] == '.';
        }

        private static bool IsReferenceToResolvedSymbol(
            string filePath,
            string[] lines,
            int lineNumber,
            int linePosition,
            VerilogGlobals.ParseDataSnapshot parseData,
            VerilogGlobals.VerilogDefinitionLocation definition,
            string originalLookupText) {

            if (definition == null) {
                return false;
            }

            if (IsGlobalSearch(definition, originalLookupText)) {
                return true;
            }

            if (!IsSameFilePath(filePath, definition.FilePath)) {
                return false;
            }

            if (parseData == null) {
                return true;
            }

            if (IsLocalScope(definition.Scope)) {
                string activeLocalScope;
                return TryFindActiveLocalScope(parseData, lines, lineNumber, out activeLocalScope) &&
                    string.Equals(activeLocalScope, definition.Scope, StringComparison.Ordinal);
            }

            string moduleScope = parseData.TextModuleName(lineNumber, linePosition);
            return string.Equals(moduleScope, definition.Scope, StringComparison.Ordinal);
        }

        private static bool TryFindActiveLocalScope(
            VerilogGlobals.ParseDataSnapshot parseData,
            string[] lines,
            int lineNumber,
            out string activeLocalScope) {

            activeLocalScope = string.Empty;
            if (parseData == null || lines == null || lineNumber < 0) {
                return false;
            }

            int lastLine = Math.Min(lineNumber, lines.Length - 1);
            for (int i = lastLine; i >= 0; i--) {
                string lineText = lines[i] ?? string.Empty;

                if (VerilogGlobals.IsEndFunctionLineText(lineText) || VerilogGlobals.IsEndTaskLineText(lineText)) {
                    return false;
                }

                string functionName;
                if (VerilogGlobals.TryGetFunctionNameFromLineText(lineText, out functionName)) {
                    string moduleScope = parseData.TextModuleName(i, 0);
                    activeLocalScope = VerilogGlobals.FunctionLocalScopeName(moduleScope, functionName);
                    return true;
                }

                string taskName;
                if (VerilogGlobals.TryGetTaskNameFromLineText(lineText, out taskName)) {
                    string moduleScope = parseData.TextModuleName(i, 0);
                    activeLocalScope = VerilogGlobals.TaskLocalScopeName(moduleScope, taskName);
                    return true;
                }
            }

            return false;
        }

        private static void GetContainingScope(
            VerilogGlobals.ParseDataSnapshot parseData,
            string[] lines,
            int lineNumber,
            int linePosition,
            out string containingType,
            out string containingMember) {

            containingType = string.Empty;
            containingMember = string.Empty;

            if (parseData == null) {
                return;
            }

            string activeLocalScope;
            if (TryFindActiveLocalScope(parseData, lines, lineNumber, out activeLocalScope) && !string.IsNullOrEmpty(activeLocalScope)) {
                containingType = parseData.TextModuleName(lineNumber, linePosition);
                containingMember = LocalScopeDisplayName(activeLocalScope);
                return;
            }

            containingType = parseData.TextModuleName(lineNumber, linePosition);
        }

        private static string LocalScopeDisplayName(string scope) {
            if (string.IsNullOrEmpty(scope)) {
                return string.Empty;
            }

            int marker = scope.IndexOf(VerilogGlobals.SCOPE_FUNCTION_PREFIX, StringComparison.Ordinal);
            if (marker >= 0) {
                return "function " + scope.Substring(marker + VerilogGlobals.SCOPE_FUNCTION_PREFIX.Length + 2);
            }

            marker = scope.IndexOf(VerilogGlobals.SCOPE_TASK_PREFIX, StringComparison.Ordinal);
            if (marker >= 0) {
                return "task " + scope.Substring(marker + VerilogGlobals.SCOPE_TASK_PREFIX.Length + 2);
            }

            return scope;
        }

        private static VerilogReferenceKind ClassifyReferenceKind(
            string lineText,
            int linePosition,
            int length,
            VerilogGlobals.VerilogDefinitionLocation definition,
            string filePath,
            int lineNumber) {

            if (definition != null && IsSameFilePath(filePath, definition.FilePath) &&
                lineNumber == definition.LineNumber && linePosition == definition.LinePosition) {
                return VerilogReferenceKind.Declaration;
            }

            string codeBefore = linePosition <= lineText.Length
                ? lineText.Substring(0, linePosition)
                : lineText;

            if (LooksLikeDeclarationBeforeName(codeBefore) && !HasAssignmentOperator(codeBefore)) {
                return VerilogReferenceKind.Declaration;
            }

            if (LooksLikeWrite(lineText, linePosition, length)) {
                return VerilogReferenceKind.Write;
            }

            return VerilogReferenceKind.Read;
        }

        private static bool LooksLikeDeclarationBeforeName(string textBeforeName) {
            if (string.IsNullOrEmpty(textBeforeName)) {
                return false;
            }

            return ContainsWord(textBeforeName, "module") ||
                ContainsWord(textBeforeName, "function") ||
                ContainsWord(textBeforeName, "task") ||
                ContainsWord(textBeforeName, "localparam") ||
                ContainsWord(textBeforeName, "parameter") ||
                ContainsWord(textBeforeName, "input") ||
                ContainsWord(textBeforeName, "output") ||
                ContainsWord(textBeforeName, "inout") ||
                ContainsWord(textBeforeName, "wire") ||
                ContainsWord(textBeforeName, "reg") ||
                ContainsWord(textBeforeName, "logic") ||
                ContainsWord(textBeforeName, "bit") ||
                ContainsWord(textBeforeName, "genvar");
        }

        private static bool HasAssignmentOperator(string text) {
            if (string.IsNullOrEmpty(text)) {
                return false;
            }

            for (int i = 0; i < text.Length; i++) {
                if (text[i] == '<' && i + 1 < text.Length && text[i + 1] == '=') {
                    return true;
                }

                if (text[i] == '=' && !IsEqualityOperator(text, i)) {
                    return true;
                }
            }

            return false;
        }

        private static bool LooksLikeWrite(string lineText, int linePosition, int length) {
            int index = linePosition + length;
            int squareDepth = 0;

            while (index < lineText.Length) {
                char c = lineText[index];
                if (c == '[') {
                    squareDepth++;
                    index++;
                    continue;
                }

                if (c == ']') {
                    if (squareDepth > 0) {
                        squareDepth--;
                    }
                    index++;
                    continue;
                }

                if (squareDepth > 0 || char.IsWhiteSpace(c)) {
                    index++;
                    continue;
                }

                if (c == '<' && index + 1 < lineText.Length && lineText[index + 1] == '=') {
                    return true;
                }

                if (c == '=' && !IsEqualityOperator(lineText, index)) {
                    return true;
                }

                return false;
            }

            return false;
        }

        private static bool IsEqualityOperator(string lineText, int index) {
            if (index + 1 < lineText.Length && lineText[index + 1] == '=') {
                return true;
            }

            if (index > 0 && (lineText[index - 1] == '=' || lineText[index - 1] == '!' || lineText[index - 1] == '<' || lineText[index - 1] == '>')) {
                return true;
            }

            return false;
        }

        private static IEnumerable<string> CandidateReferenceFiles(
            string currentFile,
            VerilogGlobals.VerilogDefinitionLocation definition,
            bool globalSearch) {

            HashSet<string> visitedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(currentFile)) {
                visitedFiles.Add(currentFile);
                yield return currentFile;
            }

            if (!globalSearch) {
                if (definition != null && !string.IsNullOrEmpty(definition.FilePath) &&
                    !visitedFiles.Contains(definition.FilePath) && File.Exists(definition.FilePath)) {
                    visitedFiles.Add(definition.FilePath);
                    yield return definition.FilePath;
                }

                yield break;
            }

            string rootDirectory = FindSearchRoot(currentFile);
            if (string.IsNullOrEmpty(rootDirectory) || !Directory.Exists(rootDirectory)) {
                yield break;
            }

            int returnedFiles = 0;
            Stack<string> pendingDirectories = new Stack<string>();
            pendingDirectories.Push(rootDirectory);

            while (pendingDirectories.Count > 0 && returnedFiles < MaxReferenceSearchFiles) {
                string directory = pendingDirectories.Pop();

                string[] files;
                try {
                    files = Directory.GetFiles(directory);
                }
                catch (IOException) {
                    continue;
                }
                catch (UnauthorizedAccessException) {
                    continue;
                }

                foreach (string filePath in files) {
                    if (!IsVerilogSourceFile(filePath) || !visitedFiles.Add(filePath)) {
                        continue;
                    }

                    returnedFiles++;
                    yield return filePath;

                    if (returnedFiles >= MaxReferenceSearchFiles) {
                        yield break;
                    }
                }

                string[] childDirectories;
                try {
                    childDirectories = Directory.GetDirectories(directory);
                }
                catch (IOException) {
                    continue;
                }
                catch (UnauthorizedAccessException) {
                    continue;
                }

                foreach (string childDirectory in childDirectories) {
                    if (!ShouldSkipSearchDirectory(childDirectory)) {
                        pendingDirectories.Push(childDirectory);
                    }
                }
            }
        }

        private static bool TryReadSmallTextFile(string filePath, out string[] lines) {
            lines = null;

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) {
                return false;
            }

            try {
                FileInfo fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > MaxReferenceSearchFileBytes) {
                    return false;
                }

                lines = File.ReadAllLines(filePath);
                return true;
            }
            catch (IOException) {
                return false;
            }
            catch (UnauthorizedAccessException) {
                return false;
            }
            catch (NotSupportedException) {
                return false;
            }
        }

        private static string FindSearchRoot(string currentFile) {
            if (string.IsNullOrEmpty(currentFile)) {
                return string.Empty;
            }

            DirectoryInfo directory = null;
            try {
                directory = new FileInfo(currentFile).Directory;
            }
            catch (ArgumentException) {
                return string.Empty;
            }
            catch (NotSupportedException) {
                return string.Empty;
            }

            if (directory == null) {
                return string.Empty;
            }

            DirectoryInfo bestDirectory = directory;
            for (int i = 0; i < 6 && directory != null; i++) {
                if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || Directory.GetFiles(directory.FullName, "*.sln").Length > 0) {
                    return directory.FullName;
                }

                bestDirectory = directory;
                directory = directory.Parent;
            }

            return bestDirectory.FullName;
        }

        private static bool IsGlobalSearch(VerilogGlobals.VerilogDefinitionLocation definition, string lookupText) {
            if (definition == null) {
                return false;
            }

            if (IsMacroLookup(definition, lookupText)) {
                return true;
            }

            if (!string.IsNullOrEmpty(definition.Name) &&
                string.Equals(definition.Scope, definition.Name, StringComparison.Ordinal)) {
                return true;
            }

            return false;
        }

        private static bool IsMacroLookup(VerilogGlobals.VerilogDefinitionLocation definition, string lookupText) {
            return (!string.IsNullOrEmpty(lookupText) && lookupText.StartsWith("`", StringComparison.Ordinal)) ||
                (definition != null && string.Equals(definition.Scope, VerilogGlobals.SCOPE_MACRO, StringComparison.Ordinal));
        }

        private static bool IsLocalScope(string scope) {
            if (string.IsNullOrEmpty(scope)) {
                return false;
            }

            return scope.IndexOf(VerilogGlobals.SCOPE_FUNCTION_PREFIX, StringComparison.Ordinal) >= 0 ||
                scope.IndexOf(VerilogGlobals.SCOPE_TASK_PREFIX, StringComparison.Ordinal) >= 0;
        }

        private static bool ContainsWord(string text, string word) {
            return IndexOfIdentifier(text, word) >= 0;
        }

        private static int IndexOfIdentifier(string text, string identifier) {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(identifier)) {
                return -1;
            }

            int startIndex = 0;
            while (startIndex < text.Length) {
                int foundIndex = text.IndexOf(identifier, startIndex, StringComparison.Ordinal);
                if (foundIndex < 0) {
                    return -1;
                }

                int beforeIndex = foundIndex - 1;
                int afterIndex = foundIndex + identifier.Length;
                bool beforeOk = beforeIndex < 0 || !IsIdentifierCharacter(text[beforeIndex]);
                bool afterOk = afterIndex >= text.Length || !IsIdentifierCharacter(text[afterIndex]);
                if (beforeOk && afterOk) {
                    return foundIndex;
                }

                startIndex = foundIndex + identifier.Length;
            }

            return -1;
        }

        private static bool IsIdentifierCharacter(char c) {
            return char.IsLetterOrDigit(c) || c == '_' || c == '$' || c == '\\';
        }

        private static bool IsVerilogSourceFile(string filePath) {
            string extension = Path.GetExtension(filePath);
            return string.Equals(extension, ".v", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".sv", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".vh", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".svh", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldSkipSearchDirectory(string directory) {
            string name = Path.GetFileName(directory);
            return string.Equals(name, ".git", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, ".vs", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "bin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "obj", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "packages", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSameFilePath(string firstPath, string secondPath) {
            if (string.IsNullOrEmpty(firstPath) || string.IsNullOrEmpty(secondPath)) {
                return false;
            }

            try {
                firstPath = Path.GetFullPath(firstPath);
                secondPath = Path.GetFullPath(secondPath);
            }
            catch (ArgumentException) {
            }
            catch (NotSupportedException) {
            }
            catch (PathTooLongException) {
            }

            return string.Equals(firstPath, secondPath, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareReferences(VerilogReferenceLocation first, VerilogReferenceLocation second) {
            int fileCompare = string.Compare(first.FilePath, second.FilePath, StringComparison.OrdinalIgnoreCase);
            if (fileCompare != 0) {
                return fileCompare;
            }

            int lineCompare = first.LineNumber.CompareTo(second.LineNumber);
            if (lineCompare != 0) {
                return lineCompare;
            }

            return first.LinePosition.CompareTo(second.LinePosition);
        }
    }
}
