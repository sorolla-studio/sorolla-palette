namespace Sorolla.Palette.Adapters
{
    internal interface ITikTokAdapter
    {
        void Initialize(string appId, string tiktokAppId, string accessToken, bool debugMode);
        void TrackPurchase(double value, string currency);
        void TrackAdRevenue(double value, string currency, string networkName,
            string adFormat, string adUnitId, string placement);
    }

    /// <summary>
    ///     TikTok Business SDK adapter. Delegates to native bridge when available.
    ///     No-ops when not initialized (empty tiktokAppId in config).
    /// </summary>
    internal static class TikTokAdapter
    {
        const string Tag = "[Palette:TikTok]";

        static ITikTokAdapter s_impl;
        static bool s_initialized;

        internal static void RegisterImpl(ITikTokAdapter impl)
        {
            s_impl = impl;
            PaletteLog.Vital($"{Tag} Implementation registered");
        }

        public static void Initialize(string appId, string tiktokAppId, string accessToken, bool debugMode = false)
        {
            if (s_initialized)
            {
                PaletteLog.Warning($"{Tag} Already initialized.");
                return;
            }

            if (s_impl != null)
            {
                s_impl.Initialize(appId, tiktokAppId, accessToken, debugMode);
                s_initialized = true;
            }
            else
            {
                PaletteLog.Warning($"{Tag} Not installed");
            }
        }

        public static void TrackPurchase(double value, string currency = "USD")
        {
            if (s_initialized)
                s_impl.TrackPurchase(value, currency);
        }

        public static void TrackAdRevenue(double value, string currency = "USD", string networkName = null,
            string adFormat = null, string adUnitId = null, string placement = null)
        {
            if (s_initialized)
                s_impl.TrackAdRevenue(value, currency, networkName, adFormat, adUnitId, placement);
        }
    }
}
