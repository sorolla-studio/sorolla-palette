using System;
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
        ///     Checks if GameAnalytics has valid game keys configured.
        /// </summary>
        public static ConfigStatus GetGameAnalyticsStatus()
        {
            if (!SdkDetector.IsInstalled(SdkId.GameAnalytics))
                return ConfigStatus.NotInstalled;

            try
            {
                // Load GameAnalytics Settings from Resources
                var settings = Resources.Load("GameAnalytics/Settings");
                if (settings == null)
                    return ConfigStatus.NotConfigured;

                // Use reflection to check if game keys are configured
                var settingsType = settings.GetType();
                var platformsField = settingsType.GetMethod("GetAllPlatformGameKeys");
                
                // Alternative: check the gameKey list directly
                var gameKeyField = settingsType.GetField("gameKey", 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance);
                
                if (gameKeyField?.GetValue(settings) is System.Collections.IList keys && keys.Count > 0)
                {
                    foreach (var key in keys)
                    {
                        if (key is string keyStr && !string.IsNullOrEmpty(keyStr) && keyStr.Length > 10)
                            return ConfigStatus.Configured;
                    }
                }
                
                return ConfigStatus.NotConfigured;
            }
            catch
            {
                return ConfigStatus.NotConfigured;
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
        ///     Checks if AppLovin MAX has a valid SDK key configured.
        /// </summary>
        public static ConfigStatus GetMaxStatus(SorollaConfig config)
        {
            if (!SdkDetector.IsInstalled(SdkId.AppLovinMAX))
                return ConfigStatus.NotInstalled;

            if (config == null)
                return ConfigStatus.NotConfigured;

            return !string.IsNullOrEmpty(config.maxSdkKey) && config.maxSdkKey.Length > 10
                ? ConfigStatus.Configured
                : ConfigStatus.NotConfigured;
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
        ///     Opens the AppLovin Integration Manager.
        /// </summary>
        public static void OpenMaxSettings()
        {
            EditorApplication.ExecuteMenuItem("AppLovin/Integration Manager");
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
        ///     Checks if Firebase is fully configured (both platforms).
        /// </summary>
        public static ConfigStatus GetFirebaseStatus(SorollaConfig config)
        {
            if (!SdkDetector.IsInstalled(SdkId.FirebaseAnalytics))
                return ConfigStatus.NotInstalled;

            if (config == null || !config.enableFirebaseAnalytics)
                return ConfigStatus.NotConfigured;

            // At least one platform should be configured
            if (IsFirebaseAndroidConfigured() || IsFirebaseIOSConfigured())
                return ConfigStatus.Configured;

            return ConfigStatus.NotConfigured;
        }

        /// <summary>
        ///     Checks if Firebase Crashlytics is configured.
        /// </summary>
        public static ConfigStatus GetCrashlyticsStatus(SorollaConfig config)
        {
            if (!SdkDetector.IsInstalled(SdkId.FirebaseCrashlytics))
                return ConfigStatus.NotInstalled;

            if (config == null || !config.enableCrashlytics)
                return ConfigStatus.NotConfigured;

            // Crashlytics needs config files too
            if (IsFirebaseAndroidConfigured() || IsFirebaseIOSConfigured())
                return ConfigStatus.Configured;

            return ConfigStatus.NotConfigured;
        }

        /// <summary>
        ///     Checks if Firebase Remote Config is configured.
        /// </summary>
        public static ConfigStatus GetRemoteConfigStatus(SorollaConfig config)
        {
            if (!SdkDetector.IsInstalled(SdkId.FirebaseRemoteConfig))
                return ConfigStatus.NotInstalled;

            if (config == null || !config.enableRemoteConfig)
                return ConfigStatus.NotConfigured;

            // Remote Config needs config files too
            if (IsFirebaseAndroidConfigured() || IsFirebaseIOSConfigured())
                return ConfigStatus.Configured;

            return ConfigStatus.NotConfigured;
        }
    }
}
