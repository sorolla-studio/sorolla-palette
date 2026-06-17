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

            PaletteLog.Vital($"{Tag} Initializing...");
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Initializing,
                "init_requested", "Initializing");

            if (!FB.IsInitialized)
                FB.Init(OnInit, OnHideUnity);
            else
            {
                ApplyConsent();
                s_init = true;
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Ready,
                    "already_initialized", "Already initialized");
            }
        }

        private static void OnInit()
        {
            if (FB.IsInitialized)
            {
                ApplyConsent();
                s_init = true;
                PaletteLog.Vital($"{Tag} Initialized (tracking: {s_consent})");
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Ready,
                    "initialized", $"Initialized (tracking: {s_consent})");
            }
            else
            {
                PaletteLog.Error($"{Tag} Failed to initialize");
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Failed,
                    "init_failed", "Initialization failed");
            }
        }

        public static void UpdateConsent(bool consent)
        {
            s_consent = consent;
            if (!s_init) return; // will be applied in ApplyConsent() when init completes
            FB.Mobile.SetAdvertiserTrackingEnabled(consent);
            PaletteLog.Vital($"{Tag} SetAdvertiserTrackingEnabled({consent})");
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
        public static void Initialize(bool consent)
        {
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Unavailable,
                "not_installed", "Facebook implementation not installed");
            PaletteLog.Warning("[Palette:FB] Not installed");
        }
        public static void UpdateConsent(bool consent) { }

    }
}
#endif
