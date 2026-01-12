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


using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using VerilogLanguage.Testing;

namespace VerilogLanguage
{
    internal static class SnapshotExporterUiContext
    {
        // New GUID: generates a stable custom UI context that we can AutoLoad against.
        public const string GuidString = "B13F0C52-3E9B-4E14-9C0C-8D3D8B7F2A4B";
    }

    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("VerilogLanguage", "Verilog syntax highlighting", "0.3.6.3")]
    [ProvideMenuResource("Menus.ctmenu", 1)]

    // This rule is effectively "always on" once the shell is up because either SolutionExists or NoSolution is true.
    [ProvideUIContextRule(
        SnapshotExporterUiContext.GuidString,
        "VerilogLanguage SnapshotExporter UIContext",
        "SolutionExists | NoSolution",
        new[] { "SolutionExists", "NoSolution" },
        new[] { VSConstants.UICONTEXT.SolutionExists_string, VSConstants.UICONTEXT.NoSolution_string })]

    // Load when our always-true UI context is active.
    [ProvideAutoLoad(SnapshotExporterUiContext.GuidString, PackageAutoLoadFlags.BackgroundLoad)]

    [Guid(GuidList.GuidVerilogLanguagePackageString)]
    public sealed class VerilogLanguagePackage : AsyncPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress) {
            await base.InitializeAsync(cancellationToken, progress);

            // Breadcrumb: confirm package load in ActivityLog.xml (single entry).
            try {
                object logObj = await GetServiceAsync(typeof(SVsActivityLog)).ConfigureAwait(false);
                var log = logObj as IVsActivityLog;
                if (log != null) {
                    log.LogEntry(
                        (uint)__ACTIVITYLOG_ENTRYTYPE.ALE_INFORMATION,
                        "VerilogLanguage",
                        "VerilogLanguagePackage.InitializeAsync loaded (SnapshotExporter commands registering).");
                }
            }
            catch {
                // ignore logging failures
            }

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await ExportSnapshotCommand.InitializeAsync(this);
        }
    }
}
