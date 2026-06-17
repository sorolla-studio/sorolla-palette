using System;
using Sorolla.Palette.Adapters;

namespace Sorolla.Palette
{
    internal static partial class SorollaDiagnostics
    {
        internal struct AdapterDiagnosticState
        {
            public bool Seen;
            public AdapterDiagnosticStatus Status;
            public string Code;
            public string Detail;
            public int Count;
        }

        static AdapterDiagnosticState s_maxAdapterOutcome;
        static AdapterDiagnosticState s_adjustAdapterOutcome;
        static AdapterDiagnosticState s_firebaseCoreOutcome;
        static AdapterDiagnosticState s_firebaseAnalyticsOutcome;
        static AdapterDiagnosticState s_crashlyticsOutcome;
        static AdapterDiagnosticState s_remoteConfigOutcome;
        static AdapterDiagnosticState s_gameAnalyticsOutcome;
        static AdapterDiagnosticState s_facebookOutcome;

        static void InstallAdapterDiagnostics()
        {
            AdapterDiagnostics.OutcomeRecorded += RecordAdapterOutcome;
            AdapterDiagnostics.ReplayLatest(RecordAdapterOutcome);
        }

        static void RecordAdapterOutcome(AdapterDiagnosticOutcome outcome)
        {
            lock (s_lock)
            {
                AdapterDiagnosticState next = BuildAdapterState(outcome, CurrentAdapterState(outcome.Vendor));
                SetAdapterState(outcome.Vendor, next);
            }
        }

        static AdapterDiagnosticState BuildAdapterState(AdapterDiagnosticOutcome outcome, AdapterDiagnosticState previous)
        {
            bool sameOutcome = previous.Seen
                && previous.Status == outcome.Status
                && string.Equals(previous.Code, outcome.Code, StringComparison.Ordinal);

            return new AdapterDiagnosticState
            {
                Seen = true,
                Status = outcome.Status,
                Code = outcome.Code,
                Detail = outcome.Detail,
                Count = sameOutcome ? previous.Count + 1 : 1,
            };
        }

        static AdapterDiagnosticState CurrentAdapterState(AdapterDiagnosticVendor vendor)
        {
            switch (vendor)
            {
                case AdapterDiagnosticVendor.Max: return s_maxAdapterOutcome;
                case AdapterDiagnosticVendor.Adjust: return s_adjustAdapterOutcome;
                case AdapterDiagnosticVendor.FirebaseCore: return s_firebaseCoreOutcome;
                case AdapterDiagnosticVendor.FirebaseAnalytics: return s_firebaseAnalyticsOutcome;
                case AdapterDiagnosticVendor.FirebaseCrashlytics: return s_crashlyticsOutcome;
                case AdapterDiagnosticVendor.FirebaseRemoteConfig: return s_remoteConfigOutcome;
                case AdapterDiagnosticVendor.GameAnalytics: return s_gameAnalyticsOutcome;
                case AdapterDiagnosticVendor.Facebook: return s_facebookOutcome;
                default: return default;
            }
        }

        static void SetAdapterState(AdapterDiagnosticVendor vendor, AdapterDiagnosticState state)
        {
            switch (vendor)
            {
                case AdapterDiagnosticVendor.Max:
                    s_maxAdapterOutcome = state;
                    break;
                case AdapterDiagnosticVendor.Adjust:
                    s_adjustAdapterOutcome = state;
                    break;
                case AdapterDiagnosticVendor.FirebaseCore:
                    s_firebaseCoreOutcome = state;
                    break;
                case AdapterDiagnosticVendor.FirebaseAnalytics:
                    s_firebaseAnalyticsOutcome = state;
                    break;
                case AdapterDiagnosticVendor.FirebaseCrashlytics:
                    s_crashlyticsOutcome = state;
                    break;
                case AdapterDiagnosticVendor.FirebaseRemoteConfig:
                    s_remoteConfigOutcome = state;
                    break;
                case AdapterDiagnosticVendor.GameAnalytics:
                    s_gameAnalyticsOutcome = state;
                    break;
                case AdapterDiagnosticVendor.Facebook:
                    s_facebookOutcome = state;
                    break;
            }
        }

