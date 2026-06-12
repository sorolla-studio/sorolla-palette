using System;
using System.Collections.Generic;
using System.Text;
using Sorolla.Palette.Adapters;
using UnityEngine;

namespace Sorolla.Palette
{
    internal enum SorollaDiagnosticSeverity
    {
        Info,
        Waiting,
        Pass,
        Warning,
        Fail,
    }

    internal enum SorollaDiagnosticKind
    {
        Required,
        Observed,
        Context,
    }

    internal readonly struct SorollaDiagnosticRow
    {
        public readonly string Group;
        public readonly string Name;
        public readonly SorollaDiagnosticSeverity Severity;
        public readonly string Detail;
        public readonly SorollaDiagnosticKind Kind;

        public SorollaDiagnosticRow(string group, string name, SorollaDiagnosticSeverity severity, string detail,
            SorollaDiagnosticKind kind)
        {
            Group = group;
            Name = name;
            Severity = severity;
            Detail = detail;
            Kind = kind;
        }
    }

    internal readonly struct SorollaRuntimeProblem
    {
        public readonly int Id;
        public readonly string Fingerprint;
        public readonly float FirstTimeSeconds;
        public readonly float LastTimeSeconds;
        public readonly int Count;
        public readonly SorollaDiagnosticSeverity Severity;
        public readonly string Source;
        public readonly string Type;
        public readonly string Message;
        public readonly string TopFrame;
        public readonly string StackTrace;

        public SorollaRuntimeProblem(int id, string fingerprint, float firstTimeSeconds, float lastTimeSeconds,
            int count, SorollaDiagnosticSeverity severity, string source, string type, string message,
            string topFrame, string stackTrace)
        {
            Id = id;
            Fingerprint = fingerprint;
            FirstTimeSeconds = firstTimeSeconds;
            LastTimeSeconds = lastTimeSeconds;
            Count = count;
            Severity = severity;
            Source = source;
            Type = type;
            Message = message;
            TopFrame = topFrame;
            StackTrace = stackTrace;
        }

        public SorollaRuntimeProblem WithRepeat(float timeSeconds, SorollaDiagnosticSeverity severity)
        {
            return new SorollaRuntimeProblem(Id, Fingerprint, FirstTimeSeconds, timeSeconds, Count + 1,
                severity, Source, Type, Message, TopFrame, StackTrace);
        }
    }

    internal readonly struct SorollaDiagnosticEventLogEntry
    {
        public readonly int Id;
        public readonly float TimeSeconds;
        public readonly string Source;
        public readonly string Name;
        public readonly string Payload;
        public readonly SorollaDiagnosticPayloadLine[] PayloadLines;

        public SorollaDiagnosticEventLogEntry(int id, float timeSeconds, string source, string name, string payload,
            SorollaDiagnosticPayloadLine[] payloadLines)
        {
            Id = id;
            TimeSeconds = timeSeconds;
            Source = source;
            Name = name;
            Payload = payload;
            PayloadLines = payloadLines ?? Array.Empty<SorollaDiagnosticPayloadLine>();
        }
    }

