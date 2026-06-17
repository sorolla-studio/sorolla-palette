using System;

namespace Sorolla.Palette.Adapters
{
    internal enum AdapterDiagnosticVendor
    {
        Max,
        Adjust,
        FirebaseCore,
        FirebaseAnalytics,
        FirebaseCrashlytics,
        FirebaseRemoteConfig,
        GameAnalytics,
        Facebook,
    }

    internal enum AdapterDiagnosticStatus
    {
        Registered,
        Initializing,
        Ready,
        DispatchAccepted,
        DispatchDropped,
        Warning,
        Failed,
        Unavailable,
    }

    internal readonly struct AdapterDiagnosticOutcome
    {
        public readonly AdapterDiagnosticVendor Vendor;
        public readonly AdapterDiagnosticStatus Status;
        public readonly string Code;
        public readonly string Detail;

        public AdapterDiagnosticOutcome(AdapterDiagnosticVendor vendor, AdapterDiagnosticStatus status,
            string code, string detail)
        {
            Vendor = vendor;
            Status = status;
            Code = code ?? "";
            Detail = detail ?? "";
        }
    }

    internal static class AdapterDiagnostics
    {
        const int VendorCount = (int)AdapterDiagnosticVendor.Facebook + 1;
        static readonly AdapterDiagnosticOutcome[] s_latest = new AdapterDiagnosticOutcome[VendorCount];
        static readonly bool[] s_seen = new bool[VendorCount];

        internal static event Action<AdapterDiagnosticOutcome> OutcomeRecorded;

        internal static void Record(AdapterDiagnosticVendor vendor, AdapterDiagnosticStatus status,
            string code, string detail)
        {
            var outcome = new AdapterDiagnosticOutcome(vendor, status, code, detail);
            int index = (int)vendor;
            s_latest[index] = outcome;
            s_seen[index] = true;
            OutcomeRecorded?.Invoke(outcome);
        }

        internal static void ReplayLatest(Action<AdapterDiagnosticOutcome> receiver)
        {
            if (receiver == null) return;

            for (int i = 0; i < VendorCount; i++)
            {
                if (s_seen[i])
                    receiver(s_latest[i]);
            }
        }
    }
}
