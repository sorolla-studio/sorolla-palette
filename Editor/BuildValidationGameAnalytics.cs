using System.Collections.Generic;
using UnityEditor;

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
    }
}
