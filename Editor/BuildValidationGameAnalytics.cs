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
                results.Add(Error(
                    category,
                    $"GameAnalytics has no game key + secret key pair for {platformName} in Assets/Resources/GameAnalytics/Settings.asset.\n" +
                    "  Progression/economy/custom events silently stop reaching GameAnalytics on this platform.",
                    $"Window > GameAnalytics > Select Settings, add {platformName}, and paste the game key + secret key from the GameAnalytics dashboard"));
            }

            return results;
        }
    }
}
