#if FIREBASE_REMOTE_CONFIG_INSTALLED
using System;
using System.Collections.Generic;
using Firebase.RemoteConfig;
using Firebase.Extensions;
using UnityEngine;

namespace Sorolla.Adapters
{
    /// <summary>
    ///     Firebase Remote Config adapter. Use Sorolla API instead.
    /// </summary>
    public static class FirebaseRemoteConfigAdapter
    {
        private const string Tag = "[Sorolla:RemoteConfig]";
        private static bool s_initRequested;
        private static bool s_init;
        private static bool s_ready;
        private static bool s_fetching;
        private static Dictionary<string, object> s_pendingDefaults;
        private static bool s_pendingAutoFetch;
        private static Action<bool> s_pendingFetchCallback;

        /// <summary>Whether Remote Config is initialized and has fetched values</summary>
        public static bool IsReady => s_ready;

        /// <summary>
        ///     Initialize Remote Config with optional defaults.
        ///     Will wait for Firebase core to be ready first.
        /// </summary>
        /// <param name="defaults">Default values to use before fetch completes</param>
        /// <param name="autoFetch">Whether to automatically fetch on init</param>
        public static void Initialize(Dictionary<string, object> defaults = null, bool autoFetch = true)
        {
            if (s_initRequested)
                return;
            s_initRequested = true;
            s_pendingDefaults = defaults;
            s_pendingAutoFetch = autoFetch;

            FirebaseCoreManager.Initialize(available =>
            {
                if (available)
                {
                    InitializeInternal();
                }
                else
                {
                    Debug.LogError($"{Tag} Firebase not available");
                    // Still invoke pending callback with failure
                    if (s_pendingFetchCallback != null)
                    {
                        var callback = s_pendingFetchCallback;
                        s_pendingFetchCallback = null;
                        callback.Invoke(false);
                    }
                }
            });
        }

        private static void InitializeInternal()
        {
            if (s_init) return;
            s_init = true;

            // Set defaults if provided
            if (s_pendingDefaults != null && s_pendingDefaults.Count > 0)
            {
                FirebaseRemoteConfig.DefaultInstance.SetDefaultsAsync(s_pendingDefaults).ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted)
                        Debug.LogError($"{Tag} Failed to set defaults: {task.Exception}");
                    else
                        Debug.Log($"{Tag} Defaults set ({s_pendingDefaults.Count} values)");

                    s_pendingDefaults = null;
                });
            }

            // Execute pending fetch callback or auto-fetch
            if (s_pendingFetchCallback != null)
            {
                var callback = s_pendingFetchCallback;
                s_pendingFetchCallback = null;
                FetchAndActivate(callback);
            }
            else if (s_pendingAutoFetch)
            {
                FetchAndActivate(null);
            }
            else
            {
                s_ready = true;
            }
        }

        /// <summary>
        ///     Fetch remote config values and activate them.
        /// </summary>
        /// <param name="onComplete">Callback when fetch completes (success/failure)</param>
        public static void FetchAndActivate(Action<bool> onComplete = null)
        {
            if (!s_init)
            {
                // Queue fetch if initialization in progress
                if (s_initRequested)
                {
                    s_pendingFetchCallback = onComplete;
                    return;
                }
                
                Debug.LogWarning($"{Tag} Not initialized");
                onComplete?.Invoke(false);
                return;
            }

            if (s_fetching)
                return;

            s_fetching = true;

            FirebaseRemoteConfig.DefaultInstance.FetchAndActivateAsync().ContinueWithOnMainThread(task =>
            {
                s_fetching = false;
                s_ready = true;

                if (task.IsFaulted)
                {
                    Debug.LogError($"{Tag} Fetch failed: {task.Exception?.Message}");
                    onComplete?.Invoke(false);
                    return;
                }

                Debug.Log($"{Tag} Fetch complete (activated: {task.Result})");
                onComplete?.Invoke(true);
            });
        }

        /// <summary>
        ///     Get a string value from Remote Config.
        /// </summary>
        public static string GetString(string key, string defaultValue = "")
        {
            if (!s_init)
            {
                Debug.LogWarning($"{Tag} Not initialized, returning default for '{key}'");
                return defaultValue;
            }

            try
            {
                var value = FirebaseRemoteConfig.DefaultInstance.GetValue(key).StringValue;
                return string.IsNullOrEmpty(value) ? defaultValue : value;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Tag} Error getting '{key}': {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        ///     Get a boolean value from Remote Config.
        /// </summary>
        public static bool GetBool(string key, bool defaultValue = false)
        {
            if (!s_init)
            {
                Debug.LogWarning($"{Tag} Not initialized, returning default for '{key}'");
                return defaultValue;
            }

            try
            {
                var configValue = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
                // If the key doesn't exist (StaticValue with empty string), return default
                if (configValue.Source == ValueSource.StaticValue && string.IsNullOrEmpty(configValue.StringValue))
                    return defaultValue;
                return configValue.BooleanValue;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Tag} Error getting '{key}': {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        ///     Get a long (int64) value from Remote Config.
        /// </summary>
        public static long GetLong(string key, long defaultValue = 0)
        {
            if (!s_init)
            {
                Debug.LogWarning($"{Tag} Not initialized, returning default for '{key}'");
                return defaultValue;
            }

            try
            {
                var configValue = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
                if (configValue.Source == ValueSource.StaticValue && string.IsNullOrEmpty(configValue.StringValue))
                    return defaultValue;
                return configValue.LongValue;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Tag} Error getting '{key}': {ex.Message}");
                return defaultValue;
            }
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
            if (!s_init)
            {
                Debug.LogWarning($"{Tag} Not initialized, returning default for '{key}'");
                return defaultValue;
            }

            try
            {
                var configValue = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
                if (configValue.Source == ValueSource.StaticValue && string.IsNullOrEmpty(configValue.StringValue))
                    return defaultValue;
                return configValue.DoubleValue;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Tag} Error getting '{key}': {ex.Message}");
                return defaultValue;
            }
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
        public static void FetchAndActivate(Action<bool> onComplete = null) => onComplete?.Invoke(false);
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
