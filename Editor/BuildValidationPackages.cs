using System;
using System.Collections.Generic;
using System.Linq;

namespace Sorolla.Palette.Editor
{
    public static partial class BuildValidator
    {
        /// <summary>
        ///     Check that all required SDKs for the current mode are installed.
        /// </summary>
        static List<ValidationResult> CheckRequiredSdks()
        {
            var results = new List<ValidationResult>();

            if (!SorollaSettings.IsConfigured)
            {
                results.Add(Valid(CheckCategory.RequiredSdks, "Mode not configured"));
                return results;
            }

            var missing = new List<string>();
            foreach (SdkInfo sdk in SdkRegistry.GetRequired(SorollaSettings.IsPrototype))
            {
                if (!SdkDetector.IsInstalled(sdk))
                    missing.Add(sdk.Name);
            }

            if (missing.Count > 0)
            {
                string modeName = SorollaSettings.IsPrototype ? "Prototype" : "Full";
                results.Add(Error(
                    CheckCategory.RequiredSdks,
                    $"Missing required SDKs for {modeName} mode:\n  {string.Join(", ", missing)}",
                    "Click Refresh to auto-install missing SDKs"));
            }
            else
            {
                string modeName = SorollaSettings.IsPrototype ? "Prototype" : "Full";
                results.Add(Valid(CheckCategory.RequiredSdks, $"{modeName} mode SDKs OK"));
            }

            return results;
        }

        /// <summary>
        ///     Check for version mismatches between SdkRegistry and manifest.
        ///     Only warns if manifest version is OLDER than expected (newer is fine).
        /// </summary>
        static List<ValidationResult> CheckVersionMismatches(Dictionary<string, object> dependencies)
        {
            var results = new List<ValidationResult>();
            bool hasIssues = false;

            foreach (SdkInfo sdk in SdkRegistry.All.Values)
            {
                if (!dependencies.TryGetValue(sdk.PackageId, out object manifestValue))
                    continue; // Not installed, checked elsewhere

                string manifestVersion = manifestValue?.ToString() ?? "";
                string expectedVersion = sdk.DependencyValue;

                if (string.IsNullOrEmpty(expectedVersion))
                    continue; // Git URL or no version

                // Skip if versions match exactly
                if (manifestVersion == expectedVersion)
                    continue;

                // For Git URLs, compare tags
                if (manifestVersion.Contains("#") && expectedVersion.Contains("#"))
                {
                    string manifestTag = manifestVersion.Split('#').LastOrDefault();
                    string expectedTag = expectedVersion.Split('#').LastOrDefault();

                    if (manifestTag == expectedTag)
                        continue; // Tags match

                    // Compare Git URL tags as versions
                    if (CompareVersions(manifestTag, expectedTag) >= 0)
                        continue; // Manifest tag is newer or equal

                    hasIssues = true;
                    results.Add(Warning(
                        CheckCategory.VersionMismatches,
                        $"Outdated version - {sdk.PackageId}\n  Minimum: {expectedTag}\n  Found: {manifestTag}",
                        "Update the package to the minimum required version"));
                    continue;
                }

                // Compare semantic versions - only warn if manifest is OLDER
                if (CompareVersions(manifestVersion, expectedVersion) < 0)
                {
                    hasIssues = true;
                    results.Add(Warning(
                        CheckCategory.VersionMismatches,
                        $"Outdated version - {sdk.PackageId}\n  Minimum: {expectedVersion}\n  Found: {manifestVersion}",
                        "Update the package to the minimum required version"));
                }
            }

            // Add valid result if no issues found
            if (!hasIssues)
                results.Add(Valid(CheckCategory.VersionMismatches, "All SDK versions OK"));

            return results;
        }

        /// <summary>
        ///     Compare two version strings. Returns:
        ///     -1 if v1 &lt; v2, 0 if equal, 1 if v1 &gt; v2
        /// </summary>
        static int CompareVersions(string v1, string v2)
        {
            if (v1 == v2) return 0;
            if (string.IsNullOrEmpty(v1)) return -1;
            if (string.IsNullOrEmpty(v2)) return 1;

            try
            {
                int[] parts1 = v1.Split('.').Select(p => int.TryParse(p, out int n) ? n : 0).ToArray();
                int[] parts2 = v2.Split('.').Select(p => int.TryParse(p, out int n) ? n : 0).ToArray();

                int maxLen = Math.Max(parts1.Length, parts2.Length);
                for (int i = 0; i < maxLen; i++)
                {
                    int p1 = i < parts1.Length ? parts1[i] : 0;
                    int p2 = i < parts2.Length ? parts2[i] : 0;

                    if (p1 < p2) return -1;
                    if (p1 > p2) return 1;
                }

                return 0;
            }
            catch
            {
                // Fallback to string comparison
                return string.Compare(v1, v2, StringComparison.Ordinal);
            }
        }

