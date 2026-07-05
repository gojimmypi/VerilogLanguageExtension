// file: Navigation/VerilogDefinitionResolver.cs
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

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.IO;

namespace VerilogLanguage.Navigation
{
    internal static class VerilogDefinitionResolver
    {
        internal static bool TryGetIdentifierSpanAtCaret(IWpfTextView textView, out SnapshotSpan tokenSpan, out string lookupText) {
            tokenSpan = new SnapshotSpan();
            lookupText = string.Empty;

            if (textView == null || textView.TextBuffer == null || textView.TextBuffer.CurrentSnapshot == null) {
                return false;
            }

            return TryGetIdentifierSpanAtPosition(
                textView.TextBuffer.CurrentSnapshot,
                textView.Caret.Position.BufferPosition.Position,
                out tokenSpan,
                out lookupText);
        }

        internal static bool TryGetIdentifierSpanAtPosition(ITextSnapshot snapshot, int position, out SnapshotSpan tokenSpan, out string lookupText) {
            tokenSpan = new SnapshotSpan();
            lookupText = string.Empty;

            if (snapshot == null || snapshot.Length == 0) {
                return false;
            }

            if (position >= snapshot.Length) {
                position = snapshot.Length - 1;
            }

            if (position < 0) {
                return false;
            }

            if (!IsIdentifierCharacter(snapshot[position]) && position > 0 && IsIdentifierCharacter(snapshot[position - 1])) {
                position--;
            }

            if (!IsIdentifierCharacter(snapshot[position])) {
                return false;
            }

            int start = position;
            while (start > 0 && IsIdentifierCharacter(snapshot[start - 1])) {
                start--;
            }

            if (start > 0 && snapshot[start - 1] == '`') {
                start--;
            }

            int end = position + 1;
            while (end < snapshot.Length && IsIdentifierCharacter(snapshot[end])) {
                end++;
            }

            if (end <= start) {
                return false;
            }

            tokenSpan = new SnapshotSpan(snapshot, Span.FromBounds(start, end));
            lookupText = tokenSpan.GetText();

            return IsIdentifierLookupText(lookupText);
        }

        internal static bool TryFindDefinition(
            SnapshotSpan tokenSpan,
            string lookupText,
            out VerilogGlobals.VerilogDefinitionLocation definition) {

            definition = null;

            if (tokenSpan.Snapshot == null || string.IsNullOrEmpty(lookupText)) {
                return false;
            }

            ITextSnapshot snapshot = tokenSpan.Snapshot;
            string thisFile = VerilogGlobals.GetDocumentPath(snapshot);
            if (string.IsNullOrEmpty(thisFile)) {
                return false;
            }

            VerilogGlobals.ParseDataSnapshot parseData;
            if (!VerilogGlobals.TryGetParseData(thisFile, snapshot.Version.VersionNumber, false, out parseData)) {
                VerilogGlobals.ParseStatusController.NeedReparse_SetValue(thisFile, true);
                VerilogGlobals.Reparse(snapshot.TextBuffer, thisFile);
                if (!VerilogGlobals.TryGetParseData(thisFile, snapshot.Version.VersionNumber, false, out parseData)) {
                    return false;
                }
            }

            if (parseData == null || parseData.VerilogDefinitionLocations == null) {
                return false;
            }

            ITextSnapshotLine lineInfo = snapshot.GetLineFromPosition(tokenSpan.Start.Position);
            int lineNumber = lineInfo.LineNumber;
            int linePosition = tokenSpan.Start.Position - lineInfo.Start.Position;

            string macroName = lookupText.StartsWith("`", StringComparison.Ordinal)
                ? lookupText.Substring(1)
                : string.Empty;

            if (!string.IsNullOrEmpty(macroName) && TryGetDefinitionFromScope(parseData, VerilogGlobals.SCOPE_MACRO, macroName, out definition)) {
                System.Diagnostics.Debug.WriteLine("VLE definition lookup: TryGetDefinitionFromScope initial hit " + lookupText);
                return true;
            }

            string activeLocalScope;
            if (TryFindActiveLocalScope(snapshot, lineNumber, parseData, out activeLocalScope) &&
                TryGetDefinitionFromScope(parseData, activeLocalScope, lookupText, out definition)) {
                System.Diagnostics.Debug.WriteLine("VLE definition lookup: TryGetDefinitionFromScope activeLocalScope hit " + lookupText);
                return true;
            }

            string moduleScope = parseData.TextModuleName(lineNumber, linePosition);
            if (TryGetDefinitionFromScope(parseData, moduleScope, lookupText, out definition)) {
                System.Diagnostics.Debug.WriteLine("VLE definition lookup: TryGetDefinitionFromScope moduleScope hit " + lookupText);
                return true;
            }

            if (TryGetDefinitionFromScope(parseData, VerilogGlobals.SCOPE_CONST, lookupText, out definition)) {
                System.Diagnostics.Debug.WriteLine("VLE definition lookup: TryGetDefinitionFromScope hit " + lookupText);
                return true;
            }

            if (parseData.VerilogVariables != null && parseData.VerilogVariables.ContainsKey(lookupText) &&
                TryGetDefinitionFromScope(parseData, lookupText, lookupText, out definition)) {
                System.Diagnostics.Debug.WriteLine("VLE definition lookup: TryGetDefinitionFromScope ContainsKey hit " + lookupText);
                return true;
            }

            if (TryGetDefinitionFromScope(parseData, VerilogGlobals.SCOPE_MACRO, lookupText, out definition)) {
                System.Diagnostics.Debug.WriteLine("VLE definition lookup: TryGetDefinitionFromScope hit " + lookupText);
                return true;
            }

            if (VerilogGlobals.TryGetModuleDefinitionFromParsedFiles(lookupText, out definition)) {
                System.Diagnostics.Debug.WriteLine("VLE definition lookup: TryGetModuleDefinitionFromParsedFiles hit " + lookupText);
                return true;
            }

            if (VerilogGlobals.TryGetDefinitionFromParsedFiles(lookupText, thisFile, out definition)) {
                System.Diagnostics.Debug.WriteLine("VLE definition lookup: TryGetDefinitionFromParsedFiles parsed-files hit " + lookupText);
                return true;
            }

            if (TryFindDefinitionByTextSearch(lookupText, thisFile, out definition)) {
                System.Diagnostics.Debug.WriteLine("VLE definition lookup: TryFindDefinitionByTextSearch text-search fallback hit " + lookupText);
                return true;
            }

            return false;
        }

