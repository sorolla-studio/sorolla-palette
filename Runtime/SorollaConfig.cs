using UnityEngine;

namespace Sorolla.Palette
{
    /// <summary>
    ///     Configuration asset for Palette SDK.
    ///     Create via: Assets > Create > Palette > Config
    ///     Save to: Assets/Resources/SorollaConfig.asset
    /// </summary>
    [CreateAssetMenu(fileName = "SorollaConfig", menuName = "Palette/Config", order = 1)]
    public class SorollaConfig : ScriptableObject
    {
        [Header("Mode")]
        [Tooltip("Prototype = GA + Facebook | Full = GA + MAX + Adjust")]
        public bool isPrototypeMode = true;

        [Tooltip("Rewarded ad unit ID")]
        public string maxRewardedAdUnitId;

        [Tooltip("Interstitial ad unit ID")]
        public string maxInterstitialAdUnitId;

        [Tooltip("Banner ad unit ID (optional)")]
        public string maxBannerAdUnitId;

        [Header("Adjust (Full Mode Only)")]
        [Tooltip("Adjust App Token")]
        public string adjustAppToken;

        [Tooltip("Use Sandbox environment for testing (disable for production builds)")]
        public bool adjustSandboxMode = true;


        [Header("Firebase Analytics (Optional)")]
        [Tooltip("Enable Firebase Analytics (requires google-services.json / GoogleService-Info.plist)")]
        public bool enableFirebaseAnalytics;

        [Header("Firebase Crashlytics (Optional)")]
        [Tooltip("Enable Firebase Crashlytics for crash reporting")]
        public bool enableCrashlytics;

        [Header("Firebase Remote Config (Optional)")]
        [Tooltip("Enable Firebase Remote Config for A/B testing and feature flags")]
        public bool enableRemoteConfig;
    }
}
