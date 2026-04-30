using System;
using System.Collections.Generic;
using UnityEngine;
using Sorolla.Palette.Adapters;
using Sorolla.Palette.ATT;
#if GAMEANALYTICS_INSTALLED
using GameAnalyticsSDK;
#endif

namespace Sorolla.Palette
{
    /// <summary>
    ///     Main API for Palette SDK.
    ///     Provides unified interface for analytics, ads, and attribution.
    ///     Auto-initialized - no manual setup required.
    /// </summary>
    public static partial class Palette
    {
        const string Tag = "[Palette]";

        /// <summary>Whether the SDK is initialized</summary>
        public static bool IsInitialized { get; private set; }

        /// <summary>Current user consent status (legacy - use ConsentStatus for GDPR compliance)</summary>
        public static bool HasConsent { get; private set; }

        /// <summary>Current configuration (may be null)</summary>
        public static SorollaConfig Config { get; private set; }

        /// <summary>
        ///     Whether verbose logging is active. Resolved from config + build type.
        ///     Always false in non-development builds regardless of config.
        /// </summary>
        public static bool VerboseLogging { get; private set; }

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
        ///     iOS AppTrackingTransparency authorization status. Returns Authorized on non-iOS / Editor.
        ///     Canonical read for game code and debug UI — prefer this over reaching into ATTBridge.
        /// </summary>
        public static ATTBridge.AuthorizationStatus AttStatus => ATTBridge.GetStatus();

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
        /// <example>
        /// <code>
        /// Palette.TrackEvent("booster_used", new Dictionary&lt;string, object&gt;
        /// {
        ///     { "booster_id", "speed_2x" },
        ///     { "level", 12 },
        /// });
        /// </code>
        /// </example>
        public static void TrackEvent(string eventName, Dictionary<string, object> parameters = null)
        {
            if (!ValidateEvent(ref eventName, parameters)) return;

            QueueOrExecute(() =>
            {
#if FIREBASE_ANALYTICS_INSTALLED
                FirebaseAdapter.TrackEvent(eventName, parameters);
#endif

                // GA best-effort: design event with first numeric value
                GameAnalyticsAdapter.TrackDesignEvent(eventName, ExtractFirstNumericValue(parameters));
            });
        }

        #endregion

        #region User Identity

        /// <summary>
        ///     Set the user ID for analytics, crash reporting, and attribution.
        ///     Pass null to clear.
        /// </summary>
        public static void SetUserId(string userId)
        {
            QueueOrExecute(() =>
            {
#if FIREBASE_ANALYTICS_INSTALLED
                FirebaseAdapter.SetUserId(userId);
#endif

#if FIREBASE_CRASHLYTICS_INSTALLED
                FirebaseCrashlyticsAdapter.SetCustomKey("user_id", userId ?? "");
#endif

#if SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
                AdjustAdapter.SetUserId(userId);
#endif
            });
        }

        /// <summary>
        ///     Set a user property for Firebase Analytics segmentation and audience building.
        ///     Register custom properties in Firebase Console > Analytics > User Properties.
        /// </summary>
        public static void SetUserProperty(string name, string value)
        {
            QueueOrExecute(() =>
            {
#if FIREBASE_ANALYTICS_INSTALLED
                FirebaseAdapter.SetUserProperty(name, value);
#endif
            });
        }

        #endregion

        #region Debug UI

        /// <summary>Fired when <see cref="ShowDebugger"/> is called. Subscribed by the DebugUI sample prefab.</summary>
        public static event Action OnShowDebuggerRequested;
        /// <summary>Fired when <see cref="HideDebugger"/> is called. Subscribed by the DebugUI sample prefab.</summary>
        public static event Action OnHideDebuggerRequested;
        /// <summary>Fired when <see cref="ToggleDebugger"/> is called. Subscribed by the DebugUI sample prefab.</summary>
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

        /// <summary>
        ///     Get Adjust attribution data (network, campaign, tracker).
        ///     Returns null to callback if attribution is not yet available.
        /// </summary>
        /// <param name="callback">Callback with attribution data, or null if unavailable</param>
        public static void GetAttribution(Action<AttributionData?> callback)
        {
#if SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
            AdjustAdapter.GetAttribution(callback);
#else
            callback?.Invoke(null);
#endif
        }

        /// <summary>
        ///     Get the Adjust device ID (ADID).
        /// </summary>
        /// <param name="callback">Callback with the ADID string, or null if unavailable</param>
        public static void GetAdjustId(Action<string> callback)
        {
#if SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
            AdjustAdapter.GetAdid(callback);
#else
            callback?.Invoke(null);
#endif
        }

