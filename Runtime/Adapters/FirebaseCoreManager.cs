#if FIREBASE_ANALYTICS_INSTALLED || FIREBASE_CRASHLYTICS_INSTALLED || FIREBASE_REMOTE_CONFIG_INSTALLED
using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Extensions;
using UnityEngine;

namespace Sorolla.Adapters
{
    /// <summary>
    ///     Centralized Firebase initialization manager.
    ///     Ensures CheckAndFixDependenciesAsync is only called once.
    /// </summary>
    public static class FirebaseCoreManager
    {
        private const string Tag = "[Sorolla:FirebaseCore]";
        
        private static bool s_initRequested;
        private static bool s_initialized;
        private static bool s_available;
        private static readonly List<Action<bool>> s_pendingCallbacks = new();

        /// <summary>Whether Firebase core initialization has been requested</summary>
        public static bool IsInitializing => s_initRequested && !s_initialized;
        
        /// <summary>Whether Firebase core has completed initialization</summary>
        public static bool IsInitialized => s_initialized;
        
        /// <summary>Whether Firebase dependencies are available</summary>
        public static bool IsAvailable => s_available;

        /// <summary>
        ///     Initialize Firebase core and invoke callback when ready.
        ///     Safe to call multiple times - callbacks are queued if init is in progress.
        /// </summary>
        public static void Initialize(Action<bool> onReady)
        {
            // If already initialized, invoke callback immediately
            if (s_initialized)
            {
                onReady?.Invoke(s_available);
                return;
            }

            // Queue the callback
            if (onReady != null)
                s_pendingCallbacks.Add(onReady);

            // If init already requested, just wait
            if (s_initRequested)
                return;

            s_initRequested = true;
            Debug.Log($"{Tag} Initializing...");

            try
            {
                FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
                {
                    s_initialized = true;

                    if (task.IsFaulted || task.IsCanceled)
                    {
                        Debug.LogError($"{Tag} Failed: {task.Exception?.Message ?? "Cancelled"}");
                        s_available = false;
                        InvokePendingCallbacks(false);
                        return;
                    }

                    var status = task.Result;
                    s_available = status == DependencyStatus.Available;
                    
                    if (s_available)
                        Debug.Log($"{Tag} Ready");
                    else
                        Debug.LogError($"{Tag} Dependencies not available: {status}");

                    InvokePendingCallbacks(s_available);
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Tag} Exception: {ex.Message}");
                s_initialized = true;
                s_available = false;
                InvokePendingCallbacks(false);
            }
        }

        private static void InvokePendingCallbacks(bool success)
        {
            foreach (var callback in s_pendingCallbacks)
            {
                try { callback?.Invoke(success); }
                catch (Exception ex) { Debug.LogError($"{Tag} Callback error: {ex.Message}"); }
            }
            s_pendingCallbacks.Clear();
        }
    }
}
#else
using System;

namespace Sorolla.Adapters
{
    /// <summary>
    ///     Firebase Core Manager stub (no Firebase SDKs installed).
    /// </summary>
    public static class FirebaseCoreManager
    {
        public static bool IsInitialized => false;
        public static bool IsAvailable => false;
        public static void Initialize(Action<bool> onReady) => onReady?.Invoke(false);
    }
}
#endif
