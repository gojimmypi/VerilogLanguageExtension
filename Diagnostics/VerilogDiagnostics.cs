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