        /// <summary>
        ///     Get the platform advertising ID (GAID on Android, IDFA on iOS).
        /// </summary>
        /// <param name="callback">Callback with the advertising ID string, or null if unavailable</param>
        public static void GetAdvertisingId(Action<string> callback)
        {
#if SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
#if UNITY_ANDROID
            AdjustAdapter.GetGoogleAdId(callback);
#elif UNITY_IOS
            AdjustAdapter.GetIdfa(callback);
#else
            callback?.Invoke(null);
#endif
#else
            callback?.Invoke(null);
#endif
        }

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
            AdjustAdapter.Initialize(Config.adjustAppToken, environment, VerboseLogging);
        }

#endif

        #endregion


        #region Initialization

        /// <summary>
        ///     Initialize Palette SDK. Invoked exclusively by <see cref="SorollaBootstrapper"/>
        ///     once consent / ATT resolve. Internal — studios do not call this.
        /// </summary>
        internal static void Initialize(bool consent)
        {
            if (IsInitialized)
            {
                Debug.LogWarning($"{Tag} Already initialized. Remove any manual Palette.Initialize() call — the SDK auto-initializes via SorollaBootstrapper.");
                return;
            }

            HasConsent = consent;
            Config = Resources.Load<SorollaConfig>("SorollaConfig");

            bool isPrototype = Config == null || Config.isPrototypeMode;

            // Resolve verbose logging: config toggle AND development build required.
            // Safety net: release builds never get verbose vendor output.
            VerboseLogging = Config != null && Config.verboseLogging && Debug.isDebugBuild;
            Debug.Log($"{Tag} Initializing ({(isPrototype ? "Prototype" : "Full")} mode, consent: {consent}, verbose: {VerboseLogging})...");

            // GameAnalytics (always)
            GameAnalyticsAdapter.Initialize(consent, VerboseLogging);

            // Facebook (always)
#if SOROLLA_FACEBOOK_ENABLED
            FacebookAdapter.Initialize(consent);
#endif

            // MAX + Adjust ship together in Full mode. Adjust is initialized
            // inside OnMaxSdkInitialized so consent is resolved first.
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            InitializeMax();
#endif

            // Firebase modules (always enabled when installed)
#if FIREBASE_ANALYTICS_INSTALLED
            Debug.Log($"{Tag} Initializing Firebase Analytics...");
            FirebaseAdapter.Initialize(consent, VerboseLogging);
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
                TikTokAdapter.Initialize(Config.tiktokEmAppId.Current, Config.tiktokAppId.Current, Config.tiktokAccessToken?.Current ?? "", VerboseLogging);
            }

