using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using UnityEngine;
using Sorolla.Palette.Adapters;
#if GAMEANALYTICS_INSTALLED
using GameAnalyticsSDK;
#endif

namespace Sorolla.Palette
{

    /// <summary>
    ///     Progression status for tracking level/stage events.
    /// </summary>
    public enum ProgressionStatus
    {
        Start,
        Complete,
        Fail,
    }

    /// <summary>
    ///     Resource flow type for tracking economy events.
    /// </summary>
    public enum ResourceFlowType
    {
        /// <summary>Player gained resources</summary>
        Source,
        /// <summary>Player spent resources</summary>
        Sink,
    }


    /// <summary>
    ///     Main API for Palette SDK.
    ///     Provides unified interface for analytics, ads, and attribution.
    ///     Auto-initialized - no manual setup required.
    /// </summary>
    public static class Palette
    {
        const string Tag = "[Palette]";

        /// <summary>Whether the SDK is initialized</summary>
        public static bool IsInitialized { get; private set; }

        /// <summary>Current user consent status (legacy - use ConsentStatus for GDPR compliance)</summary>
        public static bool HasConsent { get; private set; }

        /// <summary>Current configuration (may be null)</summary>
        public static SorollaConfig Config { get; private set; }

        #region GDPR/Privacy Consent

        /// <summary>
        ///     Current consent status from MAX's UMP integration.
        ///     Use this to determine ad loading/showing in GDPR regions.
        /// </summary>
        /// <remarks>
        ///     Values: Unknown, NotApplicable, Required, Obtained, Denied.
        ///     See <see cref="Adapters.ConsentStatus"/> for details.
        /// </remarks>
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
        public static Adapters.ConsentStatus ConsentStatus => MaxAdapter.ConsentStatus;
#else
        public static Adapters.ConsentStatus ConsentStatus => Adapters.ConsentStatus.NotApplicable;
#endif

        /// <summary>
        ///     Whether ads can be requested (consent obtained or not required).
        ///     Use this to gate ad loading/showing in GDPR regions.
        /// </summary>
        /// <example>
        ///     if (Palette.CanRequestAds)
        ///         Palette.ShowRewardedAd(onComplete, onFailed);
        ///     else
        ///         Debug.Log("Consent required");
        /// </example>
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
        public static bool CanRequestAds => MaxAdapter.CanRequestAds;
#else
        public static bool CanRequestAds => false;
#endif

        /// <summary>
        ///     Whether a privacy options button should be shown in settings.
        ///     Only true if MAX CMP is available and user is in a consent region.
        /// </summary>
        /// <example>
        ///     privacyButton.gameObject.SetActive(Palette.PrivacyOptionsRequired);
        /// </example>
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
        public static bool PrivacyOptionsRequired => MaxAdapter.IsPrivacyOptionsRequired;
#else
        public static bool PrivacyOptionsRequired => false;
#endif

        /// <summary>
        ///     Event fired when consent status changes.
        ///     Subscribe to update UI or behavior based on consent.
        /// </summary>
        public static event Action<Adapters.ConsentStatus> OnConsentStatusChanged
        {
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            add => MaxAdapter.OnConsentStatusChanged += value;
            remove => MaxAdapter.OnConsentStatusChanged -= value;
#else
            add { } // No-op when MAX not available
            remove { } // No-op when MAX not available
#endif
        }

        /// <summary>
        ///     Show privacy options form (UMP consent form) for users to update their consent.
        ///     Call this from your settings screen when PrivacyOptionsRequired is true.
        /// </summary>
        /// <param name="onComplete">Optional callback when form is dismissed</param>
        /// <example>
        ///     // In your settings UI
        ///     if (Palette.PrivacyOptionsRequired)
        ///     {
        ///         privacyButton.onClick.AddListener(() =>
        ///             Palette.ShowPrivacyOptions());
        ///     }
        /// </example>
        public static void ShowPrivacyOptions(Action onComplete = null)
        {
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            MaxAdapter.ShowPrivacyOptions(onComplete);
#else
            Debug.LogWarning($"{Tag} MAX not available - privacy options require MAX SDK.");
            onComplete?.Invoke();
#endif
        }

