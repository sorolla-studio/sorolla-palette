using System;
using UnityEngine;

namespace Sorolla.Palette
{
    /// <summary>
    ///     Platform-specific ad unit ID container.
    ///     Use .Current to get the correct ID for the active build target.
    /// </summary>
    [Serializable]
    public class PlatformAdUnitId
    {
        public string android;
        public string ios;

        public string Current =>
#if UNITY_IOS
            ios;
#else
            android;
#endif

        public bool IsConfigured =>
            !string.IsNullOrEmpty(android) || !string.IsNullOrEmpty(ios);
    }

    /// <summary>
    ///     Configuration asset for Palette SDK.
    ///     Create via: Assets > Create > Palette > Config
    ///     Save to: Assets/Resources/SorollaConfig.asset
    /// </summary>
    [CreateAssetMenu(fileName = "SorollaConfig", menuName = "Palette/Config", order = 1)]
    public class SorollaConfig : ScriptableObject
    {
        [Header("Mode")]
        [Tooltip("Prototype = Core SDKs only | Full = Core SDKs + MAX + Adjust")]
        public bool isPrototypeMode = true;

        [Header("MAX Ad Units")]
        [Tooltip("AppLovin MAX SDK Key (synced to AppLovinSettings)")]
        public string maxSdkKey;

        [Tooltip("Rewarded ad unit IDs per platform")]
        public PlatformAdUnitId rewardedAdUnit;

        [Tooltip("Interstitial ad unit IDs per platform")]
        public PlatformAdUnitId interstitialAdUnit;

        [Tooltip("Banner ad unit IDs per platform (optional)")]
        public PlatformAdUnitId bannerAdUnit;

        [Header("Adjust (Full Mode Only)")]
        [Tooltip("Adjust App Token")]
        public string adjustAppToken;

        [Tooltip("Use Sandbox environment for testing")]
        public bool adjustSandboxMode;

        [Header("TikTok")]
        [Tooltip("TikTok App ID from Events Manager (long numeric ID). Leave empty to disable.")]
        public PlatformAdUnitId tiktokAppId;

        [Tooltip("App ID from TikTok Events Manager (maps to SDK appId parameter)")]
        public PlatformAdUnitId tiktokEmAppId;

        [Tooltip("App Secret (Access Token) from Events Manager.")]
        public PlatformAdUnitId tiktokAccessToken;

        // Note: Firebase modules (Analytics, Crashlytics, Remote Config) are always enabled
        // when Firebase is installed. No toggles needed as of v3.1.0.
    }
}
