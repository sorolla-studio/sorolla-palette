using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     SDK installation via manifest modification.
    ///     Uses manifest.json for reliable, order-independent installation.
    /// </summary>
    public static class SdkInstaller
    {
        const string OpenUpmRegistryName = "package.openupm.com";
        const string OpenUpmRegistryUrl = "https://package.openupm.com";
        const string MaxRegistryName = "AppLovin MAX";
        const string MaxRegistryUrl = "https://unity.packages.applovin.com/";

        internal static bool EnsureRegistryEntry(List<object> scopedRegistries, SdkInfo sdk)
        {
            if (string.IsNullOrEmpty(sdk.Scope)) return false;

            if (sdk.Id != SdkId.AppLovinMAX)
            {
                return ManifestManager.AddOrUpdateRegistryInternal(
                    scopedRegistries,
                    OpenUpmRegistryName,
                    OpenUpmRegistryUrl,
                    new[] { sdk.Scope });
            }

            // MAX owns com.applovin exclusively; leaving it in OpenUPM creates a duplicate scope.
            bool changed = ManifestManager.RemoveScopeFromRegistryInternal(
                scopedRegistries, OpenUpmRegistryUrl, sdk.Scope);
            changed |= ManifestManager.AddOrUpdateRegistryInternal(
                scopedRegistries,
                MaxRegistryName,
                MaxRegistryUrl,
                new[] { sdk.Scope });
            return changed;
        }

        internal static bool EnsureRequiredRegistryEntries(List<object> scopedRegistries, bool isPrototype)
        {
            bool changed = false;
            foreach (SdkInfo sdk in SdkRegistry.GetRequired(isPrototype))
                changed |= EnsureRegistryEntry(scopedRegistries, sdk);
            return changed;
        }

        static bool EnsureRegistry(SdkInfo sdk)
        {
            if (string.IsNullOrEmpty(sdk.Scope)) return false;

            return ManifestManager.ModifyManifest((manifest, scopedRegistries) =>
                EnsureRegistryEntry(scopedRegistries, sdk));
        }

        internal static bool EnsureRequiredRegistries(bool isPrototype)
        {
            return ManifestManager.ModifyManifest((manifest, scopedRegistries) =>
                EnsureRequiredRegistryEntries(scopedRegistries, isPrototype));
        }

        internal static bool EnsureCoreRegistries()
        {
            return ManifestManager.ModifyManifest((manifest, scopedRegistries) =>
            {
                bool changed = false;
                foreach (SdkInfo sdk in SdkRegistry.All.Values)
                    if (sdk.Requirement == SdkRequirement.Core)
                        changed |= EnsureRegistryEntry(scopedRegistries, sdk);
                return changed;
            });
        }

        /// <summary>
        ///     Install an SDK by adding it to manifest.json
        /// </summary>
        public static void Install(SdkId id)
        {
            if (!SdkRegistry.All.TryGetValue(id, out SdkInfo info))
            {
                Debug.LogError($"[Palette] Unknown SDK: {id}");
                return;
            }

            Debug.Log($"[Palette] Installing {info.Name}...");

            EnsureRegistry(info);

            // Add dependency to manifest
            ManifestManager.AddDependencies(new Dictionary<string, string>
            {
                { info.PackageId, info.DependencyValue },
            });

            Debug.Log($"[Palette] {info.Name} added to manifest. Package Manager will resolve.");
        }

        /// <summary>
        ///     Uninstall an SDK by removing it from manifest.json
        /// </summary>
        public static void Uninstall(SdkId id)
        {
            if (!SdkRegistry.All.TryGetValue(id, out SdkInfo info))
            {
                Debug.LogError($"[Palette] Unknown SDK: {id}");
                return;
            }

            Debug.Log($"[Palette] Uninstalling {info.Name}...");
            ManifestManager.RemoveDependencies(new[] { info.PackageId });
            Debug.Log($"[Palette] {info.Name} removed from manifest.");
        }

        /// <summary>
        ///     Install all SDKs required for a mode (that aren't already installed)
        /// </summary>
        public static void InstallRequiredSdks(bool isPrototype)
        {
            Debug.Log($"[Palette] Installing required SDKs for {(isPrototype ? "Prototype" : "Full")} mode...");

            var dependencies = new Dictionary<string, string>();

            EnsureRequiredRegistries(isPrototype);

            foreach (SdkInfo sdk in SdkRegistry.GetRequired(isPrototype))
            {
                // Don't skip based on assembly detection - it's unreliable during mode switches.
                // ManifestManager.AddDependencies handles idempotency via manifest.json check.
                Debug.Log($"[Palette] Will install: {sdk.Name} ({sdk.PackageId})");

                dependencies[sdk.PackageId] = sdk.DependencyValue;
            }

            // Add all dependencies at once
            if (dependencies.Count > 0)
            {
                // Clear Firebase cache if any Firebase packages are being installed
                foreach (string packageId in dependencies.Keys)
                {
                    if (packageId.StartsWith("com.google.firebase"))
                    {
                        ClearFirebasePackageCache();
                        break;
                    }
                }

                ManifestManager.AddDependencies(dependencies);
                Debug.Log($"[Palette] Added {dependencies.Count} package(s) to manifest.");
            }
            else
            {
                Debug.Log("[Palette] All required SDKs already installed.");
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
                Debug.Log($"[Palette] Will uninstall: {sdk.Name}");
                toRemove.Add(sdk.PackageId);
            }

            if (toRemove.Count > 0)
            {
                ManifestManager.RemoveDependencies(toRemove);
                Debug.Log($"[Palette] Removed {toRemove.Count} package(s) from manifest.");
            }
        }

        /// <summary>
        ///     Clear stale Firebase package cache to prevent "Directory not empty" errors.
        ///     Unity Package Manager has issues when multiple packages reference the same git repo
        ///     with different path parameters - stale .tmp directories can block new installations.
        /// </summary>
        internal static void ClearFirebasePackageCache()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string cacheRoot = Path.Combine(projectRoot, "Library", "PackageCache");

            if (!Directory.Exists(cacheRoot))
                return;

            try
            {
                // Clear stale .tmp directories (failed checkouts)
                foreach (string tmpDir in Directory.GetDirectories(cacheRoot, ".tmp*"))
                {
                    Debug.Log($"[Palette] Clearing stale temp directory: {Path.GetFileName(tmpDir)}");
                    Directory.Delete(tmpDir, true);
                }

                // Clear existing Firebase cache entries (forces fresh clone)
                foreach (string dir in Directory.GetDirectories(cacheRoot, "com.google.firebase*"))
                {
                    Debug.Log($"[Palette] Clearing Firebase cache: {Path.GetFileName(dir)}");
                    Directory.Delete(dir, true);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Palette] Could not clear package cache: {e.Message}");
                // Non-fatal - installation may still succeed
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

            Debug.Log("[Palette] Installing Firebase (App + Analytics + Crashlytics + Remote Config)...");

            // Clear stale cache to prevent "Directory not empty" errors from concurrent git clones
            ClearFirebasePackageCache();

            ManifestManager.AddDependencies(new Dictionary<string, string>
            {
                { appInfo.PackageId, appInfo.DependencyValue },
                { analyticsInfo.PackageId, analyticsInfo.DependencyValue },
                { crashlyticsInfo.PackageId, crashlyticsInfo.DependencyValue },
                { remoteConfigInfo.PackageId, remoteConfigInfo.DependencyValue },
            });

            Debug.Log("[Palette] Firebase added to manifest. Package Manager will resolve.");
        }
    }
}
