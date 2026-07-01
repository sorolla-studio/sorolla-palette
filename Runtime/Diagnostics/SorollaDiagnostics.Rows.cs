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
    }
}
