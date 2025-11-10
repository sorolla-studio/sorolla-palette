#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
using UnityEngine;
using System;

namespace SorollaPalette.MAX
{
    /// <summary>
    /// AppLovin MAX adapter - only compiles when SOROLLA_MAX_ENABLED is defined
    /// </summary>
    public static class MaxAdapter
    {
        private static bool _isInitialized;
        private static string _rewardedAdUnitId;
        private static string _interstitialAdUnitId;
        private static string _bannerAdUnitId;
        
        private static Action _onRewardedAdComplete;
        private static Action _onRewardedAdFailed;
        private static Action _onInterstitialComplete;
        
        private static bool _rewardedAdReady;
        private static bool _interstitialAdReady;
        
        public static void Initialize(string sdkKey, string rewardedAdUnitId, string interstitialAdUnitId, string bannerAdUnitId)
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[MAX Adapter] Already initialized");
                return;
            }
            
            _rewardedAdUnitId = rewardedAdUnitId;
            _interstitialAdUnitId = interstitialAdUnitId;
            _bannerAdUnitId = bannerAdUnitId;
            
            Debug.Log("[MAX Adapter] Initializing AppLovin MAX...");
            
            MaxSdkCallbacks.OnSdkInitializedEvent += OnSdkInitialized;
            
            MaxSdk.SetSdkKey(sdkKey);
            MaxSdk.InitializeSdk();
        }
        
        private static void OnSdkInitialized(MaxSdkBase.SdkConfiguration sdkConfiguration)
        {
            Debug.Log("[MAX Adapter] MAX SDK Initialized");
            _isInitialized = true;
            
            // Initialize ad units
            InitializeRewardedAds();
            InitializeInterstitialAds();
        }
        
        #region Rewarded Ads
        
        private static void InitializeRewardedAds()
        {
            if (string.IsNullOrEmpty(_rewardedAdUnitId))
            {
                Debug.LogWarning("[MAX Adapter] Rewarded ad unit ID not set");
                return;
            }
            
            // Attach callbacks
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnRewardedAdLoaded;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnRewardedAdLoadFailed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += OnRewardedAdDisplayed;
            MaxSdkCallbacks.Rewarded.OnAdClickedEvent += OnRewardedAdClicked;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRewardedAdHidden;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnRewardedAdFailedToDisplay;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRewardedAdReceivedReward;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += OnRewardedAdRevenuePaid;
            
            // Load first ad
            LoadRewardedAd();
        }
        
        private static void LoadRewardedAd()
        {
            Debug.Log("[MAX Adapter] Loading rewarded ad...");
            MaxSdk.LoadRewardedAd(_rewardedAdUnitId);
        }
        
        public static void ShowRewardedAd(Action onComplete, Action onFailed)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[MAX Adapter] Not initialized");
                onFailed?.Invoke();
                return;
            }
            
            if (!_rewardedAdReady || !MaxSdk.IsRewardedAdReady(_rewardedAdUnitId))
            {
                Debug.LogWarning("[MAX Adapter] Rewarded ad not ready");
                onFailed?.Invoke();
                LoadRewardedAd(); // Try to load for next time
                return;
            }
            
            _onRewardedAdComplete = onComplete;
            _onRewardedAdFailed = onFailed;
            
            Debug.Log("[MAX Adapter] Showing rewarded ad");
            MaxSdk.ShowRewardedAd(_rewardedAdUnitId);
        }
        
        private static void OnRewardedAdLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[MAX Adapter] Rewarded ad loaded");
            _rewardedAdReady = true;
        }
        
        private static void OnRewardedAdLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            Debug.LogWarning($"[MAX Adapter] Rewarded ad failed to load: {errorInfo.Message}");
            _rewardedAdReady = false;
            
            // Retry after 3 seconds
            MaxSdk.LoadRewardedAd(_rewardedAdUnitId);
        }
        
        private static void OnRewardedAdDisplayed(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[MAX Adapter] Rewarded ad displayed");
        }
        
        private static void OnRewardedAdClicked(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[MAX Adapter] Rewarded ad clicked");
        }
        
        private static void OnRewardedAdHidden(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[MAX Adapter] Rewarded ad hidden");
            _rewardedAdReady = false;
            
            // Load next ad
            LoadRewardedAd();
        }
        
        private static void OnRewardedAdFailedToDisplay(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            Debug.LogWarning($"[MAX Adapter] Rewarded ad failed to display: {errorInfo.Message}");
            _rewardedAdReady = false;
            
            _onRewardedAdFailed?.Invoke();
            _onRewardedAdFailed = null;
            _onRewardedAdComplete = null;
            
            // Load next ad
            LoadRewardedAd();
        }
        
        private static void OnRewardedAdReceivedReward(string adUnitId, MaxSdkBase.Reward reward, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log($"[MAX Adapter] Rewarded ad received reward: {reward.Amount} {reward.Label}");
            
            _onRewardedAdComplete?.Invoke();
            _onRewardedAdComplete = null;
            _onRewardedAdFailed = null;
        }
        
        private static void OnRewardedAdRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log($"[MAX Adapter] Rewarded ad revenue: {adInfo.Revenue}");
            
            // Forward to Adjust if enabled
