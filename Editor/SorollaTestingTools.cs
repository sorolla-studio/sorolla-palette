using UnityEngine;
using UnityEditor;
using System.IO;

namespace Sorolla.Editor
{
    /// <summary>
    ///     Testing utilities for rapid package development workflow
    /// </summary>
    public static class SorollaTestingTools
    {
        [MenuItem("Sorolla/Testing/Reset Package State")]
        public static void ResetPackageState()
        {
            var hash = Application.dataPath.GetHashCode();
            EditorPrefs.DeleteKey($"Sorolla_Setup_v3_{hash}");
            EditorPrefs.DeleteKey($"Sorolla_Mode_{hash}");
            Debug.Log("[Sorolla Testing] Package state reset.");
        }

        [MenuItem("Sorolla/Testing/Clear Manifest Changes")]
        public static void ClearManifestChanges()
        {
            string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");

            if (!File.Exists(manifestPath))
            {
                Debug.LogError("[Sorolla Testing] manifest.json not found!");
                return;
            }

            bool shouldClear = EditorUtility.DisplayDialog(
                "Clear Manifest Changes",
                "This will remove:\n" +
                "- Sorolla registries\n" +
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
                var manifest = MiniJson.Deserialize(jsonContent) as System.Collections.Generic.Dictionary<string, object>;

                if (manifest == null)
                {
                    Debug.LogError("[Sorolla Testing] Failed to parse manifest.json");
                    return;
                }

                bool modified = false;

                // Remove scoped registries
                if (manifest.ContainsKey("scopedRegistries"))
                {
                    var scopedRegistries = manifest["scopedRegistries"] as System.Collections.Generic.List<object>;
                    if (scopedRegistries != null)
                    {
                        scopedRegistries.RemoveAll(reg =>
                        {
                            var registry = reg as System.Collections.Generic.Dictionary<string, object>;
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
                    var dependencies = manifest["dependencies"] as System.Collections.Generic.Dictionary<string, object>;
                    if (dependencies != null)
                    {
                        var toRemove = new[] {
                            "com.gameanalytics.sdk",
                            "com.google.external-dependency-manager",
                            "com.applovin.mediation.ads"
                        };

                        foreach (var dep in toRemove)
                        {
                            if (dependencies.Remove(dep))
                            {
                                modified = true;
                                Debug.Log($"[Sorolla Testing] Removed {dep}");
                            }
                        }
                    }
                }

                if (modified)
                {
                    string updatedJson = MiniJson.Serialize(manifest, prettyPrint: true);
                    File.WriteAllText(manifestPath, updatedJson);

                    Debug.Log("[Sorolla Testing] Manifest cleaned. Refreshing...");
                    AssetDatabase.Refresh();

                    EditorUtility.DisplayDialog("Manifest Cleared", "Manifest changes removed.", "OK");
                }
                else
                {
                    Debug.Log("[Sorolla Testing] No Sorolla entries found in manifest.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Sorolla Testing] Error: {e.Message}");
            }
        }

        [MenuItem("Sorolla/Testing/Full Reset & Rerun")]
        public static void FullResetAndRerun()
        {
            Debug.Log("[Sorolla Testing] Starting full reset...");
            ClearManifestChanges();

            EditorApplication.delayCall += () =>
            {
                ResetPackageState();
                EditorApplication.delayCall += () =>
                {
                    Debug.Log("[Sorolla Testing] Forcing setup rerun...");
                    SorollaSetup.ForceRunSetup();
                };
            };
        }

        [MenuItem("Sorolla/Testing/Show State")]
        public static void ShowSessionState()
        {
            var mode = SorollaSettings.Mode;

            EditorUtility.DisplayDialog(
                "Sorolla State",
                $"Mode: {mode}\n" +
                $"Is Configured: {SorollaSettings.IsConfigured}\n\n" +
                "(Stored in EditorPrefs)",
                "OK"
            );
        }

        [MenuItem("Sorolla/Testing/Open Manifest")]
        public static void OpenManifest()
        {
            string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            if (File.Exists(manifestPath))
                EditorUtility.RevealInFinder(manifestPath);
            else
                Debug.LogError("[Sorolla Testing] manifest.json not found!");
        }
    }
}
