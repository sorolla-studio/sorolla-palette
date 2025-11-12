using UnityEditor;
using UnityEngine;

namespace SorollaPalette.Editor
{
    /// <summary>
    /// KISS mode management - single method call to switch modes
    /// Eliminates complex wizard + define sync + validation flow
    /// </summary>
    public static class ModeManager
    {
        private const string MODE_KEY = "SorollaPalette_Mode";

        /// <summary>
        /// Single method to switch modes - handles everything
        /// </summary>
        public static void SetMode(string mode)
        {
            // Validate mode
            if (mode != "Prototype" && mode != "Full")
            {
                Debug.LogError($"[ModeManager] Invalid mode: {mode}. Use 'Prototype' or 'Full'");
                return;
            }

            // Set mode preference
            EditorPrefs.SetString(MODE_KEY, mode);
            Debug.Log($"[Sorolla Palette] Mode set to: {mode}");

            // Apply defines
            DefineManager.ApplyModeDefines(mode);

            // Auto-install required SDKs
            if (mode == "Full")
            {
                InstallFullModeDependencies();
            }

            // Refresh assets
            AssetDatabase.Refresh();

            Debug.Log("[Sorolla Palette] Mode switch complete. Open 'Sorolla Palette → Configuration' to set up your SDKs.");
        }

        /// <summary>
        /// Get current mode
        /// </summary>
        public static string GetCurrentMode()
        {
            return EditorPrefs.GetString(MODE_KEY, "Not Selected");
        }

        /// <summary>
        /// Check if mode is selected
        /// </summary>
        public static bool IsModeSelected()
        {
            return GetCurrentMode() != "Not Selected";
        }

        private static void InstallFullModeDependencies()
        {
            // Check if MAX is already installed
            var isMaxInstalled = SdkDetection.IsMaxInstalled();
            if (!isMaxInstalled)
            {
                Debug.Log("[ModeManager] Full Mode requires AppLovin MAX. Installing automatically...");
                SorollaPaletteSetup.InstallAppLovinMAX();
            }

            // Check if Adjust is already installed
            var isAdjustInstalled = SdkDetection.IsAdjustInstalled();
            if (!isAdjustInstalled)
            {
                Debug.Log("[ModeManager] Full Mode requires Adjust SDK. Installing automatically...");
                SorollaPaletteSetup.InstallAdjustSDK();
            }
        }
    }
}