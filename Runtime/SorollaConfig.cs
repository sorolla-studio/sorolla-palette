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
        /// <summary>Android platform value (ad unit ID, app ID, access token, etc).</summary>
        public string android;
        /// <summary>iOS platform value (ad unit ID, app ID, access token, etc).</summary>
        public string ios;

        /// <summary>
        ///     Returns the value for the current build target: <c>ios</c> on iOS, <c>android</c> everywhere else.
        /// </summary>
        public string Current =>
#if UNITY_IOS
            ios;
#else
            android;
#endif

        /// <summary>True when at least one platform value is populated.</summary>
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
        /// <summary>
        ///     Prototype = Core SDKs only (GameAnalytics + Facebook + Firebase).
        ///     Full = Core + MAX + Adjust + Firebase. Set via the Configuration window.
        /// </summary>
        [Header("Mode")]
        [Tooltip("Prototype = Core SDKs only | Full = Core SDKs + MAX + Adjust")]
        public bool isPrototypeMode = true;

        /// <summary>Rewarded ad unit IDs from AppLovin MAX (one per platform).</summary>
        [Header("MAX Ad Units")]
        [Tooltip("Rewarded ad unit IDs per platform")]
        public PlatformAdUnitId rewardedAdUnit;

        /// <summary>Interstitial ad unit IDs from AppLovin MAX (one per platform).</summary>
        [Tooltip("Interstitial ad unit IDs per platform")]
        public PlatformAdUnitId interstitialAdUnit;

        /// <summary>Banner ad unit IDs from AppLovin MAX (optional, one per platform).</summary>
        [Tooltip("Banner ad unit IDs per platform (optional)")]
        public PlatformAdUnitId bannerAdUnit;

        /// <summary>Adjust app token from the Adjust Dashboard. Required in Full mode.</summary>
        [Header("Adjust (Full Mode Only)")]
        [Tooltip("Adjust App Token")]
        public string adjustAppToken;

        /// <summary>
        ///     When true, Adjust runs in sandbox environment for testing.
        ///     Must be false for production store builds.
        /// </summary>
        [Tooltip("Use Sandbox environment for testing")]
        public bool adjustSandboxMode;

        /// <summary>
        ///     Adjust event token used by <see cref="Palette.TrackPurchase"/> for revenue tracking.
        ///     Create in Adjust Dashboard -> Events.
        /// </summary>
        [Tooltip("Adjust event token for purchase/revenue tracking (from Adjust Dashboard)")]
        public string adjustPurchaseEventToken;

        /// <summary>
        ///     Master switch for TikTok Business SDK integration.
        ///     TikTok is also disabled if <see cref="tiktokAppId"/> is empty for the current platform.
        /// </summary>
        [Header("TikTok (Optional)")]
        [Tooltip("Enable TikTok Business SDK integration")]
        public bool enableTikTok;


        /// <summary>TikTok App ID from Events Manager (long numeric ID). Empty = disabled for that platform.</summary>
        [Tooltip("TikTok App ID from Events Manager (long numeric ID). Leave empty to disable.")]
        public PlatformAdUnitId tiktokAppId;

        /// <summary>TikTok Events Manager App ID (maps to the SDK's <c>appId</c> parameter).</summary>
        [Tooltip("App ID from TikTok Events Manager (maps to SDK appId parameter)")]
        public PlatformAdUnitId tiktokEmAppId;

        /// <summary>TikTok Events Manager Access Token used by the server-side event API.</summary>
        [Tooltip("App Secret (Access Token) from Events Manager.")]
        public PlatformAdUnitId tiktokAccessToken;

        /// <summary>
        ///     Enables detailed SDK diagnostics and vendor debug logging for QA investigation.
        ///     Automatically forced OFF in non-development builds as a safety net.
        ///     Production-safe SDK health markers, warnings, and errors are always logged even when this is OFF.
        /// </summary>
        [Header("Logging")]
        [Tooltip("Enable detailed SDK diagnostics and vendor debug logs. Forced OFF in release builds; production-safe health logs remain on.")]
        public bool verboseLogging;

        // Note: Firebase modules (Analytics, Crashlytics, Remote Config) are always enabled
        // when Firebase is installed. No toggles needed as of v3.1.0.
    }
}
