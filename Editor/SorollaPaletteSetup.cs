using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.IO;
using System.Collections.Generic;

namespace SorollaPalette.Editor
{
    [InitializeOnLoad]
    public static class SorollaPaletteSetup
    {
        private const string SETUP_COMPLETE_KEY = "SorollaPalette_SetupComplete";
        private const string OPENUPM_REGISTRY_URL = "https://package.openupm.com";
        private const string GOOGLE_REGISTRY_URL = "https://unityregistry-pa.googleapis.com/";
        
        private static AddRequest gameAnalyticsRequest;
        private static AddRequest edmRequest;
        private static int packagesAdded;
        
        static SorollaPaletteSetup()
        {
            // Run setup once when package is first imported
            if (!SessionState.GetBool(SETUP_COMPLETE_KEY, false))
            {
                EditorApplication.delayCall += RunSetup;
            }
        }
        
        [MenuItem("Tools/Sorolla Palette/Run Setup (Force)")]
        public static void ForceRunSetup()
        {
            SessionState.SetBool(SETUP_COMPLETE_KEY, false);
            RunSetup();
        }
        
        private static void RunSetup()
        {
            SessionState.SetBool(SETUP_COMPLETE_KEY, true);
            
            Debug.Log("[Sorolla Palette] Running initial setup...");
            
            // First, add scoped registries to manifest (required before adding packages)
            bool registriesAdded = AddScopedRegistriesToManifest();
            
            // Then check if packages need to be added
            if (NeedToAddPackages())
            {
                Debug.Log("[Sorolla Palette] Adding package dependencies via Package Manager...");
                AddPackagesViaAPI();
            }
            else if (registriesAdded)
            {
                Debug.Log("[Sorolla Palette] Registries added. All dependencies already configured.");
            }
            else
            {
                Debug.Log("[Sorolla Palette] All dependencies already configured.");
            }
        }
        
        private static bool NeedToAddPackages()
        {
            // Check if packages are already in manifest
            string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            
            try
            {
                string jsonContent = File.ReadAllText(manifestPath);
                var manifest = MiniJson.Deserialize(jsonContent) as Dictionary<string, object>;
                
                if (manifest == null || !manifest.ContainsKey("dependencies"))
                    return true;
                
                var dependencies = manifest["dependencies"] as Dictionary<string, object>;
                
                bool hasGA = dependencies.ContainsKey("com.gameanalytics.sdk");
                bool hasEDM = dependencies.ContainsKey("com.google.external-dependency-manager");
                
                return !hasGA || !hasEDM;
            }
            catch
            {
                return true;
            }
        }
        
        private static void AddPackagesViaAPI()
        {
            packagesAdded = 0;
            
            // Add GameAnalytics SDK
            gameAnalyticsRequest = Client.Add("com.gameanalytics.sdk@7.10.6");
            EditorApplication.update += CheckGameAnalyticsProgress;
        }
        
        private static void CheckGameAnalyticsProgress()
        {
            if (gameAnalyticsRequest == null || !gameAnalyticsRequest.IsCompleted)
                return;
            
            EditorApplication.update -= CheckGameAnalyticsProgress;
            
            if (gameAnalyticsRequest.Status == StatusCode.Success)
            {
                Debug.Log("[Sorolla Palette] GameAnalytics SDK added successfully.");
                packagesAdded++;
                
                // Now add EDM
                edmRequest = Client.Add("com.google.external-dependency-manager");
                EditorApplication.update += CheckEDMProgress;
            }
            else if (gameAnalyticsRequest.Status >= StatusCode.Failure)
            {
                Debug.LogError($"[Sorolla Palette] Failed to add GameAnalytics SDK: {gameAnalyticsRequest.Error.message}");
            }
            
            gameAnalyticsRequest = null;
        }
        
        private static void CheckEDMProgress()
        {
            if (edmRequest == null || !edmRequest.IsCompleted)
                return;
            
            EditorApplication.update -= CheckEDMProgress;
            
            if (edmRequest.Status == StatusCode.Success)
            {
                Debug.Log("[Sorolla Palette] External Dependency Manager added successfully.");
                packagesAdded++;
                Debug.Log($"[Sorolla Palette] Setup complete. {packagesAdded} package(s) added.");
            }
            else if (edmRequest.Status >= StatusCode.Failure)
            {
                Debug.LogError($"[Sorolla Palette] Failed to add External Dependency Manager: {edmRequest.Error.message}");
            }
            
            edmRequest = null;
        }
        
