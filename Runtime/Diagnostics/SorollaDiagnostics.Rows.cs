using System;
using System.Collections.Generic;
using System.Text;
using Sorolla.Palette.Adapters;
using UnityEngine;

namespace Sorolla.Palette
{
    internal static partial class SorollaDiagnostics
    {
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
            bool gameAnalyticsReady = GameAnalyticsAdapter.IsInitialized || snapshot.GaInitialized;
            Add(rows, "SDKs", "GameAnalytics",
                gameAnalyticsReady
                    ? SorollaDiagnosticSeverity.Pass
                    : AdapterRowSeverity(snapshot.GameAnalyticsOutcome, SorollaDiagnosticSeverity.Waiting),
                gameAnalyticsReady
                    ? "Ready"
                    : AdapterRowDetail(snapshot.GameAnalyticsOutcome, "Waiting for GameAnalytics initialization"));
#else
            Add(rows, "SDKs", "GameAnalytics", SorollaDiagnosticSeverity.Fail, "Package not installed");
#endif

#if SOROLLA_FACEBOOK_ENABLED
            // Readiness is gated on the managed Graph validation probe, not init alone. Init completing
            // only flips FacebookInitialized; the probe is what confirms the app credentials actually work.
            // Until the probe records Ready, Facebook stays Waiting, so a pending or never-returning probe
            // (offline, or a VPN/ad-blocker/private-DNS blocking the Graph domain) cannot false-green the row.
            bool facebookValidated = snapshot.FacebookOutcome.Seen
                && snapshot.FacebookOutcome.Status == AdapterDiagnosticStatus.Ready;
            SorollaDiagnosticSeverity facebookSeverity = snapshot.FacebookFailed ? SorollaDiagnosticSeverity.Fail :
                facebookValidated ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting;
            string facebookDetail = snapshot.FacebookFailed ? "Initialization failed" :
                facebookValidated ? "Initialized and validated" :
                snapshot.FacebookInitialized ? "Validating app credentials" : "Waiting for init callback";
            Add(rows, "SDKs", "Facebook",
                AdapterRowSeverity(snapshot.FacebookOutcome, facebookSeverity),
                AdapterRowDetail(snapshot.FacebookOutcome, facebookDetail));
#else
            Add(rows, "SDKs", "Facebook", SorollaDiagnosticSeverity.Info, "Package not installed");
#endif

#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            Add(rows, "SDKs", "MAX implementation", snapshot.MaxRegistered ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting,
                snapshot.MaxRegistered ? "Registered" : "Waiting for adapter registration");
            Add(rows, "SDKs", "MAX initialized",
                AdapterRowSeverity(snapshot.MaxOutcome, snapshot.MaxInitialized ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting),
                AdapterRowDetail(snapshot.MaxOutcome, snapshot.MaxInitialized ? "Initialized" : "Waiting for MAX callback"));
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
            bool firebaseCoreReady = snapshot.FirebaseCoreReady || FirebaseCoreManager.IsInitialized;
            bool firebaseAnalyticsReady = snapshot.FirebaseAnalyticsReady || FirebaseAdapter.IsReady;
            Add(rows, "Firebase", "Core",
                AdapterRowSeverity(snapshot.FirebaseCoreOutcome, firebaseCoreReady ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting),
                AdapterRowDetail(snapshot.FirebaseCoreOutcome, firebaseCoreReady ? "Ready" : "Waiting for Firebase Core"));
            Add(rows, "Firebase", "Analytics",
                AdapterRowSeverity(snapshot.FirebaseAnalyticsOutcome, firebaseAnalyticsReady ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting),
                AdapterRowDetail(snapshot.FirebaseAnalyticsOutcome, firebaseAnalyticsReady ? "Ready" : "Waiting for Firebase Analytics"));
#else
            Add(rows, "Firebase", "Analytics", fullMode ? SorollaDiagnosticSeverity.Fail : SorollaDiagnosticSeverity.Info,
                fullMode ? "Package missing for Full mode" : "Not installed");
#endif

#if FIREBASE_CRASHLYTICS_INSTALLED
            bool crashlyticsReady = snapshot.CrashlyticsReady || FirebaseCrashlyticsAdapter.IsReady;
            Add(rows, "Firebase", "Crashlytics",
                AdapterRowSeverity(snapshot.CrashlyticsOutcome, crashlyticsReady ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting),
                AdapterRowDetail(snapshot.CrashlyticsOutcome, crashlyticsReady ? "Initialized" : "Waiting for init"));
#else
            Add(rows, "Firebase", "Crashlytics", fullMode ? SorollaDiagnosticSeverity.Fail : SorollaDiagnosticSeverity.Info,
                fullMode ? "Package missing for Full mode" : "Not installed");
#endif

#if FIREBASE_REMOTE_CONFIG_INSTALLED
            RemoteConfigStatus remoteConfigStatus = Palette.RemoteConfigStatus;
            SorollaDiagnosticSeverity remoteConfigSeverity = remoteConfigStatus == RemoteConfigStatus.Defaults
                ? SorollaDiagnosticSeverity.Info
                : SorollaDiagnosticSeverity.Pass;
            string remoteConfigDetail = RemoteConfigRowDetail(remoteConfigStatus, snapshot);
            Add(rows, "Firebase", "Remote Config",
                AdapterRowSeverity(snapshot.RemoteConfigOutcome, remoteConfigSeverity),
                AdapterRowDetail(snapshot.RemoteConfigOutcome, remoteConfigDetail));
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

