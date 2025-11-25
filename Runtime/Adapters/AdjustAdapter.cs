#if SOROLLA_ADJUST_ENABLED
using System;
using AdjustSdk;
using UnityEngine;

namespace Sorolla.Adapters
{
    /// <summary>
    ///     Adjust SDK adapter. Use Sorolla API instead.
    /// </summary>
    public static class AdjustAdapter
    {
        private static bool s_init;

        public static void Initialize(string appToken, AdjustEnvironment environment)
        {
            if (s_init) return;

            Debug.Log($"[Sorolla:Adjust] Initializing ({environment})...");

            var config = new AdjustConfig(appToken, environment == AdjustEnvironment.Production
                ? AdjustSdk.AdjustEnvironment.Production
                : AdjustSdk.AdjustEnvironment.Sandbox);

            AdjustSdk.Adjust.InitSdk(config);
            s_init = true;
            Debug.Log("[Sorolla:Adjust] Initialized");
        }

        public static void TrackEvent(string eventToken)
        {
            if (!s_init) return;
            AdjustSdk.Adjust.TrackEvent(new AdjustEvent(eventToken));
        }

        public static void TrackRevenue(string eventToken, double amount, string currency = "USD")
        {
            if (!s_init) return;
            var e = new AdjustEvent(eventToken);
            e.SetRevenue(amount, currency);
            AdjustSdk.Adjust.TrackEvent(e);
        }

#if APPLOVIN_MAX_INSTALLED
        public static void TrackAdRevenue(MaxSdkBase.AdInfo adInfo)
        {
            if (!s_init) return;
            var adRevenue = new AdjustAdRevenue(AdjustConfig.AdjustAdRevenueSourceAppLovinMAX);
            adRevenue.setRevenue(adInfo.Revenue, "USD");
            adRevenue.setAdRevenueNetwork(adInfo.NetworkName);
            adRevenue.setAdRevenueUnit(adInfo.AdUnitIdentifier);
            adRevenue.setAdRevenuePlacement(adInfo.Placement);
            AdjustSdk.Adjust.trackAdRevenue(adRevenue);
        }
#else
        public static void TrackAdRevenue(object adInfo) { }
#endif

        public static void SetUserId(string userId)
        {
            if (!s_init) return;
            AdjustSdk.Adjust.AddGlobalPartnerParameter("user_id", userId);
        }

        public static void GetAttribution(Action<AdjustAttribution> callback)
        {
            if (!s_init) return;
            AdjustSdk.Adjust.GetAttribution(callback);
        }

        public static void GetAdid(Action<string> callback)
        {
            if (!s_init) return;
            AdjustSdk.Adjust.GetAdid(callback);
        }
    }
}
#else
namespace Sorolla.Adapters
{
    public static class AdjustAdapter
    {
        public static void Initialize(string t, AdjustEnvironment e) => UnityEngine.Debug.LogWarning("[Sorolla:Adjust] Not installed");
        public static void TrackEvent(string t) { }
        public static void TrackRevenue(string t, double a, string c = "USD") { }
        public static void TrackAdRevenue(object i) { }
        public static void SetUserId(string u) { }
        public static void GetAttribution(System.Action<object> c) { }
        public static void GetAdid(System.Action<string> c) { }
    }
}
#endif

namespace Sorolla.Adapters
{
    public enum AdjustEnvironment { Sandbox, Production }
}
