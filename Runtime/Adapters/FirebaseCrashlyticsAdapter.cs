#if FIREBASE_CRASHLYTICS_INSTALLED
using System;
using System.Collections.Generic;
using Firebase.Crashlytics;
using UnityEngine;

namespace Sorolla.Adapters
{
    /// <summary>
    ///     Firebase Crashlytics adapter. Use Sorolla API instead.
    /// </summary>
    public static class FirebaseCrashlyticsAdapter
    {
        private const string Tag = "[Sorolla:Crashlytics]";
        private static bool s_initRequested;
        private static bool s_ready;
        private static bool s_captureExceptions;
        private static readonly Queue<Action> s_pendingActions = new();

        public static bool IsReady => s_ready;

        /// <summary>
        ///     Initialize Crashlytics and optionally register for uncaught exceptions.
        /// </summary>
        /// <param name="captureUncaughtExceptions">If true, automatically logs Unity errors and exceptions</param>
        public static void Initialize(bool captureUncaughtExceptions = true)
        {
            if (s_initRequested) return;
            s_initRequested = true;
            s_captureExceptions = captureUncaughtExceptions;

            FirebaseCoreManager.Initialize(available =>
            {
                if (available)
                {
                    s_ready = true;
                    Debug.Log($"{Tag} Initialized");

                    if (s_captureExceptions)
                        Application.logMessageReceived += OnLogMessageReceived;

                    FlushPendingActions();
                }
                else
                {
                    Debug.LogError($"{Tag} Firebase not available");
                }
            });
        }

        private static void FlushPendingActions()
        {
            while (s_pendingActions.Count > 0)
            {
                var action = s_pendingActions.Dequeue();
                action?.Invoke();
            }
        }

        private static void QueueOrExecute(Action action)
        {
            if (s_ready)
                action();
            else if (s_initRequested)
                s_pendingActions.Enqueue(action);
        }

        /// <summary>
        ///     Unity log callback to capture errors and exceptions.
        /// </summary>
        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            // Only capture errors and exceptions
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                return;

            try
            {
                // Log custom key for context
                Crashlytics.SetCustomKey("unity_log_type", type.ToString());

                if (type == LogType.Exception)
                {
                    // For exceptions, try to parse and record as exception
                    Crashlytics.LogException(new Exception($"{condition}\n{stackTrace}"));
                }
                else
                {
                    // For errors/asserts, log as message
                    Crashlytics.Log($"[{type}] {condition}");
                    if (!string.IsNullOrEmpty(stackTrace))
                        Crashlytics.Log(stackTrace);
                }
            }
            catch
            {
                // Silently fail - don't cause additional errors in error handler
            }
        }

        /// <summary>
        ///     Log a non-fatal exception to Crashlytics.
        /// </summary>
        public static void LogException(Exception exception)
        {
            QueueOrExecute(() => Crashlytics.LogException(exception));
        }

        /// <summary>
        ///     Log a custom message to Crashlytics (appears in crash reports).
        /// </summary>
        public static void Log(string message)
        {
            QueueOrExecute(() => Crashlytics.Log(message));
        }

        /// <summary>
        ///     Set a custom key-value pair for crash reports.
        /// </summary>
        public static void SetCustomKey(string key, string value)
        {
            QueueOrExecute(() => Crashlytics.SetCustomKey(key, value));
        }

        /// <summary>
        ///     Set a custom key-value pair for crash reports.
        /// </summary>
        public static void SetCustomKey(string key, int value)
        {
            QueueOrExecute(() => Crashlytics.SetCustomKey(key, value.ToString()));
        }

        /// <summary>
        ///     Set a custom key-value pair for crash reports.
        /// </summary>
        public static void SetCustomKey(string key, bool value)
        {
            QueueOrExecute(() => Crashlytics.SetCustomKey(key, value.ToString()));
        }

        /// <summary>
        ///     Set a custom key-value pair for crash reports.
        /// </summary>
        public static void SetCustomKey(string key, float value)
        {
            QueueOrExecute(() => Crashlytics.SetCustomKey(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        /// <summary>
        ///     Set the user identifier for crash reports.
        /// </summary>
        public static void SetUserId(string userId)
        {
            QueueOrExecute(() => Crashlytics.SetUserId(userId));
        }

        /// <summary>
        ///     Check if Crashlytics collection is enabled.
        /// </summary>
        public static bool IsCrashlyticsCollectionEnabled => s_ready && Crashlytics.IsCrashlyticsCollectionEnabled;
    }
}
#else
using System;

namespace Sorolla.Adapters
{
    /// <summary>
    ///     Firebase Crashlytics adapter stub (SDK not installed).
    /// </summary>
    public static class FirebaseCrashlyticsAdapter
    {
        public static bool IsReady => false;
        public static void Initialize(bool captureUncaughtExceptions = true) => UnityEngine.Debug.LogWarning("[Sorolla:Crashlytics] Not installed");
        public static void LogException(Exception exception) { }
        public static void Log(string message) { }
        public static void SetCustomKey(string key, string value) { }
        public static void SetCustomKey(string key, int value) { }
        public static void SetCustomKey(string key, bool value) { }
        public static void SetCustomKey(string key, float value) { }
        public static void SetUserId(string userId) { }
        public static bool IsCrashlyticsCollectionEnabled => false;
    }
}
#endif
