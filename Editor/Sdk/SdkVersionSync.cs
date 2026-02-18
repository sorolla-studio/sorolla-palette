using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Auto-updates installed SDK packages to versions defined in SdkRegistry.
    ///     Runs on every domain reload. Catches the case where updating the Palette SDK
    ///     bumps SdkRegistry versions but manifest.json still has old entries.
    /// </summary>
    [InitializeOnLoad]
    static class SdkVersionSync
    {
        static SdkVersionSync()
        {
            EditorApplication.delayCall += SyncVersions;
        }

        static void SyncVersions()
        {
            ManifestManager.ModifyManifest((manifest, _) =>
            {
                if (!manifest.TryGetValue("dependencies", out var depsObj) ||
                    depsObj is not Dictionary<string, object> deps)
                    return false;

                var updates = new Dictionary<string, string>();

                foreach (SdkInfo sdk in SdkRegistry.All.Values)
                {
                    if (!deps.TryGetValue(sdk.PackageId, out var currentValue))
                        continue; // Not installed

                    string expected = sdk.DependencyValue;
                    if (string.IsNullOrEmpty(expected))
                        continue; // No version to enforce

                    string current = currentValue?.ToString() ?? "";
                    if (current == expected)
                        continue; // Already up to date

                    updates[sdk.PackageId] = expected;
                    Debug.Log($"[Palette] Updating {sdk.Name}: {current} → {expected}");
                }

                if (updates.Count == 0)
                    return false;

                // Clear Firebase cache if any Firebase packages are being updated
                if (updates.Keys.Any(k => k.StartsWith("com.google.firebase")))
                    SdkInstaller.ClearFirebasePackageCache();

                foreach (var (key, value) in updates)
                    deps[key] = value;

                Debug.Log($"[Palette] Updated {updates.Count} package(s) to latest versions.");
                return true; // Signal manifest was modified → triggers save + UPM resolve
            });
        }
    }
}
