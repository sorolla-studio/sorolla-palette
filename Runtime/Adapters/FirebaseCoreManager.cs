using System;

namespace Sorolla.Palette.Adapters
{
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
