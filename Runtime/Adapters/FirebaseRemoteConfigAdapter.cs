using System;
using System.Collections.Generic;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     Interface for Firebase Remote Config adapter implementation.
    /// </summary>
    internal interface IFirebaseRemoteConfigAdapter
    {
        bool IsReady { get; }
        void Initialize(Dictionary<string, object> defaults, bool autoFetch);
        void FetchAndActivate(Action<bool> onComplete);
        string GetString(string key, string defaultValue);
        bool GetBool(string key, bool defaultValue);
        long GetLong(string key, long defaultValue);
        int GetInt(string key, int defaultValue);
        double GetDouble(string key, double defaultValue);
        float GetFloat(string key, float defaultValue);
        IEnumerable<string> GetKeys();
    }

    /// <summary>
    ///     Firebase Remote Config adapter. Delegates to implementation when available.
    /// </summary>
    public static class FirebaseRemoteConfigAdapter
    {
        private static IFirebaseRemoteConfigAdapter s_impl;

        internal static void RegisterImpl(IFirebaseRemoteConfigAdapter impl)
        {
            s_impl = impl;
            UnityEngine.Debug.Log("[Sorolla:RemoteConfig] Implementation registered");
        }

        public static bool IsReady => s_impl?.IsReady ?? false;

        public static void Initialize(Dictionary<string, object> defaults = null, bool autoFetch = true)
        {
            if (s_impl != null)
                s_impl.Initialize(defaults, autoFetch);
            else
                UnityEngine.Debug.LogWarning("[Sorolla:RemoteConfig] Not installed");
        }

        public static void FetchAndActivate(Action<bool> onComplete = null)
        {
            if (s_impl != null)
                s_impl.FetchAndActivate(onComplete);
            else
                onComplete?.Invoke(false);
        }

        public static string GetString(string key, string defaultValue = "") => s_impl?.GetString(key, defaultValue) ?? defaultValue;
        public static bool GetBool(string key, bool defaultValue = false) => s_impl?.GetBool(key, defaultValue) ?? defaultValue;
        public static long GetLong(string key, long defaultValue = 0) => s_impl?.GetLong(key, defaultValue) ?? defaultValue;
        public static int GetInt(string key, int defaultValue = 0) => s_impl?.GetInt(key, defaultValue) ?? defaultValue;
        public static double GetDouble(string key, double defaultValue = 0.0) => s_impl?.GetDouble(key, defaultValue) ?? defaultValue;
        public static float GetFloat(string key, float defaultValue = 0f) => s_impl?.GetFloat(key, defaultValue) ?? defaultValue;
        public static IEnumerable<string> GetKeys() => s_impl?.GetKeys() ?? Array.Empty<string>();
        public static DateTime LastFetchTime => DateTime.MinValue;
    }
}
