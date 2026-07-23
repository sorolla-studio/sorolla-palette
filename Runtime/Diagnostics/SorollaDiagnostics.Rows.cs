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
            if (ModeSeverity(config, snapshot) == SorollaDiagnosticSeverity.Fail)
                AddDiagnosed(rows, "Boot", "Palette mode", SorollaDiagnosticSeverity.Fail, ModeDetail(config, snapshot), PaletteModeUnknownDiagnosis());
            else
                Add(rows, "Boot", "Palette mode", ModeSeverity(config, snapshot), ModeDetail(config, snapshot));
            Add(rows, "Boot", "Palette ready", Palette.IsInitialized || snapshot.ReadySeen ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting,
                Palette.IsInitialized || snapshot.ReadySeen ? "Ready" : snapshot.InitDetail);
            SorollaDiagnosticSeverity networkSeverity = ReachabilitySeverity();
            if (networkSeverity == SorollaDiagnosticSeverity.Fail)
                AddDiagnosed(rows, "Boot", "Network reachability", networkSeverity,
                    Application.internetReachability.ToString(), NetworkUnavailableDiagnosis());
            else
                Add(rows, "Boot", "Network reachability", networkSeverity, Application.internetReachability.ToString());

            if (config != null)
            {
                Add(rows, "Config", "SorollaConfig", SorollaDiagnosticSeverity.Pass, "Loaded from Resources");
            }
            else
            {
                AddDiagnosed(rows, "Config", "SorollaConfig", SorollaDiagnosticSeverity.Fail,
                    "Missing Assets/Resources/SorollaConfig.asset", SorollaConfigMissingDiagnosis());
            }

            if (fullMode && snapshot.AdjustMissingToken)
            {
                AddDiagnosed(rows, "Config", "Adjust token", SorollaDiagnosticSeverity.Fail,
                    "Missing or rejected by SDK", AdjustTokenMissingDiagnosis());
            }
            else
            {
                Add(rows, "Config", "Adjust token", ConfigPresence(config?.adjustAppToken, fullMode, snapshot.AdjustMissingToken));
            }
            SorollaDiagnosticSeverity environmentSeverity = AdjustEnvironmentSeverity(config, fullMode);
            string environmentDetail = AdjustEnvironmentDetail(config, fullMode);
            if (environmentSeverity == SorollaDiagnosticSeverity.Fail)
                AddDiagnosed(rows, "Config", "Adjust environment", environmentSeverity, environmentDetail,
                    AdjustSandboxReleaseDiagnosis());
            else
                Add(rows, "Config", "Adjust environment", environmentSeverity, environmentDetail);
            AddConfigPresence(rows, "Rewarded ad unit", config?.rewardedAdUnit?.Current, fullMode,
                MissingAdUnitDiagnosis("rewarded"));
            AddConfigPresence(rows, "Interstitial ad unit", config?.interstitialAdUnit?.Current, fullMode,
                MissingAdUnitDiagnosis("interstitial"));
            AddConfigPresence(rows, "Purchase event token", config?.adjustPurchaseEventToken, fullMode,
                MissingPurchaseEventTokenDiagnosis());

#if GAMEANALYTICS_INSTALLED
            bool gameAnalyticsReady = GameAnalyticsAdapter.IsInitialized || snapshot.GaInitialized;
            Add(rows, "SDKs", "GameAnalytics",
                gameAnalyticsReady
                    ? SorollaDiagnosticSeverity.Pass
                    : AdapterRowSeverity(snapshot.GameAnalyticsOutcome, SorollaDiagnosticSeverity.Waiting),
                gameAnalyticsReady
                    ? "Ready"
                    : AdapterRowDetail(snapshot.GameAnalyticsOutcome, "Waiting for GameAnalytics initialization"));
            AddGameAnalyticsPlatformKeysRow(rows);
