using System;
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

        #region Analytics - Progression

        /// <summary>
        ///     Track a progression event (level start, complete, fail).
        /// </summary>
        public static void TrackProgression(ProgressionStatus status, string progression01,
            string progression02 = null, string progression03 = null, int score = 0)
        {
            if (!EnsureInit()) return;

#if GAMEANALYTICS_INSTALLED
            GAProgressionStatus gaStatus = ToGA(status);
            GameAnalyticsAdapter.TrackProgressionEvent(gaStatus, progression01, progression02, progression03, score);
#else
            GameAnalyticsAdapter.TrackProgressionEvent(status.ToString().ToLower(), progression01, progression02, progression03, score);
#endif

#if FIREBASE_ANALYTICS_INSTALLED
            if (Config != null && Config.enableFirebaseAnalytics)
                FirebaseAdapter.TrackProgressionEvent(status.ToString().ToLower(), progression01, progression02, progression03, score);
#endif
        }

        #endregion

        #region Analytics - Design Events

        /// <summary>
        ///     Track a design event (custom analytics).
        /// </summary>
        public static void TrackDesign(string eventName, float value = 0)
        {
            if (!EnsureInit()) return;

            GameAnalyticsAdapter.TrackDesignEvent(eventName, value);

#if SOROLLA_FACEBOOK_ENABLED
            var isPrototype = s_config == null || s_config.isPrototypeMode;
            if (isPrototype)
                FacebookAdapter.TrackEvent(eventName, value);
#endif

#if FIREBASE_ANALYTICS_INSTALLED
            if (Config != null && Config.enableFirebaseAnalytics)
                FirebaseAdapter.TrackDesignEvent(eventName, value);
#endif
        }

        #endregion

        #region Analytics - Resource Events

        /// <summary>
        ///     Track a resource event (economy source/sink).
        /// </summary>
        public static void TrackResource(ResourceFlowType flowType, string currency, float amount,
            string itemType, string itemId)
        {
            if (!EnsureInit()) return;

#if GAMEANALYTICS_INSTALLED
            GAResourceFlowType gaFlow = ToGA(flowType);
            GameAnalyticsAdapter.TrackResourceEvent(gaFlow, currency, amount, itemType, itemId);
#else
            GameAnalyticsAdapter.TrackResourceEvent(flowType.ToString().ToLower(), currency, amount, itemType, itemId);
#endif

#if FIREBASE_ANALYTICS_INSTALLED
            if (Config != null && Config.enableFirebaseAnalytics)
                FirebaseAdapter.TrackResourceEvent(flowType.ToString().ToLower(), currency, amount, itemType, itemId);
#endif
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
                Debug.LogWarning($"{Tag} Already initialized.");
                return;
            }

            HasConsent = consent;
            Config = Resources.Load<SorollaConfig>("SorollaConfig");

            bool isPrototype = Config == null || Config.isPrototypeMode;
            Debug.Log($"{Tag} Initializing ({(isPrototype ? "Prototype" : "Full")} mode, consent: {consent})...");

            // GameAnalytics (always)
            GameAnalyticsAdapter.Initialize();

            // Facebook (Prototype mode)
#if SOROLLA_FACEBOOK_ENABLED
            if (isPrototype)
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


            // Firebase Analytics (optional)
#if FIREBASE_ANALYTICS_INSTALLED
            if (Config != null && Config.enableFirebaseAnalytics)
            {
                Debug.Log($"{Tag} Initializing Firebase Analytics...");
                FirebaseAdapter.Initialize();
            }
            else
            {
                Debug.Log($"{Tag} Firebase Analytics disabled (config: {Config != null}, enabled: {Config?.enableFirebaseAnalytics})");
            }
#endif

            // Firebase Crashlytics (optional)
#if FIREBASE_CRASHLYTICS_INSTALLED
            if (Config != null && Config.enableCrashlytics)
            {
                Debug.Log($"{Tag} Initializing Firebase Crashlytics...");
                FirebaseCrashlyticsAdapter.Initialize(captureUncaughtExceptions: true);
            }
            else
            {
                Debug.Log($"{Tag} Firebase Crashlytics disabled (config: {Config != null}, enabled: {Config?.enableCrashlytics})");
            }
#endif

            // Firebase Remote Config (optional)
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            if (Config != null && Config.enableRemoteConfig)
            {
                Debug.Log($"{Tag} Initializing Firebase Remote Config...");
                FirebaseRemoteConfigAdapter.Initialize(autoFetch: true);
            }
            else
            {
                Debug.Log($"{Tag} Firebase Remote Config disabled (config: {Config != null}, enabled: {Config?.enableRemoteConfig})");
            }
#endif

            IsInitialized = true;
            OnInitialized?.Invoke();
            Debug.Log($"{Tag} Ready!");
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
            if (Config != null && Config.enableRemoteConfig && FirebaseRemoteConfigAdapter.IsReady)
                return true;
#endif
            return GameAnalyticsAdapter.IsRemoteConfigReady();
        }

        /// <summary>
        ///     Fetch Remote Config values. Fetches from Firebase if enabled, GameAnalytics is always ready.
        /// </summary>
        public static void FetchRemoteConfig(Action<bool> onComplete = null)
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            if (Config != null && Config.enableRemoteConfig)
            {
                FirebaseRemoteConfigAdapter.FetchAndActivate(onComplete);
                return;
            }
