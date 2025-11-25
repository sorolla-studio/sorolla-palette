#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
using System;
using UnityEngine;

namespace Sorolla.Adapters
{
    /// <summary>
    ///     AppLovin MAX adapter. Use Sorolla API instead.
    /// </summary>
    public static class MaxAdapter
    {
        private static bool s_init;
        private static string s_rewardedId;
        private static string s_interstitialId;
        private static string s_bannerId;

        private static Action s_onRewardComplete;
        private static Action s_onRewardFailed;
        private static Action s_onInterstitialComplete;

        private static bool s_rewardedReady;
        private static bool s_interstitialReady;

        public static void Initialize(string sdkKey, string rewardedId, string interstitialId, string bannerId)
        {
            if (s_init) return;

            s_rewardedId = rewardedId;
            s_interstitialId = interstitialId;
            s_bannerId = bannerId;

            Debug.Log("[Sorolla:MAX] Initializing...");
            MaxSdkCallbacks.OnSdkInitializedEvent += OnSdkInit;
            MaxSdk.SetSdkKey(sdkKey);
            MaxSdk.InitializeSdk();
        }

        private static void OnSdkInit(MaxSdkBase.SdkConfiguration config)
        {
            Debug.Log($"[Sorolla:MAX] Initialized (ATT: {config.AppTrackingStatus})");
            s_init = true;
            InitRewarded();
            InitInterstitial();
        }

        #region Rewarded

        private static void InitRewarded()
        {
            if (string.IsNullOrEmpty(s_rewardedId)) return;

            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += (_, __) => { s_rewardedReady = true; };
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += (_, __) => { s_rewardedReady = false; LoadRewarded(); };
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += (_, __) => { s_rewardedReady = false; LoadRewarded(); };
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

        private static void LoadRewarded() => MaxSdk.LoadRewardedAd(s_rewardedId);

        public static void ShowRewardedAd(Action onComplete, Action onFailed)
        {
            if (!s_init) { onFailed?.Invoke(); return; }

            if (!s_rewardedReady || !MaxSdk.IsRewardedAdReady(s_rewardedId))
            {
                LoadRewarded();
                SorollaLoadingOverlay.WaitForAd(
                    () => MaxSdk.IsRewardedAdReady(s_rewardedId),
                    () => { s_onRewardComplete = onComplete; s_onRewardFailed = onFailed; MaxSdk.ShowRewardedAd(s_rewardedId); },
                    () => onFailed?.Invoke()
                );
                return;
            }

            s_onRewardComplete = onComplete;
            s_onRewardFailed = onFailed;
            MaxSdk.ShowRewardedAd(s_rewardedId);
        }

        #endregion

        #region Interstitial

        private static void InitInterstitial()
        {
            if (string.IsNullOrEmpty(s_interstitialId)) return;

            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += (_, __) => { s_interstitialReady = true; };
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += (_, __) => { s_interstitialReady = false; LoadInterstitial(); };
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

        private static void LoadInterstitial() => MaxSdk.LoadInterstitial(s_interstitialId);

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
    public static class MaxAdapter
    {
        public static void Initialize(string k, string r, string i, string b) => UnityEngine.Debug.LogWarning("[Sorolla:MAX] Not installed");
        public static void ShowRewardedAd(System.Action c, System.Action f) => f?.Invoke();
        public static void ShowInterstitialAd(System.Action c) => c?.Invoke();
    }
}
#endif
