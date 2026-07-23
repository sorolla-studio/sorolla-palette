using System;
using System.Collections.Generic;
using System.IO;
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
            FirebaseConfigAndroid,
            FirebaseConfigIos,
            GameAnalyticsSettings,
            FacebookPlatformConfig,
            VerboseLogging,
            DevelopmentBuild,
            AdjustSandboxMode,
            AndroidKeystore,
            GradleJavaHome,
            GameAnalyticsResourceWhitelist,
            AddressablesContent,
            SdkPin,
            GameAnalyticsCredentialProbe,
        }

        public enum ValidationStatus
        {
            Valid,
            Warning,
            Error,
            /// <summary>Could not verify: offline or the vendor endpoint is unreachable. Never blocks a
            /// build and never renders as a pass - re-run the check when online.</summary>
            Unverifiable,
            /// <summary>The check did not run because it does not apply here (vendor not installed,
            /// wrong platform, wrong validation profile) - never a build blocker, but NOT an affirmative
            /// pass either: renders as a neutral notice (<see cref="Greenlight.RowStatus.Info"/>),
            /// never a green check, so absence/skip can't be misread as "verified healthy" (product-audit
            /// finding F5, 2026-07-21).</summary>
            Skipped,
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
            [CheckCategory.FirebaseConfigAndroid] = "Firebase Android Config",
            [CheckCategory.FirebaseConfigIos] = "Firebase iOS Config",
            [CheckCategory.GameAnalyticsSettings] = "GameAnalytics Platform Keys",
            [CheckCategory.FacebookPlatformConfig] = "Facebook Platform",
            [CheckCategory.VerboseLogging] = "Verbose Logging",
            [CheckCategory.DevelopmentBuild] = "Development Build",
            [CheckCategory.AdjustSandboxMode] = "Adjust Sandbox Mode",
            [CheckCategory.AndroidKeystore] = "Android Keystore",
            [CheckCategory.GradleJavaHome] = "Gradle Java Home",
            [CheckCategory.GameAnalyticsResourceWhitelist] = "GameAnalytics Resource Whitelist",
            [CheckCategory.AddressablesContent] = "Addressables Content",
            [CheckCategory.SdkPin] = "SDK Pin",
            [CheckCategory.GameAnalyticsCredentialProbe] = "GameAnalytics Credentials",
        };

        static ValidationResult Valid(CheckCategory category, string message, string fix = null) =>
            new ValidationResult(ValidationStatus.Valid, message, fix, category);

        static ValidationResult Warning(CheckCategory category, string message, string fix = null) =>
            new ValidationResult(ValidationStatus.Warning, message, fix, category);

        static ValidationResult Error(CheckCategory category, string message, string fix = null) =>
            new ValidationResult(ValidationStatus.Error, message, fix, category);

        /// <summary>Offline/unreachable network check - never blocks a build, never renders as a pass.</summary>
        static ValidationResult Unverifiable(CheckCategory category, string message, string fix = null) =>
            new ValidationResult(ValidationStatus.Unverifiable, message, fix, category);

        /// <summary>Check does not apply here (vendor absent, wrong platform/profile) - a neutral notice,
        /// not an affirmative pass (F5).</summary>
        static ValidationResult Skipped(CheckCategory category, string message, string fix = null) =>
            new ValidationResult(ValidationStatus.Skipped, message, fix, category);

        // Stashed by RunSafeAutoFixes(), consumed once by RunAllChecks() to avoid double detection.
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
                    results.Add(Error(CheckCategory.VersionMismatches, "Failed to read Packages/manifest.json",
                        "Restore valid JSON in Packages/manifest.json, then click Refresh"));
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
                results.AddRange(CheckAdjustSettings(dependencies));
                results.AddRange(CheckEdm4uSettings());
                results.AddRange(CheckGradleConfig());
                results.AddRange(CheckR8AgpConfig());
                results.AddRange(CheckGameAnalyticsSettings());
                results.AddRange(CheckFacebookPlatformConfig());
                results.AddRange(CheckGameAnalyticsCredential());

                // Phase 3 (Build Health parity with the pre-build gates) - profile-scoped and
                // always-Warning-or-info checks, see BuildValidationReleaseReadiness.cs.
                results.AddRange(CheckVerboseLogging());
                results.AddRange(CheckDevelopmentBuildFlag());
                results.AddRange(CheckAdjustSandboxMode());
                results.AddRange(CheckAndroidKeystore());
                results.AddRange(CheckGradleJavaHome());
                results.AddRange(CheckGameAnalyticsResourceWhitelist());
                results.AddRange(CheckAddressablesContent(dependencies));
                results.AddRange(CheckSdkPin(dependencies));
            }
            catch (Exception e)
            {
                results.Add(Error(CheckCategory.VersionMismatches, $"Validation failed: {e.Message}",
                    "Copy Report and send this SDK validation error to Sorolla"));
            }

            return results;
        }

        /// <summary>
        ///     Run all auto-fixes before validation. Returns list of fixes applied.
        ///     This is the single source of truth for all sanitizers.
        /// </summary>
        public static List<string> RunSafeAutoFixes()
        {
            var fixes = new List<string>();

            // AndroidManifest sanitization - captures diagnostics to skip re-detection in RunAllChecks
            AndroidManifestSanitizer.ManifestDiagnostics diag = AndroidManifestSanitizer.SanitizeWithDiagnostics(refreshAssetDatabase: false);
            fixes.AddRange(diag.Fixes);
            _lastManifestDiagnostics = diag;

            // MAX SDK key - sync shared publisher key before validating AppLovin settings
            if (MaxSettingsSanitizer.SyncEmbeddedSdkKey())
                fixes.Add("Synced AppLovin MAX SDK key");

            // MAX Ad Review - auto-enable Quality Service
            if (MaxSettingsSanitizer.EnableQualityService())
                fixes.Add("Enabled AppLovin Ad Review (Quality Service)");

            // MAX Consent Flow - sync shared publisher privacy policy URL
            if (MaxSettingsSanitizer.SyncConsentFlowSettings())
                fixes.Add("Synced AppLovin consent flow settings");

            // GameAnalytics whitelist spelling: only entries that already mean a value Palette sends, only
            // rewritten to that exact value. Nothing added, nothing removed.
            fixes.AddRange(FixGameAnalyticsWhitelistSpelling());

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
                        // Remove the buildscript { ... } block using brace matching
                        string cleaned = RemoveBuildscriptBlock(baseGradle);
                        if (cleaned != baseGradle)
                        {
                            File.WriteAllText(BaseProjectTemplatePath, cleaned);
                            fixes.Add("Removed R8 version pin from baseProjectTemplate.gradle - incompatible with AGP 8.x");
                            Debug.Log($"{Tag} Removed R8 pin from baseProjectTemplate.gradle (revert via git if needed)");
                        }
                    }
                }
#endif

                // org.gradle.java.home (Unity 2022 only - Unity 6+ bundles JDK 17) is injected at BUILD
                // TIME into the generated gradle.properties by GradlePropertiesFixer, never into the
                // committed gradleTemplate.properties: writing an absolute machine-local JDK path into a
                // version-controlled file breaks every other machine (B-16).
            }

            return fixes;
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
