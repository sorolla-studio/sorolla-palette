using System.Runtime.CompilerServices;
using Sorolla.Palette.Adapters;
using UnityEngine;
#if GAMEANALYTICS_INSTALLED
using GameAnalyticsSDK;
#endif

namespace Sorolla.Palette
{
    /// <summary>
    ///     Internal adapter for GameAnalytics SDK.
    ///     Use Palette API instead of calling this directly.
    /// </summary>
    internal static class GameAnalyticsAdapter
    {
        const string Tag = "[Palette:GA]";
        static bool s_init;

        public static bool IsInitialized
        {
            get
            {
#if GAMEANALYTICS_INSTALLED
                return s_init && GameAnalytics.Initialized;
#else
                return false;
#endif
            }
        }

        public static void Initialize(bool consent, bool verboseLogging = false)
        {
            if (s_init) return;

#if GAMEANALYTICS_INSTALLED
            GameAnalytics.OnRemoteConfigsUpdatedEvent += OnGaRemoteConfigsUpdated;

            if (GameAnalytics.Initialized)
            {
                PaletteLog.Vital($"{Tag} Already initialized externally");
                s_init = true;
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.GameAnalytics, AdapterDiagnosticStatus.Ready,
                    "already_initialized", "Already initialized externally");
                GameAnalytics.SetEnabledEventSubmission(consent);
                PaletteLog.Vital($"{Tag} Event submission: {consent}");
                if (GameAnalytics.IsRemoteConfigsReady()) OnGaRemoteConfigsUpdated();
                return;
            }

            GameAnalyticsSDK.Events.GA_Setup.SetInfoLog(verboseLogging);
            GameAnalyticsSDK.Events.GA_Setup.SetVerboseLog(verboseLogging);

            PaletteLog.Vital($"{Tag} Initializing (event submission: {consent}, verbose: {verboseLogging})...");
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.GameAnalytics, AdapterDiagnosticStatus.Initializing,
                "init_requested", "Initializing");
            GameAnalytics.Initialize();
            GameAnalytics.SetEnabledEventSubmission(consent);
            s_init = true;
            if (GameAnalytics.Initialized)
            {
                PaletteLog.Vital($"{Tag} Ready");
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.GameAnalytics, AdapterDiagnosticStatus.Ready,
                    "initialized", "Ready");
            }
            else
            {
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.GameAnalytics, AdapterDiagnosticStatus.Warning,
                    "init_unverified", "Initialize returned before GameAnalytics reported ready");
            }
            if (GameAnalytics.IsRemoteConfigsReady()) OnGaRemoteConfigsUpdated();
#else
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.GameAnalytics, AdapterDiagnosticStatus.Unavailable,
                "not_installed", "GameAnalytics SDK not installed");
            PaletteLog.Warning($"{Tag} SDK not installed");
            s_init = true;
#endif
        }

        public static void UpdateConsent(bool consent)
        {
#if GAMEANALYTICS_INSTALLED
            if (!EnsureInit()) return;
            GameAnalytics.SetEnabledEventSubmission(consent);
            PaletteLog.Vital($"{Tag} Event submission: {consent}");
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool EnsureInit()
        {
#if GAMEANALYTICS_INSTALLED
            if (s_init && GameAnalytics.Initialized) return true;
#else
            if (s_init) return true;
#endif
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.GameAnalytics, AdapterDiagnosticStatus.DispatchDropped,
                "not_initialized", "Event dropped because GameAnalytics is not initialized");
            PaletteLog.Warning($"{Tag} Not initialized");
            return false;
        }

        /// <summary>
        ///     Raw string value for a key known to GameAnalytics Remote Configs.
        ///     False when GameAnalytics is not installed, not initialized, configs are not ready,
        ///     or the key is absent.
        /// </summary>
        public static bool TryGetRemoteConfigValue(string key, out string value)
        {
            value = null;
#if GAMEANALYTICS_INSTALLED
            if (!s_init || !GameAnalytics.IsRemoteConfigsReady()) return false;
            value = GameAnalytics.GetRemoteConfigsValueAsString(key, null);
            return value != null;
#else
            return false;
#endif
        }

