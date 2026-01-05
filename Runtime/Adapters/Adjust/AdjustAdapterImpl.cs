using System;
using AdjustSdk;
using UnityEngine;
using UnityEngine.Scripting;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     Adjust SDK adapter implementation. Registered at runtime.
    /// </summary>
    [Preserve]
    internal class AdjustAdapterImpl : IAdjustAdapter
    {
        private bool _init;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        [Preserve]
        private static void Register()
        {
            Debug.Log("[Sorolla:Adjust] Register() called - assembly is loaded!");
            AdjustAdapter.RegisterImpl(new AdjustAdapterImpl());
        }

        public void Initialize(string appToken, AdjustEnvironment environment)
        {
            if (_init) return;

            Debug.Log($"[Sorolla:Adjust] Initializing ({environment})...");

            var config = new AdjustConfig(appToken, environment == AdjustEnvironment.Production
                ? AdjustSdk.AdjustEnvironment.Production
                : AdjustSdk.AdjustEnvironment.Sandbox);

            // Enable attribution logging
            config.LogLevel = AdjustLogLevel.Info;
            config.AttributionChangedDelegate = attribution =>
            {
                Debug.Log($"[Sorolla:Adjust] Attribution: network={attribution.Network}, campaign={attribution.Campaign}");
            };

            Adjust.InitSdk(config);
            _init = true;
            Debug.Log("[Sorolla:Adjust] Initialized");
        }

        public void TrackEvent(string eventToken)
        {
            if (!_init) return;
            Adjust.TrackEvent(new AdjustEvent(eventToken));
        }

        public void TrackRevenue(string eventToken, double amount, string currency)
        {
            if (!_init) return;
            var e = new AdjustEvent(eventToken);
            e.SetRevenue(amount, currency);
            Adjust.TrackEvent(e);
        }

        public void TrackAdRevenue(AdRevenueInfo info)
        {
            if (!_init) return;

            var adRevenue = new AdjustAdRevenue(info.Source ?? "applovin_max_sdk");
            adRevenue.SetRevenue(info.Revenue, info.Currency ?? "USD");
            adRevenue.AdRevenueNetwork = info.Network;
            adRevenue.AdRevenueUnit = info.AdUnit;
            adRevenue.AdRevenuePlacement = info.Placement;
            Adjust.TrackAdRevenue(adRevenue);
        }

        public void SetUserId(string userId)
        {
            if (!_init) return;
            Adjust.AddGlobalPartnerParameter("user_id", userId);
        }

        public void GetAttribution(Action<object> callback)
        {
            if (!_init) return;
            Adjust.GetAttribution(callback);
        }

        public void GetAdid(Action<string> callback)
        {
            if (!_init) return;
            Adjust.GetAdid(callback);
        }

        public void GetGoogleAdId(Action<string> callback)
        {
            if (!_init) return;
            Adjust.GetGoogleAdId(callback);
        }

        public void GetIdfa(Action<string> callback)
        {
            if (!_init) return;
            Adjust.GetIdfa(callback);
        }
    }
}