        /// <summary>
        ///     Refresh consent status from MAX SDK.
        ///     Call this if consent may have changed externally.
        /// </summary>
        public static void RefreshConsentStatus()
        {
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            MaxAdapter.RefreshConsentStatus();
#endif
        }

        #endregion

        /// <summary>Whether a rewarded ad is ready to show</summary>
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
        public static bool IsRewardedAdReady => IsInitialized && MaxAdapter.IsRewardedAdReady;
#else
        public static bool IsRewardedAdReady => false;
#endif

        /// <summary>Whether an interstitial ad is ready to show</summary>
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
        public static bool IsInterstitialAdReady => IsInitialized && MaxAdapter.IsInterstitialAdReady;
#else
        public static bool IsInterstitialAdReady => false;
#endif

        /// <summary>Event fired when SDK initialization completes</summary>
        public static event Action OnInitialized;

        #region Analytics - Custom Events

        /// <summary>
        ///     Track a custom structured event with arbitrary parameters.
        ///     Firebase receives full structured params. GA receives best-effort design event.
        ///     Use GA4 recommended event names where possible (e.g. "post_score", "tutorial_begin").
        /// </summary>
        /// <param name="eventName">GA4-compatible event name (lowercase, underscores, max 40 chars)</param>
        /// <param name="parameters">Structured params. Supported types: string, int, long, float, double, bool, enum.</param>
        public static void TrackEvent(string eventName, Dictionary<string, object> parameters = null)
        {
            if (!EnsureInit()) return;
            if (!ValidateEvent(ref eventName, parameters)) return;

#if FIREBASE_ANALYTICS_INSTALLED
            FirebaseAdapter.TrackEvent(eventName, parameters);
#endif

            // GA best-effort: design event with first numeric value
            GameAnalyticsAdapter.TrackDesignEvent(eventName, ExtractFirstNumericValue(parameters));
        }

        #endregion

        #region Analytics - Progression

        /// <summary>
        ///     Track a progression event (level start, complete, fail).
        ///     Firebase mapping: Start -> level_start, Complete -> level_end, Fail -> level_fail.
        /// </summary>
        /// <param name="extraParams">Optional structured params for Firebase (e.g. world, game_mode, duration_sec).
        ///     Ignored by GameAnalytics. Supported types: string, int, long, float, double, bool, enum.</param>
        public static void TrackProgression(ProgressionStatus status, string progression01,
            string progression02 = null, string progression03 = null, int score = 0,
            Dictionary<string, object> extraParams = null)
        {
            if (!EnsureInit()) return;
            if (extraParams != null && !ValidateParams(extraParams)) return;

#if GAMEANALYTICS_INSTALLED
            GAProgressionStatus gaStatus = ToGA(status);
            GameAnalyticsAdapter.TrackProgressionEvent(gaStatus, progression01, progression02, progression03, score);
#else
            GameAnalyticsAdapter.TrackProgressionEvent(status.ToString().ToLower(), progression01, progression02, progression03, score);
#endif

#if FIREBASE_ANALYTICS_INSTALLED
            FirebaseAdapter.TrackProgressionEvent(status.ToString().ToLower(), progression01, progression02, progression03, score, extraParams);
#endif
        }

        #endregion

        #region Analytics - Design Events

        /// <summary>
        ///     Track a design event (custom analytics).
        /// </summary>
        [System.Obsolete("Use Palette.TrackEvent(eventName, parameters) for structured custom events with Firebase/BigQuery support.")]
        public static void TrackDesign(string eventName, float value = 0)
        {
            if (!EnsureInit()) return;

            GameAnalyticsAdapter.TrackDesignEvent(eventName, value);

#if FIREBASE_ANALYTICS_INSTALLED
            FirebaseAdapter.TrackDesignEvent(eventName, value);
#endif
        }

