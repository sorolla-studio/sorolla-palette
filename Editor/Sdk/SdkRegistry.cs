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
    ///     Metadata for a single SDK. Immutable to prevent accidental mutation.
    /// </summary>
    public readonly struct SdkInfo
    {
        public readonly SdkId Id;
        public readonly string Name;
        public readonly string PackageId;
        /// <summary>Version for OpenUPM packages (null = use InstallUrl instead)</summary>
        public readonly string Version;
        /// <summary>Git URL for packages not on OpenUPM (null = use PackageId@Version)</summary>
        public readonly string InstallUrl;
        /// <summary>OpenUPM scope required for this package (null = no scope needed)</summary>
        public readonly string Scope;
        public readonly string[] DetectionAssemblies;
        public readonly string[] DetectionTypes;
        public readonly SdkRequirement Requirement;

        public SdkInfo(
            SdkId id,
            string name,
            string packageId,
            SdkRequirement requirement,
            string[] detectionAssemblies,
            string[] detectionTypes,
            string version = null,
            string installUrl = null,
            string scope = null)
        {
            Id = id;
            Name = name;
            PackageId = packageId;
            Requirement = requirement;
            DetectionAssemblies = detectionAssemblies;
            DetectionTypes = detectionTypes;
            Version = version;
            InstallUrl = installUrl;
            Scope = scope;
        }

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
        public const string EDM_VERSION = "1.2.187";
        public const string GA_VERSION = "7.10.6";
        public const string MAX_VERSION = "8.5.0";
        public const string FB_VERSION = "18.0.1";
        public const string FIREBASE_VERSION = "13.7.0";

        public static readonly IReadOnlyDictionary<SdkId, SdkInfo> All = new Dictionary<SdkId, SdkInfo>
        {
            [SdkId.ExternalDependencyManager] = new SdkInfo(
                id: SdkId.ExternalDependencyManager,
                name: "External Dependency Manager",
                packageId: "com.google.external-dependency-manager",
                requirement: SdkRequirement.Core,
                detectionAssemblies: new[] { "Google.JarResolver" },
                detectionTypes: System.Array.Empty<string>(),
                version: EDM_VERSION,
                scope: "com.google.external-dependency-manager"
            ),
            [SdkId.GameAnalytics] = new SdkInfo(
                id: SdkId.GameAnalytics,
                name: "GameAnalytics",
                packageId: "com.gameanalytics.sdk",
                requirement: SdkRequirement.Core,
                detectionAssemblies: new[] { "GameAnalyticsSDK" },
                detectionTypes: new[] { "GameAnalyticsSDK.GameAnalytics, GameAnalyticsSDK" },
                version: GA_VERSION,
                scope: "com.gameanalytics"
            ),
            [SdkId.IosSupport] = new SdkInfo(
                id: SdkId.IosSupport,
                name: "iOS Support (ATT)",
                packageId: "com.unity.ads.ios-support",
                requirement: SdkRequirement.Core,
                detectionAssemblies: new[] { "Unity.Advertisement.IosSupport" },
                detectionTypes: new[] { "Unity.Advertisement.IosSupport.ATTrackingStatusBinding, Unity.Advertisement.IosSupport" },
                version: "1.2.0"
                // No scope needed - Unity registry
            ),
            [SdkId.Facebook] = new SdkInfo(
                id: SdkId.Facebook,
                name: "Facebook SDK",
                packageId: "com.lacrearthur.facebook-sdk-for-unity",
                requirement: SdkRequirement.Core,
                detectionAssemblies: new[] { "Facebook.Unity" },
                detectionTypes: new[] { "Facebook.Unity.FB, Facebook.Unity" },
                installUrl: "https://github.com/LaCreArthur/facebook-unity-sdk-upm.git"
            ),
            [SdkId.AppLovinMAX] = new SdkInfo(
                id: SdkId.AppLovinMAX,
                name: "AppLovin MAX",
                packageId: "com.applovin.mediation.ads",
                requirement: SdkRequirement.FullRequired,
                detectionAssemblies: new[] { "MaxSdk.Scripts", "applovin" },
                detectionTypes: new[] { "MaxSdk, MaxSdk.Scripts", "MaxSdkBase, MaxSdk.Scripts" },
                version: MAX_VERSION,
                scope: "com.applovin"
            ),
            [SdkId.Adjust] = new SdkInfo(
                id: SdkId.Adjust,
                name: "Adjust SDK",
                packageId: "com.adjust.sdk",
                requirement: SdkRequirement.FullOnly,
                detectionAssemblies: new[] { "com.adjust.sdk", "adjustsdk.scripts", "adjust" },
                detectionTypes: new[] { "AdjustSdk.Adjust, AdjustSdk.Scripts" },
                installUrl: "https://github.com/adjust/unity_sdk.git?path=Assets/Adjust"
            ),
            [SdkId.FirebaseApp] = new SdkInfo(
                id: SdkId.FirebaseApp,
                name: "Firebase App",
                packageId: "com.google.firebase.app",
                requirement: SdkRequirement.FullRequired,
                detectionAssemblies: new[] { "Firebase.App" },
                detectionTypes: new[] { "Firebase.FirebaseApp, Firebase.App" },
                installUrl: "https://github.com/LaCreArthur/unity-firebase-app.git?path=FirebaseApp#" + FIREBASE_VERSION
            ),
            [SdkId.FirebaseAnalytics] = new SdkInfo(
                id: SdkId.FirebaseAnalytics,
                name: "Firebase Analytics",
                packageId: "com.google.firebase.analytics",
                requirement: SdkRequirement.FullRequired,
                detectionAssemblies: new[] { "Firebase.Analytics" },
                detectionTypes: new[] { "Firebase.Analytics.FirebaseAnalytics, Firebase.Analytics" },
                installUrl: "https://github.com/LaCreArthur/unity-firebase-app.git?path=FirebaseAnalytics#" + FIREBASE_VERSION
            ),
            [SdkId.FirebaseCrashlytics] = new SdkInfo(
                id: SdkId.FirebaseCrashlytics,
                name: "Firebase Crashlytics",
                packageId: "com.google.firebase.crashlytics",
                requirement: SdkRequirement.FullRequired,
                detectionAssemblies: new[] { "Firebase.Crashlytics" },
                detectionTypes: new[] { "Firebase.Crashlytics.Crashlytics, Firebase.Crashlytics" },
                installUrl: "https://github.com/LaCreArthur/unity-firebase-app.git?path=FirebaseCrashlytics#" + FIREBASE_VERSION
            ),
            [SdkId.FirebaseRemoteConfig] = new SdkInfo(
                id: SdkId.FirebaseRemoteConfig,
                name: "Firebase Remote Config",
                packageId: "com.google.firebase.remote-config",
                requirement: SdkRequirement.FullRequired,
                detectionAssemblies: new[] { "Firebase.RemoteConfig" },
                detectionTypes: new[] { "Firebase.RemoteConfig.FirebaseRemoteConfig, Firebase.RemoteConfig" },
                installUrl: "https://github.com/LaCreArthur/unity-firebase-app.git?path=FirebaseRemoteConfig#" + FIREBASE_VERSION
            )
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