    internal readonly struct SorollaDiagnosticPayloadLine
    {
        public readonly string Key;
        public readonly string Value;

        public SorollaDiagnosticPayloadLine(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }

    internal static class SorollaDiagnostics
    {
        const float IdentifierRefreshIntervalSeconds = 20f;
        const int MaxEventLogEntries = 40;
        const int MaxRuntimeProblemEntries = 20;
        const string RuntimeProblemsRowName = "Runtime problems";
        static readonly object s_lock = new object();
        static readonly Queue<SorollaDiagnosticEventLogEntry> s_eventLog = new Queue<SorollaDiagnosticEventLogEntry>(MaxEventLogEntries);
        static readonly List<SorollaRuntimeProblem> s_runtimeProblems = new List<SorollaRuntimeProblem>(MaxRuntimeProblemEntries);
        static int s_nextEventId;
        static int s_nextRuntimeProblemId;

        static bool s_logBridgeInstalled;
        static bool s_unityLogInstalled;

        static SorollaDiagnostics()
        {
            MaxAdapter.OnAdRevenueTracked += RecordAdRevenue;
        }

        static bool s_autoInitSeen;
        static bool s_initializeSeen;
        static bool s_readySeen;
        static bool s_modeKnown;
        static bool s_fullMode;
        static string s_initDetail = "Not observed yet";

        static bool s_gaInitialized;
        static bool s_facebookInitialized;
        static bool s_facebookFailed;
        static bool s_maxRegistered;
        static bool s_maxInitialized;
        static bool s_maxConsentSeen;
        static string s_maxConsentDetail = "Not observed yet";
        static bool s_adjustRegistered;
        static bool s_adjustInitializing;
        static bool s_adjustInitialized;
        static bool s_adjustMissingToken;
        static string s_adjustEnvironment = "Unknown";
        static bool s_firebaseCoreReady;
        static bool s_firebaseAnalyticsReady;
        static bool s_crashlyticsReady;
        static bool s_remoteConfigFetchSeen;
        static bool s_remoteConfigFetchSuccess;
        static string s_remoteConfigDetail = "Not observed yet";

        static bool s_purchaseTrackingAttached;
        static int s_purchaseAcceptedCount;
        static int s_purchaseDuplicateCount;
        static string s_purchaseIssue = "No issue observed";
        static string s_purchaseVerification = "Not observed";

        static int s_progressionStartCount;
        static int s_progressionEndCount;
        static int s_economyEarnCount;
        static int s_economySpendCount;
        static int s_customEventCount;
        static string s_lastCustomEvent = "None";

        static bool s_rewardedLoaded;
        static bool s_rewardedCompleted;
        static bool s_interstitialLoaded;
        static bool s_interstitialCompleted;
        static bool s_adRevenueSeen;
        static string s_lastAdIssue = "No issue observed";

        static int s_paletteWarningCount;
        static int s_paletteErrorCount;
        static string s_lastPaletteWarning = "None";
        static string s_lastPaletteError = "None";

        static float s_lastIdentifierRefresh = -999f;
        static float s_adIdRequestTime = -999f;
        static float s_adjustIdRequestTime = -999f;
        static float s_attributionRequestTime = -999f;
        static bool s_adIdRequested;
        static bool s_adIdReceived;
        static bool s_adIdPresent;
        static bool s_adIdZeroed;
        static bool s_adjustIdRequested;
        static bool s_adjustIdReceived;
        static bool s_adjustIdPresent;
        static string s_attributionSummary = "Not requested";

        // Resolved GA4/Firebase consent-mode signals, recorded by the Palette consent-propagation
        // layer (NOT inside an adapter, to stay out of Adapter Endpoint Review scope). Surface only;
        // they do not drive adapter behavior.
        static bool s_consentSignalsKnown;
        static bool s_adStorageConsent;
        static bool s_adPersonalizationConsent;
        static bool s_adUserDataConsent;
        static bool s_analyticsStorageConsent;
        static bool s_consentFormShownThisSession;

        internal static void EnsureLogBridge()
        {
            if (s_logBridgeInstalled) return;

            PaletteLog.MessageEmitted += RecordPaletteLog;
            s_logBridgeInstalled = true;
        }

        internal static void InstallUnityLogSink()
        {
            if (s_unityLogInstalled) return;

            Application.logMessageReceived += RecordUnityLog;
            s_unityLogInstalled = true;
        }

        internal static void UninstallUnityLogSink()
        {
            if (!s_unityLogInstalled) return;

            Application.logMessageReceived -= RecordUnityLog;
            s_unityLogInstalled = false;
        }

        internal static void RecordProgression(string status)
        {
            lock (s_lock)
            {
                if (status == "start") s_progressionStartCount++;
                else s_progressionEndCount++;
            }
        }

        internal static void RecordEconomy(bool earn)
        {
            lock (s_lock)
            {
                if (earn) s_economyEarnCount++;
                else s_economySpendCount++;
            }
        }

        // Records the four resolved GA4/Firebase consent-mode signals. Called from the Palette consent
        // layer on every resolution so the snapshot reports what the SDK actually resolved consent into,
        // not the raw user taps. ad_user_data tracks ad_personalization (both require consent + ATT).
        internal static void RecordConsentSignals(bool adStorage, bool adPersonalization, bool adUserData, bool analyticsStorage)
        {
            lock (s_lock)
            {
                s_consentSignalsKnown = true;
                s_adStorageConsent = adStorage;
                s_adPersonalizationConsent = adPersonalization;
                s_adUserDataConsent = adUserData;
                s_analyticsStorageConsent = analyticsStorage;
            }
        }

        // Marks that a consent form was displayed this session. Enables relaunch-persistence gates:
        // a second-launch snapshot with form_shown_this_session=false confirms consent persisted.
        internal static void MarkConsentFormShown()
        {
            lock (s_lock)
            {
                s_consentFormShownThisSession = true;
            }
        }

        internal static void RecordCustomEvent(string eventName, IDictionary<string, object> parameters = null)
        {
            lock (s_lock)
            {
                s_customEventCount++;
                s_lastCustomEvent = string.IsNullOrEmpty(eventName) ? "unnamed" : eventName;
                EnqueueEvent("custom", s_lastCustomEvent, parameters);
            }
        }

        internal static void RecordEventDispatch(string source, string eventName, IDictionary<string, object> parameters = null)
        {
            lock (s_lock)
            {
                EnqueueEvent(source, eventName, parameters);
            }
        }

        // Records a forwarded ad-revenue impression. Drives the Vitals "Ad revenue" row directly so
        // it reflects real dispatch regardless of log verbosity (the old "TrackAdRevenue:" log-sniff
        // only fired on Verbose/debug builds, so release builds always read "No revenue callback
        // observed") and regardless of which revenue vendors are installed (DR-09).
        internal static void RecordAdRevenue(string network, double revenue, string currency, string adFormat, string revenuePrecision = null)
        {
            lock (s_lock)
            {
                s_adRevenueSeen = true;
                var parameters = new Dictionary<string, object>
                {
                    { "network", network ?? "unknown" },
                    { "ad_format", adFormat ?? "unknown" },
                    { "revenue", revenue },
                    { "currency", currency ?? "USD" },
                };
                if (!string.IsNullOrEmpty(revenuePrecision))
                    parameters["revenue_precision"] = revenuePrecision;
                EnqueueEvent("ads", "ad_revenue", parameters);
            }
        }

        static void RecordAdRevenue(MaxAdRevenueInfo info)
        {
            RecordAdRevenue(info.Network, info.Revenue, info.Currency, info.AdFormat, info.RevenuePrecision);
        }

        internal static void ClearEventLog()
        {
            lock (s_lock)
            {
                s_eventLog.Clear();
            }
        }

        internal static void ClearRuntimeProblems()
        {
            lock (s_lock)
            {
                s_runtimeProblems.Clear();
            }
        }

        internal static void CopyEventLog(List<SorollaDiagnosticEventLogEntry> target)
        {
            target.Clear();
            lock (s_lock)
            {
                foreach (SorollaDiagnosticEventLogEntry entry in s_eventLog)
                    target.Add(entry);
            }
        }

        internal static void CopyRuntimeProblems(List<SorollaRuntimeProblem> target)
        {
            target.Clear();
            lock (s_lock)
            {
                for (int i = 0; i < s_runtimeProblems.Count; i++)
                    target.Add(s_runtimeProblems[i]);
            }
        }

        internal static void UpdatePolling()
        {
            if (!Palette.IsInitialized) return;

            float now = Time.realtimeSinceStartup;
            if (now - s_lastIdentifierRefresh < IdentifierRefreshIntervalSeconds) return;

            RefreshIdentifiers();
        }

        internal static void RefreshIdentifiers()
        {
            float now = Time.realtimeSinceStartup;
            lock (s_lock)
            {
                s_lastIdentifierRefresh = now;
                s_adIdRequested = true;
                s_adIdReceived = false;
                s_adIdRequestTime = now;
                s_adjustIdRequested = true;
                s_adjustIdReceived = false;
                s_adjustIdRequestTime = now;
                s_attributionRequestTime = now;
                s_attributionSummary = "Fetching";
            }

            Palette.GetAdvertisingId(id =>
            {
                lock (s_lock)
                {
                    s_adIdReceived = true;
                    s_adIdPresent = !string.IsNullOrEmpty(id);
                    s_adIdZeroed = IsZeroAdvertisingId(id);
                }
            });

            Palette.GetAdjustId(adid =>
            {
                lock (s_lock)
                {
                    s_adjustIdReceived = true;
                    s_adjustIdPresent = !string.IsNullOrEmpty(adid);
                }
            });

            Palette.GetAttribution(attr =>
            {
                lock (s_lock)
                {
                    if (!attr.HasValue)
                    {
                        s_attributionSummary = "Unavailable";
                        return;
                    }

                    AttributionData data = attr.Value;
                    s_attributionSummary = string.IsNullOrEmpty(data.Network) ? "Network missing" : data.Network;
                }
            });
        }

        internal static void BuildRows(List<SorollaDiagnosticRow> rows)
        {
            rows.Clear();

            SorollaConfig config = LoadConfig();
            Snapshot snapshot = CaptureSnapshot();

            bool fullMode = config != null && !config.isPrototypeMode;

            Add(rows, "Boot", "Auto-init marker", snapshot.AutoInitSeen ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting,
                snapshot.AutoInitSeen ? "Observed" : "Waiting for bootstrap");
            Add(rows, "Boot", "Palette mode", ModeSeverity(config, snapshot), ModeDetail(config, snapshot));
            Add(rows, "Boot", "Palette ready", Palette.IsInitialized || snapshot.ReadySeen ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting,
                Palette.IsInitialized || snapshot.ReadySeen ? "Ready" : snapshot.InitDetail);
            Add(rows, "Boot", "Network reachability", ReachabilitySeverity(), Application.internetReachability.ToString());

            Add(rows, "Config", "SorollaConfig", config != null ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Fail,
                config != null ? "Loaded from Resources" : "Missing Assets/Resources/SorollaConfig.asset");
            Add(rows, "Config", "Adjust token", ConfigPresence(config?.adjustAppToken, fullMode, snapshot.AdjustMissingToken));
            Add(rows, "Config", "Adjust environment", AdjustEnvironmentSeverity(config, fullMode),
                AdjustEnvironmentDetail(config, fullMode));
            Add(rows, "Config", "Rewarded ad unit", ConfigPresence(config?.rewardedAdUnit?.Current, fullMode, false));
            Add(rows, "Config", "Interstitial ad unit", ConfigPresence(config?.interstitialAdUnit?.Current, fullMode, false));
            Add(rows, "Config", "Purchase event token", ConfigPresence(config?.adjustPurchaseEventToken, fullMode, false));

#if GAMEANALYTICS_INSTALLED
            Add(rows, "SDKs", "GameAnalytics", snapshot.GaInitialized ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting,
                snapshot.GaInitialized ? "Initialized" : "Waiting for init log");
#else
            Add(rows, "SDKs", "GameAnalytics", SorollaDiagnosticSeverity.Fail, "Package not installed");
#endif

#if SOROLLA_FACEBOOK_ENABLED
            Add(rows, "SDKs", "Facebook", snapshot.FacebookFailed ? SorollaDiagnosticSeverity.Fail : snapshot.FacebookInitialized ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting,
                snapshot.FacebookFailed ? "Initialization failed" : snapshot.FacebookInitialized ? "Initialized" : "Waiting for init log");
#else
            Add(rows, "SDKs", "Facebook", SorollaDiagnosticSeverity.Info, "Package not installed");
#endif

#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            Add(rows, "SDKs", "MAX implementation", snapshot.MaxRegistered ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting,
                snapshot.MaxRegistered ? "Registered" : "Waiting for adapter registration");
            Add(rows, "SDKs", "MAX initialized", snapshot.MaxInitialized ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting,
                snapshot.MaxInitialized ? "Initialized" : "Waiting for MAX callback");
#else
            Add(rows, "SDKs", "MAX", fullMode ? SorollaDiagnosticSeverity.Fail : SorollaDiagnosticSeverity.Info,
                fullMode ? "Package missing for Full mode" : "Not installed");
#endif

#if SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
            Add(rows, "SDKs", "Adjust implementation", snapshot.AdjustRegistered ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting,
                snapshot.AdjustRegistered ? "Registered" : "Waiting for adapter registration");
            Add(rows, "SDKs", "Adjust initialized", AdjustRuntimeSeverity(snapshot, fullMode),
                AdjustRuntimeDetail(snapshot, fullMode));
#else
            Add(rows, "SDKs", "Adjust", fullMode ? SorollaDiagnosticSeverity.Fail : SorollaDiagnosticSeverity.Info,
                fullMode ? "Package missing for Full mode" : "Not installed");
#endif

#if FIREBASE_ANALYTICS_INSTALLED
            Add(rows, "Firebase", "Core", snapshot.FirebaseCoreReady || FirebaseCoreManager.IsInitialized ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting,
                snapshot.FirebaseCoreReady || FirebaseCoreManager.IsInitialized ? "Ready" : "Waiting for Firebase Core");
            Add(rows, "Firebase", "Analytics", snapshot.FirebaseAnalyticsReady || FirebaseAdapter.IsReady ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting,
                snapshot.FirebaseAnalyticsReady || FirebaseAdapter.IsReady ? "Ready" : "Waiting for Firebase Analytics");
#else
            Add(rows, "Firebase", "Analytics", fullMode ? SorollaDiagnosticSeverity.Fail : SorollaDiagnosticSeverity.Info,
                fullMode ? "Package missing for Full mode" : "Not installed");
#endif

#if FIREBASE_CRASHLYTICS_INSTALLED
            Add(rows, "Firebase", "Crashlytics", snapshot.CrashlyticsReady || FirebaseCrashlyticsAdapter.IsReady ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting,
                snapshot.CrashlyticsReady || FirebaseCrashlyticsAdapter.IsReady ? "Initialized" : "Waiting for init");
#else
            Add(rows, "Firebase", "Crashlytics", fullMode ? SorollaDiagnosticSeverity.Fail : SorollaDiagnosticSeverity.Info,
                fullMode ? "Package missing for Full mode" : "Not installed");
#endif

#if FIREBASE_REMOTE_CONFIG_INSTALLED
            Add(rows, "Firebase", "Remote Config", RemoteConfigSeverity(snapshot),
                snapshot.RemoteConfigFetchSeen ? snapshot.RemoteConfigDetail : "Waiting for fetch");
#else
            Add(rows, "Firebase", "Remote Config", fullMode ? SorollaDiagnosticSeverity.Fail : SorollaDiagnosticSeverity.Info,
                fullMode ? "Package missing for Full mode" : "Not installed");
#endif

            Add(rows, "Consent", "MAX consent", ConsentSeverity(snapshot),
                snapshot.MaxConsentSeen ? snapshot.MaxConsentDetail : "Waiting for consent status");
            Add(rows, "Consent", "Can request ads", Palette.CanRequestAds ? SorollaDiagnosticSeverity.Pass : fullMode ? SorollaDiagnosticSeverity.Warning : SorollaDiagnosticSeverity.Info,
                Palette.CanRequestAds ? "True" : fullMode ? "False" : "Not applicable");
            Add(rows, "Consent", "ATT", AttSeverity(), Palette.AttStatus.ToString());
            AddObserved(rows, "Identity", AdvertisingIdLabel(), AdvertisingIdSeverity(snapshot), AdvertisingIdDetail(snapshot));
            AddObserved(rows, "Identity", "Adjust ADID", AdjustIdSeverity(snapshot, fullMode), AdjustIdDetail(snapshot, fullMode));
            AddObserved(rows, "Identity", "Attribution", AttributionSeverity(snapshot, fullMode), AttributionDetail(snapshot));

            AddObserved(rows, "Activity", "Progression start/end", CountPairSeverity(snapshot.ProgressionStartCount, snapshot.ProgressionEndCount),
                $"{snapshot.ProgressionStartCount} start / {snapshot.ProgressionEndCount} end observed");
            AddObserved(rows, "Activity", "Economy earn/spend", CountPairSeverity(snapshot.EconomyEarnCount, snapshot.EconomySpendCount),
                $"{snapshot.EconomyEarnCount} earn / {snapshot.EconomySpendCount} spend observed");
            AddObserved(rows, "Activity", "Custom events", snapshot.CustomEventCount > 0 ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Info,
                snapshot.CustomEventCount > 0 ? $"{snapshot.CustomEventCount} observed, last={snapshot.LastCustomEvent}" : "None observed");
            AddObserved(rows, "Activity", "IAP tracking attached", snapshot.PurchaseTrackingAttached ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Info,
                snapshot.PurchaseTrackingAttached ? "AttachPurchaseTracking wired" : "Waiting for store controller wiring");
            AddObserved(rows, "Activity", "Purchase accepted", snapshot.PurchaseAcceptedCount > 0 ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Info,
                snapshot.PurchaseAcceptedCount > 0 ? $"{snapshot.PurchaseAcceptedCount} purchase event(s)" : "No purchase observed");
            AddObserved(rows, "Activity", "Purchase verification", PurchaseVerificationSeverity(snapshot), snapshot.PurchaseVerification);
            AddObserved(rows, "Activity", "Purchase issues", snapshot.PurchaseIssue == "No issue observed" ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Warning,
                snapshot.PurchaseDuplicateCount > 0 ? $"{snapshot.PurchaseIssue}; duplicates={snapshot.PurchaseDuplicateCount}" : snapshot.PurchaseIssue);

            Add(rows, "Ads", "Interstitial", AdSeverity(snapshot.InterstitialReady, snapshot.InterstitialLoadStarted,
                    snapshot.InterstitialLoadFailed, snapshot.InterstitialLoaded, snapshot.InterstitialCompleted, snapshot.MaxInitialized),
                AdDetail(snapshot.InterstitialReady, snapshot.InterstitialLoadStarted, snapshot.InterstitialLoadFailed,
                    snapshot.InterstitialLoaded, snapshot.InterstitialCompleted, snapshot.InterstitialLoadIssue, snapshot.MaxInitialized));
            Add(rows, "Ads", "Rewarded", AdSeverity(snapshot.RewardedReady, snapshot.RewardedLoadStarted,
                    snapshot.RewardedLoadFailed, snapshot.RewardedLoaded, snapshot.RewardedCompleted, snapshot.MaxInitialized),
                AdDetail(snapshot.RewardedReady, snapshot.RewardedLoadStarted, snapshot.RewardedLoadFailed,
                    snapshot.RewardedLoaded, snapshot.RewardedCompleted, snapshot.RewardedLoadIssue, snapshot.MaxInitialized));
            AddObserved(rows, "Ads", "Ad revenue", snapshot.AdRevenueSeen ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Info,
                snapshot.AdRevenueSeen ? "Observed" : "No revenue callback observed");
            Add(rows, "Ads", "Ad issues", snapshot.LastAdIssue == "No issue observed" ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Warning,
                snapshot.LastAdIssue);

            Add(rows, "Red flags", "SDK warnings", snapshot.PaletteWarningCount == 0 ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Warning,
                snapshot.PaletteWarningCount == 0 ? "None" : $"{snapshot.PaletteWarningCount}, last={snapshot.LastPaletteWarning}");
            Add(rows, "Red flags", "SDK errors", snapshot.PaletteErrorCount == 0 ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Fail,
                snapshot.PaletteErrorCount == 0 ? "None" : $"{snapshot.PaletteErrorCount}, last={snapshot.LastPaletteError}");
            Add(rows, "Red flags", RuntimeProblemsRowName, RuntimeProblemSeverity(snapshot), RuntimeProblemDetail(snapshot));
        }

        static SorollaConfig LoadConfig()
        {
            return Palette.Config != null
                ? Palette.Config
                : Resources.Load<SorollaConfig>("SorollaConfig");
        }

        static Snapshot CaptureSnapshot()
        {
            lock (s_lock)
            {
                return new Snapshot
                {
                    AutoInitSeen = s_autoInitSeen,
                    InitializeSeen = s_initializeSeen,
                    ReadySeen = s_readySeen,
                    ModeKnown = s_modeKnown,
                    FullMode = s_fullMode,
                    InitDetail = s_initDetail,
                    GaInitialized = s_gaInitialized,
                    FacebookInitialized = s_facebookInitialized,
                    FacebookFailed = s_facebookFailed,
                    MaxRegistered = s_maxRegistered || MaxAdapter.IsRegistered,
                    MaxInitialized = s_maxInitialized || MaxAdapter.IsInitialized,
                    MaxConsentSeen = s_maxConsentSeen,
                    MaxConsentDetail = s_maxConsentDetail,
                    AdjustRegistered = s_adjustRegistered || AdjustAdapter.IsRegistered,
                    AdjustInitializing = s_adjustInitializing,
                    AdjustInitialized = s_adjustInitialized || AdjustAdapter.IsInitialized,
                    AdjustMissingToken = s_adjustMissingToken,
                    AdjustEnvironment = s_adjustEnvironment,
                    FirebaseCoreReady = s_firebaseCoreReady,
                    FirebaseAnalyticsReady = s_firebaseAnalyticsReady,
                    CrashlyticsReady = s_crashlyticsReady,
                    RemoteConfigFetchSeen = s_remoteConfigFetchSeen,
                    RemoteConfigFetchSuccess = s_remoteConfigFetchSuccess,
                    RemoteConfigDetail = s_remoteConfigDetail,
                    PurchaseTrackingAttached = s_purchaseTrackingAttached,
                    PurchaseAcceptedCount = s_purchaseAcceptedCount,
                    PurchaseDuplicateCount = s_purchaseDuplicateCount,
                    PurchaseIssue = s_purchaseIssue,
                    PurchaseVerification = s_purchaseVerification,
                    ProgressionStartCount = s_progressionStartCount,
                    ProgressionEndCount = s_progressionEndCount,
                    EconomyEarnCount = s_economyEarnCount,
                    EconomySpendCount = s_economySpendCount,
                    CustomEventCount = s_customEventCount,
                    LastCustomEvent = s_lastCustomEvent,
                    RewardedReady = Palette.IsRewardedAdReady,
                    RewardedLoadStarted = MaxAdapter.HasRewardedLoadStarted,
                    RewardedLoadFailed = MaxAdapter.HasRewardedLoadFailed,
                    RewardedLoadIssue = MaxAdapter.LastRewardedLoadIssue,
                    RewardedLoaded = s_rewardedLoaded,
                    RewardedCompleted = s_rewardedCompleted,
                    InterstitialReady = Palette.IsInterstitialAdReady,
                    InterstitialLoadStarted = MaxAdapter.HasInterstitialLoadStarted,
                    InterstitialLoadFailed = MaxAdapter.HasInterstitialLoadFailed,
                    InterstitialLoadIssue = MaxAdapter.LastInterstitialLoadIssue,
                    InterstitialLoaded = s_interstitialLoaded,
                    InterstitialCompleted = s_interstitialCompleted,
                    AdRevenueSeen = s_adRevenueSeen,
                    LastAdIssue = s_lastAdIssue,
                    PaletteWarningCount = s_paletteWarningCount,
                    PaletteErrorCount = s_paletteErrorCount,
                    LastPaletteWarning = s_lastPaletteWarning,
                    LastPaletteError = s_lastPaletteError,
                    RuntimeProblemUniqueCount = s_runtimeProblems.Count,
                    RuntimeProblemTotalCount = RuntimeProblemTotalCount(),
                    RuntimeProblemSummary = RuntimeProblemHeadline(),
                    RuntimeProblemSeverity = HighestRuntimeProblemSeverity(),
                    AdIdRequested = s_adIdRequested,
                    AdIdReceived = s_adIdReceived,
                    AdIdPresent = s_adIdPresent,
                    AdIdZeroed = s_adIdZeroed,
                    AdIdRequestTime = s_adIdRequestTime,
                    AdjustIdRequested = s_adjustIdRequested,
                    AdjustIdReceived = s_adjustIdReceived,
                    AdjustIdPresent = s_adjustIdPresent,
                    AdjustIdRequestTime = s_adjustIdRequestTime,
                    AttributionRequestTime = s_attributionRequestTime,
                    AttributionSummary = s_attributionSummary,
                    ConsentSignalsKnown = s_consentSignalsKnown,
                    AdStorageConsent = s_adStorageConsent,
                    AdPersonalizationConsent = s_adPersonalizationConsent,
                    AdUserDataConsent = s_adUserDataConsent,
                    AnalyticsStorageConsent = s_analyticsStorageConsent,
                    ConsentFormShownThisSession = s_consentFormShownThisSession,
                };
            }
        }

        // Builds the QA bridge snapshot model. MUST run on the Unity main thread: it reads Palette
        // surfaces (consent, ATT), PlayerPrefs/SharedPreferences (IABTCF), and the bridge arm state.
        internal static SorollaQaState CaptureQaState()
        {
            SorollaConfig config = LoadConfig();
            Snapshot snap = CaptureSnapshot();
            bool fullMode = IsFullMode(config, snap);

            ReadIabtcf(out bool tcStringPresent, out string purposeConsents);

            return new SorollaQaState
            {
                SdkVersion = Palette.SdkVersion,
                Mode = ModeForSnapshot(config, snap),
                DevelopmentBuild = Debug.isDebugBuild,
                BridgeArmed = QaBridgeServer.IsArmed,
                Ready = Palette.IsInitialized || snap.ReadySeen,

                ConsentStatus = Palette.ConsentStatus.ToString(),
                ConsentGeography = ConsentGeography(Palette.ConsentStatus),
                Att = Palette.AttString(Palette.AttStatus),
                CanRequestAds = Palette.CanRequestAds,
                ConsentFormShownThisSession = snap.ConsentFormShownThisSession,
                ConsentSignalsKnown = snap.ConsentSignalsKnown,
                AdStorageConsent = snap.AdStorageConsent,
                AdPersonalizationConsent = snap.AdPersonalizationConsent,
                AdUserDataConsent = snap.AdUserDataConsent,
                AnalyticsStorageConsent = snap.AnalyticsStorageConsent,
                TcStringPresent = tcStringPresent,
                PurposeConsents = purposeConsents,

                MaxAdapter = MaxAdapterStatus(snap, fullMode),
                AdjustAdapter = AdjustAdapterStatus(snap, fullMode),
                FirebaseAdapter = FirebaseAdapterStatus(snap, fullMode),
                GameAnalyticsAdapter = GameAnalyticsAdapterStatus(snap),
                FacebookAdapter = FacebookAdapterStatus(snap),

                AdvertisingIdPresent = snap.AdIdPresent,
                AdvertisingIdZeroed = snap.AdIdZeroed,
                AdjustAdidPresent = snap.AdjustIdPresent,
                AttributionNetwork = snap.AttributionSummary,
                AdjustEnvironment = snap.AdjustEnvironment,

                InterstitialLoaded = snap.InterstitialLoaded,
                InterstitialCompleted = snap.InterstitialCompleted,
                RewardedLoaded = snap.RewardedLoaded,
                RewardedCompleted = snap.RewardedCompleted,
                AdRevenueSeen = snap.AdRevenueSeen,

                SdkWarningCount = snap.PaletteWarningCount,
                SdkErrorCount = snap.PaletteErrorCount,
                LastSdkError = snap.LastPaletteError,
                RuntimeProblemUniqueCount = snap.RuntimeProblemUniqueCount,
                RuntimeProblemTotalCount = snap.RuntimeProblemTotalCount,
                RuntimeProblemSummary = snap.RuntimeProblemSummary,
            };
        }

        static string ModeForSnapshot(SorollaConfig config, Snapshot snapshot)
        {
            if (config == null && !snapshot.ModeKnown) return "unknown";
            return IsFullMode(config, snapshot) ? "full" : "prototype";
        }

        // GDPR applicability derived from the resolved UMP consent status. NotApplicable is the UMP
        // signal for a non-GDPR geography; Required/Obtained/Denied all mean GDPR applies.
        static string ConsentGeography(Adapters.ConsentStatus status)
        {
            switch (status)
            {
                case Adapters.ConsentStatus.NotApplicable:
                    return "non_gdpr";
                case Adapters.ConsentStatus.Obtained:
                case Adapters.ConsentStatus.Required:
                case Adapters.ConsentStatus.Denied:
                    return "gdpr";
                default:
                    return "unknown";
            }
        }

        static string MaxAdapterStatus(Snapshot snapshot, bool fullMode)
        {
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            if (snapshot.MaxInitialized) return "ready";
            if (snapshot.MaxRegistered) return "registered";
            return "waiting";
#else
            return fullMode ? "missing" : "not_installed";
#endif
        }

        static string AdjustAdapterStatus(Snapshot snapshot, bool fullMode)
        {
#if SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
            if (!fullMode) return "not_required";
            if (snapshot.AdjustMissingToken) return "missing_token";
            if (snapshot.AdjustInitialized) return $"enabled({snapshot.AdjustEnvironment.ToLowerInvariant()})";
            if (snapshot.AdjustInitializing) return "initializing";
            if (snapshot.AdjustRegistered) return "registered";
            return "waiting";
#else
            return fullMode ? "missing" : "not_installed";
#endif
        }

        static string FirebaseAdapterStatus(Snapshot snapshot, bool fullMode)
        {
#if FIREBASE_ANALYTICS_INSTALLED
            return snapshot.FirebaseAnalyticsReady || FirebaseAdapter.IsReady ? "ready" : "waiting";
#else
            return fullMode ? "missing" : "not_installed";
#endif
        }

        static string GameAnalyticsAdapterStatus(Snapshot snapshot)
        {
#if GAMEANALYTICS_INSTALLED
            return snapshot.GaInitialized ? "ready" : "waiting";
#else
            return "not_installed";
#endif
        }

        static string FacebookAdapterStatus(Snapshot snapshot)
        {
#if SOROLLA_FACEBOOK_ENABLED
            if (snapshot.FacebookFailed) return "failed";
            return snapshot.FacebookInitialized ? "ready" : "waiting";
#else
            return "not_installed";
#endif
        }

        // IAB TCF v2 strings. iOS CMPs and the Editor store them in standard user defaults, which Unity
        // maps to PlayerPrefs. Android stores them in the default SharedPreferences (per the IAB spec),
        // reachable only via JNI. Only the TC-string PRESENCE is exposed (never the string); purpose
        // bits are the consent decision a gate checks, not PII.
        static void ReadIabtcf(out bool tcStringPresent, out string purposeConsents)
        {
            tcStringPresent = false;
            purposeConsents = "";
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var context = activity.Call<AndroidJavaObject>("getApplicationContext");
                using var prefsManager = new AndroidJavaClass("android.preference.PreferenceManager");
                using var prefs = prefsManager.CallStatic<AndroidJavaObject>("getDefaultSharedPreferences", context);
                string tcf = prefs.Call<string>("getString", "IABTCF_TCString", null);
                purposeConsents = prefs.Call<string>("getString", "IABTCF_PurposeConsents", null) ?? "";
                tcStringPresent = !string.IsNullOrEmpty(tcf);
#else
                string tcf = PlayerPrefs.GetString("IABTCF_TCString", "");
                purposeConsents = PlayerPrefs.GetString("IABTCF_PurposeConsents", "");
                tcStringPresent = !string.IsNullOrEmpty(tcf);
#endif
            }
            catch (Exception e)
            {
                PaletteLog.Verbose($"[Palette] QA snapshot IABTCF read failed: {e.Message}");
            }
        }