        #endregion

        #region Analytics - Resource Events

        /// <summary>
        ///     Track a resource event (economy source/sink).
        ///     Firebase mapping: Source -> earn_virtual_currency, Sink -> spend_virtual_currency.
        /// </summary>
        /// <param name="extraParams">Optional structured params for Firebase (e.g. source, level, world).
        ///     Ignored by GameAnalytics. Supported types: string, int, long, float, double, bool, enum.</param>
        public static void TrackResource(ResourceFlowType flowType, string currency, float amount,
            string itemType, string itemId, Dictionary<string, object> extraParams = null)
        {
            if (!EnsureInit()) return;
            if (extraParams != null && !ValidateParams(extraParams)) return;

#if GAMEANALYTICS_INSTALLED
            GAResourceFlowType gaFlow = ToGA(flowType);
            GameAnalyticsAdapter.TrackResourceEvent(gaFlow, currency, amount, itemType, itemId);
#else
            GameAnalyticsAdapter.TrackResourceEvent(flowType.ToString().ToLower(), currency, amount, itemType, itemId);
#endif

#if FIREBASE_ANALYTICS_INSTALLED
            FirebaseAdapter.TrackResourceEvent(flowType.ToString().ToLower(), currency, amount, itemType, itemId, extraParams);
#endif
        }

        #endregion

        #region Analytics - Purchase

        /// <summary>
        ///     Track an in-app purchase. Fans out to Adjust, TikTok, and Firebase Analytics.
        ///     Do not double-log if you rely on automatic store-side collection.
        /// </summary>
        /// <param name="amount">Purchase amount (e.g. 4.99)</param>
        /// <param name="currency">ISO 4217 currency code (default: USD)</param>
        /// <param name="productId">Store product ID for Firebase deduplication</param>
        /// <param name="transactionId">Transaction ID for Firebase deduplication</param>
        public static void TrackPurchase(double amount, string currency = "USD",
            string productId = null, string transactionId = null)
        {
            if (!EnsureInit()) return;

#if SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
            if (!string.IsNullOrEmpty(Config?.adjustPurchaseEventToken))
                AdjustAdapter.TrackRevenue(Config.adjustPurchaseEventToken, amount, currency);
#endif

            if (Config.enableTikTok && !string.IsNullOrEmpty(Config?.tiktokAppId?.Current))
                TikTokAdapter.TrackPurchase(amount, currency);

#if FIREBASE_ANALYTICS_INSTALLED
            FirebaseAdapter.TrackPurchase(productId, amount, currency, transactionId);
#endif
        }

        #endregion

        #region User Identity

        /// <summary>
        ///     Set the user ID for analytics, crash reporting, and attribution.
        ///     Pass null to clear.
        /// </summary>
        public static void SetUserId(string userId)
        {
            if (!EnsureInit()) return;

#if FIREBASE_ANALYTICS_INSTALLED
            FirebaseAdapter.SetUserId(userId);
#endif

#if FIREBASE_CRASHLYTICS_INSTALLED
            FirebaseCrashlyticsAdapter.SetCustomKey("user_id", userId ?? "");
#endif

#if SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
            AdjustAdapter.SetUserId(userId);
#endif
        }

        /// <summary>
        ///     Set a user property for Firebase Analytics segmentation and audience building.
        ///     Register custom properties in Firebase Console > Analytics > User Properties.
        /// </summary>
        public static void SetUserProperty(string name, string value)
        {
            if (!EnsureInit()) return;

#if FIREBASE_ANALYTICS_INSTALLED
            FirebaseAdapter.SetUserProperty(name, value);
#endif
        }

        #endregion

        #region Debug UI

        public static event Action OnShowDebuggerRequested;
        public static event Action OnHideDebuggerRequested;
        public static event Action OnToggleDebuggerRequested;

