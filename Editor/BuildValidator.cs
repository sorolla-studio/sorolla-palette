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

    }

}
