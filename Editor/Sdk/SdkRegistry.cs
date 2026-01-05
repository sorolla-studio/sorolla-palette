using System.Collections.Generic;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Unique identifier for each SDK
    /// </summary>
    public enum SdkId
    {
        GameAnalytics,
        IosSupport,
        Facebook,
        AppLovinMAX,
        Adjust,
        ExternalDependencyManager,
        FirebaseApp,
        FirebaseAnalytics,
        FirebaseCrashlytics,
        FirebaseRemoteConfig
    }

    /// <summary>
    ///     SDK requirement level
    /// </summary>
    public enum SdkRequirement
    {
        /// <summary>Required in both modes</summary>
        Core,
        /// <summary>Required only in Prototype mode</summary>
        PrototypeOnly,
        /// <summary>Required only in Full mode, uninstalled in Prototype</summary>
        FullOnly,
        /// <summary>Required in Full mode, optional in Prototype (never uninstalled)</summary>
        FullRequired,
        /// <summary>Optional in all modes</summary>
        Optional
    }

    /// <summary>
    ///     Metadata for a single SDK
    /// </summary>
    public class SdkInfo
    {
        public SdkId Id;
        public string Name;
        public string PackageId;
        /// <summary>Version for OpenUPM packages (null = use InstallUrl instead)</summary>
        public string Version;
        /// <summary>Git URL for packages not on OpenUPM (null = use PackageId@Version)</summary>
        public string InstallUrl;
        /// <summary>OpenUPM scope required for this package (null = no scope needed)</summary>
        public string Scope;
        public string[] DetectionAssemblies;
        public string[] DetectionTypes;
        public SdkRequirement Requirement;

        /// <summary>Get the dependency value for manifest.json</summary>
        public string DependencyValue => !string.IsNullOrEmpty(InstallUrl) ? InstallUrl : Version;

        public bool IsRequiredFor(bool isPrototype) => Requirement switch
        {
            SdkRequirement.Core => true,
            SdkRequirement.PrototypeOnly => isPrototype,
            SdkRequirement.FullOnly => !isPrototype,
            SdkRequirement.FullRequired => !isPrototype,
            _ => false
        };

        public bool ShouldUninstallFor(bool isPrototype) => Requirement switch
        {
            SdkRequirement.PrototypeOnly => !isPrototype,
            SdkRequirement.FullOnly => isPrototype,
            // FullRequired: never uninstall (optional in Proto)
            _ => false
        };
    }

    /// <summary>
    ///     Single source of truth for all SDK metadata and versions.
    /// </summary>
    public static class SdkRegistry
    {
        // ============================================================
        // VERSION CONSTANTS - Update these when upgrading SDK versions
        // ============================================================
        public const string EDM_VERSION = "1.2.186";
        public const string GA_VERSION = "7.10.6";
        public const string MAX_VERSION = "8.5.0";
        public const string FB_VERSION = "18.0.1";
        public const string FIREBASE_VERSION = "12.10.1";

        public static readonly IReadOnlyDictionary<SdkId, SdkInfo> All = new Dictionary<SdkId, SdkInfo>
        {
            [SdkId.ExternalDependencyManager] = new()
            {
                Id = SdkId.ExternalDependencyManager,
                Name = "External Dependency Manager",
                PackageId = "com.google.external-dependency-manager",
                Version = EDM_VERSION,
                Scope = "com.google.external-dependency-manager",
                DetectionAssemblies = new[] { "Google.JarResolver" },
                DetectionTypes = System.Array.Empty<string>(),
                Requirement = SdkRequirement.Core
            },
            [SdkId.GameAnalytics] = new()
            {
                Id = SdkId.GameAnalytics,
                Name = "GameAnalytics",
                PackageId = "com.gameanalytics.sdk",
                Version = GA_VERSION,
                Scope = "com.gameanalytics",
                DetectionAssemblies = new[] { "GameAnalyticsSDK" },
                DetectionTypes = new[] { "GameAnalyticsSDK.GameAnalytics, GameAnalyticsSDK" },
                Requirement = SdkRequirement.Core
            },
            [SdkId.IosSupport] = new()
            {
                Id = SdkId.IosSupport,
                Name = "iOS Support (ATT)",
                PackageId = "com.unity.ads.ios-support",
                Version = "1.2.0",
                // No scope needed - Unity registry
                DetectionAssemblies = new[] { "Unity.Advertisement.IosSupport" },
                DetectionTypes = new[] { "Unity.Advertisement.IosSupport.ATTrackingStatusBinding, Unity.Advertisement.IosSupport" },
                Requirement = SdkRequirement.Core
            },
            [SdkId.Facebook] = new()
            {
                Id = SdkId.Facebook,
                Name = "Facebook SDK",
                PackageId = "com.lacrearthur.facebook-sdk-for-unity",
                // Use Git URL - works great, no need to change
                InstallUrl = "https://github.com/LaCreArthur/facebook-unity-sdk-upm.git",
                DetectionAssemblies = new[] { "Facebook.Unity" },
                DetectionTypes = new[] { "Facebook.Unity.FB, Facebook.Unity" },
                Requirement = SdkRequirement.PrototypeOnly
            },
            [SdkId.AppLovinMAX] = new()
            {
                Id = SdkId.AppLovinMAX,
                Name = "AppLovin MAX",
                PackageId = "com.applovin.mediation.ads",
                Version = MAX_VERSION,
                Scope = "com.applovin",
                DetectionAssemblies = new[] { "MaxSdk.Scripts", "applovin" },
                DetectionTypes = new[] { "MaxSdk, MaxSdk.Scripts", "MaxSdkBase, MaxSdk.Scripts" },
                Requirement = SdkRequirement.FullRequired
            },
            [SdkId.Adjust] = new()
            {
                Id = SdkId.Adjust,
                Name = "Adjust SDK",
                PackageId = "com.adjust.sdk",
                // Use Git URL - official distribution
                InstallUrl = "https://github.com/adjust/unity_sdk.git?path=Assets/Adjust",
                DetectionAssemblies = new[] { "com.adjust.sdk", "adjustsdk.scripts", "adjust" },
                DetectionTypes = new[] { "AdjustSdk.Adjust, AdjustSdk.Scripts" },
                Requirement = SdkRequirement.FullOnly
            },
            [SdkId.FirebaseApp] = new()
            {
                Id = SdkId.FirebaseApp,
                Name = "Firebase App",
                PackageId = "com.google.firebase.app",
                InstallUrl = "https://github.com/LaCreArthur/unity-firebase-app.git?path=FirebaseApp#" + FIREBASE_VERSION,
                DetectionAssemblies = new[] { "Firebase.App" },
                DetectionTypes = new[] { "Firebase.FirebaseApp, Firebase.App" },
                Requirement = SdkRequirement.Optional
            },
            [SdkId.FirebaseAnalytics] = new()
            {
                Id = SdkId.FirebaseAnalytics,
                Name = "Firebase Analytics",
                PackageId = "com.google.firebase.analytics",
                InstallUrl = "https://github.com/LaCreArthur/unity-firebase-app.git?path=FirebaseAnalytics#" + FIREBASE_VERSION,
                DetectionAssemblies = new[] { "Firebase.Analytics" },
                DetectionTypes = new[] { "Firebase.Analytics.FirebaseAnalytics, Firebase.Analytics" },
                Requirement = SdkRequirement.Optional
            },
            [SdkId.FirebaseCrashlytics] = new()
            {
                Id = SdkId.FirebaseCrashlytics,
                Name = "Firebase Crashlytics",
                PackageId = "com.google.firebase.crashlytics",
                InstallUrl = "https://github.com/LaCreArthur/unity-firebase-app.git?path=FirebaseCrashlytics#" + FIREBASE_VERSION,
                DetectionAssemblies = new[] { "Firebase.Crashlytics" },
                DetectionTypes = new[] { "Firebase.Crashlytics.Crashlytics, Firebase.Crashlytics" },
                Requirement = SdkRequirement.Optional
            },
            [SdkId.FirebaseRemoteConfig] = new()
            {
                Id = SdkId.FirebaseRemoteConfig,
                Name = "Firebase Remote Config",
                PackageId = "com.google.firebase.remote-config",
                InstallUrl = "https://github.com/LaCreArthur/unity-firebase-app.git?path=FirebaseRemoteConfig#" + FIREBASE_VERSION,
                DetectionAssemblies = new[] { "Firebase.RemoteConfig" },
                DetectionTypes = new[] { "Firebase.RemoteConfig.FirebaseRemoteConfig, Firebase.RemoteConfig" },
                Requirement = SdkRequirement.Optional
            }
        };

        /// <summary>
        ///     Get SDKs required for a mode
        /// </summary>
        public static IEnumerable<SdkInfo> GetRequired(bool isPrototype)
        {
            foreach (var sdk in All.Values)
                if (sdk.IsRequiredFor(isPrototype))
                    yield return sdk;
        }

        /// <summary>
        ///     Get SDKs that should be uninstalled for a mode
        /// </summary>
        public static IEnumerable<SdkInfo> GetToUninstall(bool isPrototype)
        {
            foreach (var sdk in All.Values)
                if (sdk.ShouldUninstallFor(isPrototype))
                    yield return sdk;
        }
    }
}