        /// <summary>
        ///     Check mode consistency - verify installed SDKs match current mode
        /// </summary>
        static List<ValidationResult> CheckModeConsistency(Dictionary<string, object> dependencies)
        {
            var results = new List<ValidationResult>();
            bool hasIssues = false;

            if (!SorollaSettings.IsConfigured)
            {
                results.Add(Warning(
                    CheckCategory.ModeConsistency,
                    "No SDK mode configured. Open Tools > Sorolla Palette SDK to select Prototype or Full mode."));
                return results;
            }

            bool isPrototype = SorollaSettings.IsPrototype;
            string modeName = isPrototype ? "Prototype" : "Full";

            foreach (SdkInfo sdk in SdkRegistry.All.Values)
            {
                bool isInstalled = dependencies.ContainsKey(sdk.PackageId);

                // Check PrototypeOnly SDKs in Full mode
                if (sdk.Requirement == SdkRequirement.PrototypeOnly && !isPrototype && isInstalled)
                {
                    hasIssues = true;
                    results.Add(Warning(
                        CheckCategory.ModeConsistency,
                        $"{sdk.Name} is installed but only needed in Prototype mode (current: {modeName})",
                        "Switch to Prototype mode or remove the SDK"));
                }

                // Check FullOnly SDKs missing in Full mode
                if (sdk.Requirement == SdkRequirement.FullOnly && !isPrototype && !isInstalled)
                {
                    hasIssues = true;
                    results.Add(Error(
                        CheckCategory.ModeConsistency,
                        $"{sdk.Name} is required in Full mode but not installed",
                        "Install the SDK or switch to Prototype mode"));
                }

                // Check FullOnly SDKs in Prototype mode
                if (sdk.Requirement == SdkRequirement.FullOnly && isPrototype && isInstalled)
                {
                    hasIssues = true;
                    results.Add(Warning(
                        CheckCategory.ModeConsistency,
                        $"{sdk.Name} is installed but only needed in Full mode (current: {modeName})",
                        "Switch to Full mode or remove the SDK"));
                }
            }

            if (!hasIssues)
                results.Add(Valid(CheckCategory.ModeConsistency, $"{modeName} mode SDKs OK"));

            return results;
        }

        /// <summary>
        ///     Check that required scoped registries are configured
        /// </summary>
        static List<ValidationResult> CheckScopedRegistries(
            Dictionary<string, object> dependencies,
            List<object> registries)
        {
            var results = new List<ValidationResult>();
            bool hasIssues = false;

            // Build list of all scopes in registries
            var configuredScopes = new HashSet<string>();
            foreach (object reg in registries)
            {
                if (reg is Dictionary<string, object> registry &&
                    registry.TryGetValue("scopes", out object scopesObj) &&
                    scopesObj is List<object> scopes)
                {
                    foreach (object scope in scopes)
                        configuredScopes.Add(scope.ToString());
                }
            }

            // Check each installed SDK has required scope
            foreach (SdkInfo sdk in SdkRegistry.All.Values)
            {
                if (string.IsNullOrEmpty(sdk.Scope))
                    continue; // No scope needed (Unity registry or Git URL)

                if (!dependencies.ContainsKey(sdk.PackageId))
                    continue; // Not installed

                if (!configuredScopes.Contains(sdk.Scope))
                {
                    hasIssues = true;
                    results.Add(Error(
                        CheckCategory.ScopedRegistries,
                        $"Missing scoped registry for {sdk.Name}\n  Required scope: {sdk.Scope}",
                        "Open Tools > Sorolla Palette SDK to fix registry configuration"));
                }
            }

            if (!hasIssues)
                results.Add(Valid(CheckCategory.ScopedRegistries, "All registries configured"));

            return results;
        }
    }
}
