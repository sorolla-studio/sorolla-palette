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
        private bool _init;
        private string _rewardedId;
        private string _interstitialId;
        private string _bannerId;

        private Action _onRewardComplete;
        private Action _onRewardFailed;
        private Action _onInterstitialComplete;

        private bool _rewardedReady;
        private bool _interstitialReady;
        private bool _consent;

        private MaxSdkBase.SdkConfiguration _sdkConfig;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        [Preserve]
        private static void Register()
        {
            Debug.Log("[Sorolla:MAX] Register() called - assembly is loaded!");
            MaxAdapter.RegisterImpl(new MaxAdapterImpl());
        }

        public bool IsRewardedAdReady => _init && _rewardedReady && MaxSdk.IsRewardedAdReady(_rewardedId);
        public bool IsInterstitialAdReady => _init && _interstitialReady && MaxSdk.IsInterstitialReady(_interstitialId);
        public ConsentStatus ConsentStatus { get; private set; } = ConsentStatus.Unknown;

        public bool CanRequestAds => ConsentStatus == ConsentStatus.Obtained ||
                                     ConsentStatus == ConsentStatus.NotApplicable;

        public bool IsPrivacyOptionsRequired
        {
            get
            {
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

        public void Initialize(string sdkKey, string rewardedId, string interstitialId, string bannerId, bool consent)
        {
            if (_init) return;

            _rewardedId = rewardedId;
            _interstitialId = interstitialId;
            _bannerId = bannerId;
            _consent = consent;

            Debug.Log("[Sorolla:MAX] Initializing...");
            MaxSdkCallbacks.OnSdkInitializedEvent += OnSdkInit;

            MaxSdk.SetSdkKey(sdkKey);
            MaxSdk.InitializeSdk();
        }

        private void OnSdkInit(MaxSdkBase.SdkConfiguration config)
        {
            Debug.Log("[Sorolla:MAX] Initialized");

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
                Debug.Log("[Sorolla:MAX] CMP enabled - consent handled by UMP");
            }
            else
            {
                MaxSdk.SetHasUserConsent(_consent);
                Debug.Log($"[Sorolla:MAX] SetHasUserConsent({_consent}) - no CMP");
            }

            UpdateConsentStatusFromConfig(config);

            InitRewarded();
            InitInterstitial();

            OnSdkInitialized?.Invoke();
        }

        private void UpdateConsentStatusFromConfig(MaxSdkBase.SdkConfiguration config)
        {
            var oldStatus = ConsentStatus;

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

            Debug.Log($"[Sorolla:MAX] ConsentStatus: {ConsentStatus} (Geography: {config.ConsentFlowUserGeography})");

            if (oldStatus != ConsentStatus)
            {
                OnConsentStatusChanged?.Invoke(ConsentStatus);
            }
        }

        public void ShowPrivacyOptions(Action onComplete)
        {
            if (!_init)
            {
                Debug.LogWarning("[Sorolla:MAX] Cannot show privacy options - SDK not initialized");
                onComplete?.Invoke();
                return;
            }

            try
            {
                if (!MaxSdk.CmpService.HasSupportedCmp)
                {
                    Debug.LogWarning("[Sorolla:MAX] No CMP configured - privacy options not available");
                    onComplete?.Invoke();
                    return;
                }

                Debug.Log("[Sorolla:MAX] Showing privacy options...");
                MaxSdk.CmpService.ShowCmpForExistingUser(error =>
                {
                    if (error != null)
                    {
                        Debug.LogWarning($"[Sorolla:MAX] Privacy options error: {error.Message}");
                    }
                    else
                    {
                        Debug.Log("[Sorolla:MAX] Privacy options dismissed");
                        RefreshConsentStatus();
                    }
                    onComplete?.Invoke();
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Sorolla:MAX] CmpService not available: {e.Message}");
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
                Debug.Log($"[Sorolla:MAX] UpdateConsent({consent})");
            }
            else
            {
                Debug.LogWarning("[Sorolla:MAX] UpdateConsent called but CMP is enabled - use ShowPrivacyOptions() instead");
            }

            if (_init && _sdkConfig != null)
            {
                UpdateConsentStatusFromConfig(_sdkConfig);
            }
        }

        #region Rewarded

        private void InitRewarded()
        {
            if (string.IsNullOrEmpty(_rewardedId)) return;

            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += (_, __) =>
            {
                _rewardedReady = true;
                OnAdLoadingStateChanged?.Invoke(AdType.Rewarded, false);
            };
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += (_, __) =>
            {
                _rewardedReady = false;
                OnAdLoadingStateChanged?.Invoke(AdType.Rewarded, false);
                LoadRewarded();
            };
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += (_, __) =>
            {
                _rewardedReady = false;
                LoadRewarded();
            };
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += (_, __, ___) =>
            {
                _rewardedReady = false;
                _onRewardFailed?.Invoke();
                _onRewardFailed = null;
                _onRewardComplete = null;
                LoadRewarded();
            };
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += (_, __, ___) =>
            {
                _onRewardComplete?.Invoke();
                _onRewardComplete = null;
                _onRewardFailed = null;
            };
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += (_, info) =>
            {
#if SOROLLA_ADJUST_ENABLED
                AdjustAdapter.TrackAdRevenue(new AdRevenueInfo
                {
                    Source = "applovin_max_sdk",
                    Revenue = info.Revenue,
                    Currency = "USD",
                    Network = info.NetworkName,
                    AdUnit = info.AdUnitIdentifier,
                    Placement = info.Placement
                });
#endif
            };

            LoadRewarded();
        }

        private void LoadRewarded()
        {
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
                LoadRewarded();
                Debug.LogWarning("[Sorolla:MAX] Rewarded ad not ready");
                onFailed?.Invoke();
                return;
            }

            _onRewardComplete = onComplete;
            _onRewardFailed = onFailed;
            MaxSdk.ShowRewardedAd(_rewardedId);
        }

        #endregion

        #region Interstitial

        private void InitInterstitial()
        {
            if (string.IsNullOrEmpty(_interstitialId)) return;

            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += (_, __) =>
            {
                _interstitialReady = true;
                OnAdLoadingStateChanged?.Invoke(AdType.Interstitial, false);
            };
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += (_, __) =>
            {
                _interstitialReady = false;
                OnAdLoadingStateChanged?.Invoke(AdType.Interstitial, false);
                LoadInterstitial();
            };
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += (_, __) =>
            {
                _interstitialReady = false;
                _onInterstitialComplete?.Invoke();
                _onInterstitialComplete = null;
                LoadInterstitial();
            };
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += (_, __, ___) =>
            {
                _interstitialReady = false;
                _onInterstitialComplete?.Invoke();
                _onInterstitialComplete = null;
                LoadInterstitial();
            };
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += (_, info) =>
            {
#if SOROLLA_ADJUST_ENABLED
                AdjustAdapter.TrackAdRevenue(new AdRevenueInfo
                {
                    Source = "applovin_max_sdk",
                    Revenue = info.Revenue,
                    Currency = "USD",
                    Network = info.NetworkName,
                    AdUnit = info.AdUnitIdentifier,
                    Placement = info.Placement
                });
#endif
            };

            LoadInterstitial();
        }

        private void LoadInterstitial()
        {
            OnAdLoadingStateChanged?.Invoke(AdType.Interstitial, true);
            MaxSdk.LoadInterstitial(_interstitialId);
        }

        public void ShowInterstitialAd(Action onComplete)
        {
            if (!_init || !_interstitialReady || !MaxSdk.IsInterstitialReady(_interstitialId))
            {
                onComplete?.Invoke();
                LoadInterstitial();
                return;
            }

            _onInterstitialComplete = onComplete;
            MaxSdk.ShowInterstitial(_interstitialId);
        }

        #endregion
    }
}
