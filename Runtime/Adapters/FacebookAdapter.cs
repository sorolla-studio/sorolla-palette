#if SOROLLA_FACEBOOK_ENABLED
using System;
using System.Collections.Generic;
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
        private static bool s_validationRequested;

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
                RequestValidation("already_initialized");
            }
        }

        private static void OnInit()
        {
            if (FB.IsInitialized)
            {
                ApplyConsent();
                s_init = true;
                PaletteLog.Vital($"{Tag} Initialized (tracking: {s_consent})");
                RequestValidation("initialized");
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

        private static void RequestValidation(string source)
        {
            if (s_validationRequested) return;
            s_validationRequested = true;

            string appId = NormalizeAppId(FB.AppId);
            string clientToken = FB.ClientToken;
            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(clientToken))
            {
                PaletteLog.Error($"{Tag} AuthError: App ID or Client Token missing");
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Failed,
                    "auth_missing", "App ID or Client Token missing");
                return;
            }

            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Initializing,
                "validation_requested", $"Initialized ({source}); validating app credentials");

            try
            {
                var formData = new Dictionary<string, string>
                {
                    { "access_token", appId + "|" + clientToken },
                };
                FB.API("/" + appId + "?fields=id", HttpMethod.GET, OnValidationProbe, formData);
            }
            catch (Exception e)
            {
                PaletteLog.Error($"{Tag} AuthError: validation request failed ({e.Message})");
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Failed,
                    "auth_probe_failed", "Validation request failed: " + e.Message);
            }
        }

        private static void OnValidationProbe(IGraphResult result)
        {
            if (result == null)
            {
                PaletteLog.Error($"{Tag} AuthError: validation returned no result");
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Failed,
                    "auth_no_result", "Validation returned no result");
                return;
            }

            if (!string.IsNullOrEmpty(result.Error))
            {
                DiagnoseProbeFailure(SafeDetail(result.Error));
                return;
            }

            if (!ContainsAppId(result))
            {
                string detail = string.IsNullOrEmpty(result.RawResult)
                    ? "Validation response did not include app id"
                    : "Validation response did not include app id: " + SafeDetail(result.RawResult);
                PaletteLog.Warning($"{Tag} Validation warning: {detail}");
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Warning,
                    "auth_unverified", detail);
                return;
            }

            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Ready,
                "validated", "Initialized and app credentials validated");
        }

        /// <summary>
        ///     A failed validation probe surfaces the vendor's raw transport error by default (often a
        ///     misleading SSL/connection message). Before logging it, ask the Graph API whether the
        ///     current platform is even registered on the FB app - that is the actual root cause of the
        ///     Boulder Evolution incident (FB app provisioned Android-only, iOS rejected every call).
        /// </summary>
        private static void DiagnoseProbeFailure(string vendorDetail)
        {
            string appId = NormalizeAppId(FB.AppId);
            string clientToken = FB.ClientToken;

            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(clientToken))
            {
                ReportProbeFailure(vendorDetail);
                return;
            }

            try
            {
                var formData = new Dictionary<string, string>
                {
                    { "access_token", appId + "|" + clientToken },
                };
                FB.API("/" + appId + "?fields=supported_platforms", HttpMethod.GET,
                    platformResult => OnPlatformDiagnosisProbe(platformResult, vendorDetail), formData);
            }
            catch (Exception)
            {
                ReportProbeFailure(vendorDetail);
            }
        }

        private static void OnPlatformDiagnosisProbe(IGraphResult platformResult, string vendorDetail)
        {
            if (platformResult != null && string.IsNullOrEmpty(platformResult.Error)
                && !string.IsNullOrEmpty(platformResult.RawResult))
            {
                var parsed = JsonUtility.FromJson<SupportedPlatformsResponse>(platformResult.RawResult);
                if (parsed?.supported_platforms != null
                    && Array.IndexOf(parsed.supported_platforms, CurrentPlatformName) < 0)
                {
                    string appId = NormalizeAppId(FB.AppId);
                    ReportProbeFailure($"{CurrentPlatformName} not registered on FB app {appId}");
                    return;
                }
            }

            ReportProbeFailure(vendorDetail);
        }

        private static void ReportProbeFailure(string detail)
        {
            PaletteLog.Error($"{Tag} AuthError: {detail}");
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Facebook, AdapterDiagnosticStatus.Failed,
                "auth_error", detail);
        }

        private static string CurrentPlatformName =>
#if UNITY_IOS
            "IOS";
#else
            "ANDROID";
#endif

        [Serializable]
        private class SupportedPlatformsResponse
        {
            public string[] supported_platforms;
        }

        private static bool ContainsAppId(IGraphResult result)
        {
            string appId = NormalizeAppId(FB.AppId);
            if (result.ResultDictionary != null
                && result.ResultDictionary.TryGetValue("id", out object id)
                && string.Equals(id?.ToString(), appId, StringComparison.Ordinal))
                return true;

            return !string.IsNullOrEmpty(result.RawResult)
                && result.RawResult.Contains("\"id\":\"" + appId + "\"");
        }

        private static string NormalizeAppId(string appId)
        {
            if (string.IsNullOrEmpty(appId)) return appId;
            return appId.StartsWith("fb", StringComparison.OrdinalIgnoreCase)
                ? appId.Substring(2)
                : appId;
        }

        private static string SafeDetail(string detail)
        {
            if (string.IsNullOrEmpty(detail)) return "Unknown";
            detail = detail.Replace('\n', ' ').Replace('\r', ' ');
            return detail.Length > 180 ? detail.Substring(0, 179) + "..." : detail;
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
