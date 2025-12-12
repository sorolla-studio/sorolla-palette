#if SOROLLA_FACEBOOK_ENABLED
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Facebook.Unity;
using UnityEngine;

namespace Sorolla.Adapters
{
    /// <summary>
    ///     Facebook SDK adapter. Use Sorolla API instead.
    /// </summary>
    internal static class FacebookAdapter
    {
        private const string Tag = "[Sorolla:FB]";
        private static bool s_init;
        private static bool s_consent;

        public static event Action<bool> OnGameVisibilityChanged;

        public static void Initialize(bool consent)
        {
            s_consent = consent;
            if (s_init) return;

            Debug.Log($"{Tag} Initializing...");

            if (!FB.IsInitialized)
                FB.Init(OnInit, OnHideUnity);
            else
            {
                ApplyConsent();
                s_init = true;
            }
        }

        private static void OnInit()
        {
            if (FB.IsInitialized)
            {
                ApplyConsent();
                s_init = true;
                Debug.Log($"{Tag} Initialized (tracking: {s_consent})");
            }
            else
                Debug.LogError($"{Tag} Failed to initialize");
        }

        private static void ApplyConsent()
        {
            FB.Mobile.SetAdvertiserTrackingEnabled(s_consent);
            FB.ActivateApp();
        }

        private static void OnHideUnity(bool isGameShown)
        {
            OnGameVisibilityChanged?.Invoke(isGameShown);
            if (isGameShown && FB.IsInitialized)
                FB.ActivateApp();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool EnsureInit()
        {
            if (s_init) return true;
            Debug.LogWarning($"{Tag} Not initialized");
            return false;
        }

        public static void TrackEvent(string eventName, float? value = null, Dictionary<string, object> parameters = null)
        {
            if (!EnsureInit()) return;
#if !UNITY_EDITOR
            if (value.HasValue) FB.LogAppEvent(eventName, value.Value, parameters);
            else FB.LogAppEvent(eventName, parameters: parameters);
#endif
        }

        public static void TrackPurchase(float amount, string currency = "USD", Dictionary<string, object> parameters = null)
        {
            if (!EnsureInit()) return;
#if !UNITY_EDITOR
            FB.LogPurchase((decimal)amount, currency, parameters);
#endif
        }

        public static void TrackLevelAchieved(int level)
        {
            if (!EnsureInit()) return;
            TrackEvent(AppEventName.AchievedLevel, null, new Dictionary<string, object> { { AppEventParameterName.Level, level.ToString() } });
        }

        public static void TrackTutorialCompleted()
        {
            if (!EnsureInit()) return;
            TrackEvent(AppEventName.CompletedTutorial);
        }

        public static void TrackAdImpression(string adNetwork, string placementId, float revenue)
        {
            if (!EnsureInit()) return;
            TrackEvent("ad_impression", revenue, new Dictionary<string, object>
            {
                { "ad_network", adNetwork },
                { "ad_placement_id", placementId },
                { "revenue", revenue }
            });
        }
    }
}
#else
namespace Sorolla.Adapters
{
    internal static class FacebookAdapter
    {
        #pragma warning disable CS0067 // Event is never used (stub for API compatibility)
        public static event System.Action<bool> OnGameVisibilityChanged;
        #pragma warning restore CS0067
        public static void Initialize(bool consent) => UnityEngine.Debug.LogWarning("[Sorolla:FB] Not installed");

        public static void TrackEvent(string e, float? v = null, System.Collections.Generic.Dictionary<string, object> p = null) { }
        public static void TrackPurchase(float a, string c = "USD", System.Collections.Generic.Dictionary<string, object> p = null) { }
        public static void TrackLevelAchieved(int l) { }
        public static void TrackTutorialCompleted() { }
        public static void TrackAdImpression(string n, string p, float r) { }
    }
}
#endif