#endif
            // GameAnalytics RC doesn't need explicit fetch
            onComplete?.Invoke(GameAnalyticsAdapter.IsRemoteConfigReady());
        }

        /// <summary>
        ///     Get Remote Config string value. Checks Firebase first (if enabled), then GameAnalytics.
        /// </summary>
        public static string GetRemoteConfig(string key, string defaultValue = "")
        {
            if (!IsInitialized) return defaultValue;

#if FIREBASE_REMOTE_CONFIG_INSTALLED
            if (Config != null && Config.enableRemoteConfig && FirebaseRemoteConfigAdapter.IsReady)
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
        ///     Get Remote Config int value. Checks Firebase first (if enabled), then GameAnalytics.
        /// </summary>
        public static int GetRemoteConfigInt(string key, int defaultValue = 0)
        {
            if (!IsInitialized) return defaultValue;

#if FIREBASE_REMOTE_CONFIG_INSTALLED
            if (Config != null && Config.enableRemoteConfig && FirebaseRemoteConfigAdapter.IsReady)
                return FirebaseRemoteConfigAdapter.GetInt(key, defaultValue);
#endif
            string strValue = GameAnalyticsAdapter.GetRemoteConfigValue(key, null);
            return strValue != null && int.TryParse(strValue, out int r) ? r : defaultValue;
        }

        /// <summary>
        ///     Get Remote Config float value. Checks Firebase first (if enabled), then GameAnalytics.
        /// </summary>
        public static float GetRemoteConfigFloat(string key, float defaultValue = 0f)
        {
            if (!IsInitialized) return defaultValue;

#if FIREBASE_REMOTE_CONFIG_INSTALLED
            if (Config != null && Config.enableRemoteConfig && FirebaseRemoteConfigAdapter.IsReady)
                return FirebaseRemoteConfigAdapter.GetFloat(key, defaultValue);
#endif
            string strValue = GameAnalyticsAdapter.GetRemoteConfigValue(key, null);
            return strValue != null && float.TryParse(strValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float r)
                ? r : defaultValue;
        }

        /// <summary>
        ///     Get Remote Config bool value. Checks Firebase first (if enabled), then GameAnalytics.
        /// </summary>
        public static bool GetRemoteConfigBool(string key, bool defaultValue = false)
        {
            if (!IsInitialized) return defaultValue;

#if FIREBASE_REMOTE_CONFIG_INSTALLED
            if (Config != null && Config.enableRemoteConfig && FirebaseRemoteConfigAdapter.IsReady)
                return FirebaseRemoteConfigAdapter.GetBool(key, defaultValue);
#endif
            string strValue = GameAnalyticsAdapter.GetRemoteConfigValue(key, null);
            return strValue != null && bool.TryParse(strValue, out bool r) ? r : defaultValue;
        }

        #endregion

        #region Error Logging

        /// <summary>Log an exception to crash reporting services (Firebase Crashlytics)</summary>
        public static void LogException(Exception exception)
        {
#if FIREBASE_CRASHLYTICS_INSTALLED
            if (!EnsureInit()) return;
            if (Config != null && Config.enableCrashlytics)
                FirebaseCrashlyticsAdapter.LogException(exception);
#endif
        }

        /// <summary>Log a message to crash reporting services (Firebase Crashlytics)</summary>
        public static void LogCrashlytics(string message)
        {
#if FIREBASE_CRASHLYTICS_INSTALLED
            if (!EnsureInit()) return;
            if (Config != null && Config.enableCrashlytics)
                FirebaseCrashlyticsAdapter.Log(message);
#endif
        }

        /// <summary>Set a custom key for crash reports (Firebase Crashlytics)</summary>
        public static void SetCrashlyticsKey(string key, string value)
        {
#if FIREBASE_CRASHLYTICS_INSTALLED
            if (!EnsureInit()) return;
            if (Config != null && Config.enableCrashlytics)
                FirebaseCrashlyticsAdapter.SetCustomKey(key, value);
#endif
        }

        #endregion

        #region Ads

#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
        static void InitializeMax()
        {
            if (Config == null || string.IsNullOrEmpty(Config.maxSdkKey))
            {
                Debug.LogWarning($"{Tag} MAX SDK Key not configured.");
                return;
            }

            Debug.Log($"{Tag} Initializing AppLovin MAX...");

            // Subscribe to ad loading state changes for loading overlay
            MaxAdapter.OnAdLoadingStateChanged += OnMaxAdLoadingStateChanged;

            // Subscribe to SDK initialized event to init Adjust (per MAX docs)
            MaxAdapter.OnSdkInitialized += OnMaxSdkInitialized;

            MaxAdapter.Initialize(Config.maxSdkKey, Config.maxRewardedAdUnitId,
                Config.maxInterstitialAdUnitId, Config.maxBannerAdUnitId, HasConsent);
        }

        static void OnMaxSdkInitialized()
        {
            // Per MAX SDK docs: Initialize other SDKs (like Adjust) INSIDE the MAX callback
            // to ensure proper consent flow handling
#if SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
            bool isPrototype = Config == null || Config.isPrototypeMode;
            if (!isPrototype && Config != null)
            {
                InitializeAdjust();
            }
#endif
        }


        static void OnMaxAdLoadingStateChanged(AdType adType, bool isLoading)
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

        #endregion
    }
}