#if SOROLLA_ADJUST_ENABLED
            SorollaPalette.Adjust.AdjustAdapter.TrackAdRevenue(adInfo);
#endif
        }
        
        #endregion
        
        #region Interstitial Ads
        
        private static void InitializeInterstitialAds()
        {
            if (string.IsNullOrEmpty(_interstitialAdUnitId))
            {
                Debug.LogWarning("[MAX Adapter] Interstitial ad unit ID not set");
                return;
            }
            
            // Attach callbacks
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnInterstitialAdLoaded;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnInterstitialAdLoadFailed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += OnInterstitialAdDisplayed;
            MaxSdkCallbacks.Interstitial.OnAdClickedEvent += OnInterstitialAdClicked;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnInterstitialAdHidden;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += OnInterstitialAdFailedToDisplay;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += OnInterstitialAdRevenuePaid;
            
            // Load first ad
            LoadInterstitialAd();
        }
        
        private static void LoadInterstitialAd()
        {
            Debug.Log("[MAX Adapter] Loading interstitial ad...");
            MaxSdk.LoadInterstitial(_interstitialAdUnitId);
        }
        
        public static void ShowInterstitialAd(Action onComplete)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[MAX Adapter] Not initialized");
                onComplete?.Invoke();
                return;
            }
            
            if (!_interstitialAdReady || !MaxSdk.IsInterstitialReady(_interstitialAdUnitId))
            {
                Debug.LogWarning("[MAX Adapter] Interstitial ad not ready");
                onComplete?.Invoke();
                LoadInterstitialAd(); // Try to load for next time
                return;
            }
            
            _onInterstitialComplete = onComplete;
            
            Debug.Log("[MAX Adapter] Showing interstitial ad");
            MaxSdk.ShowInterstitial(_interstitialAdUnitId);
        }
        
        private static void OnInterstitialAdLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[MAX Adapter] Interstitial ad loaded");
            _interstitialAdReady = true;
        }
        
        private static void OnInterstitialAdLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            Debug.LogWarning($"[MAX Adapter] Interstitial ad failed to load: {errorInfo.Message}");
            _interstitialAdReady = false;
            
            // Retry after 3 seconds
            MaxSdk.LoadInterstitial(_interstitialAdUnitId);
        }
        
        private static void OnInterstitialAdDisplayed(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[MAX Adapter] Interstitial ad displayed");
        }
        
        private static void OnInterstitialAdClicked(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[MAX Adapter] Interstitial ad clicked");
        }
        
        private static void OnInterstitialAdHidden(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[MAX Adapter] Interstitial ad hidden");
            _interstitialAdReady = false;
            
            _onInterstitialComplete?.Invoke();
            _onInterstitialComplete = null;
            
            // Load next ad
            LoadInterstitialAd();
        }
        
        private static void OnInterstitialAdFailedToDisplay(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            Debug.LogWarning($"[MAX Adapter] Interstitial ad failed to display: {errorInfo.Message}");
            _interstitialAdReady = false;
            
            _onInterstitialComplete?.Invoke();
            _onInterstitialComplete = null;
            
            // Load next ad
            LoadInterstitialAd();
        }
        
        private static void OnInterstitialAdRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log($"[MAX Adapter] Interstitial ad revenue: {adInfo.Revenue}");
            
            // Forward to Adjust if enabled
#if SOROLLA_ADJUST_ENABLED
            SorollaPalette.Adjust.AdjustAdapter.TrackAdRevenue(adInfo);
#endif
        }
        
        #endregion
    }
}
#else
namespace SorollaPalette.MAX
{
    public static class MaxAdapter
    {
        public static void Initialize(string sdkKey, string rewardedAdUnitId, string interstitialAdUnitId, string bannerAdUnitId)
        {
            UnityEngine.Debug.LogWarning("[MAX Adapter] AppLovin MAX package not installed. Install MAX before enabling the module.");
        }
        public static void ShowRewardedAd(System.Action onComplete, System.Action onFailed)
        {
            UnityEngine.Debug.LogWarning("[MAX Adapter] AppLovin MAX package not installed.");
            onFailed?.Invoke();
        }
        public static void ShowInterstitialAd(System.Action onComplete)
        {
            UnityEngine.Debug.LogWarning("[MAX Adapter] AppLovin MAX package not installed.");
            onComplete?.Invoke();
        }
    }
}
#endif