#if GAMEANALYTICS_INSTALLED
        static void OnGaRemoteConfigsUpdated() => RemoteConfigState.NotifyGaReady();
#endif

#if GAMEANALYTICS_INSTALLED
        public static void TrackProgressionEvent(GAProgressionStatus status, string p1, string p2 = null, string p3 = null, int score = 0)
        {
            if (!EnsureInit()) return;

            if (string.IsNullOrEmpty(p2) && string.IsNullOrEmpty(p3))
                if (score > 0) GameAnalytics.NewProgressionEvent(status, p1, score);
                else GameAnalytics.NewProgressionEvent(status, p1);
            else if (string.IsNullOrEmpty(p3))
                if (score > 0) GameAnalytics.NewProgressionEvent(status, p1, p2, score);
                else GameAnalytics.NewProgressionEvent(status, p1, p2);
            else if (score > 0) GameAnalytics.NewProgressionEvent(status, p1, p2, p3, score);
            else GameAnalytics.NewProgressionEvent(status, p1, p2, p3);
            RecordDispatchAccepted("progression");
        }

        public static void TrackDesignEvent(string eventName, float value = 0)
        {
            if (!EnsureInit()) return;
            if (value != 0) GameAnalytics.NewDesignEvent(eventName, value);
            else GameAnalytics.NewDesignEvent(eventName);
            RecordDispatchAccepted("design");
        }

        public static void TrackResourceEvent(GAResourceFlowType flowType, string currency, float amount, string itemType, string itemId)
        {
            if (!EnsureInit()) return;
            GameAnalytics.NewResourceEvent(flowType, currency, amount, itemType, itemId);
            RecordDispatchAccepted("resource");
        }

        public static void TrackBusinessEvent(string currency, int amountInCents, string itemType, string itemId, string cartType)
        {
            if (!EnsureInit()) return;
            GameAnalytics.NewBusinessEvent(currency, amountInCents, itemType, itemId, cartType);
            RecordDispatchAccepted("business");
        }

        public static void TrackBusinessEventGooglePlay(string currency, int amountInCents, string itemType, string itemId, string cartType, string receipt, string signature)
        {
            if (!EnsureInit()) return;
#if UNITY_ANDROID
            GameAnalytics.NewBusinessEventGooglePlay(currency, amountInCents, itemType, itemId, cartType, receipt, signature);
#else
            // Fallback to generic business event on non-Android platforms
            GameAnalytics.NewBusinessEvent(currency, amountInCents, itemType, itemId, cartType);
#endif
            RecordDispatchAccepted("business_google_play");
        }

        public static void TrackBusinessEventIOS(string currency, int amountInCents, string itemType, string itemId, string cartType, string receipt)
        {
            if (!EnsureInit()) return;
#if UNITY_IOS
            GameAnalytics.NewBusinessEventIOS(currency, amountInCents, itemType, itemId, cartType, receipt);
#else
            // Fallback to generic business event on non-iOS platforms
            GameAnalytics.NewBusinessEvent(currency, amountInCents, itemType, itemId, cartType);
#endif
            RecordDispatchAccepted("business_ios");
        }

        static void RecordDispatchAccepted(string eventType)
        {
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.GameAnalytics, AdapterDiagnosticStatus.DispatchAccepted,
                "event_sent", $"GameAnalytics {eventType} event sent");
        }

#else
        public static void TrackProgressionEvent(string status, string p1, string p2 = null, string p3 = null, int score = 0) { }
        public static void TrackDesignEvent(string eventName, float value = 0) { }
        public static void TrackResourceEvent(string flowType, string currency, float amount, string itemType, string itemId) { }
        public static void TrackBusinessEvent(string currency, int amountInCents, string itemType, string itemId, string cartType) { }
        public static void TrackBusinessEventGooglePlay(string currency, int amountInCents, string itemType, string itemId, string cartType, string receipt, string signature) { }
        public static void TrackBusinessEventIOS(string currency, int amountInCents, string itemType, string itemId, string cartType, string receipt) { }
#endif
    }
}
