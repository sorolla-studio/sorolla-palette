using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace SorollaPalette.Editor
{
    /// <summary>
    /// KISS installation manager - synchronous installation with clear error handling
    /// Eliminates complex async progress tracking
    /// </summary>
    public static class InstallationManager
    {
        private static AddRequest _currentRequest;

        /// <summary>
        /// Synchronous package installation with clear error handling
        /// </summary>
        public static bool InstallPackage(string packageId, string displayName = null)
        {
            if (string.IsNullOrEmpty(displayName))
                displayName = packageId;

            try
            {
                Debug.Log($"[InstallationManager] Installing {displayName}...");

                _currentRequest = Client.Add(packageId);

                // Wait synchronously for completion
                var startTime = EditorApplication.timeSinceStartup;
                const double timeout = 30.0; // 30 second timeout

                while (!_currentRequest.IsCompleted)
                {
                    if (EditorApplication.timeSinceStartup - startTime > timeout)
                    {
                        Debug.LogError($"[InstallationManager] Timeout installing {displayName}");
                        return false;
                    }

                    // Allow Unity to process events
                    System.Threading.Thread.Sleep(10);
                }

                if (_currentRequest.Status == StatusCode.Success)
                {
                    Debug.Log($"[InstallationManager] Successfully installed {displayName}");
                    return true;
                }
                else
                {
                    Debug.LogError($"[InstallationManager] Failed to install {displayName}: {_currentRequest.Error.message}");
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[InstallationManager] Exception installing {displayName}: {e.Message}");
                return false;
            }
            finally
            {
                _currentRequest = null;
            }
        }

        /// <summary>
        /// Install AppLovin MAX with registry and dependency
        /// </summary>
        public static bool InstallAppLovinMAX()
        {
            Debug.Log("[InstallationManager] Installing AppLovin MAX SDK...");

            // Add registry
            var registryAdded = ManifestManager.AddOrUpdateRegistry(
                "AppLovin MAX Unity",
                "https://unity.packages.applovin.com/",
                new[] { "com.applovin.mediation.ads", "com.applovin.mediation.adapters", "com.applovin.mediation.dsp" }
            );

            // Add dependency
            var dependencyAdded = ManifestManager.AddDependencies(new System.Collections.Generic.Dictionary<string, string>
            {
                { "com.applovin.mediation.ads", "8.5.0" }
            });

            if (registryAdded || dependencyAdded)
            {
                Debug.Log("[InstallationManager] AppLovin MAX added to manifest. Installing package...");
                return InstallPackage("com.applovin.mediation.ads@8.5.0", "AppLovin MAX");
            }
            else
            {
                Debug.Log("[InstallationManager] AppLovin MAX is already installed.");
                return true;
            }
        }

        /// <summary>
        /// Install Adjust SDK
        /// </summary>
        public static bool InstallAdjustSDK()
        {
            Debug.Log("[InstallationManager] Installing Adjust SDK...");
            return InstallPackage("https://github.com/adjust/unity_sdk.git?path=Assets/Adjust", "Adjust SDK");
        }

        /// <summary>
        /// Install GameAnalytics SDK
        /// </summary>
        public static bool InstallGameAnalytics()
        {
            Debug.Log("[InstallationManager] Installing GameAnalytics SDK...");
            return InstallPackage("com.gameanalytics.sdk@7.10.6", "GameAnalytics SDK");
        }

        /// <summary>
        /// Install External Dependency Manager
        /// </summary>
        public static bool InstallExternalDependencyManager()
        {
            Debug.Log("[InstallationManager] Installing External Dependency Manager...");
            return InstallPackage("com.google.external-dependency-manager", "External Dependency Manager");
        }
    }
}