        static int RuntimeProblemTotalCount()
        {
            int total = 0;
            for (int i = 0; i < s_runtimeProblems.Count; i++)
                total += s_runtimeProblems[i].Count;
            return total;
        }

        static string RuntimeProblemHeadline()
        {
            if (s_runtimeProblems.Count == 0) return "None observed";

            SorollaRuntimeProblem top = s_runtimeProblems[0];
            for (int i = 1; i < s_runtimeProblems.Count; i++)
            {
                SorollaRuntimeProblem candidate = s_runtimeProblems[i];
                if (SeverityRank(candidate.Severity) > SeverityRank(top.Severity)
                    || SeverityRank(candidate.Severity) == SeverityRank(top.Severity) && candidate.LastTimeSeconds > top.LastTimeSeconds)
                    top = candidate;
            }

            return RuntimeProblemSummary(top);
        }

        static SorollaDiagnosticSeverity HighestRuntimeProblemSeverity()
        {
            SorollaDiagnosticSeverity severity = SorollaDiagnosticSeverity.Pass;
            for (int i = 0; i < s_runtimeProblems.Count; i++)
            {
                if (SeverityRank(s_runtimeProblems[i].Severity) > SeverityRank(severity))
                    severity = s_runtimeProblems[i].Severity;
            }

            return severity;
        }