        static bool AdapterOutcomeIsReady(AdapterDiagnosticState state)
        {
            return state.Seen
                && (state.Status == AdapterDiagnosticStatus.Ready
                    || state.Status == AdapterDiagnosticStatus.DispatchAccepted);
        }

        static bool AdapterOutcomeNeedsAttention(AdapterDiagnosticState state)
        {
            return state.Seen
                && (state.Status == AdapterDiagnosticStatus.DispatchDropped
                    || state.Status == AdapterDiagnosticStatus.Warning
                    || state.Status == AdapterDiagnosticStatus.Failed
                    || state.Status == AdapterDiagnosticStatus.Unavailable);
        }

        static SorollaDiagnosticSeverity AdapterOutcomeSeverity(AdapterDiagnosticState state)
        {
            switch (state.Status)
            {
                case AdapterDiagnosticStatus.DispatchDropped:
                case AdapterDiagnosticStatus.Failed:
                case AdapterDiagnosticStatus.Unavailable:
                    return SorollaDiagnosticSeverity.Fail;
                case AdapterDiagnosticStatus.Warning:
                    return SorollaDiagnosticSeverity.Warning;
                case AdapterDiagnosticStatus.Registered:
                case AdapterDiagnosticStatus.Initializing:
                    return SorollaDiagnosticSeverity.Waiting;
                case AdapterDiagnosticStatus.Ready:
                case AdapterDiagnosticStatus.DispatchAccepted:
                    return SorollaDiagnosticSeverity.Pass;
                default:
                    return SorollaDiagnosticSeverity.Info;
            }
        }

        static SorollaDiagnosticSeverity AdapterRowSeverity(AdapterDiagnosticState state,
            SorollaDiagnosticSeverity fallback)
        {
            return AdapterOutcomeNeedsAttention(state) ? AdapterOutcomeSeverity(state) : fallback;
        }

        static string AdapterRowDetail(AdapterDiagnosticState state, string fallback)
        {
            return AdapterOutcomeNeedsAttention(state) ? AdapterOutcomeDetail(state) : fallback;
        }

        static string AdapterOutcomeDetail(AdapterDiagnosticState state)
        {
            string detail = string.IsNullOrEmpty(state.Detail) ? AdapterStatusLabel(state.Status) : state.Detail;
            return state.Count > 1 ? $"{detail} (x{state.Count})" : detail;
        }

        static string AdapterStatusForSnapshot(AdapterDiagnosticState state)
        {
            if (!state.Seen) return null;

            switch (state.Status)
            {
                case AdapterDiagnosticStatus.DispatchDropped:
                    return "dispatch_dropped";
                case AdapterDiagnosticStatus.Warning:
                    return "warning";
                case AdapterDiagnosticStatus.Failed:
                    return "failed";
                case AdapterDiagnosticStatus.Unavailable:
                    return "unavailable";
                case AdapterDiagnosticStatus.Ready:
                case AdapterDiagnosticStatus.DispatchAccepted:
                    return "ready";
                default:
                    return null;
            }
        }

        static string AdapterStatusLabel(AdapterDiagnosticStatus status)
        {
            switch (status)
            {
                case AdapterDiagnosticStatus.Registered: return "Registered";
                case AdapterDiagnosticStatus.Initializing: return "Initializing";
                case AdapterDiagnosticStatus.Ready: return "Ready";
                case AdapterDiagnosticStatus.DispatchAccepted: return "Dispatch accepted";
                case AdapterDiagnosticStatus.DispatchDropped: return "Dispatch dropped";
                case AdapterDiagnosticStatus.Warning: return "Warning";
                case AdapterDiagnosticStatus.Failed: return "Failed";
                case AdapterDiagnosticStatus.Unavailable: return "Unavailable";
                default: return "Unknown";
            }
        }
    }
}