#else
            AddDiagnosed(rows, "SDKs", "GameAnalytics", SorollaDiagnosticSeverity.Fail, "Package not installed",
                PackageMissingDiagnosis("GameAnalytics", "com.gameanalytics.sdk"));
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
            SorollaDiagnosticSeverity facebookRowSeverity = AdapterRowSeverity(snapshot.FacebookOutcome, facebookSeverity);
            string facebookRowDetail = AdapterRowDetail(snapshot.FacebookOutcome, facebookDetail);
            if (facebookRowSeverity == SorollaDiagnosticSeverity.Fail)
            {
                // fb-failure-triage.md rung 1: DiagnoseProbeFailure already wrote "{platform} not
                // registered on FB app {appId}" into the detail when Graph confirmed the platform
                // gap (the boulder-evolution cause). Any other Fail detail is a genuine SDK-can't-see
                // boundary (rungs 1.5-5 of the ladder) - honestly named as unknown, not guessed.
                var diagnosis = FacebookFailureDiagnosis(facebookRowDetail);
                AddDiagnosed(rows, "SDKs", "Facebook", facebookRowSeverity, facebookRowDetail, diagnosis);
            }
            else
            {
                Add(rows, "SDKs", "Facebook", facebookRowSeverity, facebookRowDetail);
            }
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
            if (fullMode)
                AddDiagnosed(rows, "SDKs", "MAX", SorollaDiagnosticSeverity.Fail, "Package missing for Full mode",
                    PackageMissingDiagnosis("AppLovin MAX", "com.applovin.mediation.ads"));
            else
                Add(rows, "SDKs", "MAX", SorollaDiagnosticSeverity.Info, "Not installed");
#endif

#if SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
            Add(rows, "SDKs", "Adjust implementation", snapshot.AdjustRegistered ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting,
                snapshot.AdjustRegistered ? "Registered" : "Waiting for adapter registration");
            Add(rows, "SDKs", "Adjust initialized", AdjustRuntimeSeverity(snapshot, fullMode),
                AdjustRuntimeDetail(snapshot, fullMode));
#else
            if (fullMode)
                AddDiagnosed(rows, "SDKs", "Adjust", SorollaDiagnosticSeverity.Fail, "Package missing for Full mode",
                    PackageMissingDiagnosis("Adjust", "com.adjust.sdk"));
            else
                Add(rows, "SDKs", "Adjust", SorollaDiagnosticSeverity.Info, "Not installed");
#endif

#if FIREBASE_ANALYTICS_INSTALLED
            bool firebaseCoreReady = snapshot.FirebaseCoreReady || FirebaseCoreManager.IsInitialized;
            bool firebaseAnalyticsReady = snapshot.FirebaseAnalyticsReady || FirebaseAdapter.IsReady;
            AddFirebaseRow(rows, "Core", "Firebase Core",
                AdapterRowSeverity(snapshot.FirebaseCoreOutcome, firebaseCoreReady ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting),
                AdapterRowDetail(snapshot.FirebaseCoreOutcome, firebaseCoreReady ? "Ready" : "Waiting for Firebase Core"));
            AddFirebaseRow(rows, "Analytics", "Firebase Analytics",
                AdapterRowSeverity(snapshot.FirebaseAnalyticsOutcome, firebaseAnalyticsReady ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting),
                AdapterRowDetail(snapshot.FirebaseAnalyticsOutcome, firebaseAnalyticsReady ? "Ready" : "Waiting for Firebase Analytics"));
#else
            if (fullMode)
                AddDiagnosed(rows, "Firebase", "Analytics", SorollaDiagnosticSeverity.Fail, "Package missing for Full mode",
                    PackageMissingDiagnosis("Firebase Analytics", "com.google.firebase.analytics"));
            else
                Add(rows, "Firebase", "Analytics", SorollaDiagnosticSeverity.Info, "Not installed");
#endif

#if FIREBASE_CRASHLYTICS_INSTALLED
            bool crashlyticsReady = snapshot.CrashlyticsReady || FirebaseCrashlyticsAdapter.IsReady;
            AddFirebaseRow(rows, "Crashlytics", "Firebase Crashlytics",
                AdapterRowSeverity(snapshot.CrashlyticsOutcome, crashlyticsReady ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting),
                AdapterRowDetail(snapshot.CrashlyticsOutcome, crashlyticsReady ? "Initialized" : "Waiting for init"));
#else
            if (fullMode)
                AddDiagnosed(rows, "Firebase", "Crashlytics", SorollaDiagnosticSeverity.Fail, "Package missing for Full mode",
                    PackageMissingDiagnosis("Firebase Crashlytics", "com.google.firebase.crashlytics"));
            else
                Add(rows, "Firebase", "Crashlytics", SorollaDiagnosticSeverity.Info, "Not installed");
#endif

