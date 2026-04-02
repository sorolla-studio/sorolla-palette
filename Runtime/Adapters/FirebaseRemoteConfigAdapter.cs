using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     Interface for Firebase Remote Config adapter implementation.
    /// </summary>
    internal interface IFirebaseRemoteConfigAdapter
    {
        bool IsReady { get; }
        bool AutoActivateUpdates { get; set; }
        void Initialize(Dictionary<string, object> defaults, bool autoFetch);
        void SetDefaults(Dictionary<string, object> defaults);
        void FetchAndActivate(Action<bool> onComplete);
        Task<bool> ActivateAsync();
        string GetString(string key, string defaultValue);
        bool GetBool(string key, bool defaultValue);
        long GetLong(string key, long defaultValue);
        int GetInt(string key, int defaultValue);
        double GetDouble(string key, double defaultValue);
        float GetFloat(string key, float defaultValue);
        IEnumerable<string> GetKeys();
        event Action<IReadOnlyCollection<string>> OnConfigUpdated;
    }

    /// <summary>
    ///     Firebase Remote Config adapter. Delegates to implementation when available.
    /// </summary>
    public static class FirebaseRemoteConfigAdapter
    {
        const string Tag = "[Palette:RemoteConfig]";

        private static IFirebaseRemoteConfigAdapter s_impl;

        internal static void RegisterImpl(IFirebaseRemoteConfigAdapter impl)
        {
            s_impl = impl;
            UnityEngine.Debug.Log($"{Tag} Implementation registered");
        }

        public static bool IsReady => s_impl?.IsReady ?? false;

        /// <summary>
        ///     When true, real-time config updates are activated immediately.
        ///     Set false for games where mid-session activation would be jarring.
        /// </summary>
        public static bool AutoActivateUpdates
        {
            get => s_impl?.AutoActivateUpdates ?? true;
            set { if (s_impl != null) s_impl.AutoActivateUpdates = value; }
        }

        public static void Initialize(Dictionary<string, object> defaults = null, bool autoFetch = true)
        {
            if (s_impl != null)
                s_impl.Initialize(defaults, autoFetch);
            else
                UnityEngine.Debug.LogWarning($"{Tag} Not installed");
        }

        /// <summary>
        ///     Set in-app defaults. Works before or after initialization.
        /// </summary>
        public static void SetDefaults(Dictionary<string, object> defaults)
        {
            if (s_impl != null)
                s_impl.SetDefaults(defaults);
            else
                UnityEngine.Debug.LogWarning($"{Tag} Not installed");
        }

        public static void FetchAndActivate(Action<bool> onComplete = null)
        {
            if (s_impl != null)
                s_impl.FetchAndActivate(onComplete);
            else
                onComplete?.Invoke(false);
        }

        /// <summary>
        ///     Manually activate fetched config. Use when AutoActivateUpdates is false.
        /// </summary>
        public static Task<bool> ActivateAsync()
        {
            if (s_impl != null)
                return s_impl.ActivateAsync();
            return Task.FromResult(false);
        }

        public static string GetString(string key, string defaultValue = "") => s_impl?.GetString(key, defaultValue) ?? defaultValue;
        public static bool GetBool(string key, bool defaultValue = false) => s_impl?.GetBool(key, defaultValue) ?? defaultValue;
        public static long GetLong(string key, long defaultValue = 0) => s_impl?.GetLong(key, defaultValue) ?? defaultValue;
        public static int GetInt(string key, int defaultValue = 0) => s_impl?.GetInt(key, defaultValue) ?? defaultValue;
        public static double GetDouble(string key, double defaultValue = 0.0) => s_impl?.GetDouble(key, defaultValue) ?? defaultValue;
        public static float GetFloat(string key, float defaultValue = 0f) => s_impl?.GetFloat(key, defaultValue) ?? defaultValue;
        public static IEnumerable<string> GetKeys() => s_impl?.GetKeys() ?? Array.Empty<string>();

        /// <summary>
        ///     Fired when real-time config update is received. Includes updated keys.
        ///     If AutoActivateUpdates is true, values are already activated when this fires.
        /// </summary>
        public static event Action<IReadOnlyCollection<string>> OnConfigUpdated
        {
            add { if (s_impl != null) s_impl.OnConfigUpdated += value; }
            remove { if (s_impl != null) s_impl.OnConfigUpdated -= value; }
        }
    }
}