        private static bool TryFindDefinitionByTextSearch(
            string lookupText,
            string currentFile,
            out VerilogGlobals.VerilogDefinitionLocation definition) {

            definition = null;

            if (string.IsNullOrEmpty(lookupText) || string.IsNullOrEmpty(currentFile)) {
                return false;
            }

            string lookupName = lookupText.StartsWith("`", StringComparison.Ordinal)
                ? lookupText.Substring(1)
                : lookupText;

            if (string.IsNullOrEmpty(lookupName)) {
                return false;
            }

            foreach (string filePath in CandidateDefinitionFiles(currentFile)) {
                if (TryFindDefinitionInFile(filePath, lookupName, out definition)) {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindDefinitionInFile(
            string filePath,
            string lookupName,
            out VerilogGlobals.VerilogDefinitionLocation definition) {

            definition = null;

            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(lookupName) || !File.Exists(filePath)) {
                return false;
            }

            string[] lines;
            try {
                FileInfo fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > 1024 * 1024) {
                    return false;
                }

                lines = File.ReadAllLines(filePath);
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

            for (int i = 0; i < lines.Length; i++) {
                string lineText = StripLineComment(lines[i]);
                int nameIndex = IndexOfIdentifier(lineText, lookupName);
                if (nameIndex < 0 || !LooksLikeDefinitionLine(lineText, lookupName, nameIndex)) {
                    continue;
                }

                definition = new VerilogGlobals.VerilogDefinitionLocation(
                    filePath,
                    string.Empty,
                    lookupName,
                    i,
                    nameIndex,
                    lookupName.Length,
                    VerilogLanguage.VerilogToken.VerilogTokenTypes.Verilog_Variable,
                    lineText.Trim());
                return true;
            }

            return false;
        }

        private static IEnumerable<string> CandidateDefinitionFiles(string currentFile) {
            HashSet<string> visitedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(currentFile)) {
                visitedFiles.Add(currentFile);
                yield return currentFile;
            }

            string rootDirectory = FindSearchRoot(currentFile);
            if (string.IsNullOrEmpty(rootDirectory) || !Directory.Exists(rootDirectory)) {
                yield break;
            }

            int returnedFiles = 0;
            Stack<string> pendingDirectories = new Stack<string>();
            pendingDirectories.Push(rootDirectory);

            while (pendingDirectories.Count > 0 && returnedFiles < 600) {
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

                    if (returnedFiles >= 600) {
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

        private static bool LooksLikeDefinitionLine(string lineText, string lookupName, int nameIndex) {
            if (string.IsNullOrEmpty(lineText) || string.IsNullOrEmpty(lookupName) || nameIndex < 0) {
                return false;
            }

            string textBeforeName = lineText.Substring(0, nameIndex);
            string trimmedBeforeName = textBeforeName.TrimStart();

            if (trimmedBeforeName.StartsWith("`define", StringComparison.Ordinal)) {
                return true;
            }

            string[] declarationKeywords = new string[] {
                "localparam",
                "parameter",
                "input",
                "output",
                "inout",
                "wire",
                "reg",
                "logic",
                "bit",
                "genvar"
            };

            foreach (string declarationKeyword in declarationKeywords) {
                if (ContainsWord(textBeforeName, declarationKeyword)) {
                    return true;
                }
            }

            return false;
        }

        private static string StripLineComment(string lineText) {
            if (lineText == null) {
                return string.Empty;
            }

            int commentIndex = lineText.IndexOf("//", StringComparison.Ordinal);
            return commentIndex >= 0
                ? lineText.Substring(0, commentIndex)
                : lineText;
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

        private static bool ContainsWord(string text, string word) {
            return IndexOfIdentifier(text, word) >= 0;
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

        private static bool IsIdentifierLookupText(string text) {
            if (string.IsNullOrEmpty(text)) {
                return false;
            }

            string identifier = text;
            if (identifier.StartsWith("`", StringComparison.Ordinal)) {
                identifier = identifier.Substring(1);
            }

            if (identifier.Length == 0) {
                return false;
            }

            if (identifier[0] == '\\') {
                return identifier.Length > 1;
            }

            if (!(char.IsLetter(identifier[0]) || identifier[0] == '_')) {
                return false;
            }

            for (int i = 1; i < identifier.Length; i++) {
                if (!IsIdentifierCharacter(identifier[i])) {
                    return false;
                }
            }

            return true;
        }

        private static bool IsIdentifierCharacter(char c) {
            return char.IsLetterOrDigit(c) || c == '_' || c == '$' || c == '\\';
        }

        private static bool TryGetDefinitionFromScope(
            VerilogGlobals.ParseDataSnapshot parseData,
            string scope,
            string lookupText,
            out VerilogGlobals.VerilogDefinitionLocation definition) {

            definition = null;

            if (parseData == null || parseData.VerilogDefinitionLocations == null ||
                string.IsNullOrEmpty(scope) || string.IsNullOrEmpty(lookupText)) {
                return false;
            }

            Dictionary<string, VerilogGlobals.VerilogDefinitionLocation> scopeMap;
            if (!parseData.VerilogDefinitionLocations.TryGetValue(scope, out scopeMap) || scopeMap == null) {
                return false;
            }

            return scopeMap.TryGetValue(lookupText, out definition) && definition != null;
        }

        private static bool TryFindActiveLocalScope(
            ITextSnapshot snapshot,
            int lineNumber,
            VerilogGlobals.ParseDataSnapshot parseData,
            out string activeLocalScope) {

            activeLocalScope = string.Empty;

            if (snapshot == null || parseData == null || lineNumber < 0) {
                return false;
            }

            int lastLine = Math.Min(lineNumber, snapshot.LineCount - 1);
            for (int i = lastLine; i >= 0; i--) {
                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(i);
                string lineText = line.GetText();

                if (VerilogGlobals.IsEndFunctionLineText(lineText) || VerilogGlobals.IsEndTaskLineText(lineText)) {
                    return false;
                }

                string functionName;
                if (VerilogGlobals.TryGetFunctionNameFromLineText(lineText, out functionName)) {
                    string moduleScope = parseData.TextModuleName(line.LineNumber, 0);
                    activeLocalScope = VerilogGlobals.FunctionLocalScopeName(moduleScope, functionName);
                    return true;
                }

                string taskName;
                if (VerilogGlobals.TryGetTaskNameFromLineText(lineText, out taskName)) {
                    string moduleScope = parseData.TextModuleName(line.LineNumber, 0);
                    activeLocalScope = VerilogGlobals.TaskLocalScopeName(moduleScope, taskName);
                    return true;
                }
            }

            return false;
        }
    }
}
