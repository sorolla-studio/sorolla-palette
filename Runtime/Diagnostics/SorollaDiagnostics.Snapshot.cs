using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Sorolla.Palette.Adapters;
using UnityEngine;

namespace Sorolla.Palette
{
    internal static partial class SorollaDiagnostics
    {
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
                    GameAnalyticsOutcome = s_gameAnalyticsOutcome,
                    FacebookInitialized = s_facebookInitialized,
                    FacebookFailed = s_facebookFailed,
                    FacebookOutcome = s_facebookOutcome,
                    FacebookAttEnabled = FacebookAdapter.LastAdvertiserTrackingEnabled,
                    FacebookAttApplied = FacebookAdapter.AdvertiserTrackingApplied,
                    MaxRegistered = s_maxRegistered || MaxAdapter.IsRegistered,
                    MaxInitialized = s_maxInitialized || MaxAdapter.IsInitialized,
                    MaxOutcome = s_maxAdapterOutcome,
                    MaxConsentSeen = s_maxConsentSeen,
                    MaxConsentDetail = s_maxConsentDetail,
                    AdjustRegistered = s_adjustRegistered || AdjustAdapter.IsRegistered,
                    AdjustInitializing = s_adjustInitializing,
                    AdjustInitialized = s_adjustInitialized || AdjustAdapter.IsInitialized,
                    AdjustMissingToken = s_adjustMissingToken,
                    AdjustEnvironment = s_adjustEnvironment,
                    AdjustOutcome = s_adjustAdapterOutcome,
                    FirebaseCoreReady = s_firebaseCoreReady,
                    FirebaseAnalyticsReady = s_firebaseAnalyticsReady,
                    CrashlyticsReady = s_crashlyticsReady,
                    FirebaseCoreOutcome = s_firebaseCoreOutcome,
                    FirebaseAnalyticsOutcome = s_firebaseAnalyticsOutcome,
                    CrashlyticsOutcome = s_crashlyticsOutcome,
                    RemoteConfigOutcome = s_remoteConfigOutcome,
                    RemoteConfigFetchSeen = s_remoteConfigFetchSeen,
                    RemoteConfigFetchSuccess = s_remoteConfigFetchSuccess,
                    RemoteConfigDetail = s_remoteConfigDetail,
                    PurchaseTrackingAttached = s_purchaseTrackingAttached,
                    PurchaseAttachAttempted = s_purchaseAttachAttempted,
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
        /// <summary>
        ///     App Tracking Transparency status, or null off iOS (2026-07-23 platform-scoping pass). The ATT
        ///     bridge answers a constant "authorized" on every other platform, so reporting it there stated an
        ///     iOS-only fact about a build that can never prompt for it. Null rather than an omitted member:
        ///     the JSON keeps its shape, and a reader sees an explicit "no value" instead of a plausible one.
        ///     Moves in lockstep with the ATT row in the Vitals overlay - the two frontends must show the same
        ///     state.
        /// </summary>
        static string AttForSnapshot() =>
#if UNITY_IOS
            Palette.AttString(Palette.AttStatus);
#else
            null;
#endif

        internal static SorollaQaState CaptureQaState()
        {
            SorollaConfig config = LoadConfig();
            Snapshot snap = CaptureSnapshot();
            bool fullMode = IsFullMode(config, snap);

            ReadIabtcf(out bool tcStringPresent, out string purposeConsents);

            var events = new List<SorollaQaEvent>(s_eventAggregates.Count);
            CopyEventAggregates(events);

            return new SorollaQaState
            {
                Events = events.ToArray(),
                IapTrackingAttached = snap.PurchaseTrackingAttached,
                IapAttachAttempted = snap.PurchaseAttachAttempted,
                IapPurchaseCount = snap.PurchaseAcceptedCount,
                IapDuplicateCount = snap.PurchaseDuplicateCount,
                IapVerification = snap.PurchaseVerification,
                IapLastIssue = snap.PurchaseIssue,

                SdkVersion = Palette.SdkVersion,
                Mode = ModeForSnapshot(config, snap),
                DevelopmentBuild = Debug.isDebugBuild,
                BridgeArmed = QaBridgeServer.IsArmed,
                Ready = Palette.IsInitialized || snap.ReadySeen,
                DeviceWallClock = CurrentDeviceWallClock(),

                ApplicationId = Application.identifier,
                Platform = Application.platform.ToString(),
                AppVersion = Application.version,
                BuildGuid = Application.buildGUID,

                ConsentStatus = Palette.ConsentStatus.ToString(),
                ConsentGeography = ConsentGeography(Palette.ConsentStatus),
                Att = AttForSnapshot(),
                CanRequestAds = Palette.CanRequestAds,
                ConsentFormShownThisSession = snap.ConsentFormShownThisSession,
                ConsentSignalsKnown = snap.ConsentSignalsKnown,
                AdStorageConsent = snap.AdStorageConsent,
                AdPersonalizationConsent = snap.AdPersonalizationConsent,
                AdUserDataConsent = snap.AdUserDataConsent,
                AnalyticsStorageConsent = snap.AnalyticsStorageConsent,
                TcStringPresent = tcStringPresent,
                PurposeConsents = purposeConsents,

                RemoteConfigStatus = Palette.RemoteConfigStatus.ToString().ToLowerInvariant(),
                RemoteConfigFetchSeen = snap.RemoteConfigFetchSeen,
                RemoteConfigFetchSuccess = snap.RemoteConfigFetchSuccess,
                RemoteConfigValues = CaptureRemoteConfigValues(),

                MaxAdapter = MaxAdapterStatus(snap, fullMode),
                AdjustAdapter = AdjustAdapterStatus(snap, fullMode),
                FirebaseAdapter = FirebaseAdapterStatus(snap, fullMode),
                GameAnalyticsAdapter = GameAnalyticsAdapterStatus(snap),
                FacebookAdapter = FacebookAdapterStatus(snap),
                CrashlyticsReady = CrashlyticsReadyForSnapshot(snap),
                CrashlyticsOutcome = CrashlyticsOutcomeStatus(snap, fullMode),

                AdvertisingIdPresent = snap.AdIdPresent,
                AdvertisingIdZeroed = snap.AdIdZeroed,
                AdjustAdidPresent = snap.AdjustIdPresent,
                AttributionNetwork = snap.AttributionSummary,
                AdjustEnvironment = snap.AdjustEnvironment,
                FacebookAttEnabled = snap.FacebookAttEnabled,
                FacebookAttApplied = snap.FacebookAttApplied,

                InterstitialLoaded = snap.InterstitialLoaded,
                InterstitialCompleted = snap.InterstitialCompleted,
                RewardedLoaded = snap.RewardedLoaded,
                RewardedCompleted = snap.RewardedCompleted,
                AdRevenueSeen = snap.AdRevenueSeen,

                SdkWarningCount = snap.PaletteWarningCount,
                SdkErrorCount = snap.PaletteErrorCount,
                LastSdkError = snap.LastPaletteError,
            };
        }