        static void Add(List<SorollaDiagnosticRow> rows, string group, string name, SorollaDiagnosticSeverity severity, string detail) =>
            Add(rows, group, name, severity, detail, SorollaDiagnosticKind.Required);

        static void Add(List<SorollaDiagnosticRow> rows, string group, string name, (SorollaDiagnosticSeverity severity, string detail) item) =>
            Add(rows, group, name, item.severity, item.detail, SorollaDiagnosticKind.Required);

        static void AddObserved(List<SorollaDiagnosticRow> rows, string group, string name, SorollaDiagnosticSeverity severity, string detail) =>
            Add(rows, group, name, severity, detail, SorollaDiagnosticKind.Observed);

        static void Add(List<SorollaDiagnosticRow> rows, string group, string name, SorollaDiagnosticSeverity severity, string detail,
            SorollaDiagnosticKind kind) =>
            rows.Add(new SorollaDiagnosticRow(group, name, severity, detail, kind));

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
            if (AdapterOutcomeNeedsAttention(snapshot.AdjustOutcome)) return AdapterOutcomeSeverity(snapshot.AdjustOutcome);
            if (AdapterOutcomeIsReady(snapshot.AdjustOutcome)) return SorollaDiagnosticSeverity.Pass;
            // Adjust v5 has no init callback; treat the post-init state as "verifying" until a
            // real Adjust getter callback proves the native SDK is reachable (DR-49).
            if (snapshot.AdjustInitialized) return SorollaDiagnosticSeverity.Waiting;
            return SorollaDiagnosticSeverity.Waiting;
        }

        static string AdjustRuntimeDetail(Snapshot snapshot, bool fullMode)
        {
            if (!fullMode) return "Not required in Prototype";
            if (snapshot.AdjustMissingToken) return "App token missing";
            if (AdapterOutcomeNeedsAttention(snapshot.AdjustOutcome)) return AdapterOutcomeDetail(snapshot.AdjustOutcome);
            if (AdapterOutcomeIsReady(snapshot.AdjustOutcome)) return $"Verified ({snapshot.AdjustEnvironment})";
            if (snapshot.AdjustInitialized) return $"Initialized ({snapshot.AdjustEnvironment}); waiting for ADID callback";
            if (snapshot.AdjustInitializing) return $"Initializing ({snapshot.AdjustEnvironment})";
            return "Waiting for MAX consent before Adjust init";
        }

        static string RemoteConfigRowDetail(RemoteConfigStatus status, Snapshot snapshot)
        {
            // Status is authoritative; the scraped fetch line is appended as secondary "last fetch" detail.
            string fetch = snapshot.RemoteConfigFetchSeen ? $" ({snapshot.RemoteConfigDetail})" : "";
            return status switch
            {
                RemoteConfigStatus.Live => $"Live{fetch}",
                RemoteConfigStatus.Cached => $"Cached{fetch}",
                _ => "Serving in-code defaults",
            };
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
            if (AdapterOutcomeNeedsAttention(snapshot.AdjustOutcome)) return AdapterOutcomeSeverity(snapshot.AdjustOutcome);
            if (snapshot.AdjustInitialized && !AdapterOutcomeIsReady(snapshot.AdjustOutcome)) return SorollaDiagnosticSeverity.Waiting;
            if (!snapshot.AdjustIdRequested) return SorollaDiagnosticSeverity.Waiting;
            if (!snapshot.AdjustIdReceived && Time.realtimeSinceStartup - snapshot.AdjustIdRequestTime < 4f)
                return SorollaDiagnosticSeverity.Waiting;
            return snapshot.AdjustIdPresent ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Warning;
        }

        static string AdjustIdDetail(Snapshot snapshot, bool fullMode)
        {
            if (!fullMode) return "Not required in Prototype";
            if (AdapterOutcomeNeedsAttention(snapshot.AdjustOutcome)) return AdapterOutcomeDetail(snapshot.AdjustOutcome);
            if (snapshot.AdjustInitialized && !AdapterOutcomeIsReady(snapshot.AdjustOutcome)) return "Waiting for Adjust ADID callback";
            if (!snapshot.AdjustIdRequested) return "Not requested yet";
            if (!snapshot.AdjustIdReceived && Time.realtimeSinceStartup - snapshot.AdjustIdRequestTime < 4f) return "Fetching";
            return snapshot.AdjustIdPresent ? "Present" : "Missing or unavailable";
        }

        static SorollaDiagnosticSeverity AttributionSeverity(Snapshot snapshot, bool fullMode)
        {
            if (!fullMode) return SorollaDiagnosticSeverity.Info;
            if (AdapterOutcomeNeedsAttention(snapshot.AdjustOutcome)) return AdapterOutcomeSeverity(snapshot.AdjustOutcome);
            if (snapshot.AdjustInitialized && !AdapterOutcomeIsReady(snapshot.AdjustOutcome)) return SorollaDiagnosticSeverity.Waiting;
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
            if (AdapterOutcomeNeedsAttention(snapshot.AdjustOutcome)) return AdapterOutcomeDetail(snapshot.AdjustOutcome);
            if (snapshot.AdjustInitialized && !AdapterOutcomeIsReady(snapshot.AdjustOutcome))
                return "Waiting for Adjust verification";
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
            if (ready) return SorollaDiagnosticSeverity.Pass;
            if (loadFailed) return SorollaDiagnosticSeverity.Warning;
            if (loadStarted) return SorollaDiagnosticSeverity.Waiting;
            if (loaded) return SorollaDiagnosticSeverity.Warning;
            if (completed) return SorollaDiagnosticSeverity.Info;
            return SorollaDiagnosticSeverity.Info;
        }

        static string AdDetail(bool ready, bool loadStarted, bool loadFailed, bool loaded, bool completed, string loadIssue,
            bool maxInitialized)
        {
            if (!maxInitialized) return "Waiting for MAX initialization";
            if (ready) return "Ready to show";
            if (loadFailed) return string.IsNullOrEmpty(loadIssue) ? "Load failed; retrying with backoff" : $"{loadIssue}; retrying";
            if (loadStarted) return "Requested; waiting for network/fill";
            if (loaded) return "Loaded once, but current readiness check is false";
            if (completed) return "Completed once; current readiness unknown";
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
    }
}
