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
        private const string SetupKey = "SorollaPalette_Setup_Completed_v1";

        static SorollaPaletteSetup()
        {
            // Run simplified setup on package import
            EditorApplication.delayCall += RunSetup;
        }

        [MenuItem("Tools/Sorolla Palette/Run Setup (Force)")]
        public static void ForceRunSetup()
        {
            // Clear the key to force run
            EditorPrefs.DeleteKey(GetProjectSpecificKey());
            RunSetup();
        }

        private static void RunSetup()
        {
            if (EditorPrefs.GetBool(GetProjectSpecificKey(), false))
            {
                return;
            }

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
            
            // Mark setup as complete
            EditorPrefs.SetBool(GetProjectSpecificKey(), true);
        }

        private static string GetProjectSpecificKey()
        {
            return $"{SetupKey}_{Application.dataPath.GetHashCode()}";
        }
    }
}