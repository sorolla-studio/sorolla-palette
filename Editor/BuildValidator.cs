using System;
using System.Collections.Generic;
using System.IO;
using Sorolla.Palette.Health;
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
            /// pass either: renders as a neutral notice (a neutral informational row),
            /// never a green check, so absence/skip can't be misread as "verified healthy" (product-audit
            /// finding F5, 2026-07-21).</summary>
            Skipped,
        }
        const string Tag = "[Palette BuildValidator]";

        // ── Gradle Configuration ──────────────────────────────────────────

        const int RequiredJavaVersion = 17;

        static ValidationResult Valid(ReadinessCheck check, string message, string fix = null) =>
            new ValidationResult(ValidationStatus.Valid, message, fix, check);

        static ValidationResult Warning(ReadinessCheck check, string message, string fix = null) =>
            new ValidationResult(ValidationStatus.Warning, message, fix, check);

        static ValidationResult Error(ReadinessCheck check, string message, string fix = null) =>
            new ValidationResult(ValidationStatus.Error, message, fix, check);

        /// <summary>Offline/unreachable network check - never blocks a build, never renders as a pass.</summary>
        static ValidationResult Unverifiable(ReadinessCheck check, string message, string fix = null) =>
            new ValidationResult(ValidationStatus.Unverifiable, message, fix, check);

        /// <summary>Check does not apply here (vendor absent, wrong platform/profile) - a neutral notice,
        /// not an affirmative pass (F5).</summary>
        static ValidationResult Skipped(ReadinessCheck check, string message, string fix = null) =>
            new ValidationResult(ValidationStatus.Skipped, message, fix, check);

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

            var manifest = ReadManifest();
            if (manifest == null)
            {
                results.Add(Error(ReadinessChecks.VersionMismatches,
                    "Packages/manifest.json could not be read as JSON, so no package fact could be checked.",
                    "Open Packages/manifest.json, restore valid JSON (git checkout the file if it is unedited), then Refresh"));
                return results;
            }

            var dependencies = manifest.TryGetValue("dependencies", out object deps)
                ? deps as Dictionary<string, object>
                : new Dictionary<string, object>();

            var registries = manifest.TryGetValue("scopedRegistries", out object regs)
                ? regs as List<object>
                : new List<object>();

            AndroidManifestSanitizer.ManifestDiagnostics manifestDiag = _lastManifestDiagnostics;
            _lastManifestDiagnostics = null;

            Run(results, ReadinessChecks.RequiredSdks, CheckRequiredSdks);
            Run(results, ReadinessChecks.VersionMismatches, r => CheckVersionMismatches(r, dependencies));
            Run(results, ReadinessChecks.ModeConsistency, r => CheckModeConsistency(r, dependencies));
            Run(results, ReadinessChecks.ScopedRegistries, r => CheckScopedRegistries(r, dependencies, registries));
            Run(results, ReadinessChecks.FirebaseCoherence, r => CheckFirebaseCoherence(r, dependencies));
            // Off-mobile both Firebase config gates resolve NotApplicable, so there is no row to attribute a
            // failure to - and nothing to check either.
            ReadinessCheck firebaseConfigGate = FirebaseConfigGate();
            if (firebaseConfigGate != null)
                Run(results, firebaseConfigGate, r => CheckFirebaseConfigFiles(r, dependencies));
            Run(results, ReadinessChecks.ConfigSync, r => CheckConfigSync(r, dependencies));
            Run(results, ReadinessChecks.AndroidManifest, r => CheckAndroidManifest(r, manifestDiag));
            Run(results, ReadinessChecks.MaxSettings, CheckMaxSettings);
            Run(results, ReadinessChecks.AdjustSettings, r => CheckAdjustSettings(r, dependencies));
            Run(results, ReadinessChecks.Edm4uSettings, CheckEdm4uSettings);
            Run(results, ReadinessChecks.GradleConfig, CheckGradleConfig, "Java + templates");
            Run(results, ReadinessChecks.GradleConfig, CheckR8AgpConfig, "R8 + AGP");
            Run(results, ReadinessChecks.GameAnalyticsSettings, CheckGameAnalyticsSettings);
            Run(results, ReadinessChecks.FacebookPlatformConfig, CheckFacebookPlatformConfig);
            Run(results, ReadinessChecks.GameAnalyticsCredentialProbe, CheckGameAnalyticsCredential);

            // Phase 3 (Build Health parity with the pre-build gates) - profile-scoped and
            // always-Warning-or-info checks, see BuildValidationReleaseReadiness.cs.
            Run(results, ReadinessChecks.VerboseLogging, CheckVerboseLogging);
            Run(results, ReadinessChecks.DevelopmentBuild, CheckDevelopmentBuildFlag);
            Run(results, ReadinessChecks.AdjustSandboxMode, CheckAdjustSandboxMode);
            Run(results, ReadinessChecks.AndroidKeystore, CheckAndroidKeystore);
            Run(results, ReadinessChecks.GradleJavaHome, CheckGradleJavaHome);
            Run(results, ReadinessChecks.GameAnalyticsResourceWhitelist, CheckGameAnalyticsResourceWhitelist);
            Run(results, ReadinessChecks.AddressablesContent, r => CheckAddressablesContent(r, dependencies));
            Run(results, ReadinessChecks.SdkPin, r => CheckSdkPin(r, dependencies));

            return results;
        }

        /// <summary>
        ///     Runs one check in isolation. A check that throws names ITSELF, reports Unverifiable (the
        ///     evaluation failed - that is not evidence the integration is broken, so it must not fail a
        ///     build), and every check after it still runs. One shared try/catch used to abandon the whole
        ///     pass at the first throw and blame the SDK-versions row for it.
        ///
        ///     Checks append into the shared list as they go rather than returning one at the end, so a
        ///     throw halfway through keeps everything that check already PROVED - a Gradle Java-11 Error
        ///     found before the throw still fails the report instead of vanishing with the exception.
        ///
        ///     <paramref name="part" /> distinguishes several checks that report against the same row, so
        ///     two thrown findings on that row are not indistinguishable.
        /// </summary>
        internal static void Run(List<ValidationResult> results, ReadinessCheck attribution,
            Action<List<ValidationResult>> check, string part = null)
        {
            string name = part == null ? attribution.Label : $"{attribution.Label} ({part})";
            try
            {
                check(results);
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} {attribution.Id}{(part == null ? "" : $" [{part}]")} threw: {e}");
                results.Add(Unverifiable(attribution,
                    $"The {name} check failed to run: {e.Message}\n" +
                    "  Evaluation failed here; nothing about the integration itself was proven either way.",
                    ReadinessEvaluator.NoResultAction));
            }
        }

        /// <summary>
        ///     Run all auto-fixes before validation. Returns list of fixes applied.
        ///     This is the single source of truth for all sanitizers.
        /// </summary>
        public static List<string> RunSafeAutoFixes()
        {
            var fixes = new List<string>();

            // AndroidManifest sanitization - captures diagnostics to skip re-detection in RunAllChecks
            Repair("AndroidManifest sanitizer", () =>
            {
                AndroidManifestSanitizer.ManifestDiagnostics diag = AndroidManifestSanitizer.SanitizeWithDiagnostics(refreshAssetDatabase: false);
                fixes.AddRange(diag.Fixes);
                _lastManifestDiagnostics = diag;
            });

            // MAX SDK key - sync shared publisher key before validating AppLovin settings
            Repair("AppLovin MAX SDK key sync", () =>
            {
                if (MaxSettingsSanitizer.SyncEmbeddedSdkKey())
                    fixes.Add("Synced AppLovin MAX SDK key");
            });

            // MAX Ad Review - auto-enable Quality Service
            Repair("AppLovin Ad Review", () =>
            {
                if (MaxSettingsSanitizer.EnableQualityService())
                    fixes.Add("Enabled AppLovin Ad Review (Quality Service)");
            });

            // AppLovin's consent flow stays off: Palette runs Google UMP and ATT itself.
            Repair("AppLovin consent flow off", () =>
            {
                if (MaxSettingsSanitizer.DisableConsentFlow())
                    fixes.Add("Turned off AppLovin's consent flow");
            });

            // GameAnalytics whitelist spelling: only entries that already mean a value Palette sends, only
            // rewritten to that exact value. Nothing added, nothing removed.
            Repair("GameAnalytics whitelist spelling",
                () => fixes.AddRange(FixGameAnalyticsWhitelistSpelling()));

            // GameAnalytics item types: written in full where currencies are configured and item types are
            // empty. Runs after the spelling pass, which is a no-op on values written straight from the
            // vocabulary.
            Repair("GameAnalytics item types", () => fixes.AddRange(FillGameAnalyticsItemTypes()));

            // Gradle config auto-fixes (Android only)
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                // compileOptions: Java 11 → 17 (both mainTemplate and launcherTemplate)
                foreach (string templatePath in GradleTemplatePaths)
                {
                    Repair($"{Path.GetFileName(templatePath)} compileOptions", () =>
                    {
                        if (!File.Exists(templatePath)) return;
                        string gradle = File.ReadAllText(templatePath);
                        if (gradle.Contains("VERSION_11") && (gradle.Contains("sourceCompatibility") || gradle.Contains("targetCompatibility")))
                        {
                            gradle = gradle.Replace("VERSION_11", "VERSION_17");
                            File.WriteAllText(templatePath, gradle);
                            fixes.Add($"Upgraded {Path.GetFileName(templatePath)} compileOptions: Java 11 → 17 (required by Firebase/MAX/Kotlin)");
                        }
                    });
                }

                // R8 pin removal (Unity 6 only - AGP 8.x bundles modern R8, pin causes NoSuchMethodError)
#if UNITY_6000_0_OR_NEWER
                Repair("baseProjectTemplate.gradle R8 pin", () =>
                {
                    if (!File.Exists(BaseProjectTemplatePath)) return;
                    string baseGradle = File.ReadAllText(BaseProjectTemplatePath);
                    if (!baseGradle.Contains("com.android.tools:r8")) return;

                    // Remove the buildscript { ... } block using brace matching
                    string cleaned = RemoveBuildscriptBlock(baseGradle);
                    if (cleaned == baseGradle) return;

                    File.WriteAllText(BaseProjectTemplatePath, cleaned);
                    fixes.Add("Removed R8 version pin from baseProjectTemplate.gradle - incompatible with AGP 8.x");
                    Debug.Log($"{Tag} Removed R8 pin from baseProjectTemplate.gradle (revert via git if needed)");
                });
#endif

                // org.gradle.java.home (Unity 2022 only - Unity 6+ bundles JDK 17) is injected at BUILD
                // TIME into the generated gradle.properties by GradlePropertiesFixer, never into the
                // committed gradleTemplate.properties: writing an absolute machine-local JDK path into a
                // version-controlled file breaks every other machine (B-16). Any such line already in the
                // committed template is that broken state, so it is deleted rather than reported: the path
                // points at one developer's JDK and is worthless to everyone else, including its author
                // once they change machines.
                Repair("gradleTemplate.properties JDK path", () =>
                {
                    if (!File.Exists(GradlePropertiesPath)) return;
                    string props = File.ReadAllText(GradlePropertiesPath);
                    string cleaned = RemoveGradleJavaHomeLines(props);
                    if (cleaned == props) return;

                    File.WriteAllText(GradlePropertiesPath, cleaned);
                    fixes.Add("Removed the hardcoded org.gradle.java.home line from " +
                              "gradleTemplate.properties - that JDK path is machine-local and breaks " +
                              "every other machine's Gradle build");
                    Debug.Log($"{Tag} Removed org.gradle.java.home from gradleTemplate.properties " +
                              "(the JDK home is injected into the generated gradle.properties at build time)");
                });
            }

            // Repairs above write either a project FILE (on disk the moment they return) or a serialized
            // ASSET (dirtied in memory only). This is the flush owner for the second kind: without it a
            // repair reports success, the check re-reads the same cached in-memory object and agrees, and
            // the file on disk never changes - a green row over an unrepaired project if the editor dies
            // before Unity flushes, or if the asset is read-only.
            Repair("save repaired assets", () =>
            {
                if (fixes.Count > 0)
                    AssetDatabase.SaveAssets();
            });

            return fixes;
        }

        /// <summary>
        ///     Runs one repair in isolation, mirroring <see cref="Run"/>'s check isolation. A repair that
        ///     throws (a read-only Gradle template raises UnauthorizedAccessException) names itself in the
        ///     log and every later repair still runs.
        ///
        ///     The pass must always return: the window renders its rows from the checks that run AFTER
        ///     this, and the pre-build hook runs it before validation - so one throwing repair used to
        ///     leave a rowless window, or surface as a raw exception that fails a build with no diagnosis.
        ///     A failed repair is deliberately NOT added to the returned list: that list is "repairs
        ///     applied" and is rendered as such. The residue is the check's to report, which it does,
        ///     because the thing the repair could not fix is still there for it to find.
        /// </summary>
        static void Repair(string name, Action repair)
        {
            try
            {
                repair();
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} auto-fix '{name}' failed: {e}");
            }
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
