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

        /// <summary>Package version of the Sorolla Palette SDK.</summary>
        public const string SdkVersion = "4.0.0";

        /// <summary>Whether the SDK is initialized</summary>
        public static bool IsInitialized { get; private set; }

        // Set synchronously at the top of Initialize, before IsInitialized (which on the MAX
        // path only flips after the CMP window). Guards against a second Initialize() in that
        // window double-subscribing adapter callbacks (DR-02).
        static bool s_initStarted;

        // Resolved ad-storage consent. Drives MAX init and the change-gated consent event below.
        // Internal-only: studios read ConsentStatus / CanRequestAds, never this raw flag.
        static bool s_adConsent;

        // R1 (DR-129): last ATT status we fanned consent out for. Compared on app-focus so a
        // mid-session ATT flip (user toggled tracking in iOS Settings while backgrounded)
        // re-propagates to every vendor exactly once.
        static ATTBridge.AuthorizationStatus s_lastAttStatus;

        /// <summary>Current configuration (may be null)</summary>
        public static SorollaConfig Config { get; private set; }

        /// <summary>
        ///     Whether detailed diagnostics are active. Resolved from config + build type.
        ///     Always false in non-development builds regardless of config; production-safe
        ///     health markers, warnings, and errors are still logged when this is false.
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

        /// <summary>Lowercase snake_case ATT status for analytics params: authorized | denied | restricted | not_determined.</summary>
        internal static string AttString(ATTBridge.AuthorizationStatus status) => status switch
        {
            ATTBridge.AuthorizationStatus.Authorized => "authorized",
            ATTBridge.AuthorizationStatus.Denied => "denied",
            ATTBridge.AuthorizationStatus.Restricted => "restricted",
            _ => "not_determined",
        };

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
            // The UMP form is being displayed; mark it for the QA snapshot's persistence signal.
            SorollaDiagnostics.MarkConsentFormShown();
            MaxAdapter.ShowPrivacyOptions(onComplete);