        private static bool AddScopedRegistriesToManifest()
        {
            string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            
            if (!File.Exists(manifestPath))
            {
                Debug.LogError("[Sorolla Palette] manifest.json not found!");
                return false;
            }
            
            try
            {
                string jsonContent = File.ReadAllText(manifestPath);
                var manifest = MiniJson.Deserialize(jsonContent) as Dictionary<string, object>;
                
                if (manifest == null)
                {
                    Debug.LogError("[Sorolla Palette] Failed to parse manifest.json");
                    return false;
                }
                
                // Get or create scopedRegistries array
                List<object> scopedRegistries;
                if (manifest.ContainsKey("scopedRegistries"))
                {
                    scopedRegistries = manifest["scopedRegistries"] as List<object>;
                }
                else
                {
                    scopedRegistries = new List<object>();
                    manifest["scopedRegistries"] = scopedRegistries;
                }
                
                // Check if Google registry already exists
                bool hasGoogle = false;
                Dictionary<string, object> googleRegistry = null;
                
                // Check if OpenUPM registry already exists
                bool hasOpenUPM = false;
                Dictionary<string, object> openUpmRegistry = null;
                
                foreach (var reg in scopedRegistries)
                {
                    var registry = reg as Dictionary<string, object>;
                    if (registry != null && registry.ContainsKey("url"))
                    {
                        string url = registry["url"].ToString();
                        if (url == GOOGLE_REGISTRY_URL)
                        {
                            hasGoogle = true;
                            googleRegistry = registry;
                        }
                        else if (url == OPENUPM_REGISTRY_URL)
                        {
                            hasOpenUPM = true;
                            openUpmRegistry = registry;
                        }
                    }
                }
                
                // Add or update Google registry
                if (!hasGoogle)
                {
                    googleRegistry = new Dictionary<string, object>
                    {
                        { "name", "Game Package Registry by Google" },
                        { "url", GOOGLE_REGISTRY_URL },
                        { "scopes", new List<object> { "com.google" } }
                    };
                    scopedRegistries.Add(googleRegistry);
                    Debug.Log("[Sorolla Palette] Added Google registry to manifest.json");
                }
                else
                {
                    // Update existing Google registry scopes
                    if (googleRegistry.ContainsKey("scopes"))
                    {
                        var scopes = googleRegistry["scopes"] as List<object>;
                        if (scopes != null && !scopes.Contains("com.google"))
                        {
                            scopes.Add("com.google");
                            Debug.Log("[Sorolla Palette] Updated Google registry scopes");
                        }
                    }
                }
                
                // Add or update OpenUPM registry
                if (!hasOpenUPM)
                {
                    // Create new OpenUPM registry
                    openUpmRegistry = new Dictionary<string, object>
                    {
                        { "name", "package.openupm.com" },
                        { "url", OPENUPM_REGISTRY_URL },
                        { "scopes", new List<object> { "com.gameanalytics", "com.google.external-dependency-manager" } }
                    };
                    scopedRegistries.Add(openUpmRegistry);
                    Debug.Log("[Sorolla Palette] Added OpenUPM registry to manifest.json");
                }
                else
                {
                    // Update existing OpenUPM registry scopes
                    if (openUpmRegistry.ContainsKey("scopes"))
                    {
                        var scopes = openUpmRegistry["scopes"] as List<object>;
                        if (scopes != null)
                        {
                            bool modified = false;
                            
                            if (!scopes.Contains("com.gameanalytics"))
                            {
                                scopes.Add("com.gameanalytics");
                                modified = true;
                            }
                            
                            if (!scopes.Contains("com.google.external-dependency-manager"))
                            {
                                scopes.Add("com.google.external-dependency-manager");
                                modified = true;
                            }
                            
                            if (modified)
                            {
                                Debug.Log("[Sorolla Palette] Updated OpenUPM registry scopes in manifest.json");
                            }
                            else
                            {
                                Debug.Log("[Sorolla Palette] OpenUPM registry already configured correctly");
                            }
                        }
                    }
                }
                
                // Write back to file with pretty formatting
                string updatedJson = MiniJson.Serialize(manifest, prettyPrint: true);
                File.WriteAllText(manifestPath, updatedJson);
                
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Sorolla Palette] Error modifying manifest.json: {e.Message}");
                return false;
            }
        }
    }

}
