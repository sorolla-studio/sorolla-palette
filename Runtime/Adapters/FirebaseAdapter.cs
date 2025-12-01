#if FIREBASE_ANALYTICS_INSTALLED
using System.Collections.Generic;
using Firebase.Analytics;
using UnityEngine;

namespace Sorolla.Adapters
{
    /// <summary>
    ///     Firebase Analytics adapter. Use Sorolla API instead.
    /// </summary>
    public static class FirebaseAdapter
    {
        private const string Tag = "[Sorolla:Firebase]";
        private static bool s_initRequested;
        private static bool s_ready;
        private static readonly Queue<System.Action> s_pendingEvents = new();

        public static bool IsReady => s_ready;

        public static void Initialize()
        {
            if (s_initRequested) return;
            s_initRequested = true;

            FirebaseCoreManager.Initialize(available =>
            {
                if (available)
                {
                    Debug.Log($"{Tag} Initialized");
                    s_ready = true;
                    FlushPendingEvents();
                }
                else
                {
                    Debug.LogError($"{Tag} Firebase not available");
                }
            });
        }

        private static void FlushPendingEvents()
        {
            while (s_pendingEvents.Count > 0)
            {
                var action = s_pendingEvents.Dequeue();
                action?.Invoke();
            }
        }

        private static void QueueOrExecute(System.Action action)
        {
            if (s_ready)
                action();
            else
                s_pendingEvents.Enqueue(action);
        }

        public static void TrackDesignEvent(string eventName, float value = 0)
        {
            QueueOrExecute(() =>
            {
                // Firebase event names: alphanumeric + underscore, max 40 chars
                var sanitized = SanitizeEventName(eventName);

                if (value != 0)
                    FirebaseAnalytics.LogEvent(sanitized, "value", value);
                else
                    FirebaseAnalytics.LogEvent(sanitized);
            });
        }

        public static void TrackProgressionEvent(string status, string p1, string p2 = null, string p3 = null, int score = 0)
        {
            QueueOrExecute(() =>
            {
                var parameters = new List<Parameter>
                {
                    new("status", status),
                    new("progression_01", p1 ?? "")
                };

                if (!string.IsNullOrEmpty(p2))
                    parameters.Add(new Parameter("progression_02", p2));

                if (!string.IsNullOrEmpty(p3))
                    parameters.Add(new Parameter("progression_03", p3));

                if (score > 0)
                    parameters.Add(new Parameter("score", score));

                FirebaseAnalytics.LogEvent("progression", parameters.ToArray());
            });
        }

        public static void TrackResourceEvent(string flowType, string currency, float amount, string itemType, string itemId)
        {
            QueueOrExecute(() =>
            {
                var parameters = new[]
                {
                    new Parameter("flow_type", flowType),
                    new Parameter("currency", currency),
                    new Parameter("amount", amount),
                    new Parameter("item_type", itemType),
                    new Parameter("item_id", itemId)
                };

                FirebaseAnalytics.LogEvent("resource_flow", parameters);
            });
        }

        public static void SetUserId(string userId)
        {
            QueueOrExecute(() => FirebaseAnalytics.SetUserId(userId));
        }

        public static void SetUserProperty(string name, string value)
        {
            QueueOrExecute(() => FirebaseAnalytics.SetUserProperty(name, value));
        }

        /// <summary>
        ///     Sanitize event name for Firebase (alphanumeric + underscore, max 40 chars)
        /// </summary>
        private static string SanitizeEventName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unnamed_event";

            // Replace common separators with underscore
            var sanitized = name.Replace(":", "_").Replace("-", "_").Replace(" ", "_").Replace(".", "_");

            // Remove any remaining invalid characters
            var result = new System.Text.StringBuilder();
            foreach (var c in sanitized)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    result.Append(c);
            }

            // Ensure it starts with a letter
            if (result.Length > 0 && !char.IsLetter(result[0]))
                result.Insert(0, 'e');

            // Truncate to 40 chars
            if (result.Length > 40)
                return result.ToString(0, 40);

            return result.Length > 0 ? result.ToString() : "unnamed_event";
        }
    }
}
#else
namespace Sorolla.Adapters
{
    /// <summary>
    ///     Firebase Analytics adapter stub (SDK not installed).
    /// </summary>
    public static class FirebaseAdapter
    {
        public static bool IsReady => false;
        public static void Initialize() => UnityEngine.Debug.LogWarning("[Sorolla:Firebase] Not installed");
        public static void TrackDesignEvent(string eventName, float value = 0) { }
        public static void TrackProgressionEvent(string status, string p1, string p2 = null, string p3 = null, int score = 0) { }
        public static void TrackResourceEvent(string flowType, string currency, float amount, string itemType, string itemId) { }
        public static void SetUserId(string userId) { }
        public static void SetUserProperty(string name, string value) { }
    }
}
#endif
