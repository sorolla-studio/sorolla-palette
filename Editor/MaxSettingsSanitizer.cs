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

        /// <summary>
        ///     Check if Quality Service is enabled (causes build failures with 401 errors)
        /// </summary>
        public static bool IsQualityServiceEnabled()
        {
#if SOROLLA_MAX_INSTALLED
            try
            {
                // AppLovinSettings is not namespaced, access via reflection for safety
                var settingsType = System.Type.GetType("AppLovinSettings, Assembly-CSharp-Editor")
                                   ?? System.Type.GetType("AppLovinSettings, MaxSdk.Scripts.IntegrationManager.Editor");

                if (settingsType == null)
                {
                    // Try to find it in loaded assemblies
                    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        settingsType = assembly.GetType("AppLovinSettings");
                        if (settingsType != null)
                            break;
                    }
                }

                if (settingsType == null)
                    return false;

                var instanceProp = settingsType.GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (instanceProp == null)
                    return false;

                var instance = instanceProp.GetValue(null);
                if (instance == null)
                    return false;

                var qsProp = settingsType.GetProperty("QualityServiceEnabled",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (qsProp == null)
                    return false;

                return (bool)qsProp.GetValue(instance);
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
        ///     Sanitize MAX settings by disabling problematic features.
        ///     Currently disables Quality Service which causes 401 build failures.
        /// </summary>
        public static bool Sanitize() => DisableQualityService();

        /// <summary>
        ///     Disable Quality Service to prevent 401 errors during Android builds.
        ///     Quality Service is optional and not required for ads to function.
        /// </summary>
        private static bool DisableQualityService()
        {
#if SOROLLA_MAX_INSTALLED
            if (!IsQualityServiceEnabled())
                return false;

            try
            {
                var settingsType = System.Type.GetType("AppLovinSettings, Assembly-CSharp-Editor");
                if (settingsType == null)
                {
                    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        settingsType = assembly.GetType("AppLovinSettings");
                        if (settingsType != null)
                            break;
                    }
                }

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

                qsProp.SetValue(instance, false);

                // Call SaveAsync to persist
                var saveMethod = settingsType.GetMethod("SaveAsync",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                saveMethod?.Invoke(instance, null);

                Debug.Log($"{Tag} Disabled AppLovin Quality Service (not required, prevents 401 build errors)");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{Tag} Failed to disable Quality Service: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>
        ///     Menu item to check MAX settings status
        /// </summary>
        [MenuItem("Palette/Tools/Check MAX Settings")]
        public static void CheckMaxSettingsMenuItem()
        {
#if SOROLLA_MAX_INSTALLED
            var qsEnabled = IsQualityServiceEnabled();

            if (qsEnabled)
            {
                var disable = EditorUtility.DisplayDialog(
                    "MAX Settings Check",
                    "AppLovin Quality Service is ENABLED.\n\n" +
                    "This feature can cause 401 errors and build failures " +
                    "if not properly configured in the AppLovin dashboard.\n\n" +
                    "Quality Service is optional - ads work without it.\n\n" +
                    "Disable Quality Service?",
                    "Yes, Disable",
                    "Keep Enabled"
                );

                if (disable)
                {
                    Sanitize();
                    EditorUtility.DisplayDialog(
                        "MAX Settings Check",
                        "Quality Service has been disabled.",
                        "OK"
                    );
                }
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "MAX Settings Check",
                    "AppLovin Quality Service is disabled.\nNo issues detected.",
                    "OK"
                );
            }
#else
            EditorUtility.DisplayDialog(
                "MAX Settings Check",
                "AppLovin MAX is not installed.",
                "OK"
            );
#endif
        }
    }
}
