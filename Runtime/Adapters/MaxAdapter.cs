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
    ///     Consent status for GDPR/privacy compliance.
    ///     Reflects the user's consent state as determined by MAX's UMP integration.
    /// </summary>
    public enum ConsentStatus
    {
        /// <summary>Consent status not yet determined</summary>
        Unknown,
        /// <summary>User is not in a GDPR region, consent not required</summary>
        NotApplicable,
        /// <summary>User is in GDPR region, consent required but not yet obtained</summary>
        Required,
        /// <summary>User has provided consent</summary>
        Obtained,
        /// <summary>User has denied consent</summary>
        Denied,
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

        static MaxSdkBase.SdkConfiguration s_sdkConfig;

        /// <summary>Whether a rewarded ad is ready to show</summary>
        public static bool IsRewardedAdReady => s_init && s_rewardedReady && MaxSdk.IsRewardedAdReady(s_rewardedId);

        /// <summary>Whether an interstitial ad is ready to show</summary>
        public static bool IsInterstitialAdReady => s_init && s_interstitialReady && MaxSdk.IsInterstitialReady(s_interstitialId);

        /// <summary>
        ///     Current consent status from MAX's UMP integration.
        ///     Check this after SDK initialization to determine if ads can be shown.
        /// </summary>
        public static ConsentStatus ConsentStatus { get; private set; } = ConsentStatus.Unknown;

        /// <summary>
        ///     Whether ads can be requested (consent obtained or not required).
        ///     Use this to gate ad loading/showing in GDPR regions.
        /// </summary>
        public static bool CanRequestAds => ConsentStatus == ConsentStatus.Obtained ||
                                            ConsentStatus == ConsentStatus.NotApplicable;

        /// <summary>
        ///     Whether privacy options should be shown in settings.
        ///     Only true if a CMP is available and user is in a consent region.
        /// </summary>
        public static bool IsPrivacyOptionsRequired
        {
            get
            {
                if (!s_init) return false;
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

        /// <summary>Event fired when ad loading state changes. (adType, isLoading)</summary>
        public static event Action<AdType, bool> OnAdLoadingStateChanged;

        /// <summary>Event fired when MAX SDK is initialized. Use this to initialize other SDKs like Adjust.</summary>
        public static event Action OnSdkInitialized;

        /// <summary>Event fired when consent status changes.</summary>
        public static event Action<ConsentStatus> OnConsentStatusChanged;

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

            MaxSdk.SetSdkKey(sdkKey);
            MaxSdk.InitializeSdk();
        }

        static void OnSdkInit(MaxSdkBase.SdkConfiguration config)
        {
            Debug.Log("[Sorolla:MAX] Initialized");

            s_init = true;
            s_sdkConfig = config;

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
                // UMP is enabled - MAX has already handled consent via CMP flow
                // Do NOT call SetHasUserConsent as it would override UMP's consent
                Debug.Log("[Sorolla:MAX] CMP enabled - consent handled by UMP");
            }
            else
            {
                // No CMP - use passed consent flag (legacy behavior for iOS ATT)
                MaxSdk.SetHasUserConsent(s_consent);
                Debug.Log($"[Sorolla:MAX] SetHasUserConsent({s_consent}) - no CMP");
            }

            // Determine consent status from SDK configuration
            UpdateConsentStatusFromConfig(config);

            InitRewarded();
            InitInterstitial();

            // Fire event so other SDKs (like Adjust) can initialize in this callback
            OnSdkInitialized?.Invoke();
        }

        static void UpdateConsentStatusFromConfig(MaxSdkBase.SdkConfiguration config)
        {
            var oldStatus = ConsentStatus;

            // Check if user is in GDPR region
            if (config.ConsentFlowUserGeography == MaxSdkBase.ConsentFlowUserGeography.Gdpr)
            {
                // User is in GDPR region - determine consent from CMP/UMP state
                try
                {
                    if (MaxSdk.CmpService.HasSupportedCmp)
                    {
                        // CMP is available - check if consent flow requires showing
                        // After UMP completes, we can check HasUserConsent for the result
                        // Note: MAX SDK's HasUserConsent() returns the consent state set by UMP
                        bool hasConsent = MaxSdk.HasUserConsent();
                        ConsentStatus = hasConsent ? ConsentStatus.Obtained : ConsentStatus.Denied;
                    }
                    else
                    {
                        // No CMP configured - use passed consent flag (from iOS ATT)
                        ConsentStatus = s_consent ? ConsentStatus.Obtained : ConsentStatus.Required;
                    }
                }
                catch
                {
                    // CmpService not available in this SDK version - use legacy flag
                    ConsentStatus = s_consent ? ConsentStatus.Obtained : ConsentStatus.Required;
                }
            }
            else if (config.ConsentFlowUserGeography == MaxSdkBase.ConsentFlowUserGeography.Unknown)
            {
                // Geography unknown - treat as requiring consent for safety
                ConsentStatus = s_consent ? ConsentStatus.Obtained : ConsentStatus.Required;
            }
            else
            {
                // Not in GDPR region (Other)
                ConsentStatus = ConsentStatus.NotApplicable;
            }

            Debug.Log($"[Sorolla:MAX] ConsentStatus: {ConsentStatus} (Geography: {config.ConsentFlowUserGeography})");

            if (oldStatus != ConsentStatus)
            {
                OnConsentStatusChanged?.Invoke(ConsentStatus);
            }
        }

        /// <summary>
        ///     Show privacy options form (UMP consent form) for users to update their consent.
        ///     Call this from your settings screen when PrivacyOptionsRequired is true.
        /// </summary>
        /// <param name="onComplete">Optional callback when form is dismissed</param>
        public static void ShowPrivacyOptions(Action onComplete = null)
        {
            if (!s_init)
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
                        // Re-check consent status after user interaction
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

        /// <summary>
        ///     Refresh consent status from MAX SDK.
        ///     Call this after showing privacy options or if consent may have changed.
        /// </summary>
        public static void RefreshConsentStatus()
        {
            if (!s_init || s_sdkConfig == null) return;

            // Update s_consent from MAX SDK's actual state
            try
            {
                s_consent = MaxSdk.HasUserConsent();
            }
            catch
            {
                // HasUserConsent not available - keep existing value
            }

            UpdateConsentStatusFromConfig(s_sdkConfig);
        }

        /// <summary>
        ///     Update consent status manually.
        ///     Note: When UMP/CMP is enabled, use ShowPrivacyOptions() instead for GDPR compliance.
        ///     This method is primarily for iOS ATT consent when CMP is not configured.
        /// </summary>
        public static void UpdateConsent(bool consent)
        {
            s_consent = consent;

            // Only set consent directly if CMP is not handling it
            bool cmpHandlesConsent = false;
            try
            {
                cmpHandlesConsent = s_init && MaxSdk.CmpService.HasSupportedCmp;
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

            if (s_init && s_sdkConfig != null)
            {
                UpdateConsentStatusFromConfig(s_sdkConfig);
            }
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

    public static class MaxAdapter
    {
        #pragma warning disable CS0067 // Event is never used (stub for API compatibility)
        public static event System.Action<AdType, bool> OnAdLoadingStateChanged;
        public static event System.Action OnSdkInitialized;
        public static event System.Action<ConsentStatus> OnConsentStatusChanged;
        #pragma warning restore CS0067

        public static bool IsRewardedAdReady => false;
        public static bool IsInterstitialAdReady => false;
        public static ConsentStatus ConsentStatus => ConsentStatus.Unknown;
        public static bool CanRequestAds => false;
        public static bool IsPrivacyOptionsRequired => false;

        public static void Initialize(string k, string r, string i, string b, bool c) => UnityEngine.Debug.LogWarning("[Sorolla:MAX] Not installed");
        public static void ShowRewardedAd(System.Action c, System.Action f) => f?.Invoke();
        public static void ShowInterstitialAd(System.Action c) => c?.Invoke();
        public static void ShowPrivacyOptions(System.Action c = null) => c?.Invoke();
        public static void RefreshConsentStatus() { }
        public static void UpdateConsent(bool c) { }
    }
}
#endif
