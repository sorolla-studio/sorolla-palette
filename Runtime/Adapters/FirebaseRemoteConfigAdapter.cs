#if FIREBASE_REMOTE_CONFIG_INSTALLED
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Extensions;
using Firebase.RemoteConfig;
using UnityEngine;

namespace Sorolla.Adapters
{
    /// <summary>
    ///     Firebase Remote Config adapter. Use Sorolla API instead.
    /// </summary>
    public static class FirebaseRemoteConfigAdapter
    {
        private const string Tag = "[Sorolla:RemoteConfig]";
        private static bool s_init;
        private static bool s_ready;
        private static bool s_fetching;

        /// <summary>Whether Remote Config is initialized</summary>
        public static bool IsReady => s_ready;

        /// <summary>
        ///     Initialize Remote Config with optional defaults.
        /// </summary>
        /// <param name="defaults">Default values to use before fetch completes</param>
        /// <param name="autoFetch">Whether to automatically fetch on init</param>
        public static void Initialize(Dictionary<string, object> defaults = null, bool autoFetch = true)
        {
            if (s_init) return;
            s_init = true;

            Debug.Log($"{Tag} Initializing...");

            // Set defaults if provided
            if (defaults != null && defaults.Count > 0)
            {
                FirebaseRemoteConfig.DefaultInstance.SetDefaultsAsync(defaults).ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted)
                        Debug.LogError($"{Tag} Failed to set defaults: {task.Exception}");
                    else
                        Debug.Log($"{Tag} Defaults set ({defaults.Count} values)");
                });
            }

            if (autoFetch)
                FetchAndActivate(null);
            else
                s_ready = true;
        }

        /// <summary>
        ///     Fetch remote config values and activate them.
        /// </summary>
        /// <param name="onComplete">Callback when fetch completes (success/failure)</param>
        /// <param name="cacheExpirationSeconds">Cache expiration in seconds (0 for immediate fetch, default 3600)</param>
        public static void FetchAndActivate(Action<bool> onComplete, int cacheExpirationSeconds = 3600)
        {
            if (s_fetching)
            {
                Debug.LogWarning($"{Tag} Fetch already in progress");
                return;
            }

            s_fetching = true;
            Debug.Log($"{Tag} Fetching...");

            FirebaseRemoteConfig.DefaultInstance.FetchAndActivateAsync().ContinueWithOnMainThread(task =>
            {
                s_fetching = false;

                if (task.IsFaulted)
                {
                    Debug.LogError($"{Tag} Fetch failed: {task.Exception}");
                    onComplete?.Invoke(false);
                    return;
                }

                s_ready = true;
                var result = task.Result;
                Debug.Log($"{Tag} Fetch complete (changed: {result})");
                onComplete?.Invoke(true);
            });
        }

        /// <summary>
        ///     Get a string value from Remote Config.
        /// </summary>
        public static string GetString(string key, string defaultValue = "")
        {
            if (!s_init) return defaultValue;

            var value = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
            return value.Source == ValueSource.StaticValue ? defaultValue : value.StringValue;
        }

        /// <summary>
        ///     Get a boolean value from Remote Config.
        /// </summary>
        public static bool GetBool(string key, bool defaultValue = false)
        {
            if (!s_init) return defaultValue;

            var value = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
            return value.Source == ValueSource.StaticValue ? defaultValue : value.BooleanValue;
        }

        /// <summary>
        ///     Get a long (int64) value from Remote Config.
        /// </summary>
        public static long GetLong(string key, long defaultValue = 0)
        {
            if (!s_init) return defaultValue;

            var value = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
            return value.Source == ValueSource.StaticValue ? defaultValue : value.LongValue;
        }

        /// <summary>
        ///     Get an int value from Remote Config.
        /// </summary>
        public static int GetInt(string key, int defaultValue = 0)
        {
            return (int)GetLong(key, defaultValue);
        }

        /// <summary>
        ///     Get a double value from Remote Config.
        /// </summary>
        public static double GetDouble(string key, double defaultValue = 0.0)
        {
            if (!s_init) return defaultValue;

            var value = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
            return value.Source == ValueSource.StaticValue ? defaultValue : value.DoubleValue;
        }

        /// <summary>
        ///     Get a float value from Remote Config.
        /// </summary>
        public static float GetFloat(string key, float defaultValue = 0f)
        {
            return (float)GetDouble(key, defaultValue);
        }

        /// <summary>
        ///     Get all keys from Remote Config.
        /// </summary>
        public static IEnumerable<string> GetKeys()
        {
            if (!s_init) return Array.Empty<string>();
            return FirebaseRemoteConfig.DefaultInstance.Keys;
        }

        /// <summary>
        ///     Get the last fetch time.
        /// </summary>
        public static DateTime LastFetchTime => s_init 
            ? FirebaseRemoteConfig.DefaultInstance.Info.FetchTime 
            : DateTime.MinValue;

        /// <summary>
        ///     Get the last fetch status.
        /// </summary>
        public static LastFetchStatus LastFetchStatus => s_init
            ? FirebaseRemoteConfig.DefaultInstance.Info.LastFetchStatus
            : LastFetchStatus.Pending;
    }
}
#else
using System;
using System.Collections.Generic;

namespace Sorolla.Adapters
{
    /// <summary>
    ///     Firebase Remote Config adapter stub (SDK not installed).
    /// </summary>
    public static class FirebaseRemoteConfigAdapter
    {
        public static bool IsReady => false;
        public static void Initialize(Dictionary<string, object> defaults = null, bool autoFetch = true) => UnityEngine.Debug.LogWarning("[Sorolla:RemoteConfig] Not installed");
        public static void FetchAndActivate(Action<bool> onComplete, int cacheExpirationSeconds = 3600) => onComplete?.Invoke(false);
        public static string GetString(string key, string defaultValue = "") => defaultValue;
        public static bool GetBool(string key, bool defaultValue = false) => defaultValue;
        public static long GetLong(string key, long defaultValue = 0) => defaultValue;
        public static int GetInt(string key, int defaultValue = 0) => defaultValue;
        public static double GetDouble(string key, double defaultValue = 0.0) => defaultValue;
        public static float GetFloat(string key, float defaultValue = 0f) => defaultValue;
        public static IEnumerable<string> GetKeys() => Array.Empty<string>();
        public static DateTime LastFetchTime => DateTime.MinValue;
    }
}
#endif