        // Union of keys Firebase knows (remote + in-app defaults) and keys registered via
        // SetRemoteConfigDefaults, each resolved through the same tier order as the Palette getters.
        // Verbose-independent: reads live adapter state, so it works in release (non-development) builds.
        static SorollaQaRcValue[] CaptureRemoteConfigValues()
        {
            var keys = new SortedSet<string>(FirebaseRemoteConfigAdapter.GetKnownKeys(), StringComparer.Ordinal);
            foreach (string key in RemoteConfigState.RegisteredKeys) keys.Add(key);

            var values = new SorollaQaRcValue[keys.Count];
            int i = 0;
            foreach (string key in keys)
            {
                string value, source;
                if (FirebaseRemoteConfigAdapter.TryGetRaw(key, out value))
                    source = FirebaseRemoteConfigAdapter.TryGetSource(key, out string fbSource)
                        ? $"firebase_{fbSource}"
                        : "firebase";
                else if (GameAnalyticsAdapter.TryGetRemoteConfigValue(key, out value))
                    source = "gameanalytics";
                else if (RemoteConfigState.TryGetDefault(key, out value))
                    source = "in_app_default";
                else
                {
                    value = "";
                    source = "missing";
                }
                values[i++] = new SorollaQaRcValue { Key = key, Value = value, Source = source };
            }
            return values;
        }

        static string CurrentDeviceWallClock() =>
            DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

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
            string outcome = AdapterStatusForSnapshot(snapshot.MaxOutcome);
            if (outcome != null && outcome != "ready") return outcome;
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
            string outcome = AdapterStatusForSnapshot(snapshot.AdjustOutcome);
            if (outcome != null && outcome != "ready") return outcome;
            if (AdapterOutcomeIsReady(snapshot.AdjustOutcome)) return $"enabled({snapshot.AdjustEnvironment.ToLowerInvariant()})";
            if (snapshot.AdjustInitialized) return "verifying";
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
            string coreOutcome = AdapterStatusForSnapshot(snapshot.FirebaseCoreOutcome);
            if (coreOutcome != null && coreOutcome != "ready") return coreOutcome;
            string analyticsOutcome = AdapterStatusForSnapshot(snapshot.FirebaseAnalyticsOutcome);
            if (analyticsOutcome != null && analyticsOutcome != "ready") return analyticsOutcome;
            return snapshot.FirebaseAnalyticsReady || FirebaseAdapter.IsReady ? "ready" : "waiting";
#else
            return fullMode ? "missing" : "not_installed";
#endif
        }

        static bool CrashlyticsReadyForSnapshot(Snapshot snapshot)
        {
#if FIREBASE_CRASHLYTICS_INSTALLED
            return snapshot.CrashlyticsReady || FirebaseCrashlyticsAdapter.IsReady;
#else
            return false;
#endif
        }

        static string CrashlyticsOutcomeStatus(Snapshot snapshot, bool fullMode)
        {
#if FIREBASE_CRASHLYTICS_INSTALLED
            string outcome = AdapterStatusForSnapshot(snapshot.CrashlyticsOutcome);
            if (outcome != null && outcome != "ready") return outcome;
            return CrashlyticsReadyForSnapshot(snapshot) ? "ready" : "waiting";
#else
            return fullMode ? "missing" : "not_installed";
#endif
        }

        static string GameAnalyticsAdapterStatus(Snapshot snapshot)
        {
#if GAMEANALYTICS_INSTALLED
            if (GameAnalyticsAdapter.IsInitialized || snapshot.GaInitialized) return "ready";
            string outcome = AdapterStatusForSnapshot(snapshot.GameAnalyticsOutcome);
            if (outcome != null && outcome != "ready") return outcome;
            return "waiting";
#else
            return "not_installed";
#endif
        }

        static string FacebookAdapterStatus(Snapshot snapshot)
        {
#if SOROLLA_FACEBOOK_ENABLED
            // "ready" only when the Graph validation probe recorded Ready (AdapterStatusForSnapshot
            // returns it). Init completing alone (FacebookInitialized) is NOT enough: a pending or
            // never-returning probe reads "waiting", and a failing one reads "failed"/"warning".
            string outcome = AdapterStatusForSnapshot(snapshot.FacebookOutcome);
            if (outcome != null) return outcome;
            if (snapshot.FacebookFailed) return "failed";
            return "waiting";
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
    }
}
