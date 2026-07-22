using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    public static partial class BuildValidator
    {
        /// <summary>
        ///     Check that GameAnalytics has a game key + secret key pair for BOTH platforms, graded by which
        ///     one the build in front of you targets. A key configured for a different platform still reads
        ///     "Configured" in the old Count&gt;0 proxy, so a prototype game (GA as sole vendor) silently
        ///     drops 100% of its events on the unconfigured platform (issue #8).
        ///
        ///     Severity (2026-07-22, superseding the earlier awareness-first ruling): the ACTIVE platform
        ///     missing is an Error, which blocks the build like the Adjust token does. A build whose sole
        ///     analytics vendor cannot report a single event is not a build worth making, and the previous
        ///     Warning made that fact easy to scroll past. The SIBLING platform missing stays a Warning:
        ///     games ship both platforms, so it must be visible, but it does not break the build at hand.
        /// </summary>
        static List<ValidationResult> CheckGameAnalyticsSettings()
        {
            var results = new List<ValidationResult>();
            const CheckCategory category = CheckCategory.GameAnalyticsSettings;

            if (!SdkDetector.IsInstalled(SdkId.GameAnalytics))
            {
                results.Add(Skipped(category, "GameAnalytics not installed"));
                return results;
            }

            bool activeIsIos = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;
            string activeName = activeIsIos ? "iOS" : "Android";
            string siblingName = activeIsIos ? "Android" : "iOS";
            bool activeConfigured = SdkConfigDetector.GetGameAnalyticsStatus() == SdkConfigDetector.ConfigStatus.Configured;
            bool siblingConfigured = SdkConfigDetector.HasGameAnalyticsKeysForOtherPlatform();

            // Fix hints deliberately omit the "Window > GameAnalytics > Select Settings" navigation step: the
            // row's "Edit" button performs exactly that navigation (product-audit fix cycle residual,
            // 2026-07-21) - the hint states only what to do once there.
            if (!activeConfigured)
            {
                string alsoSibling = siblingConfigured
                    ? ""
                    : $"\n  {siblingName} has no key pair either, so no platform of this game reports at all.";
                results.Add(Error(
                    category,
                    $"{activeName} has no game key + secret key pair in Assets/Resources/GameAnalytics/Settings.asset.\n" +
                    $"  GameAnalytics will drop 100% of events on {activeName}, the platform this build targets; " +
                    "device log shows the SDK never leaving \"not initialized\"." + alsoSibling,
                    $"Add {activeName} and paste the game key + secret key from the GameAnalytics dashboard"));
            }
            else if (!siblingConfigured)
            {
                results.Add(Warning(
                    category,
                    $"{siblingName} has no game key + secret key pair in Assets/Resources/GameAnalytics/Settings.asset.\n" +
                    $"  {activeName} (the active target) is fine, so this build reports - but GameAnalytics will " +
                    $"drop 100% of events on {siblingName} the moment this game ships there.",
                    $"Add {siblingName} and paste the game key + secret key from the GameAnalytics dashboard"));
            }
            else
            {
                results.Add(Valid(category, SdkConfigDetector.GetGameAnalyticsPlatformDetail()));
            }

            return results;
        }

        /// <summary>
        ///     GameAnalytics silently drops every resource event when the ResourceCurrencies
        ///     whitelist is empty. This only matters for games that track economy via
        ///     Palette.Economy - do NOT infer that usage by scanning game scripts (out of scope for
        ///     an editor-only check), state the conditionality in the message instead.
        /// </summary>
        static List<ValidationResult> CheckGameAnalyticsResourceWhitelist()
        {
            var results = new List<ValidationResult>();
            const CheckCategory category = CheckCategory.GameAnalyticsResourceWhitelist;

            if (!SdkDetector.IsInstalled(SdkId.GameAnalytics))
            {
                results.Add(Skipped(category, "GameAnalytics not installed"));
                return results;
            }

            var settings = Resources.Load("GameAnalytics/Settings");
            if (settings == null)
            {
                results.Add(Skipped(category, "Settings.asset not found (covered by GameAnalytics Platform Keys check)"));
                return results;
            }

            var serialized = new SerializedObject(settings);
            var resourceCurrenciesProperty = serialized.FindProperty("ResourceCurrencies");
            bool isEmpty = resourceCurrenciesProperty == null || !resourceCurrenciesProperty.isArray ||
                           resourceCurrenciesProperty.arraySize == 0;

            if (isEmpty)
            {
                // Fix hint omits the "Window > GameAnalytics > Select Settings" navigation step for the
                // same reason as CheckGameAnalyticsSettings above - the row's button already opens it.
                results.Add(Warning(
                    category,
                    "GameAnalytics ResourceCurrencies whitelist is empty.\n" +
                    "  Only matters if this game tracks economy via Palette.Economy - GameAnalytics silently drops every resource event when the whitelist is empty.",
                    "If the game tracks economy: add each currency name (e.g. Coins) to Resource Currencies"));
            }
            else
            {
                results.Add(Valid(category, $"ResourceCurrencies: {resourceCurrenciesProperty.arraySize} configured"));
            }

            return results;
        }
    }
}
