using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using UnityEngine;
#if SOROLLA_MAX_ENABLED || SOROLLA_ADJUST_ENABLED || SOROLLA_FACEBOOK_ENABLED || FIREBASE_ANALYTICS_INSTALLED || FIREBASE_CRASHLYTICS_INSTALLED || FIREBASE_REMOTE_CONFIG_INSTALLED
using Sorolla.Adapters;
#endif
#if GAMEANALYTICS_INSTALLED
using GameAnalyticsSDK;
#endif

namespace Sorolla
{
    /// <summary>
    ///     Progression status for tracking level/stage events.
    /// </summary>
    public enum ProgressionStatus
    {
        Start,
        Complete,
        Fail
    }

    /// <summary>
    ///     Resource flow type for tracking economy events.
    /// </summary>
    public enum ResourceFlowType
    {
        /// <summary>Player gained resources</summary>
        Source,
        /// <summary>Player spent resources</summary>
        Sink
    }

    /// <summary>
    ///     Main API for Sorolla SDK.
    ///     Provides unified interface for analytics, ads, and attribution.
    ///     Auto-initialized - no manual setup required.
    /// </summary>
    public static class Sorolla
    {
        private const string Tag = "[Sorolla]";

        private static SorollaConfig s_config;
        private static bool s_consent;

        /// <summary>Whether the SDK is initialized</summary>
        public static bool IsInitialized { get; private set; }

        /// <summary>Current user consent status</summary>
        public static bool HasConsent => s_consent;

        /// <summary>Current configuration (may be null)</summary>
        public static SorollaConfig Config => s_config;

        #region Initialization

        /// <summary>
        ///     Initialize Sorolla SDK. Called automatically by SorollaBootstrapper.
        ///     Do NOT call directly.
        /// </summary>
        public static void Initialize(bool consent)
        {
            if (IsInitialized)
            {
                Debug.LogWarning($"{Tag} Already initialized.");
                return;
            }

            s_consent = consent;
            s_config = Resources.Load<SorollaConfig>("SorollaConfig");

            var isPrototype = s_config == null || s_config.isPrototypeMode;
            Debug.Log($"{Tag} Initializing ({(isPrototype ? "Prototype" : "Full")} mode, consent: {consent})...");

            // GameAnalytics (always)
            GameAnalyticsAdapter.Initialize();

            // Facebook (Prototype mode)
#if SOROLLA_FACEBOOK_ENABLED
            if (isPrototype)
                FacebookAdapter.Initialize(consent);
#endif

            // MAX (if available)
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            InitializeMax();
#endif

            // Adjust (Full mode)
#if SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
            if (!isPrototype && s_config != null)
                InitializeAdjust();
#endif

            // Firebase Analytics (optional)
#if FIREBASE_ANALYTICS_INSTALLED
            if (s_config != null && s_config.enableFirebaseAnalytics)
                FirebaseAdapter.Initialize();
#endif

            // Firebase Crashlytics (optional)
#if FIREBASE_CRASHLYTICS_INSTALLED
            if (s_config != null && s_config.enableCrashlytics)
                FirebaseCrashlyticsAdapter.Initialize(captureUncaughtExceptions: true);
#endif

            // Firebase Remote Config (optional)
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            if (s_config != null && s_config.enableRemoteConfig)
                FirebaseRemoteConfigAdapter.Initialize(autoFetch: true);
#endif

            IsInitialized = true;
            Debug.Log($"{Tag} Ready!");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool EnsureInit()
        {
            if (IsInitialized) return true;
            Debug.LogWarning($"{Tag} Not initialized. Events may be lost.");
            return false;
        }

        #endregion

        #region Analytics - Progression

        /// <summary>
        ///     Track a progression event (level start, complete, fail).
        /// </summary>
        public static void TrackProgression(ProgressionStatus status, string progression01,
            string progression02 = null, string progression03 = null, int score = 0)
        {
            if (!EnsureInit()) return;

#if GAMEANALYTICS_INSTALLED
            var gaStatus = ToGA(status);
            GameAnalyticsAdapter.TrackProgressionEvent(gaStatus, progression01, progression02, progression03, score);
#else
            GameAnalyticsAdapter.TrackProgressionEvent(status.ToString().ToLower(), progression01, progression02, progression03, score);
#endif

#if FIREBASE_ANALYTICS_INSTALLED
            if (s_config != null && s_config.enableFirebaseAnalytics)
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
            if (s_config != null && s_config.enableFirebaseAnalytics)
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
            var gaFlow = ToGA(flowType);
            GameAnalyticsAdapter.TrackResourceEvent(gaFlow, currency, amount, itemType, itemId);
#else
            GameAnalyticsAdapter.TrackResourceEvent(flowType.ToString().ToLower(), currency, amount, itemType, itemId);
#endif

#if FIREBASE_ANALYTICS_INSTALLED
            if (s_config != null && s_config.enableFirebaseAnalytics)
                FirebaseAdapter.TrackResourceEvent(flowType.ToString().ToLower(), currency, amount, itemType, itemId);
#endif
        }

        #endregion

        #region Remote Config

        /// <summary>Check if remote config is ready</summary>
        public static bool IsRemoteConfigReady() => IsInitialized && GameAnalyticsAdapter.IsRemoteConfigReady();

        /// <summary>Get remote config string value</summary>
        public static string GetRemoteConfig(string key, string defaultValue = null) =>
            IsInitialized ? GameAnalyticsAdapter.GetRemoteConfigValue(key, defaultValue ?? "") : defaultValue;

        /// <summary>Get remote config int value</summary>
        public static int GetRemoteConfigInt(string key, int defaultValue = 0) =>
            int.TryParse(GetRemoteConfig(key, defaultValue.ToString()), out var r) ? r : defaultValue;

        /// <summary>Get remote config float value</summary>
        public static float GetRemoteConfigFloat(string key, float defaultValue = 0f) =>
            float.TryParse(GetRemoteConfig(key, defaultValue.ToString(CultureInfo.InvariantCulture)),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : defaultValue;

        /// <summary>Get remote config bool value</summary>
        public static bool GetRemoteConfigBool(string key, bool defaultValue = false) =>
            bool.TryParse(GetRemoteConfig(key, defaultValue.ToString()), out var r) ? r : defaultValue;

        #endregion

        #region Firebase Remote Config

        /// <summary>Check if Firebase Remote Config is ready</summary>
        public static bool IsFirebaseRemoteConfigReady()
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            return IsInitialized && FirebaseRemoteConfigAdapter.IsReady;
#else
            return false;
#endif
        }

        /// <summary>Fetch Firebase Remote Config values</summary>
        public static void FetchFirebaseRemoteConfig(Action<bool> onComplete = null)
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            if (!EnsureInit()) { onComplete?.Invoke(false); return; }
            FirebaseRemoteConfigAdapter.FetchAndActivate(onComplete);
#else
            Debug.LogWarning($"{Tag} Firebase Remote Config not available.");
            onComplete?.Invoke(false);
#endif
        }

