using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Detects whether SDKs are properly configured with required keys/settings.
    /// </summary>
    public static class SdkConfigDetector
    {
        /// <summary>
        ///     Configuration status for an SDK.
        /// </summary>
        public enum ConfigStatus
        {
            NotInstalled,
            NotConfigured,
            Configured
        }

        /// <summary>
        ///     Checks if GameAnalytics has a game key + secret key pair for the ACTIVE build target.
        ///     A key configured for a different platform (e.g. Android-only) no longer false-greens
        ///     an iOS build (issue #8).
        /// </summary>
        public static ConfigStatus GetGameAnalyticsStatus()
        {
            if (!SdkDetector.IsInstalled(SdkId.GameAnalytics))
                return ConfigStatus.NotInstalled;

            RuntimePlatform active = ActiveGameAnalyticsPlatform();
            return HasGameAnalyticsKeys(active) ? ConfigStatus.Configured : ConfigStatus.NotConfigured;
        }

        /// <summary>
        ///     Human-readable detail for the GameAnalytics group header, covering BOTH platforms - e.g.
        ///     "Android ✓ · iOS missing". Both are named with their own state so the text always agrees
        ///     with the header glyph (Arthur ruling 2026-07-21 ~17:40, superseding the active-target-only
        ///     scoping): games ship both platforms, so a missing sibling-platform key is information the
        ///     studio needs, surfaced as a Warn - not silence.
        /// </summary>
        public static string GetGameAnalyticsPlatformDetail()
        {
            if (!SdkDetector.IsInstalled(SdkId.GameAnalytics))
                return "Not installed";

            if (Resources.Load("GameAnalytics/Settings") == null)
                return "Settings.asset not found";

            bool android = HasGameAnalyticsKeys(RuntimePlatform.Android);
            bool ios = HasGameAnalyticsKeys(RuntimePlatform.IPhonePlayer);
            if (android && ios) return "Android + iOS configured";
            return $"Android {(android ? "✓" : "missing")} · iOS {(ios ? "✓" : "missing")}";
        }

        /// <summary>Key-pair status for the platform the active build target does NOT map to - lets the
        /// window warn (not fail) when only the sibling platform is unconfigured.</summary>
        public static bool HasGameAnalyticsKeysForOtherPlatform()
        {
            RuntimePlatform other = ActiveGameAnalyticsPlatform() == RuntimePlatform.IPhonePlayer
                ? RuntimePlatform.Android
                : RuntimePlatform.IPhonePlayer;
            return HasGameAnalyticsKeys(other);
        }

        /// <summary>
        ///     Maps the active build target to the GameAnalytics <c>RuntimePlatform</c> entry it stores
        ///     keys under.
        /// </summary>
        public static RuntimePlatform ActiveGameAnalyticsPlatform() =>
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS
                ? RuntimePlatform.IPhonePlayer
                : RuntimePlatform.Android;

        /// <summary>
        ///     Checks whether GameAnalytics Settings.asset has a non-empty game key + secret key pair
        ///     for the given platform. Reflection is required: Settings.gameKey/secretKey are private
        ///     fields on the vendor's ScriptableObject, parallel-indexed with the public Platforms list.
        /// </summary>
        static bool HasGameAnalyticsKeys(RuntimePlatform platform)
        {
            try
            {
                UnityEngine.Object settings = Resources.Load("GameAnalytics/Settings");
                if (settings == null)
                    return false;

                Type settingsType = settings.GetType();
                const BindingFlags publicInstance = BindingFlags.Public | BindingFlags.Instance;
                const BindingFlags privateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

                if (!(settingsType.GetField("Platforms", publicInstance)?.GetValue(settings) is IList platforms))
                    return false;
                if (!(settingsType.GetField("gameKey", privateInstance)?.GetValue(settings) is IList gameKeys))
                    return false;
                if (!(settingsType.GetField("secretKey", privateInstance)?.GetValue(settings) is IList secretKeys))
                    return false;

                for (int i = 0; i < platforms.Count; i++)
                {
                    if (!(platforms[i] is RuntimePlatform p) || p != platform) continue;

                    string gameKey = i < gameKeys.Count ? gameKeys[i] as string : null;
                    string secretKey = i < secretKeys.Count ? secretKeys[i] as string : null;
                    return !string.IsNullOrEmpty(gameKey) && !string.IsNullOrEmpty(secretKey);
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     Reads the GameAnalytics game key + secret key pair for the ACTIVE build target from
        ///     Settings.asset. Same reflection approach as <see cref="HasGameAnalyticsKeys"/> - used by
        ///     the GA credential probe, which needs the actual values, not just a presence bool.
        /// </summary>
        public static bool TryGetGameAnalyticsCredentials(out string gameKey, out string secretKey)
        {
            gameKey = null;
            secretKey = null;

            if (!SdkDetector.IsInstalled(SdkId.GameAnalytics))
                return false;

            try
            {
                UnityEngine.Object settings = Resources.Load("GameAnalytics/Settings");
                if (settings == null)
                    return false;

                Type settingsType = settings.GetType();
                const BindingFlags publicInstance = BindingFlags.Public | BindingFlags.Instance;
                const BindingFlags privateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

                if (!(settingsType.GetField("Platforms", publicInstance)?.GetValue(settings) is IList platforms))
                    return false;
                if (!(settingsType.GetField("gameKey", privateInstance)?.GetValue(settings) is IList gameKeys))
                    return false;
                if (!(settingsType.GetField("secretKey", privateInstance)?.GetValue(settings) is IList secretKeys))
                    return false;

                RuntimePlatform active = ActiveGameAnalyticsPlatform();
                for (int i = 0; i < platforms.Count; i++)
                {
                    if (!(platforms[i] is RuntimePlatform p) || p != active) continue;

                    string candidateGameKey = i < gameKeys.Count ? gameKeys[i] as string : null;
                    string candidateSecretKey = i < secretKeys.Count ? secretKeys[i] as string : null;
                    if (string.IsNullOrEmpty(candidateGameKey) || string.IsNullOrEmpty(candidateSecretKey))
                        return false;

                    gameKey = candidateGameKey;
                    secretKey = candidateSecretKey;
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     Reads the app id + client token pair from FacebookSettings.asset (first entry, same
        ///     index convention as <see cref="GetFacebookStatus"/>). Used by the Graph platform check.
        /// </summary>
        public static bool TryGetFacebookCredentials(out string appId, out string clientToken)
        {
            appId = null;
            clientToken = null;

            if (!SdkDetector.IsInstalled(SdkId.Facebook))
                return false;

            try
            {
                var settings = Resources.Load("FacebookSettings");
                if (settings == null)
                    return false;

                var serialized = new SerializedObject(settings);
                var appIdsProperty = serialized.FindProperty("appIds");
                var clientTokensProperty = serialized.FindProperty("clientTokens");

                if (appIdsProperty == null || !appIdsProperty.isArray || appIdsProperty.arraySize == 0)
                    return false;
                if (clientTokensProperty == null || !clientTokensProperty.isArray || clientTokensProperty.arraySize == 0)
                    return false;

                string candidateAppId = appIdsProperty.GetArrayElementAtIndex(0).stringValue;
                string candidateClientToken = clientTokensProperty.GetArrayElementAtIndex(0).stringValue;

                if (string.IsNullOrEmpty(candidateAppId) || candidateAppId == "0" || candidateAppId.Length <= 5)
                    return false;
                if (string.IsNullOrEmpty(candidateClientToken))
                    return false;

                appId = candidateAppId;
                clientToken = candidateClientToken;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     Checks if Facebook SDK has BOTH a valid App ID and Client Token configured. Used to false-green
        ///     as Configured on App ID alone while the Graph platform probe (which needs both) warns about the
        ///     same vendor beside a flat green Overview row (product-audit finding F7, 2026-07-21) - now the
        ///     same <see cref="TryGetFacebookCredentials"/> pair the probe itself requires.
        /// </summary>
        public static ConfigStatus GetFacebookStatus()
        {
            if (!SdkDetector.IsInstalled(SdkId.Facebook))
                return ConfigStatus.NotInstalled;

            return TryGetFacebookCredentials(out _, out _) ? ConfigStatus.Configured : ConfigStatus.NotConfigured;
        }

        /// <summary>
        ///     Checks if Adjust has a valid app token configured.
        /// </summary>
        public static ConfigStatus GetAdjustStatus(SorollaConfig config)
        {
            if (!SdkDetector.IsInstalled(SdkId.Adjust))
                return ConfigStatus.NotInstalled;

            if (config == null)
                return ConfigStatus.NotConfigured;

            return !string.IsNullOrEmpty(config.adjustAppToken) && config.adjustAppToken.Length > 5
                ? ConfigStatus.Configured
                : ConfigStatus.NotConfigured;
        }

        /// <summary>
        ///     Opens the GameAnalytics Setup Wizard.
        /// </summary>
        public static void OpenGameAnalyticsSettings()
        {
            EditorApplication.ExecuteMenuItem("Window/GameAnalytics/Select Settings");
        }

        /// <summary>
        ///     Opens the Facebook Settings window.
        /// </summary>
        public static void OpenFacebookSettings()
        {
            EditorApplication.ExecuteMenuItem("Facebook/Edit Settings");
        }

        // The presence-only Firebase helpers (IsFirebaseAndroidConfigured / IsFirebaseIOSConfigured /
        // GetFirebaseStatus) were deleted 2026-07-22 along with their only callers - the Firebase group
        // header and its internal per-file sub-rows. Presence of a file is not evidence the file belongs to
        // this game, and the iOS one matched ANY asset named like GoogleService-Info anywhere in the project,
        // so a stray copy could paint the header green. The Firebase Config Files check is the one answer
        // now: both platforms, each matched against the app id it must carry, using the exact-path helpers
        // below.

        /// <summary>
        ///     Existing google-services.json paths for the active Android config (exact locations only, so the
        ///     auto-generated google-services-desktop.json can never false-positive). Zero/one/many is
        ///     meaningful to the caller: many means the shipping config is ambiguous, not a silent pick.
        /// </summary>
        public static System.Collections.Generic.List<string> FirebaseAndroidConfigPaths()
        {
            var paths = new System.Collections.Generic.List<string>();
            foreach (string p in new[] { "Assets/google-services.json", "Assets/StreamingAssets/google-services.json" })
                if (System.IO.File.Exists(p))
                    paths.Add(p);
            return paths;
        }

        /// <summary>
        ///     Existing GoogleService-Info.plist paths for the active iOS config - the SAME exact locations
        ///     the Android helper checks, and only those. A project-wide AssetDatabase search used to be
        ///     folded in here, which made the two platforms behave differently: any stray second plist
        ///     anywhere in the project (a vendor sample, a backup folder, another game's copy) made the
        ///     shipping config "ambiguous" and pinned the report permanently non-green, while the correct
        ///     file sat at Assets/GoogleService-Info.plist. Only the paths Unity actually ships from count.
        /// </summary>
        public static System.Collections.Generic.List<string> FirebaseIosConfigPaths()
        {
            var paths = new System.Collections.Generic.List<string>();
            foreach (string p in new[] { "Assets/GoogleService-Info.plist", "Assets/StreamingAssets/GoogleService-Info.plist" })
                if (System.IO.File.Exists(p))
                    paths.Add(p);
            return paths;
        }

    }
}
