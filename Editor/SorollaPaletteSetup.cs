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

        private static AddRequest _AdjustRequest;

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
            var modified = ManifestManager.AddOrUpdateRegistry("Game Package Registry by Google", GOOGLE_REGISTRY_URL,
                new[] { "com.google" });
            if (ManifestManager.AddOrUpdateRegistry("package.openupm.com", OPENUPM_REGISTRY_URL,
                    new[] { "com.gameanalytics", "com.google.external-dependency-manager" }))
                modified = true;
            return modified;
        }

        /// <summary>
        ///     Installs AppLovin MAX SDK and its scoped registry - called by Configuration Window
        /// </summary>
        public static void InstallAppLovinMAX()
        {
            InstallationManager.InstallAppLovinMAX();
        }

        /// <summary>
        ///     Uninstalls AppLovin MAX package from manifest and resolves UPM
        /// </summary>
        public static void UninstallAppLovinMAX()
        {
            Debug.Log("[Sorolla Palette] Uninstalling AppLovin MAX SDK...");
            var removed = ManifestManager.RemoveDependencies(new[] { "com.applovin.mediation.ads" });
            if (removed)
            {
#if UNITY_EDITOR
                Client.Resolve();
#endif
                Debug.Log("[Sorolla Palette] AppLovin MAX removed. Resolving packages...");
            }
            else
            {
                Debug.Log("[Sorolla Palette] AppLovin MAX was not present in manifest.");
            }
        }

        /// <summary>
        ///     Installs Adjust SDK via UPM - called by Mode Selector for Full Mode
        /// </summary>
        public static void InstallAdjustSDK()
        {
            InstallationManager.InstallAdjustSDK();
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
            return ManifestManager.AddOrUpdateRegistry("AppLovin MAX Unity", APPLOVIN_REGISTRY_URL,
                new[]
                {
                    "com.applovin.mediation.ads", "com.applovin.mediation.adapters", "com.applovin.mediation.dsp"
                });
        }

        private static bool AddAppLovinDependency()
        {
            return ManifestManager.AddDependencies(new Dictionary<string, string>
            {
                { "com.applovin.mediation.ads", "8.5.0" }
            });
        }
    }
}