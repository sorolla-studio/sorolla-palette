using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Automatic dependency checking and installation for plug-and-play experience.
    ///     Detects missing SDKs and auto-installs them based on current mode.
    /// </summary>
    [InitializeOnLoad]
    public static class SorollaDependencyChecker
    {
        const string CheckKey = "Sorolla_DependencyCheck_v1";
        const float CheckDelay = 2f; // Delay after domain reload

        static SorollaDependencyChecker()
        {
            // Run check after domain reload with a delay
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetBool(CheckKey, false))
                    return;

                EditorApplication.delayCall += RunDependencyCheck;
            };
        }

        [MenuItem("Palette/Check Dependencies")]
        public static void ForceRunDependencyCheck()
        {
            EditorPrefs.DeleteKey(CheckKey);
            RunDependencyCheck();
        }

        static void RunDependencyCheck()
        {
            // Skip if mode not configured yet
            if (!SorollaSettings.IsConfigured)
                return;

            // Skip in play mode
            if (Application.isPlaying)
                return;

            bool isPrototype = SorollaSettings.IsPrototype;
            var missingRequired = new List<SdkInfo>();

            // Check for missing required SDKs
            foreach (var sdk in SdkRegistry.GetRequired(isPrototype))
            {
                if (!SdkDetector.IsInstalled(sdk.Id))
                {
                    missingRequired.Add(sdk);
                }
            }

            // If any required SDKs are missing, auto-install them
            if (missingRequired.Count > 0)
            {
                Debug.LogWarning($"[Palette] Detected {missingRequired.Count} missing required SDK(s). Auto-installing for plug-and-play experience...");
                
                foreach (var sdk in missingRequired)
                {
                    Debug.Log($"[Palette] Auto-installing missing SDK: {sdk.Name}");
                }

                // Use the existing installer infrastructure
                SdkInstaller.InstallRequiredSdks(isPrototype);
                
                Debug.Log("[Palette] Missing dependencies added to manifest. Package Manager will resolve automatically.");
                Debug.Log("[Palette] Unity will reload after dependencies are resolved.");
            }
            else
            {
                Debug.Log("[Palette] All required dependencies are installed. Ready to use!");
            }

            // Mark as checked
            EditorPrefs.SetBool(CheckKey, true);
        }

        /// <summary>
        ///     Reset the check flag to force re-checking on next domain reload.
        ///     Used when switching modes.
        /// </summary>
        public static void ResetCheckFlag()
        {
            EditorPrefs.DeleteKey(CheckKey);
        }
    }
}
