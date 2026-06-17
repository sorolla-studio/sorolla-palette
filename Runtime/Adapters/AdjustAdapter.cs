using System;

namespace Sorolla.Palette.Adapters
{
    public enum AdjustEnvironment { Sandbox, Production }

    /// <summary>
    ///     Attribution data returned by the Adjust adapter.
    /// </summary>
    public struct AttributionData
    {
        public string Network;
        public string Campaign;
        public string Adgroup;
        public string Creative;
        public string TrackerName;
    }

    /// <summary>
    ///     Ad revenue info for cross-SDK tracking.
    /// </summary>
    public struct AdRevenueInfo
    {
        public const string DefaultSource = "applovin_max_sdk";

        public string Source;
        public double Revenue;
        public string Currency;
        public string Network;
        public string AdUnit;
        public string Placement;
    }

    /// <summary>
    ///     Interface for Adjust adapter implementation.
    /// </summary>
    internal interface IAdjustAdapter
    {
        void Initialize(string appToken, AdjustEnvironment environment, bool verboseLogging = false);
        void UpdateConsent(bool consent);
        void TrackAdRevenue(AdRevenueInfo info);
        void TrackPurchaseIOS(string eventToken, double amount, string currency, string productId, string transactionId, string deduplicationId);
        void TrackPurchaseAndroid(string eventToken, double amount, string currency, string productId, string purchaseToken, string deduplicationId);
        void TrackPurchase(string eventToken, double amount, string currency, string productId, string transactionId, string purchaseToken);
        void TrackPurchaseSimple(string eventToken, double amount, string currency, string deduplicationId, string productId);
        void SetUserId(string userId);
        void GetAttribution(Action<AttributionData?> callback);
        void GetAdid(Action<string> callback);
        void GetGoogleAdId(Action<string> callback);
        void GetIdfa(Action<string> callback);
    }

    /// <summary>
    ///     Adjust SDK adapter. Delegates to implementation when available.
    /// </summary>
    internal static class AdjustAdapter
    {
        const string Tag = "[Palette:Adjust]";

        private static IAdjustAdapter s_impl;
        static bool s_initialized;

        internal static void RegisterImpl(IAdjustAdapter impl)
        {
            s_impl = impl;
            PaletteLog.Vital($"{Tag} Implementation registered");
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Adjust, AdapterDiagnosticStatus.Registered,
                "registered", "Implementation registered");
        }

        public static bool IsRegistered => s_impl != null;
        public static bool IsInitialized => s_initialized;

        public static void Initialize(string appToken, AdjustEnvironment environment, bool verboseLogging = false)
        {
            if (s_impl != null)
            {
                s_impl.Initialize(appToken, environment, verboseLogging);
                s_initialized = true;
            }
            else
            {
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Adjust, AdapterDiagnosticStatus.Unavailable,
                    "not_installed", "Adjust implementation not installed");
                PaletteLog.Warning($"{Tag} Not installed");
            }
        }

        public static void UpdateConsent(bool consent)
        {
            s_impl?.UpdateConsent(consent);
        }

        public static void TrackAdRevenue(AdRevenueInfo info)
        {
            s_impl?.TrackAdRevenue(info);
        }

        public static void TrackPurchase(string eventToken, double amount, string currency,
            string productId, string transactionId, string purchaseToken)
        {
            s_impl?.TrackPurchase(eventToken, amount, currency, productId, transactionId, purchaseToken);
        }

        public static void SetUserId(string userId)
        {
            s_impl?.SetUserId(userId);
        }

        public static void GetAttribution(Action<AttributionData?> callback)
        {
            if (s_impl != null)
                s_impl.GetAttribution(callback);
            else
                callback?.Invoke(null);
        }

        public static void GetAdid(Action<string> callback)
        {
            if (s_impl != null)
                s_impl.GetAdid(callback);
            else
                callback?.Invoke(null);
        }

        public static void GetGoogleAdId(Action<string> callback)
        {
            if (s_impl != null)
                s_impl.GetGoogleAdId(callback);
            else
                callback?.Invoke(null);
        }

        public static void GetIdfa(Action<string> callback)
        {
            if (s_impl != null)
                s_impl.GetIdfa(callback);
            else
                callback?.Invoke(null);
        }
    }
}
