using System;

namespace Sorolla.Palette.Adapters
{
    public enum AdjustEnvironment { Sandbox, Production }

    /// <summary>
    ///     Ad revenue info for cross-SDK tracking.
    /// </summary>
    public struct AdRevenueInfo
    {
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
        void Initialize(string appToken, AdjustEnvironment environment);
        void TrackEvent(string eventToken);
        void TrackRevenue(string eventToken, double amount, string currency);
        void TrackAdRevenue(AdRevenueInfo info);
        void SetUserId(string userId);
        void GetAttribution(Action<object> callback);
        void GetAdid(Action<string> callback);
        void GetGoogleAdId(Action<string> callback);
        void GetIdfa(Action<string> callback);
    }

    /// <summary>
    ///     Adjust SDK adapter. Delegates to implementation when available.
    /// </summary>
    public static class AdjustAdapter
    {
        private static IAdjustAdapter s_impl;

        internal static void RegisterImpl(IAdjustAdapter impl)
        {
            s_impl = impl;
            UnityEngine.Debug.Log("[Sorolla:Adjust] Implementation registered");
        }

        public static void Initialize(string appToken, AdjustEnvironment environment)
        {
            if (s_impl != null)
                s_impl.Initialize(appToken, environment);
            else
                UnityEngine.Debug.LogWarning("[Sorolla:Adjust] Not installed");
        }

        public static void TrackEvent(string eventToken)
        {
            s_impl?.TrackEvent(eventToken);
        }

        public static void TrackRevenue(string eventToken, double amount, string currency = "USD")
        {
            s_impl?.TrackRevenue(eventToken, amount, currency);
        }

        public static void TrackAdRevenue(AdRevenueInfo info)
        {
            s_impl?.TrackAdRevenue(info);
        }

        public static void SetUserId(string userId)
        {
            s_impl?.SetUserId(userId);
        }

        public static void GetAttribution(Action<object> callback)
        {
            if (s_impl != null)
                s_impl.GetAttribution(callback);
        }

        public static void GetAdid(Action<string> callback)
        {
            if (s_impl != null)
                s_impl.GetAdid(callback);
        }

        public static void GetGoogleAdId(Action<string> callback)
        {
            if (s_impl != null)
                s_impl.GetGoogleAdId(callback);
        }

        public static void GetIdfa(Action<string> callback)
        {
            if (s_impl != null)
                s_impl.GetIdfa(callback);
        }
    }
}
