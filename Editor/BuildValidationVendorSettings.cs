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
                    "Reopen Unity or click Refresh above; report this if it persists."));
                return results;
            }

            if (!MaxSettingsSanitizer.IsConsentFlowConfigured())
            {
                results.Add(Error(
                    CheckCategory.MaxSettings,
                    "AppLovin consent flow auto-sync failed.\n" +
                    "  The shared privacy policy URL could not be written to AppLovin internal settings.",
                    "Reopen Unity or click Refresh above; report this if it persists."));
                return results;
            }

            // MAX installed = the game intends to show ads (no separate "ads enabled" flag exists on
            // SorollaConfig). Full mode only - ad units are optional in Prototype.
            if (!SorollaSettings.IsPrototype)
            {
                var config = Resources.Load<SorollaConfig>("SorollaConfig");

                // Every format x platform, not "both formats empty on the active platform" (2026-07-22). The
                // old condition passed a game with a rewarded unit and no interstitial - every interstitial
                // call then failed to load with the row green - and it never looked at the platform it was
                // not building, so an iOS ship could carry no ad units at all.
                var missing = new List<string>();
                foreach ((string format, PlatformAdUnitId unit) in new[]
                         {
                             ("Rewarded", config?.rewardedAdUnit),
                             ("Interstitial", config?.interstitialAdUnit),
                         })
                {
                    if (string.IsNullOrEmpty(unit?.android)) missing.Add($"{format} (Android)");
                    if (string.IsNullOrEmpty(unit?.ios)) missing.Add($"{format} (iOS)");
                }

                if (missing.Count > 0)
                {
                    // Fix hint doesn't tell you to open the window you're already inside (F6, 2026-07-21
                    // audit) - the MAX Ad Units fields are in this same window's AppLovin MAX group,
                    // below this row (vendor-consolidation cycle, 2026-07-21 15:35: SDK Keys is gone).
                    results.Add(Warning(
                        CheckCategory.MaxSettings,
                        $"MAX ad unit IDs missing in SorollaConfig: {string.Join(", ", missing)}.\n" +
                        "  Every ad call for a missing format/platform pair fails to load; banner units are not checked (optional format).",
                        "Enter the AppLovin MAX ad unit IDs for both platforms below"));
                    return results;
                }
            }

            results.Add(Valid(CheckCategory.MaxSettings, "MAX settings synced"));
#else
            results.Add(Skipped(CheckCategory.MaxSettings, "MAX not installed"));
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
        static List<ValidationResult> CheckAdjustSettings(Dictionary<string, object> dependencies)
        {
            var results = new List<ValidationResult>();

            // Only check in Full mode when Adjust is installed
            if (!SorollaSettings.IsConfigured || SorollaSettings.IsPrototype)
            {
                results.Add(Skipped(CheckCategory.AdjustSettings, "Adjust not required"));
                return results;
            }

            if (!SdkDetector.IsInstalled(SdkId.Adjust))
            {
                // Installation is checked by CheckRequiredSdks - just skip config check here
                results.Add(Skipped(CheckCategory.AdjustSettings, "Adjust not installed"));
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
                // Fix hint no longer tells you to open the window you're already inside (F6, 2026-07-21
                // audit) - the Adjust App Token field is in this same window's Adjust group, below this
                // row (vendor-consolidation cycle, 2026-07-21 15:35: SDK Keys is gone).
                results.Add(Error(
                    CheckCategory.AdjustSettings,
                    "Adjust app token is not configured!\n" +
                    "  Attribution tracking will not work without a valid app token.\n" +
                    "  Enter your Adjust app token below.",
                    "Enter Adjust app token below"));
            }
            else
            {
                results.Add(Valid(CheckCategory.AdjustSettings, "Adjust app token OK"));
            }

            // The purchase event token was validated NOWHERE before 2026-07-22, so a game could wire IAP,
            // ship, and simply never see revenue in Adjust - Palette.TrackPurchase needs this token to send
            // the revenue event. Only meaningful once the game actually sells something, so it is scoped to
            // projects that have Unity IAP installed.
            if (dependencies.ContainsKey("com.unity.purchasing") && string.IsNullOrEmpty(config.adjustPurchaseEventToken))
            {
                results.Add(Warning(
                    CheckCategory.AdjustSettings,
                    "Unity IAP is installed but SorollaConfig has no Adjust purchase event token.\n" +
                    "  Purchases will track everywhere else and send no revenue event to Adjust.",
                    "Adjust dashboard > this app > All Settings > Events: add a revenue/\"Purchase\" event and paste its 6-character event token below"));
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
