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
        ///     Human-readable per-platform breakdown for the SDK Overview row, e.g.
        ///     "Android configured, iOS key missing".
        /// </summary>
        public static string GetGameAnalyticsPlatformDetail()
        {
            if (!SdkDetector.IsInstalled(SdkId.GameAnalytics))
                return "Not installed";

            if (Resources.Load("GameAnalytics/Settings") == null)
                return "Settings.asset not found";

            string androidText = HasGameAnalyticsKeys(RuntimePlatform.Android) ? "Android configured" : "Android key missing";
            string iosText = HasGameAnalyticsKeys(RuntimePlatform.IPhonePlayer) ? "iOS configured" : "iOS key missing";
            return $"{androidText}, {iosText}";
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
        ///     Checks if Facebook SDK has a valid App ID configured.
        /// </summary>
        public static ConfigStatus GetFacebookStatus()
        {
            if (!SdkDetector.IsInstalled(SdkId.Facebook))
                return ConfigStatus.NotInstalled;

            try
            {
                // Load FacebookSettings from Resources
                var settings = Resources.Load("FacebookSettings");
                if (settings == null)
                    return ConfigStatus.NotConfigured;

                // Use SerializedObject to read appIds
                var serialized = new SerializedObject(settings);
                var appIdsProperty = serialized.FindProperty("appIds");
                
                if (appIdsProperty != null && appIdsProperty.isArray && appIdsProperty.arraySize > 0)
                {
                    var firstAppId = appIdsProperty.GetArrayElementAtIndex(0).stringValue;
                    // Check if it's a valid app ID (not "0" and has reasonable length)
                    if (!string.IsNullOrEmpty(firstAppId) && firstAppId != "0" && firstAppId.Length > 5)
                        return ConfigStatus.Configured;
                }
                
                return ConfigStatus.NotConfigured;
            }
            catch
            {
                return ConfigStatus.NotConfigured;
            }
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

        /// <summary>
        ///     Checks if Firebase Android config file exists.
        /// </summary>
        public static bool IsFirebaseAndroidConfigured()
        {
            // Check common locations for google-services.json
            return System.IO.File.Exists("Assets/google-services.json") ||
                   System.IO.File.Exists("Assets/StreamingAssets/google-services.json") ||
                   AssetDatabase.FindAssets("google-services").Length > 0;
        }

        /// <summary>
        ///     Checks if Firebase iOS config file exists.
        /// </summary>
        public static bool IsFirebaseIOSConfigured()
        {
            // Check common locations for GoogleService-Info.plist
            return System.IO.File.Exists("Assets/GoogleService-Info.plist") ||
                   System.IO.File.Exists("Assets/StreamingAssets/GoogleService-Info.plist") ||
                   AssetDatabase.FindAssets("GoogleService-Info").Length > 0;
        }

        /// <summary>
        ///     Checks if Firebase is fully configured (config files present).
        /// </summary>
        public static ConfigStatus GetFirebaseStatus(SorollaConfig config)
        {
            if (!SdkDetector.IsInstalled(SdkId.FirebaseAnalytics))
                return ConfigStatus.NotInstalled;

            // At least one platform should be configured
            if (IsFirebaseAndroidConfigured() || IsFirebaseIOSConfigured())
                return ConfigStatus.Configured;

            return ConfigStatus.NotConfigured;
        }
    }
}
