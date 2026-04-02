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
        const string Tag = "[Palette:Firebase]";
        private bool _initRequested;
        private bool _ready;
        private bool _consent;
        private readonly Queue<System.Action> _pendingEvents = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        [Preserve]
        private static void Register()
        {
            Debug.Log($"{Tag} Register() called - assembly is loaded!");
            FirebaseAdapter.RegisterImpl(new FirebaseAdapterImpl());
        }

        public bool IsReady => _ready;

        public void Initialize(bool consent)
        {
            if (_initRequested) return;
            _initRequested = true;
            _consent = consent;

            FirebaseCoreManager.Initialize(available =>
            {
                if (available)
                {
                    FirebaseAnalytics.SetAnalyticsCollectionEnabled(_consent);
                    Debug.Log($"{Tag} Initialized (analytics collection: {_consent})");
                    _ready = true;
                    FlushPendingEvents();
                }
                else
                {
                    Debug.LogError($"{Tag} Firebase not available");
                }
            });
        }

        public void UpdateConsent(bool consent)
        {
            _consent = consent;
            if (_ready)
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(consent);
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

        public void TrackEvent(string eventName, Dictionary<string, object> parameters)
        {
            // Defensive copy - caller may modify dict after this call
            var paramsCopy = parameters != null ? new Dictionary<string, object>(parameters) : null;

            QueueOrExecute(() =>
            {
                var sanitized = SanitizeEventName(eventName);
                if (sanitized == null) return;

                if (paramsCopy == null || paramsCopy.Count == 0)
                {
                    FirebaseAnalytics.LogEvent(sanitized);
                    return;
                }

                var firebaseParams = BuildFirebaseParams(paramsCopy);
                if (firebaseParams == null) return;

                FirebaseAnalytics.LogEvent(sanitized, firebaseParams);
            });
        }

        public void TrackDesignEvent(string eventName, float value)
        {
            QueueOrExecute(() =>
            {
                var sanitized = SanitizeEventName(eventName);
                if (sanitized == null) return;

                if (value != 0)
                    FirebaseAnalytics.LogEvent(sanitized, "value", value);
                else
                    FirebaseAnalytics.LogEvent(sanitized);
            });
        }

        public void TrackProgressionEvent(string status, string p1, string p2, string p3, int score,
            Dictionary<string, object> extraParams)
        {
            // Defensive copy
            var extraCopy = extraParams != null ? new Dictionary<string, object>(extraParams) : null;

            QueueOrExecute(() =>
            {
                // Firebase mapping: Start -> level_start, Complete -> level_end, Fail -> level_fail
                string eventName;
                if (status == "start")
                    eventName = FirebaseAnalytics.EventLevelStart;
                else if (status == "complete")
                    eventName = FirebaseAnalytics.EventLevelEnd;
                else
                    eventName = "level_fail";

                var parameters = new List<Parameter>
                {
                    new(FirebaseAnalytics.ParameterLevelName, BuildCanonicalLevelName(p1, p2, p3))
                };

                if (score > 0)
                    parameters.Add(new Parameter(FirebaseAnalytics.ParameterScore, score));

                // Merge extra params (skip collisions with base params)
                if (extraCopy != null)
                {
                    var reservedKeys = new HashSet<string>
                    {
                        FirebaseAnalytics.ParameterLevelName, FirebaseAnalytics.ParameterScore
                    };
                    MergeExtraParams(parameters, extraCopy, reservedKeys);
                }

                FirebaseAnalytics.LogEvent(eventName, parameters.ToArray());
            });
        }

        public void TrackResourceEvent(string flowType, string currency, float amount, string itemType, string itemId,
            Dictionary<string, object> extraParams)
        {
            // Defensive copy
            var extraCopy = extraParams != null ? new Dictionary<string, object>(extraParams) : null;

            QueueOrExecute(() =>
            {
                string eventName = flowType == "source"
                    ? FirebaseAnalytics.EventEarnVirtualCurrency
                    : FirebaseAnalytics.EventSpendVirtualCurrency;

                var parameters = new List<Parameter>
                {
                    new(FirebaseAnalytics.ParameterVirtualCurrencyName, currency),
                    new(FirebaseAnalytics.ParameterValue, amount)
                };

                if (flowType != "source" && !string.IsNullOrEmpty(itemId))
                    parameters.Add(new Parameter("item_name", itemId));

                // Merge extra params (skip collisions with base params)
                if (extraCopy != null)
                {
                    var reservedKeys = new HashSet<string>
                    {
                        FirebaseAnalytics.ParameterVirtualCurrencyName,
                        FirebaseAnalytics.ParameterValue,
                        "item_name"
                    };
                    MergeExtraParams(parameters, extraCopy, reservedKeys);
                }

                FirebaseAnalytics.LogEvent(eventName, parameters.ToArray());
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

        public void TrackAdImpression(string adPlatform, string adSource, string adFormat, string adUnitName, double revenue, string currency)
        {
            QueueOrExecute(() =>
            {
                var parameters = new[]
                {
                    new Parameter("ad_platform", adPlatform ?? ""),
                    new Parameter("ad_source", adSource ?? ""),
                    new Parameter("ad_format", adFormat ?? ""),
                    new Parameter("ad_unit_name", adUnitName ?? ""),
                    new Parameter("value", revenue),
                    new Parameter("currency", currency ?? "USD")
                };

                FirebaseAnalytics.LogEvent("ad_impression", parameters);
            });
        }

        public void TrackPurchase(string productId, double price, string currency, string transactionId)
        {
            QueueOrExecute(() =>
            {
                FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventPurchase, new[]
                {
                    new Parameter(FirebaseAnalytics.ParameterCurrency, currency ?? "USD"),
                    new Parameter(FirebaseAnalytics.ParameterValue, price),
                    new Parameter(FirebaseAnalytics.ParameterTransactionID, transactionId ?? ""),
                    new Parameter(FirebaseAnalytics.ParameterItemID, productId ?? ""),
                });
            });
        }

        #region Helpers

        /// <summary>
        ///     Build deterministic level_name from progression parts.
        ///     e.g. ("World3", "Level12", null) -> "world3_level12"
        /// </summary>
        private static string BuildCanonicalLevelName(string p1, string p2, string p3)
        {
            if (string.IsNullOrEmpty(p1)) return "";
            var name = p1.ToLowerInvariant();
            if (!string.IsNullOrEmpty(p2)) name += "_" + p2.ToLowerInvariant();
            if (!string.IsNullOrEmpty(p3)) name += "_" + p3.ToLowerInvariant();
            return name;
        }

        /// <summary>
        ///     Convert a Dictionary param value to a Firebase Parameter.
        ///     Only supports: string, int, long, float, double, bool, enum.
        /// </summary>
        private static Parameter ToFirebaseParameter(string key, object value)
        {
            // Sanitize param name
            var sanitizedKey = SanitizeParamName(key);
            if (sanitizedKey == null) return null;

            return value switch
            {
                string s => new Parameter(sanitizedKey, s.Length > 100 ? s[..100] : s),
                int i => new Parameter(sanitizedKey, (long)i),
                long l => new Parameter(sanitizedKey, l),
                float f => new Parameter(sanitizedKey, (double)f),
                double d => new Parameter(sanitizedKey, d),
                bool b => new Parameter(sanitizedKey, b ? 1L : 0L),
                System.Enum e => new Parameter(sanitizedKey, e.ToString()),
                _ => null // Should not reach here - Palette validates before calling
            };
        }

        /// <summary>
        ///     Build Firebase Parameter array from validated dictionary.
        /// </summary>
        private static Parameter[] BuildFirebaseParams(Dictionary<string, object> parameters)
        {
            var result = new List<Parameter>(parameters.Count);
            foreach (var kvp in parameters)
            {
                var param = ToFirebaseParameter(kvp.Key, kvp.Value);
                if (param != null)
                    result.Add(param);
            }
            return result.ToArray();
        }

        /// <summary>
        ///     Merge extra params into an existing parameter list, skipping reserved keys.
        /// </summary>
        private static void MergeExtraParams(List<Parameter> target, Dictionary<string, object> extra,
            HashSet<string> reservedKeys)
        {
            foreach (var kvp in extra)
            {
                var sanitizedKey = SanitizeParamName(kvp.Key);
                if (sanitizedKey == null) continue;

                if (reservedKeys.Contains(sanitizedKey))
                {
                    Debug.LogWarning($"{Tag} Extra param '{kvp.Key}' collides with base param - skipped");
                    continue;
                }

                var param = ToFirebaseParameter(kvp.Key, kvp.Value);
                if (param != null)
                    target.Add(param);
            }
        }

        private static string SanitizeEventName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

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

            return result.Length > 0 ? result.ToString() : null;
        }

        private static string SanitizeParamName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var sanitized = name.Replace(":", "_").Replace("-", "_").Replace(" ", "_").Replace(".", "_");

            var result = new System.Text.StringBuilder();
            foreach (var c in sanitized)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    result.Append(c);
            }

            if (result.Length > 40)
                return result.ToString(0, 40);

            return result.Length > 0 ? result.ToString() : null;
        }

        #endregion
    }
}
