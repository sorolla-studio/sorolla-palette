using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
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
            if (!SdkRegistry.All.TryGetValue(id, out SdkInfo info))
            {
                Debug.LogError($"[SorollaSDK] Unknown SDK: {id}");
                return;
            }

            Debug.Log($"[SorollaSDK] Installing {info.Name}...");

            // Add registry if needed (for MAX - uses its own registry, not OpenUPM)
            if (id == SdkId.AppLovinMAX)
            {
                // Remove com.applovin from OpenUPM if it exists (fixes duplicate scope error)
                ManifestManager.RemoveScopeFromRegistry("https://package.openupm.com", "com.applovin");

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
                { info.PackageId, info.DependencyValue },
            });

            Debug.Log($"[SorollaSDK] {info.Name} added to manifest. Package Manager will resolve.");
        }

        /// <summary>
        ///     Uninstall an SDK by removing it from manifest.json
        /// </summary>
        public static void Uninstall(SdkId id)
        {
            if (!SdkRegistry.All.TryGetValue(id, out SdkInfo info))
            {
                Debug.LogError($"[SorollaSDK] Unknown SDK: {id}");
                return;
            }

            Debug.Log($"[SorollaSDK] Uninstalling {info.Name}...");
            ManifestManager.RemoveDependencies(new[] { info.PackageId });
            Debug.Log($"[SorollaSDK] {info.Name} removed from manifest.");
        }

        /// <summary>
        ///     Install all SDKs required for a mode (that aren't already installed)
        /// </summary>
        public static void InstallRequiredSdks(bool isPrototype)
        {
            Debug.Log($"[SorollaSDK] Installing required SDKs for {(isPrototype ? "Prototype" : "Full")} mode...");

            var scopes = new List<string>();
            var dependencies = new Dictionary<string, string>();

            foreach (SdkInfo sdk in SdkRegistry.GetRequired(isPrototype))
            {
                // Don't skip based on assembly detection - it's unreliable during mode switches.
                // ManifestManager.AddDependencies handles idempotency via manifest.json check.
                Debug.Log($"[SorollaSDK] Will install: {sdk.Name} ({sdk.PackageId})");

                // Add scope to OpenUPM - but NOT for MAX (it uses its own registry)
                if (!string.IsNullOrEmpty(sdk.Scope) && sdk.Id != SdkId.AppLovinMAX)
                    scopes.Add(sdk.Scope);

                dependencies[sdk.PackageId] = sdk.DependencyValue;

                // Special handling for MAX - uses its own registry, not OpenUPM
                if (sdk.Id == SdkId.AppLovinMAX)
                {
                    // Remove com.applovin from OpenUPM if it exists (fixes duplicate scope error)
                    ManifestManager.RemoveScopeFromRegistry("https://package.openupm.com", "com.applovin");

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
                Debug.Log($"[SorollaSDK] Added {dependencies.Count} package(s) to manifest.");
            }
            else
            {
                Debug.Log("[SorollaSDK] All required SDKs already installed.");
            }
        }


        /// <summary>
        ///     Uninstall SDKs not needed for a mode
        /// </summary>
        public static void UninstallUnnecessarySdks(bool isPrototype)
        {
            var toRemove = new List<string>();

            // Don't check assembly detection - always remove from manifest.
            // ManifestManager.RemoveDependencies handles non-existent packages gracefully.
            foreach (SdkInfo sdk in SdkRegistry.GetToUninstall(isPrototype))
            {
                Debug.Log($"[SorollaSDK] Will uninstall: {sdk.Name}");
                toRemove.Add(sdk.PackageId);
            }

            if (toRemove.Count > 0)
            {
                ManifestManager.RemoveDependencies(toRemove);
                Debug.Log($"[SorollaSDK] Removed {toRemove.Count} package(s) from manifest.");
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
                MethodInfo resolveMethod = resolverType?.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static);
                if (resolveMethod != null)
                {
                    Debug.Log("[SorollaSDK] Triggering EDM resolution...");
                    resolveMethod.Invoke(null, new object[] { null, null, true });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SorollaSDK] Could not auto-resolve dependencies: {e.Message}");
            }
        }

        /// <summary>
        ///     Install Firebase packages (App + Analytics + Crashlytics + Remote Config)
        ///     Also enables all Firebase modules in SorollaConfig for immediate use.
        /// </summary>
        public static void InstallFirebase()
        {
            SdkInfo appInfo = SdkRegistry.All[SdkId.FirebaseApp];
            SdkInfo analyticsInfo = SdkRegistry.All[SdkId.FirebaseAnalytics];
            SdkInfo crashlyticsInfo = SdkRegistry.All[SdkId.FirebaseCrashlytics];
            SdkInfo remoteConfigInfo = SdkRegistry.All[SdkId.FirebaseRemoteConfig];

            Debug.Log("[SorollaSDK] Installing Firebase (App + Analytics + Crashlytics + Remote Config)...");

            ManifestManager.AddDependencies(new Dictionary<string, string>
            {
                { appInfo.PackageId, appInfo.DependencyValue },
                { analyticsInfo.PackageId, analyticsInfo.DependencyValue },
                { crashlyticsInfo.PackageId, crashlyticsInfo.DependencyValue },
                { remoteConfigInfo.PackageId, remoteConfigInfo.DependencyValue },
            });

            // Auto-enable Firebase modules in config
            EnableFirebaseInConfig(true);

            Debug.Log("[SorollaSDK] Firebase added to manifest. Package Manager will resolve.");
        }

        /// <summary>
        ///     Uninstall all Firebase packages
        ///     Also disables all Firebase modules in SorollaConfig.
        /// </summary>
        public static void UninstallFirebase()
        {
            Debug.Log("[SorollaSDK] Uninstalling Firebase...");

            // IMPORTANT: Disable config FIRST, before removing packages.
            // Package removal triggers domain reload which interrupts asset saves.
            EnableFirebaseInConfig(false);

            SdkInfo appInfo = SdkRegistry.All[SdkId.FirebaseApp];
            SdkInfo analyticsInfo = SdkRegistry.All[SdkId.FirebaseAnalytics];
            SdkInfo crashlyticsInfo = SdkRegistry.All[SdkId.FirebaseCrashlytics];
            SdkInfo remoteConfigInfo = SdkRegistry.All[SdkId.FirebaseRemoteConfig];

            ManifestManager.RemoveDependencies(new[]
            {
                appInfo.PackageId,
                analyticsInfo.PackageId,
                crashlyticsInfo.PackageId,
                remoteConfigInfo.PackageId,
            });

            Debug.Log("[SorollaSDK] Firebase removed from manifest.");
        }

        /// <summary>
        ///     Enable or disable all Firebase modules in SorollaConfig
        /// </summary>
        static void EnableFirebaseInConfig(bool enable)
        {
            string[] guids = AssetDatabase.FindAssets("t:SorollaConfig");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[SorollaSDK] No SorollaConfig found to update Firebase settings.");
                return;
            }

            string configPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var config = AssetDatabase.LoadAssetAtPath<SorollaConfig>(configPath);

            if (config == null)
            {
                Debug.LogWarning("[SorollaSDK] Could not load SorollaConfig.");
                return;
            }

            config.enableFirebaseAnalytics = enable;
            config.enableCrashlytics = enable;
            config.enableRemoteConfig = enable;

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SorollaSDK] Firebase modules {(enable ? "enabled" : "disabled")} in config.");
        }
    }
}
