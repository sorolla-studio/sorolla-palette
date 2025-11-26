using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Sorolla.Editor
{
    /// <summary>
    ///     Non-blocking package installation manager.
    /// </summary>
    public static class SdkInstaller
    {
        private static readonly Queue<PackageOp> s_queue = new();
        private static Request s_request;
        private static PackageOp s_current;
        private static double s_startTime;
        private const double Timeout = 120.0;

        private enum OpType { Install, Uninstall }
        private class PackageOp { public OpType Type; public string PackageId; public string Name; }

        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
            EditorUtility.ClearProgressBar();
            s_queue.Clear();
            s_request = null;
            s_current = null;
        }

        /// <summary>
        ///     Install an SDK
        /// </summary>
        public static void Install(SdkId id)
        {
            if (!SdkRegistry.All.TryGetValue(id, out var info))
            {
                Debug.LogError($"[Sorolla] Unknown SDK: {id}");
                return;
            }

            // Special handling for MAX (needs registry)
            if (id == SdkId.AppLovinMAX)
            {
                InstallAppLovinMAX();
                return;
            }

            var packageRef = !string.IsNullOrEmpty(info.InstallUrl) ? info.InstallUrl : info.PackageId;
            Enqueue(OpType.Install, packageRef, info.Name);
        }

        /// <summary>
        ///     Uninstall an SDK
        /// </summary>
        public static void Uninstall(SdkId id)
        {
            if (!SdkRegistry.All.TryGetValue(id, out var info))
            {
                Debug.LogError($"[Sorolla] Unknown SDK: {id}");
                return;
            }

            Enqueue(OpType.Uninstall, info.PackageId, info.Name);
        }

        /// <summary>
        ///     Install all SDKs required for a mode (that aren't already installed)
        /// </summary>
        public static void InstallRequiredSdks(bool isPrototype)
        {
            foreach (var sdk in SdkRegistry.GetRequired(isPrototype))
            {
                if (!SdkDetector.IsInstalled(sdk))
                {
                    Debug.Log($"[Sorolla] Installing required SDK: {sdk.Name}");
                    Install(sdk.Id);
                }
            }
        }

        /// <summary>
        ///     Uninstall SDKs not needed for a mode
        /// </summary>
        public static void UninstallUnnecessarySdks(bool isPrototype)
        {
            foreach (var sdk in SdkRegistry.GetToUninstall(isPrototype))
            {
                if (SdkDetector.IsInstalled(sdk))
                {
                    Debug.Log($"[Sorolla] Uninstalling unnecessary SDK: {sdk.Name}");
                    Uninstall(sdk.Id);
                }
            }
        }

        /// <summary>
        ///     Install core dependencies (called on package import).
        ///     Order matters: EDM must be installed before GA (GA depends on EDM).
        /// </summary>
        public static void InstallCoreDependencies()
        {
            // Priority order: EDM first, then others that depend on it
            var priorityOrder = new[] { SdkId.ExternalDependencyManager, SdkId.IosSupport, SdkId.GameAnalytics };

            foreach (var id in priorityOrder)
            {
                if (SdkRegistry.All.TryGetValue(id, out var sdk) && 
                    sdk.Requirement == SdkRequirement.Core && 
                    !SdkDetector.IsInstalled(sdk))
                {
                    Debug.Log($"[Sorolla] Installing core dependency: {sdk.Name}");
                    Install(sdk.Id);
                }
            }
        }

        private static void InstallAppLovinMAX()
        {
            Debug.Log("[Sorolla] Setting up AppLovin MAX Registry...");
            ManifestManager.AddOrUpdateRegistry(
                "AppLovin MAX Unity",
                "https://unity.packages.applovin.com/",
                new[] { "com.applovin.mediation.ads", "com.applovin.mediation.adapters", "com.applovin.mediation.dsp" }
            );

            ManifestManager.AddDependencies(new Dictionary<string, string>
            {
                { "com.applovin.mediation.ads", "8.5.0" }
            });

            Enqueue(OpType.Install, "com.applovin.mediation.ads@8.5.0", "AppLovin MAX");
        }

        private static void Enqueue(OpType type, string packageId, string name)
        {
            s_queue.Enqueue(new PackageOp { Type = type, PackageId = packageId, Name = name });
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (s_request == null && s_queue.Count > 0)
            {
                ProcessNext();
            }
            else if (s_request != null)
            {
                if (EditorApplication.timeSinceStartup - s_startTime > Timeout)
                {
                    Debug.LogError($"[Sorolla] Timeout: {s_current.Name}");
                    Complete();
                    return;
                }

                if (s_request.IsCompleted)
                {
                    HandleCompletion();
                }
                else
                {
                    var remaining = s_queue.Count > 0 ? $" ({s_queue.Count} remaining)" : "";
                    var action = s_current.Type == OpType.Install ? "Installing" : "Uninstalling";
                    EditorUtility.DisplayProgressBar("Sorolla", $"{action} {s_current.Name}...{remaining}", 0.5f);
                }
            }
            else
            {
                EditorApplication.update -= Update;
                EditorUtility.ClearProgressBar();
            }
        }

        private static void ProcessNext()
        {
            s_current = s_queue.Dequeue();
            s_startTime = EditorApplication.timeSinceStartup;

            Debug.Log($"[Sorolla] {(s_current.Type == OpType.Install ? "Installing" : "Uninstalling")} {s_current.Name}...");

            s_request = s_current.Type == OpType.Install
                ? Client.Add(s_current.PackageId)
                : Client.Remove(s_current.PackageId);
        }

        private static void HandleCompletion()
        {
            var action = s_current.Type == OpType.Install ? "Installation" : "Uninstallation";

            if (s_request.Status == StatusCode.Success)
            {
                Debug.Log($"[Sorolla] {action} of {s_current.Name} complete.");
                if (s_current.Type == OpType.Install)
                    TryResolveDependencies();
            }
            else if (s_request.Status >= StatusCode.Failure)
            {
                Debug.LogError($"[Sorolla] {action} of {s_current.Name} failed: {s_request.Error?.message}");
            }

            Complete();
        }

        private static void Complete()
        {
            s_request = null;
            s_current = null;

            if (s_queue.Count == 0)
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }
        }

        private static void TryResolveDependencies()
        {
            try
            {
                var resolverType = Type.GetType("Google.JarResolver.PlayServicesResolver, Google.JarResolver");
                var resolveMethod = resolverType?.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static);
                if (resolveMethod != null)
                {
                    Debug.Log("[Sorolla] Triggering EDM resolution...");
                    resolveMethod.Invoke(null, new object[] { null, null, true });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Sorolla] Could not auto-resolve dependencies: {e.Message}");
            }
        }
    }
}
