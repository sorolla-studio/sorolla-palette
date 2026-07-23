using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    public static partial class BuildValidator
    {
        /// <summary>
        ///     Check that GameAnalytics has a game key + secret key pair for the platform this build targets.
        ///     A key configured for a different platform still reads "Configured" in the old Count&gt;0 proxy,
        ///     so a prototype game (GA as sole vendor) silently drops 100% of its events on the unconfigured
        ///     platform (issue #8).
        ///
        ///     Severity: missing keys for the active platform is an Error, which blocks the build like the
        ///     Adjust token does - a build whose sole analytics vendor cannot report a single event is not a
        ///     build worth making.
        ///
        ///     Scope (2026-07-23, superseding the 2026-07-22 both-platforms ruling): the check judges the
        ///     ACTIVE build target only. Warning about the other platform's keys assumed every game ships
        ///     both, so a game deliberately shipping one platform carried a warning it could never clear, and
        ///     that warning was enough to keep the whole report from reading green. The other platform's key
        ///     state is still visible - the vendor group caption states it - and it becomes a graded check the
        ///     moment the build target switches.
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

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            {
                results.Add(Skipped(category, "Select Android or iOS to check GameAnalytics platform keys"));
                return results;
            }

            string activeName = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS ? "iOS" : "Android";
            bool activeConfigured = SdkConfigDetector.GetGameAnalyticsStatus() == SdkConfigDetector.ConfigStatus.Configured;

            // Fix hints deliberately omit the "Window > GameAnalytics > Select Settings" navigation step: the
            // row's "Edit" button performs exactly that navigation (product-audit fix cycle residual,
            // 2026-07-21) - the hint states only what to do once there.
            if (!activeConfigured)
            {
                results.Add(Error(
                    category,
                    $"{activeName} has no game key + secret key pair in Assets/Resources/GameAnalytics/Settings.asset.\n" +
                    $"  GameAnalytics will drop 100% of events on {activeName}, the platform this build targets; " +
                    "device log shows the SDK never leaving \"not initialized\".",
                    $"Add {activeName} and paste the game key + secret key from the GameAnalytics dashboard"));
            }
            else
            {
                results.Add(Valid(category, $"{activeName} has a game key + secret key pair."));
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
            SerializedProperty resourceCurrenciesProperty = serialized.FindProperty("ResourceCurrencies");
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
                    "If the game tracks economy: add each currency name (e.g. coins) to Resource Currencies"));
                return results;
            }

            List<string> mismatches = WhitelistMismatches(resourceCurrenciesProperty, EconomyVocabulary.Currencies());
            mismatches.AddRange(WhitelistMismatches(
                serialized.FindProperty("ResourceItemTypes"), EconomyVocabulary.ItemTypes()));

            if (mismatches.Count > 0)
                results.Add(Warning(
                    category,
                    "GameAnalytics whitelist entries do not match what Palette sends:\n  " +
                    string.Join("\n  ", mismatches) + "\n" +
                    "  GameAnalytics silently drops every resource event whose currency or item type is not whitelisted EXACTLY.",
                    "Rewrite each listed entry in the form Palette sends (lower_snake_case)"));
            else
                results.Add(Valid(category, $"ResourceCurrencies: {resourceCurrenciesProperty.arraySize} configured"));

            return results;
        }

        /// <summary>
        ///     Whitelist entries that name something Palette emits but spell it differently - "Coins" for the
        ///     "coins" the SDK actually sends. GameAnalytics matches these strings exactly, so the event is
        ///     dropped in silence and only surfaces days later as missing dashboard data. Entries matching
        ///     nothing Palette emits are left alone: they cost nothing and may be another integration's.
        /// </summary>
        static List<string> WhitelistMismatches(SerializedProperty list, string[] emitted)
        {
            var mismatches = new List<string>();
            if (list == null || !list.isArray) return mismatches;

            for (int i = 0; i < list.arraySize; i++)
            {
                string entry = list.GetArrayElementAtIndex(i).stringValue;
                if (string.IsNullOrEmpty(entry)) continue;

                string normalized = Normalize(entry);
                foreach (string name in emitted)
                {
                    if (entry == name || Normalize(name) != normalized) continue;
                    mismatches.Add($"'{entry}' -> Palette sends '{name}'");
                    break;
                }
            }
            return mismatches;
        }

        /// <summary>Case- and separator-insensitive form, so "Level Reward", "LevelReward" and "level-reward"
        /// all collapse onto the emitted "level_reward".</summary>
        static string Normalize(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }
    }
}
