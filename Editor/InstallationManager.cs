using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace SorollaPalette.Editor
{
    /// <summary>
    ///     Non-blocking installation manager with queue system and progress bar.
    ///     Uses EditorApplication.update to poll requests without freezing the Editor.
    /// </summary>
    [InitializeOnLoad]
    public static class InstallationManager
    {
        private enum OperationType
        {
            Install,
            Uninstall
        }

        private class PackageOperation
        {
            public OperationType Type;
            public string PackageId;
            public string DisplayName;
        }

        private static readonly Queue<PackageOperation> _operationQueue = new Queue<PackageOperation>();
        private static Request _currentRequest;
        private static PackageOperation _currentOperation;

        // Removed static constructor to avoid unconditional update loop
        // static InstallationManager()
        // {
        //     EditorApplication.update += Update;
        // }

        private static void Update()
        {
            if (_currentRequest == null && _operationQueue.Count > 0)
            {
                ProcessNextOperation();
            }
            else if (_currentRequest != null)
            {
                if (_currentRequest.IsCompleted)
                {
                    HandleRequestCompletion();
                }
                else
                {
                    // Update Progress Bar
                    var progress = _operationQueue.Count > 0 
                        ? $"Processing... ({_operationQueue.Count} remaining)" 
                        : "Processing...";
                    
                    EditorUtility.DisplayProgressBar("Sorolla Palette", 
                        $"{(_currentOperation.Type == OperationType.Install ? "Installing" : "Uninstalling")} {_currentOperation.DisplayName}...", 
                        0.5f);
                }
            }
            else
            {
                // No requests and empty queue, stop updating
                EditorApplication.update -= Update;
                EditorUtility.ClearProgressBar();
            }
        }

        private static void ProcessNextOperation()
        {
            _currentOperation = _operationQueue.Dequeue();
            
            if (_currentOperation.Type == OperationType.Install)
            {
                Debug.Log($"[InstallationManager] Starting installation of {_currentOperation.DisplayName}...");
                _currentRequest = Client.Add(_currentOperation.PackageId);
            }
            else
            {
                Debug.Log($"[InstallationManager] Starting uninstallation of {_currentOperation.DisplayName}...");
                _currentRequest = Client.Remove(_currentOperation.PackageId);
            }
        }

        private static void HandleRequestCompletion()
        {
            var operationName = _currentOperation.Type == OperationType.Install ? "Installation" : "Uninstallation";

            if (_currentRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[InstallationManager] {operationName} of {_currentOperation.DisplayName} successful.");
                
                // If we just installed something, try to resolve dependencies (EDM)
                if (_currentOperation.Type == OperationType.Install)
                {
                    ResolveDependencies();
                }
            }
            else if (_currentRequest.Status >= StatusCode.Failure)
            {
                Debug.LogError($"[InstallationManager] {operationName} of {_currentOperation.DisplayName} failed: {_currentRequest.Error.message}");
            }

            _currentRequest = null;
            _currentOperation = null;

            // If queue is empty, clear progress bar
            if (_operationQueue.Count == 0)
            {
                EditorUtility.ClearProgressBar();
                // Refresh assets to ensure defines and scripts are updated
                AssetDatabase.Refresh();
            }
        }

        private static void ResolveDependencies()
        {
            // Use reflection to call Google.JarResolver.PlayServicesResolver.Resolve
            // This avoids a hard dependency on the EDM package which might not be compiled yet
            try
            {
                var resolverType = Type.GetType("Google.JarResolver.PlayServicesResolver, Google.JarResolver");
                if (resolverType != null)
                {
                    var resolveMethod = resolverType.GetMethod("Resolve", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (resolveMethod != null)
                    {
                        Debug.Log("[InstallationManager] Triggering External Dependency Manager Resolution...");
                        resolveMethod.Invoke(null, new object[] { null, null, true });
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InstallationManager] Could not auto-resolve dependencies: {e.Message}");
            }
        }

        /// <summary>
        ///     Queue a package for installation
        /// </summary>
        public static void InstallPackage(string packageId, string displayName = null)
        {
            if (string.IsNullOrEmpty(displayName)) displayName = packageId;
            
            _operationQueue.Enqueue(new PackageOperation
            {
                Type = OperationType.Install,
                PackageId = packageId,
                DisplayName = displayName
            });
            
            // Ensure update loop is running
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        /// <summary>
        ///     Queue a package for uninstallation
        /// </summary>
        public static void UninstallPackage(string packageId, string displayName = null)
        {
            if (string.IsNullOrEmpty(displayName)) displayName = packageId;

            _operationQueue.Enqueue(new PackageOperation
            {
                Type = OperationType.Uninstall,
                PackageId = packageId,
                DisplayName = displayName
            });

            // Ensure update loop is running
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        // --- Specific SDK Helpers (Using Constants) ---

        public static void InstallAppLovinMAX()
        {
            // Add registry logic is synchronous/instant usually, but let's keep it simple
            // For MAX, we need to add registry first. 
            // Since this is a specific complex flow, we'll do the registry part immediately (it's fast)
            // and queue the package install.
            
            Debug.Log("[InstallationManager] Setting up AppLovin MAX Registry...");
            var registryAdded = ManifestManager.AddOrUpdateRegistry(
                "AppLovin MAX Unity",
                "https://unity.packages.applovin.com/",
                new[] { "com.applovin.mediation.ads", "com.applovin.mediation.adapters", "com.applovin.mediation.dsp" }
            );

            var dependencyAdded = ManifestManager.AddDependencies(new Dictionary<string, string>
            {
                { "com.applovin.mediation.ads", "8.5.0" }
            });

            if (registryAdded || dependencyAdded)
            {
                InstallPackage("com.applovin.mediation.ads@8.5.0", "AppLovin MAX");
            }
            else
            {
                Debug.Log("[InstallationManager] AppLovin MAX is already configured.");
            }
        }
        
        public static void UninstallAppLovinMAX()
        {
             // For uninstallation, we just remove the package. 
             // Removing registry entries is complex and usually not required, but we can remove the package.
             UninstallPackage("com.applovin.mediation.ads", "AppLovin MAX");
        }

        public static void InstallFacebookSDK()
        {
            InstallPackage(SorollaConstants.UrlFacebook, "Facebook SDK");
        }

        public static void UninstallFacebookSDK()
        {
            UninstallPackage(SorollaConstants.PackageIdFacebook, "Facebook SDK");
        }

        public static void InstallAdjustSDK()
        {
            InstallPackage(SorollaConstants.UrlAdjust + "?path=Assets/Adjust", "Adjust SDK");
        }

        public static void UninstallAdjustSDK()
        {
            UninstallPackage(SorollaConstants.PackageIdAdjust, "Adjust SDK");
        }

        public static void InstallGameAnalytics()
        {
            InstallPackage(SorollaConstants.UrlGameAnalytics, "GameAnalytics SDK"); // Using URL from constants if available, or ID
            // Note: Constants might need update if I used ID before. Checking Constants...
            // Constants had UrlGameAnalytics.
        }
        
        public static void InstallExternalDependencyManager()
        {
            InstallPackage(SorollaConstants.PackageIdExternalDependencyManager, "External Dependency Manager");
        }
    }
}