#if FIREBASE_REMOTE_CONFIG_INSTALLED
            RemoteConfigStatus remoteConfigStatus = Palette.RemoteConfigStatus;
            SorollaDiagnosticSeverity remoteConfigSeverity = remoteConfigStatus == RemoteConfigStatus.Defaults
                ? SorollaDiagnosticSeverity.Info
                : SorollaDiagnosticSeverity.Pass;
            string remoteConfigDetail = RemoteConfigRowDetail(remoteConfigStatus, snapshot);
            AddFirebaseRow(rows, "Remote Config", "Firebase Remote Config",
                AdapterRowSeverity(snapshot.RemoteConfigOutcome, remoteConfigSeverity),
                AdapterRowDetail(snapshot.RemoteConfigOutcome, remoteConfigDetail));
#else
            if (fullMode)
                AddDiagnosed(rows, "Firebase", "Remote Config", SorollaDiagnosticSeverity.Fail, "Package missing for Full mode",
                    PackageMissingDiagnosis("Firebase Remote Config", "com.google.firebase.remote-config"));
            else
                Add(rows, "Firebase", "Remote Config", SorollaDiagnosticSeverity.Info, "Not installed");
#endif

            SorollaDiagnosticSeverity consentSeverity = ConsentSeverity(snapshot);
            string consentDetail = snapshot.MaxConsentSeen ? snapshot.MaxConsentDetail : "Waiting for consent status";
            if (consentSeverity == SorollaDiagnosticSeverity.Waiting)
                AddDiagnosed(rows, "Consent", "MAX consent", consentSeverity, consentDetail,
                    ConsentWaitingDiagnosis());
            else
                Add(rows, "Consent", "MAX consent", consentSeverity, consentDetail);
            if (!Palette.CanRequestAds && fullMode)
            {
                AddDiagnosed(rows, "Consent", "Can request ads", SorollaDiagnosticSeverity.Info, "False",
                    CannotRequestAdsDiagnosis(), SorollaDiagnosticKind.Observed);
            }
            else
            {
                Add(rows, "Consent", "Can request ads", Palette.CanRequestAds ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Info,
                    Palette.CanRequestAds ? "True" : "Not applicable");
            }

            // App Tracking Transparency exists only on iOS. Off-iOS the bridge answers a constant
            // "Authorized", so this row used to render on Android as a green fact about a prompt that can
            // never appear there (2026-07-23 platform-scoping pass). Vitals reports the platform it runs on.
#if UNITY_IOS
            (SorollaDiagnosticSeverity severity, bool denied) att = AttState();
            if (att.denied)
                AddDiagnosed(rows, "Consent", "ATT", att.severity, Palette.AttStatus.ToString(), AttDeniedDiagnosis());
            else
                Add(rows, "Consent", "ATT", att.severity, Palette.AttStatus.ToString());