        /// <summary>Shows the Sorolla debug panel. Requires DebugUI sample imported and prefab in scene.</summary>
        public static void ShowDebugger()
        {
            if (OnShowDebuggerRequested == null)
            {
                Debug.LogWarning($"{Tag} Debug UI not available. Import the DebugUI sample and add the prefab to your scene.");
                return;
            }
            OnShowDebuggerRequested.Invoke();
        }

        /// <summary>Hides the Sorolla debug panel.</summary>
        public static void HideDebugger() => OnHideDebuggerRequested?.Invoke();

        /// <summary>Toggles the Sorolla debug panel visibility.</summary>
        public static void ToggleDebugger()
        {
            if (OnToggleDebuggerRequested == null)
            {
                Debug.LogWarning($"{Tag} Debug UI not available. Import the DebugUI sample and add the prefab to your scene.");
                return;
            }
            OnToggleDebuggerRequested.Invoke();
        }

        #endregion

        #region Attribution

#if SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
        static void InitializeAdjust()
        {
            if (string.IsNullOrEmpty(Config.adjustAppToken))
            {
                Debug.LogError($"{Tag} Adjust App Token not configured.");
                return;
            }

            AdjustEnvironment environment = Config.adjustSandboxMode
                ? AdjustEnvironment.Sandbox
                : AdjustEnvironment.Production;

            Debug.Log($"{Tag} Initializing Adjust ({environment})...");
            AdjustAdapter.Initialize(Config.adjustAppToken, environment);
        }

#endif

        #endregion


        #region Initialization

        /// <summary>
        ///     Initialize Palette SDK. Called automatically by SorollaBootstrapper.
        ///     Do NOT call directly.
        /// </summary>
        public static void Initialize(bool consent)
        {
            if (IsInitialized)
            {
                Debug.LogWarning($"{Tag} Already initialized. Remove any manual Palette.Initialize() call — the SDK auto-initializes via SorollaBootstrapper.");
                return;
            }

            HasConsent = consent;
            Config = Resources.Load<SorollaConfig>("SorollaConfig");

            bool isPrototype = Config == null || Config.isPrototypeMode;
            Debug.Log($"{Tag} Initializing ({(isPrototype ? "Prototype" : "Full")} mode, consent: {consent})...");

            // GameAnalytics (always)
            GameAnalyticsAdapter.Initialize(consent);

            // Facebook (always)
#if SOROLLA_FACEBOOK_ENABLED
            FacebookAdapter.Initialize(consent);
#endif

            // MAX (if available) - Adjust will be initialized in MAX callback
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            InitializeMax();
#elif SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
            // No MAX, initialize Adjust directly (Full mode only)
            if (!isPrototype && Config != null)
                InitializeAdjust();
#endif


            // Firebase modules (always enabled when installed)
#if FIREBASE_ANALYTICS_INSTALLED
            Debug.Log($"{Tag} Initializing Firebase Analytics...");
            FirebaseAdapter.Initialize(consent);
#endif

#if FIREBASE_CRASHLYTICS_INSTALLED
            Debug.Log($"{Tag} Initializing Firebase Crashlytics...");
            FirebaseCrashlyticsAdapter.Initialize(captureUncaughtExceptions: true);
#endif

#if FIREBASE_REMOTE_CONFIG_INSTALLED
            Debug.Log($"{Tag} Initializing Firebase Remote Config...");
            FirebaseRemoteConfigAdapter.Initialize(autoFetch: true);
#endif

            // TikTok (optional — requires enableTikTok + both App IDs)
            if (Config.enableTikTok && !string.IsNullOrEmpty(Config?.tiktokAppId?.Current) && !string.IsNullOrEmpty(Config?.tiktokEmAppId?.Current))
            {
                Debug.Log($"{Tag} Initializing TikTok...");
                TikTokAdapter.Initialize(Config.tiktokEmAppId.Current, Config.tiktokAppId.Current, Config.tiktokAccessToken?.Current ?? "", Config.tiktokDebugMode);
            }

            // When MAX is installed, defer IsInitialized until MAX consent resolves
            // (set in OnMaxSdkInitialized). Without MAX, we're ready now.
#if !(SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED)
            IsInitialized = true;
            OnInitialized?.Invoke();
            Debug.Log($"{Tag} Ready!");
#else
            Debug.Log($"{Tag} Waiting for MAX consent resolution...");
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool EnsureInit()
        {
            if (IsInitialized) return true;
            Debug.LogWarning($"{Tag} Not initialized. Events may be lost.");
            return false;
        }

        #endregion

        #region Remote Config

        /// <summary>
        ///     Check if Remote Config is ready (Firebase if enabled, otherwise GameAnalytics)
        /// </summary>
        public static bool IsRemoteConfigReady()
        {
            if (!IsInitialized) return false;

#if FIREBASE_REMOTE_CONFIG_INSTALLED
            if (FirebaseRemoteConfigAdapter.IsReady)
                return true;
#endif
            return GameAnalyticsAdapter.IsRemoteConfigReady();
        }

        /// <summary>
        ///     Fetch Remote Config values. Fetches from Firebase if installed, GameAnalytics is always ready.
        /// </summary>
        public static void FetchRemoteConfig(Action<bool> onComplete = null)
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            FirebaseRemoteConfigAdapter.FetchAndActivate(onComplete);
#else
            // GameAnalytics RC doesn't need explicit fetch
            onComplete?.Invoke(GameAnalyticsAdapter.IsRemoteConfigReady());
#endif
        }

