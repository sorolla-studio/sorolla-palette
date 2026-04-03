#if SOROLLA_FACEBOOK_ENABLED
using System;
using Facebook.Unity;
using UnityEngine;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     Facebook SDK adapter. Use Sorolla API instead.
    /// </summary>
    internal static class FacebookAdapter
    {
        const string Tag = "[Palette:FB]";
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

    }
}
#else
namespace Sorolla.Palette.Adapters
{
    internal static class FacebookAdapter
    {
        #pragma warning disable CS0067 // Event is never used (stub for API compatibility)
        public static event System.Action<bool> OnGameVisibilityChanged;
        #pragma warning restore CS0067
        public static void Initialize(bool consent) => UnityEngine.Debug.LogWarning("[Palette:FB] Not installed");

    }
}
#endif
