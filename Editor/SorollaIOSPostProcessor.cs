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
        const string TrackingDescription = "This identifier lets us show you more relevant ads and measure ad performance - for example, ads for games similar to this one.";

        // The languages AppLovin's consent flow used to write its tracking prompt text in. English needs its
        // own folder too: without one, iOS picks a player's second language (French under an English phone
        // UI, say) over the Info.plist text.
        static readonly (string locale, string text)[] TrackingDescriptionTranslations =
        {
            ("en", TrackingDescription),
            ("de", "Mit dieser Kennung können wir dir relevantere Werbung zeigen und die Werbeleistung messen, zum Beispiel Werbung für Spiele, die diesem ähneln."),
            ("es", "Este identificador nos permite mostrarte anuncios más relevantes y medir su rendimiento; por ejemplo, anuncios de juegos similares a este."),
            ("fr", "Cet identifiant nous permet de vous montrer des publicités plus pertinentes et de mesurer leurs performances, par exemple des publicités pour des jeux similaires à celui-ci."),
            ("ja", "この識別子により、より関連性の高い広告を表示し、広告の効果を測定できます（例：このゲームに似たゲームの広告）。"),
            ("ko", "이 식별자를 통해 더 관련성 높은 광고를 보여 드리고 광고 성과를 측정할 수 있습니다. 예를 들어 이 게임과 비슷한 게임의 광고입니다."),
            ("zh-Hans", "此标识符让我们能够向您展示更相关的广告并衡量广告效果，例如与本游戏类似的游戏广告。"),
            ("zh-Hant", "此識別碼讓我們能夠向您顯示更相關的廣告並衡量廣告成效，例如與本遊戲類似的遊戲廣告。"),
        };

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
                rootDict.SetString("NSUserTrackingUsageDescription", TrackingDescription);
                Debug.Log("[Palette] Added NSUserTrackingUsageDescription to Info.plist");
            }
            // Translate only Palette's own text: a game that sets its own text owns its translations too.
            bool localizeTracking = rootDict["NSUserTrackingUsageDescription"].AsString() == TrackingDescription;

            // 2. Add SKAdNetwork IDs
            // MAX SDK usually handles this if configured, but we ensure the array exists.
            // Ideally, we would merge a list of IDs here.
            if (rootDict["SKAdNetworkItems"] == null)
            {
                rootDict.CreateArray("SKAdNetworkItems");
            }

            // 3. Consent Mode v2 DEFAULTS (Google Analytics for Firebase).
            // These govern the very first native ping — notably first_open, which fires at launch
            // BEFORE the runtime UMP/ATT flow resolves. analytics_storage defaults GRANTED so the install
            // is counted with an app-instance-id (otherwise it fires cookieless and is uncountable in
            // GA4 standard reports). Ad signals default DENIED until consent resolves at runtime.
            // Runtime SetConsent (FirebaseAdapterImpl) overrides these once consent is known.
            // Keys per Google tag-platform app-consent guide. Guarded so studio overrides win.
            SetDefaultBoolIfAbsent(rootDict, "GOOGLE_ANALYTICS_DEFAULT_ALLOW_ANALYTICS_STORAGE", true);
            SetDefaultBoolIfAbsent(rootDict, "GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_STORAGE", false);
            SetDefaultBoolIfAbsent(rootDict, "GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_USER_DATA", false);
            SetDefaultBoolIfAbsent(rootDict, "GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_PERSONALIZATION_SIGNALS", false);

            File.WriteAllText(plistPath, plist.WriteToString());

            RemoveAppLovinConsentFlow(buildPath);
            if (localizeTracking)
                LocalizeTrackingDescription(buildPath);
        }

        // 5. Palette's tracking prompt text in the languages above. Each <locale>.lproj folder is copied to
        // the bundle root, where iOS reads its InfoPlist.strings. An Append build over a project exported
        // while AppLovin's flow was on still references AppLovin's folder for the same language (AppLovin
        // empties it once its flow is off); both would land at the same place in the app, so it goes.
        static void LocalizeTrackingDescription(string buildPath)
        {
            string projectPath = PBXProject.GetPBXProjectPath(buildPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            string mainTarget = project.GetUnityMainTargetGuid();

            foreach ((string locale, string text) in TrackingDescriptionTranslations)
            {
                string appLovinFolder = project.FindFileGuidByProjectPath($"AppLovinMAXResources/{locale}.lproj");
                if (appLovinFolder != null)
                    project.RemoveFile(appLovinFolder);

                string folder = $"SorollaResources/{locale}.lproj";
                Directory.CreateDirectory(Path.Combine(buildPath, folder));
                File.WriteAllText(Path.Combine(buildPath, folder, "InfoPlist.strings"),
                    $"\"NSUserTrackingUsageDescription\" = \"{text}\";\n");
                if (!project.ContainsFileByProjectPath(folder))
                    project.AddFileToBuild(mainTarget, project.AddFolderReference(folder, folder));
            }

            project.WriteToFile(projectPath);
            Debug.Log("[Palette] Localized the tracking prompt text (en, de, es, fr, ja, ko, zh-Hans, zh-Hant)");
        }

        // 4. AppLovin's consent flow stays off: Palette runs Google UMP and ATT itself. AppLovin only ever
        // adds its flow entry to AppLovin-Settings.plist and never removes it, so an Append build over an
        // Xcode project made while the flow was on would still show AppLovin's flow at launch.
        static void RemoveAppLovinConsentFlow(string buildPath)
        {
            string settingsPath = Path.Combine(buildPath, "AppLovin-Settings.plist");
            if (!File.Exists(settingsPath)) return;

            var settings = new PlistDocument();
            settings.ReadFromFile(settingsPath);
            if (settings.root["ConsentFlowInfo"] == null) return;

            settings.root.values.Remove("ConsentFlowInfo");
            settings.WriteToFile(settingsPath);
            Debug.Log("[Palette] Removed AppLovin's consent flow from AppLovin-Settings.plist (Palette runs Google UMP and ATT itself)");
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
