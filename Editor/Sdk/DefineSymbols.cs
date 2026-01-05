using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Manages scripting define symbols for Palette SDK.
    /// </summary>
    [InitializeOnLoad]
    public static class DefineSymbols
    {
        /// <summary>
        ///     Package ID to define symbol mapping.
        ///     When a package is detected, its corresponding define is set globally.
        /// </summary>
        private static readonly Dictionary<string, string[]> PackageDefines = new()
        {
            // MAX SDK - both for defineConstraints and internal #if usage
            { "com.applovin.mediation.ads", new[] { "APPLOVIN_MAX_INSTALLED", "SOROLLA_MAX_ENABLED" } },
            // Adjust SDK
            { "com.adjust.sdk", new[] { "ADJUST_SDK_INSTALLED", "SOROLLA_ADJUST_ENABLED" } },
            // Firebase modules
            { "com.google.firebase.analytics", new[] { "FIREBASE_ANALYTICS_INSTALLED" } },
            { "com.google.firebase.crashlytics", new[] { "FIREBASE_CRASHLYTICS_INSTALLED" } },
            { "com.google.firebase.remote-config", new[] { "FIREBASE_REMOTE_CONFIG_INSTALLED" } },
        };

        static DefineSymbols()
        {
            // Run on domain reload
            EditorApplication.delayCall += RefreshSdkDefines;

            // Also listen for package changes
            Events.registeredPackages += OnPackagesChanged;
        }

        private static void OnPackagesChanged(PackageRegistrationEventArgs args)
        {
            RefreshSdkDefines();
        }

        /// <summary>
        ///     Detect installed SDK packages and set their global defines.
        ///     This ensures defineConstraints work correctly in builds.
        /// </summary>
        [MenuItem("Sorolla/Refresh SDK Defines")]
        public static void RefreshSdkDefines()
        {
            var listRequest = Client.List(true, false);

            // Wait for the request to complete
            while (!listRequest.IsCompleted)
            {
                System.Threading.Thread.Sleep(10);
            }

            if (listRequest.Status != StatusCode.Success)
            {
                Debug.LogWarning("[Palette] Failed to list packages for SDK detection");
                return;
            }

            var installedPackages = listRequest.Result.Select(p => p.name).ToHashSet();
            bool anyChange = false;

            foreach (var kvp in PackageDefines)
            {
                bool isInstalled = installedPackages.Contains(kvp.Key);

                foreach (string define in kvp.Value)
                {
                    bool wasChanged = SetIfChanged(define, isInstalled);
                    anyChange |= wasChanged;

                    if (wasChanged)
                    {
                        Debug.Log($"[Palette] {define} = {isInstalled} (package: {kvp.Key})");
                    }
                }
            }

            if (anyChange)
            {
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        ///     Set a define only if the current state differs. Returns true if changed.
        /// </summary>
        private static bool SetIfChanged(string define, bool enabled)
        {
            bool changed = false;

            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                var defines = PlayerSettings.GetScriptingDefineSymbols(target)
                    .Split(';')
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .Distinct()
                    .ToList();

                bool hasDefine = defines.Contains(define);

                if (enabled && !hasDefine)
                {
                    defines.Add(define);
                    PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
                    changed = true;
                }
                else if (!enabled && hasDefine)
                {
                    defines.Remove(define);
                    PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>
        ///     Apply mode-specific defines
        /// </summary>
        public static void Apply(bool isPrototype)
        {
            Set(SorollaSettings.DefinePrototype, isPrototype);
            Set(SorollaSettings.DefineFull, !isPrototype);
        }

        /// <summary>
        ///     Set a define symbol enabled/disabled for all build targets
        /// </summary>
        public static void Set(string define, bool enabled)
        {
            SetForTarget(NamedBuildTarget.Android, define, enabled);
            SetForTarget(NamedBuildTarget.iOS, define, enabled);
        }

        /// <summary>
        ///     Set a define symbol for a specific build target
        /// </summary>
        public static void SetForTarget(NamedBuildTarget target, string define, bool enabled)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(';')
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct()
                .ToList();

            bool hasDefine = defines.Contains(define);

            if (enabled && !hasDefine)
            {
                defines.Add(define);
            }
            else if (!enabled && hasDefine)
            {
                defines.Remove(define);
            }
            else
            {
                return; // No change needed
            }

            string newSymbols = string.Join(";", defines);
            PlayerSettings.SetScriptingDefineSymbols(target, newSymbols);
            Debug.Log($"[Palette] Defines for {target}: {newSymbols}");
        }
    }
}
