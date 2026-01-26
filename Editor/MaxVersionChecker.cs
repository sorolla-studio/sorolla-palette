using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Checks for AppLovin MAX SDK updates on Unity editor load.
    ///     Prompts user with Update/Skip/Later options when newer version available.
    /// </summary>
    public static class MaxVersionChecker
    {
        private const string Tag = "[Palette:MaxVersionChecker]";
        private const string PackageName = "com.applovin.mediation.ads";

        // EditorPrefs keys
        private const string PrefKeySkippedVersion = "Palette_MaxSkippedVersion";

        // Session state (resets on domain reload)
        private static bool s_checkedThisSession;

        [InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            // Delay to let editor fully initialize
            EditorApplication.delayCall += CheckForUpdatesAsync;
        }

        private static async void CheckForUpdatesAsync()
        {
            // Guard: Only check once per session
            if (s_checkedThisSession)
                return;
            s_checkedThisSession = true;

            // Guard: Only check if MAX is installed
            var installedVersion = GetInstalledVersion();
            if (string.IsNullOrEmpty(installedVersion))
                return; // MAX not installed, nothing to update

            // Query latest version from registry
            var latestVersion = await GetLatestVersionAsync();
            if (string.IsNullOrEmpty(latestVersion))
            {
                Debug.LogWarning($"{Tag} Could not query latest MAX version");
                return;
            }

            // Compare versions
            if (!IsNewerVersion(latestVersion, installedVersion))
            {
                Debug.Log($"{Tag} MAX SDK is up to date ({installedVersion})");
                return;
            }

            // Guard: User skipped this version?
            var skippedVersion = EditorPrefs.GetString(PrefKeySkippedVersion, "");
            if (skippedVersion == latestVersion)
            {
                Debug.Log($"{Tag} MAX {latestVersion} available but user skipped");
                return;
            }

            // Show update dialog
            ShowUpdateDialog(installedVersion, latestVersion);
        }

        /// <summary>
        ///     Get installed MAX version from manifest.json
        /// </summary>
        private static string GetInstalledVersion()
        {
            try
            {
                var manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
                if (!File.Exists(manifestPath))
                    return null;

                var json = File.ReadAllText(manifestPath);
                var manifest = MiniJson.Deserialize(json) as Dictionary<string, object>;

                if (manifest?.TryGetValue("dependencies", out var depsObj) == true &&
                    depsObj is Dictionary<string, object> deps &&
                    deps.TryGetValue(PackageName, out var versionObj))
                {
                    return versionObj?.ToString();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} Failed to read installed version: {e.Message}");
            }

            return null;
        }

        /// <summary>
        ///     Query latest compatible version from AppLovin scoped registry
        /// </summary>
        private static async Task<string> GetLatestVersionAsync()
        {
            try
            {
                var request = Client.Search(PackageName);

                // Wait for completion (async polling)
                while (!request.IsCompleted)
                {
                    await Task.Delay(100);
                }

                if (request.Status != StatusCode.Success)
                {
                    Debug.LogWarning($"{Tag} Registry query failed: {request.Error?.message}");
                    return null;
                }

                // Get first result's latest compatible version
                var results = request.Result;
                if (results != null && results.Length > 0)
                {
                    return results[0].versions.latestCompatible;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} Failed to query latest version: {e.Message}");
            }

            return null;
        }

        /// <summary>
        ///     Compare semver versions. Returns true if latest > installed.
        /// </summary>
        private static bool IsNewerVersion(string latest, string installed)
        {
            try
            {
                var latestParts = latest.Split('.');
                var installedParts = installed.Split('.');

                for (int i = 0; i < Math.Min(latestParts.Length, installedParts.Length); i++)
                {
                    if (int.TryParse(latestParts[i], out var latestNum) &&
                        int.TryParse(installedParts[i], out var installedNum))
                    {
                        if (latestNum > installedNum) return true;
                        if (latestNum < installedNum) return false;
                    }
                }

                // If all compared parts equal, longer version is newer
                return latestParts.Length > installedParts.Length;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     Show 3-button dialog: Update Now / Skip This Version / Remind Later
        /// </summary>
        private static void ShowUpdateDialog(string installed, string latest)
        {
            var choice = EditorUtility.DisplayDialogComplex(
                "MAX SDK Update Available",
                $"AppLovin MAX {latest} is available.\n\n" +
                $"You have: {installed}\n\n" +
                "Would you like to update now?",
                "Update Now",        // option 0
                "Skip This Version", // option 1
                "Remind Me Later"    // option 2
            );

            switch (choice)
            {
                case 0: // Update Now
                    UpdateToVersion(latest);
                    break;
                case 1: // Skip This Version
                    EditorPrefs.SetString(PrefKeySkippedVersion, latest);
                    Debug.Log($"{Tag} Skipped MAX {latest}");
                    break;
                case 2: // Remind Later
                    // Do nothing - will check again next session
                    Debug.Log($"{Tag} Will remind about MAX {latest} next session");
                    break;
            }
        }

        /// <summary>
        ///     Update manifest.json to new version and trigger Package Manager resolve
        /// </summary>
        private static void UpdateToVersion(string version)
        {
            Debug.Log($"{Tag} Updating MAX SDK to {version}...");

            // Use ManifestManager to update the dependency
            var success = ManifestManager.ModifyManifest((manifest, scopedRegistries) =>
            {
                if (manifest.TryGetValue("dependencies", out var depsObj) &&
                    depsObj is Dictionary<string, object> deps)
                {
                    deps[PackageName] = version;
                    Debug.Log($"{Tag} Updated {PackageName} to {version}");
                    return true;
                }

                return false;
            });

            if (success)
            {
                // Clear skipped version since user explicitly updated
                EditorPrefs.DeleteKey(PrefKeySkippedVersion);

                EditorUtility.DisplayDialog(
                    "MAX SDK Updated",
                    $"MAX SDK has been updated to {version}.\n\n" +
                    "Unity Package Manager will resolve the new version.",
                    "OK"
                );
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Update Failed",
                    "Failed to update manifest.json.\n\n" +
                    "Check the Console for details.",
                    "OK"
                );
            }
        }

        /// <summary>
        ///     Menu item for manual version check. Clears skip state and forces fresh check.
        /// </summary>
        [MenuItem("Palette/Tools/Check MAX Updates")]
        public static void CheckForUpdatesMenuItem()
        {
            // Reset session flag to force check
            s_checkedThisSession = false;

            // Clear skipped version for manual check
            var skipped = EditorPrefs.GetString(PrefKeySkippedVersion, "");
            if (!string.IsNullOrEmpty(skipped))
            {
                EditorPrefs.DeleteKey(PrefKeySkippedVersion);
                Debug.Log($"{Tag} Cleared skipped version ({skipped})");
            }

            CheckForUpdatesAsync();
        }
    }
}
