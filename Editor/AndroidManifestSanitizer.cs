using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Single owner of all Android manifest logic: source manifests (pre-build)
    ///     and auto-generated Gradle manifests (post-export).
    /// </summary>
    public static class AndroidManifestSanitizer
    {
        // -- Configuration --

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
        private const string ActivityTheme = "@style/UnityThemeSelector";
        private const string GameActivityTheme = "@style/BaseUnityGameActivityTheme";

        /// <summary>
        ///     Returns the correct theme for the expected main activity class.
        /// </summary>
        public static string GetExpectedTheme()
        {
            return GetExpectedMainActivity() == GameActivityClass ? GameActivityTheme : ActivityTheme;
        }

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

        /// <summary>
        ///     Raw androidApplicationEntry value for diagnostics. Returns -1 if unavailable.
        /// </summary>
        internal static int GetApplicationEntryRaw()
        {
            try
            {
                var settings = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
                var so = new SerializedObject(settings);
                var prop = so.FindProperty("androidApplicationEntry");
                return prop?.intValue ?? -1;
            }
            catch
            {
                return -1;
            }
        }

        // -- Shared Utilities --

        internal static readonly XNamespace AndroidNs = "http://schemas.android.com/apk/res/android";
        private static readonly XNamespace ToolsNs = "http://schemas.android.com/tools";

        /// <summary>
        ///     Find the activity element with a LAUNCHER intent-filter category.
        /// </summary>
        internal static XElement FindLauncherActivity(XElement application)
        {
            return application?.Elements("activity")
                .FirstOrDefault(a => a.Elements("intent-filter")
                    .Any(f => f.Elements("category")
                        .Any(c => c.Attribute(AndroidNs + "name")?.Value ==
                                  "android.intent.category.LAUNCHER")));
        }

        // -- Library Manifest Detection --

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

        // -- Launcher Manifest Detection --

        /// <summary>
        ///     Detect if the launcher activity uses the wrong class name.
        ///     Returns the wrong class name if found, or null if correct.
        /// </summary>
        public static string DetectWrongMainActivity()
        {
            if (!File.Exists(AndroidManifestPath))
                return null;

            try
            {
                var xmlContent = File.ReadAllText(AndroidManifestPath);
                return DetectWrongMainActivityInXml(xmlContent, GetExpectedMainActivity());
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{Tag} Failed to detect main activity: {e.Message}");
            }

            return null;
        }

        /// <summary>
        ///     Pure XML detection logic - testable without file I/O or PlayerSettings.
        /// </summary>
        internal static string DetectWrongMainActivityInXml(string xmlContent, string expectedActivity)
        {
            var doc = XDocument.Parse(xmlContent);
            var launcherActivity = FindLauncherActivity(doc.Root?.Element("application"));
            if (launcherActivity == null)
                return null;

            var activityName = launcherActivity.Attribute(AndroidNs + "name")?.Value;
            if (activityName != null && activityName != expectedActivity)
                return activityName;

            return null;
        }

        /// <summary>
        ///     Detect if the launcher activity in AndroidManifest.xml has a theme mismatch
        ///     or is missing tools:replace="android:theme". Either condition causes Gradle
        ///     merge conflicts when Unity's auto-generated base manifest has a different theme.
        /// </summary>
        public static string DetectThemeMismatch()
        {
            if (!File.Exists(AndroidManifestPath))
                return null;

            try
            {
                var doc = XDocument.Load(AndroidManifestPath);
                var launcherActivity = FindLauncherActivity(doc.Root?.Element("application"));
                if (launcherActivity == null)
                    return null;

                var expectedTheme = GetExpectedTheme();
                var themeAttr = launcherActivity.Attribute(AndroidNs + "theme");
                if (themeAttr != null && themeAttr.Value != expectedTheme)
                    return $"Wrong theme: {themeAttr.Value} (expected {expectedTheme})";

                // tools:replace only needed when a custom launcher manifest exists -
                // can't predict its theme, so tools:replace prevents merge conflicts.
                // When themes match the expected value, identical attributes merge
                // cleanly without tools:replace.
                if (UsesCustomLauncherManifest())
                {
                    var replaceAttr = launcherActivity.Attribute(ToolsNs + "replace");
                    if (replaceAttr == null || !replaceAttr.Value.Contains("android:theme"))
                        return "Missing tools:replace=\"android:theme\" - will cause Gradle merge conflict";
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{Tag} Failed to detect theme mismatch: {e.Message}");
            }

            return null;
        }

        // -- Fixes --

        /// <summary>
        ///     Fix the launcher activity class name, theme, and optionally tools:replace.
        ///     tools:replace is only needed when a custom launcher manifest exists - it prevents
        ///     Gradle merge conflicts when two modules declare the same activity with different themes.
        /// </summary>
        /// <param name="doc">The manifest document to fix.</param>
        /// <param name="requireToolsReplace">
        ///     When true, ensures tools:replace="android:theme" is present.
        ///     Pass true for launcher manifests; pass UsesCustomLauncherManifest() for library manifests.
        /// </param>
        internal static bool FixMainActivity(XDocument doc, bool requireToolsReplace)
        {
            var launcherActivity = FindLauncherActivity(doc.Root?.Element("application"));
            if (launcherActivity == null)
                return false;

            var modified = false;
            var expected = GetExpectedMainActivity();
            var expectedTheme = GetExpectedTheme();

            var nameAttr = launcherActivity.Attribute(AndroidNs + "name");
            if (nameAttr != null && nameAttr.Value != expected)
            {
                Debug.Log($"{Tag} Fixing main activity: {nameAttr.Value} → {expected} " +
                          $"(androidApplicationEntry={GetApplicationEntryRaw()})");
                nameAttr.Value = expected;
                modified = true;
            }

            var themeAttr = launcherActivity.Attribute(AndroidNs + "theme");
            if (themeAttr != null && themeAttr.Value != expectedTheme)
            {
                Debug.Log($"{Tag} Fixing activity theme: {themeAttr.Value} → {expectedTheme}");
                themeAttr.Value = expectedTheme;
                modified = true;
            }

            if (requireToolsReplace)
            {
                EnsureToolsNamespace(doc);
                var replaceAttr = launcherActivity.Attribute(ToolsNs + "replace");
                var replaceValue = replaceAttr?.Value ?? "";
                if (!replaceValue.Contains("android:theme"))
                {
                    var newReplace = string.IsNullOrEmpty(replaceValue)
                        ? "android:theme"
                        : replaceValue + ",android:theme";
                    launcherActivity.SetAttributeValue(ToolsNs + "replace", newReplace);
                    Debug.Log($"{Tag} Added tools:replace=\"{newReplace}\" to prevent theme merge conflict");
                    modified = true;
                }
            }

            return modified;
        }

        /// <summary>
        ///     Ensure the tools namespace is declared on the root manifest element.
        /// </summary>
        internal static void EnsureToolsNamespace(XDocument doc)
        {
            if (doc.Root != null && doc.Root.Attribute(XNamespace.Xmlns + "tools") == null)
                doc.Root.Add(new XAttribute(XNamespace.Xmlns + "tools", ToolsNs.NamespaceName));
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
                var xmlContent = File.ReadAllText(LauncherManifestPath);
                return DetectLauncherManifestIssueInXml(xmlContent, GetExpectedMainActivity());
            }
            catch (System.Exception e)
            {
                return $"Failed to parse LauncherManifest.xml: {e.Message}";
            }
        }

        /// <summary>
        ///     Pure XML detection logic - testable without file I/O or PlayerSettings.
        /// </summary>
        internal static string DetectLauncherManifestIssueInXml(string xmlContent, string expectedActivity)
        {
            var doc = XDocument.Parse(xmlContent);
            var application = doc.Root?.Element("application");
            if (application == null)
                return "LauncherManifest.xml has no <application> element";

            var launcherActivity = FindLauncherActivity(application);
            if (launcherActivity == null)
                return "LauncherManifest.xml has no activity with MAIN/LAUNCHER intent filter - app will not launch";

            var activityName = launcherActivity.Attribute(AndroidNs + "name")?.Value;
            if (activityName != null && activityName != expectedActivity)
                return $"LauncherManifest.xml has wrong activity class: {activityName} (expected {expectedActivity})";

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
                var doc = XDocument.Load(LauncherManifestPath);
                var application = doc.Root?.Element("application");
                if (application == null)
                    return false;

                var expected = GetExpectedMainActivity();
                var theme = GetExpectedTheme();

                var launcherActivity = FindLauncherActivity(application);
                const string toolsReplace = "android:enabled,android:theme";

                if (launcherActivity != null)
                {
                    var modified = false;

                    var nameAttr = launcherActivity.Attribute(AndroidNs + "name");
                    if (nameAttr != null && nameAttr.Value != expected)
                    {
                        Debug.Log($"{Tag} Fixing LauncherManifest.xml activity: {nameAttr.Value} -> {expected}");
                        nameAttr.Value = expected;
                        modified = true;
                    }

                    // Fix theme + add tools:replace to prevent Gradle merge conflicts
                    var themeAttr = launcherActivity.Attribute(AndroidNs + "theme");
                    if (themeAttr == null || themeAttr.Value != theme)
                    {
                        launcherActivity.SetAttributeValue(AndroidNs + "theme", theme);
                        modified = true;
                    }

                    var replaceAttr = launcherActivity.Attribute(ToolsNs + "replace");
                    if (replaceAttr == null || replaceAttr.Value != toolsReplace)
                    {
                        launcherActivity.SetAttributeValue(ToolsNs + "replace", toolsReplace);
                        modified = true;
                    }

                    if (!modified)
                        return false;
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
                        new XAttribute(ToolsNs + "replace", toolsReplace),
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

        // -- Orchestration --

        /// <summary>
        ///     Post-fix diagnostics: what was fixed and what remains unfixed.
        ///     Passed to CheckAndroidManifest() to avoid redundant detection.
        /// </summary>
        internal class ManifestDiagnostics
        {
            public List<string> Fixes = new();
            public List<(SdkId sdk, string[] entries)> RemainingOrphans = new();
            public List<string> RemainingDuplicates = new();
            public string RemainingWrongActivity;
            public string RemainingThemeMismatch;
            public string RemainingLauncherIssue;

            public bool HasRemainingIssues =>
                RemainingOrphans.Count > 0 || RemainingDuplicates.Count > 0
                || RemainingWrongActivity != null || RemainingThemeMismatch != null
                || RemainingLauncherIssue != null;
        }

        /// <summary>
        ///     Sanitize all manifests and return diagnostics (fixes applied + remaining issues).
        ///     Use this when you need both the fix list AND remaining issues (e.g. BuildValidator).
        /// </summary>
        internal static ManifestDiagnostics SanitizeWithDiagnostics(bool refreshAssetDatabase)
        {
            var diag = new ManifestDiagnostics();

            // --- Library manifest (AndroidManifest.xml) ---
            if (File.Exists(AndroidManifestPath))
            {
                var orphaned = DetectOrphanedEntries();
                var duplicates = DetectDuplicateActivities();
                var wrongActivity = DetectWrongMainActivity();
                var themeMismatch = DetectThemeMismatch();

                if (orphaned.Count > 0 || duplicates.Count > 0 || wrongActivity != null || themeMismatch != null)
                {
                    try
                    {
                        var doc = XDocument.Load(AndroidManifestPath);
                        var libraryFixCount = 0;

                        foreach (var (sdkId, patterns) in orphaned)
                        {
                            if (RemoveSdkEntries(doc, patterns))
                            {
                                var sdkName = SdkRegistry.All[sdkId].Name;
                                Debug.Log($"{Tag} Removed {sdkName} entries from AndroidManifest.xml");
                                diag.Fixes.Add($"Removed {sdkName} entries from AndroidManifest.xml");
                                libraryFixCount++;
                            }
                        }

                        if (duplicates.Count > 0 && RemoveDuplicateActivities(doc))
                        {
                            Debug.Log($"{Tag} Removed {duplicates.Count} duplicate activity declaration(s)");
                            diag.Fixes.Add($"Removed {duplicates.Count} duplicate activity declaration(s)");
                            libraryFixCount++;
                        }

                        if ((wrongActivity != null || themeMismatch != null) && FixMainActivity(doc, UsesCustomLauncherManifest()))
                        {
                            if (wrongActivity != null)
                            {
                                Debug.Log($"{Tag} Fixed main activity: {wrongActivity} -> {GetExpectedMainActivity()}");
                                diag.Fixes.Add($"Fixed main activity: {wrongActivity} -> {GetExpectedMainActivity()}");
                                libraryFixCount++;
                            }
                            if (themeMismatch != null)
                            {
                                Debug.Log($"{Tag} Fixed theme mismatch: {themeMismatch}");
                                diag.Fixes.Add($"Fixed theme mismatch: {themeMismatch}");
                                libraryFixCount++;
                            }
                        }

                        if (libraryFixCount > 0)
                        {
                            var backupPath = AndroidManifestPath + ".backup";
                            File.Copy(AndroidManifestPath, backupPath, true);
                            doc.Save(AndroidManifestPath);

                            if (refreshAssetDatabase)
                                AssetDatabase.Refresh();

                            Debug.Log($"{Tag} AndroidManifest.xml sanitized successfully (backup at {backupPath})");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"{Tag} Failed to sanitize AndroidManifest.xml: {e.Message}");
                    }

                    // Re-detect after fixes to capture remaining issues
                    diag.RemainingOrphans = DetectOrphanedEntries();
                    diag.RemainingDuplicates = DetectDuplicateActivities();
                    diag.RemainingWrongActivity = DetectWrongMainActivity();
                    diag.RemainingThemeMismatch = DetectThemeMismatch();
                }
            }

            // --- Launcher manifest (LauncherManifest.xml, Unity 6 split module) ---
            var launcherIssue = DetectLauncherManifestIssue();
            if (launcherIssue != null)
            {
                if (FixLauncherManifest())
                    diag.Fixes.Add($"Fixed LauncherManifest.xml: {launcherIssue}");
                diag.RemainingLauncherIssue = DetectLauncherManifestIssue();
            }

            if (diag.Fixes.Count == 0)
                Debug.Log($"{Tag} No manifest issues found");

            return diag;
        }

        /// <summary>
        ///     Sanitize all manifests. Returns list of fix descriptions (empty if nothing changed).
        /// </summary>
        public static List<string> Sanitize(bool refreshAssetDatabase = true)
        {
            return SanitizeWithDiagnostics(refreshAssetDatabase).Fixes;
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
                var nameAttr = element.Attribute(AndroidNs + "name");
                var authAttr = element.Attribute(AndroidNs + "authorities");
                var valueAttr = element.Attribute(AndroidNs + "value");

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

        // -- Post-Export (auto-generated Gradle project manifests) --

        /// <summary>
        ///     Enforce manifest invariants on the auto-generated launcher manifest
        ///     (activity class, theme, tools:replace). Called by GradlePropertiesFixer
        ///     during IPostGenerateGradleAndroidProject.
        /// </summary>
        /// <param name="unityLibraryPath">
        ///     The path passed by Unity to IPostGenerateGradleAndroidProject (points to unityLibrary).
        /// </param>
        internal static void FixAutoGeneratedLauncherManifest(string unityLibraryPath)
        {
            var launcherManifestPath = Path.GetFullPath(
                Path.Combine(unityLibraryPath, "..", "launcher", "src", "main", "AndroidManifest.xml"));

            if (!File.Exists(launcherManifestPath))
                return;

            try
            {
                var doc = XDocument.Load(launcherManifestPath);
                if (FixMainActivity(doc, requireToolsReplace: true))
                {
                    doc.Save(launcherManifestPath);
                    Debug.Log($"{Tag} Launcher manifest patched");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{Tag} Failed to patch launcher manifest: {e.Message}");
            }
        }

        /// <summary>
        ///     Strip LAUNCHER intent-filter from unityLibrary's auto-generated manifest.
        ///     Unity 6 splits the build into launcher + unityLibrary modules. If both define
        ///     a LAUNCHER activity, the merged APK gets two - Android hides such apps.
        /// </summary>
        /// <param name="unityLibraryPath">
        ///     The path passed by Unity to IPostGenerateGradleAndroidProject (points to unityLibrary).
        /// </param>
        internal static void StripLibraryLauncherIntent(string unityLibraryPath)
        {
            var libraryManifestPath = Path.Combine(unityLibraryPath, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(libraryManifestPath))
                return;

            // Only strip if the launcher module actually owns the LAUNCHER intent.
            // Unity 6 always generates the launcher module directory even when
            // useCustomLauncherManifest is OFF, but the auto-generated manifest
            // may be empty (no activity). Stripping from unityLibrary in that case
            // would leave no LAUNCHER activity anywhere - the app becomes unlaunched.
            var launcherManifestPath = Path.GetFullPath(
                Path.Combine(unityLibraryPath, "..", "launcher", "src", "main", "AndroidManifest.xml"));
            if (!File.Exists(launcherManifestPath))
                return;

            try
            {
                var launcherDoc = XDocument.Load(launcherManifestPath);
                if (FindLauncherActivity(launcherDoc.Root?.Element("application")) == null)
                {
                    Debug.Log($"{Tag} Launcher module has no LAUNCHER activity - keeping it in unityLibrary");
                    return;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{Tag} Failed to parse launcher manifest, skipping strip: {e.Message}");
                return;
            }

            try
            {
                var doc = XDocument.Load(libraryManifestPath);
                var application = doc.Root?.Element("application");
                if (application == null)
                    return;

                var modified = false;

                foreach (var activity in application.Elements("activity").ToList())
                {
                    var launcherFilters = activity.Elements("intent-filter")
                        .Where(f => f.Elements("category")
                            .Any(c => c.Attribute(AndroidNs + "name")?.Value ==
                                      "android.intent.category.LAUNCHER"))
                        .ToList();

                    foreach (var filter in launcherFilters)
                    {
                        var activityName = activity.Attribute(AndroidNs + "name")?.Value ?? "?";
                        Debug.Log($"{Tag} Stripping LAUNCHER intent from unityLibrary activity: {activityName}");
                        filter.Remove();
                        modified = true;
                    }
                }

                if (modified)
                {
                    doc.Save(libraryManifestPath);
                    Debug.Log($"{Tag} unityLibrary manifest: LAUNCHER intent stripped (launcher module owns it)");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{Tag} Failed to strip library LAUNCHER intent: {e.Message}");
            }
        }

    }
}
