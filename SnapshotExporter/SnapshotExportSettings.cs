// file: SnapshotExporter/SnapshotExportSettings.cs
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

using System;
using System.Globalization;
using System.IO;

namespace VerilogLanguage.Testing
{
    internal static class SnapshotExportSettings
    {
        private const string EnvSnapshotEnable = "VLE_SNAPSHOT_ENABLE";
        private const string EnvSnapshotGateFile = "VLE_SNAPSHOT_GATE_FILE";
        private const string EnvSnapshotOutputDir = "VLE_SNAPSHOT_OUTPUT_DIR";
        private const string EnvSnapshotRunName = "VLE_SNAPSHOT_RUN_NAME";
        private const string EnvSnapshotDelayMs = "VLE_SNAPSHOT_DELAY_MS";
        private const string EnvRepoRoot = "VLE_REPO_ROOT";
        private const string EnvGitCommit = "VLE_GIT_COMMIT";
        private const string ConfigFileName = "VerilogLanguage.ExportSnapshots.config";

        internal static bool IsExportOnOpenEnabled() {
            string enabled = Environment.GetEnvironmentVariable(EnvSnapshotEnable);
            if (string.Equals(enabled, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(enabled, "yes", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            string configEnabled = ReadConfigValue("Enable");
            if (string.Equals(configEnabled, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(configEnabled, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(configEnabled, "yes", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            string gateFile = Environment.GetEnvironmentVariable(EnvSnapshotGateFile);
            if (!string.IsNullOrWhiteSpace(gateFile) && File.Exists(gateFile)) {
                return true;
            }

            string legacyGateFile = Path.Combine(Path.GetTempPath(), "VerilogLanguage.ExportSnapshots.enable");
            return File.Exists(legacyGateFile);
        }

        internal static int GetDelayMs() {
            string value = GetValue(EnvSnapshotDelayMs, "DelayMs");
            int delayMs;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out delayMs) && delayMs >= 0) {
                return delayMs;
            }

            return 350;
        }

        internal static string GetOutputDirectory() {
            string outDir = GetValue(EnvSnapshotOutputDir, "OutputDir");
            if (!string.IsNullOrWhiteSpace(outDir)) {
                return outDir;
            }

            return Path.Combine(Path.GetTempPath(), "VerilogLanguageSnapshot");
        }

        internal static string GetRunName() {
            string runName = GetValue(EnvSnapshotRunName, "RunName");
            if (!string.IsNullOrWhiteSpace(runName)) {
                return runName;
            }

            return "manual";
        }

        internal static string GetRepoRoot() {
            string repoRoot = GetValue(EnvRepoRoot, "RepoRoot");
            if (!string.IsNullOrWhiteSpace(repoRoot)) {
                return Path.GetFullPath(repoRoot);
            }

            return null;
        }

        internal static string GetGitCommit() {
            return GetValue(EnvGitCommit, "GitCommit") ?? string.Empty;
        }

        private static string GetValue(string environmentName, string configName) {
            string value = Environment.GetEnvironmentVariable(environmentName);
            if (!string.IsNullOrWhiteSpace(value)) {
                return value;
            }

            return ReadConfigValue(configName);
        }

        private static string ReadConfigValue(string name) {
            if (string.IsNullOrWhiteSpace(name)) {
                return null;
            }

            string configFile = Path.Combine(Path.GetTempPath(), ConfigFileName);
            if (!File.Exists(configFile)) {
                return null;
            }

            try {
                foreach (string line in File.ReadAllLines(configFile)) {
                    if (string.IsNullOrWhiteSpace(line)) {
                        continue;
                    }

                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("#", StringComparison.Ordinal)) {
                        continue;
                    }

                    int eq = trimmed.IndexOf('=');
                    if (eq <= 0) {
                        continue;
                    }

                    string key = trimmed.Substring(0, eq).Trim();
                    string value = trimmed.Substring(eq + 1).Trim();
                    if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase)) {
                        return value;
                    }
                }
            }
            catch {
                return null;
            }

            return null;
        }

        internal static string MakeRelativePath(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath)) {
                return string.Empty;
            }

            string repoRoot = GetRepoRoot();
            if (string.IsNullOrWhiteSpace(repoRoot)) {
                return Path.GetFileName(filePath);
            }

            try {
                string fullPath = Path.GetFullPath(filePath);
                if (!repoRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)) {
                    repoRoot += Path.DirectorySeparatorChar;
                }

                Uri rootUri = new Uri(repoRoot);
                Uri fileUri = new Uri(fullPath);
                string relative = Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString());
                return relative.Replace('/', Path.DirectorySeparatorChar);
            }
            catch {
                return Path.GetFileName(filePath);
            }
        }

        internal static string MakeSnapshotFilePath(string filePath, int sequenceNumber) {
            string outDir = GetOutputDirectory();
            string fileName = MakeSafeFileName(filePath);

            if (sequenceNumber > 0) {
                fileName = sequenceNumber.ToString("0000", CultureInfo.InvariantCulture) + "-" + fileName;
            }

            return Path.Combine(outDir, fileName + ".snapshot.json");
        }

        internal static string MakeSafeFileName(string filePath) {
            string name = string.IsNullOrEmpty(filePath) ? "untitled" : Path.GetFileName(filePath);
            foreach (char c in Path.GetInvalidFileNameChars()) {
                name = name.Replace(c, '_');
            }

            return name;
        }
    }
}
