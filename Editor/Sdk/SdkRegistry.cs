using System.Collections.Generic;

namespace Sorolla.Editor
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
        ExternalDependencyManager
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
        /// <summary>Required only in Full mode</summary>
        FullOnly,
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
        public string InstallUrl;
        public string[] DetectionAssemblies;
        public string[] DetectionTypes;
        public SdkRequirement Requirement;

        public bool IsRequiredFor(bool isPrototype) => Requirement switch
        {
            SdkRequirement.Core => true,
            SdkRequirement.PrototypeOnly => isPrototype,
            SdkRequirement.FullOnly => !isPrototype,
            _ => false
        };

        public bool ShouldUninstallFor(bool isPrototype) => Requirement switch
        {
            SdkRequirement.PrototypeOnly => !isPrototype,
            SdkRequirement.FullOnly => isPrototype,
            _ => false
        };
    }

    /// <summary>
    ///     Single source of truth for all SDK metadata.
    /// </summary>
    public static class SdkRegistry
    {
        public static readonly IReadOnlyDictionary<SdkId, SdkInfo> All = new Dictionary<SdkId, SdkInfo>
        {
            [SdkId.GameAnalytics] = new()
            {
                Id = SdkId.GameAnalytics,
                Name = "GameAnalytics",
                PackageId = "com.gameanalytics.sdk",
                InstallUrl = "https://github.com/GameAnalytics/GA-SDK-UNITY.git",
                DetectionAssemblies = new[] { "GameAnalyticsSDK" },
                DetectionTypes = new[] { "GameAnalyticsSDK.GameAnalytics, GameAnalyticsSDK" },
                Requirement = SdkRequirement.Core
            },
            [SdkId.IosSupport] = new()
            {
                Id = SdkId.IosSupport,
                Name = "iOS Support (ATT)",
                PackageId = "com.unity.ads.ios-support",
                InstallUrl = "com.unity.ads.ios-support",
                DetectionAssemblies = new[] { "Unity.Advertisement.IosSupport" },
                DetectionTypes = new[] { "Unity.Advertisement.IosSupport.ATTrackingStatusBinding, Unity.Advertisement.IosSupport" },
                Requirement = SdkRequirement.Core
            },
            [SdkId.ExternalDependencyManager] = new()
            {
                Id = SdkId.ExternalDependencyManager,
                Name = "External Dependency Manager",
                PackageId = "com.google.external-dependency-manager",
                InstallUrl = "com.google.external-dependency-manager",
                DetectionAssemblies = new[] { "Google.JarResolver" },
                DetectionTypes = System.Array.Empty<string>(),
                Requirement = SdkRequirement.Core
            },
            [SdkId.Facebook] = new()
            {
                Id = SdkId.Facebook,
                Name = "Facebook SDK",
                PackageId = "com.lacrearthur.facebook-sdk-for-unity",
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
                InstallUrl = null, // Special handling (registry + version)
                DetectionAssemblies = new[] { "MaxSdk.Scripts", "applovin" },
                DetectionTypes = new[] { "MaxSdk, MaxSdk.Scripts", "MaxSdkBase, MaxSdk.Scripts" },
                Requirement = SdkRequirement.FullOnly
            },
            [SdkId.Adjust] = new()
            {
                Id = SdkId.Adjust,
                Name = "Adjust SDK",
                PackageId = "com.adjust.sdk",
                InstallUrl = "https://github.com/adjust/unity_sdk.git?path=Assets/Adjust",
                DetectionAssemblies = new[] { "com.adjust.sdk", "adjustsdk.scripts", "adjust" },
                DetectionTypes = new[] { "AdjustSdk.Adjust, AdjustSdk.Scripts" },
                Requirement = SdkRequirement.FullOnly
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
