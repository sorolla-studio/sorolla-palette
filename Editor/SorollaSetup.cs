using UnityEditor;
using UnityEngine;

namespace Sorolla.Editor
{
    /// <summary>
    ///     Auto-setup on package import.
    ///     Installs core dependencies required regardless of mode.
    /// </summary>
    [InitializeOnLoad]
    public static class SorollaSetup
    {
        private const string SetupVersion = "v3";
        private static string SetupKey => $"Sorolla_Setup_{SetupVersion}_{Application.dataPath.GetHashCode()}";

        static SorollaSetup()
        {
            EditorApplication.delayCall += RunSetup;
        }

        [MenuItem("Sorolla/Run Setup (Force)")]
        public static void ForceRunSetup()
        {
            EditorPrefs.DeleteKey(SetupKey);
            RunSetup();
        }

        private static void RunSetup()
        {
            if (EditorPrefs.GetBool(SetupKey, false))
                return;

            Debug.Log("[Sorolla] Running initial setup...");

            // Add OpenUPM registry for GA and EDM
            ManifestManager.AddOrUpdateRegistry(
                "package.openupm.com",
                "https://package.openupm.com",
                new[] { "com.gameanalytics", "com.google.external-dependency-manager" }
            );

            // Install core dependencies
            SdkInstaller.InstallCoreDependencies();

            EditorPrefs.SetBool(SetupKey, true);
            Debug.Log("[Sorolla] Setup complete. Select a mode in the Configuration window.");
        }
    }
}
