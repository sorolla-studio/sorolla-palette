using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Pre-build validation for SDK conflicts, version mismatches, and configuration issues.
    ///     Menu: Palette > Tools > Validate Build
    ///     Also runs automatically before builds via IPreprocessBuildWithReport.
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
            VersionMismatches,
            ModeConsistency,
            ScopedRegistries,
            FirebaseCoherence,
            ConfigSync,
            AndroidManifest
        }

        public static readonly Dictionary<CheckCategory, string> CheckNames = new()
        {
            [CheckCategory.VersionMismatches] = "SDK Versions",
            [CheckCategory.ModeConsistency] = "Mode Consistency",
            [CheckCategory.ScopedRegistries] = "Scoped Registries",
            [CheckCategory.FirebaseCoherence] = "Firebase Coherence",
            [CheckCategory.ConfigSync] = "Config Sync",
            [CheckCategory.AndroidManifest] = "Android Manifest"
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
                results.AddRange(CheckVersionMismatches(dependencies));
                results.AddRange(CheckModeConsistency(dependencies));
                results.AddRange(CheckScopedRegistries(dependencies, registries));
                results.AddRange(CheckFirebaseCoherence(dependencies));
                results.AddRange(CheckConfigSync(dependencies));
                results.AddRange(CheckAndroidManifest());
            }
            catch (Exception e)
            {
                results.Add(new ValidationResult(ValidationStatus.Error, $"Validation failed: {e.Message}"));
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
            else
            {
                results.Add(new ValidationResult(ValidationStatus.Valid, "No Firebase (optional)", category: CheckCategory.FirebaseCoherence));
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

            // Check Firebase Analytics
            var hasFirebaseAnalytics = dependencies.ContainsKey(SdkRegistry.All[SdkId.FirebaseAnalytics].PackageId);
            if (config.enableFirebaseAnalytics && !hasFirebaseAnalytics)
            {
                hasIssues = true;
                results.Add(new ValidationResult(
                    ValidationStatus.Error,
                    "Firebase Analytics enabled in config but not installed in manifest",
                    "Install Firebase Analytics or disable in SorollaConfig",
                    CheckCategory.ConfigSync
                ));
            }

            // Check Firebase Crashlytics
            var hasCrashlytics = dependencies.ContainsKey(SdkRegistry.All[SdkId.FirebaseCrashlytics].PackageId);
            if (config.enableCrashlytics && !hasCrashlytics)
            {
                hasIssues = true;
                results.Add(new ValidationResult(
                    ValidationStatus.Error,
                    "Firebase Crashlytics enabled in config but not installed in manifest",
                    "Install Firebase Crashlytics or disable in SorollaConfig",
                    CheckCategory.ConfigSync
                ));
            }

            // Check Firebase Remote Config
            var hasRemoteConfig = dependencies.ContainsKey(SdkRegistry.All[SdkId.FirebaseRemoteConfig].PackageId);
            if (config.enableRemoteConfig && !hasRemoteConfig)
            {
                hasIssues = true;
                results.Add(new ValidationResult(
                    ValidationStatus.Error,
                    "Firebase Remote Config enabled in config but not installed in manifest",
                    "Install Firebase Remote Config or disable in SorollaConfig",
                    CheckCategory.ConfigSync
                ));
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
        ///     Auto-fix config sync issues by disabling Firebase flags when SDK is not installed.
        ///     Returns true if any fixes were applied.
        /// </summary>
        public static bool FixConfigSync()
        {
            var config = Resources.Load<SorollaConfig>("SorollaConfig");
            if (config == null)
                return false;

            var manifest = ReadManifest();
            if (manifest == null)
                return false;

            var dependencies = manifest.TryGetValue("dependencies", out var deps)
                ? deps as Dictionary<string, object>
                : new Dictionary<string, object>();

            bool changed = false;

            // Fix Firebase Analytics
            var hasFirebaseAnalytics = dependencies.ContainsKey(SdkRegistry.All[SdkId.FirebaseAnalytics].PackageId);
            if (config.enableFirebaseAnalytics && !hasFirebaseAnalytics)
            {
                config.enableFirebaseAnalytics = false;
                changed = true;
                Debug.Log($"{Tag} Auto-fixed: Disabled Firebase Analytics in config (SDK not installed)");
            }

            // Fix Firebase Crashlytics
            var hasCrashlytics = dependencies.ContainsKey(SdkRegistry.All[SdkId.FirebaseCrashlytics].PackageId);
            if (config.enableCrashlytics && !hasCrashlytics)
            {
                config.enableCrashlytics = false;
                changed = true;
                Debug.Log($"{Tag} Auto-fixed: Disabled Firebase Crashlytics in config (SDK not installed)");
            }

            // Fix Firebase Remote Config
            var hasRemoteConfig = dependencies.ContainsKey(SdkRegistry.All[SdkId.FirebaseRemoteConfig].PackageId);
            if (config.enableRemoteConfig && !hasRemoteConfig)
            {
                config.enableRemoteConfig = false;
                changed = true;
                Debug.Log($"{Tag} Auto-fixed: Disabled Firebase Remote Config in config (SDK not installed)");
            }

            // Fix mode sync
            if (SorollaSettings.IsConfigured && config.isPrototypeMode != SorollaSettings.IsPrototype)
            {
                config.isPrototypeMode = SorollaSettings.IsPrototype;
                changed = true;
                Debug.Log($"{Tag} Auto-fixed: Synced config.isPrototypeMode to {SorollaSettings.IsPrototype}");
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
                        "Run Palette > Tools > Sanitize Android Manifest",
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
                    "Run Palette > Tools > Sanitize Android Manifest",
                    CheckCategory.AndroidManifest
                ));
            }

            if (!hasIssues)
                results.Add(new ValidationResult(ValidationStatus.Valid, "Manifest clean", category: CheckCategory.AndroidManifest));

            return results;
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

            // Auto-fix AndroidManifest issues before validation
            var orphanedEntries = AndroidManifestSanitizer.DetectOrphanedEntries();
            var duplicateActivities = AndroidManifestSanitizer.DetectDuplicateActivities();
            if (orphanedEntries.Count > 0 || duplicateActivities.Count > 0)
            {
                Debug.Log("[Palette BuildValidator] Detected AndroidManifest issues, auto-fixing...");
                AndroidManifestSanitizer.Sanitize();
            }

            var results = BuildValidator.RunAllChecks();
            var errors = results.Where(r => r.Status == BuildValidator.ValidationStatus.Error).ToList();

            if (errors.Count > 0)
            {
                foreach (var error in errors)
                    Debug.LogError($"[Palette BuildValidator] ERROR: {error.Message}");

                throw new BuildFailedException(
                    $"Build validation failed with {errors.Count} error(s). " +
                    "Run Palette > Tools > Validate Build for details."
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
