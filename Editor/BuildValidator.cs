using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Pre-build validation for SDK conflicts, version mismatches, and configuration issues.
    ///     Runs automatically before builds via IPreprocessBuildWithReport.
    /// </summary>
    public static partial class BuildValidator
    {
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
            Edm4uSettings,
            GradleConfig,
            FirebaseConfig,
        }

        public enum ValidationStatus
        {
            Valid,
            Warning,
            Error,
        }
        const string Tag = "[Palette BuildValidator]";

        // ── Gradle Configuration ──────────────────────────────────────────

        const int RequiredJavaVersion = 17;

        public static readonly Dictionary<CheckCategory, string> CheckNames = new Dictionary<CheckCategory, string>
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
            [CheckCategory.Edm4uSettings] = "EDM4U Settings",
            [CheckCategory.GradleConfig] = "Gradle Configuration",
            [CheckCategory.FirebaseConfig] = "Firebase Config Files",
        };

        static ValidationResult Valid(CheckCategory category, string message) =>
            new ValidationResult(ValidationStatus.Valid, message, category: category);

        static ValidationResult Warning(CheckCategory category, string message, string fix = null) =>
            new ValidationResult(ValidationStatus.Warning, message, fix, category);

        static ValidationResult Error(CheckCategory category, string message, string fix = null) =>
            new ValidationResult(ValidationStatus.Error, message, fix, category);

        // Stashed by RunAutoFixes(), consumed once by RunAllChecks() to avoid double detection.
        static AndroidManifestSanitizer.ManifestDiagnostics _lastManifestDiagnostics;
        static readonly string MainTemplatePath =
            Path.Combine(Application.dataPath, "Plugins", "Android", "mainTemplate.gradle");
        static readonly string LauncherTemplatePath =
            Path.Combine(Application.dataPath, "Plugins", "Android", "launcherTemplate.gradle");
        static readonly string GradlePropertiesPath =
            Path.Combine(Application.dataPath, "Plugins", "Android", "gradleTemplate.properties");
        static readonly string BaseProjectTemplatePath =
            Path.Combine(Application.dataPath, "Plugins", "Android", "baseProjectTemplate.gradle");
        static readonly string[] GradleTemplatePaths = { MainTemplatePath, LauncherTemplatePath };
        static string ManifestPath => Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");

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
                    results.Add(Error(CheckCategory.VersionMismatches, "Failed to read manifest.json"));
                    return results;
                }

                var dependencies = manifest.TryGetValue("dependencies", out object deps)
                    ? deps as Dictionary<string, object>
                    : new Dictionary<string, object>();

                var registries = manifest.TryGetValue("scopedRegistries", out object regs)
                    ? regs as List<object>
                    : new List<object>();

                // Run all checks
                results.AddRange(CheckRequiredSdks());
                results.AddRange(CheckVersionMismatches(dependencies));
                results.AddRange(CheckModeConsistency(dependencies));
                results.AddRange(CheckScopedRegistries(dependencies, registries));
                results.AddRange(CheckFirebaseCoherence(dependencies));
                results.AddRange(CheckFirebaseConfigFiles(dependencies));
                results.AddRange(CheckConfigSync(dependencies));
                AndroidManifestSanitizer.ManifestDiagnostics manifestDiag = _lastManifestDiagnostics;
                _lastManifestDiagnostics = null;
                results.AddRange(CheckAndroidManifest(manifestDiag));
                results.AddRange(CheckMaxSettings());
                results.AddRange(CheckAdjustSettings());
                results.AddRange(CheckEdm4uSettings());
                results.AddRange(CheckGradleConfig());
                results.AddRange(CheckR8AgpConfig());
            }
            catch (Exception e)
            {
                results.Add(Error(CheckCategory.VersionMismatches, $"Validation failed: {e.Message}"));
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

            // AndroidManifest sanitization - captures diagnostics to skip re-detection in RunAllChecks
            AndroidManifestSanitizer.ManifestDiagnostics diag = AndroidManifestSanitizer.SanitizeWithDiagnostics(refreshAssetDatabase: false);
            fixes.AddRange(diag.Fixes);
            _lastManifestDiagnostics = diag;

            // MAX Ad Review - auto-enable Quality Service
            if (MaxSettingsSanitizer.EnableQualityService())
                fixes.Add("Enabled AppLovin Ad Review (Quality Service)");

            // MAX Consent Flow - auto-set privacy policy URL
            if (MaxSettingsSanitizer.SetConsentFlowPrivacyPolicy())
                fixes.Add("Set AppLovin consent flow privacy policy URL");

            // Gradle config auto-fixes (Android only)
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                // compileOptions: Java 11 → 17 (both mainTemplate and launcherTemplate)
                foreach (string templatePath in GradleTemplatePaths)
                {
                    if (!File.Exists(templatePath)) continue;
                    string gradle = File.ReadAllText(templatePath);
                    if (gradle.Contains("VERSION_11") && (gradle.Contains("sourceCompatibility") || gradle.Contains("targetCompatibility")))
                    {
                        gradle = gradle.Replace("VERSION_11", "VERSION_17");
                        File.WriteAllText(templatePath, gradle);
                        fixes.Add($"Upgraded {Path.GetFileName(templatePath)} compileOptions: Java 11 → 17 (required by Firebase/MAX/Kotlin)");
                    }
                }

                // R8 pin removal (Unity 6 only - AGP 8.x bundles modern R8, pin causes NoSuchMethodError)
#if UNITY_6000_0_OR_NEWER
                if (File.Exists(BaseProjectTemplatePath))
                {
                    string baseGradle = File.ReadAllText(BaseProjectTemplatePath);
                    if (baseGradle.Contains("com.android.tools:r8"))
                    {
                        string backupPath = BaseProjectTemplatePath + ".backup";
                        File.Copy(BaseProjectTemplatePath, backupPath, true);

                        // Remove the buildscript { ... } block using brace matching
                        string cleaned = RemoveBuildscriptBlock(baseGradle);
                        if (cleaned != baseGradle)
                        {
                            File.WriteAllText(BaseProjectTemplatePath, cleaned);
                            fixes.Add("Removed R8 version pin from baseProjectTemplate.gradle - incompatible with AGP 8.x (backup created)");
                            Debug.Log($"{Tag} Removed R8 pin from baseProjectTemplate.gradle (backup at {backupPath})");
                        }
                    }
                }
#endif

                // Auto-detect JDK 17+ and write org.gradle.java.home (Unity 2022 only - Unity 6+ bundles JDK 17)
#if !UNITY_6000_0_OR_NEWER
                if (File.Exists(GradlePropertiesPath))
                {
                    var props = File.ReadAllText(GradlePropertiesPath);
                    if (!props.Contains("org.gradle.java.home"))
                    {
                        var jdkPath = GradleJdkDetector.FindJdk17OrNewer();
                        if (jdkPath != null)
                        {
                            File.AppendAllText(GradlePropertiesPath, $"\norg.gradle.java.home={jdkPath}\n");
                            fixes.Add($"Set org.gradle.java.home={jdkPath} (Unity {Application.unityVersion} bundles JDK 11, need 17+)");
                        }
                    }
                }
#endif
            }

            return fixes;
        }

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
                    "No SDK mode configured. Run Palette > Configuration to select Prototype or Full mode."));
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
                        "Run Palette > Configuration to fix registry configuration"));
                }
            }

            if (!hasIssues)
                results.Add(Valid(CheckCategory.ScopedRegistries, "All registries configured"));

            return results;
        }

        /// <summary>
        ///     Check Firebase module coherence - FirebaseApp required if other modules installed
        /// </summary>
        static List<ValidationResult> CheckFirebaseCoherence(Dictionary<string, object> dependencies)
        {
            var results = new List<ValidationResult>();

            var firebaseModules = new[]
            {
                SdkId.FirebaseAnalytics,
                SdkId.FirebaseCrashlytics,
                SdkId.FirebaseRemoteConfig,
            };

            bool hasFirebaseApp = dependencies.ContainsKey(SdkRegistry.All[SdkId.FirebaseApp].PackageId);
            var installedModules = firebaseModules
                .Where(id => dependencies.ContainsKey(SdkRegistry.All[id].PackageId))
                .Select(id => SdkRegistry.All[id].Name)
                .ToList();

            if (installedModules.Count > 0 && !hasFirebaseApp)
            {
                results.Add(Error(
                    CheckCategory.FirebaseCoherence,
                    $"Firebase modules installed without FirebaseApp:\n  {string.Join(", ", installedModules)}",
                    "Install com.google.firebase.app or remove Firebase modules"));
            }
            else if (installedModules.Count > 0)
            {
                results.Add(Valid(CheckCategory.FirebaseCoherence, "Firebase modules OK"));
            }
            else if (!SorollaSettings.IsPrototype)
            {
                // Firebase missing in Full mode — warn
                results.Add(Warning(
                    CheckCategory.FirebaseCoherence,
                    "Firebase not installed (required in Full mode)",
                    "Run setup or open Palette > Configuration to install Firebase."));
            }
            else
            {
                // Firebase missing in Prototype mode — silently valid (optional)
                results.Add(Valid(CheckCategory.FirebaseCoherence, "Firebase not installed (optional in Prototype)"));
            }

            return results;
        }

        /// <summary>
        ///     Check Firebase config files (google-services.json / GoogleService-Info.plist) for active build target.
        /// </summary>
        static List<ValidationResult> CheckFirebaseConfigFiles(Dictionary<string, object> dependencies)
        {
            var results = new List<ValidationResult>();
            const CheckCategory category = CheckCategory.FirebaseConfig;

            bool hasFirebase = dependencies.ContainsKey(SdkRegistry.All[SdkId.FirebaseAnalytics].PackageId);
            if (!hasFirebase)
            {
                results.Add(Valid(category, "Firebase not installed, config check skipped"));
                return results;
            }

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;

            if (target == BuildTarget.Android && !SdkConfigDetector.IsFirebaseAndroidConfigured())
            {
                results.Add(Error(
                    category,
                    "google-services.json not found",
                    "Download from Firebase Console > Project Settings > Android app and place in Assets/"));
            }
            else if (target == BuildTarget.iOS && !SdkConfigDetector.IsFirebaseIOSConfigured())
            {
                results.Add(Error(
                    category,
                    "GoogleService-Info.plist not found",
                    "Download from Firebase Console > Project Settings > iOS app and place in Assets/"));
            }
            else
            {
                results.Add(Valid(category, "Firebase config files present"));
            }

            return results;
        }

        /// <summary>
        ///     Check that SorollaConfig settings match installed SDKs
        /// </summary>
        static List<ValidationResult> CheckConfigSync(Dictionary<string, object> dependencies)
        {
            var results = new List<ValidationResult>();
            bool hasIssues = false;

            var config = Resources.Load<SorollaConfig>("SorollaConfig");
            if (config == null)
            {
                results.Add(Warning(
                    CheckCategory.ConfigSync,
                    "SorollaConfig not found in Resources folder",
                    "Create config via Assets > Create > Palette > Config"));
                return results;
            }

            // Check mode sync
            if (SorollaSettings.IsConfigured && config.isPrototypeMode != SorollaSettings.IsPrototype)
            {
                hasIssues = true;
                results.Add(Warning(
                    CheckCategory.ConfigSync,
                    $"Config mode mismatch - SorollaConfig.isPrototypeMode={config.isPrototypeMode}, " +
                    $"SorollaSettings.Mode={SorollaSettings.Mode}",
                    "Run Palette > Configuration to sync mode settings"));
            }

            if (!hasIssues)
                results.Add(Valid(CheckCategory.ConfigSync, "Config synced"));

            return results;
        }

        /// <summary>
        ///     Auto-fix config sync issues and install missing required SDKs.
        ///     Always installs missing SDKs - the user should never be in a state
        ///     where required SDKs are missing for the configured mode.
        /// </summary>
        public static bool FixConfigSync()
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

            // Auto-install missing required SDKs
            if (SorollaSettings.IsConfigured && !SdkDetector.AreAllRequiredInstalled(SorollaSettings.IsPrototype))
            {
                string modeName = SorollaSettings.IsPrototype ? "Prototype" : "Full";
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
        /// <summary>
        ///     Check Android manifest health. Uses pre-computed diagnostics from Sanitize
        ///     when available (after RunAutoFixes), falls back to fresh detection.
        /// </summary>
        static List<ValidationResult> CheckAndroidManifest(
            AndroidManifestSanitizer.ManifestDiagnostics diagnostics = null)
        {
            var results = new List<ValidationResult>();
            bool hasIssues = false;

            // Use pre-computed diagnostics from SanitizeWithDiagnostics if available
            var orphaned = diagnostics?.RemainingOrphans ?? AndroidManifestSanitizer.DetectOrphanedEntries();
            var duplicates = diagnostics?.RemainingDuplicates ?? AndroidManifestSanitizer.DetectDuplicateActivities();
            string wrongActivity = diagnostics?.RemainingWrongActivity ?? AndroidManifestSanitizer.DetectWrongMainActivity();
            string themeMismatch = diagnostics?.RemainingThemeMismatch ?? AndroidManifestSanitizer.DetectThemeMismatch();
            string launcherIssue = diagnostics?.RemainingLauncherIssue ?? AndroidManifestSanitizer.DetectLauncherManifestIssue();

            if (orphaned.Count > 0)
            {
                hasIssues = true;
                foreach ((SdkId sdkId, string[] entries) in orphaned)
                {
                    string sdkName = SdkRegistry.All[sdkId].Name;
                    results.Add(Error(
                        CheckCategory.AndroidManifest,
                        $"AndroidManifest.xml has {sdkName} entries but SDK is not installed!\n" +
                        $"  Found patterns: {string.Join(", ", entries)}\n" +
                        "  This WILL crash at runtime.",
                        "Open Palette > Configuration and click Refresh in Build Health"));
                }
            }

            if (duplicates.Count > 0)
            {
                hasIssues = true;
                results.Add(Error(
                    CheckCategory.AndroidManifest,
                    "AndroidManifest.xml has duplicate activity declarations!\n" +
                    $"  Duplicates: {string.Join(", ", duplicates)}\n" +
                    "  This WILL cause build failures.",
                    "Open Palette > Configuration and click Refresh in Build Health"));
            }

            if (wrongActivity != null)
            {
                hasIssues = true;
                results.Add(Error(
                    CheckCategory.AndroidManifest,
                    "AndroidManifest.xml has wrong main activity!\n" +
                    $"  Found: {wrongActivity}\n" +
                    $"  Expected: {AndroidManifestSanitizer.GetExpectedMainActivity()}\n" +
                    "  The app WILL crash on launch.",
                    "Open Palette > Configuration and click Refresh in Build Health"));
            }

            if (themeMismatch != null)
            {
                hasIssues = true;
                results.Add(Error(
                    CheckCategory.AndroidManifest,
                    "AndroidManifest.xml activity theme issue!\n" +
                    $"  {themeMismatch}\n" +
                    "  This WILL cause a Gradle merge conflict on build.",
                    "Open Palette > Configuration and click Refresh in Build Health"));
            }

            if (launcherIssue != null)
            {
                hasIssues = true;
                results.Add(Error(
                    CheckCategory.AndroidManifest,
                    $"LauncherManifest.xml issue: {launcherIssue}\n" +
                    "  The app will install but fail to launch.",
                    "Open Palette > Configuration and click Refresh in Build Health"));
            }

            if (!hasIssues)
                results.Add(Valid(CheckCategory.AndroidManifest, "Manifest clean"));

            return results;
        }

        /// <summary>
        ///     Check AppLovin MAX settings for known issues
        /// </summary>
        static List<ValidationResult> CheckMaxSettings()
        {
            var results = new List<ValidationResult>();

#if SOROLLA_MAX_INSTALLED
            bool hasIssues = false;

            var config = Resources.Load<SorollaConfig>("SorollaConfig");
            if (config == null)
            {
                results.Add(Warning(
                    CheckCategory.MaxSettings,
                    "SorollaConfig not found - cannot validate MAX SDK key",
                    "Create config via Assets > Create > Palette > Config"));
                return results;
            }

            // Check SDK key is configured in SorollaConfig (single source of truth)
            SdkConfigDetector.ConfigStatus maxStatus = SdkConfigDetector.GetMaxStatus(config);
            if (maxStatus == SdkConfigDetector.ConfigStatus.NotConfigured)
            {
                hasIssues = true;
                results.Add(Error(
                    CheckCategory.MaxSettings,
                    "AppLovin SDK key is not configured!\n" +
                    "  SDK key must be set in Palette Configuration.\n" +
                    "  Ads will not work without a valid SDK key.",
                    "Open Palette > Configuration and enter MAX SDK key"));
            }

            if (!hasIssues)
                results.Add(Valid(CheckCategory.MaxSettings, "MAX SDK key OK"));
#else
            results.Add(Valid(CheckCategory.MaxSettings, "MAX not installed"));
#endif

            return results;
        }

        /// <summary>
        ///     Check Adjust SDK app token configuration (Full mode only).
        ///     Note: SDK installation is checked by CheckRequiredSdks().
        /// </summary>
        static List<ValidationResult> CheckAdjustSettings()
        {
            var results = new List<ValidationResult>();

            // Only check in Full mode when Adjust is installed
            if (!SorollaSettings.IsConfigured || SorollaSettings.IsPrototype)
            {
                results.Add(Valid(CheckCategory.AdjustSettings, "Adjust not required"));
                return results;
            }

            if (!SdkDetector.IsInstalled(SdkId.Adjust))
            {
                // Installation is checked by CheckRequiredSdks - just skip config check here
                results.Add(Valid(CheckCategory.AdjustSettings, "Adjust not installed"));
                return results;
            }

            var config = Resources.Load<SorollaConfig>("SorollaConfig");
            if (config == null)
            {
                results.Add(Warning(
                    CheckCategory.AdjustSettings,
                    "SorollaConfig not found - cannot validate Adjust app token",
                    "Create config via Assets > Create > Palette > Config"));
                return results;
            }

            SdkConfigDetector.ConfigStatus adjustStatus = SdkConfigDetector.GetAdjustStatus(config);
            if (adjustStatus == SdkConfigDetector.ConfigStatus.NotConfigured)
            {
                results.Add(Error(
                    CheckCategory.AdjustSettings,
                    "Adjust app token is not configured!\n" +
                    "  Attribution tracking will not work without a valid app token.\n" +
                    "  Enter your Adjust app token in Palette > Configuration.",
                    "Open Palette > Configuration and enter Adjust app token"));
            }
            else
            {
                results.Add(Valid(CheckCategory.AdjustSettings, "Adjust app token OK"));
            }

            return results;
        }

        /// <summary>
        ///     Check for duplicate EDM4U installations and Gradle template mode configuration.
        /// </summary>
        static List<ValidationResult> CheckEdm4uSettings()
        {
            var results = new List<ValidationResult>();
            bool hasIssues = false;

            // Check for duplicate installations
            var duplicates = Edm4uSanitizer.DetectDuplicateInstallations();
            if (duplicates.Count > 0)
            {
                hasIssues = true;
                results.AddRange(duplicates.Select(dup => Warning(
                    CheckCategory.Edm4uSettings,
                    dup,
                    "Remove duplicate EDM4U from Assets/ folder")));
            }

            // Check Gradle template mode (prevents Java 17+ compatibility issues)
            ValidationResult gradleCheck = CheckEdm4uGradleMode();
            if (gradleCheck != null)
            {
                hasIssues = true;
                results.Add(gradleCheck);
            }

            if (!hasIssues)
                results.Add(Valid(CheckCategory.Edm4uSettings, "EDM4U settings OK"));

            return results;
        }

        /// <summary>
        ///     Check that EDM4U is configured for Gradle template mode.
        ///     Without this, EDM4U uses its bundled Gradle 5.1.1 which is incompatible with Java 17+ (Unity 6+).
        /// </summary>
        static ValidationResult CheckEdm4uGradleMode()
        {
            // Find EDM4U's SettingsDialog type via reflection
            Type settingsType = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
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
                PropertyInfo mainTemplateProp = settingsType.GetProperty("PatchMainTemplateGradle", staticFlags);

                if (mainTemplateProp != null && !(bool)mainTemplateProp.GetValue(null))
                {
                    return Warning(
                        CheckCategory.Edm4uSettings,
                        "EDM4U not configured for Gradle templates.\n" +
                        "  This causes Java 17+ compatibility errors on Android resolve.\n" +
                        "  Unity 6+ requires Gradle template mode.",
                        "Run Palette > Run Setup (Force)");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} Could not check EDM4U Gradle mode: {e.Message}");
            }

            return null;
        }

        /// <summary>
        ///     Validate Gradle configuration for Java 17 compatibility.
        ///     Firebase 23.x, AppLovin MAX 13.x, and Kotlin 2.x all require Java 17 bytecode.
        ///     Unity 2022 bundles JDK 11 — Gradle must be pointed at JDK 17+ or dexing fails.
        /// </summary>
        static List<ValidationResult> CheckGradleConfig()
        {
            var results = new List<ValidationResult>();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                results.Add(Valid(CheckCategory.GradleConfig, "Gradle checks skipped (not Android)"));
                return results;
            }

            bool hasIssues = false;

            // Check compileOptions in both Gradle templates
            foreach (string templatePath in GradleTemplatePaths)
            {
                if (!File.Exists(templatePath)) continue;
                string gradle = File.ReadAllText(templatePath);
                if (HasJava11CompileOptions(gradle))
                {
                    hasIssues = true;
                    string fileName = Path.GetFileName(templatePath);
                    results.Add(Error(
                        CheckCategory.GradleConfig,
                        $"{fileName} has Java 11 compileOptions!\n" +
                        $"  Firebase 23.x, AppLovin MAX 13.x, and Kotlin 2.x require Java {RequiredJavaVersion}.\n" +
                        $"  Change sourceCompatibility and targetCompatibility to VERSION_{RequiredJavaVersion}.",
                        "Open Palette > Configuration and click Refresh in Build Health"));
                }
            }

            // Check org.gradle.java.home in gradleTemplate.properties
            if (File.Exists(GradlePropertiesPath))
            {
                string props = File.ReadAllText(GradlePropertiesPath);
                if (MissingGradleJavaHome(props))
                {
                    // Unity 2022 bundles JDK 11 — need explicit JDK 17+ override
#if !UNITY_6000_0_OR_NEWER
                    hasIssues = true;
                    results.Add(Error(
                        CheckCategory.GradleConfig,
                        "gradleTemplate.properties missing org.gradle.java.home!\n" +
                        $"  Unity {Application.unityVersion} bundles JDK 11 which cannot dex Java {RequiredJavaVersion} bytecode.\n" +
                        $"  Add org.gradle.java.home pointing to a JDK {RequiredJavaVersion}+ installation.\n" +
                        "  Without this, ALL Firebase/MAX/Kotlin deps will fail to dex.",
                        "Install JDK 17 and add org.gradle.java.home to gradleTemplate.properties"));
#endif
                }
            }
            else
            {
                hasIssues = true;
                results.Add(Warning(
                    CheckCategory.GradleConfig,
                    "gradleTemplate.properties not found.\n" +
                    "  Enable Custom Gradle Properties Template in Player Settings > Publishing Settings.",
                    "Enable Custom Gradle Properties Template"));
            }

            if (!hasIssues)
                results.Add(Valid(CheckCategory.GradleConfig, "Gradle config OK"));

            return results;
        }

        internal static bool HasJava11CompileOptions(string gradle) =>
            !string.IsNullOrEmpty(gradle) &&
            gradle.Contains("VERSION_11") &&
            (gradle.Contains("sourceCompatibility") || gradle.Contains("targetCompatibility"));

        internal static bool MissingGradleJavaHome(string gradleProperties) =>
            string.IsNullOrEmpty(gradleProperties) ||
            !gradleProperties.Contains("org.gradle.java.home");

        /// <summary>
        ///     Remove the buildscript { ... } block from a Gradle file using brace matching.
        ///     Returns the original string if no buildscript block is found.
        /// </summary>
        internal static string RemoveBuildscriptBlock(string gradle)
        {
            int idx = gradle.IndexOf("buildscript", StringComparison.Ordinal);
            if (idx < 0) return gradle;

            // Find opening brace
            int braceStart = gradle.IndexOf('{', idx);
            if (braceStart < 0) return gradle;

            // Match closing brace
            int depth = 1;
            int pos = braceStart + 1;
            while (pos < gradle.Length && depth > 0)
            {
                if (gradle[pos] == '{') depth++;
                else if (gradle[pos] == '}') depth--;
                pos++;
            }

            if (depth != 0) return gradle; // Unbalanced braces, don't touch

            // Include any trailing newlines
            while (pos < gradle.Length && (gradle[pos] == '\n' || gradle[pos] == '\r'))
            {
                pos++;
            }

            // Include any leading whitespace/newlines before "buildscript"
            int blockStart = idx;
            while (blockStart > 0 && (gradle[blockStart - 1] == ' ' || gradle[blockStart - 1] == '\t' ||
                                      gradle[blockStart - 1] == '\n' || gradle[blockStart - 1] == '\r'))
            {
                blockStart--;
            }

            return gradle.Substring(0, blockStart) + gradle.Substring(pos);
        }

        // ── R8 / AGP Compatibility ────────────────────────────────────────

        /// <summary>
        ///     Check for R8 version pins and Kotlin stdlib forcing that conflict with the current AGP version.
        ///     Unity 2022 (AGP 7.4.2) needs R8 8.1.56+ pin for Kotlin 2.0 metadata.
        ///     Unity 6 (AGP 8.x) bundles modern R8 - the pin must be removed.
        /// </summary>
        static List<ValidationResult> CheckR8AgpConfig()
        {
            var results = new List<ValidationResult>();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                results.Add(Valid(CheckCategory.GradleConfig, "R8/AGP checks skipped (not Android)"));
                return results;
            }

            bool hasIssues = false;

            // Check R8 pin in baseProjectTemplate.gradle
            if (File.Exists(BaseProjectTemplatePath))
            {
                string gradle = File.ReadAllText(BaseProjectTemplatePath);
                if (HasR8Pin(gradle))
                {
#if UNITY_6000_0_OR_NEWER
                    hasIssues = true;
                    results.Add(Error(
                        CheckCategory.GradleConfig,
                        "baseProjectTemplate.gradle has an R8 version pin!\n" +
                        "  AGP 8.x bundles modern R8 that handles Kotlin 2.0 natively.\n" +
                        "  The pin causes NoSuchMethodError during dexing.\n" +
                        "  Remove the buildscript { ... } block from baseProjectTemplate.gradle.",
                        "Open Palette > Configuration and click Refresh in Build Health"));
#endif
                    // Unity 2022: R8 pin is expected and correct - no warning needed
                }
            }

            // Check Kotlin stdlib version forcing in mainTemplate.gradle
            if (File.Exists(MainTemplatePath))
            {
                string gradle = File.ReadAllText(MainTemplatePath);
                if (ForcesKotlinStdlibVersion(gradle))
                {
#if UNITY_6000_0_OR_NEWER
                    hasIssues = true;
                    results.Add(Warning(
                        CheckCategory.GradleConfig,
                        "mainTemplate.gradle forces Kotlin stdlib to an older version.\n" +
                        "  AGP 8.x handles Kotlin 2.0 metadata natively.\n" +
                        "  Consider removing the resolutionStrategy block.",
                        "Remove the resolutionStrategy.eachDependency block for kotlin-stdlib"));
#endif
                    // Unity 2022: Kotlin forcing is expected and correct - no warning needed
                }
            }

            if (!hasIssues)
                results.Add(Valid(CheckCategory.GradleConfig, "R8/AGP config OK"));

            return results;
        }

        internal static bool HasR8Pin(string gradle) =>
            !string.IsNullOrEmpty(gradle) &&
            gradle.Contains("com.android.tools:r8");

        internal static bool ForcesKotlinStdlibVersion(string gradle) =>
            !string.IsNullOrEmpty(gradle) &&
            gradle.Contains("kotlin-stdlib") &&
            gradle.Contains("useVersion");

        /// <summary>
        ///     Read and parse manifest.json
        /// </summary>
        static Dictionary<string, object> ReadManifest()
        {
            if (!File.Exists(ManifestPath))
            {
                Debug.LogError($"{Tag} manifest.json not found at: {ManifestPath}");
                return null;
            }

            try
            {
                string json = File.ReadAllText(ManifestPath);
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
        static void DisplayResults(List<ValidationResult> results)
        {
            int errors = results.Count(r => r.Status == ValidationStatus.Error);
            int warnings = results.Count(r => r.Status == ValidationStatus.Warning);

            // Log each result
            foreach (ValidationResult result in results)
            {
                string fixText = string.IsNullOrEmpty(result.Fix) ? "" : $"\n  Fix: {result.Fix}";

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
                string summary = $"Validation complete: {errors} error(s), {warnings} warning(s)";
                Debug.Log($"{Tag} {summary}");

                EditorUtility.DisplayDialog(
                    "Build Validation",
                    $"{summary}\n\nCheck Console for details.",
                    "OK"
                );
            }
        }

    }

}
