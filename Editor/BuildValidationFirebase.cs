using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace Sorolla.Palette.Editor
{
    public static partial class BuildValidator
    {
        /// <summary>
        ///     Check Firebase module coherence - FirebaseApp required if other modules installed.
        /// </summary>
        static List<ValidationResult> CheckFirebaseCoherence(Dictionary<string, object> dependencies)
        {
            var results = new List<ValidationResult>();

            var firebaseModules = new[]
            {
                SdkId.FirebaseAnalytics,
                SdkId.FirebaseCrashlytics,
                SdkId.FirebaseRemoteConfig,
            };

            bool hasFirebaseApp = dependencies.ContainsKey(SdkRegistry.All[SdkId.FirebaseApp].PackageId);
            var installedModules = firebaseModules
                .Where(id => dependencies.ContainsKey(SdkRegistry.All[id].PackageId))
                .Select(id => SdkRegistry.All[id].Name)
                .ToList();

            if (installedModules.Count > 0 && !hasFirebaseApp)
            {
                results.Add(Error(
                    CheckCategory.FirebaseCoherence,
                    $"Firebase modules installed without FirebaseApp:\n  {string.Join(", ", installedModules)}",
                    "Install com.google.firebase.app or remove Firebase modules"));
            }
            else if (installedModules.Count > 0)
            {
                results.Add(Valid(CheckCategory.FirebaseCoherence, "Firebase modules OK"));
            }
            else if (!SorollaSettings.IsPrototype)
            {
                // Firebase missing in Full mode — warn
                // Fix hint no longer tells you to open the window you're already inside (F6, 2026-07-21
                // audit) - points at the actual SDK Overview row below instead.
                results.Add(Warning(
                    CheckCategory.FirebaseCoherence,
                    "Firebase not installed (required in Full mode)",
                    "Install Firebase from the SDK Overview row below."));
            }
            else
            {
                // Firebase missing in Prototype mode - optional, so this is an absence notice, not a pass:
                // nothing was verified here.
                results.Add(Skipped(CheckCategory.FirebaseCoherence, "Firebase not installed (optional in Prototype)"));
            }

            return results;
        }

        /// <summary>
        ///     Check the active platform's Firebase config file: not just present, but carrying the matching
        ///     application id (a copied wrong-game google-services.json / GoogleService-Info.plist is a
        ///     silent-data-corruption source Firebase cannot report at runtime). Honest limit: the bundle-id
        ///     match cannot prove Firebase PROJECT identity (see <see cref="FirebaseConfigMatch"/>).
        ///
        ///     Only the ACTIVE build target is judged (2026-07-23). Each platform keeps its own check category
        ///     and gate - that split (2026-07-22) is what lets one platform's file be judged without the other
        ///     hiding it - but the gate for the platform this build is not for resolves NotApplicable in the
        ///     catalog, so no observation may be produced for it: a report judges the platform in front of it.
        ///     The previous shape graded the other platform too, demoted to a Warning; that warning could never
        ///     be cleared by a game deliberately shipping one platform, so it permanently blocked a green
        ///     verdict. The other platform's row still prints in the copied report as NotApplicable, and its
        ///     checks return in full the moment the build target switches.
        /// </summary>
        static List<ValidationResult> CheckFirebaseConfigFiles(Dictionary<string, object> dependencies)
        {
            // In Full mode Firebase is required, so a missing config file that makes Firebase initialization
            // fail must BLOCK, not merely warn (review F4-05) - otherwise the "Required" label on the config
            // gate is decoration. In Prototype Firebase is optional, so a MISSING file stays a warning.
            bool required = !SorollaSettings.IsPrototype;
            bool hasFirebase = dependencies.ContainsKey(SdkRegistry.All[SdkId.FirebaseAnalytics].PackageId);

            switch (EditorUserBuildSettings.activeBuildTarget)
            {
                case BuildTarget.Android:
                    return One(hasFirebase
                        ? CheckAndroidConfig(required)
                        : Skipped(CheckCategory.FirebaseConfigAndroid, "Firebase not installed, config check skipped"));
                case BuildTarget.iOS:
                    return One(hasFirebase
                        ? CheckIosConfig(required)
                        : Skipped(CheckCategory.FirebaseConfigIos, "Firebase not installed, config check skipped"));
                default:
                    // Neither gate applies off-mobile, and an observation against a NotApplicable gate is a
                    // context mismatch - so say nothing rather than report a skip.
                    return new List<ValidationResult>();
            }
        }

        static List<ValidationResult> One(ValidationResult result) => new List<ValidationResult> { result };

        static ValidationResult CheckAndroidConfig(bool required)
        {
            const CheckCategory category = CheckCategory.FirebaseConfigAndroid;
            List<string> candidates = SdkConfigDetector.FirebaseAndroidConfigPaths();
            if (candidates.Count == 0)
                return MissingConfig(category, required,
                    "Assets/google-services.json not found.\n" +
                    "  Firebase Android (Analytics/Crashlytics/Remote Config) will fail to initialize on this platform.",
                    "Download from Firebase Console > Project Settings > Android app and place in Assets/");
            if (candidates.Count > 1)
                return Unverifiable(category,
                    $"Multiple google-services.json files found - cannot determine which one ships:\n  {string.Join("\n  ", candidates)}",
                    "Keep exactly one google-services.json (in Assets/) so the active app's config is unambiguous.");

            if (!TryReadFile(candidates[0], out string json, out ValidationResult readError, category))
                return readError;

            string appId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            switch (FirebaseConfigMatch.MatchAndroid(json, appId, out IReadOnlyCollection<string> found))
            {
                case FirebaseConfigMatchResult.Match:
                    return Valid(category, $"google-services.json matches the Android application id ({appId}).");
                case FirebaseConfigMatchResult.Mismatch:
                    return Error(category,
                        "google-services.json is for a different app (wrong google-services.json copied in?).\n" +
                        $"  Android application id: {appId}\n" +
                        $"  Config package name(s): {(found.Count == 0 ? "(none found)" : string.Join(", ", found))}",
                        "Download the google-services.json for THIS app from Firebase Console > Project Settings, or correct the Android application id.");
                default:
                    return Unverifiable(category,
                        $"{candidates[0]} could not be parsed as a google-services.json.",
                        "Re-download the file from Firebase Console; it may be truncated or the wrong file.");
            }
        }

        static ValidationResult CheckIosConfig(bool required)
        {
            const CheckCategory category = CheckCategory.FirebaseConfigIos;
            List<string> candidates = SdkConfigDetector.FirebaseIosConfigPaths();
            if (candidates.Count == 0)
                return MissingConfig(category, required,
                    "GoogleService-Info.plist not found.\n" +
                    "  Firebase iOS (Analytics/Crashlytics/Remote Config) will fail to initialize on this platform.",
                    "Download from Firebase Console > Project Settings > iOS app and place in Assets/");
            if (candidates.Count > 1)
                return Unverifiable(category,
                    $"Multiple GoogleService-Info.plist files found - cannot determine which one ships:\n  {string.Join("\n  ", candidates)}",
                    "Keep exactly one GoogleService-Info.plist so the active app's config is unambiguous.");

            if (!TryReadFile(candidates[0], out string plist, out ValidationResult readError, category))
                return readError;

            string bundleId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            switch (FirebaseConfigMatch.MatchIos(plist, bundleId, out string found))
            {
                case FirebaseConfigMatchResult.Match:
                    return Valid(category, $"GoogleService-Info.plist matches the iOS bundle id ({bundleId}).");
                case FirebaseConfigMatchResult.Mismatch:
                    return Error(category,
                        "GoogleService-Info.plist is for a different app (wrong GoogleService-Info.plist copied in?).\n" +
                        $"  iOS bundle id: {bundleId}\n" +
                        $"  Config BUNDLE_ID: {found ?? "(none found)"}",
                        "Download the GoogleService-Info.plist for THIS app from Firebase Console > Project Settings, or correct the iOS bundle id.");
                default:
                    return Unverifiable(category,
                        $"{candidates[0]} could not be parsed as a GoogleService-Info.plist.",
                        "Re-download the file from Firebase Console; it may be truncated or the wrong file.");
            }
        }

        static bool TryReadFile(string path, out string contents, out ValidationResult error, CheckCategory category)
        {
            error = null;
            try
            {
                contents = File.ReadAllText(path);
                return true;
            }
            catch (Exception e)
            {
                contents = null;
                error = Unverifiable(category, $"Could not read {path}: {e.Message}",
                    "Confirm the file is readable, then re-run validation.");
                return false;
            }
        }

        static ValidationResult MissingConfig(CheckCategory category, bool block, string message, string fix) =>
            block ? Error(category, message, fix) : Warning(category, message, fix);
    }
}
