using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace SorollaPalette.Editor
{
    [InitializeOnLoad]
    public static class SorollaPaletteSetup
    {
        private const string SETUP_COMPLETE_KEY = "SorollaPalette_SetupComplete";
        private const string OPENUPM_REGISTRY_URL = "https://package.openupm.com";
        private const string GOOGLE_REGISTRY_URL = "https://unityregistry-pa.googleapis.com/";
        private const string APPLOVIN_REGISTRY_URL = "https://unity.packages.applovin.com/";

        private static AddRequest _GameAnalyticsRequest;
        private static AddRequest _EdmRequest;
        private static AddRequest _MaxRequest;
        private static int _PackagesAdded;

        static SorollaPaletteSetup()
        {
            // Run setup once when package is first imported
            if (!SessionState.GetBool(SETUP_COMPLETE_KEY, false)) EditorApplication.delayCall += RunSetup;
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
            var registriesAdded = AddScopedRegistriesToManifest();

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
            var manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");

            try
            {
                var jsonContent = File.ReadAllText(manifestPath);
                var manifest = MiniJson.Deserialize(jsonContent) as Dictionary<string, object>;

                if (manifest == null || !manifest.ContainsKey("dependencies"))
                    return true;

                var dependencies = manifest["dependencies"] as Dictionary<string, object>;

                var hasGA = dependencies.ContainsKey("com.gameanalytics.sdk");
                var hasEDM = dependencies.ContainsKey("com.google.external-dependency-manager");

                return !hasGA || !hasEDM;
            }
            catch
            {
                return true;
            }
        }

        private static void AddPackagesViaAPI()
        {
            _PackagesAdded = 0;

            // Add EDM first (GameAnalytics depends on it)
            _EdmRequest = Client.Add("com.google.external-dependency-manager");
            EditorApplication.update += CheckEDMProgress;
        }

        private static void CheckEDMProgress()
        {
            if (_EdmRequest == null || !_EdmRequest.IsCompleted)
                return;

            EditorApplication.update -= CheckEDMProgress;

            if (_EdmRequest.Status == StatusCode.Success)
            {
                Debug.Log("[Sorolla Palette] External Dependency Manager added successfully.");
                _PackagesAdded++;

                // Now add GameAnalytics SDK
                _GameAnalyticsRequest = Client.Add("com.gameanalytics.sdk@7.10.6");
                EditorApplication.update += CheckGameAnalyticsProgress;
            }
            else if (_EdmRequest.Status >= StatusCode.Failure)
            {
                Debug.LogError(
                    $"[Sorolla Palette] Failed to add External Dependency Manager: {_EdmRequest.Error.message}");
            }

            _EdmRequest = null;
        }

        private static void CheckGameAnalyticsProgress()
        {
            if (_GameAnalyticsRequest == null || !_GameAnalyticsRequest.IsCompleted)
                return;

            EditorApplication.update -= CheckGameAnalyticsProgress;

            if (_GameAnalyticsRequest.Status == StatusCode.Success)
            {
                Debug.Log("[Sorolla Palette] GameAnalytics SDK added successfully.");
                _PackagesAdded++;
                Debug.Log($"[Sorolla Palette] Setup complete. {_PackagesAdded} package(s) added.");
            }
            else if (_GameAnalyticsRequest.Status >= StatusCode.Failure)
            {
                Debug.LogError(
                    $"[Sorolla Palette] Failed to add GameAnalytics SDK: {_GameAnalyticsRequest.Error.message}");
            }

            _GameAnalyticsRequest = null;
        }

        private static bool AddScopedRegistriesToManifest()
        {
            return ModifyManifest((manifest, scopedRegistries) =>
            {
                var modified = false;
                if (AddOrUpdateRegistry(scopedRegistries, "Game Package Registry by Google", GOOGLE_REGISTRY_URL,
                        new[] { "com.google" }))
                    modified = true;
                if (AddOrUpdateRegistry(scopedRegistries, "package.openupm.com", OPENUPM_REGISTRY_URL,
                        new[] { "com.gameanalytics", "com.google.external-dependency-manager" }))
                    modified = true;
                return modified; // only write file if any registry actually changed
            });
        }

        /// <summary>
        ///     Installs AppLovin MAX SDK and its scoped registry - called by Configuration Window
        /// </summary>
        public static void InstallAppLovinMAX()
        {
            Debug.Log("[Sorolla Palette] Installing AppLovin MAX SDK...");

            var registryAdded = AddAppLovinRegistry();
            var dependencyAdded = AddAppLovinDependency();

            if (registryAdded || dependencyAdded)
            {
                Debug.Log("[Sorolla Palette] AppLovin MAX added to manifest. Triggering Package Manager resolve...");

                _MaxRequest = Client.Add("com.applovin.mediation.ads@8.5.0");
                EditorApplication.update += CheckMaxInstallProgress;
            }
            else
            {
                Debug.Log("[Sorolla Palette] AppLovin MAX is already installed.");
            }
        }

        /// <summary>
        ///     Installs Adjust SDK via UPM - called by Mode Selector for Full Mode
        /// </summary>
        public static void InstallAdjustSDK()
        {
            Debug.Log("[Sorolla Palette] Installing Adjust SDK...");

            // Adjust SDK is available via git URL: https://github.com/adjust/unity_sdk.git?path=Assets/Adjust
            var adjustRequest = Client.Add("https://github.com/adjust/unity_sdk.git?path=Assets/Adjust");
            EditorApplication.update += () => CheckAdjustInstallProgress(adjustRequest);
        }

        private static void CheckAdjustInstallProgress(AddRequest request)
        {
            if (request == null || !request.IsCompleted)
                return;

            EditorApplication.update -= () => CheckAdjustInstallProgress(request);

            if (request.Status == StatusCode.Success)
                Debug.Log("[Sorolla Palette] Adjust SDK installed successfully!");
            else if (request.Status >= StatusCode.Failure)
                Debug.LogError($"[Sorolla Palette] Adjust SDK installation failed: {request.Error.message}");
        }

        private static void CheckMaxInstallProgress()
        {
            if (_MaxRequest == null || !_MaxRequest.IsCompleted)
                return;

            EditorApplication.update -= CheckMaxInstallProgress;

            if (_MaxRequest.Status == StatusCode.Success)
                Debug.Log("[Sorolla Palette] AppLovin MAX SDK installed successfully!");
            else if (_MaxRequest.Status >= StatusCode.Failure)
                Debug.LogError($"[Sorolla Palette] AppLovin MAX installation failed: {_MaxRequest.Error.message}");

            _MaxRequest = null;
        }

        private static bool AddAppLovinRegistry()
        {
            return ModifyManifest((manifest, scopedRegistries) =>
            {
                return AddOrUpdateRegistry(scopedRegistries, "AppLovin MAX Unity", APPLOVIN_REGISTRY_URL,
                    new[]
                    {
                        "com.applovin.mediation.ads", "com.applovin.mediation.adapters", "com.applovin.mediation.dsp"
                    });
            });
        }

        private static bool AddAppLovinDependency()
        {
            return AddDependencies(new Dictionary<string, string>
            {
                { "com.applovin.mediation.ads", "8.5.0" }
            });
        }

        // DRY Helper Methods

        private static bool AddOrUpdateRegistry(List<object> scopedRegistries, string name, string url,
            string[] requiredScopes)
        {
            Dictionary<string, object> registry = null;
            var exists = false;

            // Find existing registry
            foreach (var reg in scopedRegistries)
            {
                var r = reg as Dictionary<string, object>;
                if (r != null && r.ContainsKey("url") && r["url"].ToString() == url)
                {
                    registry = r;
                    exists = true;
                    break;
                }
            }

            // Create new registry if doesn't exist
            if (!exists)
            {
                registry = new Dictionary<string, object>
                {
                    { "name", name },
                    { "url", url },
                    { "scopes", new List<object>(requiredScopes) }
                };
                scopedRegistries.Add(registry);
                Debug.Log($"[Sorolla Palette] Added {name} registry to manifest.json");
                return true;
            }

            // Update existing registry scopes
            if (registry.ContainsKey("scopes"))
            {
                var scopes = registry["scopes"] as List<object>;
                if (scopes != null)
                {
                    var modified = false;
                    foreach (var scope in requiredScopes)
                        if (!scopes.Contains(scope))
                        {
                            scopes.Add(scope);
                            modified = true;
                        }

                    if (modified)
                    {
                        Debug.Log($"[Sorolla Palette] Updated {name} registry scopes");
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ModifyManifest(Func<Dictionary<string, object>, List<object>, bool> modifier)
        {
            var manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");

            if (!File.Exists(manifestPath))
            {
                Debug.LogError("[Sorolla Palette] manifest.json not found!");
                return false;
            }

            try
            {
                var jsonContent = File.ReadAllText(manifestPath);
                var manifest = MiniJson.Deserialize(jsonContent) as Dictionary<string, object>;

                if (manifest == null)
                {
                    Debug.LogError("[Sorolla Palette] Failed to parse manifest.json");
                    return false;
                }

                // Get or create scopedRegistries array
                if (!manifest.ContainsKey("scopedRegistries")) manifest["scopedRegistries"] = new List<object>();

                var scopedRegistries = manifest["scopedRegistries"] as List<object>;

                if (modifier(manifest, scopedRegistries))
                {
                    var updatedJson = MiniJson.Serialize(manifest, true);
                    File.WriteAllText(manifestPath, updatedJson);
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Sorolla Palette] Error modifying manifest.json: {e.Message}");
                return false;
            }
        }

        private static bool AddDependencies(Dictionary<string, string> packagesToAdd)
        {
            var manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");

            try
            {
                var jsonContent = File.ReadAllText(manifestPath);
                var manifest = MiniJson.Deserialize(jsonContent) as Dictionary<string, object>;

                if (manifest == null || !manifest.ContainsKey("dependencies")) return false;

                var dependencies = manifest["dependencies"] as Dictionary<string, object>;
                var modified = false;

                foreach (var package in packagesToAdd)
                    if (!dependencies.ContainsKey(package.Key))
                    {
                        dependencies[package.Key] = package.Value;
                        modified = true;
                        Debug.Log($"[Sorolla Palette] Added {package.Key} dependency");
                    }

                if (modified)
                {
                    var updatedJson = MiniJson.Serialize(manifest, true);
                    File.WriteAllText(manifestPath, updatedJson);
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Sorolla Palette] Error adding dependencies: {e.Message}");
                return false;
            }
        }
    }
}