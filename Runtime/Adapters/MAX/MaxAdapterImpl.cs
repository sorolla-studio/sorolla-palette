using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     AppLovin MAX adapter implementation. Registered at runtime.
    /// </summary>
    [Preserve]
    internal class MaxAdapterImpl : IMaxAdapter
    {
        string _bannerId;
        bool _consent;
        bool _init;
        string _interstitialId;
        bool _interstitialReady;
        Action _onInterstitialComplete;

        Action _onRewardComplete;
        Action _onRewardFailed;
        string _rewardedId;

        bool _rewardedReady;
        bool _userWaitingForRewarded;
        bool _userWaitingForInterstitial;

        MaxSdkBase.SdkConfiguration _sdkConfig;

        public bool IsRewardedAdReady => _init && _rewardedReady && MaxSdk.IsRewardedAdReady(_rewardedId);
        public bool IsInterstitialAdReady => _init && _interstitialReady && MaxSdk.IsInterstitialReady(_interstitialId);
        public ConsentStatus ConsentStatus { get; private set; } = ConsentStatus.Unknown;

        public bool CanRequestAds => ConsentStatus == ConsentStatus.Obtained ||
                                     ConsentStatus == ConsentStatus.NotApplicable;

        public bool IsPrivacyOptionsRequired
        {
            get {
                if (!_init) return false;
                try
                {
                    return MaxSdk.CmpService.HasSupportedCmp;
                }
                catch
                {
                    return false;
                }
            }
        }

        public event Action<AdType, bool> OnAdLoadingStateChanged;
        public event Action OnSdkInitialized;
        public event Action<ConsentStatus> OnConsentStatusChanged;

        public void Initialize(string rewardedId, string interstitialId, string bannerId, bool consent)
        {
            if (_init) return;

            _rewardedId = rewardedId;
            _interstitialId = interstitialId;
            _bannerId = bannerId;
            _consent = consent;

            Debug.Log("[Palette:MAX] Initializing...");
            MaxSdkCallbacks.OnSdkInitializedEvent += OnSdkInit;

            // SDK key is read from AppLovinSettings (configured in Integration Manager)
            MaxSdk.InitializeSdk();
        }

        public void ShowPrivacyOptions(Action onComplete)
        {
            if (!_init)
            {
                Debug.LogWarning("[Palette:MAX] Cannot show privacy options - SDK not initialized");
                onComplete?.Invoke();
                return;
            }

            try
            {
                if (!MaxSdk.CmpService.HasSupportedCmp)
                {
                    Debug.LogWarning("[Palette:MAX] No CMP configured - privacy options not available");
                    onComplete?.Invoke();
                    return;
                }

                Debug.Log("[Palette:MAX] Showing privacy options...");
                MaxSdk.CmpService.ShowCmpForExistingUser(error =>
                {
                    if (error != null)
                    {
                        Debug.LogWarning($"[Palette:MAX] Privacy options error: {error.Message}");
                    }
                    else
                    {
                        Debug.Log("[Palette:MAX] Privacy options dismissed");
                        RefreshConsentStatus();
                    }
                    onComplete?.Invoke();
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Palette:MAX] CmpService not available: {e.Message}");
                onComplete?.Invoke();
            }
        }

        public void RefreshConsentStatus()
        {
            if (!_init || _sdkConfig == null) return;

            try
            {
                _consent = MaxSdk.HasUserConsent();
            }
            catch
            {
                // HasUserConsent not available - keep existing value
            }

            UpdateConsentStatusFromConfig(_sdkConfig);
        }

        public void UpdateConsent(bool consent)
        {
            _consent = consent;

            bool cmpHandlesConsent = false;
            try
            {
                cmpHandlesConsent = _init && MaxSdk.CmpService.HasSupportedCmp;
            }
            catch
            {
                // CmpService not available
            }

            if (!cmpHandlesConsent)
            {
                MaxSdk.SetHasUserConsent(consent);
                Debug.Log($"[Palette:MAX] UpdateConsent({consent})");
            }
            else
            {
                Debug.LogWarning("[Palette:MAX] UpdateConsent called but CMP is enabled - use ShowPrivacyOptions() instead");
            }

            if (_init && _sdkConfig != null)
            {
                UpdateConsentStatusFromConfig(_sdkConfig);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        [Preserve]
        static void Register()
        {
            Debug.Log("[Palette:MAX] Register() called - assembly is loaded!");
            MaxAdapter.RegisterImpl(new MaxAdapterImpl());
        }

        void OnSdkInit(MaxSdkBase.SdkConfiguration config)
        {
            Debug.Log("[Palette:MAX] Initialized");

            _init = true;
            _sdkConfig = config;

            // Check if CMP (UMP) is handling consent automatically
            bool cmpHandlesConsent = false;
            try
            {
                cmpHandlesConsent = MaxSdk.CmpService.HasSupportedCmp;
            }
            catch
            {
                // CmpService not available in older SDK versions
            }

            if (cmpHandlesConsent)
            {
                Debug.Log("[Palette:MAX] CMP enabled - consent handled by UMP");
            }
            else
            {
                MaxSdk.SetHasUserConsent(_consent);
                Debug.Log($"[Palette:MAX] SetHasUserConsent({_consent}) - no CMP");
            }

            UpdateConsentStatusFromConfig(config);

            InitRewarded();
            InitInterstitial();
            SubscribeILRD();

            OnSdkInitialized?.Invoke();
        }

        void UpdateConsentStatusFromConfig(MaxSdkBase.SdkConfiguration config)
        {
            ConsentStatus oldStatus = ConsentStatus;

            if (config.ConsentFlowUserGeography == MaxSdkBase.ConsentFlowUserGeography.Gdpr)
            {
                try
                {
                    if (MaxSdk.CmpService.HasSupportedCmp)
                    {
                        bool hasConsent = MaxSdk.HasUserConsent();
                        ConsentStatus = hasConsent ? ConsentStatus.Obtained : ConsentStatus.Denied;
                    }
                    else
                    {
                        ConsentStatus = _consent ? ConsentStatus.Obtained : ConsentStatus.Required;
                    }
                }
                catch
                {
                    ConsentStatus = _consent ? ConsentStatus.Obtained : ConsentStatus.Required;
                }
            }
            else if (config.ConsentFlowUserGeography == MaxSdkBase.ConsentFlowUserGeography.Unknown)
            {
                ConsentStatus = _consent ? ConsentStatus.Obtained : ConsentStatus.Required;
            }
            else
            {
                ConsentStatus = ConsentStatus.NotApplicable;
            }

            Debug.Log($"[Palette:MAX] ConsentStatus: {ConsentStatus} (Geography: {config.ConsentFlowUserGeography})");

            if (oldStatus != ConsentStatus)
            {
                OnConsentStatusChanged?.Invoke(ConsentStatus);
            }
        }

        #region Rewarded

        void InitRewarded()
        {
            if (string.IsNullOrEmpty(_rewardedId)) return;

            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnRewardedAdLoaded;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnRewardedAdLoadFailed;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRewardedAdHidden;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnRewardedAdDisplayFailed;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRewardedAdReceivedReward;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += OnRewardedAdRevenuePaid;

            LoadRewarded();
        }

        void OnRewardedAdLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            _rewardedReady = true;
            if (_userWaitingForRewarded)
            {
                _userWaitingForRewarded = false;
                OnAdLoadingStateChanged?.Invoke(AdType.Rewarded, false);
            }
        }

        void OnRewardedAdLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            _rewardedReady = false;
            if (_userWaitingForRewarded)
            {
                _userWaitingForRewarded = false;
                OnAdLoadingStateChanged?.Invoke(AdType.Rewarded, false);
            }
            LoadRewarded();
        }

        void OnRewardedAdHidden(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            _rewardedReady = false;
            LoadRewarded();
        }

        void OnRewardedAdDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            _rewardedReady = false;

            // Any visible overlay should be dismissed once we know we won't show.
            if (_userWaitingForRewarded)
            {
                _userWaitingForRewarded = false;
                OnAdLoadingStateChanged?.Invoke(AdType.Rewarded, false);
            }

            _onRewardFailed?.Invoke();
            _onRewardFailed = null;
            _onRewardComplete = null;
            LoadRewarded();
        }

        void OnRewardedAdReceivedReward(string adUnitId, MaxSdkBase.Reward reward, MaxSdkBase.AdInfo adInfo)
        {
            if (_userWaitingForRewarded)
            {
                _userWaitingForRewarded = false;
                OnAdLoadingStateChanged?.Invoke(AdType.Rewarded, false);
            }

            _onRewardComplete?.Invoke();
            _onRewardComplete = null;
            _onRewardFailed = null;
        }

        void OnRewardedAdRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo) => TrackAdRevenue(adInfo, "REWARDED");

        void LoadRewarded()
        {
            // Only show overlay when user is actively waiting for an ad
            if (_userWaitingForRewarded)
                OnAdLoadingStateChanged?.Invoke(AdType.Rewarded, true);
            MaxSdk.LoadRewardedAd(_rewardedId);
        }

        public void ShowRewardedAd(Action onComplete, Action onFailed)
        {
            if (!_init)
            {
                onFailed?.Invoke();
                return;
            }

            if (!_rewardedReady || !MaxSdk.IsRewardedAdReady(_rewardedId))
            {
                _userWaitingForRewarded = true;
                LoadRewarded();
                Debug.LogWarning("[Palette:MAX] Rewarded ad not ready");
                onFailed?.Invoke();
                return;
            }

            _onRewardComplete = onComplete;
            _onRewardFailed = onFailed;
            MaxSdk.ShowRewardedAd(_rewardedId);
        }

        #endregion

        #region Interstitial

        void InitInterstitial()
        {
            if (string.IsNullOrEmpty(_interstitialId)) return;

            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnInterstitialAdLoaded;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnInterstitialAdLoadFailed;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnInterstitialAdHidden;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += OnInterstitialAdDisplayFailed;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += OnInterstitialAdRevenuePaid;

            LoadInterstitial();
        }

        void OnInterstitialAdLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            _interstitialReady = true;
            if (_userWaitingForInterstitial)
            {
                _userWaitingForInterstitial = false;
                OnAdLoadingStateChanged?.Invoke(AdType.Interstitial, false);
            }
        }

        void OnInterstitialAdLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            _interstitialReady = false;
            if (_userWaitingForInterstitial)
            {
                _userWaitingForInterstitial = false;
                OnAdLoadingStateChanged?.Invoke(AdType.Interstitial, false);
            }
            LoadInterstitial();
        }

        void OnInterstitialAdHidden(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            _interstitialReady = false;

            if (_userWaitingForInterstitial)
            {
                _userWaitingForInterstitial = false;
                OnAdLoadingStateChanged?.Invoke(AdType.Interstitial, false);
            }

            _onInterstitialComplete?.Invoke();
            _onInterstitialComplete = null;
            LoadInterstitial();
        }

        void OnInterstitialAdDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            _interstitialReady = false;

            if (_userWaitingForInterstitial)
            {
                _userWaitingForInterstitial = false;
                OnAdLoadingStateChanged?.Invoke(AdType.Interstitial, false);
            }

            _onInterstitialComplete?.Invoke();
            _onInterstitialComplete = null;
            LoadInterstitial();
        }

        void OnInterstitialAdRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo) => TrackAdRevenue(adInfo, "INTERSTITIAL");

        void LoadInterstitial()
        {
            // Only show overlay when user is actively waiting for an ad
            if (_userWaitingForInterstitial)
                OnAdLoadingStateChanged?.Invoke(AdType.Interstitial, true);
            MaxSdk.LoadInterstitial(_interstitialId);
        }

        public void ShowInterstitialAd(Action onComplete)
        {
            if (!_init || !_interstitialReady || !MaxSdk.IsInterstitialReady(_interstitialId))
            {
                _userWaitingForInterstitial = true;
                LoadInterstitial();
                onComplete?.Invoke();
                return;
            }

            _onInterstitialComplete = onComplete;
            MaxSdk.ShowInterstitial(_interstitialId);
        }

        #endregion

        #region Ad Revenue

        void SubscribeILRD()
        {
#if GAMEANALYTICS_INSTALLED
            try
            {
                GameAnalyticsSDK.GameAnalyticsILRD.SubscribeMaxImpressions();
                Debug.Log("[Palette:MAX] GameAnalytics ILRD subscribed");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Palette:MAX] ILRD subscription failed: {e.Message}");
            }
#endif
        }

        void TrackAdRevenue(MaxSdkBase.AdInfo adInfo, string adFormat)
        {
#if SOROLLA_ADJUST_ENABLED
            AdjustAdapter.TrackAdRevenue(new AdRevenueInfo
            {
                Source = AdRevenueInfo.DefaultSource,
                Revenue = adInfo.Revenue,
                Currency = "USD",
                Network = adInfo.NetworkName,
                AdUnit = adInfo.AdUnitIdentifier,
                Placement = adInfo.Placement,
            });
#endif

            // Firebase ad_impression event
            FirebaseAdapter.TrackAdImpression(
                adPlatform: "applovin_max",
                adSource: adInfo.NetworkName,
                adFormat: adFormat,
                adUnitName: adInfo.AdUnitIdentifier,
                revenue: adInfo.Revenue,
                currency: "USD"
            );
        }

        #endregion
    }
}
