// file: Diagnostics/VerilogDiagnostics.cs
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

using System;
using System.Diagnostics;

namespace VerilogLanguage.Diagnostics
{
    internal class VerilogDiagnostics
    {
        // shared home for common helpers
    }

    internal static class BufferAttributesDebug
    {
        [Conditional("VLE_DEBUG_BUFFER_ATTRIBUTES")]
        public static void WriteLine(string message) {
            Debug.WriteLine(message);
        }
    }

    internal static class ExceptionDebug
    {
        [Conditional("VLE_DEBUG_EXCEPTIONS")]
        public static void WriteLine(string message) {
            Debug.WriteLine(message);
        }

        [Conditional("VLE_DEBUG_EXCEPTIONS")]
        public static void WriteLine(Exception ex) {
            Debug.WriteLine(ex);
        }

        [Conditional("VLE_DEBUG_EXCEPTIONS")]
        public static void WriteLine(string message, Exception ex) {
            Debug.WriteLine(message);
            Debug.WriteLine(ex);
        }

        [Conditional("VLE_DEBUG_EXCEPTIONS")]
        public static void WriteLine(string format, params object[] args) {
            Debug.WriteLine(string.Format(format, args));
        }
    }
}
