using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Sanitizes AndroidManifest.xml by removing entries for uninstalled SDKs.
    ///     Prevents runtime crashes from orphaned manifest entries.
    /// </summary>
    public static class AndroidManifestSanitizer
    {
        private const string Tag = "[Palette ManifestSanitizer]";

        private static string AndroidManifestPath =>
            Path.Combine(Application.dataPath, "Plugins", "Android", "AndroidManifest.xml");

        // SDK-specific patterns to detect and remove
        private static readonly Dictionary<SdkId, string[]> SdkManifestPatterns = new()
        {
            [SdkId.Facebook] = new[]
            {
                "com.facebook.unity",
                "com.facebook.FacebookContentProvider",
                "com.facebook.sdk",
                "com.facebook.app"
            },
            [SdkId.Adjust] = new[]
            {
                "com.adjust.sdk"
            }
        };

        /// <summary>
        ///     Check if AndroidManifest has entries for uninstalled SDKs
        /// </summary>
        public static List<(SdkId sdk, string[] entries)> DetectOrphanedEntries()
        {
            var orphaned = new List<(SdkId, string[])>();

            if (!File.Exists(AndroidManifestPath))
                return orphaned;

            var manifestContent = File.ReadAllText(AndroidManifestPath);

            // Read UPM manifest to check installed packages
            var upmManifest = ReadUpmManifest();

            foreach (var (sdkId, patterns) in SdkManifestPatterns)
            {
                // Check if SDK is installed (via UPM manifest - more reliable than assembly detection)
                var packageId = SdkRegistry.All[sdkId].PackageId;
                var isInUpmManifest = upmManifest != null && upmManifest.ContainsKey(packageId);
                var isDetected = SdkDetector.IsInstalled(sdkId);

                if (isInUpmManifest || isDetected)
                    continue;

                // Check if Android manifest has entries for this SDK
                var foundEntries = patterns
                    .Where(pattern => manifestContent.Contains(pattern))
                    .ToArray();

                if (foundEntries.Length > 0)
                    orphaned.Add((sdkId, foundEntries));
            }

            return orphaned;
        }

        /// <summary>
        ///     Read UPM manifest.json dependencies
        /// </summary>
        private static Dictionary<string, object> ReadUpmManifest()
        {
            var upmManifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            if (!File.Exists(upmManifestPath))
                return null;

            try
            {
                var json = File.ReadAllText(upmManifestPath);
                var manifest = MiniJson.Deserialize(json) as Dictionary<string, object>;
                return manifest?.TryGetValue("dependencies", out var deps) == true
                    ? deps as Dictionary<string, object>
                    : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        ///     Remove orphaned SDK entries from AndroidManifest.xml
        /// </summary>
        public static bool Sanitize()
        {
            if (!File.Exists(AndroidManifestPath))
            {
                Debug.Log($"{Tag} No AndroidManifest.xml found, nothing to sanitize");
                return false;
            }

            var orphaned = DetectOrphanedEntries();
            if (orphaned.Count == 0)
            {
                Debug.Log($"{Tag} No orphaned entries found");
                return false;
            }

            try
            {
                var doc = XDocument.Load(AndroidManifestPath);
                var modified = false;

                foreach (var (sdkId, patterns) in orphaned)
                {
                    Debug.Log($"{Tag} Removing {SdkRegistry.All[sdkId].Name} entries from AndroidManifest.xml");
                    modified |= RemoveSdkEntries(doc, patterns);
                }

                if (modified)
                {
                    // Create backup
                    var backupPath = AndroidManifestPath + ".backup";
                    if (File.Exists(AndroidManifestPath))
                        File.Copy(AndroidManifestPath, backupPath, true);

                    doc.Save(AndroidManifestPath);
                    AssetDatabase.Refresh();

                    Debug.Log($"{Tag} AndroidManifest.xml sanitized successfully (backup at {backupPath})");
                    return true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{Tag} Failed to sanitize AndroidManifest.xml: {e.Message}");
            }

            return false;
        }

        /// <summary>
        ///     Remove elements matching patterns from manifest
        /// </summary>
        private static bool RemoveSdkEntries(XDocument doc, string[] patterns)
        {
            var modified = false;
            var application = doc.Root?.Element("application");
            if (application == null)
                return false;

            // Find elements to remove
            var toRemove = new List<XElement>();

            foreach (var element in application.Elements())
            {
                var nameAttr = element.Attribute(XName.Get("name", "http://schemas.android.com/apk/res/android"));
                var authAttr = element.Attribute(XName.Get("authorities", "http://schemas.android.com/apk/res/android"));
                var valueAttr = element.Attribute(XName.Get("value", "http://schemas.android.com/apk/res/android"));

                var name = nameAttr?.Value ?? "";
                var authorities = authAttr?.Value ?? "";
                var value = valueAttr?.Value ?? "";

                foreach (var pattern in patterns)
                {
                    if (name.Contains(pattern) || authorities.Contains(pattern) || value.Contains(pattern))
                    {
                        toRemove.Add(element);
                        Debug.Log($"{Tag} Removing: <{element.Name.LocalName} android:name=\"{name}\">");
                        break;
                    }
                }
            }

            // Remove marked elements
            foreach (var element in toRemove)
            {
                element.Remove();
                modified = true;
            }

            return modified;
        }

        /// <summary>
        ///     Menu item to manually sanitize manifest
        /// </summary>
        [MenuItem("Palette/Tools/Sanitize Android Manifest")]
        public static void SanitizeMenuItem()
        {
            var orphaned = DetectOrphanedEntries();

            if (orphaned.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Manifest Sanitizer",
                    "No orphaned SDK entries found in AndroidManifest.xml",
                    "OK"
                );
                return;
            }

            var sdkNames = string.Join(", ", orphaned.Select(o => SdkRegistry.All[o.sdk].Name));
            var confirm = EditorUtility.DisplayDialog(
                "Manifest Sanitizer",
                $"Found orphaned entries for: {sdkNames}\n\n" +
                "These SDKs are referenced in AndroidManifest.xml but not installed.\n" +
                "This will cause runtime crashes!\n\n" +
                "Remove these entries?",
                "Yes, Remove",
                "Cancel"
            );

            if (confirm)
            {
                Sanitize();
                EditorUtility.DisplayDialog(
                    "Manifest Sanitizer",
                    "AndroidManifest.xml has been sanitized.\nA backup was created.",
                    "OK"
                );
            }
        }
    }
}