        /// <summary>
        ///     Get Remote Config string value. Checks Firebase first, then GameAnalytics.
        /// </summary>
        public static string GetRemoteConfig(string key, string defaultValue = "")
        {
            if (!IsInitialized) return defaultValue;

#if FIREBASE_REMOTE_CONFIG_INSTALLED
            if (FirebaseRemoteConfigAdapter.IsReady)
            {
                string value = FirebaseRemoteConfigAdapter.GetString(key, null);
                if (value != null) return value;
            }
#endif
            // Fallback to GameAnalytics
            string gaValue = GameAnalyticsAdapter.GetRemoteConfigValue(key, null);
            return gaValue ?? defaultValue;
        }

        /// <summary>
        ///     Get Remote Config int value. Checks Firebase first, then GameAnalytics.
        /// </summary>
        public static int GetRemoteConfigInt(string key, int defaultValue = 0)
        {
            if (!IsInitialized) return defaultValue;

#if FIREBASE_REMOTE_CONFIG_INSTALLED
            if (FirebaseRemoteConfigAdapter.IsReady)
                return FirebaseRemoteConfigAdapter.GetInt(key, defaultValue);
#endif
            string strValue = GameAnalyticsAdapter.GetRemoteConfigValue(key, null);
            return strValue != null && int.TryParse(strValue, out int r) ? r : defaultValue;
        }

        /// <summary>
        ///     Get Remote Config float value. Checks Firebase first, then GameAnalytics.
        /// </summary>
        public static float GetRemoteConfigFloat(string key, float defaultValue = 0f)
        {
            if (!IsInitialized) return defaultValue;

#if FIREBASE_REMOTE_CONFIG_INSTALLED
            if (FirebaseRemoteConfigAdapter.IsReady)
                return FirebaseRemoteConfigAdapter.GetFloat(key, defaultValue);
#endif
            string strValue = GameAnalyticsAdapter.GetRemoteConfigValue(key, null);
            return strValue != null && float.TryParse(strValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float r)
                ? r : defaultValue;
        }

