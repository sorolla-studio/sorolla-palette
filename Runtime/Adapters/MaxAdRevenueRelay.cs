using UnityEngine;
using UnityEngine.Scripting;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     Fans MAX ad-revenue impressions out to Adjust/TikTok/Firebase. Kept separate from
    ///     MaxAdapterImpl so the MAX bridge only ever talks to the MAX SDK; this is the one place
    ///     that decides which other vendors receive ad-revenue telemetry and in what shape.
    /// </summary>
    [Preserve]
    static class MaxAdRevenueRelay
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        [Preserve]
        static void Register() => MaxAdapter.OnAdRevenueTracked += OnAdRevenueTracked;

        static void OnAdRevenueTracked(MaxAdRevenueInfo info)
        {
#if SOROLLA_ADJUST_ENABLED
            AdjustAdapter.TrackAdRevenue(new AdRevenueInfo
            {
                Source = AdRevenueInfo.DefaultSource,
                Revenue = info.Revenue,
                Currency = info.Currency,
                Network = info.Network,
                AdUnit = info.AdUnitIdentifier,
                Placement = info.Placement,
            });
#endif

            // TikTok ad revenue (always call - stub no-ops if not initialized)
            TikTokAdapter.TrackAdRevenue(info.Revenue, info.Currency, info.Network,
                info.AdFormat, info.AdUnitIdentifier, info.Placement);

            FirebaseAdapter.TrackAdImpression(
                adPlatform: "applovin_max",
                adSource: info.Network,
                adFormat: info.AdFormat,
                adUnitName: info.AdUnitIdentifier,
                revenue: info.Revenue,
                currency: info.Currency,
                revenuePrecision: info.RevenuePrecision
            );
        }
    }
}