            // When MAX is installed, defer IsInitialized until MAX consent resolves
            // (set in OnMaxSdkInitialized). Without MAX, we're ready now.
#if !(SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED)
            IsInitialized = true;
            FlushPending();
            OnInitialized?.Invoke();
            Debug.Log($"{Tag} Ready!");
#else
            Debug.Log($"{Tag} Waiting for MAX consent resolution...");
#endif
        }

        // Events fired from game Awake/Start can land before MAX CMP resolves on iOS
        // (pre-consent window is ~1-3s). Queue them here and flush on IsInitialized
        // so adapter dispatch always runs with resolved consent.
        static readonly Queue<Action> s_pendingEvents = new Queue<Action>();
        const int PendingQueueCap = 256;

        static void QueueOrExecute(Action action)
        {
            if (IsInitialized) { action(); return; }
            if (s_pendingEvents.Count >= PendingQueueCap)
            {
                Debug.LogWarning($"{Tag} Pending event queue full ({PendingQueueCap}); dropping oldest.");
                s_pendingEvents.Dequeue();
            }
            s_pendingEvents.Enqueue(action);
        }

        static void FlushPending()
        {
            if (s_pendingEvents.Count == 0) return;
            Debug.Log($"{Tag} Flushing {s_pendingEvents.Count} queued event(s).");
            while (s_pendingEvents.Count > 0)
                s_pendingEvents.Dequeue().Invoke();
        }

        #endregion

        #region Error Logging

        /// <summary>Log an exception to crash reporting services (Firebase Crashlytics)</summary>
        public static void LogException(Exception exception)
        {
#if FIREBASE_CRASHLYTICS_INSTALLED
            QueueOrExecute(() => FirebaseCrashlyticsAdapter.LogException(exception));
#endif
        }

        /// <summary>Log a message to crash reporting services (Firebase Crashlytics)</summary>
        public static void LogCrashlytics(string message)
        {
#if FIREBASE_CRASHLYTICS_INSTALLED
            QueueOrExecute(() => FirebaseCrashlyticsAdapter.Log(message));
#endif
        }

        /// <summary>Set a custom key for crash reports (Firebase Crashlytics)</summary>
        public static void SetCrashlyticsKey(string key, string value)
        {
#if FIREBASE_CRASHLYTICS_INSTALLED
            QueueOrExecute(() => FirebaseCrashlyticsAdapter.SetCustomKey(key, value));
#endif
        }

        /// <summary>Set a custom int key for crash reports (Firebase Crashlytics)</summary>
        public static void SetCrashlyticsKey(string key, int value)
        {
#if FIREBASE_CRASHLYTICS_INSTALLED
            QueueOrExecute(() => FirebaseCrashlyticsAdapter.SetCustomKey(key, value));
#endif
        }

        /// <summary>Set a custom float key for crash reports (Firebase Crashlytics)</summary>
        public static void SetCrashlyticsKey(string key, float value)
        {
#if FIREBASE_CRASHLYTICS_INSTALLED
            QueueOrExecute(() => FirebaseCrashlyticsAdapter.SetCustomKey(key, value));
#endif
        }

        /// <summary>Set a custom bool key for crash reports (Firebase Crashlytics)</summary>
        public static void SetCrashlyticsKey(string key, bool value)
        {
#if FIREBASE_CRASHLYTICS_INSTALLED
            QueueOrExecute(() => FirebaseCrashlyticsAdapter.SetCustomKey(key, value));
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
                HasConsent,
                VerboseLogging);
        }

        static void OnMaxSdkInitialized()
        {
            // MAX CMP has resolved. GA/Firebase/FB already received UpdateConsent via
            // OnMaxConsentChanged (fired from MaxAdapterImpl.UpdateConsentStatusFromConfig
            // during OnSdkInit, BEFORE this callback runs). Only Adjust still needs init
            // here per MAX SDK docs: "initialize other SDKs INSIDE the MAX callback".
            bool consent = HasConsent;
            Debug.Log($"{Tag} MAX consent resolved: {MaxAdapter.ConsentStatus} (consent={consent})");
            LogConsentDiagnostics();

#if SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
            bool isPrototype = Config == null || Config.isPrototypeMode;
            if (!isPrototype && Config != null)
            {
                InitializeAdjust();
                // Adjust starts enabled after InitSdk; immediately Disable if consent denied
                // so we don't ship attribution events for a user who opted out.
                AdjustAdapter.UpdateConsent(consent);
            }
#endif

            IsInitialized = true;
            FlushPending();
            OnInitialized?.Invoke();
            Debug.Log($"{Tag} Ready!");

            // Ship decision to analytics so we can query consent-drop cohorts from our own data.
            TrackEvent("consent_resolved", new Dictionary<string, object>
            {
                { "max_status", MaxAdapter.ConsentStatus.ToString() },
                { "consent", consent },
                { "source", "max" },
            });
#if UNITY_IOS && !UNITY_EDITOR
            TrackEvent("att_decision", new Dictionary<string, object>
            {
                { "att_status", ATTBridge.GetStatus().ToString() },
                { "source", "max" },
            });
#endif
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
#if SOROLLA_FACEBOOK_ENABLED
            FacebookAdapter.UpdateConsent(consent);
#endif
#if SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
            AdjustAdapter.UpdateConsent(consent);
#endif

            TrackEvent("consent_changed", new Dictionary<string, object>
            {
                { "max_status", status.ToString() },
                { "consent", consent },
            });
        }

        static void LogConsentDiagnostics()
        {
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            try
            {
                bool canRequest = MaxAdapter.CanRequestAds;
                Debug.Log($"{Tag} [Consent Diagnostics] CanRequestAds={canRequest}, ConsentStatus={MaxAdapter.ConsentStatus}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{Tag} [Consent Diagnostics] Could not read MAX consent state: {e.Message}");
            }

#if UNITY_ANDROID
            try
            {
                using var activity = new UnityEngine.AndroidJavaClass("com.unity3d.player.UnityPlayer")
                    .GetStatic<UnityEngine.AndroidJavaObject>("currentActivity");
                using var context = activity.Call<UnityEngine.AndroidJavaObject>("getApplicationContext");
                using var prefs = context.Call<UnityEngine.AndroidJavaObject>(
                    "getSharedPreferences", "IABTCF_CMP_SDK", 0);
                string tcfString = prefs.Call<string>("getString", "IABTCF_TCString", null);
                string purposeConsents = prefs.Call<string>("getString", "IABTCF_PurposeConsents", null);
                Debug.Log($"{Tag} [Consent Diagnostics] IABTCF_TCString={(string.IsNullOrEmpty(tcfString) ? "EMPTY - CMP not writing TCF string!" : $"present ({tcfString.Length} chars)")}, PurposeConsents={purposeConsents ?? "null"}");
                if (!string.IsNullOrEmpty(purposeConsents))
                {
                    // Purposes 1 (storage), 3 (ad personalization), 4 (ad selection) must be '1'
                    bool p1 = purposeConsents.Length > 0 && purposeConsents[0] == '1';
                    bool p3 = purposeConsents.Length > 2 && purposeConsents[2] == '1';
                    bool p4 = purposeConsents.Length > 3 && purposeConsents[3] == '1';
                    Debug.Log($"{Tag} [Consent Diagnostics] Purpose 1 (storage)={p1}, Purpose 3 (personalization)={p3}, Purpose 4 (ad selection)={p4}");
                    if (!p1 || !p3 || !p4)
                        Debug.LogWarning($"{Tag} [Consent Diagnostics] Missing required TCF purposes → ads will be non-personalized despite user consent");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{Tag} [Consent Diagnostics] Android TCF read failed: {e.Message}");
            }
#endif
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                string tcfString = UnityEngine.iOS.Device.advertisingIdentifier; // triggers ATT read as side-effect
                string tcf = PlayerPrefs.GetString("IABTCF_TCString", null);
                string purposes = PlayerPrefs.GetString("IABTCF_PurposeConsents", null);
                Debug.Log($"{Tag} [Consent Diagnostics] IABTCF_TCString={(string.IsNullOrEmpty(tcf) ? "EMPTY - CMP not writing TCF string!" : $"present ({tcf.Length} chars)")}, PurposeConsents={purposes ?? "null"}");
                if (!string.IsNullOrEmpty(purposes))
                {
                    bool p1 = purposes.Length > 0 && purposes[0] == '1';
                    bool p3 = purposes.Length > 2 && purposes[2] == '1';
                    bool p4 = purposes.Length > 3 && purposes[3] == '1';
                    Debug.Log($"{Tag} [Consent Diagnostics] Purpose 1 (storage)={p1}, Purpose 3 (personalization)={p3}, Purpose 4 (ad selection)={p4}");
                    if (!p1 || !p3 || !p4)
                        Debug.LogWarning($"{Tag} [Consent Diagnostics] Missing required TCF purposes → ads will be non-personalized despite user consent");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{Tag} [Consent Diagnostics] iOS TCF read failed: {e.Message}");
            }
#endif
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

        /// <summary>
        ///     Show an interstitial ad. <paramref name="onComplete"/> fires after the user
        ///     dismisses the ad. <paramref name="onFailed"/> fires when the ad cannot be
        ///     shown (no fill, display error at runtime, ad subsystem unavailable). Exactly
        ///     one of the two callbacks fires per call — studios must handle failure to keep
        ///     game flow alive when interstitials no-fill.
        /// </summary>
        public static void ShowInterstitialAd(Action onComplete, Action onFailed)
        {
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            MaxAdapter.ShowInterstitialAd(onComplete, onFailed);
#else
            Debug.LogWarning($"{Tag} MAX not available - interstitial skipped, invoking onFailed.");
            onFailed?.Invoke();
#endif
        }

        /// <summary>
        ///     Opens AppLovin's Mediation Debugger — an in-app modal listing every
        ///     integrated ad network, its adapter SDK version, config status, and
        ///     a per-network "Live Test Ads" button to force end-to-end delivery
        ///     from each network. Canonical tool for verifying ad-network wiring.
        /// </summary>
        public static void ShowMediationDebugger()
        {
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            MaxAdapter.ShowMediationDebugger();
#else
            Debug.LogWarning($"{Tag} MAX not available - mediation debugger requires MAX SDK.");
#endif
        }

        /// <summary>
        ///     Opens AppLovin's Creative Debugger. While enabled, long-pressing a
        ///     displayed ad overlays its network, ad unit, bid price, and creative
        ///     ID — diagnostic for "why did that specific ad show" questions.
        /// </summary>
        public static void ShowCreativeDebugger()
        {
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            MaxAdapter.ShowCreativeDebugger();
#else
            Debug.LogWarning($"{Tag} MAX not available - creative debugger requires MAX SDK.");
#endif
        }

        #endregion

    }
}
