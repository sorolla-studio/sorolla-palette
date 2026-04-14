using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Manages AppLovin MAX settings via reflection (SDK key sync).
    /// </summary>
    public static class MaxSettingsSanitizer
    {
        private const string Tag = "[Palette MaxSanitizer]";

        // Cached Type lookup to avoid repeated reflection
        private static System.Type s_appLovinSettingsType;
        private static bool s_typeSearched;

        /// <summary>
        ///     Get the SDK key from AppLovinSettings (configured in Integration Manager)
        /// </summary>
        public static string GetSdkKey()
        {
#if SOROLLA_MAX_INSTALLED
            try
            {
                var settingsType = GetAppLovinSettingsType();
                if (settingsType == null)
                    return null;

                var instanceProp = settingsType.GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var instance = instanceProp?.GetValue(null);
                if (instance == null)
                    return null;

                var sdkKeyProp = settingsType.GetProperty("SdkKey",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                return sdkKeyProp?.GetValue(instance) as string;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{Tag} Failed to get SDK key: {e.Message}");
                return null;
            }
#else
            return null;
#endif
        }

        /// <summary>
        ///     Check if SDK key is configured in AppLovinSettings
        /// </summary>
        public static bool IsSdkKeyConfigured()
        {
            var key = GetSdkKey();
            return !string.IsNullOrEmpty(key) && key.Length > 10;
        }

        /// <summary>
        ///     Set the SDK key in AppLovinSettings (programmatically sync from SorollaConfig)
        /// </summary>
        public static bool SetSdkKey(string sdkKey)
        {
#if SOROLLA_MAX_INSTALLED
            try
            {
                var settingsType = GetAppLovinSettingsType();
                if (settingsType == null)
                {
                    Debug.LogWarning($"{Tag} Could not find AppLovinSettings type");
                    return false;
                }

                var instanceProp = settingsType.GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var instance = instanceProp?.GetValue(null);
                if (instance == null)
                {
                    Debug.LogWarning($"{Tag} Could not get AppLovinSettings instance");
                    return false;
                }

                var sdkKeyProp = settingsType.GetProperty("SdkKey",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (sdkKeyProp == null)
                {
                    Debug.LogWarning($"{Tag} Could not find SdkKey property");
                    return false;
                }

                sdkKeyProp.SetValue(instance, sdkKey);

                // Call SaveAsync to persist to AppLovinSettings.asset
                var saveMethod = settingsType.GetMethod("SaveAsync",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                saveMethod?.Invoke(instance, null);

                Debug.Log($"{Tag} Synced SDK key to AppLovinSettings");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{Tag} Failed to set SDK key: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        private static System.Type GetAppLovinSettingsType()
        {
            if (!s_typeSearched)
            {
                s_typeSearched = true;
                s_appLovinSettingsType = FindAppLovinSettingsType();
            }
            return s_appLovinSettingsType;
        }

        private static System.Type FindAppLovinSettingsType()
        {
            var settingsType = System.Type.GetType("AppLovinSettings, Assembly-CSharp-Editor")
                               ?? System.Type.GetType("AppLovinSettings, MaxSdk.Scripts.IntegrationManager.Editor");

            if (settingsType == null)
            {
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    settingsType = assembly.GetType("AppLovinSettings");
                    if (settingsType != null)
                        break;
                }
            }

            return settingsType;
        }

    }
}
