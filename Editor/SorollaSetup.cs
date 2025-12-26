using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Editor
{
    /// <summary>
    ///     Auto-setup on package import.
    ///     Configures manifest.json with registries and core dependencies in one shot.
    ///     Unity Package Manager handles resolution order automatically.
    /// </summary>
    [InitializeOnLoad]
    public static class SorollaSetup
    {
        const string SetupVersion = "v5"; // Bumped for link.xml auto-copy

        static SorollaSetup()
        {
            EditorApplication.delayCall += RunSetup;
        }
        static string SetupKey => $"Sorolla_Setup_{SetupVersion}_{Application.dataPath.GetHashCode()}";

        [MenuItem("SorollaSDK/Run Setup (Force)")]
        public static void ForceRunSetup()
        {
            EditorPrefs.DeleteKey(SetupKey);
            RunSetup();
        }

        static void RunSetup()
        {
            if (EditorPrefs.GetBool(SetupKey, false))
                return;

            Debug.Log("[SorollaSDK] Running initial setup...");

            // Copy link.xml to Assets/ for IL2CPP stripping protection
            CopyLinkXmlToAssets();

            // Collect all scopes needed for OpenUPM
            var openUpmScopes = new List<string>();
            var dependencies = new Dictionary<string, string>();

            foreach (SdkInfo sdk in SdkRegistry.All.Values)
            {
                if (sdk.Requirement != SdkRequirement.Core)
                    continue;

                // Add scope if needed
                if (!string.IsNullOrEmpty(sdk.Scope))
                    openUpmScopes.Add(sdk.Scope);

                // Add dependency
                dependencies[sdk.PackageId] = sdk.DependencyValue;
            }

            // Add OpenUPM registry with all scopes
            if (openUpmScopes.Count > 0)
            {
                ManifestManager.AddOrUpdateRegistry(
                    "package.openupm.com",
                    "https://package.openupm.com",
                    openUpmScopes.ToArray()
                );
            }

            // Add all core dependencies in one shot - UPM handles resolution order
            ManifestManager.AddDependencies(dependencies);

            EditorPrefs.SetBool(SetupKey, true);
            Debug.Log("[SorollaSDK] Setup complete. Package Manager will resolve dependencies.");
            Debug.Log("[SorollaSDK] Open SorollaSDK > Configuration to select a mode.");
        }

        /// <summary>
        ///     Copy link.xml from package to Assets/ for IL2CPP code stripping protection.
        ///     Unity does NOT auto-include link.xml from UPM packages, so we must copy it.
        /// </summary>
        static void CopyLinkXmlToAssets()
        {
            const string sourcePath = "Packages/com.sorolla.sdk/Runtime/link.xml";
            const string destPath = "Assets/Sorolla.link.xml";

            // Skip if already exists (don't overwrite user modifications)
            if (File.Exists(destPath))
            {
                Debug.Log("[SorollaSDK] link.xml already exists in Assets/, skipping copy.");
                return;
            }

            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning($"[SorollaSDK] Source link.xml not found at {sourcePath}");
                return;
            }

            try
            {
                File.Copy(sourcePath, destPath);
                AssetDatabase.Refresh();
                Debug.Log($"[SorollaSDK] Copied link.xml to {destPath} for IL2CPP stripping protection.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SorollaSDK] Failed to copy link.xml: {e.Message}");
            }
        }
    }
}
