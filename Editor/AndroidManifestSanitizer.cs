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

        private static string LauncherManifestPath =>
            Path.Combine(Application.dataPath, "Plugins", "Android", "LauncherManifest.xml");

        /// <summary>
        ///     Check if the project uses a custom launcher manifest (Unity 6 split module architecture).
        /// </summary>
        private static bool UsesCustomLauncherManifest()
        {
            try
            {
                var settings = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
                var so = new SerializedObject(settings);
                var prop = so.FindProperty("useCustomLauncherManifest");
                return prop != null && prop.boolValue;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        // SDK-specific patterns to detect and remove
        // Note: Only SDKs that can be uninstalled (FullOnly) need orphan cleanup
        private static readonly Dictionary<SdkId, string[]> SdkManifestPatterns = new()
        {
            [SdkId.Adjust] = new[]
            {
                "com.adjust.sdk"
            }
        };

        private const string ActivityClass = "com.unity3d.player.UnityPlayerActivity";
        private const string GameActivityClass = "com.unity3d.player.UnityPlayerGameActivity";

        /// <summary>
        ///     The correct main activity depends on the Application Entry point selected
        ///     in Player Settings (Android tab). We read the serialized property directly
        ///     to work across all Unity versions without enum API differences.
        ///     Bitmask: 1 = Activity (legacy), 2 = GameActivity, 3 = both.
        ///     If GameActivity bit is set (value & 2), returns GameActivity; otherwise Activity.
        /// </summary>
        public static string GetExpectedMainActivity()
        {
            try
            {
                var settings = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
                var so = new SerializedObject(settings);
                var prop = so.FindProperty("androidApplicationEntry");
                if (prop != null)
                    return (prop.intValue & 2) != 0 ? GameActivityClass : ActivityClass;
            }
            catch (System.Exception)
            {
                // Property doesn't exist on older Unity versions - fall through
            }

            return ActivityClass;
        }

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
                if (activityName != null && activityName != GetExpectedMainActivity())
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
            var expected = GetExpectedMainActivity();
            if (nameAttr == null || nameAttr.Value == expected)
                return false;

            Debug.Log($"{Tag} Fixing main activity: {nameAttr.Value} → {expected}");
            nameAttr.Value = expected;
            return true;
        }

        /// <summary>
        ///     Detect if LauncherManifest.xml is missing the launcher activity when useCustomLauncherManifest is enabled.
        ///     Returns a description of the issue, or null if OK.
        /// </summary>
        public static string DetectLauncherManifestIssue()
        {
            if (!UsesCustomLauncherManifest())
                return null;

            if (!File.Exists(LauncherManifestPath))
                return "useCustomLauncherManifest is enabled but LauncherManifest.xml does not exist";

            try
            {
                var doc = XDocument.Load(LauncherManifestPath);
                var application = doc.Root?.Element("application");
                if (application == null)
                    return "LauncherManifest.xml has no <application> element";

                var launcherActivity = application.Elements("activity")
                    .FirstOrDefault(a => a.Elements("intent-filter")
                        .Any(f => f.Elements("category")
                            .Any(c => c.Attribute(AndroidNs + "name")?.Value ==
                                      "android.intent.category.LAUNCHER")));

                if (launcherActivity == null)
                    return "LauncherManifest.xml has no activity with MAIN/LAUNCHER intent filter - app will not launch";

                var activityName = launcherActivity.Attribute(AndroidNs + "name")?.Value;
                var expected = GetExpectedMainActivity();
                if (activityName != null && activityName != expected)
                    return $"LauncherManifest.xml has wrong activity class: {activityName} (expected {expected})";
            }
            catch (System.Exception e)
            {
                return $"Failed to parse LauncherManifest.xml: {e.Message}";
            }

            return null;
        }

        /// <summary>
        ///     Fix LauncherManifest.xml to have the correct launcher activity.
        /// </summary>
        public static bool FixLauncherManifest()
        {
            if (!UsesCustomLauncherManifest() || !File.Exists(LauncherManifestPath))
                return false;

            try
            {
                XNamespace tools = "http://schemas.android.com/tools";
                var doc = XDocument.Load(LauncherManifestPath);
                var application = doc.Root?.Element("application");
                if (application == null)
                    return false;

                var expected = GetExpectedMainActivity();
                var isGameActivity = expected == GameActivityClass;
                var theme = isGameActivity ? "@style/BaseUnityGameActivityTheme" : "@style/UnityThemeSelector";

                // Find existing launcher activity
                var launcherActivity = application.Elements("activity")
                    .FirstOrDefault(a => a.Elements("intent-filter")
                        .Any(f => f.Elements("category")
                            .Any(c => c.Attribute(AndroidNs + "name")?.Value ==
                                      "android.intent.category.LAUNCHER")));

                if (launcherActivity != null)
                {
                    // Fix existing activity
                    var nameAttr = launcherActivity.Attribute(AndroidNs + "name");
                    if (nameAttr != null && nameAttr.Value == expected)
                        return false; // Already correct

                    if (nameAttr != null)
                    {
                        Debug.Log($"{Tag} Fixing LauncherManifest.xml activity: {nameAttr.Value} -> {expected}");
                        nameAttr.Value = expected;
                    }

                    launcherActivity.SetAttributeValue(AndroidNs + "theme", theme);
                }
                else
                {
                    // Add launcher activity
                    Debug.Log($"{Tag} Adding launcher activity to LauncherManifest.xml: {expected}");
                    var activity = new XElement("activity",
                        new XAttribute(AndroidNs + "name", expected),
                        new XAttribute(AndroidNs + "theme", theme),
                        new XAttribute(AndroidNs + "enabled", "true"),
                        new XAttribute(AndroidNs + "exported", "true"),
                        new XAttribute(tools + "replace", "android:enabled"),
                        new XElement("intent-filter",
                            new XElement("action", new XAttribute(AndroidNs + "name", "android.intent.action.MAIN")),
                            new XElement("category", new XAttribute(AndroidNs + "name", "android.intent.category.LAUNCHER"))
                        ),
                        new XElement("meta-data",
                            new XAttribute(AndroidNs + "name", "unityplayer.UnityActivity"),
                            new XAttribute(AndroidNs + "value", "true")
                        ),
                        new XElement("meta-data",
                            new XAttribute(AndroidNs + "name", "android.app.lib_name"),
                            new XAttribute(AndroidNs + "value", "game")
                        )
                    );
                    application.Add(activity);
                }

                var backupPath = LauncherManifestPath + ".backup";
                File.Copy(LauncherManifestPath, backupPath, true);
                doc.Save(LauncherManifestPath);
                Debug.Log($"{Tag} LauncherManifest.xml fixed (backup at {backupPath})");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{Tag} Failed to fix LauncherManifest.xml: {e.Message}");
                return false;
            }
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
