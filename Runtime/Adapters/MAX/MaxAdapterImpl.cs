using System;
using System.Collections.Generic;
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
        // _init only flips true after MAX finishes (OnSdkInit), so a second Initialize() in the
        // CMP window would pass an `if (_init)` guard and re-subscribe OnSdkInitializedEvent,
        // double-registering the ad-revenue callbacks. _initStarted is set at entry to block that (DR-02).
        bool _initStarted;
        string _interstitialId;
        bool _interstitialReady;
        Action _onInterstitialComplete;
        Action _onInterstitialFailed;

        Action _onRewardComplete;
        Action _onRewardFailed;
        string _rewardedId;

        bool _rewardedReady;
        bool _userWaitingForRewarded;
        bool _userWaitingForInterstitial;
        bool _rewardedLoadLogged;
        bool _interstitialLoadLogged;
        bool _rewardedLoadStarted;
        bool _interstitialLoadStarted;
        bool _rewardedLoadFailed;
        bool _interstitialLoadFailed;
        string _lastRewardedLoadIssue = "Not requested";
        string _lastInterstitialLoadIssue = "Not requested";

        MaxSdkBase.SdkConfiguration _sdkConfig;
        int _savedSleepTimeout;
        bool _screenAwakeActive;

        // Exponential backoff for ad load failures, per AppLovin guidance:
        // https://dash.applovin.com/documentation/mediation/unity/ad-formats/interstitials
        // Without backoff, no-fill / network failures retry-storm the waterfall and
        // burn battery + heat. Cap at 2^6 = 64s.
        const int MaxBackoffShift = 6;
        int _interstitialRetryAttempt;
        int _rewardedRetryAttempt;
        int _interstitialRetryGen;
        int _rewardedRetryGen;

        public bool IsInitialized => _init;
        public bool IsRewardedAdReady => _init && _rewardedReady && MaxSdk.IsRewardedAdReady(_rewardedId);
        public bool IsInterstitialAdReady => _init && _interstitialReady && MaxSdk.IsInterstitialReady(_interstitialId);
        public bool HasRewardedLoadStarted => _rewardedLoadStarted;
        public bool HasInterstitialLoadStarted => _interstitialLoadStarted;
        public bool HasRewardedLoadFailed => _rewardedLoadFailed;
        public bool HasInterstitialLoadFailed => _interstitialLoadFailed;
        public string LastRewardedLoadIssue => _lastRewardedLoadIssue;
        public string LastInterstitialLoadIssue => _lastInterstitialLoadIssue;
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

        public void Initialize(string rewardedId, string interstitialId, string bannerId, bool consent, bool verboseLogging = false)
        {
            if (_init || _initStarted) return;
            _initStarted = true;

            _rewardedId = rewardedId;
            _interstitialId = interstitialId;
            _bannerId = bannerId;
            _consent = consent;

            PaletteLog.Vital("[Palette:MAX] Initializing...");
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Max, AdapterDiagnosticStatus.Initializing,
                "init_requested", "Initializing");

            MaxSdk.SetVerboseLogging(verboseLogging);
            MaxSdk.SetCreativeDebuggerEnabled(verboseLogging);

            // Pin all MAX publisher callbacks to the Unity main thread. The default marshals most
            // events to the main thread, but per-event keepInBackground flags (or any code setting
            // this property false) can deliver a callback on a background thread - and Palette's
            // pending-event queues are not thread-safe (B-2). Forcing true guarantees main-thread delivery.
            MaxSdkBase.InvokeEventsOnUnityMainThread = true;

            MaxSdkCallbacks.OnSdkInitializedEvent += OnSdkInit;

            // SDK key is read from AppLovinSettings; Palette editor auto-syncs the shared publisher key.
            MaxSdk.InitializeSdk();
        }

        public void ShowPrivacyOptions(Action onComplete)
        {
            if (!_init)
            {
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Max, AdapterDiagnosticStatus.Warning,
                    "privacy_options_before_init", "Privacy options requested before MAX initialized");
                PaletteLog.Warning("[Palette:MAX] Cannot show privacy options - SDK not initialized");
                onComplete?.Invoke();
                return;
            }

            try
            {
                if (!MaxSdk.CmpService.HasSupportedCmp)
                {
                    AdapterDiagnostics.Record(AdapterDiagnosticVendor.Max, AdapterDiagnosticStatus.Warning,
                        "privacy_options_unavailable", "No CMP configured - privacy options not available");
                    PaletteLog.Warning("[Palette:MAX] No CMP configured - privacy options not available");
                    onComplete?.Invoke();
                    return;
                }

                PaletteLog.Vital("[Palette:MAX] Showing privacy options...");
                MaxSdk.CmpService.ShowCmpForExistingUser(error =>
                {
                    if (error != null)
                    {
                        AdapterDiagnostics.Record(AdapterDiagnosticVendor.Max, AdapterDiagnosticStatus.Warning,
                            "privacy_options_error", "Privacy options error");
                        PaletteLog.Warning("[Palette:MAX] Privacy options error. Rebuild with verbose logging to inspect SDK details.");
                        PaletteLog.Verbose($"[Palette:MAX] Privacy options error detail: {error.Message}");
                    }
                    else
                    {
                        PaletteLog.Vital("[Palette:MAX] Privacy options dismissed");
                        RefreshConsentStatus();
                    }
                    onComplete?.Invoke();
                });
            }
            catch (Exception e)
            {
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Max, AdapterDiagnosticStatus.Warning,
                    "cmp_unavailable", "CMP service not available");
                PaletteLog.Warning("[Palette:MAX] CMP service not available. Rebuild with verbose logging to inspect SDK details.");
                PaletteLog.Verbose($"[Palette:MAX] CmpService not available: {e.Message}");
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

        public void ShowMediationDebugger()
        {
            if (!_init)
            {
                PaletteLog.Warning("[Palette:MAX] Cannot show mediation debugger - SDK not initialized yet");
                return;
            }
            MaxSdk.ShowMediationDebugger();
        }

        public void ShowCreativeDebugger()
        {
            if (!_init)
            {
                PaletteLog.Warning("[Palette:MAX] Cannot show creative debugger - SDK not initialized yet");
                return;
            }
            MaxSdk.ShowCreativeDebugger();
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
                PaletteLog.Vital($"[Palette:MAX] UpdateConsent({consent})");
            }
            else
            {
                PaletteLog.Warning("[Palette:MAX] UpdateConsent called but CMP is enabled - use ShowPrivacyOptions() instead");
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
            PaletteLog.Verbose("[Palette:MAX] Register() called - assembly is loaded!");
            var impl = new MaxAdapterImpl();
            Application.focusChanged += impl.OnAppFocusChanged;
            MaxAdapter.RegisterImpl(impl);
        }

        // Prevent device screen from dimming/sleeping while a fullscreen ad is displayed.
        // MAX and mediated networks don't consistently set FLAG_KEEP_SCREEN_ON on every adapter,
        // and some hidden/failed callbacks can be missed; focus-regain acts as a safety release.
        void AcquireScreenWake()
        {
            if (_screenAwakeActive) return;
            _savedSleepTimeout = Screen.sleepTimeout;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            _screenAwakeActive = true;
        }

        void ReleaseScreenWake()
        {
            if (!_screenAwakeActive) return;
            Screen.sleepTimeout = _savedSleepTimeout;
            _screenAwakeActive = false;
        }

        void OnAppFocusChanged(bool hasFocus)
        {
            if (hasFocus && _screenAwakeActive) ReleaseScreenWake();
        }

        void OnSdkInit(MaxSdkBase.SdkConfiguration config)
        {
            PaletteLog.Vital("[Palette:MAX] Initialized");
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Max, AdapterDiagnosticStatus.Ready,
                "initialized", "Initialized");

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
                PaletteLog.Vital("[Palette:MAX] CMP enabled - consent handled by UMP");
            }
            else
            {
                MaxSdk.SetHasUserConsent(_consent);
                PaletteLog.Vital($"[Palette:MAX] SetHasUserConsent({_consent}) - no CMP");
            }

            UpdateConsentStatusFromConfig(config);

            InitRewarded();
            InitInterstitial();

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

            PaletteLog.Vital($"[Palette:MAX] ConsentStatus: {ConsentStatus} (Geography: {config.ConsentFlowUserGeography})");

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
            // adUnitId/adInfo identify the MAX callback source; Palette tracks one configured rewarded unit here.
            _rewardedReady = true;
            _rewardedLoadFailed = false;
            _lastRewardedLoadIssue = "Loaded";
            _rewardedRetryAttempt = 0;
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Max, AdapterDiagnosticStatus.Ready,
                "rewarded_loaded", "Rewarded ad loaded");
            if (!_rewardedLoadLogged)
            {
                _rewardedLoadLogged = true;
                PaletteLog.Vital("[Palette:MAX] Rewarded ad loaded");
            }
            else
            {
                PaletteLog.Verbose("[Palette:MAX] Rewarded ad loaded");
            }
            if (_userWaitingForRewarded)
            {
                _userWaitingForRewarded = false;
                OnAdLoadingStateChanged?.Invoke(AdType.Rewarded, false);
            }
        }

        void OnRewardedAdLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            // adUnitId identifies the MAX callback source; Palette tracks one configured rewarded unit here.
            _rewardedReady = false;
            _rewardedLoadFailed = true;
            _lastRewardedLoadIssue = $"Load failed: code={(int)errorInfo.Code}, mediatedCode={errorInfo.MediatedNetworkErrorCode}";
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Max, AdapterDiagnosticStatus.Warning,
                "rewarded_load_failed", _lastRewardedLoadIssue);
            PaletteLog.WarningOnce($"max.rewarded.load_failed.{(int)errorInfo.Code}.{errorInfo.MediatedNetworkErrorCode}",
                $"[Palette:MAX] Rewarded ad load failed; retrying with backoff. code={(int)errorInfo.Code}, mediatedCode={errorInfo.MediatedNetworkErrorCode}. Rebuild with verbose logging to inspect SDK details.");
            PaletteLog.Verbose($"[Palette:MAX] Rewarded ad load failed detail: {errorInfo.Message}");
            if (_userWaitingForRewarded)
            {
                _userWaitingForRewarded = false;
                OnAdLoadingStateChanged?.Invoke(AdType.Rewarded, false);
            }
            ScheduleRewardedRetry();
        }

        void OnRewardedAdHidden(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            // adUnitId/adInfo identify the MAX callback source; hiding always triggers the same reload path.
            ReleaseScreenWake();
            _rewardedReady = false;
            LoadRewarded();
        }

        void OnRewardedAdDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            // adUnitId identifies the MAX callback source; adInfo carries the network detail we need.
            ReleaseScreenWake();
            _rewardedReady = false;

            // Any visible overlay should be dismissed once we know we won't show.
            if (_userWaitingForRewarded)
            {
                _userWaitingForRewarded = false;
                OnAdLoadingStateChanged?.Invoke(AdType.Rewarded, false);
            }

            TrackAdShowFailed("rewarded", "display_error", adInfo?.NetworkName, errorInfo);
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Max, AdapterDiagnosticStatus.Warning,
                "rewarded_display_failed", "Rewarded ad display failed");
            PaletteLog.Warning($"[Palette:MAX] Rewarded ad display failed. code={(int)errorInfo.Code}, mediatedCode={errorInfo.MediatedNetworkErrorCode}. Rebuild with verbose logging to inspect SDK details.");
            PaletteLog.Verbose($"[Palette:MAX] Rewarded ad display failed detail: {errorInfo.Message}");

            _onRewardFailed?.Invoke();
            _onRewardFailed = null;
            _onRewardComplete = null;
            LoadRewarded();
        }

        void OnRewardedAdReceivedReward(string adUnitId, MaxSdkBase.Reward reward, MaxSdkBase.AdInfo adInfo)
        {
            // adUnitId/reward/adInfo are not needed; Palette treats any reward callback as completion.
            if (_userWaitingForRewarded)
            {
                _userWaitingForRewarded = false;
                OnAdLoadingStateChanged?.Invoke(AdType.Rewarded, false);
            }

            _onRewardComplete?.Invoke();
            _onRewardComplete = null;
            _onRewardFailed = null;
            PaletteLog.Vital("[Palette:MAX] Rewarded ad completed");
        }

        void OnRewardedAdRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            // adUnitId is redundant with adInfo.AdUnitIdentifier for Palette revenue telemetry.
            TrackAdRevenue(adInfo, "rewarded");
        }

        void LoadRewarded()
        {
            _rewardedLoadStarted = true;
            if (_lastRewardedLoadIssue == "Not requested")
                _lastRewardedLoadIssue = "Requested";

            // Only show overlay when user is actively waiting for an ad
            if (_userWaitingForRewarded)
                OnAdLoadingStateChanged?.Invoke(AdType.Rewarded, true);
            MaxSdk.LoadRewardedAd(_rewardedId);
        }

        void ScheduleRewardedRetry()
        {
            int attempt = Math.Min(_rewardedRetryAttempt, MaxBackoffShift);
            float delay = 1 << attempt;
            _rewardedRetryAttempt++;
            int gen = ++_rewardedRetryGen;
            PaletteLog.Verbose($"[Palette:MAX] Rewarded load retry in {delay}s (attempt {_rewardedRetryAttempt})");

            // Defensive: if Bootstrapper hasn't wired the scheduler, fall back to
            // immediate load (matches pre-fix behavior). In practice never hits —
            // ScheduleDelegate is set at BeforeSceneLoad, well before MAX init.
            if (MaxAdapter.ScheduleDelegate == null) { LoadRewarded(); return; }

            MaxAdapter.ScheduleDelegate(delay, () =>
            {
                if (gen != _rewardedRetryGen) return;
                if (_rewardedReady) return;
                LoadRewarded();
            });
        }

        public void ShowRewardedAd(Action onComplete, Action onFailed)
        {
            TrackAdShowRequested("rewarded");

            if (!_init)
            {
                TrackAdShowFailed("rewarded", "not_initialized", network: null, errorInfo: null);
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Max, AdapterDiagnosticStatus.DispatchDropped,
                    "rewarded_not_initialized", "Rewarded show requested before MAX initialized");
                onFailed?.Invoke();
                return;
            }

            if (!_rewardedReady || !MaxSdk.IsRewardedAdReady(_rewardedId))
            {
                _userWaitingForRewarded = true;
                LoadRewarded();
                PaletteLog.Warning("[Palette:MAX] Rewarded ad not ready");
                TrackAdShowFailed("rewarded", "not_ready", network: null, errorInfo: null);
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Max, AdapterDiagnosticStatus.Warning,
                    "rewarded_not_ready", "Rewarded ad not ready");
                onFailed?.Invoke();
                return;
            }

            _onRewardComplete = onComplete;
            _onRewardFailed = onFailed;
            AcquireScreenWake();
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Max, AdapterDiagnosticStatus.DispatchAccepted,
                "rewarded_show", "Rewarded show accepted");
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
            // adUnitId/adInfo identify the MAX callback source; Palette tracks one configured interstitial unit here.
            _interstitialReady = true;
            _interstitialLoadFailed = false;
            _lastInterstitialLoadIssue = "Loaded";
            _interstitialRetryAttempt = 0;
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Max, AdapterDiagnosticStatus.Ready,
                "interstitial_loaded", "Interstitial ad loaded");
            if (!_interstitialLoadLogged)
            {
                _interstitialLoadLogged = true;
                PaletteLog.Vital("[Palette:MAX] Interstitial ad loaded");
            }
            else
            {
                PaletteLog.Verbose("[Palette:MAX] Interstitial ad loaded");
            }
            if (_userWaitingForInterstitial)
            {
                _userWaitingForInterstitial = false;
                OnAdLoadingStateChanged?.Invoke(AdType.Interstitial, false);
            }
        }

        void OnInterstitialAdLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            // adUnitId identifies the MAX callback source; Palette tracks one configured interstitial unit here.
            _interstitialReady = false;
            _interstitialLoadFailed = true;
            _lastInterstitialLoadIssue = $"Load failed: code={(int)errorInfo.Code}, mediatedCode={errorInfo.MediatedNetworkErrorCode}";
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Max, AdapterDiagnosticStatus.Warning,
                "interstitial_load_failed", _lastInterstitialLoadIssue);
            PaletteLog.WarningOnce($"max.interstitial.load_failed.{(int)errorInfo.Code}.{errorInfo.MediatedNetworkErrorCode}",
                $"[Palette:MAX] Interstitial ad load failed; retrying with backoff. code={(int)errorInfo.Code}, mediatedCode={errorInfo.MediatedNetworkErrorCode}. Rebuild with verbose logging to inspect SDK details.");
            PaletteLog.Verbose($"[Palette:MAX] Interstitial ad load failed detail: {errorInfo.Message}");
            if (_userWaitingForInterstitial)
            {
                _userWaitingForInterstitial = false;
                OnAdLoadingStateChanged?.Invoke(AdType.Interstitial, false);
            }
            ScheduleInterstitialRetry();
        }

        void OnInterstitialAdHidden(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            // adUnitId/adInfo identify the MAX callback source; hidden means the interstitial flow completed.
            ReleaseScreenWake();
            _interstitialReady = false;

            if (_userWaitingForInterstitial)
            {
                _userWaitingForInterstitial = false;
                OnAdLoadingStateChanged?.Invoke(AdType.Interstitial, false);
            }

            Action cb = _onInterstitialComplete;
            _onInterstitialComplete = null;
            _onInterstitialFailed = null;
            cb?.Invoke();
            PaletteLog.Vital("[Palette:MAX] Interstitial ad completed");
            LoadInterstitial();
        }

        void OnInterstitialAdDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            // adUnitId identifies the MAX callback source; adInfo carries the network detail we need.
            ReleaseScreenWake();
            _interstitialReady = false;

            if (_userWaitingForInterstitial)
            {
                _userWaitingForInterstitial = false;
                OnAdLoadingStateChanged?.Invoke(AdType.Interstitial, false);
            }

            TrackAdShowFailed("interstitial", "display_error", adInfo?.NetworkName, errorInfo);
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Max, AdapterDiagnosticStatus.Warning,
                "interstitial_display_failed", "Interstitial ad display failed");
            PaletteLog.Warning($"[Palette:MAX] Interstitial ad display failed. code={(int)errorInfo.Code}, mediatedCode={errorInfo.MediatedNetworkErrorCode}. Rebuild with verbose logging to inspect SDK details.");
            PaletteLog.Verbose($"[Palette:MAX] Interstitial ad display failed detail: {errorInfo.Message}");

            Action cb = _onInterstitialFailed;
            _onInterstitialComplete = null;
            _onInterstitialFailed = null;
            cb?.Invoke();
            LoadInterstitial();
        }

        void OnInterstitialAdRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            // adUnitId is redundant with adInfo.AdUnitIdentifier for Palette revenue telemetry.
            TrackAdRevenue(adInfo, "interstitial");
        }

        void LoadInterstitial()
        {
            _interstitialLoadStarted = true;
            if (_lastInterstitialLoadIssue == "Not requested")
                _lastInterstitialLoadIssue = "Requested";

            // Only show overlay when user is actively waiting for an ad
            if (_userWaitingForInterstitial)
                OnAdLoadingStateChanged?.Invoke(AdType.Interstitial, true);
            MaxSdk.LoadInterstitial(_interstitialId);
        }

        void ScheduleInterstitialRetry()
        {
            int attempt = Math.Min(_interstitialRetryAttempt, MaxBackoffShift);
            float delay = 1 << attempt;
            _interstitialRetryAttempt++;
            int gen = ++_interstitialRetryGen;
            PaletteLog.Verbose($"[Palette:MAX] Interstitial load retry in {delay}s (attempt {_interstitialRetryAttempt})");

            if (MaxAdapter.ScheduleDelegate == null) { LoadInterstitial(); return; }

            MaxAdapter.ScheduleDelegate(delay, () =>
            {
                if (gen != _interstitialRetryGen) return;
                if (_interstitialReady) return;
                LoadInterstitial();
            });
        }

        public void ShowInterstitialAd(Action onComplete, Action onFailed)
        {
            TrackAdShowRequested("interstitial");

            if (!_init || !_interstitialReady || !MaxSdk.IsInterstitialReady(_interstitialId))
            {
                _userWaitingForInterstitial = true;
                LoadInterstitial();
                PaletteLog.Warning(_init ? "[Palette:MAX] Interstitial ad not ready" : "[Palette:MAX] Interstitial ad requested before MAX initialized");
                TrackAdShowFailed("interstitial", !_init ? "not_initialized" : "not_ready", network: null, errorInfo: null);
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Max,
                    !_init ? AdapterDiagnosticStatus.DispatchDropped : AdapterDiagnosticStatus.Warning,
                    !_init ? "interstitial_not_initialized" : "interstitial_not_ready",
                    !_init ? "Interstitial show requested before MAX initialized" : "Interstitial ad not ready");
                onFailed?.Invoke();
                return;
            }

            _onInterstitialComplete = onComplete;
            _onInterstitialFailed = onFailed;
            AcquireScreenWake();
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Max, AdapterDiagnosticStatus.DispatchAccepted,
                "interstitial_show", "Interstitial show accepted");
            MaxSdk.ShowInterstitial(_interstitialId);
        }

        #endregion

        #region Ad Show Telemetry

        // User-intent events. Background load/retry churn is left to the MAX dashboard;
        // Firebase gets the two events that matter for the in-app funnel:
        //   ad_show_requested  — user code called ShowRewardedAd/ShowInterstitialAd
        //   ad_show_failed     — that call returned without showing an ad
        // Pair with existing ad_impression to get show-rate = impressions / requests.
        // Breakdown by `reason` isolates offline/no-fill (not_ready) vs display bugs
        // (display_error) vs init race conditions (not_initialized).
        static void TrackAdShowRequested(string adFormat)
        {
            FirebaseAdapter.TrackEvent("ad_show_requested", new Dictionary<string, object>
            {
                { "ad_format", adFormat },
            });
        }

        static void TrackAdShowFailed(string adFormat, string reason, string network, MaxSdkBase.ErrorInfo errorInfo)
        {
            var parameters = new Dictionary<string, object>
            {
                { "ad_format", adFormat },
                { "reason", reason },
            };
            if (!string.IsNullOrEmpty(network))
                parameters["network"] = network;
            if (errorInfo != null)
            {
                parameters["error_code"] = (int)errorInfo.Code;
                parameters["mediated_error_code"] = errorInfo.MediatedNetworkErrorCode;
            }
            FirebaseAdapter.TrackEvent("ad_show_failed", parameters);
        }

        #endregion

        #region Ad Revenue

        void TrackAdRevenue(MaxSdkBase.AdInfo adInfo, string adFormat)
        {
            double revenue = adInfo.Revenue;

            // MAX returns Revenue = -1 when the impression has no valid revenue (error or test mode,
            // per AppLovin docs). Forwarding it would push negative revenue into Adjust/Firebase/
            // TikTok and corrupt ROAS, so drop the fan-out and warn with the raw value (DR-06).
            if (revenue < 0)
            {
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Max, AdapterDiagnosticStatus.Warning,
                    "revenue_unavailable", "Ad revenue unavailable - skipping revenue fan-out");
                PaletteLog.Warning($"[Palette:MAX] Ad revenue unavailable (revenue={revenue}, network={adInfo.NetworkName}, format={adFormat}) - skipping revenue fan-out.");
                return;
            }

            // Record the dispatch so the Sorolla Vitals "Ad revenue" row reflects a real forwarded
            // impression, independent of log verbosity / installed vendors (DR-09). This also fans
            // the event out to Adjust/TikTok/Firebase via MaxAdRevenueRelay, which subscribes to
            // MaxAdapter.OnAdRevenueTracked - the MAX bridge itself stays MAX-only.
            MaxAdapter.RecordAdRevenue(new MaxAdRevenueInfo(adInfo.NetworkName, revenue, "USD", adFormat,
                adInfo.RevenuePrecision, adInfo.AdUnitIdentifier, adInfo.Placement));
        }

        #endregion
    }
}
