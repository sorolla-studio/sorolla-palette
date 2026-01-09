using System;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     Interface for Firebase Analytics adapter implementation.
    /// </summary>
    internal interface IFirebaseAdapter
    {
        bool IsReady { get; }
        void Initialize();
        void TrackDesignEvent(string eventName, float value);
        void TrackProgressionEvent(string status, string p1, string p2, string p3, int score);
        void TrackResourceEvent(string flowType, string currency, float amount, string itemType, string itemId);
        void SetUserId(string userId);
        void SetUserProperty(string name, string value);
    }

    /// <summary>
    ///     Interface for Firebase Core Manager implementation.
    /// </summary>
    internal interface IFirebaseCoreManager
    {
        bool IsInitializing { get; }
        bool IsInitialized { get; }
        bool IsAvailable { get; }
        void Initialize(Action<bool> onReady);
    }

    /// <summary>
    ///     Firebase Analytics adapter. Delegates to implementation when available.
    /// </summary>
    public static class FirebaseAdapter
    {
        private static IFirebaseAdapter s_impl;

        internal static void RegisterImpl(IFirebaseAdapter impl)
        {
            s_impl = impl;
            UnityEngine.Debug.Log("[Sorolla:Firebase] Implementation registered");
        }

        public static bool IsReady => s_impl?.IsReady ?? false;

        public static void Initialize()
        {
            if (s_impl != null)
                s_impl.Initialize();
            else
                UnityEngine.Debug.LogWarning("[Sorolla:Firebase] Not installed");
        }

        public static void TrackDesignEvent(string eventName, float value = 0)
        {
            s_impl?.TrackDesignEvent(eventName, value);
        }

        public static void TrackProgressionEvent(string status, string p1, string p2 = null, string p3 = null, int score = 0)
        {
            s_impl?.TrackProgressionEvent(status, p1, p2, p3, score);
        }

        public static void TrackResourceEvent(string flowType, string currency, float amount, string itemType, string itemId)
        {
            s_impl?.TrackResourceEvent(flowType, currency, amount, itemType, itemId);
        }

        public static void SetUserId(string userId)
        {
            s_impl?.SetUserId(userId);
        }

        public static void SetUserProperty(string name, string value)
        {
            s_impl?.SetUserProperty(name, value);
        }
    }

    /// <summary>
    ///     Centralized Firebase initialization manager.
    ///     Delegates to implementation when available.
    /// </summary>
    public static class FirebaseCoreManager
    {
        private static IFirebaseCoreManager s_impl;

        internal static void RegisterImpl(IFirebaseCoreManager impl)
        {
            s_impl = impl;
            UnityEngine.Debug.Log("[Sorolla:FirebaseCore] Implementation registered");
        }

        public static bool IsInitializing => s_impl?.IsInitializing ?? false;
        public static bool IsInitialized => s_impl?.IsInitialized ?? false;
        public static bool IsAvailable => s_impl?.IsAvailable ?? false;

        public static void Initialize(Action<bool> onReady)
        {
            if (s_impl != null)
                s_impl.Initialize(onReady);
            else
                onReady?.Invoke(false);
        }
    }
}
