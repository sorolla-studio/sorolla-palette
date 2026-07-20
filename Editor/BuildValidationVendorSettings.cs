using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    public static partial class BuildValidator
    {
        /// <summary>
        ///     Check AppLovin MAX settings for known issues
        /// </summary>
        static List<ValidationResult> CheckMaxSettings()
        {
            var results = new List<ValidationResult>();

#if SOROLLA_MAX_INSTALLED
            MaxSettingsSanitizer.SyncEmbeddedSdkKey();
            MaxSettingsSanitizer.SyncConsentFlowSettings();

            if (!MaxSettingsSanitizer.IsSdkKeyConfigured())
            {
                results.Add(Error(
                    CheckCategory.MaxSettings,
                    "AppLovin MAX SDK key auto-sync failed.\n" +
                    "  The shared publisher key could not be written to AppLovinSettings.",
                    "Reopen Unity or click Refresh in Build Health; report this if it persists"));
                return results;
            }

            if (!MaxSettingsSanitizer.IsConsentFlowConfigured())
            {
                results.Add(Error(
                    CheckCategory.MaxSettings,
                    "AppLovin consent flow auto-sync failed.\n" +
                    "  The shared privacy policy URL could not be written to AppLovin internal settings.",
                    "Reopen Unity or click Refresh in Build Health; report this if it persists"));
                return results;
            }

            // MAX installed = the game intends to show ads (no separate "ads enabled" flag exists on
            // SorollaConfig). Full mode only - ad units are optional in Prototype.
            if (!SorollaSettings.IsPrototype)
            {
                var config = Resources.Load<SorollaConfig>("SorollaConfig");
                string activePlatform = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS ? "iOS" : "Android";
                bool rewardedMissing = string.IsNullOrEmpty(config?.rewardedAdUnit?.Current);
                bool interstitialMissing = string.IsNullOrEmpty(config?.interstitialAdUnit?.Current);

                if (rewardedMissing && interstitialMissing)
                {
                    results.Add(Warning(
                        CheckCategory.MaxSettings,
                        $"MAX has no rewarded or interstitial ad unit ID set for {activePlatform} in SorollaConfig.\n" +
                        "  Ad calls will fail to load on this platform until an ad unit ID is set.",
                        "Open Tools > Sorolla Palette SDK and enter the AppLovin MAX ad unit IDs for this platform"));
                    return results;
                }
            }

            results.Add(Valid(CheckCategory.MaxSettings, "MAX settings synced"));
#else
            results.Add(Valid(CheckCategory.MaxSettings, "MAX not installed"));
#endif

            return results;
        }

        /// <summary>
        ///     Check Adjust SDK app token configuration (Full mode only).
        ///     Note: SDK installation is checked by CheckRequiredSdks().
        ///     Schema note (2026-07 vendor platform-scoping sweep): Sorolla's supported Adjust setup
        ///     is one multi-platform Adjust app with a single token; separate per-platform Adjust apps
        ///     are not supported by this config schema - if a studio's Adjust dashboard uses
        ///     per-platform apps, this token is wrong for one platform and this check cannot detect it.
        ///     Confirmed via games.yaml roster that every current Adjust-using game already uses one
        ///     token across platforms, so this is not a proxy bug like the old GA check - it is a
        ///     schema limitation on an unsupported setup, deliberately not fixed (see
        ///     greenlight-backtest-2026-07.md, "Vendor platform-scoping sweep").
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
                    "  Enter your Adjust app token in Tools > Sorolla Palette SDK.",
                    "Open Tools > Sorolla Palette SDK and enter Adjust app token"));
            }
            else
            {
                results.Add(Valid(CheckCategory.AdjustSettings, "Adjust app token OK"));
            }

            return results;
        }

        /// <summary>
        ///     Informational row: the Adjust version actually resolved into Library/PackageCache.
        ///     Skew judgment (is this version too old / does it match the manifest intent) stays
        ///     human - this check only surfaces the fact, it never warns.
        /// </summary>
        static List<ValidationResult> CheckAdjustResolvedVersion()
        {
            var results = new List<ValidationResult>();
            const CheckCategory category = CheckCategory.AdjustResolvedVersion;

            if (!SdkDetector.IsInstalled(SdkId.Adjust))
            {
                results.Add(Valid(category, "Adjust not installed"));
                return results;
            }

            string version = ReadAdjustResolvedVersion();
            results.Add(Valid(category, version != null
                ? $"Adjust resolved version: {version}"
                : "Adjust resolved version: could not read package.json from Library/PackageCache"));

            return results;
        }

        static string ReadAdjustResolvedVersion()
        {
            try
            {
                string cacheRoot = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "PackageCache");
                if (!Directory.Exists(cacheRoot))
                    return null;

                string[] dirs = Directory.GetDirectories(cacheRoot, "com.adjust.sdk*");
                if (dirs.Length == 0)
                    return null;

                string packageJsonPath = Path.Combine(dirs[0], "package.json");
                if (!File.Exists(packageJsonPath))
                    return null;

                var parsed = MiniJson.Deserialize(File.ReadAllText(packageJsonPath)) as Dictionary<string, object>;
                return parsed != null && parsed.TryGetValue("version", out object v) ? v?.ToString() : null;
            }
            catch
            {
                return null;
            }
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
                        "Allow the next domain reload to reapply EDM4U Gradle settings");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} Could not check EDM4U Gradle mode: {e.Message}");
            }

            return null;
        }
    }
}
