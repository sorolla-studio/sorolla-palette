#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
using System;
using UnityEngine;

namespace Sorolla.Adapters
{
    /// <summary>
    ///     Ad type for loading state events.
    /// </summary>
    public enum AdType
    {
        Rewarded,
        Interstitial,
        Banner,
    }

    /// <summary>
    ///     AppLovin MAX adapter. Use Sorolla API instead.
    /// </summary>
    public static class MaxAdapter
    {

        static bool s_init;
        static string s_rewardedId;
        static string s_interstitialId;
        static string s_bannerId;

        static Action s_onRewardComplete;
        static Action s_onRewardFailed;
        static Action s_onInterstitialComplete;

        static bool s_rewardedReady;
        static bool s_interstitialReady;

        /// <summary>Whether a rewarded ad is ready to show</summary>
        public static bool IsRewardedAdReady => s_init && s_rewardedReady && MaxSdk.IsRewardedAdReady(s_rewardedId);

        /// <summary>Whether an interstitial ad is ready to show</summary>
        public static bool IsInterstitialAdReady => s_init && s_interstitialReady && MaxSdk.IsInterstitialReady(s_interstitialId);

        /// <summary>Event fired when ad loading state changes. (adType, isLoading)</summary>
        public static event Action<AdType, bool> OnAdLoadingStateChanged;

        /// <summary>Event fired when MAX SDK is initialized. Use this to initialize other SDKs like Adjust.</summary>
        public static event Action OnSdkInitialized;

        static bool s_consent;

        public static void Initialize(string sdkKey, string rewardedId, string interstitialId, string bannerId, bool consent)
        {
            if (s_init) return;

            s_rewardedId = rewardedId;
            s_interstitialId = interstitialId;
            s_bannerId = bannerId;
            s_consent = consent;

            Debug.Log("[Sorolla:MAX] Initializing...");
            MaxSdkCallbacks.OnSdkInitializedEvent += OnSdkInit;

            MaxSdk.InitializeSdk();
        }

        static void OnSdkInit(MaxSdkBase.SdkConfiguration config)
        {
            Debug.Log("[Sorolla:MAX] Initialized");

            s_init = true;

            // Set consent status (passed during Initialize)
            MaxSdk.SetHasUserConsent(s_consent);
            Debug.Log($"[Sorolla:MAX] SetHasUserConsent({s_consent})");

            InitRewarded();
            InitInterstitial();

            // Fire event so other SDKs (like Adjust) can initialize in this callback
            OnSdkInitialized?.Invoke();
        }



        #region Rewarded

        static void InitRewarded()
        {
            if (string.IsNullOrEmpty(s_rewardedId)) return;

            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += (_, __) =>
            {
                s_rewardedReady = true;
                OnAdLoadingStateChanged?.Invoke(AdType.Rewarded, false);
            };
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += (_, __) =>
            {
                s_rewardedReady = false;
                OnAdLoadingStateChanged?.Invoke(AdType.Rewarded, false);

                LoadRewarded();
            };
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += (_, __) =>
            {
                s_rewardedReady = false;
                LoadRewarded();
            };
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += (_, __, ___) =>
            {
                s_rewardedReady = false;
                s_onRewardFailed?.Invoke();
                s_onRewardFailed = null;
                s_onRewardComplete = null;
                LoadRewarded();
            };
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += (_, __, ___) =>
            {
                s_onRewardComplete?.Invoke();
                s_onRewardComplete = null;
                s_onRewardFailed = null;
            };
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += (_, info) =>
            {
#if SOROLLA_ADJUST_ENABLED
                AdjustAdapter.TrackAdRevenue(info);
#endif
            };

            LoadRewarded();
        }

        static void LoadRewarded()
        {
            OnAdLoadingStateChanged?.Invoke(AdType.Rewarded, true);
            MaxSdk.LoadRewardedAd(s_rewardedId);
        }



        public static void ShowRewardedAd(Action onComplete, Action onFailed)
        {
            if (!s_init)
            {
                onFailed?.Invoke();
                return;
            }

            if (!s_rewardedReady || !MaxSdk.IsRewardedAdReady(s_rewardedId))
            {
                // Ad not ready - load for next time and fail this request
                LoadRewarded();
                Debug.LogWarning("[Sorolla:MAX] Rewarded ad not ready");
                onFailed?.Invoke();
                return;
            }

            s_onRewardComplete = onComplete;
            s_onRewardFailed = onFailed;
            MaxSdk.ShowRewardedAd(s_rewardedId);
        }

        #endregion

        #region Interstitial

        static void InitInterstitial()
        {
            if (string.IsNullOrEmpty(s_interstitialId)) return;

            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += (_, __) =>
            {
                s_interstitialReady = true;
                OnAdLoadingStateChanged?.Invoke(AdType.Interstitial, false);
            };
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += (_, __) =>
            {
                s_interstitialReady = false;
                OnAdLoadingStateChanged?.Invoke(AdType.Interstitial, false);
                LoadInterstitial();

            };
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += (_, __) =>
            {
                s_interstitialReady = false;
                s_onInterstitialComplete?.Invoke();
                s_onInterstitialComplete = null;
                LoadInterstitial();
            };
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += (_, __, ___) =>
            {
                s_interstitialReady = false;
                s_onInterstitialComplete?.Invoke();
                s_onInterstitialComplete = null;
                LoadInterstitial();
            };
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += (_, info) =>
            {
#if SOROLLA_ADJUST_ENABLED
                AdjustAdapter.TrackAdRevenue(info);
#endif
            };

            LoadInterstitial();
        }

        static void LoadInterstitial()
        {
            OnAdLoadingStateChanged?.Invoke(AdType.Interstitial, true);
            MaxSdk.LoadInterstitial(s_interstitialId);
        }



        public static void ShowInterstitialAd(Action onComplete)
        {
            if (!s_init || !s_interstitialReady || !MaxSdk.IsInterstitialReady(s_interstitialId))
            {
                onComplete?.Invoke();
                LoadInterstitial();
                return;
            }

            s_onInterstitialComplete = onComplete;
            MaxSdk.ShowInterstitial(s_interstitialId);
        }

        #endregion
    }
}
#else
namespace Sorolla.Adapters
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

    public static class MaxAdapter
    {
        #pragma warning disable CS0067 // Event is never used (stub for API compatibility)
        public static event System.Action<AdType, bool> OnAdLoadingStateChanged;
        public static event System.Action OnSdkInitialized;
        #pragma warning restore CS0067
        public static bool IsRewardedAdReady => false;
        public static bool IsInterstitialAdReady => false;


        public static void Initialize(string k, string r, string i, string b, bool c) => UnityEngine.Debug.LogWarning("[Sorolla:MAX] Not installed");
        public static void ShowRewardedAd(System.Action c, System.Action f) => f?.Invoke();
        public static void ShowInterstitialAd(System.Action c) => c?.Invoke();
    }
}
#endif
