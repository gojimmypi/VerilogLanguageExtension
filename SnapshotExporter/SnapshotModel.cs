// file: SnapshotExporter/SnapshotModel.cs
//***************************************************************************
//
//  MIT License
//
//  Copyright(c) 2025 gojimmypi
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

using System.Collections.Generic;

namespace VerilogLanguage.Testing
{
    internal sealed class EditorSnapshotExport
    {
        public int SchemaVersion { get; set; }
        public string RunName { get; set; }
        public string ExtensionVersion { get; set; }
        public string GitCommit { get; set; }
        public string FilePath { get; set; }
        public string FileRelativePath { get; set; }
        public string ContentType { get; set; }
        public int SnapshotLength { get; set; }
        public int SnapshotVersion { get; set; }
        public string TextSha256 { get; set; }

        public List<string> Errors { get; set; }
        public List<ClassificationRun> Classifications { get; set; }
        public List<TagRun> Tags { get; set; }
        public List<TokenRun> Tokens { get; set; }
        public List<SymbolRun> Symbols { get; set; }

        public EditorSnapshotExport() {
            SchemaVersion = 3;
            Errors = new List<string>();
            Classifications = new List<ClassificationRun>();
            Tags = new List<TagRun>();
            Tokens = new List<TokenRun>();
            Symbols = new List<SymbolRun>();
        }
    }

    internal sealed class ClassificationRun
    {
        public int Start { get; set; }
        public int Length { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string Text { get; set; }
        public List<string> Types { get; set; }

        public ClassificationRun() {
            Types = new List<string>();
        }
    }

    internal sealed class TagRun
    {
        public int Start { get; set; }
        public int Length { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string Text { get; set; }
        public string TagType { get; set; }
        public string TagDetail { get; set; }
        public string HoverText { get; set; }
    }

    internal sealed class TokenRun
    {
        public int Start { get; set; }
        public int Length { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string Text { get; set; }
        public string Context { get; set; }
    }

    internal sealed class SymbolRun
    {
        public string Scope { get; set; }
        public string Name { get; set; }
        public string TokenType { get; set; }
        public string HoverText { get; set; }
    }
}
