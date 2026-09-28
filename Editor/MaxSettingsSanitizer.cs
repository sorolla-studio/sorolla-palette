using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Sanitizes AppLovin MAX settings to prevent known build issues.
    ///     Quality Service causes 401 errors and build failures when not properly configured.
    /// </summary>
    public static class MaxSettingsSanitizer
    {
        private const string Tag = "[Palette MaxSanitizer]";

        // Cached Type lookup to avoid repeated reflection
        private static System.Type s_appLovinSettingsType;
        private static System.Type s_appLovinInternalSettingsType;
        private static bool s_typeSearched;
        private static bool s_internalTypeSearched;

        /// <summary>
        ///     Get the SDK key from AppLovinSettings.
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
        ///     Get the AdMob application id AppLovin writes into the Android manifest / Info.plist for the
        ///     active build target. AppLovin defaults both fields to the empty string, so an unset id reads
        ///     as EMPTY. NULL means the value could not be read at all - AppLovinSettings absent, or an
        ///     AppLovin version that renamed or removed the property - and the caller grades that as
        ///     unverifiable rather than missing. Keep the two distinct.
        /// </summary>
        public static string GetAdMobAppId(bool ios)
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

                var appIdProp = settingsType.GetProperty(ios ? "AdMobIosAppId" : "AdMobAndroidAppId",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                return appIdProp?.GetValue(instance) as string;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{Tag} Failed to get AdMob app id: {e.Message}");
                return null;
            }
#else
            return null;
#endif
        }

        /// <summary>
        ///     Check if AppLovinSettings has the shared publisher SDK key.
        /// </summary>
        public static bool IsSdkKeyConfigured()
        {
            var key = GetSdkKey();
            return PaletteConstants.IsExpectedMaxSdkKey(key);
        }

        /// <summary>
        ///     Sync the embedded publisher-level SDK key to AppLovinSettings.
        /// </summary>
        public static bool SyncEmbeddedSdkKey()
        {
#if SOROLLA_MAX_INSTALLED
            string currentKey = GetSdkKey() ?? "";
            if (PaletteConstants.IsExpectedMaxSdkKey(currentKey))
                return false;

            return SetExpectedSdkKey();
#else
            return false;
#endif
        }

        /// <summary>
        ///     Set the shared publisher SDK key in AppLovinSettings.
        /// </summary>
        static bool SetExpectedSdkKey()
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

                sdkKeyProp.SetValue(instance, PaletteConstants.MaxSdkKey);

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

        private static System.Type GetAppLovinInternalSettingsType()
        {
            if (!s_internalTypeSearched)
            {
                s_internalTypeSearched = true;
                s_appLovinInternalSettingsType = FindAppLovinInternalSettingsType();
            }
            return s_appLovinInternalSettingsType;
        }

        private static System.Type FindAppLovinInternalSettingsType()
        {
            var settingsType = System.Type.GetType(
                "AppLovinMax.Scripts.IntegrationManager.Editor.AppLovinInternalSettings, MaxSdk.Scripts.IntegrationManager.Editor");

            if (settingsType == null)
            {
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    settingsType = assembly.GetType("AppLovinMax.Scripts.IntegrationManager.Editor.AppLovinInternalSettings");
                    if (settingsType != null)
                        break;
                }
            }

            return settingsType;
        }

        /// <summary>
        ///     Check if Quality Service (Ad Review) is enabled.
        /// </summary>
        public static bool IsQualityServiceEnabled()
        {
#if SOROLLA_MAX_INSTALLED
            try
            {
                var settingsType = GetAppLovinSettingsType();
                if (settingsType == null)
                    return false;

                var instanceProp = settingsType.GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var instance = instanceProp?.GetValue(null);
                if (instance == null)
                    return false;

                var qsProp = settingsType.GetProperty("QualityServiceEnabled",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                return qsProp != null && (bool)qsProp.GetValue(instance);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{Tag} Failed to check Quality Service status: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>
        ///     Auto-enable Quality Service (Ad Review) if not already enabled.
        ///     Ad Review improves ad quality and requires an Ad Review Key configured
        ///     on the AppLovin dashboard (generated by AppLovin AM).
        /// </summary>
        public static bool EnableQualityService()
        {
#if SOROLLA_MAX_INSTALLED
            if (IsQualityServiceEnabled())
                return false;

            try
            {
                var settingsType = GetAppLovinSettingsType();
                if (settingsType == null)
                    return false;

                var instanceProp = settingsType.GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var instance = instanceProp?.GetValue(null);
                if (instance == null)
                    return false;

                var qsProp = settingsType.GetProperty("QualityServiceEnabled",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (qsProp == null)
                    return false;

                qsProp.SetValue(instance, true);

                var saveMethod = settingsType.GetMethod("SaveAsync",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                saveMethod?.Invoke(instance, null);

                Debug.Log($"{Tag} Enabled AppLovin Ad Review (Quality Service)");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{Tag} Failed to enable Quality Service: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>
        ///     Whether AppLovin's own consent flow (its UMP integration and its "Terms and Privacy Policy"
        ///     alert) is off. Palette runs Google UMP and ATT itself before MAX starts, so it must be. False
        ///     when the setting cannot be read.
        /// </summary>
        public static bool IsConsentFlowDisabled()
        {
#if SOROLLA_MAX_INSTALLED
            try
            {
                var enabled = GetConsentFlowEnabledProperty(out object settings);
                return enabled != null && !(bool)enabled.GetValue(settings);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{Tag} Failed to check consent flow settings: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>
        ///     Turns AppLovin's consent flow off. Returns true when the setting changed.
        /// </summary>
        public static bool DisableConsentFlow()
        {
#if SOROLLA_MAX_INSTALLED
            try
            {
                var enabled = GetConsentFlowEnabledProperty(out object settings);
                if (enabled == null || !(bool)enabled.GetValue(settings))
                    return false;

                enabled.SetValue(settings, false);
                settings.GetType().GetMethod("Save",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.Invoke(settings, null);
                Debug.Log($"{Tag} Disabled AppLovin's consent flow: Palette runs Google UMP and ATT itself");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{Tag} Failed to disable AppLovin's consent flow: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }

#if SOROLLA_MAX_INSTALLED
        static System.Reflection.PropertyInfo GetConsentFlowEnabledProperty(out object settings)
        {
            var settingsType = GetAppLovinInternalSettingsType();
            settings = settingsType?.GetProperty("Instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
            return settings == null
                ? null
                : settingsType.GetProperty("ConsentFlowEnabled",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        }
#endif
    }
}