#else
            PaletteLog.Warning($"{Tag} MAX not available - privacy options require MAX SDK.");
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

            // Only the queued path needs a defensive copy. When initialized, QueueOrExecute runs the
            // action synchronously so the caller has no window to mutate it. During the pre-consent
            // window the closure can run 1-3s later; snapshot so it dispatches the call-time values
            // and not whatever the caller reused the dictionary for meanwhile (B-13).
            Dictionary<string, object> payload = parameters;
            if (parameters != null && !IsInitialized)
                payload = new Dictionary<string, object>(parameters);

            QueueOrExecute(() =>
            {
                SorollaDiagnostics.RecordCustomEvent(eventName, payload);

#if FIREBASE_ANALYTICS_INSTALLED
                FirebaseAdapter.TrackEvent(eventName, payload);
#endif

                // GA best-effort: design event with the documented `value` param (0 if absent).
                GameAnalyticsAdapter.TrackDesignEvent(eventName, ExtractDesignEventValue(payload));
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

        #region Sorolla Vitals

        /// <summary>Shows the code-only Sorolla Vitals debug console. No prefab or sample scene is required.</summary>
        public static void ShowDebugger()
        {
            SorollaDebugMenuOverlay.Open();
        }

        /// <summary>Hides the code-only Sorolla Vitals debug console.</summary>
        public static void HideDebugger()
        {
            SorollaDebugMenuOverlay.Close();
        }

        /// <summary>Toggles the code-only Sorolla Vitals debug console visibility.</summary>
        public static void ToggleDebugger()
        {
            SorollaDebugMenuOverlay.Toggle();
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
            // Prototype mode: Adjust is not compiled in. iOS has a free Unity built-in getter
            // (PrototypeAdvertisingId); Android needs a JNI shim (AndroidAdvertisingId). Full mode
            // never reaches this branch - the Adjust path above wins.
#if UNITY_IOS
            PrototypeAdvertisingId.GetIdfa(callback);
#elif UNITY_ANDROID
            AndroidAdvertisingId.GetGoogleAdId(callback);
#else
            callback?.Invoke(null);
#endif
#endif
        }

#if SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
        static void InitializeAdjust()
        {
            if (string.IsNullOrEmpty(Config.adjustAppToken))
            {
                PaletteLog.Error($"{Tag} Adjust App Token not configured.");
                SorollaDiagnostics.RecordAdjustConfiguration(false, "Unknown");
                return;
            }

            AdjustEnvironment environment = Config.adjustSandboxMode
                ? AdjustEnvironment.Sandbox
                : AdjustEnvironment.Production;

            SorollaDiagnostics.RecordAdjustConfiguration(true, environment.ToString());
            PaletteLog.Vital($"{Tag} Initializing Adjust ({environment})...");
            AdjustAdapter.Initialize(Config.adjustAppToken, environment, VerboseLogging);
        }

#endif

        #endregion


        #region Initialization

        // R2 (DR-133 residual): if MAX never fires OnSdkInitialized (silently-failing SDK, no CMP
        // callback), complete init anyway after this many foreground seconds so the SDK degrades to
        // no-ads instead of wedging forever. Uses SorollaBootstrapper's realtime coroutine host, so
        // backgrounding (e.g. during the CMP/ATT dialog) pauses the countdown.
        const float MaxInitWatchdogSeconds = 30f;

        /// <summary>
        ///     Initialize Palette SDK. Invoked exclusively by <see cref="SorollaBootstrapper"/>
        ///     once consent / ATT resolve. Internal: studios do not call this.
        /// </summary>
        internal static void Initialize()
        {
            // DR-02: IsInitialized stays false for the whole CMP window on the MAX path
            // (set in OnMaxSdkInitialized ~1-3s later), so guarding on it alone lets a second
            // Initialize() in that window re-run InitializeMax() and double-subscribe the MAX
            // callbacks, doubling ad revenue all session. s_initStarted is set synchronously at
            // entry so the second call is rejected immediately, before any subscription.
            if (IsInitialized || s_initStarted)
            {
                PaletteLog.Warning($"{Tag} Already initializing/initialized. Remove any manual Palette.Initialize() call - the SDK auto-initializes via SorollaBootstrapper.");
                return;
            }
            s_initStarted = true;

            Config = Resources.Load<SorollaConfig>("SorollaConfig");

            bool isPrototype = Config == null || Config.isPrototypeMode;

            // Resolve verbose logging: config toggle AND development build required.
            // Safety net: release builds never get verbose vendor output.
            VerboseLogging = Config != null && Config.verboseLogging && Debug.isDebugBuild;
            PaletteLog.Configure(VerboseLogging);

            // Resolve the boot decision once and set the ad-consent flag MAX init reads.
            ConsentCoordinator.ConsentSignals boot = ConsentCoordinator.ResolveBootSignals();
            s_adConsent = boot.AdStorage;
            s_lastAttStatus = ATTBridge.GetStatus(); // R1: baseline for the app-focus ATT re-propagation
            string initDetail = $"Initializing ({(isPrototype ? "Prototype" : "Full")} mode, analytics: {boot.Analytics}, adStorage: {boot.AdStorage}, verbose: {VerboseLogging})";
            PaletteLog.Vital($"{Tag} {initDetail}...");
            SorollaDiagnostics.RecordInitializing(!isPrototype, initDetail);

            // Fan out boot consent to GA + Facebook + Firebase + diagnostics (idempotent). Adjust is
            // initialized later, inside OnMaxSdkInitialized, so it is skipped on this initial pass.
            ConsentCoordinator.ApplyConsent(boot, initial: true);

            // MAX + Adjust ship together in Full mode. Adjust is initialized
            // inside OnMaxSdkInitialized so consent is resolved first.
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            // Catch-continue: a MAX init throw must not strand the transition to IsInitialized. On a
            // throw maxInitStarted stays false, so the completion block below finishes init in a
            // degraded no-ads state rather than wedging (R2 / DR-133 residual).
            bool maxInitStarted = false;
            try { maxInitStarted = InitializeMax(); }
            catch (Exception e) { PaletteLog.Error($"{Tag} AppLovin MAX init failed: {e.Message}. Continuing in a degraded no-ads state."); }
#endif

#if FIREBASE_CRASHLYTICS_INSTALLED
            PaletteLog.Verbose($"{Tag} Initializing Firebase Crashlytics...");
            SafeInit("Firebase Crashlytics", () => FirebaseCrashlyticsAdapter.Initialize(captureUncaughtExceptions: true));
#endif

#if FIREBASE_REMOTE_CONFIG_INSTALLED
            PaletteLog.Verbose($"{Tag} Initializing Firebase Remote Config...");
            SafeInit("Firebase Remote Config", () => FirebaseRemoteConfigAdapter.Initialize(autoFetch: true));
#endif

            // When MAX is installed, defer IsInitialized until MAX consent resolves
            // (set in OnMaxSdkInitialized). Without MAX, we're ready now.
#if !(SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED)
            CompleteInitialization();
#else
            // MAX path: readiness is normally reached in OnMaxSdkInitialized once the CMP resolves.
            // But if MAX could not start (e.g. missing SorollaConfig) that callback never fires, so
            // complete here instead - the SDK degrades to no-ads rather than wedging forever (B-1).
            if (maxInitStarted)
            {
                PaletteLog.Vital($"{Tag} Waiting for MAX consent resolution...");
                // Watchdog: if OnSdkInitialized never arrives (silently-failing MAX SDK), complete
                // anyway so init can't wedge forever (R2 / DR-133 residual). Idempotent with the
                // normal OnMaxSdkInitialized completion via the guard in CompleteInitialization.
                SorollaBootstrapper.Schedule(MaxInitWatchdogSeconds, () =>
                {
                    if (IsInitialized) return;
                    PaletteLog.Error($"{Tag} MAX did not resolve within {MaxInitWatchdogSeconds:0}s; completing init in a degraded no-ads state.");
                    CompleteInitialization();
                });
            }
            else
                CompleteInitialization();
#endif
        }

        // R2: run a vendor's init behind a catch so one vendor throwing can't strand the rest of the
        // fan-out or block the transition to IsInitialized (DR-38 catch-continue posture). Internal so
        // ConsentCoordinator's boot fan-out can guard the analytics vendors the same way.
        internal static void SafeInit(string vendor, Action init)
        {
            try { init(); }
            catch (Exception e) { PaletteLog.Error($"{Tag} {vendor} init failed: {e.Message}. Continuing without it."); }
        }

        // R1 (DR-129): re-resolve and re-fan consent when the app regains focus, so an ATT status
        // that changed while backgrounded (user toggled tracking in iOS Settings) reaches every vendor
        // mid-session. Change-gated on ATT: off-iOS AttStatus is constant so this is inert, and it
        // does no work unless ATT actually moved. Called by SorollaBootstrapper.OnApplicationFocus.
        internal static void OnAppFocusRegained()
        {
            ATTBridge.AuthorizationStatus att = AttStatus;
            if (att == s_lastAttStatus) return;
            s_lastAttStatus = att;

            try
            {
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
                ConsentCoordinator.ConsentSignals s = ConsentCoordinator.Resolve(MaxAdapter.ConsentStatus, att, adsPresent: true);
#else
                ConsentCoordinator.ConsentSignals s = ConsentCoordinator.Resolve(Adapters.ConsentStatus.NotApplicable, att, adsPresent: false);
#endif
                // Same idempotent fan-out the CMP path uses. On Prototype this re-pushes Facebook
                // advertiser tracking (ATT-gated, not ads-gated), the only path that grants a
                // Prototype build attribution when ATT is authorized after launch.
                ConsentCoordinator.ApplyConsent(s, initial: false);
                PaletteLog.Vital($"{Tag} ATT changed on focus -> re-propagated consent (att={att}, adStorage={s.AdStorage}, adPersonalization={s.AdPersonalization}, advertiserTracking={s.AdvertiserTracking}).");

#if UNITY_IOS && !UNITY_EDITOR
                TrackEvent("att_decision", new Dictionary<string, object>
                {
                    { "att_status", AttString(att) },
                    { "source", "focus" },
                });
#endif

                // ad_storage is ATT-independent, so an ATT-only flip normally leaves it unchanged;
                // guard the flag flip + consent_changed event exactly like OnMaxConsentChanged for the
                // rare case the GDPR bucket also moved while backgrounded.
                if (s_adConsent != s.AdStorage)
                {
                    s_adConsent = s.AdStorage;
                    var changed = new Dictionary<string, object>
                    {
                        { "personalized_ads", s.AdPersonalization },
                        { "analytics", s.Analytics },
                    };
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
                    changed["gdpr"] = ConsentCoordinator.GdprString(MaxAdapter.ConsentStatus);
#endif
#if UNITY_IOS && !UNITY_EDITOR
                    changed["att_status"] = AttString(att);
#endif
                    TrackEvent("consent_changed", changed);
                }
            }
            catch (Exception e)
            {
                PaletteLog.Warning($"{Tag} ATT re-propagation on focus failed: {e.Message}");
            }
        }

        // Threading contract (B-14): all Palette analytics/IAP entry points and these pending queues
        // are main-thread only. Unity game code calls them on the main thread, and MAX callbacks are
        // pinned to the main thread at init (B-2), so the Queue<Action> here and Level.s_startTimes
        // are deliberately unsynchronized. Do not call Palette.* from a background thread.
        //
        // Events fired from game Awake/Start can land before MAX CMP resolves on iOS
        // (pre-consent window is ~1-3s). Queue them here and flush on IsInitialized
        // so adapter dispatch always runs with resolved consent.
        const int PendingQueueCap = 256;
        static readonly PendingActionQueue s_pendingEvents = new PendingActionQueue(PendingQueueCap);

        static void QueueOrExecute(Action action)
        {
            if (IsInitialized) { action(); return; }
            s_pendingEvents.Enqueue(action, onEvicted: () =>
                PaletteLog.Warning($"{Tag} Pending event queue full ({PendingQueueCap}); dropping oldest."));
        }

        static void FlushPending()
        {
            if (s_pendingEvents.Count == 0) return;
            PaletteLog.Verbose($"{Tag} Flushing {s_pendingEvents.Count} queued event(s).");
            // Catch-continue per event: one vendor throw must not strand the rest of the queue or
            // (since this runs inside OnMaxSdkInitialized) skip OnInitialized / consent markers (DR-38).
            s_pendingEvents.Flush(e => PaletteLog.Warning($"{Tag} Queued event threw during flush: {e.Message}"));
        }

        // The ready transition shared by every init path: flip the flag, optionally run
        // caller-supplied work while IsInitialized is already true but before the pre-consent queue
        // drains (the MAX path uses this to emit consent markers - DR-41: they must lead the flush
        // so a BQ query on consent_resolved doesn't miss the whole flushed batch), then drain the
        // queue and fire OnInitialized.
        static void CompleteInitialization(Action beforeFlush = null)
        {
            // Idempotent (R2): the MAX-readiness watchdog and OnMaxSdkInitialized can both reach here
            // in either order. Whichever completes first wins; the second no-ops, so the pending queue
            // is never double-flushed and OnInitialized never fires twice.
            if (IsInitialized) return;
            IsInitialized = true;
            beforeFlush?.Invoke();
            FlushPending();
            OnInitialized?.Invoke();
            PaletteLog.Vital($"{Tag} Ready!");
            SorollaDiagnostics.RecordReady();
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
        // Returns true when MAX init actually started (callbacks subscribed, readiness will be
        // reached in OnMaxSdkInitialized). Returns false when it could not start - the caller must
        // then complete initialization itself so the SDK never wedges (B-1).
        static bool InitializeMax()
        {
            if (Config == null)
            {
                PaletteLog.Error($"{Tag} SorollaConfig not found - cannot initialize AppLovin MAX. Running in a degraded no-ads state; analytics/consent/IAP still work. Add Assets/Resources/SorollaConfig.asset to enable ads.");
                return false;
            }

            PaletteLog.Vital($"{Tag} Initializing AppLovin MAX...");

            // Subscribe to ad loading state changes for loading overlay
            MaxAdapter.OnAdLoadingStateChanged += OnMaxAdLoadingStateChanged;

            // Subscribe to SDK initialized event to init Adjust (per MAX docs)
            MaxAdapter.OnSdkInitialized += OnMaxSdkInitialized;

            // Subscribe to consent status changes from MAX CMP (UMP) to propagate to other adapters
            MaxAdapter.OnConsentStatusChanged += OnMaxConsentChanged;

            // SDK key is read from AppLovinSettings; Palette editor auto-syncs the shared publisher key.
            MaxAdapter.Initialize(
                Config.rewardedAdUnit.Current,
                Config.interstitialAdUnit.Current,
                Config.bannerAdUnit.Current,
                s_adConsent,
                VerboseLogging);

            return true;
        }

        static void OnMaxSdkInitialized()
        {
            // MAX CMP has resolved. GA/Firebase/FB already received UpdateConsent via
            // OnMaxConsentChanged (fired from MaxAdapterImpl.UpdateConsentStatusFromConfig
            // during OnSdkInit, BEFORE this callback runs). Only Adjust still needs init
            // here per MAX SDK docs: "initialize other SDKs INSIDE the MAX callback".
            bool consent = s_adConsent;
            PaletteLog.Vital($"{Tag} MAX consent resolved: {MaxAdapter.ConsentStatus} (consent={consent})");
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

            // IsInitialized is set inside CompleteInitialization, before beforeFlush runs, so these
            // TrackEvent calls dispatch synchronously (not re-queued behind the pending events) and
            // therefore lead the flush (DR-41).
            //
            // Ship the consent decision to analytics so we can query consent-drop cohorts from our own
            // data. `gdpr` + `att` are the two raw user decisions; `personalized_ads` + `analytics` are
            // what the SDK RESOLVED them into. On iOS personalized ads require BOTH gdpr ad-consent AND
            // att=authorized, so gdpr=obtained + att=denied -> personalized_ads=false is a valid,
            // non-personalized user. Read the resolved booleans, not the raw decisions, for behavior.
            CompleteInitialization(() =>
            {
                var resolved = new Dictionary<string, object>
                {
                    { "gdpr", ConsentCoordinator.GdprString(MaxAdapter.ConsentStatus) },
                    { "personalized_ads", ConsentCoordinator.AdPersonalizationAllowed(consent) },
                    { "analytics", MaxAdapter.ConsentStatus != Adapters.ConsentStatus.Denied },
                };
#if UNITY_IOS && !UNITY_EDITOR
                resolved["att_status"] = AttString(ATTBridge.GetStatus());
#endif
                TrackEvent("consent_resolved", resolved);
#if UNITY_IOS && !UNITY_EDITOR
                TrackEvent("att_decision", new Dictionary<string, object>
                {
                    { "att_status", AttString(ATTBridge.GetStatus()) },
                    { "source", "max" },
                });
#endif
            });
        }

        static void OnMaxConsentChanged(Adapters.ConsentStatus status)
        {
            // Same resolver the boot path uses; ads are present on the MAX path.
            ConsentCoordinator.ConsentSignals s = ConsentCoordinator.Resolve(status, AttStatus, adsPresent: true);

            // Idempotent vendor fan-out (analytics + ad signals + Adjust + diagnostics). Runs on
            // EVERY resolution so an ATT-only change (GDPR bucket unchanged) is never missed.
            ConsentCoordinator.ApplyConsent(s, initial: false);

            // Keep the app-focus baseline in step with the CMP-resolved ATT so the first focus after
            // CMP doesn't re-fan redundantly (R1 / DR-129).
            s_lastAttStatus = AttStatus;

            // Change-gated: only the analytics EVENT and the ad-consent flag flip are guarded on the
            // ad-consent bucket actually changing (DR-41: the consent marker must precede
            // FlushPending; s_adConsent gates MAX init and this event). Vendor pushes already ran.
            if (s_adConsent == s.AdStorage) return;

            s_adConsent = s.AdStorage;
            PaletteLog.Vital($"{Tag} Consent updated by MAX CMP: {status} -> propagating to adapters (adStorage={s.AdStorage}, adPersonalization={s.AdPersonalization}, analytics={s.Analytics})");

            var changed = new Dictionary<string, object>
            {
                { "gdpr", ConsentCoordinator.GdprString(status) },
                { "personalized_ads", s.AdPersonalization },
                { "analytics", s.Analytics },
            };
#if UNITY_IOS && !UNITY_EDITOR
            changed["att_status"] = AttString(ATTBridge.GetStatus());
#endif
            TrackEvent("consent_changed", changed);
        }

        static void LogConsentDiagnostics()
        {
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            try
            {
                bool canRequest = MaxAdapter.CanRequestAds;
                PaletteLog.Vital($"{Tag} Consent summary: canRequestAds={canRequest}, consentStatus={MaxAdapter.ConsentStatus}");
                SorollaDiagnostics.RecordConsentSummary(
                    $"Consent summary: canRequestAds={canRequest}, consentStatus={MaxAdapter.ConsentStatus}");
            }
            catch (System.Exception e)
            {
                PaletteLog.Warning($"{Tag} Consent summary unavailable from MAX. Rebuild with verbose logging to inspect adapter state.");
                PaletteLog.Verbose($"{Tag} [Consent Diagnostics] Could not read MAX consent state: {e.Message}");
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
                PaletteLog.Verbose($"{Tag} [Consent Diagnostics] Android TCF string={PaletteLog.Present(tcfString)}, purposeConsents={PaletteLog.Present(purposeConsents)}");
                if (!string.IsNullOrEmpty(purposeConsents))
                {
                    // Purposes 1 (storage), 3 (ad personalization), 4 (ad selection) must be '1'
                    bool p1 = purposeConsents.Length > 0 && purposeConsents[0] == '1';
                    bool p3 = purposeConsents.Length > 2 && purposeConsents[2] == '1';
                    bool p4 = purposeConsents.Length > 3 && purposeConsents[3] == '1';
                    PaletteLog.Verbose($"{Tag} [Consent Diagnostics] Purpose 1 (storage)={p1}, Purpose 3 (personalization)={p3}, Purpose 4 (ad selection)={p4}");
                    if (!p1 || !p3 || !p4)
                        PaletteLog.Warning($"{Tag} Consent hint: required TCF ad purposes are missing; ads may be non-personalized. Rebuild with verbose logging to inspect purpose bits.");
                }
            }
            catch (System.Exception e)
            {
                PaletteLog.Verbose($"{Tag} [Consent Diagnostics] Android TCF read failed: {e.Message}");
            }
#endif
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                string tcfString = UnityEngine.iOS.Device.advertisingIdentifier; // triggers ATT read as side-effect
                string tcf = PlayerPrefs.GetString("IABTCF_TCString", null);
                string purposes = PlayerPrefs.GetString("IABTCF_PurposeConsents", null);
                PaletteLog.Verbose($"{Tag} [Consent Diagnostics] iOS TCF string={PaletteLog.Present(tcf)}, purposeConsents={PaletteLog.Present(purposes)}");
                if (!string.IsNullOrEmpty(purposes))
                {
                    bool p1 = purposes.Length > 0 && purposes[0] == '1';
                    bool p3 = purposes.Length > 2 && purposes[2] == '1';
                    bool p4 = purposes.Length > 3 && purposes[3] == '1';
                    PaletteLog.Verbose($"{Tag} [Consent Diagnostics] Purpose 1 (storage)={p1}, Purpose 3 (personalization)={p3}, Purpose 4 (ad selection)={p4}");
                    if (!p1 || !p3 || !p4)
                        PaletteLog.Warning($"{Tag} Consent hint: required TCF ad purposes are missing; ads may be non-personalized. Rebuild with verbose logging to inspect purpose bits.");
                }
            }
            catch (System.Exception e)
            {
                PaletteLog.Verbose($"{Tag} [Consent Diagnostics] iOS TCF read failed: {e.Message}");
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
            SorollaDiagnostics.RecordEventDispatch("ads", "ad_show_requested", new Dictionary<string, object>
            {
                { "ad_format", "rewarded" },
            });
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            MaxAdapter.ShowRewardedAd(
                () =>
                {
                    SorollaDiagnostics.RecordEventDispatch("ads", "rewarded_ad_completed", new Dictionary<string, object>
                    {
                        { "ad_format", "rewarded" },
                    });
                    onComplete?.Invoke();
                },
                () =>
                {
                    SorollaDiagnostics.RecordEventDispatch("ads", "ad_show_failed", new Dictionary<string, object>
                    {
                        { "ad_format", "rewarded" },
                        { "reason", "callback_failed" },
                    });
                    onFailed?.Invoke();
                });
#else
            SorollaDiagnostics.RecordEventDispatch("ads", "ad_show_failed", new Dictionary<string, object>
            {
                { "ad_format", "rewarded" },
                { "reason", "max_unavailable" },
            });
            PaletteLog.Warning($"{Tag} MAX not available.");
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
            SorollaDiagnostics.RecordEventDispatch("ads", "ad_show_requested", new Dictionary<string, object>
            {
                { "ad_format", "interstitial" },
            });
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            MaxAdapter.ShowInterstitialAd(
                () =>
                {
                    SorollaDiagnostics.RecordEventDispatch("ads", "interstitial_ad_completed", new Dictionary<string, object>
                    {
                        { "ad_format", "interstitial" },
                    });
                    onComplete?.Invoke();
                },
                () =>
                {
                    SorollaDiagnostics.RecordEventDispatch("ads", "ad_show_failed", new Dictionary<string, object>
                    {
                        { "ad_format", "interstitial" },
                        { "reason", "callback_failed" },
                    });
                    onFailed?.Invoke();
                });
#else
            SorollaDiagnostics.RecordEventDispatch("ads", "ad_show_failed", new Dictionary<string, object>
            {
                { "ad_format", "interstitial" },
                { "reason", "max_unavailable" },
            });
            PaletteLog.Warning($"{Tag} MAX not available - interstitial skipped, invoking onFailed.");
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
            PaletteLog.Warning($"{Tag} MAX not available - mediation debugger requires MAX SDK.");
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
            PaletteLog.Warning($"{Tag} MAX not available - creative debugger requires MAX SDK.");
#endif
        }

        #endregion

    }
}
