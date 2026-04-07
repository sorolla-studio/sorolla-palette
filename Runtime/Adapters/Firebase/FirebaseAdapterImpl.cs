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

        // GA4 reserved event names - dropped server-side anyway, drop here with a warning so studios notice.
        // Source: https://firebase.google.com/docs/reference/cpp/group/event-names
        private static readonly HashSet<string> ReservedEventNames = new()
        {
            "ad_activeview", "ad_click", "ad_exposure", "ad_query", "ad_reward", "adunit_exposure",
            "app_clear_data", "app_install", "app_remove", "app_update", "app_exception", "app_upgrade",
            "error", "first_open", "first_visit", "in_app_purchase",
            "notification_dismiss", "notification_foreground", "notification_open", "notification_receive",
            "os_update", "screen_view", "session_start", "session_start_with_rollout", "user_engagement"
        };

        private static readonly string[] ReservedNamePrefixes = { "ga_", "google_", "firebase_" };

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
                // GA4 spec: Start -> level_start, Complete -> level_end{success=1}, Fail -> level_end{success=0}.
                // level_fail is NOT a GA4 event - the built-in Games > Levels report only aggregates level_end.
                bool isStart = status == "start";
                string eventName = isStart ? FirebaseAnalytics.EventLevelStart : FirebaseAnalytics.EventLevelEnd;

                var parameters = new List<Parameter>
                {
                    new(FirebaseAnalytics.ParameterLevelName, BuildCanonicalLevelName(p1, p2, p3))
                };

                if (!isStart)
                {
                    long success = status == "complete" ? 1L : 0L;
                    parameters.Add(new Parameter(FirebaseAnalytics.ParameterSuccess, success));
                }

                // Merge extra params (skip collisions with base params).
                // Note: score is intentionally NOT attached to level_start/level_end - it's fired
                // separately as post_score below to match the canonical GA4 shape.
                if (extraCopy != null)
                {
                    var reservedKeys = new HashSet<string>
                    {
                        FirebaseAnalytics.ParameterLevelName, FirebaseAnalytics.ParameterSuccess
                    };
                    MergeExtraParams(parameters, extraCopy, reservedKeys);
                }

                FirebaseAnalytics.LogEvent(eventName, parameters.ToArray());

                // Canonical GA4 score routing: post_score is its own event with just the score.
                // Studios join to neighboring level_end via session_id in BigQuery if they need it.
                if (!isStart && score > 0)
                {
                    FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventPostScore,
                        new Parameter(FirebaseAnalytics.ParameterScore, score));
                }
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
                    parameters.Add(new Parameter(FirebaseAnalytics.ParameterItemName, itemId));

                // Merge extra params (skip collisions with base params)
                if (extraCopy != null)
                {
                    var reservedKeys = new HashSet<string>
                    {
                        FirebaseAnalytics.ParameterVirtualCurrencyName,
                        FirebaseAnalytics.ParameterValue,
                        FirebaseAnalytics.ParameterItemName
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
            // GA4 limits: name <= 24 chars, value <= 36 chars, name cannot start with reserved prefixes.
            // Names starting with ga_/google_/firebase_/_ are dropped server-side anyway - drop here too with a warning.
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning($"{Tag} SetUserProperty: empty name - dropped");
                return;
            }
            if (name.Length > 24)
            {
                Debug.LogWarning($"{Tag} SetUserProperty: name '{name}' exceeds 24 chars - dropped");
                return;
            }
            if (HasReservedUserPropertyPrefix(name))
            {
                Debug.LogWarning($"{Tag} SetUserProperty: name '{name}' uses reserved prefix (ga_/google_/firebase_/_) - dropped");
                return;
            }

            string sanitizedValue = value ?? "";
            if (sanitizedValue.Length > 36)
            {
                Debug.LogWarning($"{Tag} SetUserProperty: value for '{name}' exceeds 36 chars - truncated");
                sanitizedValue = sanitizedValue[..36];
            }

            QueueOrExecute(() => FirebaseAnalytics.SetUserProperty(name, sanitizedValue));
        }

        public void TrackAdImpression(string adPlatform, string adSource, string adFormat, string adUnitName, double revenue, string currency)
        {
            QueueOrExecute(() =>
            {
                var parameters = new[]
                {
                    new Parameter(FirebaseAnalytics.ParameterAdPlatform, adPlatform ?? ""),
                    new Parameter(FirebaseAnalytics.ParameterAdSource, adSource ?? ""),
                    new Parameter(FirebaseAnalytics.ParameterAdFormat, adFormat ?? ""),
                    new Parameter(FirebaseAnalytics.ParameterAdUnitName, adUnitName ?? ""),
                    new Parameter(FirebaseAnalytics.ParameterValue, revenue),
                    new Parameter(FirebaseAnalytics.ParameterCurrency, currency ?? "USD")
                };

                FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventAdImpression, parameters);
            });
        }

        public void TrackPurchase(string productId, double price, string currency, string transactionId)
        {
            QueueOrExecute(() =>
            {
                // GA4 spec: purchase requires items[] to populate the Monetization > In-app purchases report
                // (the same plumbing GA4 calls "Ecommerce purchases" - it's the canonical IAP shape).
                // Without items[], total revenue still flows but the per-product breakdown is lost.
                var items = new IDictionary<string, object>[]
                {
                    new Dictionary<string, object>
                    {
                        { FirebaseAnalytics.ParameterItemID, productId ?? "" }
                    }
                };

                FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventPurchase, new[]
                {
                    new Parameter(FirebaseAnalytics.ParameterCurrency, currency ?? "USD"),
                    new Parameter(FirebaseAnalytics.ParameterValue, price),
                    new Parameter(FirebaseAnalytics.ParameterTransactionID, transactionId ?? ""),
                    new Parameter(FirebaseAnalytics.ParameterItems, items)
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
                result.Length = 40;

            if (result.Length == 0) return null;

            var finalName = result.ToString();

            // Drop GA4-reserved names. Firebase silently rejects these server-side - warn so studios notice.
            if (ReservedEventNames.Contains(finalName))
            {
                Debug.LogWarning($"{Tag} Event name '{finalName}' is GA4-reserved - dropped");
                return null;
            }
            for (int i = 0; i < ReservedNamePrefixes.Length; i++)
            {
                if (finalName.StartsWith(ReservedNamePrefixes[i]))
                {
                    Debug.LogWarning($"{Tag} Event name '{finalName}' uses reserved prefix '{ReservedNamePrefixes[i]}' - dropped");
                    return null;
                }
            }

            return finalName;
        }

        private static bool HasReservedUserPropertyPrefix(string name)
        {
            if (name.StartsWith("_")) return true;
            for (int i = 0; i < ReservedNamePrefixes.Length; i++)
            {
                if (name.StartsWith(ReservedNamePrefixes[i])) return true;
            }
            return false;
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