#endif
            AddObserved(rows, "Identity", AdvertisingIdLabel(), AdvertisingIdSeverity(snapshot), AdvertisingIdDetail(snapshot));
            AddObserved(rows, "Identity", "Adjust ADID", AdjustIdSeverity(snapshot, fullMode), AdjustIdDetail(snapshot, fullMode));
            AddObserved(rows, "Identity", "Attribution", AttributionSeverity(snapshot, fullMode), AttributionDetail(snapshot));

            AddObserved(rows, "Activity", "Progression start/end", CountPairSeverity(snapshot.ProgressionStartCount, snapshot.ProgressionEndCount),
                $"{snapshot.ProgressionStartCount} start / {snapshot.ProgressionEndCount} end observed");
            // Economy is one integration family with two optional flows: a single observed flow already
            // proves dispatch, and earn-without-spend is normal player behavior (hungrysnake 2026-07-14
            // false-warn on a live green game). Zero activity stays Info; never Warning.
            AddObserved(rows, "Activity", "Economy earn/spend",
                snapshot.EconomyEarnCount + snapshot.EconomySpendCount > 0
                    ? SorollaDiagnosticSeverity.Pass
                    : SorollaDiagnosticSeverity.Info,
                $"{snapshot.EconomyEarnCount} earn / {snapshot.EconomySpendCount} spend observed");
            AddObserved(rows, "Activity", "Custom events", snapshot.CustomEventCount > 0 ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Info,
                snapshot.CustomEventCount > 0 ? $"{snapshot.CustomEventCount} observed, last={snapshot.LastCustomEvent}" : "None observed");
            AddObserved(rows, "Activity", "IAP tracking attached", snapshot.PurchaseTrackingAttached ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Info,
                snapshot.PurchaseTrackingAttached ? "AttachPurchaseTracking wired" : "Waiting for store controller wiring");
            AddObserved(rows, "Activity", "Purchase accepted", snapshot.PurchaseAcceptedCount > 0 ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Info,
                snapshot.PurchaseAcceptedCount > 0 ? $"{snapshot.PurchaseAcceptedCount} purchase event(s)" : "No purchase observed");
            AddPurchaseVerificationRow(rows, snapshot);
            AddPurchaseIssuesRow(rows, snapshot);

            AddAdRow(rows, "Interstitial", snapshot.InterstitialReady, snapshot.InterstitialLoadStarted,
                snapshot.InterstitialLoadFailed, snapshot.InterstitialLoaded, snapshot.InterstitialCompleted,
                snapshot.InterstitialLoadIssue, snapshot.MaxInitialized);
            AddAdRow(rows, "Rewarded", snapshot.RewardedReady, snapshot.RewardedLoadStarted,
                snapshot.RewardedLoadFailed, snapshot.RewardedLoaded, snapshot.RewardedCompleted,
                snapshot.RewardedLoadIssue, snapshot.MaxInitialized);
            AddObserved(rows, "Ads", "Ad revenue", snapshot.AdRevenueSeen ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Info,
                snapshot.AdRevenueSeen ? "Observed" : "No revenue callback observed");
            if (snapshot.LastAdIssue == "No issue observed")
                Add(rows, "Ads", "Ad issues", SorollaDiagnosticSeverity.Pass, snapshot.LastAdIssue);
            else
                AddDiagnosed(rows, "Ads", "Ad issues", SorollaDiagnosticSeverity.Warning, snapshot.LastAdIssue,
                    AdIssueDiagnosis(snapshot.LastAdIssue));

            AddSdkWarningsRow(rows, snapshot);
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
            SorollaDiagnosticKind kind)
        {
            if (NeedsAttention(severity) && group != "Red flags")
            {
                (string why, string signal, string fix) diagnosis = DefaultDiagnosis(group, name, detail);
                rows.Add(new SorollaDiagnosticRow(group, name, severity, detail, kind,
                    diagnosis.why, diagnosis.signal, diagnosis.fix));
                return;
            }
            rows.Add(new SorollaDiagnosticRow(group, name, severity, detail, kind));
        }

        static void AddConfigPresence(List<SorollaDiagnosticRow> rows, string name, string value, bool required,
            (string why, string signal, string fix) diagnosis)
        {
            (SorollaDiagnosticSeverity severity, string detail) state = ConfigPresence(value, required, false);
            if (state.severity == SorollaDiagnosticSeverity.Fail)
                AddDiagnosed(rows, "Config", name, state.severity, state.detail, diagnosis);
            else
                Add(rows, "Config", name, state);
        }

        static void AddPurchaseVerificationRow(List<SorollaDiagnosticRow> rows, Snapshot snapshot)
        {
            SorollaDiagnosticSeverity severity = PurchaseVerificationSeverity(snapshot);
            if (severity == SorollaDiagnosticSeverity.Warning)
                AddDiagnosed(rows, "Activity", "Purchase verification", severity, snapshot.PurchaseVerification,
                    PurchaseVerificationFailureDiagnosis());
            else
                AddObserved(rows, "Activity", "Purchase verification", severity, snapshot.PurchaseVerification);
        }

        static void AddPurchaseIssuesRow(List<SorollaDiagnosticRow> rows, Snapshot snapshot)
        {
            string detail = snapshot.PurchaseDuplicateCount > 0
                ? $"{snapshot.PurchaseIssue}; duplicates={snapshot.PurchaseDuplicateCount}"
                : snapshot.PurchaseIssue;
            if (snapshot.PurchaseIssue == "No issue observed")
                AddObserved(rows, "Activity", "Purchase issues", SorollaDiagnosticSeverity.Pass, detail);
            else
                AddDiagnosed(rows, "Activity", "Purchase issues", SorollaDiagnosticSeverity.Warning, detail,
                    PurchaseIssueDiagnosis(snapshot.PurchaseDuplicateCount));
        }

        // Phase 5: structured WHY/SIGNAL/FIX variant. Only called at row-producing sites where the
        // diagnosis text in SorollaDiagnostics.Diagnoses.cs applies - see that file for the strings.
        static void AddDiagnosed(List<SorollaDiagnosticRow> rows, string group, string name, SorollaDiagnosticSeverity severity,
            string detail, (string why, string signal, string fix) diagnosis, SorollaDiagnosticKind kind = SorollaDiagnosticKind.Required) =>
            rows.Add(new SorollaDiagnosticRow(group, name, severity, detail, kind, diagnosis.why, diagnosis.signal, diagnosis.fix));

        static (SorollaDiagnosticSeverity severity, string detail) ConfigPresence(string value, bool required, bool forcedFail)
        {
            if (forcedFail) return (SorollaDiagnosticSeverity.Fail, "Missing or rejected by SDK");
            if (!required && string.IsNullOrEmpty(value)) return (SorollaDiagnosticSeverity.Info, "Not required");
            return string.IsNullOrEmpty(value)
                ? (SorollaDiagnosticSeverity.Fail, "Missing")
                : (SorollaDiagnosticSeverity.Pass, "Present");
        }

        // Prototype mode is a first-class RELEASE path (prototypes ship for FB UA tests; MAX/Adjust
        // are Full-mode-only by design). The only mode failure detectable on-device is mode UNKNOWN
        // (config missing) - a wrong-mode-for-this-game mismatch needs the game's intended mode,
        // which lives in the publisher roster / QA-expectations asset, not in the build.
        static SorollaDiagnosticSeverity ModeSeverity(SorollaConfig config, Snapshot snapshot)
        {
            if (config == null && !snapshot.ModeKnown) return SorollaDiagnosticSeverity.Fail;
            return SorollaDiagnosticSeverity.Pass;
        }

        static string ModeDetail(SorollaConfig config, Snapshot snapshot)
        {
            if (config == null && !snapshot.ModeKnown) return "Config missing / mode unknown";
            return IsFullMode(config, snapshot)
                ? "Full mode (MAX + Adjust + GA + FB + Firebase)"
                : "Prototype mode (GA + FB + Firebase; MAX/Adjust excluded by design)";
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
            if (detail.Contains("Denied")) return SorollaDiagnosticSeverity.Info;
            return SorollaDiagnosticSeverity.Waiting;
        }

