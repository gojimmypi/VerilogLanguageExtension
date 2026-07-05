// file: Globals/OutputWindow.cs
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

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerilogLanguage.Globals
{
    class OutputWindow
    {
        public static void Writeln(string s) {
            System.Diagnostics.Debug.WriteLine(s);
            //IVsOutputWindow outWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;

            //Guid generalPaneGuid = VSConstants.GUID_OutWindowGeneralPane; // P.S. There's also the GUID_OutWindowDebugPane available.
            //IVsOutputWindowPane generalPane;
            //outWindow.GetPane(ref generalPaneGuid, out generalPane);

            //generalPane.OutputString("Hello World!");
            //generalPane.Activate(); // Brings this pane into view
        }

        /// <summary>
        /// This function is used to write a string on the Output window of Visual Studio.
        /// </summary>
        /// <param name="provider">The service provider to query for SVsOutputWindow</param>
        /// <param name="text">The text to write</param>
        internal static void WriteOnOutputWindow(IServiceProvider provider, string text) {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            // from https://github.com/microsoft/VSSDK-Extensibility-Samples/blob/master/Reference_Services/C%23/Reference.Services/HelperFunctions.cs
            //
            // At first write the text on the debug output.
            Debug.WriteLine(text);

            // Check if we have a provider
            if (null == provider) {
                // If there is no provider we can not do anything; exit now.
                Debug.WriteLine("No service provider passed to WriteOnOutputWindow.");
                return;
            }

            // Now get the SVsOutputWindow service from the service provider.
            IVsOutputWindow outputWindow = provider.GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (null == outputWindow) {
                // If the provider doesn't expose the service there is nothing we can do.
                // Write a message on the debug output and exit.
                Debug.WriteLine("Can not get the SVsOutputWindow service.");
                return;
            }

            // We can not write on the Output window itself, but only on one of its panes.
            // Here we try to use the "General" pane.
            Guid guidGeneral = Microsoft.VisualStudio.VSConstants.GUID_OutWindowGeneralPane;
            IVsOutputWindowPane windowPane;
            if (Microsoft.VisualStudio.ErrorHandler.Failed(outputWindow.GetPane(ref guidGeneral, out windowPane)) ||
                (null == windowPane)) {
                if (Microsoft.VisualStudio.ErrorHandler.Failed(outputWindow.CreatePane(ref guidGeneral, "General", 1, 0))) {
                    // Nothing to do here, just debug output and exit
                    Debug.WriteLine("Failed to create the Output window pane.");
                    return;
                }
                if (Microsoft.VisualStudio.ErrorHandler.Failed(outputWindow.GetPane(ref guidGeneral, out windowPane)) ||
                (null == windowPane)) {
                    // Again, there is nothing we can do to recover from this error, so write on the
                    // debug output and exit.
                    Debug.WriteLine("Failed to get the Output window pane.");
                    return;
                }
                if (Microsoft.VisualStudio.ErrorHandler.Failed(windowPane.Activate())) {
                    Debug.WriteLine("Failed to activate the Output window pane.");
                    return;
                }
            }

            // Finally we can write on the window pane.
            if (Microsoft.VisualStudio.ErrorHandler.Failed(windowPane.OutputString(text))) {
                Debug.WriteLine("Failed to write on the Output window pane.");
            }
        }
    }
}
