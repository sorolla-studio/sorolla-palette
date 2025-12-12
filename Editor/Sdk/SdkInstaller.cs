using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Sorolla.Editor
{
    /// <summary>
    ///     SDK installation via manifest modification.
    ///     Uses manifest.json for reliable, order-independent installation.
    /// </summary>
    public static class SdkInstaller
    {
        /// <summary>
        ///     Install an SDK by adding it to manifest.json
        /// </summary>
        public static void Install(SdkId id)
        {
            if (!SdkRegistry.All.TryGetValue(id, out var info))
            {
                Debug.LogError($"[Sorolla] Unknown SDK: {id}");
                return;
            }

            Debug.Log($"[Sorolla] Installing {info.Name}...");

            // Add registry if needed (for MAX - uses its own registry, not OpenUPM)
            if (id == SdkId.AppLovinMAX)
            {
                ManifestManager.AddOrUpdateRegistry(
                    "AppLovin MAX",
                    "https://unity.packages.applovin.com/",
                    new[] { "com.applovin" }
                );
            }
            // Add scope to OpenUPM if needed (but not for MAX)
            else if (!string.IsNullOrEmpty(info.Scope))
            {
                ManifestManager.AddOrUpdateRegistry(
                    "package.openupm.com",
                    "https://package.openupm.com",
                    new[] { info.Scope }
                );
            }


            // Add dependency to manifest
            ManifestManager.AddDependencies(new Dictionary<string, string>
            {
                { info.PackageId, info.DependencyValue }
            });

            Debug.Log($"[Sorolla] {info.Name} added to manifest. Package Manager will resolve.");
        }

        /// <summary>
        ///     Uninstall an SDK by removing it from manifest.json
        /// </summary>
        public static void Uninstall(SdkId id)
        {
            if (!SdkRegistry.All.TryGetValue(id, out var info))
            {
                Debug.LogError($"[Sorolla] Unknown SDK: {id}");
                return;
            }

            Debug.Log($"[Sorolla] Uninstalling {info.Name}...");
            ManifestManager.RemoveDependencies(new[] { info.PackageId });
            Debug.Log($"[Sorolla] {info.Name} removed from manifest.");
        }

        /// <summary>
        ///     Install all SDKs required for a mode (that aren't already installed)
        /// </summary>
        public static void InstallRequiredSdks(bool isPrototype)
        {
            Debug.Log($"[Sorolla] Installing required SDKs for {(isPrototype ? "Prototype" : "Full")} mode...");
            
            var scopes = new List<string>();
            var dependencies = new Dictionary<string, string>();

            foreach (var sdk in SdkRegistry.GetRequired(isPrototype))
            {
                var isInstalled = SdkDetector.IsInstalled(sdk);
                Debug.Log($"[Sorolla] Checking {sdk.Name}: {(isInstalled ? "already installed" : "needs install")}");
                
                if (isInstalled)
                    continue;

                Debug.Log($"[Sorolla] Will install: {sdk.Name} ({sdk.PackageId})");

                // Add scope to OpenUPM - but NOT for MAX (it uses its own registry)
                if (!string.IsNullOrEmpty(sdk.Scope) && sdk.Id != SdkId.AppLovinMAX)
                    scopes.Add(sdk.Scope);

                dependencies[sdk.PackageId] = sdk.DependencyValue;

                // Special handling for MAX - uses its own registry, not OpenUPM
                if (sdk.Id == SdkId.AppLovinMAX)
                {
                    ManifestManager.AddOrUpdateRegistry(
                        "AppLovin MAX",
                        "https://unity.packages.applovin.com/",
                        new[] { "com.applovin" }
                    );
                }
            }


            // Add OpenUPM scopes if any
            if (scopes.Count > 0)
            {
                ManifestManager.AddOrUpdateRegistry(
                    "package.openupm.com",
                    "https://package.openupm.com",
                    scopes.ToArray()
                );
            }

            // Add all dependencies at once
            if (dependencies.Count > 0)
            {
                ManifestManager.AddDependencies(dependencies);
                Debug.Log($"[Sorolla] Added {dependencies.Count} package(s) to manifest.");
            }
            else
            {
                Debug.Log("[Sorolla] All required SDKs already installed.");
            }
        }


        /// <summary>
        ///     Uninstall SDKs not needed for a mode
        /// </summary>
        public static void UninstallUnnecessarySdks(bool isPrototype)
        {
            var toRemove = new List<string>();

            foreach (var sdk in SdkRegistry.GetToUninstall(isPrototype))
            {
                if (SdkDetector.IsInstalled(sdk))
                {
                    Debug.Log($"[Sorolla] Will uninstall: {sdk.Name}");
                    toRemove.Add(sdk.PackageId);
                }
            }

            if (toRemove.Count > 0)
            {
                ManifestManager.RemoveDependencies(toRemove);
                Debug.Log($"[Sorolla] Removed {toRemove.Count} package(s) from manifest.");
            }
        }

        /// <summary>
        ///     Trigger Android dependency resolution via EDM
        /// </summary>
        public static void TryResolveDependencies()
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

        /// <summary>
        ///     Install Firebase packages (App + Analytics + Crashlytics + Remote Config)
        ///     Also enables all Firebase modules in SorollaConfig for immediate use.
        /// </summary>
        public static void InstallFirebase()
        {
            var appInfo = SdkRegistry.All[SdkId.FirebaseApp];
            var analyticsInfo = SdkRegistry.All[SdkId.FirebaseAnalytics];
            var crashlyticsInfo = SdkRegistry.All[SdkId.FirebaseCrashlytics];
            var remoteConfigInfo = SdkRegistry.All[SdkId.FirebaseRemoteConfig];

            Debug.Log("[Sorolla] Installing Firebase (App + Analytics + Crashlytics + Remote Config)...");

            ManifestManager.AddDependencies(new Dictionary<string, string>
            {
                { appInfo.PackageId, appInfo.DependencyValue },
                { analyticsInfo.PackageId, analyticsInfo.DependencyValue },
                { crashlyticsInfo.PackageId, crashlyticsInfo.DependencyValue },
                { remoteConfigInfo.PackageId, remoteConfigInfo.DependencyValue }
            });

            // Auto-enable Firebase modules in config
            EnableFirebaseInConfig(true);

            Debug.Log("[Sorolla] Firebase added to manifest. Package Manager will resolve.");
        }

        /// <summary>
        ///     Uninstall all Firebase packages
        ///     Also disables all Firebase modules in SorollaConfig.
        /// </summary>
        public static void UninstallFirebase()
        {
            var appInfo = SdkRegistry.All[SdkId.FirebaseApp];
            var analyticsInfo = SdkRegistry.All[SdkId.FirebaseAnalytics];
            var crashlyticsInfo = SdkRegistry.All[SdkId.FirebaseCrashlytics];
            var remoteConfigInfo = SdkRegistry.All[SdkId.FirebaseRemoteConfig];

            Debug.Log("[Sorolla] Uninstalling Firebase...");

            ManifestManager.RemoveDependencies(new[]
            {
                appInfo.PackageId,
                analyticsInfo.PackageId,
                crashlyticsInfo.PackageId,
                remoteConfigInfo.PackageId
            });

            // Auto-disable Firebase modules in config
            EnableFirebaseInConfig(false);

            Debug.Log("[Sorolla] Firebase removed from manifest.");
        }

        /// <summary>
        ///     Enable or disable all Firebase modules in SorollaConfig
        /// </summary>
        private static void EnableFirebaseInConfig(bool enable)
        {
            var guids = AssetDatabase.FindAssets("t:SorollaConfig");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[Sorolla] No SorollaConfig found to update Firebase settings.");
                return;
            }

            var configPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var config = AssetDatabase.LoadAssetAtPath<SorollaConfig>(configPath);
            
            if (config == null)
            {
                Debug.LogWarning("[Sorolla] Could not load SorollaConfig.");
                return;
            }

            config.enableFirebaseAnalytics = enable;
            config.enableCrashlytics = enable;
            config.enableRemoteConfig = enable;

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Sorolla] Firebase modules {(enable ? "enabled" : "disabled")} in config.");
        }
    }
}
