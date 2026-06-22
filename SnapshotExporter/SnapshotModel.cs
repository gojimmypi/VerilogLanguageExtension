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
        public string FilePath { get; set; }
        public int SnapshotLength { get; set; }
        public int SnapshotVersion { get; set; }
        public string TextSha256 { get; set; }

        public List<string> Errors { get; set; }
        public List<ClassificationRun> Classifications { get; set; }
        public List<TagRun> Tags { get; set; }

        public EditorSnapshotExport() {
            Errors = new List<string>();
            Classifications = new List<ClassificationRun>();
            Tags = new List<TagRun>();
        }
    }

    internal sealed class ClassificationRun
    {
        public int Start { get; set; }
        public int Length { get; set; }
        public List<string> Types { get; set; }

        public ClassificationRun() {
            Types = new List<string>();
        }
    }

    internal sealed class TagRun
    {
        public int Start { get; set; }
        public int Length { get; set; }
        public string TagType { get; set; }
        public string TagDetail { get; set; }
    }
}
