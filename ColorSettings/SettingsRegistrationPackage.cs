// file: ColorSettings/SettingsRegistrationPackage.cs
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

using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace VerilogLanguage.ColorSettings
{
    /// <summary>
    /// Registration-only package for the VLE color settings profile.
    /// </summary>
    /// <remarks>
    /// The package does not autoload. Its registration attributes add the
    /// VLE-specific category to Visual Studio's Import and Export Settings wizard.
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideProfile(
        typeof(VleColorSettingsProfile),
        "Verilog Language Extension",
        "Colorization", /* "Verilog Colorization" for the export name is found in VSPackaga.resx (data name="5101"), not here! */
        VleColorSettingsResourceIds.CategoryName,
        VleColorSettingsResourceIds.ObjectName,
        false,
        DescriptionResourceID = VleColorSettingsResourceIds.Description)]
    [Guid(PackageGuidString)]
    public sealed class VleColorSettingsRegistrationPackage : AsyncPackage
    {
        public const string PackageGuidString = "CB86B343-5900-4CB9-A7EB-E6A1A43147DA";
    }

    /// <summary>
    /// Numeric resource identifiers stored in VSPackage.resx.
    /// </summary>
    internal static class VleColorSettingsResourceIds
    {
        internal const short CategoryName = 5100;
        internal const short ObjectName = 5101;
        internal const short Description = 5102;
    }
}
