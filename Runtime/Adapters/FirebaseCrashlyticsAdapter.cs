using System;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     Interface for Firebase Crashlytics adapter implementation.
    /// </summary>
    internal interface IFirebaseCrashlyticsAdapter
    {
        bool IsReady { get; }
        bool IsCrashlyticsCollectionEnabled { get; }
        void Initialize(bool captureUncaughtExceptions);
        void LogException(Exception exception);
        void Log(string message);
        void SetCustomKey(string key, string value);
        void SetUserId(string userId);
    }

    /// <summary>
    ///     Firebase Crashlytics adapter. Delegates to implementation when available.
    /// </summary>
    public static class FirebaseCrashlyticsAdapter
    {
        private static IFirebaseCrashlyticsAdapter s_impl;

        internal static void RegisterImpl(IFirebaseCrashlyticsAdapter impl)
        {
            s_impl = impl;
            UnityEngine.Debug.Log("[Sorolla:Crashlytics] Implementation registered");
        }

        public static bool IsReady => s_impl?.IsReady ?? false;
        public static bool IsCrashlyticsCollectionEnabled => s_impl?.IsCrashlyticsCollectionEnabled ?? false;

        public static void Initialize(bool captureUncaughtExceptions = true)
        {
            if (s_impl != null)
                s_impl.Initialize(captureUncaughtExceptions);
            else
                UnityEngine.Debug.LogWarning("[Sorolla:Crashlytics] Not installed");
        }

        public static void LogException(Exception exception) => s_impl?.LogException(exception);
        public static void Log(string message) => s_impl?.Log(message);
        public static void SetCustomKey(string key, string value) => s_impl?.SetCustomKey(key, value);
        public static void SetCustomKey(string key, int value) => s_impl?.SetCustomKey(key, value.ToString());
        public static void SetCustomKey(string key, bool value) => s_impl?.SetCustomKey(key, value.ToString());
        public static void SetCustomKey(string key, float value) => s_impl?.SetCustomKey(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        public static void SetUserId(string userId) => s_impl?.SetUserId(userId);
    }
}
