using System;
using System.Collections.Generic;
using System.Text;
using Sorolla.Palette.Adapters;
using UnityEngine;

namespace Sorolla.Palette
{
    internal static partial class SorollaDiagnostics
    {
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
            string safeMessage = SafeDetail(SorollaRuntimeProblemClassifier.FirstLine(message));
            string safeStack = SorollaRuntimeProblemClassifier.FormatStackTrace(stackTrace);
            string problemType = SorollaRuntimeProblemClassifier.RuntimeProblemType(message, type, isNullReference, isFatal);
            string source = SorollaRuntimeProblemClassifier.RuntimeProblemSource(message, stackTrace);
            string topFrame = SorollaRuntimeProblemClassifier.RuntimeProblemTopFrame(stackTrace);
            string fingerprint = SorollaRuntimeProblemClassifier.RuntimeProblemFingerprint(problemType, safeMessage, topFrame);

            for (int i = 0; i < s_runtimeProblems.Count; i++)
            {
                SorollaRuntimeProblem existing = s_runtimeProblems[i];
                if (existing.Fingerprint != fingerprint) continue;

                int nextCount = existing.Count + 1;
                SorollaDiagnosticSeverity severity = SorollaRuntimeProblemClassifier.RuntimeProblemSeverity(problemType, source, isNullReference, isFatal, nextCount);
                s_runtimeProblems[i] = existing.WithRepeat(now, severity);
                return;
            }

            if (s_runtimeProblems.Count >= MaxRuntimeProblemEntries)
                s_runtimeProblems.RemoveAt(0);

            SorollaDiagnosticSeverity initialSeverity = SorollaRuntimeProblemClassifier.RuntimeProblemSeverity(problemType, source, isNullReference, isFatal, 1);
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

            if (message.Contains("[Palette:GA] Ready") || message.Contains("[Palette:GA] Already initialized"))
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

            if (message.Contains("[Palette:MAX] Rewarded ad loaded"))
            {
                s_rewardedLoaded = true;
                s_lastAdIssue = "No issue observed";
            }
            if (message.Contains("[Palette:MAX] Rewarded ad completed"))
            {
                s_rewardedCompleted = true;
                s_lastAdIssue = "No issue observed";
            }
            if (message.Contains("[Palette:MAX] Interstitial ad loaded"))
            {
                s_interstitialLoaded = true;
                s_lastAdIssue = "No issue observed";
            }
            if (message.Contains("[Palette:MAX] Interstitial ad completed"))
            {
                s_interstitialCompleted = true;
                s_lastAdIssue = "No issue observed";
            }
            // Ad-revenue is now recorded directly via RecordAdRevenue (DR-09); the old verbose-only
            // "TrackAdRevenue:" log-sniff is removed so the Vitals row no longer depends on log level.
            if (message.Contains("[Palette:MAX]") &&
                (message.Contains("load failed") || message.Contains("display failed") || message.Contains("not ready")))
                s_lastAdIssue = SafeDetail(message);
        }
    }
}
