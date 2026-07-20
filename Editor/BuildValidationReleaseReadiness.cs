using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Phase 3 (Build Health parity with the pre-build gates): profile-scoped config-flag checks
    ///     that were previously only caught by a manual QA pass. Awareness-first: every check here is
    ///     a Warning (or an informational Valid row), never an Error - a studio may be mid-iteration
    ///     when these fire, and none of them are unambiguous breakage the way a missing Adjust token is.
    /// </summary>
    public static partial class BuildValidator
    {
        /// <summary>
        ///     Drift hygiene, not a leak gate - runtime already forces verboseLogging off in
        ///     non-development builds. Still worth surfacing so a stray "on" doesn't get committed.
        /// </summary>
        static List<ValidationResult> CheckVerboseLogging()
        {
            var results = new List<ValidationResult>();
            const CheckCategory category = CheckCategory.VerboseLogging;

            var config = Resources.Load<SorollaConfig>("SorollaConfig");
            if (config == null)
            {
                results.Add(Valid(category, "SorollaConfig not found"));
                return results;
            }

            if (config.verboseLogging)
            {
                results.Add(Warning(
                    category,
                    "SorollaConfig.verboseLogging is on.\n" +
                    "  Runtime already forces this off in non-development builds, so this is drift hygiene, not a leak risk.",
                    "Turn off verboseLogging in Tools > Sorolla Palette SDK before committing, unless this is a dev/QA build on purpose"));
            }
            else
            {
                results.Add(Valid(category, "verboseLogging off"));
            }

            return results;
        }

        /// <summary>
        ///     Scans Library/BuildProfiles/*.asset for m_Development: 1 plus the legacy
        ///     EditorUserBuildSettings.development flag. Both profiles - a stray Development Build
        ///     flag is exactly as unwanted in a QA-pass build meant to mirror release as in a release
        ///     build.
        /// </summary>
        static List<ValidationResult> CheckDevelopmentBuildFlag()
        {
            var results = new List<ValidationResult>();
            const CheckCategory category = CheckCategory.DevelopmentBuild;

            var flaggedProfiles = new List<string>();
            string profilesDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "BuildProfiles");
            if (Directory.Exists(profilesDir))
            {
                foreach (string path in Directory.GetFiles(profilesDir, "*.asset"))
                {
                    if (File.ReadAllText(path).Contains("m_Development: 1"))
                        flaggedProfiles.Add(Path.GetFileName(path));
                }
            }

            bool developmentBuildSetting = EditorUserBuildSettings.development;

            if (flaggedProfiles.Count == 0 && !developmentBuildSetting)
            {
                results.Add(Valid(category, "Development Build off"));
                return results;
            }

            var signals = new List<string>();
            if (developmentBuildSetting)
                signals.Add("EditorUserBuildSettings.development is on");
            if (flaggedProfiles.Count > 0)
                signals.Add($"m_Development: 1 in {string.Join(", ", flaggedProfiles)}");

            results.Add(Warning(
                category,
                $"Development Build is enabled ({string.Join("; ", signals)}).\n" +
                "  A store submission built with Development Build carries debug symbols/profiler hooks and can be rejected or bloat the binary.",
                "Uncheck Development Build in File > Build Settings (or the active Build Profile) before a release build"));

            return results;
        }

        /// <summary>
        ///     Release profile only: adjustSandboxMode must be off before store submission.
        ///     QA-pass builds legitimately run in sandbox.
        /// </summary>
        static List<ValidationResult> CheckAdjustSandboxMode()
        {
            var results = new List<ValidationResult>();
            const CheckCategory category = CheckCategory.AdjustSandboxMode;

            if (!BuildValidationProfileSettings.IsRelease)
            {
                results.Add(Valid(category, "Skipped (QA Pass profile)"));
                return results;
            }

            if (!SdkDetector.IsInstalled(SdkId.Adjust))
            {
                results.Add(Valid(category, "Adjust not installed"));
                return results;
            }

            var config = Resources.Load<SorollaConfig>("SorollaConfig");
            if (config != null && config.adjustSandboxMode)
            {
                results.Add(Warning(
                    category,
                    "SorollaConfig.adjustSandboxMode is on.\n" +
                    "  Sandbox events are excluded from Adjust's live dashboards/attribution - must be off before store submission.",
                    "Turn off Adjust Sandbox mode in Tools > Sorolla Palette SDK before a release build"));
            }
            else
            {
                results.Add(Valid(category, "Adjust sandbox mode off"));
            }

            return results;
        }

        /// <summary>
        ///     Release profile only, Android target only: a release AAB/APK signed with the
        ///     debug/auto keystore cannot be uploaded to Play Console as a production release.
        /// </summary>
        static List<ValidationResult> CheckAndroidKeystore()
        {
            var results = new List<ValidationResult>();
            const CheckCategory category = CheckCategory.AndroidKeystore;

            if (!BuildValidationProfileSettings.IsRelease)
            {
                results.Add(Valid(category, "Skipped (QA Pass profile)"));
                return results;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                results.Add(Valid(category, "Skipped (not Android)"));
                return results;
            }

            string keystoreName = PlayerSettings.Android.keystoreName;
            if (string.IsNullOrEmpty(keystoreName) || !File.Exists(keystoreName))
            {
                results.Add(Warning(
                    category,
                    "No release keystore configured in Player Settings (or the configured file is missing).\n" +
                    "  A build signed with the debug/auto keystore is rejected by Play Console as a production release.",
                    "Set a release keystore in Player Settings > Publishing Settings > Keystore Manager"));
            }
            else
            {
                results.Add(Valid(category, "Android keystore configured"));
            }

            return results;
        }

        /// <summary>
        ///     Only runs when the Addressables package is installed. Missing content or link.xml means
        ///     Addressables-loaded assets - or IL2CPP types reachable only via Addressables - can be
        ///     absent from the built player.
        /// </summary>
        static List<ValidationResult> CheckAddressablesContent(Dictionary<string, object> dependencies)
        {
            var results = new List<ValidationResult>();
            const CheckCategory category = CheckCategory.AddressablesContent;

            if (!dependencies.ContainsKey("com.unity.addressables"))
            {
                results.Add(Valid(category, "Addressables not installed"));
                return results;
            }

            bool hasAddressablesData = Directory.Exists(Path.Combine(Application.dataPath, "AddressableAssetsData"));
            bool hasLinkXml = Directory.Exists(Application.dataPath) &&
                               Directory.GetFiles(Application.dataPath, "link.xml", SearchOption.AllDirectories).Length > 0;

            if (!hasAddressablesData || !hasLinkXml)
            {
                var missing = new List<string>();
                if (!hasAddressablesData) missing.Add("Assets/AddressableAssetsData");
                if (!hasLinkXml) missing.Add("a link.xml");

                results.Add(Warning(
                    category,
                    $"Addressables is installed but {string.Join(" and ", missing)} {(missing.Count > 1 ? "are" : "is")} missing.\n" +
                    "  Addressables groups won't build, or IL2CPP will strip types reachable only via Addressables.",
                    "Build Addressables content (Window > Asset Management > Addressables > Groups > Build) and generate a link.xml for IL2CPP stripping protection"));
            }
            else
            {
                results.Add(Valid(category, "Addressables content present"));
            }

            return results;
        }

        static readonly Regex SdkTagPattern = new Regex(@"#v\d+\.\d+\.\d+$");

        /// <summary>
        ///     Release profile: warns when com.sorolla.sdk in manifest.json isn't pinned to a
        ///     published #vX.Y.Z tag (master/hash pins are irreproducible for a release build).
        ///     QA-pass profile: same ref surfaced as an informational row - master/hash is normal there.
        /// </summary>
        static List<ValidationResult> CheckSdkPin(Dictionary<string, object> dependencies)
        {
            var results = new List<ValidationResult>();
            const CheckCategory category = CheckCategory.SdkPin;

            if (!dependencies.TryGetValue("com.sorolla.sdk", out object sdkRefObj))
            {
                results.Add(Valid(category, "com.sorolla.sdk is an embedded/local package - no manifest pin to check"));
                return results;
            }

            string sdkRef = sdkRefObj?.ToString() ?? "";
            bool isTagPinned = SdkTagPattern.IsMatch(sdkRef);

            if (!BuildValidationProfileSettings.IsRelease)
            {
                results.Add(Valid(category, $"com.sorolla.sdk ref: {sdkRef}"));
                return results;
            }

            if (isTagPinned)
            {
                results.Add(Valid(category, $"com.sorolla.sdk pinned to {sdkRef.Split('#').Last()}"));
            }
            else
            {
                results.Add(Warning(
                    category,
                    $"com.sorolla.sdk is not pinned to a #vX.Y.Z tag in manifest.json (current: {sdkRef}).\n" +
                    "  master/hash pins are normal for dev/QA but make a release build irreproducible.",
                    "Pin com.sorolla.sdk to a published tag (e.g. #v3.18.3) before a release build"));
            }

            return results;
        }
    }
}
