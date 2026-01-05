using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Testing utilities for rapid package development workflow
    /// </summary>
    public static class SorollaTestingTools
    {
        [MenuItem("Palette/Testing/Reset Package State")]
        public static void ResetPackageState()
        {
            int hash = Application.dataPath.GetHashCode();
            EditorPrefs.DeleteKey($"Sorolla_Setup_v3_{hash}");
            EditorPrefs.DeleteKey($"Sorolla_Mode_{hash}");
            Debug.Log("[Palette Testing] Package state reset.");
        }

        [MenuItem("Palette/Testing/Clear Manifest Changes")]
        public static void ClearManifestChanges()
        {
            string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");

            if (!File.Exists(manifestPath))
            {
                Debug.LogError("[Palette Testing] manifest.json not found!");
                return;
            }

            bool shouldClear = EditorUtility.DisplayDialog(
                "Clear Manifest Changes",
                "This will remove:\n" +
                "- Palette registries\n" +
                "- GameAnalytics SDK\n" +
                "- External Dependency Manager\n" +
                "- AppLovin MAX SDK\n\n" +
                "Continue?",
                "Yes, Clear",
                "Cancel"
            );

            if (!shouldClear)
                return;

            try
            {
                string jsonContent = File.ReadAllText(manifestPath);
                var manifest = MiniJson.Deserialize(jsonContent) as Dictionary<string, object>;

                if (manifest == null)
                {
                    Debug.LogError("[Palette Testing] Failed to parse manifest.json");
                    return;
                }

                bool modified = false;

                // Remove scoped registries
                if (manifest.ContainsKey("scopedRegistries"))
                {
                    var scopedRegistries = manifest["scopedRegistries"] as List<object>;
                    if (scopedRegistries != null)
                    {
                        scopedRegistries.RemoveAll(reg =>
                        {
                            var registry = reg as Dictionary<string, object>;
                            if (registry != null && registry.ContainsKey("url"))
                            {
                                string url = registry["url"].ToString();
                                return url == "https://unityregistry-pa.googleapis.com/" ||
                                       url == "https://package.openupm.com" ||
                                       url == "https://unity.packages.applovin.com/";
                            }
                            return false;
                        });

                        if (scopedRegistries.Count == 0)
                            manifest.Remove("scopedRegistries");

                        modified = true;
                    }
                }

                // Remove dependencies
                if (manifest.ContainsKey("dependencies"))
                {
                    var dependencies = manifest["dependencies"] as Dictionary<string, object>;
                    if (dependencies != null)
                    {
                        string[] toRemove =
                        {
                            "com.gameanalytics.sdk",
                            "com.google.external-dependency-manager",
                            "com.applovin.mediation.ads",
                        };

                        foreach (string dep in toRemove)
                        {
                            if (dependencies.Remove(dep))
                            {
                                modified = true;
                                Debug.Log($"[Palette Testing] Removed {dep}");
                            }
                        }
                    }
                }

                if (modified)
                {
                    string updatedJson = MiniJson.Serialize(manifest, true);
                    File.WriteAllText(manifestPath, updatedJson);

                    Debug.Log("[Palette Testing] Manifest cleaned. Refreshing...");
                    AssetDatabase.Refresh();

                    EditorUtility.DisplayDialog("Manifest Cleared", "Manifest changes removed.", "OK");
                }
                else
                {
                    Debug.Log("[Palette Testing] No Palette entries found in manifest.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Palette Testing] Error: {e.Message}");
            }
        }

        [MenuItem("Palette/Testing/Full Reset & Rerun")]
        public static void FullResetAndRerun()
        {
            Debug.Log("[Palette Testing] Starting full reset...");
            ClearManifestChanges();

            EditorApplication.delayCall += () =>
            {
                ResetPackageState();
                EditorApplication.delayCall += () =>
                {
                    Debug.Log("[Palette Testing] Forcing setup rerun...");
                    SorollaSetup.ForceRunSetup();
                };
            };
        }

        [MenuItem("Palette/Testing/Show State")]
        public static void ShowSessionState()
        {
            SorollaMode mode = SorollaSettings.Mode;

            EditorUtility.DisplayDialog(
                "Palette State",
                $"Mode: {mode}\n" +
                $"Is Configured: {SorollaSettings.IsConfigured}\n\n" +
                "(Stored in EditorPrefs)",
                "OK"
            );
        }

        [MenuItem("Palette/Testing/Open Manifest")]
        public static void OpenManifest()
        {
            string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            if (File.Exists(manifestPath))
                EditorUtility.RevealInFinder(manifestPath);
            else
                Debug.LogError("[Palette Testing] manifest.json not found!");
        }
    }
}