        /// <summary>Get Firebase Remote Config string value</summary>
        public static string GetFirebaseRemoteConfig(string key, string defaultValue = "")
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            return IsInitialized ? FirebaseRemoteConfigAdapter.GetString(key, defaultValue) : defaultValue;
#else
            return defaultValue;
#endif
        }

        /// <summary>Get Firebase Remote Config int value</summary>
        public static int GetFirebaseRemoteConfigInt(string key, int defaultValue = 0)
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            return IsInitialized ? FirebaseRemoteConfigAdapter.GetInt(key, defaultValue) : defaultValue;
#else
            return defaultValue;
#endif
        }

        /// <summary>Get Firebase Remote Config float value</summary>
        public static float GetFirebaseRemoteConfigFloat(string key, float defaultValue = 0f)
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            return IsInitialized ? FirebaseRemoteConfigAdapter.GetFloat(key, defaultValue) : defaultValue;
#else
            return defaultValue;
#endif
        }

        /// <summary>Get Firebase Remote Config bool value</summary>
        public static bool GetFirebaseRemoteConfigBool(string key, bool defaultValue = false)
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            return IsInitialized ? FirebaseRemoteConfigAdapter.GetBool(key, defaultValue) : defaultValue;
#else
            return defaultValue;
#endif
        }

        #endregion

        #region Error Logging

        /// <summary>Log an exception to crash reporting services (Firebase Crashlytics)</summary>
        public static void LogException(Exception exception)
        {
#if FIREBASE_CRASHLYTICS_INSTALLED
            if (!EnsureInit()) return;
            if (s_config != null && s_config.enableCrashlytics)
                FirebaseCrashlyticsAdapter.LogException(exception);
#endif
        }

        /// <summary>Log a message to crash reporting services (Firebase Crashlytics)</summary>
        public static void LogCrashlytics(string message)
        {
#if FIREBASE_CRASHLYTICS_INSTALLED
            if (!EnsureInit()) return;
            if (s_config != null && s_config.enableCrashlytics)
                FirebaseCrashlyticsAdapter.Log(message);
#endif
        }

        /// <summary>Set a custom key for crash reports (Firebase Crashlytics)</summary>
        public static void SetCrashlyticsKey(string key, string value)
        {
#if FIREBASE_CRASHLYTICS_INSTALLED
            if (!EnsureInit()) return;
            if (s_config != null && s_config.enableCrashlytics)
                FirebaseCrashlyticsAdapter.SetCustomKey(key, value);
#endif
        }

        #endregion

        #region Ads

#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
        private static void InitializeMax()
        {
            if (s_config == null || string.IsNullOrEmpty(s_config.maxSdkKey))
            {
                Debug.LogWarning($"{Tag} MAX SDK Key not configured.");
                return;
            }

            Debug.Log($"{Tag} Initializing AppLovin MAX...");
            MaxAdapter.Initialize(s_config.maxSdkKey, s_config.maxRewardedAdUnitId,
                s_config.maxInterstitialAdUnitId, s_config.maxBannerAdUnitId);
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

        #region Attribution

#if SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
        private static void InitializeAdjust()
        {
            if (string.IsNullOrEmpty(s_config.adjustAppToken))
            {
                Debug.LogError($"{Tag} Adjust App Token not configured.");
                return;
            }

            Debug.Log($"{Tag} Initializing Adjust...");
            AdjustAdapter.Initialize(s_config.adjustAppToken, AdjustEnvironment.Production);
        }
#endif

        #endregion

        #region Internal

#if GAMEANALYTICS_INSTALLED
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static GAProgressionStatus ToGA(ProgressionStatus s) => s switch
        {
            ProgressionStatus.Start => GAProgressionStatus.Start,
            ProgressionStatus.Complete => GAProgressionStatus.Complete,
            ProgressionStatus.Fail => GAProgressionStatus.Fail,
            _ => GAProgressionStatus.Start
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static GAResourceFlowType ToGA(ResourceFlowType f) => f switch
        {
            ResourceFlowType.Source => GAResourceFlowType.Source,
            ResourceFlowType.Sink => GAResourceFlowType.Sink,
            _ => GAResourceFlowType.Source
        };
#endif

        #endregion
    }
}

