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
        ///     Add or update a scoped registry
        /// </summary>
        public static bool AddOrUpdateRegistry(string name, string url, string[] requiredScopes)
        {
            return ModifyManifest((manifest, scopedRegistries) =>
            {
                return AddOrUpdateRegistryInternal(scopedRegistries, name, url, requiredScopes);
            });
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
        ///     Remove a scope from a specific registry (used to fix duplicate scope issues)
        /// </summary>
        public static bool RemoveScopeFromRegistry(string registryUrl, string scopeToRemove)
        {
            return ModifyManifest((manifest, scopedRegistries) =>
            {
                foreach (var reg in scopedRegistries)
                {
                    if (reg is Dictionary<string, object> r &&
                        r.ContainsKey("url") && r["url"].ToString() == registryUrl &&
                        r.TryGetValue("scopes", out var value) && value is List<object> scopes)
                    {
                        if (scopes.Remove(scopeToRemove))
                        {
                            Debug.Log($"[ManifestManager] Removed scope '{scopeToRemove}' from registry '{registryUrl}'");
                            return true;
                        }
                    }
                }
                return false;
            });
        }

        /// <summary>
        ///     Internal registry management logic
        /// </summary>
        private static bool AddOrUpdateRegistryInternal(List<object> scopedRegistries, string name, string url,
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
                Debug.Log($"[ManifestManager] Added {name} registry to manifest.json");
                return true;
            }

            // Update existing registry scopes
            if (registry.TryGetValue("scopes", out var value))
            {
                if (value is List<object> scopes)
                {
                    var modified = false;
                    foreach (var scope in requiredScopes)
                        if (!scopes.Contains(scope))
                        {
                            scopes.Add(scope);
                            modified = true;
                        }

                    if (modified)
                    {
                        Debug.Log($"[ManifestManager] Updated {name} registry scopes");
                        return true;
                    }
                }
            }

            return false;
        }
    }
}