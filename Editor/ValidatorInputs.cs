using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     One cheap fingerprint of everything the validators read. The window compares it on a timer and
    ///     re-validates when it moves, so a report can never describe project state that has since changed.
    ///     A fingerprint rather than a watcher per input: watchers are a list that silently goes out of date
    ///     as checks are added, and the two the window started with (the config asset and a hand-picked set of
    ///     vendor files) already missed player settings and the Android build files.
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
                foreach (string relative in Inputs)
                {
                    string path = Path.Combine(projectRoot ?? "", relative);
                    long stamp = 0;
                    try
                    {
                        if (File.Exists(path))
                            stamp = File.GetLastWriteTimeUtc(path).Ticks;
                        else if (Directory.Exists(path))
                            stamp = Directory.GetLastWriteTimeUtc(path).Ticks;
                    }
                    catch (IOException)
                    {
                        // A file being written right now is a change in itself; the next poll reads it.
                        stamp = -1;
                    }
                    hash = hash * 397 + stamp.GetHashCode();
                }
                return hash;
            }
        }
    }
}
