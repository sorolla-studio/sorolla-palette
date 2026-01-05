using System.Collections.Generic;
using Firebase.Analytics;
using UnityEngine;
using UnityEngine.Scripting;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     Firebase Analytics adapter implementation. Registered at runtime.
    /// </summary>
    [Preserve]
    internal class FirebaseAdapterImpl : IFirebaseAdapter
    {
        private const string Tag = "[Sorolla:Firebase]";
        private bool _initRequested;
        private bool _ready;
        private readonly Queue<System.Action> _pendingEvents = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        [Preserve]
        private static void Register()
        {
            Debug.Log($"{Tag} Register() called - assembly is loaded!");
            FirebaseAdapter.RegisterImpl(new FirebaseAdapterImpl());
        }

        public bool IsReady => _ready;

        public void Initialize()
        {
            if (_initRequested) return;
            _initRequested = true;

            FirebaseCoreManager.Initialize(available =>
            {
                if (available)
                {
                    Debug.Log($"{Tag} Initialized");
                    _ready = true;
                    FlushPendingEvents();
                }
                else
                {
                    Debug.LogError($"{Tag} Firebase not available");
                }
            });
        }

        private void FlushPendingEvents()
        {
            while (_pendingEvents.Count > 0)
            {
                var action = _pendingEvents.Dequeue();
                action?.Invoke();
            }
        }

        private void QueueOrExecute(System.Action action)
        {
            if (_ready)
                action();
            else
                _pendingEvents.Enqueue(action);
        }

        public void TrackDesignEvent(string eventName, float value)
        {
            QueueOrExecute(() =>
            {
                var sanitized = SanitizeEventName(eventName);

                if (value != 0)
                    FirebaseAnalytics.LogEvent(sanitized, "value", value);
                else
                    FirebaseAnalytics.LogEvent(sanitized);
            });
        }

        public void TrackProgressionEvent(string status, string p1, string p2, string p3, int score)
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

        public void TrackResourceEvent(string flowType, string currency, float amount, string itemType, string itemId)
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

        public void SetUserId(string userId)
        {
            QueueOrExecute(() => FirebaseAnalytics.SetUserId(userId));
        }

        public void SetUserProperty(string name, string value)
        {
            QueueOrExecute(() => FirebaseAnalytics.SetUserProperty(name, value));
        }

        private static string SanitizeEventName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unnamed_event";

            var sanitized = name.Replace(":", "_").Replace("-", "_").Replace(" ", "_").Replace(".", "_");

            var result = new System.Text.StringBuilder();
            foreach (var c in sanitized)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    result.Append(c);
            }

            if (result.Length > 0 && !char.IsLetter(result[0]))
                result.Insert(0, 'e');

            if (result.Length > 40)
                return result.ToString(0, 40);

            return result.Length > 0 ? result.ToString() : "unnamed_event";
        }
    }
}
