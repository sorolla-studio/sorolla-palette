using System;
using System.Collections.Generic;
using System.Globalization;
using Sorolla.Palette.Adapters;

namespace Sorolla.Palette
{
    public static partial class Palette
    {
        /// <summary>
        ///     Check if Remote Config is ready. Does not require <see cref="IsInitialized"/> -
        ///     returns true as soon as the underlying provider (Firebase or GameAnalytics) is ready.
        /// </summary>
        public static bool IsRemoteConfigReady()
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            if (FirebaseRemoteConfigAdapter.IsReady)
                return true;
#endif
            return GameAnalyticsAdapter.IsRemoteConfigReady();
        }

        /// <summary>
        ///     Fetch Remote Config values. Fetches from Firebase if installed, GameAnalytics is always ready.
        /// </summary>
        public static void FetchRemoteConfig(Action<bool> onComplete = null)
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            FirebaseRemoteConfigAdapter.FetchAndActivate(onComplete);
#else
            // GameAnalytics RC doesn't need explicit fetch
            onComplete?.Invoke(GameAnalyticsAdapter.IsRemoteConfigReady());
#endif
        }

        /// <summary>
        ///     Get Remote Config string value. Checks Firebase first, then GameAnalytics.
        /// </summary>
        public static string GetRemoteConfig(string key, string defaultValue = "")
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            if (FirebaseRemoteConfigAdapter.IsReady)
            {
                string value = FirebaseRemoteConfigAdapter.GetString(key, null);
                if (value != null) return value;
            }
#endif
            // Fallback to GameAnalytics
            string gaValue = GameAnalyticsAdapter.GetRemoteConfigValue(key, null);
            return gaValue ?? defaultValue;
        }

        /// <summary>
        ///     Get Remote Config int value. Checks Firebase first, then GameAnalytics.
        /// </summary>
        public static int GetRemoteConfigInt(string key, int defaultValue = 0)
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            if (FirebaseRemoteConfigAdapter.IsReady)
                return FirebaseRemoteConfigAdapter.GetInt(key, defaultValue);
#endif
            string strValue = GameAnalyticsAdapter.GetRemoteConfigValue(key, null);
            return strValue != null && int.TryParse(strValue, out int r) ? r : defaultValue;
        }

        /// <summary>
        ///     Get Remote Config float value. Checks Firebase first, then GameAnalytics.
        /// </summary>
        public static float GetRemoteConfigFloat(string key, float defaultValue = 0f)
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            if (FirebaseRemoteConfigAdapter.IsReady)
                return FirebaseRemoteConfigAdapter.GetFloat(key, defaultValue);
#endif
            string strValue = GameAnalyticsAdapter.GetRemoteConfigValue(key, null);
            return strValue != null && float.TryParse(strValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float r)
                ? r : defaultValue;
        }

        /// <summary>
        ///     Get Remote Config bool value. Checks Firebase first, then GameAnalytics.
        /// </summary>
        public static bool GetRemoteConfigBool(string key, bool defaultValue = false)
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            if (FirebaseRemoteConfigAdapter.IsReady)
                return FirebaseRemoteConfigAdapter.GetBool(key, defaultValue);
#endif
            string strValue = GameAnalyticsAdapter.GetRemoteConfigValue(key, null);
            return strValue != null && bool.TryParse(strValue, out bool r) ? r : defaultValue;
        }

        /// <summary>
        ///     Set in-app defaults for Remote Config. Works before or after initialization.
        ///     Values are used when no fetched or cached value exists.
        /// </summary>
        public static void SetRemoteConfigDefaults(Dictionary<string, object> defaults)
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            FirebaseRemoteConfigAdapter.SetDefaults(defaults);
#endif
        }

        /// <summary>
        ///     When true (default), real-time Remote Config updates are activated immediately.
        ///     Set false for games where mid-session config changes would be jarring.
        ///     Use ActivateRemoteConfigAsync() for manual control when disabled.
        /// </summary>
        public static bool AutoActivateRemoteConfigUpdates
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            get => FirebaseRemoteConfigAdapter.AutoActivateUpdates;
            set => FirebaseRemoteConfigAdapter.AutoActivateUpdates = value;
#else
            get => true;
            set { }
#endif
        }

        /// <summary>
        ///     Manually activate fetched Remote Config values.
        ///     Use when AutoActivateRemoteConfigUpdates is false.
        /// </summary>
        public static System.Threading.Tasks.Task<bool> ActivateRemoteConfigAsync()
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            return FirebaseRemoteConfigAdapter.ActivateAsync();
#else
            return System.Threading.Tasks.Task.FromResult(false);
#endif
        }

        /// <summary>
        ///     Get all available Remote Config keys from Firebase.
        ///     Returns empty if Firebase Remote Config is not installed or not ready.
        /// </summary>
        public static IEnumerable<string> GetRemoteConfigKeys()
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            return FirebaseRemoteConfigAdapter.GetKeys();
#else
            return Array.Empty<string>();
#endif
        }

        /// <summary>
        ///     Fired when a real-time Remote Config update is received.
        ///     Includes the set of updated keys so games can decide whether to react.
        ///     If AutoActivateRemoteConfigUpdates is true, values are already activated when this fires.
        /// </summary>
        public static event Action<IReadOnlyCollection<string>> OnRemoteConfigUpdated
        {
#if FIREBASE_REMOTE_CONFIG_INSTALLED
            add => FirebaseRemoteConfigAdapter.OnConfigUpdated += value;
            remove => FirebaseRemoteConfigAdapter.OnConfigUpdated -= value;
#else
            add { }
            remove { }
#endif
        }
    }
}
