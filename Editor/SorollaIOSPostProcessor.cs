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

            // 3. Consent Mode v2 DEFAULTS (Google Analytics for Firebase).
            // These govern the very first native ping — notably first_open, which fires at launch
            // BEFORE the runtime CMP/ATT resolves. analytics_storage defaults GRANTED so the install
            // is counted with an app-instance-id (otherwise it fires cookieless and is uncountable in
            // GA4 standard reports). Ad signals default DENIED until the CMP resolves at runtime.
            // Runtime SetConsent (FirebaseAdapterImpl) overrides these once consent is known.
            // Keys per Google tag-platform app-consent guide. Guarded so studio overrides win.
            SetDefaultBoolIfAbsent(rootDict, "GOOGLE_ANALYTICS_DEFAULT_ALLOW_ANALYTICS_STORAGE", true);
            SetDefaultBoolIfAbsent(rootDict, "GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_STORAGE", false);
            SetDefaultBoolIfAbsent(rootDict, "GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_USER_DATA", false);
            SetDefaultBoolIfAbsent(rootDict, "GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_PERSONALIZATION_SIGNALS", false);

            File.WriteAllText(plistPath, plist.WriteToString());
        }

        static void SetDefaultBoolIfAbsent(PlistElementDict rootDict, string key, bool value)
        {
            if (rootDict[key] != null) return;
            rootDict.SetBoolean(key, value);
            Debug.Log($"[Palette] Set Info.plist {key}={value} (Consent Mode default)");
        }
    }
}
#endif
