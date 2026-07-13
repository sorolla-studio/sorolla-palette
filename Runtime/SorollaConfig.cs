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
    ///     A set of store platforms - a curated flags value, not loose per-platform booleans. Used for two
    ///     distinct declarations on <see cref="SorollaConfig"/>: DISTRIBUTION (where the game ships, drives the
    ///     device/QA gates) and COMMERCE (where it sells in-app purchases, drives the store-config gate).
    ///     <c>None</c> means undeclared - the greenlight fails the dependent gates closed to INCOMPLETE until a
    ///     studio declares, rather than guessing from installed packages.
    /// </summary>
    [Flags]
    public enum SorollaPlatforms
    {
        None = 0,
        Android = 1 << 0,
        iOS = 1 << 1,
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

        /// <summary>
        ///     The store platforms this game is DISTRIBUTED on (where the app ships). Drives which platform's
        ///     device/QA gates apply: a game shipping on only one store is validated on only that platform. A
        ///     platform NOT listed here makes its device gates NotApplicable; a listed platform keeps them even
        ///     where no on-device collector exists yet (they resolve INCOMPLETE, a capability gap). <c>None</c>
        ///     is undeclared and fails the device gates closed to INCOMPLETE.
        /// </summary>
        [Header("Release Targets")]
        [Tooltip("Platforms this game's app SHIPS on. Drives device/QA gate applicability. Leave None only before you know - undeclared keeps device gates INCOMPLETE.")]
        public SorollaPlatforms distributionPlatforms;

        /// <summary>
        ///     The store platforms this game SELLS in-app purchases on (where products are configured in the
        ///     store console). Distinct from <see cref="distributionPlatforms"/>: a game can ship its app on
        ///     Android while selling IAP only on iOS, in which case the Android store-config gate is
        ///     NotApplicable even though the Android app QA gates apply. Drives the store-config gate only.
        ///     <c>None</c> is undeclared and fails the store gate closed to INCOMPLETE.
        /// </summary>
        [Tooltip("Platforms where this game SELLS in-app purchases (store-console products exist). Drives only the store-config gate; leave None if you don't sell IAP or haven't declared.")]
        public SorollaPlatforms commercePlatforms;

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
        ///     Parked vendor: TikTok is not part of the active supported vendor set. The adapter still
        ///     ships and these fields remain so existing integrations keep working; new integrations
        ///     should not configure TikTok. See the TikTok guide.
        ///     TikTok is also disabled if <see cref="tiktokAppId"/> is empty for the current platform.
        /// </summary>
        [Header("TikTok (Parked)")]
        [Tooltip("Enable TikTok Business SDK integration")]
        public bool enableTikTok;


        /// <summary>Parked vendor field. TikTok App ID from Events Manager (long numeric ID). Empty = disabled for that platform.</summary>
        [Tooltip("TikTok App ID from Events Manager (long numeric ID). Leave empty to disable.")]
        public PlatformAdUnitId tiktokAppId;

        /// <summary>Parked vendor field. TikTok Events Manager App ID (maps to the SDK's <c>appId</c> parameter).</summary>
        [Tooltip("App ID from TikTok Events Manager (maps to SDK appId parameter)")]
        public PlatformAdUnitId tiktokEmAppId;

        /// <summary>Parked vendor field. TikTok Events Manager Access Token used by the server-side event API.</summary>
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

        /// <summary>
        ///     Per-game override for the QA bridge password. Empty = the SDK's built-in default stays
        ///     active (zero migration for existing games). Set here so a leaked password only exposes
        ///     one game instead of the whole portfolio. Lives on this asset (not the QA-expectations
        ///     asset) because <see cref="Palette.Config"/> is guaranteed loaded by the time the QA
        ///     bridge can arm; nothing about bridge auth should depend on an optional asset.
        /// </summary>
        [Header("QA Bridge (optional)")]
        [Tooltip("Per-game QA bridge password override. Leave empty to keep the SDK's built-in default password.")]
        public string qaBridgePassword;

        // Note: Firebase modules (Analytics, Crashlytics, Remote Config) are always enabled
        // when Firebase is installed. No toggles needed as of v3.1.0.
    }
}
