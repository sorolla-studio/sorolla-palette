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
        internal static void CheckMaxSettings(List<ValidationResult> results)
        {
#if SOROLLA_MAX_INSTALLED
            // Findings THIS check produced, not the size of the shared list it appends into: every earlier
            // check has already appended to it, so an absolute count is never zero in the real pass and the
            // healthy-path pass below was never emitted - a correctly configured MAX project reported "no
            // result was produced" and could not read green.
            int findingsBefore = results.Count;

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            {
                results.Add(Skipped(ReadinessChecks.MaxSettings,
                    "Select Android or iOS to check AppLovin MAX settings"));
                return;
            }

            MaxSettingsSanitizer.SyncEmbeddedSdkKey();
            MaxSettingsSanitizer.SyncConsentFlowSettings();

            if (!MaxSettingsSanitizer.IsSdkKeyConfigured())
            {
                results.Add(Error(
                    ReadinessChecks.MaxSettings,
                    "AppLovin MAX SDK key auto-sync failed.\n" +
                    "  The shared publisher key could not be written to AppLovinSettings, which lives in " +
                    "Assets/MaxSdk/Resources/AppLovinSettings.asset.\n" +
                    "  Ads cannot initialize without it, and the sync has already retried this pass.",
                    // The expected value is quoted from the SDK's own constant, not from a doc: the check is
                    // an exact string match, so a doc that drifted would fail it again silently.
                    "Open AppLovin > Integration Manager and set SDK Key to exactly:\n" +
                    $"  {PaletteConstants.MaxSdkKey}"));
                return;
            }

            if (!MaxSettingsSanitizer.IsConsentFlowConfigured())
            {
                results.Add(Error(
                    ReadinessChecks.MaxSettings,
                    "AppLovin consent flow auto-sync failed.\n" +
                    "  The shared privacy policy URL could not be written to AppLovin internal settings " +
                    "(Assets/MaxSdk/Resources/AppLovinSettings.asset).\n" +
                    "  Without it the consent flow ships without a privacy policy, and the sync has already " +
                    "retried this pass.",
                    "Open AppLovin > Integration Manager > Consent Flow, enable it, and set the privacy " +
                    "policy URL to exactly:\n" +
                    $"  {PaletteConstants.PrivacyPolicyUrl}"));
                return;
            }

            // MAX installed = the game intends to show ads (no separate "ads enabled" flag exists on
            // SorollaConfig), so its active-platform ad units are required in either mode.
            var config = Resources.Load<SorollaConfig>("SorollaConfig");

            // EVERY format, not "both formats empty" (2026-07-22): the old condition passed a game with a
            // rewarded unit and no interstitial, and every interstitial call then failed to load with the
            // row green. Formats are graded for the ACTIVE build target only (2026-07-23): the ad units
            // for a platform this build does not target cannot break this build, and demanding them kept a
            // one-platform game permanently below green. Both platforms' fields stay editable in the MAX
            // group, and the other platform's units are graded when it becomes the build target.
            bool activeIsIos = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;
            string platformName = activeIsIos ? "iOS" : "Android";
            // Two studios in a row (2026-09-17) read the active-platform FAILs as work to do for a
            // platform their game never ships on; the header line naming the judged target was not enough.
            string switchHint = $", or switch Build Settings to {(activeIsIos ? "Android" : "iOS")} " +
                                $"if this game does not ship on {platformName}";

            // The consent flow above is Google UMP: it initializes the Google Mobile Ads SDK, which aborts
            // when no AdMob application id is present in the manifest / Info.plist. AppLovin writes that id
            // from AppLovinSettings, so an empty field ships a consent flow that cannot run. Same owner,
            // capability, scope, severity, and studio action as the rest of this row, so it grades here
            // rather than as its own check.
            ValidationResult adMob = GradeMaxAdMobAppId(
                MaxSettingsSanitizer.GetAdMobAppId(activeIsIos), platformName, switchHint);
            if (adMob != null)
                results.Add(adMob);

            var missing = new List<string>();
            foreach ((string format, PlatformAdUnitId unit) in new[]
                     {
                         ("Rewarded", config?.rewardedAdUnit),
                         ("Interstitial", config?.interstitialAdUnit),
                     })
            {
                if (string.IsNullOrEmpty(activeIsIos ? unit?.ios : unit?.android)) missing.Add(format);
            }

            if (missing.Count > 0)
            {
                // Fix hint doesn't tell you to open the window you're already inside (F6, 2026-07-21
                // audit) - the MAX Ad Units fields are in this same window's AppLovin MAX group,
                // below this row (vendor-consolidation cycle, 2026-07-21 15:35: SDK Keys is gone).
                results.Add(Error(
                    ReadinessChecks.MaxSettings,
                    $"MAX ad unit IDs missing for {platformName} in SorollaConfig: {string.Join(", ", missing)}.\n" +
                    "  Every ad call for a missing format fails to load; banner units are not checked (optional format).",
                    $"Enter the AppLovin MAX ad unit IDs for {platformName} below{switchHint}"));
            }

            // A missing AdMob id and missing ad units are two separate studio actions, so both findings are
            // produced rather than the first returning early - and both survive evaluation with their own
            // fix, in the window and in the copied report.
            if (results.Count == findingsBefore)
                results.Add(Valid(ReadinessChecks.MaxSettings, "MAX settings synced"));
#else
            results.Add(Skipped(ReadinessChecks.MaxSettings, "MAX not installed"));
#endif
        }

        /// <summary>
        ///     Grades the active platform's AdMob application id as part of the MAX settings row.
        ///     Returns null when the id is present, so the row continues to its remaining findings.
        ///
        ///     Null and empty are different facts. AppLovin defaults both id fields to the empty string, so
        ///     empty is an observed "the studio never filled this in". Null means the property could not be
        ///     read at all - an AppLovin version that renamed or removed it, or a reflection throw - and
        ///     grading an unread field as missing would block builds on a fact this check never observed.
        /// </summary>
        internal static ValidationResult GradeMaxAdMobAppId(string adMobAppId, string platformName, string switchHint)
        {
            if (adMobAppId == null)
            {
                return Unverifiable(
                    ReadinessChecks.MaxSettings,
                    $"Could not read the AdMob {platformName} application id from AppLovinSettings.\n" +
                    "  The property is missing on this AppLovin version, so the consent flow's AdMob id " +
                    "could not be checked either way.",
                    "Confirm the AdMob App ID field in AppLovin Integration Manager, then copy this report " +
                    "to Sorolla if the row does not clear");
            }

            return adMobAppId.Length == 0
                ? Error(
                    ReadinessChecks.MaxSettings,
                    $"AdMob application id for {platformName} is empty in AppLovinSettings.\n" +
                    "  The AppLovin consent flow (Google UMP) cannot initialize without it, so no consent " +
                    "is collected and ads do not serve.",
                    // Names who provisions the id, not just where it goes: the app is created in SOROLLA's
                    // AdMob account, so a studio cannot generate this value and there is no local source of
                    // truth for Palette to fill in either - a fix text that only said "paste it" sent studios
                    // looking for an id that did not exist yet. The destination is the real control on the
                    // installed AppLovin (8.6.4): a per-platform App ID field that AppLovin renders under the
                    // Google Ad Manager row of the Mediated Networks list (the AdMob row has no field; a
                    // studio hunted for it there, 2026-09-17), not a menu path.
                    $"Request the AdMob {platformName} app id (ca-app-pub-…~…) for this game from Sorolla ops - " +
                    "it is created in Sorolla's AdMob account - then open the AppLovin Integration Manager " +
                    $"window and paste it into the \"App ID ({platformName})\" field under the Google Ad Manager " +
                    $"row of the Mediated Networks list (AppLovin puts the AdMob app id there){switchHint}")
                : null;
        }

        /// <summary>
        ///     Check Adjust SDK app token configuration (Full mode only).
        ///     Note: SDK installation is checked by CheckRequiredSdks().
        ///     Schema note (2026-07 vendor platform-scoping sweep): Sorolla's supported Adjust setup
        ///     is one multi-platform Adjust app with a single token; separate per-platform Adjust apps
        ///     are not supported by this config schema - if a studio's Adjust dashboard uses
        ///     per-platform apps, this token is wrong for one platform and this check cannot detect it.
        ///     Confirmed (2026-07) that every current Adjust-using game on the publishing roster
        ///     already uses one token across platforms, so this is not a proxy bug like the old
        ///     GameAnalytics check - it is a schema limitation on an unsupported setup, deliberately
        ///     not fixed.
        /// </summary>
        static void CheckAdjustSettings(List<ValidationResult> results, Dictionary<string, object> dependencies)
        {
            // Only check in Full mode when Adjust is installed
            if (!SorollaSettings.IsConfigured || SorollaSettings.IsPrototype)
            {
                results.Add(Skipped(ReadinessChecks.AdjustSettings, "Adjust not required"));
                return;
            }

            if (!SdkDetector.IsInstalled(SdkId.Adjust))
            {
                // Installation is checked by CheckRequiredSdks - just skip config check here
                results.Add(Skipped(ReadinessChecks.AdjustSettings, "Adjust not installed"));
                return;
            }

            var config = Resources.Load<SorollaConfig>("SorollaConfig");
            if (config == null)
            {
                results.Add(Warning(
                    ReadinessChecks.AdjustSettings,
                    "SorollaConfig not found - cannot validate Adjust app token",
                    "Create config via Assets > Create > Palette > Config"));
                return;
            }

            SdkConfigDetector.ConfigStatus adjustStatus = SdkConfigDetector.GetAdjustStatus(config);
            if (adjustStatus == SdkConfigDetector.ConfigStatus.NotConfigured)
            {
                // Fix hint no longer tells you to open the window you're already inside (F6, 2026-07-21
                // audit) - the Adjust App Token field is in this same window's Adjust group, below this
                // row (vendor-consolidation cycle, 2026-07-21 15:35: SDK Keys is gone).
                results.Add(Error(
                    ReadinessChecks.AdjustSettings,
                    "Adjust app token is not configured!\n" +
                    "  Attribution tracking will not work without a valid app token.\n" +
                    "  The token is per game and created in Sorolla's Adjust account - never copy another game's token.",
                    "Request this game's Adjust app token from Sorolla ops, then enter it below"));
            }
            else
            {
                results.Add(Valid(ReadinessChecks.AdjustSettings, "Adjust app token OK"));
            }

            // The purchase event token was validated NOWHERE before 2026-07-22, so a game could wire IAP,
            // ship, and simply never see revenue in Adjust - Palette.TrackPurchase needs this token to send
            // the revenue event. Only meaningful once the game actually sells something, so it is scoped to
            // projects that have Unity IAP installed.
            if (dependencies.ContainsKey("com.unity.purchasing") && string.IsNullOrEmpty(config.adjustPurchaseEventToken))
            {
                results.Add(Warning(
                    ReadinessChecks.AdjustSettings,
                    "Unity IAP is installed but SorollaConfig has no Adjust purchase event token.\n" +
                    "  Purchases will track everywhere else and send no revenue event to Adjust.",
                    "Adjust dashboard > this app > All Settings > Events: add a revenue/\"Purchase\" event and paste its 6-character event token below"));
            }
        }

        /// <summary>
        ///     Check for duplicate EDM4U installations and Gradle template mode configuration.
        /// </summary>
        static void CheckEdm4uSettings(List<ValidationResult> results)
        {
            bool hasIssues = false;

            // Check for duplicate installations
            var duplicates = Edm4uSanitizer.DetectDuplicateInstallations();
            if (duplicates.Count > 0)
            {
                hasIssues = true;
                results.AddRange(duplicates.Select(dup => Warning(
                    ReadinessChecks.Edm4uSettings,
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
                results.Add(Valid(ReadinessChecks.Edm4uSettings, "EDM4U settings OK"));
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
                        ReadinessChecks.Edm4uSettings,
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
