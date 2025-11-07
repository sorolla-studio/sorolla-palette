using UnityEngine;
using UnityEditor;
using System.IO;

namespace SorollaPalette.Editor
{
    /// <summary>
    /// Testing utilities for rapid package development workflow
    /// </summary>
    public static class SorollaPaletteTestingTools
    {
        [MenuItem("Tools/Sorolla Palette/Testing/Reset Package State")]
        public static void ResetPackageState()
        {
            SessionState.SetBool("SorollaPalette_SetupComplete", false);
            Debug.Log("[Sorolla Testing] Package state reset. Setup will run on next domain reload or manual trigger.");
        }
        
        [MenuItem("Tools/Sorolla Palette/Testing/Clear Manifest Changes")]
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
                "- Sorolla Palette registries\n" +
                "- GameAnalytics SDK dependency\n" +
                "- External Dependency Manager\n\n" +
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
                                       url == "https://package.openupm.com";
                            }
                            return false;
                        });
                        
                        if (scopedRegistries.Count == 0)
                        {
                            manifest.Remove("scopedRegistries");
                        }
                        
                        modified = true;
                    }
                }
                
                // Remove dependencies
                if (manifest.ContainsKey("dependencies"))
                {
                    var dependencies = manifest["dependencies"] as System.Collections.Generic.Dictionary<string, object>;
                    if (dependencies != null)
                    {
                        if (dependencies.Remove("com.gameanalytics.sdk"))
                        {
                            modified = true;
                            Debug.Log("[Sorolla Testing] Removed GameAnalytics SDK dependency");
                        }
                        
                        if (dependencies.Remove("com.google.external-dependency-manager"))
                        {
                            modified = true;
                            Debug.Log("[Sorolla Testing] Removed External Dependency Manager dependency");
                        }
                    }
                }
                
                if (modified)
                {
                    string updatedJson = MiniJson.Serialize(manifest, prettyPrint: true);
                    File.WriteAllText(manifestPath, updatedJson);
                    
                    Debug.Log("[Sorolla Testing] Manifest cleaned. Refreshing Asset Database...");
                    AssetDatabase.Refresh();
                    
                    EditorUtility.DisplayDialog(
                        "Manifest Cleared",
                        "Manifest changes have been removed.\n" +
                        "Package Manager is resolving...",
                        "OK"
                    );
                }
                else
                {
                    Debug.Log("[Sorolla Testing] No Sorolla-related entries found in manifest.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Sorolla Testing] Error clearing manifest: {e.Message}");
            }
        }
        
        [MenuItem("Tools/Sorolla Palette/Testing/Full Reset & Rerun")]
        public static void FullResetAndRerun()
        {
            Debug.Log("[Sorolla Testing] Starting full reset...");
            
            // Clear manifest
            ClearManifestChanges();
            
            // Wait a frame for the asset database to refresh
            EditorApplication.delayCall += () =>
            {
                // Reset state
                SessionState.SetBool("SorollaPalette_SetupComplete", false);
                
                // Force rerun
                EditorApplication.delayCall += () =>
                {
                    Debug.Log("[Sorolla Testing] Forcing setup rerun...");
                    SorollaPaletteSetup.ForceRunSetup();
                };
            };
        }
        
        [MenuItem("Tools/Sorolla Palette/Testing/Show Session State")]
        public static void ShowSessionState()
        {
            bool setupComplete = SessionState.GetBool("SorollaPalette_SetupComplete", false);
            
            EditorUtility.DisplayDialog(
                "Package Session State",
                $"Setup Complete: {setupComplete}\n\n" +
                $"(This state is cleared when Unity restarts or domain reloads)",
                "OK"
            );
        }
        
        [MenuItem("Tools/Sorolla Palette/Testing/Open Manifest")]
        public static void OpenManifest()
        {
            string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            
            if (File.Exists(manifestPath))
            {
                EditorUtility.RevealInFinder(manifestPath);
            }
            else
            {
                Debug.LogError("[Sorolla Testing] manifest.json not found!");
            }
        }
    }
}
