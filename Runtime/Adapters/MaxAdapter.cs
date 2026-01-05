using System;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     Ad type for loading state events.
    /// </summary>
    public enum AdType
    {
        Rewarded,
        Interstitial,
        Banner
    }

    /// <summary>
    ///     Consent status for GDPR/privacy compliance.
    ///     Reflects the user's consent state as determined by MAX's UMP integration.
    /// </summary>
    public enum ConsentStatus
    {
        /// <summary>Consent status not yet determined</summary>
        Unknown,
        /// <summary>User is not in a GDPR region, consent not required</summary>
        NotApplicable,
        /// <summary>Consent required but not yet obtained</summary>
        Required,
        /// <summary>User has provided consent</summary>
        Obtained,
        /// <summary>User has denied consent</summary>
        Denied,
    }

    /// <summary>
    ///     Interface for MAX adapter implementation.
    /// </summary>
    internal interface IMaxAdapter
    {
        bool IsRewardedAdReady { get; }
        bool IsInterstitialAdReady { get; }
        ConsentStatus ConsentStatus { get; }
        bool CanRequestAds { get; }
        bool IsPrivacyOptionsRequired { get; }

        event Action<AdType, bool> OnAdLoadingStateChanged;
        event Action OnSdkInitialized;
        event Action<ConsentStatus> OnConsentStatusChanged;

        void Initialize(string sdkKey, string rewardedId, string interstitialId, string bannerId, bool consent);
        void ShowRewardedAd(Action onComplete, Action onFailed);
        void ShowInterstitialAd(Action onComplete);
        void ShowPrivacyOptions(Action onComplete);
        void RefreshConsentStatus();
        void UpdateConsent(bool consent);
    }

    /// <summary>
    ///     AppLovin MAX adapter. Delegates to implementation when available.
    /// </summary>
    public static class MaxAdapter
    {
        private static IMaxAdapter s_impl;

        internal static void RegisterImpl(IMaxAdapter impl)
        {
            s_impl = impl;
            UnityEngine.Debug.Log("[Sorolla:MAX] Implementation registered");

            // Forward events from implementation
            impl.OnAdLoadingStateChanged += (type, loading) => OnAdLoadingStateChanged?.Invoke(type, loading);
            impl.OnSdkInitialized += () => OnSdkInitialized?.Invoke();
            impl.OnConsentStatusChanged += (status) => OnConsentStatusChanged?.Invoke(status);
        }

        /// <summary>Whether a rewarded ad is ready to show</summary>
        public static bool IsRewardedAdReady => s_impl?.IsRewardedAdReady ?? false;

        /// <summary>Whether an interstitial ad is ready to show</summary>
        public static bool IsInterstitialAdReady => s_impl?.IsInterstitialAdReady ?? false;

        /// <summary>
        ///     Current consent status from MAX's UMP integration.
        ///     Check this after SDK initialization to determine if ads can be shown.
        /// </summary>
        public static ConsentStatus ConsentStatus => s_impl?.ConsentStatus ?? ConsentStatus.Unknown;

        /// <summary>
        ///     Whether ads can be requested (consent obtained or not required).
        ///     Use this to gate ad loading/showing in GDPR regions.
        /// </summary>
        public static bool CanRequestAds => s_impl?.CanRequestAds ?? false;

        /// <summary>
        ///     Whether privacy options should be shown in settings.
        ///     Only true if a CMP is available and user is in a consent region.
        /// </summary>
        public static bool IsPrivacyOptionsRequired => s_impl?.IsPrivacyOptionsRequired ?? false;

        /// <summary>Event fired when ad loading state changes. (adType, isLoading)</summary>
        public static event Action<AdType, bool> OnAdLoadingStateChanged;

        /// <summary>Event fired when MAX SDK is initialized. Use this to initialize other SDKs like Adjust.</summary>
        public static event Action OnSdkInitialized;

        /// <summary>Event fired when consent status changes.</summary>
        public static event Action<ConsentStatus> OnConsentStatusChanged;

        public static void Initialize(string sdkKey, string rewardedId, string interstitialId, string bannerId, bool consent)
        {
            if (s_impl != null)
                s_impl.Initialize(sdkKey, rewardedId, interstitialId, bannerId, consent);
            else
                UnityEngine.Debug.LogWarning("[Sorolla:MAX] Not installed");
        }

        public static void ShowRewardedAd(Action onComplete, Action onFailed)
        {
            if (s_impl != null)
                s_impl.ShowRewardedAd(onComplete, onFailed);
            else
                onFailed?.Invoke();
        }

        public static void ShowInterstitialAd(Action onComplete)
        {
            if (s_impl != null)
                s_impl.ShowInterstitialAd(onComplete);
            else
                onComplete?.Invoke();
        }

        /// <summary>
        ///     Show privacy options form (UMP consent form) for users to update their consent.
        ///     Call this from your settings screen when PrivacyOptionsRequired is true.
        /// </summary>
        public static void ShowPrivacyOptions(Action onComplete = null)
        {
            if (s_impl != null)
                s_impl.ShowPrivacyOptions(onComplete);
            else
                onComplete?.Invoke();
        }

        /// <summary>
        ///     Refresh consent status from MAX SDK.
        ///     Call this after showing privacy options or if consent may have changed.
        /// </summary>
        public static void RefreshConsentStatus()
        {
            s_impl?.RefreshConsentStatus();
        }

        /// <summary>
        ///     Update consent status manually.
        ///     Note: When UMP/CMP is enabled, use ShowPrivacyOptions() instead for GDPR compliance.
        ///     This method is primarily for iOS ATT consent when CMP is not configured.
        /// </summary>
        public static void UpdateConsent(bool consent)
        {
            s_impl?.UpdateConsent(consent);
        }
    }
}
