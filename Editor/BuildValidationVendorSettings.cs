using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

            results.Add(Valid(CheckCategory.MaxSettings, "MAX settings synced"));
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
    }
}