        /// <summary>
        ///     Get Remote Config bool value. Checks Firebase first, then GameAnalytics.
        /// </summary>
        public static bool GetRemoteConfigBool(string key, bool defaultValue = false)
        {
            if (!IsInitialized) return defaultValue;

#if FIREBASE_REMOTE_CONFIG_INSTALLED
            if (FirebaseRemoteConfigAdapter.IsReady)
                return FirebaseRemoteConfigAdapter.GetBool(key, defaultValue);
#endif
            string strValue = GameAnalyticsAdapter.GetRemoteConfigValue(key, null);
            return strValue != null && bool.TryParse(strValue, out bool r) ? r : defaultValue;
        }

        /// <summary>
        ///     Set in-app defaults for Remote Config. Works before or after initialization.
        ///     Values are used when no fetched or cached value exists.
        /// </summary>
        public static void SetRemoteConfigDefaults(Dictionary<string, object> defaults)
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            FirebaseRemoteConfigAdapter.SetDefaults(defaults);
#endif
        }

        /// <summary>
        ///     When true (default), real-time Remote Config updates are activated immediately.
        ///     Set false for games where mid-session config changes would be jarring.
        ///     Use ActivateRemoteConfigAsync() for manual control when disabled.
        /// </summary>
        public static bool AutoActivateRemoteConfigUpdates
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            get => FirebaseRemoteConfigAdapter.AutoActivateUpdates;
            set => FirebaseRemoteConfigAdapter.AutoActivateUpdates = value;
#else
            get => true;
            set { }
#endif
        }

        /// <summary>
        ///     Manually activate fetched Remote Config values.
        ///     Use when AutoActivateRemoteConfigUpdates is false.
        /// </summary>
        public static System.Threading.Tasks.Task<bool> ActivateRemoteConfigAsync()
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            return FirebaseRemoteConfigAdapter.ActivateAsync();
#else
            return System.Threading.Tasks.Task.FromResult(false);
#endif
        }

        /// <summary>
        ///     Fired when a real-time Remote Config update is received.
        ///     Includes the set of updated keys so games can decide whether to react.
        ///     If AutoActivateRemoteConfigUpdates is true, values are already activated when this fires.
        /// </summary>
        public static event Action<IReadOnlyCollection<string>> OnRemoteConfigUpdated
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            add => FirebaseRemoteConfigAdapter.OnConfigUpdated += value;
            remove => FirebaseRemoteConfigAdapter.OnConfigUpdated -= value;
#else
            add { }
            remove { }
#endif
        }

        #endregion

        #region Error Logging

        /// <summary>Log an exception to crash reporting services (Firebase Crashlytics)</summary>
        public static void LogException(Exception exception)
        {
#if FIREBASE_CRASHLYTICS_INSTALLED
            if (!EnsureInit()) return;
            FirebaseCrashlyticsAdapter.LogException(exception);
#endif
        }

        /// <summary>Log a message to crash reporting services (Firebase Crashlytics)</summary>
        public static void LogCrashlytics(string message)
        {
#if FIREBASE_CRASHLYTICS_INSTALLED
            if (!EnsureInit()) return;
            FirebaseCrashlyticsAdapter.Log(message);
#endif
        }

        /// <summary>Set a custom key for crash reports (Firebase Crashlytics)</summary>
        public static void SetCrashlyticsKey(string key, string value)
        {
#if FIREBASE_CRASHLYTICS_INSTALLED
            if (!EnsureInit()) return;
            FirebaseCrashlyticsAdapter.SetCustomKey(key, value);
#endif
        }

        #endregion

        #region Ads

