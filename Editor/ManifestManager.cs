using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     DRY manifest modification utilities - eliminates repeated JSON parsing patterns
    /// </summary>
    public static class ManifestManager
    {
        private static string ManifestPath => Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");

        /// <summary>
        ///     Generic manifest modification with callback pattern
        /// </summary>
        public static bool ModifyManifest(Func<Dictionary<string, object>, List<object>, bool> modifier)
        {
            if (!File.Exists(ManifestPath))
            {
                Debug.LogError("[ManifestManager] manifest.json not found!");
                return false;
            }

            try
            {
                var jsonContent = File.ReadAllText(ManifestPath);
                var manifest = MiniJson.Deserialize(jsonContent) as Dictionary<string, object>;

                if (manifest == null)
                {
                    Debug.LogError("[ManifestManager] Failed to parse manifest.json");
                    return false;
                }

                // Get or create scopedRegistries array
                if (!manifest.ContainsKey("scopedRegistries"))
                    manifest["scopedRegistries"] = new List<object>();

                var scopedRegistries = manifest["scopedRegistries"] as List<object>;

                if (modifier(manifest, scopedRegistries))
                {
                    var updatedJson = MiniJson.Serialize(manifest, true);
                    File.WriteAllText(ManifestPath, updatedJson);
#if UNITY_EDITOR
                    Client.Resolve();
#endif
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ManifestManager] Error modifying manifest.json: {e.Message}");
                return false;
            }
        }

        /// <summary>
        ///     Add dependencies to manifest
        /// </summary>
        public static bool AddDependencies(Dictionary<string, string> packagesToAdd)
        {
            return ModifyManifest((manifest, scopedRegistries) =>
            {
                if (!manifest.ContainsKey("dependencies"))
                    manifest["dependencies"] = new Dictionary<string, object>();

                var dependencies = manifest["dependencies"] as Dictionary<string, object>;
                var modified = false;

                foreach (var package in packagesToAdd)
                    if (!dependencies.ContainsKey(package.Key))
                    {
                        dependencies[package.Key] = package.Value;
                        modified = true;
                        Debug.Log($"[ManifestManager] Added {package.Key} dependency");
                    }

                return modified;
            });
        }

        /// <summary>
        ///     Remove dependencies from manifest
        /// </summary>
        public static bool RemoveDependencies(IEnumerable<string> packageNames)
        {
            return ModifyManifest((manifest, scopedRegistries) =>
            {
                if (!manifest.TryGetValue("dependencies", out var value))
                    return false;

                var dependencies = value as Dictionary<string, object>;
                var modified = false;

                foreach (var name in packageNames)
                    if (dependencies.Remove(name))
                    {
                        modified = true;
                        Debug.Log($"[ManifestManager] Removed {name} dependency");
                    }

                return modified;
            });
        }

        /// <summary>
        ///     Pure list-level registry management used by the installer and deterministic tests.
        /// </summary>
        internal static bool AddOrUpdateRegistryInternal(List<object> scopedRegistries, string name, string url,
            string[] requiredScopes)
        {
            Dictionary<string, object> registry = null;
            var exists = false;

            // Find existing registry
            foreach (var reg in scopedRegistries)
            {
                if (reg is Dictionary<string, object> r && r.ContainsKey("url") && r["url"].ToString() == url)
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
                return true;
            }

            // Update existing registry scopes
            if (!registry.TryGetValue("scopes", out object value) || !(value is List<object> scopes))
            {
                registry["scopes"] = new List<object>(requiredScopes);
                return requiredScopes.Length > 0;
            }

            bool modified = false;
            foreach (string scope in requiredScopes)
                if (!scopes.Contains(scope))
                {
                    scopes.Add(scope);
                    modified = true;
                }

            return modified;
        }

        internal static bool RemoveScopeFromRegistryInternal(
            List<object> scopedRegistries,
            string registryUrl,
            string scopeToRemove)
        {
            bool modified = false;
            foreach (object reg in scopedRegistries)
            {
                if (reg is Dictionary<string, object> registry &&
                    registry.TryGetValue("url", out object url) && url?.ToString() == registryUrl &&
                    registry.TryGetValue("scopes", out object value) && value is List<object> scopes)
                    while (scopes.Remove(scopeToRemove))
                        modified = true;
            }

            return modified;
        }
    }
}
