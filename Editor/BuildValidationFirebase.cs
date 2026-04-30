using System.Collections.Generic;
using System.Linq;
using UnityEditor;

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
                results.Add(Warning(
                    CheckCategory.FirebaseCoherence,
                    "Firebase not installed (required in Full mode)",
                    "Run setup or open Palette > Configuration to install Firebase."));
            }
            else
            {
                // Firebase missing in Prototype mode — silently valid (optional)
                results.Add(Valid(CheckCategory.FirebaseCoherence, "Firebase not installed (optional in Prototype)"));
            }

            return results;
        }

        /// <summary>
        ///     Check Firebase config files (google-services.json / GoogleService-Info.plist) for active build target.
        /// </summary>
        static List<ValidationResult> CheckFirebaseConfigFiles(Dictionary<string, object> dependencies)
        {
            var results = new List<ValidationResult>();
            const CheckCategory category = CheckCategory.FirebaseConfig;

            bool hasFirebase = dependencies.ContainsKey(SdkRegistry.All[SdkId.FirebaseAnalytics].PackageId);
            if (!hasFirebase)
            {
                results.Add(Valid(category, "Firebase not installed, config check skipped"));
                return results;
            }

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;

            if (target == BuildTarget.Android && !SdkConfigDetector.IsFirebaseAndroidConfigured())
            {
                results.Add(Error(
                    category,
                    "google-services.json not found",
                    "Download from Firebase Console > Project Settings > Android app and place in Assets/"));
            }
            else if (target == BuildTarget.iOS && !SdkConfigDetector.IsFirebaseIOSConfigured())
            {
                results.Add(Error(
                    category,
                    "GoogleService-Info.plist not found",
                    "Download from Firebase Console > Project Settings > iOS app and place in Assets/"));
            }
            else
            {
                results.Add(Valid(category, "Firebase config files present"));
            }

            return results;
        }
    }
}