        static int SeverityRank(SorollaDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case SorollaDiagnosticSeverity.Fail:
                    return 4;
                case SorollaDiagnosticSeverity.Warning:
                    return 3;
                case SorollaDiagnosticSeverity.Waiting:
                    return 2;
                case SorollaDiagnosticSeverity.Pass:
                    return 1;
                default:
                    return 0;
            }
        }

        internal static string BuildSummary()
        {
            var rows = new List<SorollaDiagnosticRow>(64);
            BuildRows(rows);

            var sb = new StringBuilder(4096);
            AppendReportHeader(sb, "Sorolla Palette Vitals", BuildReportDetail());

            string currentGroup = null;
            foreach (SorollaDiagnosticRow row in rows)
            {
                if (row.Group != currentGroup)
                {
                    currentGroup = row.Group;
                    sb.AppendLine($"[{currentGroup}]");
                }

                sb.AppendLine($"{SeverityLabel(row.Severity),-7} {row.Name}: {row.Detail}");
            }

            sb.AppendLine();
            sb.AppendLine("[Runtime Problems]");
            AppendRuntimeProblems(sb);

            sb.AppendLine();
            sb.AppendLine("[Recent Events]");
            AppendEventLog(sb);

            return sb.ToString();
        }

        internal static string BuildProblemsSummary()
        {
            var rows = new List<SorollaDiagnosticRow>(64);
            BuildRows(rows);

            var sb = new StringBuilder(2048);
            AppendReportHeader(sb, "Sorolla Vitals Problems", BuildReportDetail(rows));

            bool any = false;
            foreach (SorollaDiagnosticRow row in rows)
            {
                if (!NeedsAttention(row.Severity)) continue;

                any = true;
                sb.AppendLine($"{SeverityLabel(row.Severity),-7} [{row.Group}] {row.Name}: {row.Detail}");
            }

            if (!any)
                sb.AppendLine("No FAIL/WARN/WAIT diagnostics observed.");

            return sb.ToString();
        }

        internal static string BuildConsoleSummary()
        {
            var rows = new List<SorollaDiagnosticRow>(64);
            BuildRows(rows);

            var sb = new StringBuilder(2048);
            AppendReportHeader(sb, "Sorolla Vitals Console", BuildReportDetail(rows));
            sb.AppendLine("[Runtime Problems]");
            AppendRuntimeProblems(sb);
            sb.AppendLine();
            sb.AppendLine("[Events]");
            AppendEventLog(sb);
            return sb.ToString();
        }

        internal static string BuildHeaderContext()
        {
            SorollaConfig config = LoadConfig();
            Snapshot snapshot = CaptureSnapshot();
            bool fullMode = IsFullMode(config, snapshot);

            var sb = new StringBuilder(192);
            AppendContextPart(sb, "SDK " + Palette.SdkVersion);
            AppendContextPart(sb, ModeShortLabel(config, snapshot));
            AppendContextPart(sb, AdjustEnvironmentHeaderLabel(config, fullMode));
            AppendContextPart(sb, ConsentHeaderLabel(fullMode));
            if (Palette.VerboseLogging)
                AppendContextPart(sb, "Verbose logs");
            return sb.ToString();
        }

        internal static bool IsProblemSeverity(SorollaDiagnosticSeverity severity)
        {
            return severity == SorollaDiagnosticSeverity.Fail
                || severity == SorollaDiagnosticSeverity.Warning;
        }

        internal static bool NeedsAttention(SorollaDiagnosticSeverity severity)
        {
            return IsProblemSeverity(severity)
                || severity == SorollaDiagnosticSeverity.Waiting;
        }

        internal static bool DrivesHealth(SorollaDiagnosticRow row)
        {
            return row.Kind == SorollaDiagnosticKind.Required;
        }

        internal static string KindLabel(SorollaDiagnosticKind kind)
        {
            switch (kind)
            {
                case SorollaDiagnosticKind.Observed:
                    return "OBS";
                case SorollaDiagnosticKind.Context:
                    return "CTX";
                default:
                    return "REQ";
            }
        }

        static void AppendReportHeader(StringBuilder sb, string title, string buildDetail)
        {
            sb.AppendLine(title);
            sb.AppendLine($"App: {Application.identifier} {Application.version}");
            sb.AppendLine($"Platform: {Application.platform} | Unity: {Application.unityVersion}");
            sb.AppendLine(buildDetail);
            sb.AppendLine($"Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z");
            sb.AppendLine();
        }

        static string BuildReportDetail()
        {
            return $"Build: {(Debug.isDebugBuild ? "Development" : "Release")} | {BuildHeaderContext()}";
        }

        static string BuildReportDetail(List<SorollaDiagnosticRow> rows)
        {
            return $"Build: {(Debug.isDebugBuild ? "Development" : "Release")} | Mode: {FindDetail(rows, "Boot", "Palette mode")} | Env: {FindDetail(rows, "Config", "Adjust environment")}";
        }

        static string FindDetail(List<SorollaDiagnosticRow> rows, string group, string name)
        {
            foreach (SorollaDiagnosticRow row in rows)
            {
                if (row.Group == group && row.Name == name)
                    return row.Detail;
            }

            return "Unknown";
        }

        static void AppendEventLog(StringBuilder sb)
        {
            var events = new List<SorollaDiagnosticEventLogEntry>(MaxEventLogEntries);
            CopyEventLog(events);
            if (events.Count == 0)
            {
                sb.AppendLine("None observed");
            }
            else
            {
                for (int i = events.Count - 1; i >= 0; i--)
                {
                    SorollaDiagnosticEventLogEntry entry = events[i];
                    sb.AppendLine($"{FormatEventTime(entry.TimeSeconds),8} [{entry.Source}] {entry.Name} {entry.Payload}");
                }
            }
        }

        static void AppendRuntimeProblems(StringBuilder sb)
        {
            var problems = new List<SorollaRuntimeProblem>(MaxRuntimeProblemEntries);
            CopyRuntimeProblems(problems);
            if (problems.Count == 0)
            {
                sb.AppendLine("None observed");
                return;
            }

            for (int i = problems.Count - 1; i >= 0; i--)
            {
                SorollaRuntimeProblem problem = problems[i];
                sb.AppendLine($"{SeverityLabel(problem.Severity),-7} {FormatEventTime(problem.LastTimeSeconds),8} [{problem.Source}] {problem.Type} x{problem.Count}: {problem.Message}");
                sb.AppendLine($"        {problem.TopFrame}");
            }
        }

        internal static string SeverityLabel(SorollaDiagnosticSeverity severity) => severity switch
        {
            SorollaDiagnosticSeverity.Pass => "PASS",
            SorollaDiagnosticSeverity.Warning => "WARN",
            SorollaDiagnosticSeverity.Fail => "FAIL",
            SorollaDiagnosticSeverity.Waiting => "WAIT",
            _ => "INFO",
        };

        static void RecordPaletteLog(string message, LogType type)
        {
            if (string.IsNullOrEmpty(message)) return;

            lock (s_lock)
            {
                if (type == LogType.Warning)
                {
                    s_paletteWarningCount++;
                    s_lastPaletteWarning = SafeDetail(message);
                }
                else if (type == LogType.Error || type == LogType.Exception)
                {
                    s_paletteErrorCount++;
                    s_lastPaletteError = SafeDetail(message);
                }

                ParsePaletteLog(message);
            }
        }

        static void RecordUnityLog(string message, string stackTrace, LogType type)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (message.StartsWith("[Palette", StringComparison.Ordinal)) return;

            bool isException = type == LogType.Exception || type == LogType.Error;
            bool isNullReference = message.IndexOf("NullReferenceException", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isFatal = message.IndexOf("FATAL EXCEPTION", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("F AndroidRuntime", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isException && !isNullReference && !isFatal) return;

            lock (s_lock)
            {
                RecordRuntimeProblem(message, stackTrace, type, isNullReference, isFatal);
            }
        }

        static void RecordRuntimeProblem(string message, string stackTrace, LogType type, bool isNullReference, bool isFatal)
        {
            float now = Time.realtimeSinceStartup;
            string safeMessage = SafeDetail(FirstLine(message));
            string safeStack = FormatStackTrace(stackTrace);
            string problemType = RuntimeProblemType(message, type, isNullReference, isFatal);
            string source = RuntimeProblemSource(message, stackTrace);
            string topFrame = RuntimeProblemTopFrame(stackTrace);
            string fingerprint = RuntimeProblemFingerprint(problemType, safeMessage, topFrame);

            for (int i = 0; i < s_runtimeProblems.Count; i++)
            {
                SorollaRuntimeProblem existing = s_runtimeProblems[i];
                if (existing.Fingerprint != fingerprint) continue;

                int nextCount = existing.Count + 1;
                SorollaDiagnosticSeverity severity = RuntimeProblemSeverity(problemType, source, isNullReference, isFatal, nextCount);
                s_runtimeProblems[i] = existing.WithRepeat(now, severity);
                return;
            }

            if (s_runtimeProblems.Count >= MaxRuntimeProblemEntries)
                s_runtimeProblems.RemoveAt(0);

            SorollaDiagnosticSeverity initialSeverity = RuntimeProblemSeverity(problemType, source, isNullReference, isFatal, 1);
            var problem = new SorollaRuntimeProblem(
                unchecked(++s_nextRuntimeProblemId),
                fingerprint,
                now,
                now,
                1,
                initialSeverity,
                source,
                problemType,
                safeMessage,
                topFrame,
                safeStack);
            s_runtimeProblems.Add(problem);
        }

        static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int newline = text.IndexOfAny(new[] { '\n', '\r' });
            return newline < 0 ? text : text.Substring(0, newline);
        }

        static string RuntimeProblemType(string message, LogType type, bool isNullReference, bool isFatal)
        {
            if (isFatal) return "Fatal";
            if (isNullReference) return "NullReferenceException";
            string firstLine = FirstLine(message).Trim();
            int colon = firstLine.IndexOf(':');
            string candidate = colon > 0 ? firstLine.Substring(0, colon).Trim() : firstLine;
            if (candidate.EndsWith("Exception", StringComparison.Ordinal) && candidate.Length <= 80)
                return candidate;
            return type.ToString();
        }

        static string RuntimeProblemSource(string message, string stackTrace)
        {
            string combined = (message ?? "") + "\n" + (stackTrace ?? "");
            if (combined.IndexOf("SorollaDiagnostics", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Vitals";
            if (combined.IndexOf("Sorolla.Palette", StringComparison.OrdinalIgnoreCase) >= 0
                || combined.IndexOf("[Palette", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Sorolla SDK";
            if (combined.IndexOf("MaxSdk", StringComparison.OrdinalIgnoreCase) >= 0
                || combined.IndexOf("AppLovin", StringComparison.OrdinalIgnoreCase) >= 0
                || combined.IndexOf("Adjust", StringComparison.OrdinalIgnoreCase) >= 0
                || combined.IndexOf("Firebase", StringComparison.OrdinalIgnoreCase) >= 0
                || combined.IndexOf("Facebook", StringComparison.OrdinalIgnoreCase) >= 0
                || combined.IndexOf("GameAnalytics", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Vendor SDK";
            if (combined.IndexOf("Assembly-CSharp", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Game";
            return "Unity/System";
        }

        static string RuntimeProblemTopFrame(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace)) return "No stack trace";

            string[] lines = stackTrace.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (IsUsefulRuntimeFrame(line))
                    return SafeSingleLine(line, 140);
            }

            return SafeSingleLine(lines[0].Trim(), 140);
        }

        static bool IsUsefulRuntimeFrame(string frame)
        {
            if (string.IsNullOrEmpty(frame)) return false;
            return frame.IndexOf("UnityEngine.", StringComparison.Ordinal) < 0
                && frame.IndexOf("UnityEditor.", StringComparison.Ordinal) < 0
                && frame.IndexOf("System.", StringComparison.Ordinal) < 0
                && frame.IndexOf("Application.CallLogCallback", StringComparison.Ordinal) < 0;
        }

        static string RuntimeProblemFingerprint(string type, string message, string topFrame)
        {
            return $"{type}|{message}|{topFrame}";
        }

        static string FormatStackTrace(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace)) return "No stack trace";

            string[] lines = stackTrace.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder(768);
            int written = 0;
            for (int i = 0; i < lines.Length && written < 12; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (written > 0) sb.AppendLine();
                sb.Append(SafeSingleLine(line, 160));
                written++;
            }

            return written == 0 ? "No stack trace" : sb.ToString();
        }

        static SorollaDiagnosticSeverity RuntimeProblemSeverity(string type, string source, bool isNullReference, bool isFatal,
            int count)
        {
            if (isFatal || isNullReference || source == "Sorolla SDK" || source == "Vitals" || count >= 3)
                return SorollaDiagnosticSeverity.Fail;
            return SorollaDiagnosticSeverity.Warning;
        }

        static string RuntimeProblemSummary(SorollaRuntimeProblem problem)
        {
            return $"{problem.Source}: {problem.Type} x{problem.Count} at {problem.TopFrame}";
        }

        static void ParsePaletteLog(string message)
        {
            if (message.Contains("[Palette] Auto-initializing")) s_autoInitSeen = true;

            if (message.Contains("[Palette] Initializing ("))
            {
                s_initializeSeen = true;
                s_modeKnown = true;
                s_fullMode = message.Contains("(Full mode");
                s_initDetail = SafeDetail(message);
            }

            if (message.Contains("[Palette] Ready!")) s_readySeen = true;

            if (message.Contains("[Palette:GA] Initializing") || message.Contains("[Palette:GA] Already initialized"))
                s_gaInitialized = true;

            if (message.Contains("[Palette:FB] Initialized")) s_facebookInitialized = true;
            if (message.Contains("[Palette:FB] Failed")) s_facebookFailed = true;

            if (message.Contains("[Palette:MAX] Implementation registered")) s_maxRegistered = true;
            if (message.Contains("[Palette:MAX] Initialized")) s_maxInitialized = true;
            if (message.Contains("[Palette:MAX] ConsentStatus:"))
            {
                s_maxConsentSeen = true;
                s_maxConsentDetail = SafeDetail(message);
            }

            if (message.Contains("[Palette] Consent summary:"))
            {
                s_maxConsentSeen = true;
                s_maxConsentDetail = SafeDetail(message);
            }

            if (message.Contains("[Palette:Adjust] Implementation registered")) s_adjustRegistered = true;
            if (message.Contains("[Palette] Initializing Adjust ("))
            {
                s_adjustInitializing = true;
                s_adjustEnvironment = message.Contains("Production") ? "Production" : message.Contains("Sandbox") ? "Sandbox" : "Unknown";
            }
            if (message.Contains("[Palette:Adjust] Initialized")) s_adjustInitialized = true;
            if (message.Contains("Adjust App Token not configured")) s_adjustMissingToken = true;

            if (message.Contains("[Palette:FirebaseCore] Ready")) s_firebaseCoreReady = true;
            if (message.Contains("[Palette:Firebase] Initialized")) s_firebaseAnalyticsReady = true;
            if (message.Contains("[Palette:Crashlytics] Initialized")) s_crashlyticsReady = true;
            if (message.Contains("[Palette:RemoteConfig] Fetch complete"))
            {
                s_remoteConfigFetchSeen = true;
                s_remoteConfigFetchSuccess = message.Contains("lastFetchStatus: Success");
                s_remoteConfigDetail = SafeDetail(message);
            }
            if (message.Contains("[Palette:RemoteConfig] Fetch failed"))
            {
                s_remoteConfigFetchSeen = true;
                s_remoteConfigFetchSuccess = false;
                s_remoteConfigDetail = SafeDetail(message);
            }

            if (message.Contains("[Palette:Purchasing] AttachPurchaseTracking: wired"))
                s_purchaseTrackingAttached = true;
            if (message.Contains("TrackPurchase: accepted")) s_purchaseAcceptedCount++;
            if (message.Contains("duplicate transactionId"))
            {
                s_purchaseDuplicateCount++;
                s_purchaseIssue = "Duplicate transaction dropped";
            }
            if (message.Contains("TrackPurchase") && (message.Contains("invalid metadata") || message.Contains("dropping event")))
                s_purchaseIssue = SafeDetail(message);
            if (message.Contains("purchase verification:"))
                s_purchaseVerification = SafeDetail(message);

            if (message.Contains("[Palette:MAX] Rewarded ad loaded")) s_rewardedLoaded = true;
            if (message.Contains("[Palette:MAX] Rewarded ad completed")) s_rewardedCompleted = true;
            if (message.Contains("[Palette:MAX] Interstitial ad loaded")) s_interstitialLoaded = true;
            if (message.Contains("[Palette:MAX] Interstitial ad completed")) s_interstitialCompleted = true;
            // Ad-revenue is now recorded directly via RecordAdRevenue (DR-09); the old verbose-only
            // "TrackAdRevenue:" log-sniff is removed so the Vitals row no longer depends on log level.
            if (message.Contains("[Palette:MAX]") &&
                (message.Contains("load failed") || message.Contains("display failed") || message.Contains("not ready")))
                s_lastAdIssue = SafeDetail(message);
        }

        static void Add(List<SorollaDiagnosticRow> rows, string group, string name, SorollaDiagnosticSeverity severity, string detail) =>
            Add(rows, group, name, severity, detail, SorollaDiagnosticKind.Required);

        static void Add(List<SorollaDiagnosticRow> rows, string group, string name, (SorollaDiagnosticSeverity severity, string detail) item) =>
            Add(rows, group, name, item.severity, item.detail, SorollaDiagnosticKind.Required);

        static void AddObserved(List<SorollaDiagnosticRow> rows, string group, string name, SorollaDiagnosticSeverity severity, string detail) =>
            Add(rows, group, name, severity, detail, SorollaDiagnosticKind.Observed);

        static void Add(List<SorollaDiagnosticRow> rows, string group, string name, SorollaDiagnosticSeverity severity, string detail,
            SorollaDiagnosticKind kind) =>
            rows.Add(new SorollaDiagnosticRow(group, name, severity, detail, kind));

        static void EnqueueEvent(string source, string eventName, IDictionary<string, object> parameters)
        {
            if (s_eventLog.Count >= MaxEventLogEntries)
                s_eventLog.Dequeue();

            SorollaDiagnosticPayloadLine[] payloadLines = BuildPayloadLines(parameters);
            s_eventLog.Enqueue(new SorollaDiagnosticEventLogEntry(
                unchecked(++s_nextEventId),
                Time.realtimeSinceStartup,
                string.IsNullOrEmpty(source) ? "event" : source,
                string.IsNullOrEmpty(eventName) ? "unnamed" : eventName,
                FormatPayload(payloadLines),
                payloadLines));
        }

        static SorollaDiagnosticPayloadLine[] BuildPayloadLines(IDictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return Array.Empty<SorollaDiagnosticPayloadLine>();

            var lines = new SorollaDiagnosticPayloadLine[parameters.Count];
            int index = 0;
            foreach (KeyValuePair<string, object> item in parameters)
            {
                string key = string.IsNullOrEmpty(item.Key) ? "unnamed" : item.Key;
                lines[index] = new SorollaDiagnosticPayloadLine(key, FormatPayloadValue(key, item.Value));
                index++;
            }
            return lines;
        }

        static string FormatPayload(SorollaDiagnosticPayloadLine[] lines)
        {
            if (lines == null || lines.Length == 0)
                return "{}";

            var sb = new StringBuilder(256);
            sb.Append('{');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(lines[i].Key);
                sb.Append('=');
                sb.Append(lines[i].Value);
            }
            sb.Append('}');
            return SafeSingleLine(sb.ToString(), 320);
        }

        static string FormatPayloadValue(string key, object value)
        {
            if (IsSensitivePayloadKey(key))
                return value == null ? "missing" : "present";
            if (value == null)
                return "null";
            if (value is bool b)
                return b ? "true" : "false";
            if (value is float f)
                return f.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            if (value is double d)
                return d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            if (value is decimal m)
                return m.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

            string text = value.ToString() ?? "";
            if (text.Length > 80)
                text = text.Substring(0, 79) + "...";
            return text.Replace('\n', ' ').Replace('\r', ' ');
        }

        static bool IsSensitivePayloadKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            string normalized = key.ToLowerInvariant();
            string compact = normalized.Replace("_", "").Replace("-", "");
            return normalized.Contains("token")
                || normalized.Contains("secret")
                || normalized.Contains("receipt")
                || compact.Contains("transactionid")
                || compact.Contains("purchasetoken")
                || normalized.Contains("tcf");
        }

        internal static string FormatEventTime(float timeSeconds)
        {
            if (timeSeconds < 0f) timeSeconds = 0f;

            int totalSeconds = Mathf.FloorToInt(timeSeconds);
            int hours = totalSeconds / 3600;
            int minutes = totalSeconds / 60 % 60;
            int seconds = totalSeconds % 60;
            int tenths = Mathf.FloorToInt((timeSeconds - totalSeconds) * 10f);

            if (hours > 0)
                return $"{hours:0}:{minutes:00}:{seconds:00}";

            return $"{minutes:00}:{seconds:00}.{tenths:0}";
        }

        static (SorollaDiagnosticSeverity severity, string detail) ConfigPresence(string value, bool required, bool forcedFail)
        {
            if (forcedFail) return (SorollaDiagnosticSeverity.Fail, "Missing or rejected by SDK");
            if (!required && string.IsNullOrEmpty(value)) return (SorollaDiagnosticSeverity.Info, "Not required");
            return string.IsNullOrEmpty(value)
                ? (SorollaDiagnosticSeverity.Fail, "Missing")
                : (SorollaDiagnosticSeverity.Pass, "Present");
        }

        static SorollaDiagnosticSeverity ModeSeverity(SorollaConfig config, Snapshot snapshot)
        {
            if (config == null && !snapshot.ModeKnown) return SorollaDiagnosticSeverity.Fail;
            bool full = IsFullMode(config, snapshot);
            return full ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Fail;
        }

        static string ModeDetail(SorollaConfig config, Snapshot snapshot)
        {
            if (config == null && !snapshot.ModeKnown) return "Config missing / mode unknown";
            return IsFullMode(config, snapshot) ? "Full mode" : "Prototype mode - QA greenlight blocker";
        }

        static bool IsFullMode(SorollaConfig config, Snapshot snapshot)
        {
            if (config != null) return !config.isPrototypeMode;
            return snapshot.ModeKnown && snapshot.FullMode;
        }

        static string ModeShortLabel(SorollaConfig config, Snapshot snapshot)
        {
            if (config == null && !snapshot.ModeKnown) return "Mode unknown";
            return IsFullMode(config, snapshot) ? "Full" : "Prototype";
        }

        static SorollaDiagnosticSeverity ReachabilitySeverity() =>
            Application.internetReachability == NetworkReachability.NotReachable
                ? SorollaDiagnosticSeverity.Fail
                : SorollaDiagnosticSeverity.Pass;

        static SorollaDiagnosticSeverity AdjustEnvironmentSeverity(SorollaConfig config, bool fullMode)
        {
            if (!fullMode) return SorollaDiagnosticSeverity.Info;
            if (config == null) return SorollaDiagnosticSeverity.Fail;
            if (!config.adjustSandboxMode) return SorollaDiagnosticSeverity.Pass;
            return Debug.isDebugBuild ? SorollaDiagnosticSeverity.Info : SorollaDiagnosticSeverity.Fail;
        }

        static string AdjustEnvironmentDetail(SorollaConfig config, bool fullMode)
        {
            if (!fullMode) return "Not required in Prototype";
            if (config == null) return "Config missing";
            if (!config.adjustSandboxMode) return "Production";
            return Debug.isDebugBuild ? "Sandbox (development)" : "Sandbox in release build";
        }

        static string AdjustEnvironmentHeaderLabel(SorollaConfig config, bool fullMode)
        {
            if (!fullMode) return "Adjust n/a";
            if (config == null) return "Adjust missing";
            return config.adjustSandboxMode ? "Adjust Sandbox" : "Adjust Production";
        }

        static string ConsentHeaderLabel(bool fullMode)
        {
            if (!fullMode) return "Consent n/a";
            if (Palette.CanRequestAds) return "Ads OK";

            switch (Palette.ConsentStatus)
            {
                case ConsentStatus.Required:
                    return "Consent Required";
                case ConsentStatus.Denied:
                    return "Consent Denied";
                case ConsentStatus.Unknown:
                    return "Consent Unknown";
                default:
                    return "Ads Blocked";
            }
        }

        static void AppendContextPart(StringBuilder sb, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            if (sb.Length > 0)
                sb.Append("  |  ");
            sb.Append(value);
        }

        static SorollaDiagnosticSeverity AdjustRuntimeSeverity(Snapshot snapshot, bool fullMode)
        {
            if (!fullMode) return SorollaDiagnosticSeverity.Info;
            if (snapshot.AdjustMissingToken) return SorollaDiagnosticSeverity.Fail;
            if (snapshot.AdjustInitialized) return SorollaDiagnosticSeverity.Pass;
            // Not yet initialized (waiting for MAX consent, or mid-init) — both surface as Waiting;
            // AdjustRuntimeDetail differentiates the two states for the human-readable string.
            return SorollaDiagnosticSeverity.Waiting;
        }

        static string AdjustRuntimeDetail(Snapshot snapshot, bool fullMode)
        {
            if (!fullMode) return "Not required in Prototype";
            if (snapshot.AdjustMissingToken) return "App token missing";
            if (snapshot.AdjustInitialized) return $"Initialized ({snapshot.AdjustEnvironment})";
            if (snapshot.AdjustInitializing) return $"Initializing ({snapshot.AdjustEnvironment})";
            return "Waiting for MAX consent before Adjust init";
        }

        static SorollaDiagnosticSeverity RemoteConfigSeverity(Snapshot snapshot)
        {
            if (snapshot.RemoteConfigFetchSeen)
                return snapshot.RemoteConfigFetchSuccess ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Warning;
            return SorollaDiagnosticSeverity.Waiting;
        }

        static SorollaDiagnosticSeverity ConsentSeverity(Snapshot snapshot)
        {
            string detail = snapshot.MaxConsentDetail ?? "";
            if (!snapshot.MaxConsentSeen) return SorollaDiagnosticSeverity.Waiting;
            if (detail.Contains("Obtained") || detail.Contains("NotApplicable") || detail.Contains("canRequestAds=True"))
                return SorollaDiagnosticSeverity.Pass;
            if (detail.Contains("Denied")) return SorollaDiagnosticSeverity.Warning;
            return SorollaDiagnosticSeverity.Waiting;
        }

        static SorollaDiagnosticSeverity AttSeverity()
        {
#if UNITY_IOS
            return Palette.AttStatus switch
            {
                ATT.ATTBridge.AuthorizationStatus.Authorized => SorollaDiagnosticSeverity.Pass,
                ATT.ATTBridge.AuthorizationStatus.NotDetermined => SorollaDiagnosticSeverity.Waiting,
                ATT.ATTBridge.AuthorizationStatus.Denied => SorollaDiagnosticSeverity.Warning,
                ATT.ATTBridge.AuthorizationStatus.Restricted => SorollaDiagnosticSeverity.Warning,
                _ => SorollaDiagnosticSeverity.Info,
            };
#else
            return SorollaDiagnosticSeverity.Info;
#endif
        }

        static string AdvertisingIdLabel()
        {
#if UNITY_IOS
            return "IDFA";
#elif UNITY_ANDROID
            return "GAID";
#else
            return "Advertising ID";
#endif
        }

        static SorollaDiagnosticSeverity AdvertisingIdSeverity(Snapshot snapshot)
        {
            if (!snapshot.AdIdRequested) return SorollaDiagnosticSeverity.Waiting;
            if (!snapshot.AdIdReceived && Time.realtimeSinceStartup - snapshot.AdIdRequestTime < 4f)
                return SorollaDiagnosticSeverity.Waiting;
            if (snapshot.AdIdZeroed) return SorollaDiagnosticSeverity.Fail;
            return snapshot.AdIdPresent ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Warning;
        }

        static string AdvertisingIdDetail(Snapshot snapshot)
        {
            if (!snapshot.AdIdRequested) return "Not requested yet";
            if (!snapshot.AdIdReceived && Time.realtimeSinceStartup - snapshot.AdIdRequestTime < 4f) return "Fetching";
            if (snapshot.AdIdZeroed) return "Zeroed advertising ID";
            return snapshot.AdIdPresent ? "Present" : "Missing or unavailable";
        }

        static SorollaDiagnosticSeverity AdjustIdSeverity(Snapshot snapshot, bool fullMode)
        {
            if (!fullMode) return SorollaDiagnosticSeverity.Info;
            if (!snapshot.AdjustIdRequested) return SorollaDiagnosticSeverity.Waiting;
            if (!snapshot.AdjustIdReceived && Time.realtimeSinceStartup - snapshot.AdjustIdRequestTime < 4f)
                return SorollaDiagnosticSeverity.Waiting;
            return snapshot.AdjustIdPresent ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Warning;
        }

        static string AdjustIdDetail(Snapshot snapshot, bool fullMode)
        {
            if (!fullMode) return "Not required in Prototype";
            if (!snapshot.AdjustIdRequested) return "Not requested yet";
            if (!snapshot.AdjustIdReceived && Time.realtimeSinceStartup - snapshot.AdjustIdRequestTime < 4f) return "Fetching";
            return snapshot.AdjustIdPresent ? "Present" : "Missing or unavailable";
        }

        static SorollaDiagnosticSeverity AttributionSeverity(Snapshot snapshot, bool fullMode)
        {
            if (!fullMode) return SorollaDiagnosticSeverity.Info;
            if (snapshot.AttributionSummary == "Not requested") return SorollaDiagnosticSeverity.Waiting;
            return snapshot.AttributionSummary == "Unavailable" || snapshot.AttributionSummary == "Network missing"
                ? SorollaDiagnosticSeverity.Warning
                : snapshot.AttributionSummary == "Fetching"
                    ? Time.realtimeSinceStartup - snapshot.AttributionRequestTime < 4f
                        ? SorollaDiagnosticSeverity.Waiting
                        : SorollaDiagnosticSeverity.Warning
                    : SorollaDiagnosticSeverity.Pass;
        }

        static string AttributionDetail(Snapshot snapshot)
        {
            if (snapshot.AttributionSummary != "Fetching") return snapshot.AttributionSummary;
            return Time.realtimeSinceStartup - snapshot.AttributionRequestTime < 4f ? "Fetching" : "Unavailable or callback timed out";
        }

        static SorollaDiagnosticSeverity CountPairSeverity(int first, int second)
        {
            if (first > 0 && second > 0) return SorollaDiagnosticSeverity.Pass;
            if (first > 0 || second > 0) return SorollaDiagnosticSeverity.Warning;
            return SorollaDiagnosticSeverity.Info;
        }

        static SorollaDiagnosticSeverity PurchaseVerificationSeverity(Snapshot snapshot)
        {
            if (snapshot.PurchaseVerification == "Not observed") return SorollaDiagnosticSeverity.Info;
            return snapshot.PurchaseVerification.Contains("success")
                ? SorollaDiagnosticSeverity.Pass
                : SorollaDiagnosticSeverity.Warning;
        }

        static SorollaDiagnosticSeverity AdSeverity(bool ready, bool loadStarted, bool loadFailed, bool loaded, bool completed,
            bool maxInitialized)
        {
            if (!maxInitialized) return SorollaDiagnosticSeverity.Waiting;
            if (completed) return SorollaDiagnosticSeverity.Pass;
            if (ready) return SorollaDiagnosticSeverity.Pass;
            if (loadFailed) return SorollaDiagnosticSeverity.Warning;
            if (loaded) return SorollaDiagnosticSeverity.Warning;
            if (loadStarted) return SorollaDiagnosticSeverity.Waiting;
            return SorollaDiagnosticSeverity.Info;
        }

        static string AdDetail(bool ready, bool loadStarted, bool loadFailed, bool loaded, bool completed, string loadIssue,
            bool maxInitialized)
        {
            if (!maxInitialized) return "Waiting for MAX initialization";
            if (completed) return "Shown and completed";
            if (ready) return "Ready to show";
            if (loadFailed) return string.IsNullOrEmpty(loadIssue) ? "Load failed; retrying with backoff" : $"{loadIssue}; retrying";
            if (loaded) return "Loaded, not completed yet";
            if (loadStarted) return "Requested; waiting for network/fill";
            return "No load requested";
        }

        static SorollaDiagnosticSeverity RuntimeProblemSeverity(Snapshot snapshot)
        {
            return snapshot.RuntimeProblemUniqueCount == 0 ? SorollaDiagnosticSeverity.Pass : snapshot.RuntimeProblemSeverity;
        }

        static string RuntimeProblemDetail(Snapshot snapshot)
        {
            if (snapshot.RuntimeProblemUniqueCount == 0) return "None observed";
            return $"{snapshot.RuntimeProblemUniqueCount} unique / {snapshot.RuntimeProblemTotalCount} total; top={snapshot.RuntimeProblemSummary}";
        }

        static bool IsZeroAdvertisingId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            string trimmed = id.Trim();
            return trimmed == "00000000-0000-0000-0000-000000000000";
        }

        static string SafeDetail(string message)
        {
            if (string.IsNullOrEmpty(message)) return "";
            const int maxLength = 140;
            return SafeSingleLine(message, maxLength);
        }

        static string SafeSingleLine(string message, int maxLength)
        {
            if (string.IsNullOrEmpty(message)) return "";
            string singleLine = message.Replace('\n', ' ').Replace('\r', ' ');
            return singleLine.Length <= maxLength ? singleLine : singleLine.Substring(0, maxLength - 1) + "...";
        }

        struct Snapshot
        {
            public bool AutoInitSeen;
            public bool InitializeSeen;
            public bool ReadySeen;
            public bool ModeKnown;
            public bool FullMode;
            public string InitDetail;
            public bool GaInitialized;
            public bool FacebookInitialized;
            public bool FacebookFailed;
            public bool MaxRegistered;
            public bool MaxInitialized;
            public bool MaxConsentSeen;
            public string MaxConsentDetail;
            public bool AdjustRegistered;
            public bool AdjustInitializing;
            public bool AdjustInitialized;
            public bool AdjustMissingToken;
            public string AdjustEnvironment;
            public bool FirebaseCoreReady;
            public bool FirebaseAnalyticsReady;
            public bool CrashlyticsReady;
            public bool RemoteConfigFetchSeen;
            public bool RemoteConfigFetchSuccess;
            public string RemoteConfigDetail;
            public bool PurchaseTrackingAttached;
            public int PurchaseAcceptedCount;
            public int PurchaseDuplicateCount;
            public string PurchaseIssue;
            public string PurchaseVerification;
            public int ProgressionStartCount;
            public int ProgressionEndCount;
            public int EconomyEarnCount;
            public int EconomySpendCount;
            public int CustomEventCount;
            public string LastCustomEvent;
            public bool RewardedReady;
            public bool RewardedLoadStarted;
            public bool RewardedLoadFailed;
            public string RewardedLoadIssue;
            public bool RewardedLoaded;
            public bool RewardedCompleted;
            public bool InterstitialReady;
            public bool InterstitialLoadStarted;
            public bool InterstitialLoadFailed;
            public string InterstitialLoadIssue;
            public bool InterstitialLoaded;
            public bool InterstitialCompleted;
            public bool AdRevenueSeen;
            public string LastAdIssue;
            public int PaletteWarningCount;
            public int PaletteErrorCount;
            public string LastPaletteWarning;
            public string LastPaletteError;
            public int RuntimeProblemUniqueCount;
            public int RuntimeProblemTotalCount;
            public string RuntimeProblemSummary;
            public SorollaDiagnosticSeverity RuntimeProblemSeverity;
            public bool AdIdRequested;
            public bool AdIdReceived;
            public bool AdIdPresent;
            public bool AdIdZeroed;
            public float AdIdRequestTime;
            public bool AdjustIdRequested;
            public bool AdjustIdReceived;
            public bool AdjustIdPresent;
            public float AdjustIdRequestTime;
            public float AttributionRequestTime;
            public string AttributionSummary;
            public bool ConsentSignalsKnown;
            public bool AdStorageConsent;
            public bool AdPersonalizationConsent;
            public bool AdUserDataConsent;
            public bool AnalyticsStorageConsent;
            public bool ConsentFormShownThisSession;
        }
    }
}
