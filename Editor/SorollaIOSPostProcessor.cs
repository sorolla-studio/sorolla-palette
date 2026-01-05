#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    public static class SorollaIOSPostProcessor
    {
        [PostProcessBuild]
        public static void OnPostProcessBuild(BuildTarget buildTarget, string buildPath)
        {
            if (buildTarget != BuildTarget.iOS) return;

            string plistPath = buildPath + "/Info.plist";
            var plist = new PlistDocument();
            plist.ReadFromString(File.ReadAllText(plistPath));
            PlistElementDict rootDict = plist.root;

            // 1. ATT Description (MANDATORY)
            // Check if it already exists to avoid overwriting custom text if set elsewhere
            if (rootDict["NSUserTrackingUsageDescription"] == null)
            {
                string trackingDesc = "Your data will be used to provide a better and personalized ad experience.";
                rootDict.SetString("NSUserTrackingUsageDescription", trackingDesc);
                Debug.Log("[Palette] Added NSUserTrackingUsageDescription to Info.plist");
            }

            // 2. Add SKAdNetwork IDs
            // MAX SDK usually handles this if configured, but we ensure the array exists.
            // Ideally, we would merge a list of IDs here.
            if (rootDict["SKAdNetworkItems"] == null)
            {
                rootDict.CreateArray("SKAdNetworkItems");
            }

            File.WriteAllText(plistPath, plist.WriteToString());
        }
    }
}
#endif
