// file: ColorSettings/SettingsProfile.cs
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace VerilogLanguage.ColorSettings
{
    /// <summary>
    /// Exports and imports user-visible VLE editor color formats through Visual
    /// Studio's standard Import and Export Settings wizard.
    /// </summary>
    /// <remarks>
    /// The profile discovers VLE EditorFormatDefinition exports dynamically, so
    /// future user-visible classifications are included without maintaining a
    /// second hand-written name list.
    ///
    /// Raw Visual Studio COLORREF and font-flag values are preserved. This retains
    /// Automatic and other encoded colors instead of reducing them to RGB.
    /// </remarks>
    [ComVisible(true)]
    [Guid(ProfileGuidString)]
    public sealed class VleColorSettingsProfile : Component, IProfileManager
    {
        public const string ProfileGuidString = "C62DADBD-6016-4EBA-848F-F0484E9948E6"; /* a unique guid */

        private const int SettingsMajorVersion = 1;
        private const int MaximumImportedFormatCount = 4096;

        /* Visual Studio's Text Editor / MEF Fonts and Colors category.
         *  Must be 75A05685-00A8-4DED-BAE5-E7A50BFA929A */
        private static readonly Guid MefItemsFontAndColorCategory =
            new Guid("75A05685-00A8-4DED-BAE5-E7A50BFA929A");

        private static readonly Lazy<IReadOnlyList<VleColorFormatDescriptor>> FormatCatalog =
            new Lazy<IReadOnlyList<VleColorFormatDescriptor>>(
                DiscoverVleColorFormats,
                true);

        private List<PersistedColorFormat> _storedFormats =
            new List<PersistedColorFormat>();
        private List<string> _unavailableFormatNames = new List<string>();
        private List<PersistedColorFormat> _importedFormats =
            new List<PersistedColorFormat>();

        /// <summary>
        /// Loads the current VLE values before Visual Studio exports the category.
        /// </summary>
        public void LoadSettingsFromStorage() {
            ThreadHelper.ThrowIfNotOnUIThread();

            CatalogReadResult result = ReadAllFormatsFromStorage();
            _storedFormats = result.Formats;
            _unavailableFormatNames = result.UnavailableNames;
        }

        /// <summary>
        /// Writes all loaded VLE display items to the .vssettings category.
        /// </summary>
        public void SaveSettingsToXml(IVsSettingsWriter writer) {
            if (writer == null) {
                throw new ArgumentNullException("writer");
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            // Do not depend on Visual Studio preserving the same IProfileManager
            // instance between LoadSettingsFromStorage and SaveSettingsToXml.
            // Read the live values again so an export can never silently contain
            // FormatCount=0 because a prior load failed or used another instance.
            CatalogReadResult exportResult = ReadAllFormatsFromStorage();
            _storedFormats = exportResult.Formats;
            _unavailableFormatNames = exportResult.UnavailableNames;

            ThrowOnFailure(
                writer.WriteCategoryVersion(SettingsMajorVersion, 0, 0, 0));
            ThrowOnFailure(
                writer.WriteSettingLong("FormatCount", _storedFormats.Count));
            ThrowOnFailure(
                writer.WriteSettingLong(
                    "UnavailableFormatCount",
                    _unavailableFormatNames.Count));

            for (int index = 0; index < _storedFormats.Count; index++) {
                PersistedColorFormat format = _storedFormats[index];
                string prefix = MakeSettingPrefix(index);

                ThrowOnFailure(
                    writer.WriteSettingString(prefix + "Name", format.Name));
                ThrowOnFailure(
                    writer.WriteSettingString(prefix + "StorageName", format.StorageName));
                ThrowOnFailure(
                    writer.WriteSettingString(prefix + "SourceType", format.SourceType));
                ThrowOnFailure(
                    writer.WriteSettingLong(prefix + "ForegroundValid", format.ForegroundValid));
                ThrowOnFailure(
                    writer.WriteSettingLong(
                        prefix + "Foreground",
                        unchecked((int)format.Foreground)));
                ThrowOnFailure(
                    writer.WriteSettingLong(prefix + "BackgroundValid", format.BackgroundValid));
                ThrowOnFailure(
                    writer.WriteSettingLong(
                        prefix + "Background",
                        unchecked((int)format.Background)));
                ThrowOnFailure(
                    writer.WriteSettingLong(prefix + "FontFlagsValid", format.FontFlagsValid));
                ThrowOnFailure(
                    writer.WriteSettingLong(
                        prefix + "FontFlags",
                        unchecked((int)format.FontFlags)));
            }

            for (int index = 0; index < _unavailableFormatNames.Count; index++) {
                ThrowOnFailure(
                    writer.WriteSettingString(
                        MakeUnavailableSettingName(index),
                        _unavailableFormatNames[index]));
            }
        }

        /// <summary>
        /// Reads VLE values from a .vssettings file. Unknown names are ignored so
        /// a modified file cannot alter unrelated Visual Studio display items.
        /// </summary>
        public void LoadSettingsFromXml(IVsSettingsReader reader) {
            if (reader == null) {
                throw new ArgumentNullException("reader");
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            int count;
            if (!TryReadLong(reader, "FormatCount", out count)) {
                _importedFormats = new List<PersistedColorFormat>();
                return;
            }

            if (count < 0 || count > MaximumImportedFormatCount) {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Invalid VLE color format count in settings file: {0}.",
                        count));
            }

            var allowedNames = new HashSet<string>(
                FormatCatalog.Value.Select(format => format.Name),
                StringComparer.Ordinal);
            var importedByName = new Dictionary<string, PersistedColorFormat>(
                StringComparer.Ordinal);

            for (int index = 0; index < count; index++) {
                string prefix = MakeSettingPrefix(index);
                string name;

                if (!TryReadString(reader, prefix + "Name", out name) ||
                    string.IsNullOrWhiteSpace(name) ||
                    !allowedNames.Contains(name)) {
                    continue;
                }

                string storageName;
                if (!TryReadString(reader, prefix + "StorageName", out storageName)) {
                    storageName = string.Empty;
                }

                string sourceType;
                if (!TryReadString(reader, prefix + "SourceType", out sourceType)) {
                    sourceType = string.Empty;
                }

                int foregroundValid = 0;
                int foreground = 0;
                int backgroundValid = 0;
                int background = 0;
                int fontFlagsValid = 0;
                int fontFlags = 0;

                bool complete =
                    TryReadLong(reader, prefix + "ForegroundValid", out foregroundValid) &&
                    TryReadLong(reader, prefix + "Foreground", out foreground) &&
                    TryReadLong(reader, prefix + "BackgroundValid", out backgroundValid) &&
                    TryReadLong(reader, prefix + "Background", out background) &&
                    TryReadLong(reader, prefix + "FontFlagsValid", out fontFlagsValid) &&
                    TryReadLong(reader, prefix + "FontFlags", out fontFlags);

                if (!complete) {
                    continue;
                }

                importedByName[name] = new PersistedColorFormat(
                    name,
                    storageName,
                    sourceType,
                    NormalizeBoolean(foregroundValid),
                    unchecked((uint)foreground),
                    NormalizeBoolean(backgroundValid),
                    unchecked((uint)background),
                    NormalizeBoolean(fontFlagsValid),
                    unchecked((uint)fontFlags));
            }

            _importedFormats = importedByName.Values
                .OrderBy(format => format.Name, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Applies imported VLE values to Visual Studio's persistent store.
        /// </summary>
        public void SaveSettingsToStorage() {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_importedFormats.Count == 0) {
                return;
            }

            WriteFormatsToStorage(_importedFormats);
        }

        /// <summary>
        /// Reverts only VLE entries when Visual Studio requests a profile reset.
        /// </summary>
        public void ResetSettings() {
            ThreadHelper.ThrowIfNotOnUIThread();
            RevertAllFormatsToDefault();
        }

        private static CatalogReadResult ReadAllFormatsFromStorage() {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Do not refresh the shared MEF Fonts and Colors cache before an
            // export. RefreshCache reloads every installed format provider and
            // can fail because of an unrelated extension's missing resource.
            // IVsFontAndColorStorage reads the persisted values directly, and
            // FCSF_LOADDEFAULTS supplies each format's default when no override
            // has been stored.
            IVsFontAndColorStorage storage = GetRequiredGlobalService<
                SVsFontAndColorStorage,
                IVsFontAndColorStorage>();

            Guid category = MefItemsFontAndColorCategory;
            uint flags = (uint)(
                __FCSTORAGEFLAGS.FCSF_READONLY |
                __FCSTORAGEFLAGS.FCSF_LOADDEFAULTS);

            ThrowOnFailure(storage.OpenCategory(ref category, flags));

            var result = new List<PersistedColorFormat>();
            var unavailableNames = new List<string>();
            var missingRequiredNames = new List<string>();

            try {
                foreach (VleColorFormatDescriptor descriptor in FormatCatalog.Value) {
                    ColorableItemInfo info;
                    string storageName;

                    if (!TryGetStorageItem(
                        storage,
                        descriptor,
                        out storageName,
                        out info)) {
                        unavailableNames.Add(descriptor.Name);
                        if (descriptor.IsRequiredClassification) {
                            missingRequiredNames.Add(descriptor.Name);
                        }
                        continue;
                    }

                    result.Add(PersistedColorFormat.FromColorableItemInfo(
                        descriptor,
                        storageName,
                        info));
                }
            }
            finally {
                ThrowOnFailure(storage.CloseCategory());
            }

            if (missingRequiredNames.Count > 0) {
                throw new InvalidOperationException(
                    "Visual Studio did not expose the following user-visible VLE " +
                    "classification items in Text Editor Fonts and Colors: " +
                    string.Join(", ", missingRequiredNames));
            }

            if (result.Count == 0) {
                throw new InvalidOperationException(
                    "Visual Studio did not expose any VLE Fonts and Colors items.");
            }

            return new CatalogReadResult(
                result.OrderBy(format => format.Name, StringComparer.Ordinal).ToList(),
                unavailableNames.OrderBy(name => name, StringComparer.Ordinal).ToList());
        }

        private static void WriteFormatsToStorage(
            IEnumerable<PersistedColorFormat> formats) {

            ThreadHelper.ThrowIfNotOnUIThread();

            IVsFontAndColorStorage storage = GetRequiredGlobalService<
                SVsFontAndColorStorage,
                IVsFontAndColorStorage>();

            Guid category = MefItemsFontAndColorCategory;
            uint flags = (uint)(
                __FCSTORAGEFLAGS.FCSF_LOADDEFAULTS |
                __FCSTORAGEFLAGS.FCSF_PROPAGATECHANGES);

            ThrowOnFailure(storage.OpenCategory(ref category, flags));

            try {
                var descriptors = FormatCatalog.Value.ToDictionary(
                    descriptor => descriptor.Name,
                    StringComparer.Ordinal);

                foreach (PersistedColorFormat format in formats) {
                    VleColorFormatDescriptor descriptor;
                    if (!descriptors.TryGetValue(format.Name, out descriptor)) {
                        continue;
                    }

                    string storageName = ResolveStorageName(storage, descriptor);
                    ColorableItemInfo[] info = { format.ToColorableItemInfo() };
                    ThrowOnFailure(storage.SetItem(storageName, info));
                }
            }
            finally {
                ThrowOnFailure(storage.CloseCategory());
            }

            // FCSF_PROPAGATECHANGES applies the imported values to active views.
        }

        private static void RevertAllFormatsToDefault() {
            ThreadHelper.ThrowIfNotOnUIThread();

            object storageService = GetRequiredGlobalServiceObject(
                typeof(SVsFontAndColorStorage));
            IVsFontAndColorStorage storage = storageService as IVsFontAndColorStorage;
            IVsFontAndColorStorage2 storage2 = storageService as IVsFontAndColorStorage2;

            if (storage == null || storage2 == null) {
                throw new InvalidOperationException(
                    "Visual Studio font and color storage did not expose the required interfaces.");
            }

            Guid category = MefItemsFontAndColorCategory;
            uint flags = (uint)(
                __FCSTORAGEFLAGS.FCSF_LOADDEFAULTS |
                __FCSTORAGEFLAGS.FCSF_PROPAGATECHANGES);

            ThrowOnFailure(storage.OpenCategory(ref category, flags));

            try {
                foreach (VleColorFormatDescriptor descriptor in FormatCatalog.Value) {
                    string storageName = ResolveStorageName(storage, descriptor);
                    int hr = storage2.RevertItemToDefault(storageName);
                    if (Failed(hr) && descriptor.IsRequiredClassification) {
                        ThrowOnFailure(hr);
                    }
                }
            }
            finally {
                ThrowOnFailure(storage.CloseCategory());
            }

            // FCSF_PROPAGATECHANGES applies the reverted values to active views.
        }

        private static object GetRequiredGlobalServiceObject(Type serviceType) {
            ThreadHelper.ThrowIfNotOnUIThread();

            object service = Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider
                .GetService(serviceType);

            if (service == null) {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Visual Studio service {0} was not available.",
                        serviceType.FullName));
            }

            return service;
        }

        private static TInterface GetRequiredGlobalService<TService, TInterface>()
            where TInterface : class {

            ThreadHelper.ThrowIfNotOnUIThread();

            object service = GetRequiredGlobalServiceObject(typeof(TService));
            TInterface typedService = service as TInterface;

            if (typedService == null) {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Visual Studio service {0} was not available.",
                        typeof(TInterface).FullName));
            }

            return typedService;
        }

        private static bool TryGetStorageItem(
            IVsFontAndColorStorage storage,
            VleColorFormatDescriptor descriptor,
            out string storageName,
            out ColorableItemInfo info) {

            foreach (string candidate in descriptor.GetStorageNameCandidates()) {
                var buffer = new ColorableItemInfo[1];
                if (Succeeded(storage.GetItem(candidate, buffer))) {
                    storageName = candidate;
                    info = buffer[0];
                    return true;
                }
            }

            storageName = string.Empty;
            info = new ColorableItemInfo();
            return false;
        }

        private static string ResolveStorageName(
            IVsFontAndColorStorage storage,
            VleColorFormatDescriptor descriptor) {

            string storageName;
            ColorableItemInfo ignored;
            if (TryGetStorageItem(storage, descriptor, out storageName, out ignored)) {
                return storageName;
            }

            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Visual Studio did not expose VLE color item '{0}' or display name '{1}'.",
                    descriptor.Name,
                    descriptor.DisplayName));
        }

        private static IReadOnlyList<VleColorFormatDescriptor> DiscoverVleColorFormats() {
            Type editorFormatBaseType = typeof(EditorFormatDefinition);
            Type classificationFormatBaseType = typeof(ClassificationFormatDefinition);
            Assembly assembly = typeof(VleColorSettingsProfile).Assembly;
            var descriptors = new List<VleColorFormatDescriptor>();

            foreach (Type type in GetLoadableTypes(assembly)) {
                if (type == null ||
                    type.IsAbstract ||
                    !editorFormatBaseType.IsAssignableFrom(type)) {
                    continue;
                }

                NameAttribute nameAttribute = Attribute.GetCustomAttribute(
                    type,
                    typeof(NameAttribute),
                    false) as NameAttribute;
                UserVisibleAttribute userVisibleAttribute = Attribute.GetCustomAttribute(
                    type,
                    typeof(UserVisibleAttribute),
                    false) as UserVisibleAttribute;

                if (nameAttribute == null ||
                    string.IsNullOrWhiteSpace(nameAttribute.Name) ||
                    userVisibleAttribute == null ||
                    !userVisibleAttribute.UserVisible) {
                    continue;
                }

                EditorFormatDefinition definition =
                    Activator.CreateInstance(type, true) as EditorFormatDefinition;
                string displayName = definition == null
                    ? string.Empty
                    : definition.DisplayName ?? string.Empty;

                descriptors.Add(new VleColorFormatDescriptor(
                    nameAttribute.Name,
                    displayName,
                    type.FullName ?? type.Name,
                    classificationFormatBaseType.IsAssignableFrom(type)));
            }

            VleColorFormatDescriptor[] duplicate = descriptors
                .GroupBy(format => format.Name, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.First())
                .ToArray();

            if (duplicate.Length > 0) {
                throw new InvalidOperationException(
                    "Duplicate VLE editor format names: " +
                    string.Join(", ", duplicate.Select(format => format.Name)));
            }

            return descriptors
                .OrderBy(format => format.Name, StringComparer.Ordinal)
                .ToArray();
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly) {
            try {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex) {
                return ex.Types.Where(type => type != null);
            }
        }

        private static string MakeSettingPrefix(int index) {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Format.{0:D4}.",
                index);
        }

        private static string MakeUnavailableSettingName(int index) {
            return string.Format(
                CultureInfo.InvariantCulture,
                "UnavailableFormat.{0:D4}.Name",
                index);
        }

        private static int NormalizeBoolean(int value) {
            return value == 0 ? 0 : 1;
        }

        private static void ThrowOnFailure(int hr) {
            if (Failed(hr)) {
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        private static bool Failed(int hr) {
            return hr < 0;
        }

        private static bool Succeeded(int hr) {
            return hr >= 0;
        }

        private static bool TryReadLong(
            IVsSettingsReader reader,
            string name,
            out int value) {

            ThreadHelper.ThrowIfNotOnUIThread();

            value = 0;
            return Succeeded(reader.ReadSettingLong(name, out value));
        }

        private static bool TryReadString(
            IVsSettingsReader reader,
            string name,
            out string value) {

            ThreadHelper.ThrowIfNotOnUIThread();

            value = null;
            return Succeeded(reader.ReadSettingString(name, out value));
        }

        private sealed class CatalogReadResult
        {
            internal CatalogReadResult(
                List<PersistedColorFormat> formats,
                List<string> unavailableNames) {

                Formats = formats;
                UnavailableNames = unavailableNames;
            }

            internal List<PersistedColorFormat> Formats { get; private set; }
            internal List<string> UnavailableNames { get; private set; }
        }

        private sealed class VleColorFormatDescriptor
        {
            internal VleColorFormatDescriptor(
                string name,
                string displayName,
                string sourceType,
                bool isRequiredClassification) {

                Name = name;
                DisplayName = displayName ?? string.Empty;
                SourceType = sourceType;
                IsRequiredClassification = isRequiredClassification;
            }

            internal string Name { get; private set; }
            internal string DisplayName { get; private set; }
            internal string SourceType { get; private set; }
            internal bool IsRequiredClassification { get; private set; }

            internal IEnumerable<string> GetStorageNameCandidates() {
                yield return Name;

                if (!string.IsNullOrWhiteSpace(DisplayName) &&
                    !string.Equals(DisplayName, Name, StringComparison.Ordinal)) {
                    yield return DisplayName;
                }
            }
        }

        private sealed class PersistedColorFormat
        {
            internal PersistedColorFormat(
                string name,
                string storageName,
                string sourceType,
                int foregroundValid,
                uint foreground,
                int backgroundValid,
                uint background,
                int fontFlagsValid,
                uint fontFlags) {

                Name = name;
                StorageName = storageName ?? string.Empty;
                SourceType = sourceType ?? string.Empty;
                ForegroundValid = foregroundValid;
                Foreground = foreground;
                BackgroundValid = backgroundValid;
                Background = background;
                FontFlagsValid = fontFlagsValid;
                FontFlags = fontFlags;
            }

            internal string Name { get; private set; }
            internal string StorageName { get; private set; }
            internal string SourceType { get; private set; }
            internal int ForegroundValid { get; private set; }
            internal uint Foreground { get; private set; }
            internal int BackgroundValid { get; private set; }
            internal uint Background { get; private set; }
            internal int FontFlagsValid { get; private set; }
            internal uint FontFlags { get; private set; }

            internal static PersistedColorFormat FromColorableItemInfo(
                VleColorFormatDescriptor descriptor,
                string storageName,
                ColorableItemInfo info) {

                return new PersistedColorFormat(
                    descriptor.Name,
                    storageName,
                    descriptor.SourceType,
                    NormalizeBoolean(info.bForegroundValid),
                    info.crForeground,
                    NormalizeBoolean(info.bBackgroundValid),
                    info.crBackground,
                    NormalizeBoolean(info.bFontFlagsValid),
                    info.dwFontFlags);
            }

            internal ColorableItemInfo ToColorableItemInfo() {
                return new ColorableItemInfo
                {
                    bForegroundValid = ForegroundValid,
                    crForeground = Foreground,
                    bBackgroundValid = BackgroundValid,
                    crBackground = Background,
                    bFontFlagsValid = FontFlagsValid,
                    dwFontFlags = FontFlags
                };
            }
        }
    }
}
