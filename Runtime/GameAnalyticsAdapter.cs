using System.Runtime.CompilerServices;
using UnityEngine;
#if GAMEANALYTICS_INSTALLED
using GameAnalyticsSDK;
#endif

namespace Sorolla.Palette
{
    /// <summary>
    ///     Internal adapter for GameAnalytics SDK.
    ///     Use Palette API instead of calling this directly.
    /// </summary>
    internal static class GameAnalyticsAdapter
    {
        const string Tag = "[Palette:GA]";
        static bool s_init;

        public static void Initialize()
        {
            if (s_init) return;

#if GAMEANALYTICS_INSTALLED
            if (GameAnalytics.Initialized)
            {
                Debug.Log($"{Tag} Already initialized externally");
                s_init = true;
                return;
            }

            Debug.Log($"{Tag} Initializing...");
            GameAnalytics.Initialize();
            s_init = true;
#else
            Debug.LogWarning($"{Tag} SDK not installed");
            s_init = true;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool EnsureInit()
        {
            if (s_init) return true;
            Debug.LogWarning($"{Tag} Not initialized");
            return false;
        }

        public static bool IsRemoteConfigReady()
        {
#if GAMEANALYTICS_INSTALLED
            return s_init && GameAnalytics.IsRemoteConfigsReady();
#else
            return false;
#endif
        }

#if GAMEANALYTICS_INSTALLED
        public static void TrackProgressionEvent(GAProgressionStatus status, string p1, string p2 = null, string p3 = null, int score = 0)
        {
            if (!EnsureInit()) return;

            if (string.IsNullOrEmpty(p2) && string.IsNullOrEmpty(p3))
                if (score > 0) GameAnalytics.NewProgressionEvent(status, p1, score);
                else GameAnalytics.NewProgressionEvent(status, p1);
            else if (string.IsNullOrEmpty(p3))
                if (score > 0) GameAnalytics.NewProgressionEvent(status, p1, p2, score);
                else GameAnalytics.NewProgressionEvent(status, p1, p2);
            else if (score > 0) GameAnalytics.NewProgressionEvent(status, p1, p2, p3, score);
            else GameAnalytics.NewProgressionEvent(status, p1, p2, p3);
        }

        public static void TrackDesignEvent(string eventName, float value = 0)
        {
            if (!EnsureInit()) return;
            if (value != 0) GameAnalytics.NewDesignEvent(eventName, value);
            else GameAnalytics.NewDesignEvent(eventName);
        }

        public static void TrackResourceEvent(GAResourceFlowType flowType, string currency, float amount, string itemType, string itemId)
        {
            if (!EnsureInit()) return;
            GameAnalytics.NewResourceEvent(flowType, currency, amount, itemType, itemId);
        }

        public static string GetRemoteConfigValue(string key, string defaultValue = "") =>
            s_init && IsRemoteConfigReady() ? GameAnalytics.GetRemoteConfigsValueAsString(key, defaultValue) : defaultValue;
#else
        public static void TrackProgressionEvent(string status, string p1, string p2 = null, string p3 = null, int score = 0) { }
        public static void TrackDesignEvent(string eventName, float value = 0) { }
        public static void TrackResourceEvent(string flowType, string currency, float amount, string itemType, string itemId) { }
        public static string GetRemoteConfigValue(string key, string defaultValue = "") => defaultValue;
#endif
    }
}
