using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        // Note: Only SDKs that can be uninstalled (FullOnly) need orphan cleanup
        private static readonly Dictionary<SdkId, string[]> SdkManifestPatterns = new()
        {
            [SdkId.Adjust] = new[]
            {
                "com.adjust.sdk"
            }
        };

        /// <summary>
        ///     The correct main activity class for Unity 6 LTS (GameActivity backend).
        /// </summary>
        private const string Unity6MainActivity = "com.unity3d.player.UnityPlayerGameActivity";

        private static readonly XNamespace AndroidNs = "http://schemas.android.com/apk/res/android";

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
        ///     Detect duplicate activity declarations in AndroidManifest
        /// </summary>
        public static List<string> DetectDuplicateActivities()
        {
            var duplicates = new List<string>();

            if (!File.Exists(AndroidManifestPath))
                return duplicates;

            try
            {
                var doc = XDocument.Load(AndroidManifestPath);
                var application = doc.Root?.Element("application");
                if (application == null)
                    return duplicates;

                var activities = application.Elements("activity")
                    .Select(e => e.Attribute(AndroidNs + "name")?.Value)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToList();

                duplicates = activities
                    .GroupBy(name => name)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{Tag} Failed to detect duplicate activities: {e.Message}");
            }

            return duplicates;
        }

        /// <summary>
        ///     Remove duplicate activity declarations, keeping the best one
        ///     (prefers android:exported="true" for main activities)
        /// </summary>
        private static bool RemoveDuplicateActivities(XDocument doc)
        {
            var application = doc.Root?.Element("application");
            if (application == null)
                return false;

            var modified = false;
            var activitiesByName = application.Elements("activity")
                .GroupBy(e => e.Attribute(AndroidNs + "name")?.Value ?? "")
                .Where(g => !string.IsNullOrEmpty(g.Key) && g.Count() > 1);

            foreach (var group in activitiesByName)
            {
                var activities = group.ToList();

                // Keep the one with android:exported="true", or the first one
                var toKeep = activities.FirstOrDefault(e =>
                    e.Attribute(AndroidNs + "exported")?.Value == "true") ?? activities[0];

                foreach (var activity in activities)
                {
                    if (activity == toKeep)
                        continue;

                    Debug.Log($"{Tag} Removing duplicate activity: {group.Key}");
                    activity.Remove();
                    modified = true;
                }
            }

            return modified;
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
        ///     Detect if the launcher activity uses the wrong class name.
        ///     Returns the wrong class name if found, or null if correct.
        /// </summary>
        /// <remarks>
        ///     Unity 6 App UI package sets the activity to AppUIGameActivity,
        ///     but without that package installed the app crashes on launch.
        ///     The correct activity for standard Unity 6 is UnityPlayerGameActivity.
        /// </remarks>
        public static string DetectWrongMainActivity()
        {
            if (!File.Exists(AndroidManifestPath))
                return null;

            try
            {
                var doc = XDocument.Load(AndroidManifestPath);
                var application = doc.Root?.Element("application");
                if (application == null)
                    return null;

                // Find the launcher activity (has intent-filter with LAUNCHER category)
                var launcherActivity = application.Elements("activity")
                    .FirstOrDefault(a => a.Elements("intent-filter")
                        .Any(f => f.Elements("category")
                            .Any(c => c.Attribute(AndroidNs + "name")?.Value ==
                                      "android.intent.category.LAUNCHER")));

                if (launcherActivity == null)
                    return null;

                var activityName = launcherActivity.Attribute(AndroidNs + "name")?.Value;
                if (activityName != null && activityName != Unity6MainActivity)
                    return activityName;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{Tag} Failed to detect main activity: {e.Message}");
            }

            return null;
        }

        /// <summary>
        ///     Fix the launcher activity class name to the correct Unity 6 value.
        /// </summary>
        private static bool FixMainActivity(XDocument doc)
        {
            var application = doc.Root?.Element("application");
            if (application == null)
                return false;

            var launcherActivity = application.Elements("activity")
                .FirstOrDefault(a => a.Elements("intent-filter")
                    .Any(f => f.Elements("category")
                        .Any(c => c.Attribute(AndroidNs + "name")?.Value ==
                                  "android.intent.category.LAUNCHER")));

            if (launcherActivity == null)
                return false;

            var nameAttr = launcherActivity.Attribute(AndroidNs + "name");
            if (nameAttr == null || nameAttr.Value == Unity6MainActivity)
                return false;

            Debug.Log($"{Tag} Fixing main activity: {nameAttr.Value} → {Unity6MainActivity}");
            nameAttr.Value = Unity6MainActivity;
            return true;
        }

        /// <summary>
        ///     Remove orphaned SDK entries and duplicate activities from AndroidManifest.xml
        /// </summary>
        public static bool Sanitize()
        {
            if (!File.Exists(AndroidManifestPath))
            {
                Debug.Log($"{Tag} No AndroidManifest.xml found, nothing to sanitize");
                return false;
            }

            var orphaned = DetectOrphanedEntries();
            var duplicates = DetectDuplicateActivities();
            var wrongActivity = DetectWrongMainActivity();

            if (orphaned.Count == 0 && duplicates.Count == 0 && wrongActivity == null)
            {
                Debug.Log($"{Tag} No orphaned entries, duplicate activities, or wrong main activity found");
                return false;
            }

            try
            {
                var doc = XDocument.Load(AndroidManifestPath);
                var modified = false;

                // Remove orphaned SDK entries
                foreach (var (sdkId, patterns) in orphaned)
                {
                    Debug.Log($"{Tag} Removing {SdkRegistry.All[sdkId].Name} entries from AndroidManifest.xml");
                    modified |= RemoveSdkEntries(doc, patterns);
                }

                // Remove duplicate activities
                if (duplicates.Count > 0)
                {
                    Debug.Log($"{Tag} Removing {duplicates.Count} duplicate activity declaration(s)");
                    modified |= RemoveDuplicateActivities(doc);
                }

                // Fix wrong main activity class name
                if (wrongActivity != null)
                {
                    Debug.Log($"{Tag} Fixing wrong main activity: {wrongActivity}");
                    modified |= FixMainActivity(doc);
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

    }
}
