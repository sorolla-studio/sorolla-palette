using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Pre-build validation for SDK conflicts, version mismatches, and configuration issues.
    ///     Runs automatically before builds via IPreprocessBuildWithReport.
    /// </summary>
    public static class BuildValidator
    {
        private const string Tag = "[Palette BuildValidator]";
        private static string ManifestPath => Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");

        public enum ValidationStatus
        {
            Valid,
            Warning,
            Error
        }

        public enum CheckCategory
        {
            RequiredSdks,
            VersionMismatches,
            ModeConsistency,
            ScopedRegistries,
            FirebaseCoherence,
            ConfigSync,
            AndroidManifest,
            MaxSettings,
            AdjustSettings,
            Edm4uSettings
        }

        public static readonly Dictionary<CheckCategory, string> CheckNames = new()
        {
            [CheckCategory.RequiredSdks] = "Required SDKs",
            [CheckCategory.VersionMismatches] = "SDK Versions",
            [CheckCategory.ModeConsistency] = "Mode Consistency",
            [CheckCategory.ScopedRegistries] = "Scoped Registries",
            [CheckCategory.FirebaseCoherence] = "Firebase Coherence",
            [CheckCategory.ConfigSync] = "Config Sync",
            [CheckCategory.AndroidManifest] = "Android Manifest",
            [CheckCategory.MaxSettings] = "MAX Settings",
            [CheckCategory.AdjustSettings] = "Adjust Settings",
            [CheckCategory.Edm4uSettings] = "EDM4U Settings"
        };

        public class ValidationResult
        {
            public ValidationStatus Status;
            public string Message;
            public string Fix;
            public CheckCategory Category;

            public ValidationResult(ValidationStatus status, string message, string fix = null, CheckCategory category = CheckCategory.VersionMismatches)
            {
                Status = status;
                Message = message;
                Fix = fix;
                Category = category;
            }
        }

        /// <summary>
        ///     Manual validation via console (use BuildValidationWindow for UI)
        /// </summary>
        public static void ValidateBuildConsole()
        {
            Debug.Log($"{Tag} Starting validation...");

            var results = RunAllChecks();
            DisplayResults(results);
        }

        /// <summary>
        ///     Run all validation checks
        /// </summary>
        public static List<ValidationResult> RunAllChecks()
        {
            var results = new List<ValidationResult>();

            try
            {
                var manifest = ReadManifest();
                if (manifest == null)
                {
                    results.Add(new ValidationResult(ValidationStatus.Error, "Failed to read manifest.json"));
                    return results;
                }

                var dependencies = manifest.TryGetValue("dependencies", out var deps)
                    ? deps as Dictionary<string, object>
                    : new Dictionary<string, object>();

                var registries = manifest.TryGetValue("scopedRegistries", out var regs)
                    ? regs as List<object>
                    : new List<object>();

                // Run all checks
                results.AddRange(CheckRequiredSdks());
                results.AddRange(CheckVersionMismatches(dependencies));
                results.AddRange(CheckModeConsistency(dependencies));
                results.AddRange(CheckScopedRegistries(dependencies, registries));
                results.AddRange(CheckFirebaseCoherence(dependencies));
                results.AddRange(CheckConfigSync(dependencies));
                results.AddRange(CheckAndroidManifest());
                results.AddRange(CheckMaxSettings());
                results.AddRange(CheckAdjustSettings());
                results.AddRange(CheckEdm4uSettings());
            }
            catch (Exception e)
            {
                results.Add(new ValidationResult(ValidationStatus.Error, $"Validation failed: {e.Message}"));
            }

            return results;
        }

        /// <summary>
        ///     Run all auto-fixes before validation. Returns list of fixes applied.
        ///     This is the single source of truth for all sanitizers.
        /// </summary>
        public static List<string> RunAutoFixes()
        {
            var fixes = new List<string>();

            // AndroidManifest sanitization
            var orphaned = AndroidManifestSanitizer.DetectOrphanedEntries();
            var duplicates = AndroidManifestSanitizer.DetectDuplicateActivities();
            var wrongActivity = AndroidManifestSanitizer.DetectWrongMainActivity();
            if (orphaned.Count > 0 || duplicates.Count > 0 || wrongActivity != null)
            {
                foreach (var (sdkId, _) in orphaned)
                    fixes.Add($"Removed {SdkRegistry.All[sdkId].Name} entries from AndroidManifest.xml");
                if (duplicates.Count > 0)
                    fixes.Add($"Removed {duplicates.Count} duplicate activity declaration(s)");
                if (wrongActivity != null)
                    fixes.Add($"Fixed main activity: {wrongActivity} → UnityPlayerGameActivity");
                AndroidManifestSanitizer.Sanitize();
            }

            // MAX settings sanitization
            if (MaxSettingsSanitizer.Sanitize())
                fixes.Add("Disabled AppLovin Quality Service (prevents build failures)");

            return fixes;
        }

        /// <summary>
        ///     Check that all required SDKs for the current mode are installed.
        /// </summary>
        private static List<ValidationResult> CheckRequiredSdks()
        {
            var results = new List<ValidationResult>();

            if (!SorollaSettings.IsConfigured)
            {
                results.Add(new ValidationResult(ValidationStatus.Valid, "Mode not configured", category: CheckCategory.RequiredSdks));
                return results;
            }

            var missing = new List<string>();
            foreach (var sdk in SdkRegistry.GetRequired(SorollaSettings.IsPrototype))
            {
                if (!SdkDetector.IsInstalled(sdk))
                    missing.Add(sdk.Name);
            }

            if (missing.Count > 0)
            {
                var modeName = SorollaSettings.IsPrototype ? "Prototype" : "Full";
                results.Add(new ValidationResult(
                    ValidationStatus.Error,
                    $"Missing required SDKs for {modeName} mode:\n  {string.Join(", ", missing)}",
                    "Click Refresh to auto-install missing SDKs",
                    CheckCategory.RequiredSdks
                ));
            }
            else
            {
                var modeName = SorollaSettings.IsPrototype ? "Prototype" : "Full";
                results.Add(new ValidationResult(ValidationStatus.Valid, $"{modeName} mode SDKs OK", category: CheckCategory.RequiredSdks));
            }

            return results;
        }

        /// <summary>
        ///     Check for version mismatches between SdkRegistry and manifest.
        ///     Only warns if manifest version is OLDER than expected (newer is fine).
        /// </summary>
        private static List<ValidationResult> CheckVersionMismatches(Dictionary<string, object> dependencies)
        {
            var results = new List<ValidationResult>();
            var hasIssues = false;

            foreach (var sdk in SdkRegistry.All.Values)
            {
                if (!dependencies.TryGetValue(sdk.PackageId, out var manifestValue))
                    continue; // Not installed, checked elsewhere

                var manifestVersion = manifestValue?.ToString() ?? "";
                var expectedVersion = sdk.DependencyValue;

                if (string.IsNullOrEmpty(expectedVersion))
                    continue; // Git URL or no version

                // Skip if versions match exactly
                if (manifestVersion == expectedVersion)
                    continue;

                // For Git URLs, compare tags
                if (manifestVersion.Contains("#") && expectedVersion.Contains("#"))
                {
                    var manifestTag = manifestVersion.Split('#').LastOrDefault();
                    var expectedTag = expectedVersion.Split('#').LastOrDefault();

                    if (manifestTag == expectedTag)
                        continue; // Tags match

                    // Compare Git URL tags as versions
                    if (CompareVersions(manifestTag, expectedTag) >= 0)
                        continue; // Manifest tag is newer or equal

                    hasIssues = true;
                    results.Add(new ValidationResult(
                        ValidationStatus.Warning,
                        $"Outdated version - {sdk.PackageId}\n  Minimum: {expectedTag}\n  Found: {manifestTag}",
                        "Update the package to the minimum required version",
                        CheckCategory.VersionMismatches
                    ));
                    continue;
                }

                // Compare semantic versions - only warn if manifest is OLDER
                if (CompareVersions(manifestVersion, expectedVersion) < 0)
                {
                    hasIssues = true;
                    results.Add(new ValidationResult(
                        ValidationStatus.Warning,
                        $"Outdated version - {sdk.PackageId}\n  Minimum: {expectedVersion}\n  Found: {manifestVersion}",
                        "Update the package to the minimum required version",
                        CheckCategory.VersionMismatches
                    ));
                }
            }

            // Add valid result if no issues found
            if (!hasIssues)
                results.Add(new ValidationResult(ValidationStatus.Valid, "All SDK versions OK", category: CheckCategory.VersionMismatches));

            return results;
        }

        /// <summary>
        ///     Compare two version strings. Returns:
        ///     -1 if v1 &lt; v2, 0 if equal, 1 if v1 &gt; v2
        /// </summary>
        private static int CompareVersions(string v1, string v2)
        {
            if (v1 == v2) return 0;
            if (string.IsNullOrEmpty(v1)) return -1;
            if (string.IsNullOrEmpty(v2)) return 1;

            try
            {
                var parts1 = v1.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
                var parts2 = v2.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();

                var maxLen = Math.Max(parts1.Length, parts2.Length);
                for (var i = 0; i < maxLen; i++)
                {
                    var p1 = i < parts1.Length ? parts1[i] : 0;
                    var p2 = i < parts2.Length ? parts2[i] : 0;

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
        private static List<ValidationResult> CheckModeConsistency(Dictionary<string, object> dependencies)
        {
            var results = new List<ValidationResult>();
            var hasIssues = false;

            if (!SorollaSettings.IsConfigured)
            {
                results.Add(new ValidationResult(
                    ValidationStatus.Warning,
                    "No SDK mode configured. Run Palette > Configuration to select Prototype or Full mode.",
                    category: CheckCategory.ModeConsistency
                ));
                return results;
            }

            var isPrototype = SorollaSettings.IsPrototype;
            var modeName = isPrototype ? "Prototype" : "Full";

            foreach (var sdk in SdkRegistry.All.Values)
            {
                var isInstalled = dependencies.ContainsKey(sdk.PackageId);

                // Check PrototypeOnly SDKs in Full mode
                if (sdk.Requirement == SdkRequirement.PrototypeOnly && !isPrototype && isInstalled)
                {
                    hasIssues = true;
                    results.Add(new ValidationResult(
                        ValidationStatus.Warning,
                        $"{sdk.Name} is installed but only needed in Prototype mode (current: {modeName})",
                        "Switch to Prototype mode or remove the SDK",
                        CheckCategory.ModeConsistency
                    ));
                }

                // Check FullOnly SDKs in Prototype mode
                if (sdk.Requirement == SdkRequirement.FullOnly && isPrototype && isInstalled)
                {
                    hasIssues = true;
                    results.Add(new ValidationResult(
                        ValidationStatus.Warning,
                        $"{sdk.Name} is installed but only needed in Full mode (current: {modeName})",
                        "Switch to Full mode or remove the SDK",
                        CheckCategory.ModeConsistency
                    ));
                }
            }

            if (!hasIssues)
                results.Add(new ValidationResult(ValidationStatus.Valid, $"{modeName} mode SDKs OK", category: CheckCategory.ModeConsistency));

            return results;
        }

        /// <summary>
        ///     Check that required scoped registries are configured
        /// </summary>
        private static List<ValidationResult> CheckScopedRegistries(
            Dictionary<string, object> dependencies,
            List<object> registries)
        {
            var results = new List<ValidationResult>();
            var hasIssues = false;

            // Build list of all scopes in registries
            var configuredScopes = new HashSet<string>();
            foreach (var reg in registries)
            {
                if (reg is Dictionary<string, object> registry &&
                    registry.TryGetValue("scopes", out var scopesObj) &&
                    scopesObj is List<object> scopes)
                {
                    foreach (var scope in scopes)
                        configuredScopes.Add(scope.ToString());
                }
            }

            // Check each installed SDK has required scope
            foreach (var sdk in SdkRegistry.All.Values)
            {
                if (string.IsNullOrEmpty(sdk.Scope))
                    continue; // No scope needed (Unity registry or Git URL)

                if (!dependencies.ContainsKey(sdk.PackageId))
                    continue; // Not installed

                if (!configuredScopes.Contains(sdk.Scope))
                {
                    hasIssues = true;
                    results.Add(new ValidationResult(
                        ValidationStatus.Error,
                        $"Missing scoped registry for {sdk.Name}\n  Required scope: {sdk.Scope}",
                        "Run Palette > Configuration to fix registry configuration",
                        CheckCategory.ScopedRegistries
                    ));
                }
            }

            if (!hasIssues)
                results.Add(new ValidationResult(ValidationStatus.Valid, "All registries configured", category: CheckCategory.ScopedRegistries));

            return results;
        }

        /// <summary>
        ///     Check Firebase module coherence - FirebaseApp required if other modules installed
        /// </summary>
        private static List<ValidationResult> CheckFirebaseCoherence(Dictionary<string, object> dependencies)
        {
            var results = new List<ValidationResult>();

            var firebaseModules = new[]
            {
                SdkId.FirebaseAnalytics,
                SdkId.FirebaseCrashlytics,
                SdkId.FirebaseRemoteConfig
            };

            var hasFirebaseApp = dependencies.ContainsKey(SdkRegistry.All[SdkId.FirebaseApp].PackageId);
            var installedModules = firebaseModules
                .Where(id => dependencies.ContainsKey(SdkRegistry.All[id].PackageId))
                .Select(id => SdkRegistry.All[id].Name)
                .ToList();

            if (installedModules.Count > 0 && !hasFirebaseApp)
            {
                results.Add(new ValidationResult(
                    ValidationStatus.Error,
                    $"Firebase modules installed without FirebaseApp:\n  {string.Join(", ", installedModules)}",
                    "Install com.google.firebase.app or remove Firebase modules",
                    CheckCategory.FirebaseCoherence
                ));
            }
            else if (installedModules.Count > 0)
            {
                results.Add(new ValidationResult(ValidationStatus.Valid, "Firebase modules OK", category: CheckCategory.FirebaseCoherence));
            }
            else if (!SorollaSettings.IsPrototype)
            {
                // Firebase missing in Full mode — warn
                results.Add(new ValidationResult(
                    ValidationStatus.Warning,
                    "Firebase not installed (required in Full mode)",
                    "Run setup or open Palette > Configuration to install Firebase.",
                    CheckCategory.FirebaseCoherence
                ));
            }
            else
            {
                // Firebase missing in Prototype mode — silently valid (optional)
                results.Add(new ValidationResult(ValidationStatus.Valid, "Firebase not installed (optional in Prototype)", category: CheckCategory.FirebaseCoherence));
            }

            return results;
        }

        /// <summary>
        ///     Check that SorollaConfig settings match installed SDKs
        /// </summary>
        private static List<ValidationResult> CheckConfigSync(Dictionary<string, object> dependencies)
        {
            var results = new List<ValidationResult>();
            var hasIssues = false;

            var config = Resources.Load<SorollaConfig>("SorollaConfig");
            if (config == null)
            {
                results.Add(new ValidationResult(
                    ValidationStatus.Warning,
                    "SorollaConfig not found in Resources folder",
                    "Create config via Assets > Create > Palette > Config",
                    CheckCategory.ConfigSync
                ));
                return results;
            }

            // Check mode sync
            if (SorollaSettings.IsConfigured && config.isPrototypeMode != SorollaSettings.IsPrototype)
            {
                hasIssues = true;
                results.Add(new ValidationResult(
                    ValidationStatus.Warning,
                    $"Config mode mismatch - SorollaConfig.isPrototypeMode={config.isPrototypeMode}, " +
                    $"SorollaSettings.Mode={SorollaSettings.Mode}",
                    "Run Palette > Configuration to sync mode settings",
                    CheckCategory.ConfigSync
                ));
            }

            if (!hasIssues)
                results.Add(new ValidationResult(ValidationStatus.Valid, "Config synced", category: CheckCategory.ConfigSync));

            return results;
        }

        /// <summary>
        ///     Auto-fix config sync issues.
        ///     Returns true if any fixes were applied.
        /// </summary>
        /// <param name="installMissingSdks">If true, also install missing required SDKs. Use only on explicit user action.</param>
        public static bool FixConfigSync(bool installMissingSdks = false)
        {
            var config = Resources.Load<SorollaConfig>("SorollaConfig");
            if (config == null)
                return false;

            bool changed = false;

            // Fix mode sync
            if (SorollaSettings.IsConfigured && config.isPrototypeMode != SorollaSettings.IsPrototype)
            {
                config.isPrototypeMode = SorollaSettings.IsPrototype;
                changed = true;
                Debug.Log($"{Tag} Auto-fixed: Synced config.isPrototypeMode to {SorollaSettings.IsPrototype}");
            }

            // Auto-install missing required SDKs (only on explicit user action)
            if (installMissingSdks && SorollaSettings.IsConfigured && !SdkDetector.AreAllRequiredInstalled(SorollaSettings.IsPrototype))
            {
                var modeName = SorollaSettings.IsPrototype ? "Prototype" : "Full";
                Debug.Log($"{Tag} Auto-fixing: Installing missing required SDKs for {modeName} mode...");
                SdkInstaller.InstallRequiredSdks(SorollaSettings.IsPrototype);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                Debug.Log($"{Tag} Config sync issues auto-fixed");
            }

            return changed;
        }

        /// <summary>
        ///     Check AndroidManifest.xml for orphaned SDK entries that will cause runtime crashes
        /// </summary>
        private static List<ValidationResult> CheckAndroidManifest()
        {
            var results = new List<ValidationResult>();
            var hasIssues = false;

            // Check for orphaned SDK entries
            var orphaned = AndroidManifestSanitizer.DetectOrphanedEntries();
            if (orphaned.Count > 0)
            {
                hasIssues = true;
                foreach (var (sdkId, entries) in orphaned)
                {
                    var sdkName = SdkRegistry.All[sdkId].Name;
                    results.Add(new ValidationResult(
                        ValidationStatus.Error,
                        $"AndroidManifest.xml has {sdkName} entries but SDK is not installed!\n" +
                        $"  Found patterns: {string.Join(", ", entries)}\n" +
                        "  This WILL crash at runtime.",
                        "Open Palette > Configuration and click Refresh in Build Health",
                        CheckCategory.AndroidManifest
                    ));
                }
            }

            // Check for duplicate activities
            var duplicates = AndroidManifestSanitizer.DetectDuplicateActivities();
            if (duplicates.Count > 0)
            {
                hasIssues = true;
                results.Add(new ValidationResult(
                    ValidationStatus.Error,
                    $"AndroidManifest.xml has duplicate activity declarations!\n" +
                    $"  Duplicates: {string.Join(", ", duplicates)}\n" +
                    "  This WILL cause build failures.",
                    "Open Palette > Configuration and click Refresh in Build Health",
                    CheckCategory.AndroidManifest
                ));
            }

            // Check for wrong main activity class (e.g. AppUIGameActivity instead of UnityPlayerGameActivity)
            var wrongActivity = AndroidManifestSanitizer.DetectWrongMainActivity();
            if (wrongActivity != null)
            {
                hasIssues = true;
                results.Add(new ValidationResult(
                    ValidationStatus.Error,
                    $"AndroidManifest.xml has wrong main activity!\n" +
                    $"  Found: {wrongActivity}\n" +
                    $"  Expected: com.unity3d.player.UnityPlayerGameActivity\n" +
                    "  The app WILL crash on launch.",
                    "Open Palette > Configuration and click Refresh in Build Health",
                    CheckCategory.AndroidManifest
                ));
            }

            if (!hasIssues)
                results.Add(new ValidationResult(ValidationStatus.Valid, "Manifest clean", category: CheckCategory.AndroidManifest));

            return results;
        }

        /// <summary>
        ///     Check AppLovin MAX settings for known issues
        /// </summary>
        private static List<ValidationResult> CheckMaxSettings()
        {
            var results = new List<ValidationResult>();

#if SOROLLA_MAX_INSTALLED
            var hasIssues = false;

            var config = Resources.Load<SorollaConfig>("SorollaConfig");
            if (config == null)
            {
                results.Add(new ValidationResult(
                    ValidationStatus.Warning,
                    "SorollaConfig not found - cannot validate MAX SDK key",
                    "Create config via Assets > Create > Palette > Config",
                    CheckCategory.MaxSettings
                ));
                return results;
            }

            // Check SDK key is configured in SorollaConfig (single source of truth)
            var maxStatus = SdkConfigDetector.GetMaxStatus(config);
            if (maxStatus == SdkConfigDetector.ConfigStatus.NotConfigured)
            {
                hasIssues = true;
                results.Add(new ValidationResult(
                    ValidationStatus.Error,
                    "AppLovin SDK key is not configured!\n" +
                    "  SDK key must be set in Palette Configuration.\n" +
                    "  Ads will not work without a valid SDK key.",
                    "Open Palette > Configuration and enter MAX SDK key",
                    CheckCategory.MaxSettings
                ));
            }

            // Check Quality Service (causes 401 build failures)
            if (MaxSettingsSanitizer.IsQualityServiceEnabled())
            {
                hasIssues = true;
                results.Add(new ValidationResult(
                    ValidationStatus.Warning,
                    "AppLovin Quality Service is enabled.\n" +
                    "  This can cause 401 errors and build failures.\n" +
                    "  Quality Service is optional - ads work without it.",
                    "Open Palette > Configuration and click Refresh in Build Health",
                    CheckCategory.MaxSettings
                ));
            }

            if (!hasIssues)
                results.Add(new ValidationResult(ValidationStatus.Valid, "MAX SDK key OK", category: CheckCategory.MaxSettings));
#else
            results.Add(new ValidationResult(ValidationStatus.Valid, "MAX not installed", category: CheckCategory.MaxSettings));
#endif

            return results;
        }

        /// <summary>
        ///     Check Adjust SDK app token configuration (Full mode only).
        ///     Note: SDK installation is checked by CheckRequiredSdks().
        /// </summary>
        private static List<ValidationResult> CheckAdjustSettings()
        {
            var results = new List<ValidationResult>();

            // Only check in Full mode when Adjust is installed
            if (!SorollaSettings.IsConfigured || SorollaSettings.IsPrototype)
            {
                results.Add(new ValidationResult(ValidationStatus.Valid, "Adjust not required", category: CheckCategory.AdjustSettings));
                return results;
            }

            if (!SdkDetector.IsInstalled(SdkId.Adjust))
            {
                // Installation is checked by CheckRequiredSdks - just skip config check here
                results.Add(new ValidationResult(ValidationStatus.Valid, "Adjust not installed", category: CheckCategory.AdjustSettings));
                return results;
            }

            var config = Resources.Load<SorollaConfig>("SorollaConfig");
            if (config == null)
            {
                results.Add(new ValidationResult(
                    ValidationStatus.Warning,
                    "SorollaConfig not found - cannot validate Adjust app token",
                    "Create config via Assets > Create > Palette > Config",
                    CheckCategory.AdjustSettings
                ));
                return results;
            }

            var adjustStatus = SdkConfigDetector.GetAdjustStatus(config);
            if (adjustStatus == SdkConfigDetector.ConfigStatus.NotConfigured)
            {
                results.Add(new ValidationResult(
                    ValidationStatus.Error,
                    "Adjust app token is not configured!\n" +
                    "  Attribution tracking will not work without a valid app token.\n" +
                    "  Enter your Adjust app token in Palette > Configuration.",
                    "Open Palette > Configuration and enter Adjust app token",
                    CheckCategory.AdjustSettings
                ));
            }
            else
            {
                results.Add(new ValidationResult(ValidationStatus.Valid, "Adjust app token OK", category: CheckCategory.AdjustSettings));
            }

            return results;
        }

        /// <summary>
        ///     Check for duplicate EDM4U installations and Gradle template mode configuration.
        /// </summary>
        private static List<ValidationResult> CheckEdm4uSettings()
        {
            var results = new List<ValidationResult>();
            var hasIssues = false;

            // Check for duplicate installations
            var duplicates = Edm4uSanitizer.DetectDuplicateInstallations();
            if (duplicates.Count > 0)
            {
                hasIssues = true;
                results.AddRange(duplicates.Select(dup => new ValidationResult(
                    ValidationStatus.Warning,
                    dup,
                    "Remove duplicate EDM4U from Assets/ folder",
                    CheckCategory.Edm4uSettings
                )));
            }

            // Check Gradle template mode (prevents Java 17+ compatibility issues)
            var gradleCheck = CheckEdm4uGradleMode();
            if (gradleCheck != null)
            {
                hasIssues = true;
                results.Add(gradleCheck);
            }

            if (!hasIssues)
                results.Add(new ValidationResult(ValidationStatus.Valid, "EDM4U settings OK", category: CheckCategory.Edm4uSettings));

            return results;
        }

        /// <summary>
        ///     Check that EDM4U is configured for Gradle template mode.
        ///     Without this, EDM4U uses its bundled Gradle 5.1.1 which is incompatible with Java 17+ (Unity 6+).
        /// </summary>
        private static ValidationResult CheckEdm4uGradleMode()
        {
            // Find EDM4U's SettingsDialog type via reflection
            Type settingsType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                settingsType = assembly.GetType("GooglePlayServices.SettingsDialog");
                if (settingsType != null)
                    break;
            }

            if (settingsType == null)
                return null; // EDM4U not installed, nothing to check

            try
            {
                const BindingFlags staticFlags = BindingFlags.Public | BindingFlags.Static;
                var mainTemplateProp = settingsType.GetProperty("PatchMainTemplateGradle", staticFlags);

                if (mainTemplateProp != null && !(bool)mainTemplateProp.GetValue(null))
                {
                    return new ValidationResult(
                        ValidationStatus.Warning,
                        "EDM4U not configured for Gradle templates.\n" +
                        "  This causes Java 17+ compatibility errors on Android resolve.\n" +
                        "  Unity 6+ requires Gradle template mode.",
                        "Run Palette > Run Setup (Force)",
                        CheckCategory.Edm4uSettings
                    );
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} Could not check EDM4U Gradle mode: {e.Message}");
            }

            return null;
        }

        /// <summary>
        ///     Read and parse manifest.json
        /// </summary>
        private static Dictionary<string, object> ReadManifest()
        {
            if (!File.Exists(ManifestPath))
            {
                Debug.LogError($"{Tag} manifest.json not found at: {ManifestPath}");
                return null;
            }

            try
            {
                var json = File.ReadAllText(ManifestPath);
                return MiniJson.Deserialize(json) as Dictionary<string, object>;
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} Failed to parse manifest.json: {e.Message}");
                return null;
            }
        }

        /// <summary>
        ///     Display validation results to console and dialog
        /// </summary>
        private static void DisplayResults(List<ValidationResult> results)
        {
            var errors = results.Count(r => r.Status == ValidationStatus.Error);
            var warnings = results.Count(r => r.Status == ValidationStatus.Warning);

            // Log each result
            foreach (var result in results)
            {
                var fixText = string.IsNullOrEmpty(result.Fix) ? "" : $"\n  Fix: {result.Fix}";

                switch (result.Status)
                {
                    case ValidationStatus.Error:
                        Debug.LogError($"{Tag} ERROR: {result.Message}{fixText}");
                        break;
                    case ValidationStatus.Warning:
                        Debug.LogWarning($"{Tag} WARNING: {result.Message}{fixText}");
                        break;
                    default:
                        Debug.Log($"{Tag} {result.Message}");
                        break;
                }
            }

            // Summary
            if (errors == 0 && warnings == 0)
            {
                Debug.Log($"{Tag} Validation passed - no issues found");
                EditorUtility.DisplayDialog(
                    "Build Validation",
                    "All checks passed. Build is ready.",
                    "OK"
                );
            }
            else
            {
                var summary = $"Validation complete: {errors} error(s), {warnings} warning(s)";
                Debug.Log($"{Tag} {summary}");

                EditorUtility.DisplayDialog(
                    "Build Validation",
                    $"{summary}\n\nCheck Console for details.",
                    "OK"
                );
            }
        }
    }

    /// <summary>
    ///     Auto-run validation before builds
    /// </summary>
    public class BuildValidatorPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("[Palette BuildValidator] Running pre-build validation...");

            // Run all auto-fixes before validation
            var fixes = BuildValidator.RunAutoFixes();
            foreach (var fix in fixes)
                Debug.Log($"[Palette BuildValidator] Auto-fix: {fix}");

            var results = BuildValidator.RunAllChecks();
            var errors = results.Where(r => r.Status == BuildValidator.ValidationStatus.Error).ToList();

            if (errors.Count > 0)
            {
                foreach (var error in errors)
                    Debug.LogError($"[Palette BuildValidator] ERROR: {error.Message}");

                throw new BuildFailedException(
                    $"Build validation failed with {errors.Count} error(s). " +
                    "Open Palette > Configuration for details."
                );
            }

            // Log warnings but don't block build
            var warnings = results.Where(r => r.Status == BuildValidator.ValidationStatus.Warning).ToList();
            foreach (var warning in warnings)
                Debug.LogWarning($"[Palette BuildValidator] WARNING: {warning.Message}");

            if (warnings.Count > 0)
                Debug.Log($"[Palette BuildValidator] Pre-build validation passed with {warnings.Count} warning(s)");
            else
                Debug.Log("[Palette BuildValidator] Pre-build validation passed");
        }
    }
}
