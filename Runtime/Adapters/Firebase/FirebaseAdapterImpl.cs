using System;
using System.Collections.Generic;
using Firebase;
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

        // GA4 reserved event names - dropped server-side anyway, drop here with a warning so studios notice.
        // Source: https://firebase.google.com/docs/reference/cpp/group/event-names
        static readonly HashSet<string> ReservedEventNames = new HashSet<string>
        {
            "ad_activeview", "ad_click", "ad_exposure", "ad_query", "ad_reward", "adunit_exposure",
            "app_clear_data", "app_install", "app_remove", "app_update", "app_exception", "app_upgrade",
            "error", "first_open", "first_visit", "in_app_purchase",
            "notification_dismiss", "notification_foreground", "notification_open", "notification_receive",
            "os_update", "screen_view", "session_start", "session_start_with_rollout", "user_engagement",
        };

        static readonly string[] ReservedNamePrefixes = { "ga_", "google_", "firebase_" };
        readonly Queue<Action> _pendingEvents = new Queue<Action>();
        bool _adStorageConsent;
        bool _adPersonalizationConsent;
        bool _analyticsConsent;
        bool _initFailed;
        bool _initRequested;

        public bool IsReady { get; private set; }

        public void Initialize(bool adStorageConsent, bool adPersonalizationConsent, bool analyticsConsent, bool verboseLogging = false)
        {
            if (_initRequested) return;
            _initRequested = true;
            _adStorageConsent = adStorageConsent;
            _adPersonalizationConsent = adPersonalizationConsent;
            _analyticsConsent = analyticsConsent;

            FirebaseCoreManager.Initialize(available =>
            {
                if (available)
                {
                    FirebaseApp.LogLevel = verboseLogging
                        ? LogLevel.Debug
                        : LogLevel.Warning;

                    ApplyConsentSignals(_adStorageConsent, _adPersonalizationConsent, _analyticsConsent);
                    // Never disable collection. SetAnalyticsCollectionEnabled(false) only strips
                    // first_open's app-instance-id (making installs uncountable) and is unreliable
                    // on iOS — Consent Mode (SetConsent) governs identifiers instead. Force-enable
                    // to undo any persisted disabled state from prior SDK versions (the flag
                    // persists across launches per Firebase docs).
                    FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                    PaletteLog.Vital($"{Tag} Initialized (collection: on, analytics_consent: {_analyticsConsent}, ad_storage_consent: {_adStorageConsent}, ad_personalization_consent: {_adPersonalizationConsent}, verbose: {verboseLogging})");
                    IsReady = true;
                    FlushPendingEvents();
                }
                else
                {
                    PaletteLog.Error($"{Tag} Firebase not available");
                    _initFailed = true;
                    // Drop anything that queued between Initialize() and the failure callback.
                    _pendingEvents.Clear();
                }
            });
        }

        public void UpdateConsent(bool adStorageConsent, bool adPersonalizationConsent, bool analyticsConsent)
        {
            _adStorageConsent = adStorageConsent;
            _adPersonalizationConsent = adPersonalizationConsent;
            _analyticsConsent = analyticsConsent;
            if (IsReady)
                ApplyConsentSignals(adStorageConsent, adPersonalizationConsent, analyticsConsent);
        }

        public void TrackEvent(string eventName, Dictionary<string, object> parameters)
        {
            // Defensive copy - caller may modify dict after this call
            var paramsCopy = parameters != null ? new Dictionary<string, object>(parameters) : null;

            QueueOrExecute(() =>
            {
                string sanitized = SanitizeEventName(eventName);
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
                    new Parameter(FirebaseAnalytics.ParameterLevelName, BuildCanonicalLevelName(p1, p2, p3)),
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
                        FirebaseAnalytics.ParameterLevelName, FirebaseAnalytics.ParameterSuccess,
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
                // GA4 spec: earn_virtual_currency canonical params are virtual_currency_name + value
                // only; spend_virtual_currency additionally supports item_name. We respect that
                // asymmetry instead of stuffing item_name onto earn. The EconomySource/Sink enum
                // (itemType) and the granular itemId go to direction-specific custom params so
                // analysts can segment without abusing the canonical item_name slot on earn:
                //   earn  -> source (=enum), source_item (=itemId, only when caller supplied one)
                //   spend -> item_name (=itemId, canonical), sink (=enum)
                bool isSource = flowType == "source";
                string eventName = isSource
                    ? FirebaseAnalytics.EventEarnVirtualCurrency
                    : FirebaseAnalytics.EventSpendVirtualCurrency;

                var parameters = new List<Parameter>
                {
                    new Parameter(FirebaseAnalytics.ParameterVirtualCurrencyName, currency),
                    new Parameter(FirebaseAnalytics.ParameterValue, amount),
                };
                var reservedKeys = new HashSet<string>
                {
                    FirebaseAnalytics.ParameterVirtualCurrencyName,
                    FirebaseAnalytics.ParameterValue,
                    "source",
                    "source_item",
                    FirebaseAnalytics.ParameterItemName,
                    "sink",
                };

                if (isSource)
                {
                    if (!string.IsNullOrEmpty(itemType))
                    {
                        parameters.Add(new Parameter("source", itemType));
                        reservedKeys.Add("source");
                    }
                    if (!string.IsNullOrEmpty(itemId))
                    {
                        parameters.Add(new Parameter("source_item", itemId));
                        reservedKeys.Add("source_item");
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(itemId))
                    {
                        parameters.Add(new Parameter(FirebaseAnalytics.ParameterItemName, itemId));
                        reservedKeys.Add(FirebaseAnalytics.ParameterItemName);
                    }
                    if (!string.IsNullOrEmpty(itemType))
                    {
                        parameters.Add(new Parameter("sink", itemType));
                        reservedKeys.Add("sink");
                    }
                }

                if (extraCopy != null)
                    MergeExtraParams(parameters, extraCopy, reservedKeys);

                FirebaseAnalytics.LogEvent(eventName, parameters.ToArray());
            });
        }

        public void SetUserId(string userId) => QueueOrExecute(() => FirebaseAnalytics.SetUserId(userId));

        public void SetUserProperty(string name, string value)
        {
            // GA4 limits: name <= 24 chars, value <= 36 chars, name cannot start with reserved prefixes.
            // Names starting with ga_/google_/firebase_/_ are dropped server-side anyway - drop here too with a warning.
            if (string.IsNullOrEmpty(name))
            {
                PaletteLog.Warning($"{Tag} SetUserProperty: empty name - dropped");
                return;
            }
            if (name.Length > 24)
            {
                PaletteLog.Warning($"{Tag} SetUserProperty: name '{name}' exceeds 24 chars - dropped");
                return;
            }
            if (HasReservedUserPropertyPrefix(name))
            {
                PaletteLog.Warning($"{Tag} SetUserProperty: name '{name}' uses reserved prefix (ga_/google_/firebase_/_) - dropped");
                return;
            }

            string sanitizedValue = value ?? "";
            if (sanitizedValue.Length > 36)
            {
                PaletteLog.Warning($"{Tag} SetUserProperty: value for '{name}' exceeds 36 chars - truncated");
                sanitizedValue = sanitizedValue[..36];
            }

            QueueOrExecute(() => FirebaseAnalytics.SetUserProperty(name, sanitizedValue));
        }

        public void TrackAdImpression(string adPlatform, string adSource, string adFormat, string adUnitName, double revenue, string currency) => QueueOrExecute(() =>
        {
            var parameters = new[]
            {
                new Parameter(FirebaseAnalytics.ParameterAdPlatform, adPlatform ?? ""),
                new Parameter(FirebaseAnalytics.ParameterAdSource, adSource ?? ""),
                new Parameter(FirebaseAnalytics.ParameterAdFormat, adFormat ?? ""),
                new Parameter(FirebaseAnalytics.ParameterAdUnitName, adUnitName ?? ""),
                new Parameter(FirebaseAnalytics.ParameterValue, revenue),
                new Parameter(FirebaseAnalytics.ParameterCurrency, currency ?? "USD"),
            };

            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventAdImpression, parameters);
        });

        public void TrackPurchase(string productId, double price, string currency, string transactionId) => QueueOrExecute(() =>
        {
            // GA4 canonical purchase shape requires items[] with item_id + price + quantity for
            // the Monetization > In-app purchases per-product revenue breakdown to populate.
            // Spec: https://developers.google.com/analytics/devguides/collection/ga4/reference/events#purchase
            // Without price+quantity, total revenue flows via top-level `value` but per-product
            // drill-down in GA4 is degraded.
            var items = new IDictionary<string, object>[]
            {
                new Dictionary<string, object>
                {
                    { FirebaseAnalytics.ParameterItemID, productId ?? "" },
                    { FirebaseAnalytics.ParameterPrice, price },
                    { FirebaseAnalytics.ParameterQuantity, 1L },
                },
            };

            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventPurchase, new Parameter(FirebaseAnalytics.ParameterCurrency, currency ?? "USD"),
                new Parameter(FirebaseAnalytics.ParameterValue, price), new Parameter(FirebaseAnalytics.ParameterTransactionID, transactionId ?? ""),
                new Parameter(FirebaseAnalytics.ParameterItems, items));
        });

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        [Preserve]
        static void Register()
        {
            PaletteLog.Verbose($"{Tag} Register() called - assembly is loaded!");
            FirebaseAdapter.RegisterImpl(new FirebaseAdapterImpl());
        }

        /// <summary>
        ///     Sets Firebase Consent Mode v2 signals. The three axes are decoupled:
        ///     <list type="bullet">
        ///       <item><paramref name="analyticsConsent"/> gates analytics_storage (granted unless the
        ///       user is a confirmed GDPR decline) so installs/first_open stay countable.</item>
        ///       <item><paramref name="adStorageConsent"/> gates ad_storage and follows the GDPR/UMP
        ///       decision (granted only once the CMP resolves).</item>
        ///       <item><paramref name="adPersonalizationConsent"/> gates ad_personalization AND
        ///       ad_user_data. On iOS this additionally requires ATT authorization: personalized ads
        ///       need BOTH GDPR consent AND ATT, so an ATT-denied user is reported non-personalized
        ///       (Firebase sets non_personalized_ads=1) even when GDPR consent was granted.</item>
        ///     </list>
        ///     All four signals are re-asserted together every call, which preserves the stored
        ///     ad_personalization choice across an analytics/ad_storage toggle. Collection itself is
        ///     never toggled here — see Initialize.
        /// </summary>
        static void ApplyConsentSignals(bool adStorageConsent, bool adPersonalizationConsent, bool analyticsConsent)
        {
            Firebase.Analytics.ConsentStatus analyticsStatus = analyticsConsent
                ? Firebase.Analytics.ConsentStatus.Granted
                : Firebase.Analytics.ConsentStatus.Denied;
            Firebase.Analytics.ConsentStatus adStorageStatus = adStorageConsent
                ? Firebase.Analytics.ConsentStatus.Granted
                : Firebase.Analytics.ConsentStatus.Denied;
            Firebase.Analytics.ConsentStatus adPersonalizationStatus = adPersonalizationConsent
                ? Firebase.Analytics.ConsentStatus.Granted
                : Firebase.Analytics.ConsentStatus.Denied;

            FirebaseAnalytics.SetConsent(new Dictionary<ConsentType, Firebase.Analytics.ConsentStatus>
            {
                { ConsentType.AnalyticsStorage, analyticsStatus },
                { ConsentType.AdStorage, adStorageStatus },
                { ConsentType.AdPersonalization, adPersonalizationStatus },
                { ConsentType.AdUserData, adPersonalizationStatus },
            });

            PaletteLog.Vital(
                $"{Tag} Consent mode signals: analytics_storage={analyticsStatus}, ad_storage={adStorageStatus}, ad_personalization={adPersonalizationStatus}, ad_user_data={adPersonalizationStatus}");
        }

        void FlushPendingEvents()
        {
            while (_pendingEvents.Count > 0)
            {
                Action action = _pendingEvents.Dequeue();
                action?.Invoke();
            }
        }

        void QueueOrExecute(Action action)
        {
            if (IsReady) { action(); return; }
            if (_initFailed)
            {
                // Firebase permanently unavailable — drop (queueing would leak). Warn once so the
                // loss is visible: such events still show in Sorolla Vitals (recorded before the
                // Firebase hand-off) but never reach Firebase, which reads as "in the game, not in
                // Firebase realtime". Usual root cause: missing GoogleService-Info.plist / deps.
                PaletteLog.WarningOnce("firebase.event_dropped_init_failed",
                    $"{Tag} Firebase init failed — dropping analytics events. They appear in Sorolla Vitals but will NOT reach Firebase. Check GoogleService-Info.plist and Firebase dependencies.");
                return;
            }
            _pendingEvents.Enqueue(action);
        }

        #region Helpers

        /// <summary>
        ///     Build deterministic level_name from progression parts.
        ///     e.g. ("World3", "Level12", null) -> "world3_level12"
        /// </summary>
        static string BuildCanonicalLevelName(string p1, string p2, string p3)
        {
            if (string.IsNullOrEmpty(p1)) return "";
            string name = p1.ToLowerInvariant();
            if (!string.IsNullOrEmpty(p2)) name += "_" + p2.ToLowerInvariant();
            if (!string.IsNullOrEmpty(p3)) name += "_" + p3.ToLowerInvariant();
            return name;
        }

        /// <summary>
        ///     Convert a Dictionary param value to a Firebase Parameter.
        ///     Only supports: string, int, long, float, double, bool, enum.
        /// </summary>
        static Parameter ToFirebaseParameter(string key, object value)
        {
            // Sanitize param name
            string sanitizedKey = EventNameSanitizer.SanitizeParamName(key);
            if (sanitizedKey == null) return null;

            return value switch
            {
                string s => new Parameter(sanitizedKey, s.Length > 100 ? s[..100] : s),
                int i => new Parameter(sanitizedKey, i),
                long l => new Parameter(sanitizedKey, l),
                float f => new Parameter(sanitizedKey, f),
                double d => new Parameter(sanitizedKey, d),
                bool b => new Parameter(sanitizedKey, b ? 1L : 0L),
                Enum e => new Parameter(sanitizedKey, e.ToString()),
                _ => null, // Should not reach here - Palette validates before calling
            };
        }

        /// <summary>
        ///     Build Firebase Parameter array from validated dictionary.
        /// </summary>
        static Parameter[] BuildFirebaseParams(Dictionary<string, object> parameters)
        {
            var result = new List<Parameter>(parameters.Count);
            foreach (var kvp in parameters)
            {
                Parameter param = ToFirebaseParameter(kvp.Key, kvp.Value);
                if (param != null)
                    result.Add(param);
            }
            return result.ToArray();
        }

        /// <summary>
        ///     Merge extra params into an existing parameter list, skipping reserved keys.
        /// </summary>
        static void MergeExtraParams(List<Parameter> target, Dictionary<string, object> extra,
            HashSet<string> reservedKeys)
        {
            foreach (var kvp in extra)
            {
                string sanitizedKey = EventNameSanitizer.SanitizeParamName(kvp.Key);
                if (sanitizedKey == null) continue;

                if (reservedKeys.Contains(sanitizedKey))
                {
                    PaletteLog.Warning($"{Tag} Extra param '{kvp.Key}' collides with base param - skipped");
                    continue;
                }

                Parameter param = ToFirebaseParameter(kvp.Key, kvp.Value);
                if (param != null)
                    target.Add(param);
            }
        }

        static string SanitizeEventName(string name)
        {
            // Shared GA4 normalization (separators -> '_', strip invalid, lowercase, letter-start, max 40).
            string finalName = EventNameSanitizer.SanitizeEventName(name);
            if (finalName == null) return null;

            // Firebase/GA4-specific rejection on top of the shared normalization: these names and
            // prefixes are silently dropped server-side, so drop here too and warn so studios notice.
            if (ReservedEventNames.Contains(finalName))
            {
                PaletteLog.Warning($"{Tag} Event name '{finalName}' is GA4-reserved - dropped");
                return null;
            }
            for (int i = 0; i < ReservedNamePrefixes.Length; i++)
            {
                if (finalName.StartsWith(ReservedNamePrefixes[i]))
                {
                    PaletteLog.Warning($"{Tag} Event name '{finalName}' uses reserved prefix '{ReservedNamePrefixes[i]}' - dropped");
                    return null;
                }
            }

            return finalName;
        }

        static bool HasReservedUserPropertyPrefix(string name)
        {
            if (name.StartsWith("_")) return true;
            for (int i = 0; i < ReservedNamePrefixes.Length; i++)
            {
                if (name.StartsWith(ReservedNamePrefixes[i])) return true;
            }
            return false;
        }

        #endregion
    }
}