#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
        static void InitializeMax()
        {
            if (Config == null)
            {
                Debug.LogWarning($"{Tag} SorollaConfig not found.");
                return;
            }

            Debug.Log($"{Tag} Initializing AppLovin MAX...");

            // Subscribe to ad loading state changes for loading overlay
            MaxAdapter.OnAdLoadingStateChanged += OnMaxAdLoadingStateChanged;

            // Subscribe to SDK initialized event to init Adjust (per MAX docs)
            MaxAdapter.OnSdkInitialized += OnMaxSdkInitialized;

            // Subscribe to consent status changes from MAX CMP (UMP) to propagate to other adapters
            MaxAdapter.OnConsentStatusChanged += OnMaxConsentChanged;

            // SDK key is read from AppLovinSettings (configured in Integration Manager)
            MaxAdapter.Initialize(
                Config.rewardedAdUnit.Current,
                Config.interstitialAdUnit.Current,
                Config.bannerAdUnit.Current,
                HasConsent);
        }

        static void OnMaxSdkInitialized()
        {
            // MAX CMP has resolved. Propagate real consent to SDKs that started with false.
            bool consent = MaxAdapter.ConsentStatus == Adapters.ConsentStatus.Obtained
                        || MaxAdapter.ConsentStatus == Adapters.ConsentStatus.NotApplicable;
            HasConsent = consent;
            Debug.Log($"{Tag} MAX consent resolved: {MaxAdapter.ConsentStatus} (consent={consent})");

            GameAnalyticsAdapter.UpdateConsent(consent);
#if FIREBASE_ANALYTICS_INSTALLED
            FirebaseAdapter.UpdateConsent(consent);
#endif

            // Per MAX SDK docs: Initialize other SDKs (like Adjust) INSIDE the MAX callback
            // to ensure proper consent flow handling
#if SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
            bool isPrototype = Config == null || Config.isPrototypeMode;
            if (!isPrototype && Config != null)
            {
                InitializeAdjust();
            }
#endif

            IsInitialized = true;
            OnInitialized?.Invoke();
            Debug.Log($"{Tag} Ready!");
        }

        static void OnMaxConsentChanged(Adapters.ConsentStatus status)
        {
            bool consent = status == Adapters.ConsentStatus.Obtained || status == Adapters.ConsentStatus.NotApplicable;
            if (HasConsent == consent) return;

            HasConsent = consent;
            Debug.Log($"{Tag} Consent updated by MAX CMP: {status} → propagating to adapters");
            GameAnalyticsAdapter.UpdateConsent(consent);
#if FIREBASE_ANALYTICS_INSTALLED
            FirebaseAdapter.UpdateConsent(consent);
#endif
        }


        static void OnMaxAdLoadingStateChanged(Adapters.AdType adType, bool isLoading)
        {
            if (isLoading)
                SorollaLoadingOverlay.Show($"Loading {adType} ad...");
            else
                SorollaLoadingOverlay.Hide();
        }


#endif

        /// <summary>Show rewarded ad</summary>
        public static void ShowRewardedAd(Action onComplete, Action onFailed)
        {
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            MaxAdapter.ShowRewardedAd(onComplete, onFailed);
#else
            Debug.LogWarning($"{Tag} MAX not available.");
            onFailed?.Invoke();
#endif
        }

        /// <summary>Show interstitial ad</summary>
        public static void ShowInterstitialAd(Action onComplete)
        {
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            MaxAdapter.ShowInterstitialAd(onComplete);
#else
            Debug.LogWarning($"{Tag} MAX not available.");
            onComplete?.Invoke();
#endif
        }

        #endregion

        #region Internal

