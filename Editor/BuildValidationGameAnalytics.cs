using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    public static partial class BuildValidator
    {
        /// <summary>
        ///     Check that GameAnalytics has a game key + secret key pair for the active build target.
        ///     A key configured for a different platform still reads "Configured" in the old
        ///     Count&gt;0 proxy, so a prototype game (GA as sole vendor) silently drops 100% of its
        ///     events on the unconfigured platform (issue #8).
        /// </summary>
        static List<ValidationResult> CheckGameAnalyticsSettings()
        {
            var results = new List<ValidationResult>();
            const CheckCategory category = CheckCategory.GameAnalyticsSettings;

            if (!SdkDetector.IsInstalled(SdkId.GameAnalytics))
            {
                results.Add(Valid(category, "GameAnalytics not installed"));
                return results;
            }

            string platformName = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS ? "iOS" : "Android";

            if (SdkConfigDetector.GetGameAnalyticsStatus() == SdkConfigDetector.ConfigStatus.Configured)
            {
                results.Add(Valid(category, $"GameAnalytics configured for {platformName}"));
            }
            else
            {
                // Awareness-first severity ruling (Arthur, via supervisor): a studio may intentionally
                // ship one platform at a time, so a missing per-platform vendor key is not a build
                // blocker - it is a warning with a clear root cause, signal, and fix.
                results.Add(Warning(
                    category,
                    $"{platformName} has no game key + secret key pair in Assets/Resources/GameAnalytics/Settings.asset.\n" +
                    $"  GameAnalytics will drop 100% of events on {platformName}; device log shows the SDK never leaving \"not initialized\".",
                    $"Window > GameAnalytics > Select Settings, add {platformName}, and paste the game key + secret key from the GameAnalytics dashboard"));
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
                results.Add(Valid(category, "GameAnalytics not installed"));
                return results;
            }

            var settings = Resources.Load("GameAnalytics/Settings");
            if (settings == null)
            {
                results.Add(Valid(category, "Settings.asset not found (covered by GameAnalytics Platform Keys check)"));
                return results;
            }

            var serialized = new SerializedObject(settings);
            var resourceCurrenciesProperty = serialized.FindProperty("ResourceCurrencies");
            bool isEmpty = resourceCurrenciesProperty == null || !resourceCurrenciesProperty.isArray ||
                           resourceCurrenciesProperty.arraySize == 0;

            if (isEmpty)
            {
                results.Add(Warning(
                    category,
                    "GameAnalytics ResourceCurrencies whitelist is empty.\n" +
                    "  Only matters if this game tracks economy via Palette.Economy - GameAnalytics silently drops every resource event when the whitelist is empty.",
                    "If the game tracks economy: Window > GameAnalytics > Select Settings and add each currency name (e.g. Coins) to Resource Currencies"));
            }
            else
            {
                results.Add(Valid(category, $"ResourceCurrencies: {resourceCurrenciesProperty.arraySize} configured"));
            }

            return results;
        }
    }
}
