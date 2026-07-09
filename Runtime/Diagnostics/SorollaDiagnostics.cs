using System.Collections.Generic;
using System.Text;
using Sorolla.Palette.Adapters;
using UnityEngine;

namespace Sorolla.Palette
{
    // Diagnostic value types (enums + row/problem/event DTOs) live in SorollaDiagnostics.Types.cs.

    internal static partial class SorollaDiagnostics
    {
        const float IdentifierRefreshIntervalSeconds = 20f;
        const int MaxEventLogEntries = 40;
        const int MaxRuntimeProblemEntries = 20;
        const int MaxEventAggregates = 64;
        const string RuntimeProblemsRowName = "Runtime problems";
        static readonly object s_lock = new object();
        static readonly Queue<SorollaDiagnosticEventLogEntry> s_eventLog = new Queue<SorollaDiagnosticEventLogEntry>(MaxEventLogEntries);
        static readonly List<SorollaRuntimeProblem> s_runtimeProblems = new List<SorollaRuntimeProblem>(MaxRuntimeProblemEntries);

        // Per-name event aggregation (count + last params). The 40-entry ring evicts boot events
        // (first_open, consent_resolved) during a normal play session; this map is what makes ONE
        // end-of-run snapshot sufficient. Bounded by distinct name count (the event taxonomy is curated).
        sealed class SorollaEventAggregate
        {
            public int Count;
            public SorollaDiagnosticPayloadLine[] LastParams;
        }

        static readonly Dictionary<string, SorollaEventAggregate> s_eventAggregates =
            new Dictionary<string, SorollaEventAggregate>(MaxEventAggregates);

        // Reserved param key marking an event as QA/test-fired, injected by the console/bridge test
        // actions. Flows to Firebase via the existing param plumbing (production-data hygiene on RC
        // builds: tagging is the only thing separating test traffic from real players), and excludes
        // the event from the game-integration health counters. GameAnalytics progression/economy/design
        // APIs are schema-fixed and cannot carry it, so GameAnalytics test events stay untagged (DR-33).
        internal const string QaTestEventParam = "sorolla_qa_test";

        // SDK-self custom events (DR-60): the SDK emits these itself, so they must never green the
        // game-integration Activity rows. Excluded from the custom-event health counter; still logged
        // and aggregated for visibility.
        static readonly HashSet<string> s_selfEventNames = new HashSet<string>
        {
            "consent_resolved",
            "consent_changed",
            "att_decision",
        };

        // Depth of the current QA/test-action scope (console/bridge). While > 0, dispatched progression/
        // economy/custom events are excluded from the game-integration health counters. Relies on the
        // test action dispatching synchronously (post-init, the normal QA case); a pre-init test event
        // would flush outside the scope. Main-thread; guarded by s_lock.
        static int s_testActionDepth;
        static int s_nextEventId;
        static int s_nextRuntimeProblemId;

        static bool s_logBridgeInstalled;
        static bool s_unityLogInstalled;

        static SorollaDiagnostics()
        {
            InstallAdapterDiagnostics();
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
                if (s_testActionDepth > 0) return; // QA/test action: excluded from game-integration health
                if (status == "start") s_progressionStartCount++;
                else s_progressionEndCount++;
            }
        }

        internal static void RecordEconomy(bool earn)
        {
            lock (s_lock)
            {
                if (s_testActionDepth > 0) return; // QA/test action: excluded from game-integration health
                if (earn) s_economyEarnCount++;
                else s_economySpendCount++;
            }
        }

        // Opens/closes a QA/test-action scope so the events the console or bridge fire do not drive the
        // game-integration health counters. Always pair Begin with End (try/finally).
        internal static void BeginTestAction()
        {
            lock (s_lock) { s_testActionDepth++; }
        }

        internal static void EndTestAction()
        {
            lock (s_lock)
            {
                if (s_testActionDepth > 0) s_testActionDepth--;
            }
        }

        static bool HasTestTag(IDictionary<string, object> parameters) =>
            parameters != null && parameters.ContainsKey(QaTestEventParam);

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
                string name = string.IsNullOrEmpty(eventName) ? "unnamed" : eventName;
                // Exclude QA/test events (scope or tag) and SDK-self events from the Activity health row,
                // but still log + aggregate them so they remain visible in the console and the snapshot.
                bool excluded = s_testActionDepth > 0 || s_selfEventNames.Contains(name) || HasTestTag(parameters);
                if (!excluded)
                {
                    s_customEventCount++;
                    s_lastCustomEvent = name;
                }
                EnqueueEvent("custom", name, parameters);
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
                s_eventAggregates.Clear();
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

        static SorollaConfig LoadConfig()
        {
            return Palette.Config != null
                ? Palette.Config
                : Resources.Load<SorollaConfig>("SorollaConfig");
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

            return SorollaRuntimeProblemClassifier.RuntimeProblemSummary(top);
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
            public AdapterDiagnosticState GameAnalyticsOutcome;
            public bool FacebookInitialized;
            public bool FacebookFailed;
            public AdapterDiagnosticState FacebookOutcome;
            public bool FacebookAttEnabled;
            public bool FacebookAttApplied;
            public bool MaxRegistered;
            public bool MaxInitialized;
            public AdapterDiagnosticState MaxOutcome;
            public bool MaxConsentSeen;
            public string MaxConsentDetail;
            public bool AdjustRegistered;
            public bool AdjustInitializing;
            public bool AdjustInitialized;
            public bool AdjustMissingToken;
            public string AdjustEnvironment;
            public AdapterDiagnosticState AdjustOutcome;
            public bool FirebaseCoreReady;
            public bool FirebaseAnalyticsReady;
            public bool CrashlyticsReady;
            public AdapterDiagnosticState FirebaseCoreOutcome;
            public AdapterDiagnosticState FirebaseAnalyticsOutcome;
            public AdapterDiagnosticState CrashlyticsOutcome;
            public AdapterDiagnosticState RemoteConfigOutcome;
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
