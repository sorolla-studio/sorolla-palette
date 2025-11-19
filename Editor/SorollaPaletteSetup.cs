using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace SorollaPalette.Editor
{
    /// <summary>
    ///     Simplified setup - now just delegates to specialized managers
    /// </summary>
    [InitializeOnLoad]
    public static class SorollaPaletteSetup
    {
        static SorollaPaletteSetup()
        {
            // Run simplified setup on package import
            EditorApplication.delayCall += RunSetup;
        }

        [MenuItem("Tools/Sorolla Palette/Run Setup (Force)")]
        public static void ForceRunSetup()
        {
            RunSetup();
        }

        private static void RunSetup()
        {
            Debug.Log("[Sorolla Palette] Running initial setup...");

            // Add required registries
            ManifestManager.AddOrUpdateRegistry("Game Package Registry by Google",
                                      "https://unityregistry-pa.googleapis.com/", new[] { "com.google" });
            ManifestManager.AddOrUpdateRegistry("package.openupm.com",
                                      "https://package.openupm.com",
                                      new[] { "com.gameanalytics", "com.google.external-dependency-manager" });

            // Install core dependencies
            InstallationManager.InstallGameAnalytics();
            InstallationManager.InstallExternalDependencyManager();

            Debug.Log("[Sorolla Palette] Setup initiated. Check the progress bar for details.");
        }

        public static void InstallAppLovinMAX()
        {
            InstallationManager.InstallAppLovinMAX();
        }

        public static void UninstallAppLovinMAX()
        {
            InstallationManager.UninstallAppLovinMAX();
        }

        public static void InstallAdjustSDK()
        {
            InstallationManager.InstallAdjustSDK();
        }
    }
}