#if UNITY_IOS
        /// <summary>
        ///     The ATT row's severity, plus whether the status is a tracking DENIAL (denied or restricted).
        ///     A user's privacy choice is never a studio issue, so a denial is Info, not a warning - but it is
        ///     the one status a tester needs explained, so it still carries the WHY/SIGNAL/FIX depth.
        ///     iOS-only, like the row it feeds and the prompt it describes.
        /// </summary>
        static (SorollaDiagnosticSeverity severity, bool denied) AttState() =>
            Palette.AttStatus switch
            {
                ATT.ATTBridge.AuthorizationStatus.Authorized => (SorollaDiagnosticSeverity.Pass, false),
                ATT.ATTBridge.AuthorizationStatus.NotDetermined => (SorollaDiagnosticSeverity.Waiting, false),
                ATT.ATTBridge.AuthorizationStatus.Denied => (SorollaDiagnosticSeverity.Info, true),
                ATT.ATTBridge.AuthorizationStatus.Restricted => (SorollaDiagnosticSeverity.Info, true),
                _ => (SorollaDiagnosticSeverity.Info, false),
            };
#endif

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
            // A zeroed advertising id is a USER opt-out artifact on both platforms, not a defect the studio
            // can act on - Info, never Fail (it also used to flap red/amber across the 4s fetch window).
            if (snapshot.AdIdZeroed) return SorollaDiagnosticSeverity.Info;
            return snapshot.AdIdPresent ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Warning;
        }

        static string AdvertisingIdDetail(Snapshot snapshot)
        {
            if (!snapshot.AdIdRequested) return "Not requested yet";
            if (!snapshot.AdIdReceived && Time.realtimeSinceStartup - snapshot.AdIdRequestTime < 4f) return "Fetching";
            if (snapshot.AdIdZeroed) return "Zeroed (user opted out of tracking)";
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

        // Firebase's own "not available" message is the single most common Fail this menu ever
        // shows (every editor playmode session, no native lib) and is a completely different fact
        // from the same wording on a real device build (missing config file) - see
        // FirebaseUnavailableInEditorDiagnosis / FirebaseUnavailableOnDeviceDiagnosis.
        //
        // Bug found in my own verification pass (phase-5 checkpoint 2, live device-vs-editor state
        // check): the row's stored Detail text is the SHORT form ("Firebase native library not
        // available"), not the fuller log-line wording ("...not available in Editor. This does not
        // affect..."), so a detail.Contains("in Editor") string match never matched and every Fail
        // silently fell into the DEVICE-build diagnosis - shown live in the Unity EDITOR, an honesty
        // violation (guessing a config-file cause that doesn't apply). Fixed to key on
        // Application.isEditor directly instead of re-deriving it from message text.
        /// <summary>
        ///     The SDK-warnings row. A warning the SDK can DIAGNOSE gets the real WHY/SIGNAL/FIX at this
        ///     producer (the "no unknown fix" rule) - starting with the one pattern seen live: a game reading a
        ///     remote-config key that exists in neither the console nor the registered in-app defaults. An
        ///     undiagnosed warning keeps the generic shape and routes to Sorolla.
        /// </summary>
        static void AddSdkWarningsRow(List<SorollaDiagnosticRow> rows, Snapshot snapshot)
        {
            if (snapshot.PaletteWarningCount == 0)
            {
                Add(rows, "Red flags", "SDK warnings", SorollaDiagnosticSeverity.Pass, "None");
                return;
            }

            string detail = $"{snapshot.PaletteWarningCount}, last={snapshot.LastPaletteWarning}";
            string missingKey = MissingRemoteConfigKey(snapshot.LastPaletteWarning);
            if (missingKey != null)
                AddDiagnosed(rows, "Red flags", "SDK warnings", SorollaDiagnosticSeverity.Warning, detail,
                    MissingRemoteConfigKeyDiagnosis(missingKey));
            else
                Add(rows, "Red flags", "SDK warnings", SorollaDiagnosticSeverity.Warning, detail);
        }

        const string MissingRemoteConfigKeyMarker = "not found in remote config, GameAnalytics, or registered defaults";

        /// <summary>The key name out of the SDK's own missing-remote-config-key warning, or null when the
        /// warning is something else. Matches the exact text Palette.RemoteConfig's WarnOnce emits.</summary>
        static string MissingRemoteConfigKey(string warning)
        {
            if (string.IsNullOrEmpty(warning) || !warning.Contains(MissingRemoteConfigKeyMarker)) return null;
            int open = warning.IndexOf('\'');
            if (open < 0) return null;
            int close = warning.IndexOf('\'', open + 1);
            return close > open + 1 ? warning.Substring(open + 1, close - open - 1) : null;
        }

        static void AddFirebaseRow(List<SorollaDiagnosticRow> rows, string name, string vendorLabel,
            SorollaDiagnosticSeverity severity, string detail)
        {
            if (severity == SorollaDiagnosticSeverity.Fail && detail != null && detail.Contains("not available"))
            {
                var diagnosis = Application.isEditor
                    ? FirebaseUnavailableInEditorDiagnosis(vendorLabel)
                    : FirebaseUnavailableOnDeviceDiagnosis(vendorLabel);
                AddDiagnosed(rows, "Firebase", name, severity, detail, diagnosis);
            }
            else if (severity == SorollaDiagnosticSeverity.Fail && detail != null && detail.Contains("init failed"))
            {
                // Firebase Analytics' own distinct failure wording ("Firebase init failed - dropping
                // analytics event") - Core failed and Analytics inherited it by dropping events,
                // rather than reporting "not available" itself. Same editor-vs-device split applies.
                var diagnosis = Application.isEditor
                    ? FirebaseUnavailableInEditorDiagnosis(vendorLabel)
                    : FirebaseUnavailableOnDeviceDiagnosis(vendorLabel);
                AddDiagnosed(rows, "Firebase", name, severity, detail, diagnosis);
            }
            else
            {
                Add(rows, "Firebase", name, severity, detail);
            }
        }

#if GAMEANALYTICS_INSTALLED
        static void AddGameAnalyticsPlatformKeysRow(List<SorollaDiagnosticRow> rows)
        {
            GameAnalyticsPlatformKeysDiagnostic result = CaptureGameAnalyticsPlatformKeys();
            if (result.MissingKeyPair)
                AddDiagnosed(rows, "SDKs", "GameAnalytics platform keys", result.Severity, result.Detail,
                    GameAnalyticsPlatformKeyMissingDiagnosis(result.PlatformName));
            else
                Add(rows, "SDKs", "GameAnalytics platform keys", result.Severity, result.Detail);
        }

        static GameAnalyticsPlatformKeysDiagnostic CaptureGameAnalyticsPlatformKeys()
        {
            if (Application.isEditor)
                return GameAnalyticsPlatformKeysDiagnostic.Info("editor", "editor - not checkable");

            RuntimePlatform platform = Application.platform;
            if (platform != RuntimePlatform.Android && platform != RuntimePlatform.IPhonePlayer)
                return GameAnalyticsPlatformKeysDiagnostic.Info(platform.ToString(), $"{platform} - not checkable");

            string platformName = platform == RuntimePlatform.IPhonePlayer ? "iOS" : "Android";
            try
            {
                var settings = Resources.Load("GameAnalytics/Settings", typeof(GameAnalyticsSDK.Setup.Settings))
                    as GameAnalyticsSDK.Setup.Settings;
                if (settings == null)
                    return GameAnalyticsPlatformKeysDiagnostic.Missing(platformName);

                int index = settings.Platforms.IndexOf(platform);
                if (index < 0)
                    return GameAnalyticsPlatformKeysDiagnostic.Missing(platformName);

                string gameKey = settings.GetGameKey(index);
                string secretKey = settings.GetSecretKey(index);
                if (string.IsNullOrEmpty(gameKey) || string.IsNullOrEmpty(secretKey))
                    return GameAnalyticsPlatformKeysDiagnostic.Missing(platformName);

                return new GameAnalyticsPlatformKeysDiagnostic
                {
                    Severity = SorollaDiagnosticSeverity.Pass,
                    Detail = $"Configured for {platformName}",
                    PlatformName = platformName,
                };
            }
            catch (Exception e)
            {
                return GameAnalyticsPlatformKeysDiagnostic.Info(platformName,
                    $"check unavailable ({e.GetType().Name})");
            }
        }

        struct GameAnalyticsPlatformKeysDiagnostic
        {
            public SorollaDiagnosticSeverity Severity;
            public string Detail;
            public string PlatformName;
            public bool MissingKeyPair;

            public static GameAnalyticsPlatformKeysDiagnostic Info(string platformName, string detail) =>
                new GameAnalyticsPlatformKeysDiagnostic
                {
                    Severity = SorollaDiagnosticSeverity.Info,
                    Detail = detail,
                    PlatformName = platformName,
                };

            public static GameAnalyticsPlatformKeysDiagnostic Missing(string platformName) =>
                new GameAnalyticsPlatformKeysDiagnostic
                {
                    Severity = SorollaDiagnosticSeverity.Warning,
                    Detail = $"{platformName} has no game key + secret key pair in Assets/Resources/GameAnalytics/Settings.asset.",
                    PlatformName = platformName,
                    MissingKeyPair = true,
                };
        }
#endif

        static (string why, string signal, string fix) FacebookFailureDiagnosis(string detail)
        {
            if (detail != null && detail.Contains("not registered on FB app"))
                return FacebookPlatformNotRegisteredDiagnosis(detail);
            if (IsTlsCertificateFailure(detail))
                return FacebookDeviceClockSuspectDiagnosis(detail);
            return FacebookGenericFailureDiagnosis(detail);
        }

        static bool IsTlsCertificateFailure(string detail)
        {
            if (string.IsNullOrEmpty(detail)) return false;
            return detail.IndexOf("ssl", StringComparison.OrdinalIgnoreCase) >= 0
                   || detail.IndexOf("tls", StringComparison.OrdinalIgnoreCase) >= 0
                   || detail.IndexOf("certificate", StringComparison.OrdinalIgnoreCase) >= 0
                   || detail.IndexOf("cert ", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static void AddAdRow(List<SorollaDiagnosticRow> rows, string format, bool ready, bool loadStarted, bool loadFailed,
            bool loaded, bool completed, string loadIssue, bool maxInitialized)
        {
            SorollaDiagnosticSeverity severity = AdSeverity(ready, loadStarted, loadFailed, loaded, completed, maxInitialized);
            string detail = AdDetail(ready, loadStarted, loadFailed, loaded, completed, loadIssue, maxInitialized);
            if (severity == SorollaDiagnosticSeverity.Warning && loadFailed)
                AddDiagnosed(rows, "Ads", format, severity, detail, AdLoadFailedDiagnosis(format, loadIssue));
            else
                Add(rows, "Ads", format, severity, detail);
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
