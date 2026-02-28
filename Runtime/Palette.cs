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
    [Serializable]
    struct IapReceipt
    {
        public string Store;
        public string TransactionID;
        public string Payload;
    }

    [Serializable]
    struct GooglePlayPayload
    {
        public string json;
        public string signature;
    }

    [Serializable]
    struct GooglePlayReceiptData
    {
        public string purchaseToken;
    }

    struct ParsedReceipt
    {
        public string Store;
        // Android
        public string GoogleJson;
        public string GoogleSignature;
        public string GooglePurchaseToken;
        // iOS
        public string IosReceipt;
    }

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
            FacebookAdapter.TrackEvent(eventName, value);
#endif

#if FIREBASE_ANALYTICS_INSTALLED
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
            FirebaseAdapter.TrackResourceEvent(flowType.ToString().ToLower(), currency, amount, itemType, itemId);
#endif
        }

        #endregion

        #region Analytics - Purchase Events

        /// <summary>
        ///     Track a successful IAP purchase across all installed SDKs.
        ///     Call from your Unity IAP ProcessPurchase/OnPurchasePending callback.
        /// </summary>
        /// <param name="productId">IAP product ID ("gems_500")</param>
        /// <param name="price">Localized price (4.99)</param>
        /// <param name="currency">ISO 4217 currency code ("USD")</param>
        /// <param name="transactionId">Unity IAP transaction ID</param>
        /// <param name="receipt">Raw Unity IAP receipt JSON (enables server-side validation). Null is OK.</param>
        /// <param name="itemType">GA business event category</param>
        /// <param name="cartType">GA cart type ("main_shop", "end_of_level")</param>
        public static void TrackPurchase(
            string productId,
            double price,
            string currency,
            string transactionId,
            string receipt = null,
            string itemType = "iap",
            string cartType = "default")
        {
            if (!EnsureInit()) return;

            currency = string.IsNullOrEmpty(currency) ? "USD" : currency;
            int amountInCents = (int)Math.Round(price * 100);

            ParsedReceipt? parsed = ParseReceipt(receipt);

            // 1. GameAnalytics — business event with platform-specific receipt validation
#if GAMEANALYTICS_INSTALLED
            if (parsed.HasValue && parsed.Value.Store == "GooglePlay" && !string.IsNullOrEmpty(parsed.Value.GoogleJson))
            {
                GameAnalyticsAdapter.TrackBusinessEventGooglePlay(currency, amountInCents, itemType, productId, cartType,
                    parsed.Value.GoogleJson, parsed.Value.GoogleSignature);
            }
            else if (parsed.HasValue && parsed.Value.Store == "AppleAppStore" && !string.IsNullOrEmpty(parsed.Value.IosReceipt))
            {
                GameAnalyticsAdapter.TrackBusinessEventIOS(currency, amountInCents, itemType, productId, cartType,
                    parsed.Value.IosReceipt);
            }
            else
            {
                GameAnalyticsAdapter.TrackBusinessEvent(currency, amountInCents, itemType, productId, cartType);
            }
#endif

            // 2. Facebook — purchase event with content enrichment
#if SOROLLA_FACEBOOK_ENABLED
            var fbParams = new Dictionary<string, object>
            {
                { "fb_content_id", productId },
                { "fb_content_type", "product" },
            };
            if (!string.IsNullOrEmpty(transactionId))
                fbParams["transaction_id"] = transactionId;
            FacebookAdapter.TrackPurchase((float)price, currency, fbParams);
#endif

            // 3. Firebase — GA4 purchase event
#if FIREBASE_ANALYTICS_INSTALLED
            FirebaseAdapter.TrackPurchase(productId, price, currency, transactionId);
#endif

            // 4. Adjust — purchase verification (if event token configured)
#if SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
            if (Config != null && !string.IsNullOrEmpty(Config.adjustPurchaseEventToken))
            {
                string deduplicationId = transactionId ?? Guid.NewGuid().ToString();
#if UNITY_IOS
                if (parsed.HasValue && !string.IsNullOrEmpty(parsed.Value.Store))
                {
                    AdjustAdapter.TrackPurchaseIOS(Config.adjustPurchaseEventToken, price, currency,
                        productId, transactionId, deduplicationId);
                }
                else
                {
                    AdjustAdapter.TrackPurchaseSimple(Config.adjustPurchaseEventToken, price, currency, deduplicationId);
                }
#elif UNITY_ANDROID
                if (parsed.HasValue && !string.IsNullOrEmpty(parsed.Value.GooglePurchaseToken))
                {
                    AdjustAdapter.TrackPurchaseAndroid(Config.adjustPurchaseEventToken, price, currency,
                        productId, parsed.Value.GooglePurchaseToken, deduplicationId);
                }
                else
                {
                    AdjustAdapter.TrackPurchaseSimple(Config.adjustPurchaseEventToken, price, currency, deduplicationId);
                }
#else
                AdjustAdapter.TrackPurchaseSimple(Config.adjustPurchaseEventToken, price, currency, deduplicationId);
#endif
            }
#endif

            // 5. TikTok — purchase event (runtime guard, no compile-time dependency)
            TikTokAdapter.TrackPurchase(price, currency);

            Debug.Log($"{Tag} TrackPurchase: {productId} {price} {currency} (txn: {transactionId})");
        }

        /// <summary>
        ///     Track a failed IAP purchase for funnel analysis.
        /// </summary>
        /// <param name="productId">IAP product ID that failed</param>
        /// <param name="failureReason">Reason for failure ("user_cancelled", "network_error", etc.)</param>
        public static void TrackPurchaseFailed(string productId, string failureReason)
        {
            if (!EnsureInit()) return;

            string eventId = $"purchase_failed:{productId}:{failureReason}";

#if GAMEANALYTICS_INSTALLED
            GameAnalyticsAdapter.TrackDesignEvent(eventId);
#endif

#if FIREBASE_ANALYTICS_INSTALLED
            FirebaseAdapter.TrackDesignEvent($"purchase_failed_{productId}", 0);
#endif

            Debug.Log($"{Tag} TrackPurchaseFailed: {productId} reason={failureReason}");
        }

        static ParsedReceipt? ParseReceipt(string receipt)
        {
            if (string.IsNullOrEmpty(receipt)) return null;

            try
            {
                var outer = JsonUtility.FromJson<IapReceipt>(receipt);
                var result = new ParsedReceipt { Store = outer.Store };

                if (outer.Store == "GooglePlay" && !string.IsNullOrEmpty(outer.Payload))
                {
                    var payload = JsonUtility.FromJson<GooglePlayPayload>(outer.Payload);
                    result.GoogleJson = payload.json;
                    result.GoogleSignature = payload.signature;

                    if (!string.IsNullOrEmpty(payload.json))
                    {
                        var receiptData = JsonUtility.FromJson<GooglePlayReceiptData>(payload.json);
                        result.GooglePurchaseToken = receiptData.purchaseToken;
                    }
                }
                else if (outer.Store == "AppleAppStore")
                {
                    result.IosReceipt = outer.Payload;
                }

                return result;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} Failed to parse receipt: {e.Message}");
                return null;
            }
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
            FirebaseAdapter.Initialize();
#endif

#if FIREBASE_CRASHLYTICS_INSTALLED
            Debug.Log($"{Tag} Initializing Firebase Crashlytics...");
            FirebaseCrashlyticsAdapter.Initialize(captureUncaughtExceptions: true);
#endif

#if FIREBASE_REMOTE_CONFIG_INSTALLED
            Debug.Log($"{Tag} Initializing Firebase Remote Config...");
            FirebaseRemoteConfigAdapter.Initialize(autoFetch: true);
#endif

            // TikTok (if configured — requires both App IDs)
            if (!string.IsNullOrEmpty(Config?.tiktokAppId?.Current) && !string.IsNullOrEmpty(Config?.tiktokEmAppId?.Current))
            {
                Debug.Log($"{Tag} Initializing TikTok...");
                TikTokAdapter.Initialize(Config.tiktokEmAppId.Current, Config.tiktokAppId.Current, Config.tiktokAccessToken?.Current ?? "");
            }

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

            // SDK key is read from AppLovinSettings (configured in Integration Manager)
            MaxAdapter.Initialize(
                Config.rewardedAdUnit.Current,
                Config.interstitialAdUnit.Current,
                Config.bannerAdUnit.Current,
                HasConsent);
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

        #endregion
    }
}
