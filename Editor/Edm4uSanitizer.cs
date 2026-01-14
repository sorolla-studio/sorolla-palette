using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Detects EDM4U (External Dependency Manager) configuration issues.
    ///     Detection only - no auto-fixes. Users resolve issues manually.
    /// </summary>
    public static class Edm4uSanitizer
    {
        /// <summary>
        ///     Check for duplicate EDM4U installations (common when importing .unitypackages with embedded EDM4U).
        /// </summary>
        public static List<string> DetectDuplicateInstallations()
        {
            var duplicates = new List<string>();
            var edm4uPaths = new HashSet<string>();

            // Search for EDM4U markers in Assets/ folder
            CollectEdm4uPaths("PlayServicesResolver", edm4uPaths);
            CollectEdm4uPaths("ExternalDependencyManager", edm4uPaths);

            // If we have UPM EDM4U AND Assets/ EDM4U, that's a duplicate
            bool hasUpmEdm4u = SdkDetector.IsInstalled(SdkId.ExternalDependencyManager);
            if (hasUpmEdm4u && edm4uPaths.Count > 0)
            {
                foreach (var path in edm4uPaths)
                    duplicates.Add($"Duplicate EDM4U found at {path} (also installed via UPM)");
            }
            else if (edm4uPaths.Count > 1)
            {
                duplicates.Add($"Multiple EDM4U installations found: {string.Join(", ", edm4uPaths)}");
            }

            return duplicates;
        }

        private static void CollectEdm4uPaths(string searchTerm, HashSet<string> paths)
        {
            var guids = AssetDatabase.FindAssets(searchTerm);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var parts = path.Split('/');
                if (parts.Length >= 2)
                {
                    var rootPath = string.Join("/", parts.Take(Math.Min(3, parts.Length)));
                    if (rootPath.StartsWith("Assets/") && !rootPath.Contains("Packages"))
                        paths.Add(rootPath);
                }
            }
        }
    }
}