#if GAMEANALYTICS_INSTALLED
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static GAProgressionStatus ToGA(ProgressionStatus s) => s switch
        {
            ProgressionStatus.Start => GAProgressionStatus.Start,
            ProgressionStatus.Complete => GAProgressionStatus.Complete,
            ProgressionStatus.Fail => GAProgressionStatus.Fail,
            _ => GAProgressionStatus.Start,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static GAResourceFlowType ToGA(ResourceFlowType f) => f switch
        {
            ResourceFlowType.Source => GAResourceFlowType.Source,
            ResourceFlowType.Sink => GAResourceFlowType.Sink,
            _ => GAResourceFlowType.Source,
        };
#endif

        const int MaxEventNameLength = 40;
        const int MaxParamNameLength = 40;
        const int MaxParamsPerEvent = 25;

        static readonly string[] s_reservedPrefixes = { "firebase_", "google_", "ga_" };

        /// <summary>
        ///     Validate and sanitize an event name and its parameters.
        ///     Returns false if the event should be rejected entirely.
        /// </summary>
        static bool ValidateEvent(ref string eventName, Dictionary<string, object> parameters)
        {
            string originalName = eventName;
            eventName = SanitizeEventName(eventName);
            if (eventName == null)
            {
                Debug.LogError($"{Tag} Event rejected: '{originalName}' is empty or invalid after sanitization. Use lowercase letters, digits, and underscores (max {MaxEventNameLength} chars).");
                return false;
            }

            foreach (var prefix in s_reservedPrefixes)
            {
                if (eventName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string suggested = eventName.Substring(prefix.Length);
                    Debug.LogError($"{Tag} Event rejected: '{eventName}' uses reserved prefix '{prefix}'. Remove the prefix (e.g. '{suggested}') or use a different name.");
                    return false;
                }
            }

            if (parameters != null && !ValidateParams(parameters))
                return false;

            return true;
        }

        /// <summary>
        ///     Validate parameter names, types, and count.
        ///     Returns false if the event should be rejected entirely.
        /// </summary>
        static bool ValidateParams(Dictionary<string, object> parameters)
        {
            if (parameters.Count > MaxParamsPerEvent)
            {
                Debug.LogError($"{Tag} Event rejected: {parameters.Count} params exceeds max {MaxParamsPerEvent}. Remove {parameters.Count - MaxParamsPerEvent} param(s).");
                return false;
            }

            foreach (var kvp in parameters)
            {
                var sanitizedKey = SanitizeParameterName(kvp.Key);
                if (sanitizedKey == null)
                {
                    Debug.LogError($"{Tag} Event rejected: param name '{kvp.Key}' is invalid after sanitization. Use lowercase letters, digits, and underscores (max {MaxParamNameLength} chars).");
                    return false;
                }

                if (!IsSupportedParamType(kvp.Value))
                {
                    string typeName = kvp.Value?.GetType().Name ?? "null";
                    Debug.LogError($"{Tag} Event rejected: param '{kvp.Key}' has unsupported type '{typeName}'. Convert to one of: string, int, long, float, double, bool, enum.");
                    return false;
                }
            }

            return true;
        }

        static string SanitizeEventName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var sanitized = name.Replace(":", "_").Replace("-", "_").Replace(" ", "_").Replace(".", "_");

            var result = new System.Text.StringBuilder();
            foreach (var c in sanitized)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    result.Append(c);
            }

            if (result.Length > 0 && !char.IsLetter(result[0]))
                result.Insert(0, 'e');

            if (result.Length > MaxEventNameLength)
                return result.ToString(0, MaxEventNameLength);

            return result.Length > 0 ? result.ToString() : null;
        }

        static string SanitizeParameterName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var sanitized = name.Replace(":", "_").Replace("-", "_").Replace(" ", "_").Replace(".", "_");

            var result = new System.Text.StringBuilder();
            foreach (var c in sanitized)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    result.Append(c);
            }

            if (result.Length > MaxParamNameLength)
                return result.ToString(0, MaxParamNameLength);

            return result.Length > 0 ? result.ToString() : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsSupportedParamType(object value) =>
            value is string or int or long or float or double or bool or Enum;

        /// <summary>
        ///     Extract the first numeric value from parameters for GA best-effort design event.
        /// </summary>
        static float ExtractFirstNumericValue(Dictionary<string, object> parameters)
        {
            if (parameters == null) return 0f;
            foreach (var kvp in parameters)
            {
                switch (kvp.Value)
                {
                    case int i: return i;
                    case long l: return l;
                    case float f: return f;
                    case double d: return (float)d;
                }
            }
            return 0f;
        }

        #endregion
    }
}
