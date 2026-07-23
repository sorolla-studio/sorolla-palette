using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     A best-effort fingerprint of the inputs the validators read. The window compares it on a timer
    ///     and re-validates when it moves, so an edit to a listed input refreshes the report without the
    ///     studio pressing anything. It is not a staleness guarantee: an input not in the list below moves
    ///     nothing, so a report can still describe state that changed somewhere unlisted.
    ///     Cost is a build-target read, a mode read, and a stat per path - polled at most once a second.
    /// </summary>
    static class ValidatorInputs
    {
        /// <summary>Every file the Build Health checks read, project-relative. A check that starts reading a
        /// new file adds it here; nothing else about freshness needs touching.</summary>
        static readonly string[] Inputs =
        {
            "ProjectSettings/ProjectSettings.asset",
            "ProjectSettings/EditorBuildSettings.asset",
            "Packages/manifest.json",
            "Packages/packages-lock.json",
            "Assets/Resources/SorollaConfig.asset",
            "Assets/Resources/GameAnalytics/Settings.asset",
            "Assets/Resources/FacebookSettings.asset",
            "Assets/Resources/AppLovinSettings.asset",
            "Assets/AppLovin/Resources/AppLovinSettings.asset",
            "Assets/MaxSdk/Resources/AppLovinSettings.asset",
            "Assets/google-services.json",
            "Assets/GoogleService-Info.plist",
            "Assets/StreamingAssets/google-services.json",
            "Assets/StreamingAssets/GoogleService-Info.plist",
            "Assets/Plugins/Android/AndroidManifest.xml",
            "Assets/Plugins/Android/LauncherManifest.xml",
            "Assets/Plugins/Android/gradleTemplate.properties",
            "Assets/Plugins/Android/mainTemplate.gradle",
            "Assets/Plugins/Android/launcherTemplate.gradle",
            "Assets/Plugins/Android/baseProjectTemplate.gradle",
            "Assets/Plugins/Android/proguard-user.txt",
            "Assets/AddressableAssetsData",
            "Assets/Sorolla.link.xml",
            "Library/BuildProfiles",
        };

        /// <summary>
        ///     Changes whenever anything a check reads changes. Missing files contribute a stable "absent"
        ///     value, so a file APPEARING moves the fingerprint exactly like an edit does.
        /// </summary>
        internal static int Fingerprint()
        {
            unchecked
            {
                int hash = (int)EditorUserBuildSettings.activeBuildTarget;
                hash = hash * 397 + (int)SorollaSettings.Mode;

                string projectRoot = Path.GetDirectoryName(Application.dataPath);

                // The keystore is configured rather than fixed, so its path is resolved instead of listed.
                string keystore = PlayerSettings.Android.keystoreName;
                hash = hash * 397 + (keystore ?? string.Empty).GetHashCode();
                if (!string.IsNullOrEmpty(keystore))
                    hash = hash * 397 + Stamp(Path.Combine(projectRoot ?? "", keystore)).GetHashCode();

                foreach (string relative in Inputs)
                    hash = hash * 397 + Stamp(Path.Combine(projectRoot ?? "", relative)).GetHashCode();

                return hash;
            }
        }

        static long Stamp(string path)
        {
            try
            {
                if (File.Exists(path)) return File.GetLastWriteTimeUtc(path).Ticks;
                if (Directory.Exists(path)) return Directory.GetLastWriteTimeUtc(path).Ticks;
                return 0;
            }
            catch (IOException)
            {
                // A file being written right now is a change in itself; the next poll reads it.
                return -1;
            }
        }
    }
}
