// file: VerilogToken/VerilogPreprocessorEvaluator.cs
//***************************************************************************
//
//  MIT License
//
//  Copyright (c) 2019-2026 gojimmypi
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

namespace VerilogLanguage.VerilogToken
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Evaluates Verilog/SystemVerilog compiler directives for syntax-highlighting
    /// purposes. Included source files are processed in textual source order and
    /// share the same macro and conditional state as the including document.
    /// </summary>
    internal static class VerilogPreprocessorEvaluator
    {
        private const int MaximumIncludeDepth = 64;

        internal sealed class LineState
        {
            internal LineState(bool isActive, bool isDirective) {
                IsActive = isActive;
                IsDirective = isDirective;
            }

            internal bool IsActive { get; private set; }
            internal bool IsDirective { get; private set; }
        }

        internal sealed class IncludeDependency
        {
            internal IncludeDependency(string filePath, bool exists, DateTime lastWriteTimeUtc, long length) {
                FilePath = filePath;
                Exists = exists;
                LastWriteTimeUtc = lastWriteTimeUtc;
                Length = length;
            }

            internal string FilePath { get; private set; }
            internal bool Exists { get; private set; }
            internal DateTime LastWriteTimeUtc { get; private set; }
            internal long Length { get; private set; }

            internal bool IsCurrent() {
                IncludeDependency current = Capture(FilePath);
                return current.Exists == Exists &&
                    current.LastWriteTimeUtc == LastWriteTimeUtc &&
                    current.Length == Length;
            }

            internal static IncludeDependency Capture(string filePath) {
                if (string.IsNullOrEmpty(filePath)) {
                    return new IncludeDependency(string.Empty, false, DateTime.MinValue, 0);
                }

                try {
                    FileInfo fileInfo = new FileInfo(filePath);
                    fileInfo.Refresh();
                    if (!fileInfo.Exists) {
                        return new IncludeDependency(filePath, false, DateTime.MinValue, 0);
                    }

                    return new IncludeDependency(
                        filePath,
                        true,
                        fileInfo.LastWriteTimeUtc,
                        fileInfo.Length);
                }
                catch (IOException) {
                    return new IncludeDependency(filePath, false, DateTime.MinValue, 0);
                }
                catch (UnauthorizedAccessException) {
                    return new IncludeDependency(filePath, false, DateTime.MinValue, 0);
                }
                catch (NotSupportedException) {
                    return new IncludeDependency(filePath, false, DateTime.MinValue, 0);
                }
                catch (System.Security.SecurityException) {
                    return new IncludeDependency(filePath, false, DateTime.MinValue, 0);
                }
            }
        }

        internal sealed class AnalysisResult
        {
            internal AnalysisResult(
                List<LineState> lineStates,
                Dictionary<string, string> macroValues,
                List<IncludeDependency> includeDependencies) {

                LineStates = lineStates;
                MacroValues = macroValues;
                IncludeDependencies = includeDependencies;
            }

            internal List<LineState> LineStates { get; private set; }
            internal Dictionary<string, string> MacroValues { get; private set; }
            internal List<IncludeDependency> IncludeDependencies { get; private set; }
        }

        private sealed class ConditionalFrame
        {
            internal bool ParentActive;
            internal bool BranchTaken;
            internal bool CurrentBranchActive;
        }

        internal static AnalysisResult Analyze(IList<string> lines) {
            return Analyze(lines, string.Empty);
        }

        internal static AnalysisResult Analyze(IList<string> lines, string sourceFilePath) {
            List<LineState> lineStates = new List<LineState>();
            Dictionary<string, string> macroValues = new Dictionary<string, string>(StringComparer.Ordinal);
            Stack<ConditionalFrame> conditionals = new Stack<ConditionalFrame>();
            Dictionary<string, IncludeDependency> includeDependencies =
                new Dictionary<string, IncludeDependency>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> activeIncludeStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool isBlockCommentOpen = false;
            string normalizedSourceFilePath = NormalizePath(sourceFilePath);

            if (!string.IsNullOrEmpty(normalizedSourceFilePath)) {
                activeIncludeStack.Add(normalizedSourceFilePath);
            }

            ProcessLines(
                lines,
                normalizedSourceFilePath,
                true,
                lineStates,
                macroValues,
                conditionals,
                includeDependencies,
                activeIncludeStack,
                ref isBlockCommentOpen,
                0);

            return new AnalysisResult(
                lineStates,
                macroValues,
                new List<IncludeDependency>(includeDependencies.Values));
        }

        internal static bool AreIncludeDependenciesCurrent(IList<IncludeDependency> dependencies) {
            if (dependencies == null) {
                return true;
            }

            for (int index = 0; index < dependencies.Count; index++) {
                IncludeDependency dependency = dependencies[index];
                if (dependency != null && !dependency.IsCurrent()) {
                    return false;
                }
            }

            return true;
        }

        private static void ProcessLines(
            IList<string> lines,
            string sourceFilePath,
            bool collectLineStates,
            List<LineState> lineStates,
            Dictionary<string, string> macroValues,
            Stack<ConditionalFrame> conditionals,
            Dictionary<string, IncludeDependency> includeDependencies,
            HashSet<string> activeIncludeStack,
            ref bool isBlockCommentOpen,
            int includeDepth) {

            if (lines == null) {
                return;
            }

            for (int lineNumber = 0; lineNumber < lines.Count; lineNumber++) {
                string lineText = lines[lineNumber] ?? string.Empty;
                bool lineIsActive = GetCurrentActiveState(conditionals);
                string directiveName;
                string directiveArgument;
                bool isDirective = TryParseDirective(
                    lineText,
                    isBlockCommentOpen,
                    out directiveName,
                    out directiveArgument);

                if (collectLineStates) {
                    lineStates.Add(new LineState(lineIsActive, isDirective));
                }

                if (isDirective) {
                    ProcessDirective(
                        directiveName,
                        directiveArgument,
                        lineIsActive,
                        sourceFilePath,
                        macroValues,
                        conditionals,
                        includeDependencies,
                        activeIncludeStack,
                        ref isBlockCommentOpen,
                        includeDepth);
                }

                UpdateBlockCommentState(lineText, ref isBlockCommentOpen);
            }
        }

        private static bool GetCurrentActiveState(Stack<ConditionalFrame> conditionals) {
            if (conditionals == null || conditionals.Count == 0) {
                return true;
            }

            return conditionals.Peek().CurrentBranchActive;
        }

        private static void ProcessDirective(
            string directiveName,
            string directiveArgument,
            bool lineIsActive,
            string sourceFilePath,
            Dictionary<string, string> macroValues,
            Stack<ConditionalFrame> conditionals,
            Dictionary<string, IncludeDependency> includeDependencies,
            HashSet<string> activeIncludeStack,
            ref bool isBlockCommentOpen,
            int includeDepth) {

            string macroName;
            string macroValue;

            switch (directiveName) {
                case "define":
                    if (lineIsActive && TryParseMacroDefinition(directiveArgument, out macroName, out macroValue)) {
                        macroValues[macroName] = macroValue;
                    }
                    break;

                case "undef":
                    if (lineIsActive && TryParseMacroName(directiveArgument, out macroName)) {
                        macroValues.Remove(macroName);
                    }
                    break;

                case "undefineall":
                    if (lineIsActive) {
                        macroValues.Clear();
                    }
                    break;

                case "include":
                    if (lineIsActive) {
                        ProcessInclude(
                            directiveArgument,
                            sourceFilePath,
                            macroValues,
                            conditionals,
                            includeDependencies,
                            activeIncludeStack,
                            ref isBlockCommentOpen,
                            includeDepth);
                    }
                    break;

                case "ifdef":
                case "ifndef":
                    bool isDefined = TryParseMacroName(directiveArgument, out macroName) &&
                        macroValues.ContainsKey(macroName);
                    bool conditionIsTrue = directiveName == "ifdef" ? isDefined : !isDefined;
                    ConditionalFrame frame = new ConditionalFrame {
                        ParentActive = lineIsActive,
                        CurrentBranchActive = lineIsActive && conditionIsTrue,
                        BranchTaken = lineIsActive && conditionIsTrue,
                    };
                    conditionals.Push(frame);
                    break;

                case "elsif":
                    if (conditionals.Count > 0) {
                        ConditionalFrame elsifFrame = conditionals.Peek();
                        bool elsifDefined = TryParseMacroName(directiveArgument, out macroName) &&
                            macroValues.ContainsKey(macroName);
                        bool activateElsif = elsifFrame.ParentActive && !elsifFrame.BranchTaken && elsifDefined;
                        elsifFrame.CurrentBranchActive = activateElsif;
                        if (activateElsif) {
                            elsifFrame.BranchTaken = true;
                        }
                    }
                    break;

                case "else":
                    if (conditionals.Count > 0) {
                        ConditionalFrame elseFrame = conditionals.Peek();
                        bool activateElse = elseFrame.ParentActive && !elseFrame.BranchTaken;
                        elseFrame.CurrentBranchActive = activateElse;
                        if (activateElse) {
                            elseFrame.BranchTaken = true;
                        }
                    }
                    break;

                case "endif":
                    if (conditionals.Count > 0) {
                        conditionals.Pop();
                    }
                    break;
            }
        }

        private static void ProcessInclude(
            string directiveArgument,
            string sourceFilePath,
            Dictionary<string, string> macroValues,
            Stack<ConditionalFrame> conditionals,
            Dictionary<string, IncludeDependency> includeDependencies,
            HashSet<string> activeIncludeStack,
            ref bool isBlockCommentOpen,
            int includeDepth) {

            if (includeDepth >= MaximumIncludeDepth || string.IsNullOrEmpty(sourceFilePath)) {
                return;
            }

            string includeName;
            if (!TryParseIncludeName(directiveArgument, macroValues, out includeName)) {
                return;
            }

            string includePath = ResolveIncludePath(sourceFilePath, includeName);
            if (string.IsNullOrEmpty(includePath)) {
                return;
            }

            IncludeDependency dependency = IncludeDependency.Capture(includePath);
            includeDependencies[includePath] = dependency;
            if (!dependency.Exists || activeIncludeStack.Contains(includePath)) {
                return;
            }

            string[] includeLines;
            try {
                includeLines = File.ReadAllLines(includePath);
            }
            catch (IOException) {
                return;
            }
            catch (UnauthorizedAccessException) {
                return;
            }
            catch (NotSupportedException) {
                return;
            }
            catch (System.Security.SecurityException) {
                return;
            }

            activeIncludeStack.Add(includePath);
            try {
                ProcessLines(
                    includeLines,
                    includePath,
                    false,
                    null,
                    macroValues,
                    conditionals,
                    includeDependencies,
                    activeIncludeStack,
                    ref isBlockCommentOpen,
                    includeDepth + 1);
            }
            finally {
                activeIncludeStack.Remove(includePath);
            }
        }

        private static bool TryParseIncludeName(
            string directiveArgument,
            Dictionary<string, string> macroValues,
            out string includeName) {

            return TryParseIncludeName(directiveArgument, macroValues, out includeName, 0);
        }

        private static bool TryParseIncludeName(
            string directiveArgument,
            Dictionary<string, string> macroValues,
            out string includeName,
            int macroExpansionDepth) {

            includeName = string.Empty;
            if (string.IsNullOrWhiteSpace(directiveArgument)) {
                return false;
            }

            string argument = directiveArgument.Trim();
            if (argument.Length == 0) {
                return false;
            }

            if (argument[0] == '`' && macroExpansionDepth < 8) {
                string includeMacroName;
                if (!TryParseMacroName(argument.Substring(1), out includeMacroName)) {
                    return false;
                }

                string includeMacroValue;
                if (!macroValues.TryGetValue(includeMacroName, out includeMacroValue)) {
                    return false;
                }

                return TryParseIncludeName(
                    includeMacroValue,
                    macroValues,
                    out includeName,
                    macroExpansionDepth + 1);
            }

            char opening = argument[0];
            char closing;
            if (opening == '"') {
                closing = '"';
            }
            else if (opening == '<') {
                closing = '>';
            }
            else {
                return false;
            }

            int closingIndex = FindClosingIncludeDelimiter(argument, closing);
            if (closingIndex <= 1) {
                return false;
            }

            includeName = argument.Substring(1, closingIndex - 1);
            return !string.IsNullOrWhiteSpace(includeName);
        }

        private static int FindClosingIncludeDelimiter(string argument, char closing) {
            bool isEscaped = false;
            for (int index = 1; index < argument.Length; index++) {
                char current = argument[index];
                if (current == closing && !isEscaped) {
                    return index;
                }

                if (current == '\\' && !isEscaped) {
                    isEscaped = true;
                }
                else {
                    isEscaped = false;
                }
            }

            return -1;
        }

        private static string ResolveIncludePath(string sourceFilePath, string includeName) {
            if (string.IsNullOrEmpty(sourceFilePath) || string.IsNullOrWhiteSpace(includeName)) {
                return string.Empty;
            }

            try {
                string candidatePath = includeName;
                if (!Path.IsPathRooted(candidatePath)) {
                    string sourceDirectory = Path.GetDirectoryName(sourceFilePath);
                    if (string.IsNullOrEmpty(sourceDirectory)) {
                        return string.Empty;
                    }

                    candidatePath = Path.Combine(sourceDirectory, candidatePath);
                }

                return Path.GetFullPath(candidatePath);
            }
            catch (ArgumentException) {
                return string.Empty;
            }
            catch (NotSupportedException) {
                return string.Empty;
            }
            catch (System.Security.SecurityException) {
                return string.Empty;
            }
            catch (PathTooLongException) {
                return string.Empty;
            }
        }

        private static string NormalizePath(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath)) {
                return string.Empty;
            }

            try {
                return Path.GetFullPath(filePath);
            }
            catch (ArgumentException) {
                return string.Empty;
            }
            catch (NotSupportedException) {
                return string.Empty;
            }
            catch (System.Security.SecurityException) {
                return string.Empty;
            }
            catch (PathTooLongException) {
                return string.Empty;
            }
        }

        private static bool TryParseDirective(
            string lineText,
            bool isBlockCommentOpenAtLineStart,
            out string directiveName,
            out string directiveArgument) {

            directiveName = string.Empty;
            directiveArgument = string.Empty;

            int directiveStart;
            if (!TryFindFirstCodeCharacter(lineText, isBlockCommentOpenAtLineStart, out directiveStart) ||
                directiveStart < 0 ||
                directiveStart >= lineText.Length ||
                lineText[directiveStart] != '`') {

                return false;
            }

            int nameStart = directiveStart + 1;
            int nameEnd = nameStart;
            while (nameEnd < lineText.Length && IsIdentifierCharacter(lineText[nameEnd])) {
                nameEnd++;
            }

            if (nameEnd == nameStart) {
                return false;
            }

            directiveName = lineText.Substring(nameStart, nameEnd - nameStart);
            if (!IsKnownCompilerDirective(directiveName)) {
                directiveName = string.Empty;
                return false;
            }

            directiveArgument = lineText.Substring(nameEnd).Trim();
            return true;
        }

        private static bool TryFindFirstCodeCharacter(
            string lineText,
            bool isBlockCommentOpenAtLineStart,
            out int codeStart) {

            codeStart = -1;
            if (string.IsNullOrEmpty(lineText)) {
                return false;
            }

            bool isBlockCommentOpen = isBlockCommentOpenAtLineStart;
            int index = 0;
            while (index < lineText.Length) {
                if (isBlockCommentOpen) {
                    int blockEnd = lineText.IndexOf("*/", index, StringComparison.Ordinal);
                    if (blockEnd < 0) {
                        return false;
                    }

                    isBlockCommentOpen = false;
                    index = blockEnd + 2;
                    continue;
                }

                if (char.IsWhiteSpace(lineText[index])) {
                    index++;
                    continue;
                }

                if (index + 1 < lineText.Length && lineText[index] == '/' && lineText[index + 1] == '/') {
                    return false;
                }

                if (index + 1 < lineText.Length && lineText[index] == '/' && lineText[index + 1] == '*') {
                    isBlockCommentOpen = true;
                    index += 2;
                    continue;
                }

                codeStart = index;
                return true;
            }

            return false;
        }

        private static void UpdateBlockCommentState(string lineText, ref bool isBlockCommentOpen) {
            if (string.IsNullOrEmpty(lineText)) {
                return;
            }

            bool isStringOpen = false;
            bool isEscaped = false;

            for (int index = 0; index < lineText.Length; index++) {
                char current = lineText[index];
                char next = index + 1 < lineText.Length ? lineText[index + 1] : '\0';

                if (isBlockCommentOpen) {
                    if (current == '*' && next == '/') {
                        isBlockCommentOpen = false;
                        index++;
                    }
                    continue;
                }

                if (isStringOpen) {
                    if (current == '"' && !isEscaped) {
                        isStringOpen = false;
                    }

                    if (current == '\\' && !isEscaped) {
                        isEscaped = true;
                    }
                    else {
                        isEscaped = false;
                    }
                    continue;
                }

                if (current == '/' && next == '/') {
                    return;
                }

                if (current == '/' && next == '*') {
                    isBlockCommentOpen = true;
                    index++;
                    continue;
                }

                if (current == '"') {
                    isStringOpen = true;
                    isEscaped = false;
                }
            }
        }

        private static bool TryParseMacroDefinition(string directiveArgument, out string macroName, out string macroValue) {
            macroName = string.Empty;
            macroValue = string.Empty;

            if (string.IsNullOrWhiteSpace(directiveArgument)) {
                return false;
            }

            int index = 0;
            SkipWhitespace(directiveArgument, ref index);
            int macroNameEnd;
            if (!TryReadMacroIdentifier(directiveArgument, index, out macroName, out macroNameEnd)) {
                return false;
            }

            index = macroNameEnd;

            // A function-like macro has no whitespace between the name and '('.
            if (index < directiveArgument.Length && directiveArgument[index] == '(') {
                index = SkipBalancedParentheses(directiveArgument, index);
            }

            if (index < directiveArgument.Length) {
                macroValue = directiveArgument.Substring(index).Trim();
            }

            return true;
        }

        private static bool TryParseMacroName(string directiveArgument, out string macroName) {
            macroName = string.Empty;
            if (string.IsNullOrWhiteSpace(directiveArgument)) {
                return false;
            }

            int index = 0;
            SkipWhitespace(directiveArgument, ref index);
            int macroNameEnd;
            return TryReadMacroIdentifier(directiveArgument, index, out macroName, out macroNameEnd);
        }

        private static bool TryReadMacroIdentifier(
            string text,
            int start,
            out string macroName,
            out int end) {

            macroName = string.Empty;
            end = start;
            if (string.IsNullOrEmpty(text) || start < 0 || start >= text.Length) {
                return false;
            }

            if (text[start] == '\\') {
                int escapedEnd = start + 1;
                while (escapedEnd < text.Length && !char.IsWhiteSpace(text[escapedEnd])) {
                    escapedEnd++;
                }

                if (escapedEnd == start + 1) {
                    return false;
                }

                macroName = text.Substring(start, escapedEnd - start);
                end = escapedEnd;
                return true;
            }

            if (!IsIdentifierStartCharacter(text[start])) {
                return false;
            }

            int identifierEnd = start + 1;
            while (identifierEnd < text.Length && IsIdentifierCharacter(text[identifierEnd])) {
                identifierEnd++;
            }

            macroName = text.Substring(start, identifierEnd - start);
            end = identifierEnd;
            return true;
        }

        private static int SkipBalancedParentheses(string text, int openingParenthesis) {
            int depth = 0;
            bool isStringOpen = false;
            bool isEscaped = false;

            for (int index = openingParenthesis; index < text.Length; index++) {
                char current = text[index];

                if (isStringOpen) {
                    if (current == '"' && !isEscaped) {
                        isStringOpen = false;
                    }

                    if (current == '\\' && !isEscaped) {
                        isEscaped = true;
                    }
                    else {
                        isEscaped = false;
                    }
                    continue;
                }

                if (current == '"') {
                    isStringOpen = true;
                    isEscaped = false;
                    continue;
                }

                if (current == '(') {
                    depth++;
                    continue;
                }

                if (current == ')') {
                    depth--;
                    if (depth == 0) {
                        return index + 1;
                    }
                }
            }

            return text.Length;
        }

        private static void SkipWhitespace(string text, ref int index) {
            while (index < text.Length && char.IsWhiteSpace(text[index])) {
                index++;
            }
        }

        private static bool IsIdentifierStartCharacter(char value) {
            return char.IsLetter(value) || value == '_';
        }

        private static bool IsIdentifierCharacter(char value) {
            return char.IsLetterOrDigit(value) || value == '_' || value == '$';
        }

        private static bool IsKnownCompilerDirective(string directiveName) {
            switch (directiveName) {
                case "begin_keywords":
                case "celldefine":
                case "default_nettype":
                case "define":
                case "else":
                case "elsif":
                case "end_keywords":
                case "endcelldefine":
                case "endif":
                case "ifdef":
                case "ifndef":
                case "include":
                case "line":
                case "nounconnected_drive":
                case "pragma":
                case "resetall":
                case "timescale":
                case "unconnected_drive":
                case "undef":
                case "undefineall":
                    return true;

                default:
                    return false;
            }
        }
    }
}
