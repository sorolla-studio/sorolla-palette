using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Intercepts the EDM4U Gradle/Java 17+ error on first import and logs a helpful message.
    ///     EDM4U bundles Gradle 5.1.1 which fails with Java 17+ (Unity 6+). The error is cosmetic —
    ///     Edm4uGradleConfig fixes the settings on domain reload, so the next resolve works fine.
    /// </summary>
    [InitializeOnLoad]
    static class Edm4uGradleErrorInterceptor
    {
        static Edm4uGradleErrorInterceptor()
        {
            Application.logMessageReceived += OnLogMessage;
        }

        static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception)
                return;

            if (!message.Contains("org.codehaus.groovy.vmplugin") &&
                !message.Contains("NoClassDefFoundError") &&
                !(message.Contains("Gradle") && message.Contains("Java")))
                return;

            Debug.LogWarning(
                "[Palette] Known issue: EDM4U's bundled Gradle is incompatible with Java 17+.\n" +
                "This error is cosmetic — restart Unity and it will resolve automatically.\n" +
                "If it persists, run Palette > Run Setup (Force).");

            // Unsubscribe after first detection to avoid spam
            Application.logMessageReceived -= OnLogMessage;
        }
    }

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
