using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.Networking;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Validates a Facebook app's platform registration + credential pair via the Graph API
    ///     (GET /{app-id}?fields=supported_platforms). Async, non-blocking, result cached until the
    ///     app id/client token/active-platform combination changes. Never called from a build-blocking
    ///     synchronous path - <see cref="BuildValidator.RunAllChecks"/> only reads the cached
    ///     <see cref="Current"/> result and kicks off a fresh probe if needed.
    /// </summary>
    static class FacebookPlatformValidator
    {
        internal enum ProbeState
        {
            NotStarted,
            Pending,
            Verified,
            PlatformMissing,
            CredentialInvalid,
            Unreachable,
        }

        internal readonly struct ProbeResult
        {
            internal readonly ProbeState State;
            internal readonly string AppId;
            internal readonly string PlatformName;
            internal readonly string Detail;
            internal readonly double TimestampSeconds;

            internal ProbeResult(ProbeState state, string appId, string platformName, string detail, double timestampSeconds)
            {
                State = state;
                AppId = appId;
                PlatformName = platformName;
                Detail = detail ?? "";
                TimestampSeconds = timestampSeconds;
            }
        }

        const int TimeoutSeconds = 3;

        static ProbeResult s_lastResult = new ProbeResult(ProbeState.NotStarted, null, null, null, 0);
        static string s_lastRequestKey;
        static bool s_requestInFlight;

        /// <summary>Fired on the main thread once a probe settles, so the window can refresh Build Health.</summary>
        internal static event Action OnProbeSettled;

        internal static ProbeResult Current => s_lastResult;

        // Graph vocabulary trap: FB Graph API supported_platforms uses IPHONE / IPAD / ANDROID.
        // There is no "IOS" value - a correctly-provisioned iOS app registers as IPHONE and/or
        // IPAD. This shipped once as a false "platform missing" report; do not compare against
        // "IOS" again. Human-facing messages still say "iOS" (ActivePlatformName below).
        static readonly string[] s_iosGraphPlatforms = { "IPHONE", "IPAD" };

        internal static string ActivePlatformName() =>
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS ? "iOS" : "ANDROID";

        static bool IsRegistered(List<string> supportedPlatforms, string platformName) =>
            platformName == "iOS"
                ? s_iosGraphPlatforms.Any(supportedPlatforms.Contains)
                : supportedPlatforms.Contains(platformName);

        /// <summary>
        ///     Kicks off a Graph API probe for this app id/client token/active-platform combination if
        ///     one has not already run or is not already in flight. No-ops otherwise; read
        ///     <see cref="Current"/> for the (possibly still pending) result.
        /// </summary>
        internal static void EnsureChecked(string appId, string clientToken)
        {
            string platformName = ActivePlatformName();
            string key = $"{appId}|{clientToken}|{platformName}";
            if (s_requestInFlight || s_lastRequestKey == key)
                return;

            s_lastRequestKey = key;
            s_requestInFlight = true;
            s_lastResult = new ProbeResult(ProbeState.Pending, appId, platformName,
                "Checking Facebook app platform registration...", EditorApplication.timeSinceStartup);

            string accessToken = Uri.EscapeDataString(appId + "|" + clientToken);
            string url = $"https://graph.facebook.com/{Uri.EscapeDataString(appId)}?fields=supported_platforms&access_token={accessToken}";

            var request = UnityWebRequest.Get(url);
            request.timeout = TimeoutSeconds;
            UnityWebRequestAsyncOperation op = request.SendWebRequest();
            op.completed += _ =>
            {
                s_requestInFlight = false;
                s_lastResult = Evaluate(request, appId, platformName);
                request.Dispose();
                OnProbeSettled?.Invoke();
            };
        }

        static ProbeResult Evaluate(UnityWebRequest request, string appId, string platformName)
        {
            double now = EditorApplication.timeSinceStartup;

            bool networkOrProtocolError = request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.DataProcessingError;

            if (networkOrProtocolError)
            {
                return new ProbeResult(ProbeState.Unreachable, appId, platformName,
                    "Could not reach the Facebook Graph API (offline, or the endpoint is blocked). Re-run the check (Refresh) when online.", now);
            }

            long responseCode = request.responseCode;
            string body = request.downloadHandler?.text;

            if (responseCode != 200)
            {
                if (LooksLikeCredentialError(body))
                {
                    return new ProbeResult(ProbeState.CredentialInvalid, appId, platformName,
                        CredentialErrorDetail(appId, body), now);
                }

                return new ProbeResult(ProbeState.Unreachable, appId, platformName,
                    $"Facebook Graph API request failed (HTTP {responseCode}). Re-run the check (Refresh) when online.", now);
            }

            if (!TryGetSupportedPlatforms(body, out List<string> supportedPlatforms))
            {
                return new ProbeResult(ProbeState.Unreachable, appId, platformName,
                    "Facebook Graph API response could not be parsed. Re-run the check (Refresh) when online.", now);
            }

            if (!IsRegistered(supportedPlatforms, platformName))
            {
                return new ProbeResult(ProbeState.PlatformMissing, appId, platformName,
                    $"FB app {appId} has no {platformName} platform registered in the FB console.\n" +
                    $"  Every native Graph/Login/attribution call from {platformName} will be rejected.", now);
            }

            return new ProbeResult(ProbeState.Verified, appId, platformName,
                $"Facebook app {appId} has {platformName} platform registered.", now);
        }

        static bool LooksLikeCredentialError(string body)
        {
            if (string.IsNullOrEmpty(body)) return false;
            return body.Contains("OAuthException") || body.Contains("Invalid OAuth access token")
                || body.Contains("invalid_token") || body.Contains("Invalid access token");
        }

        // The Graph API collapses three distinct credential failures under the same errorCode 190
        // (OAuthException) umbrella. The message text is the only thing that tells them apart -
        // confirmed against 6 real app ids in the greenlight probe spike (2026-07-10). Split them
        // here so the studio-facing fix text names the actual cause instead of a generic "rejected".
        static string CredentialErrorDetail(string appId, string body)
        {
            string graphMessage = TryGetErrorMessage(body, out string msg) ? msg : null;

            string cause = graphMessage != null && graphMessage.Contains("Application has been deleted")
                ? $"Facebook app {appId} has been deleted from the developer console."
                : graphMessage != null && graphMessage.Contains("Invalid OAuth access token signature")
                    ? $"Facebook app {appId} exists, but the client token in FacebookSettings.asset does not match it."
                    : graphMessage != null && graphMessage.Contains("Invalid application ID")
                        ? $"App id {appId} in FacebookSettings.asset does not match any Facebook app."
                        : $"Facebook appId/clientToken pair for app {appId} was rejected by the Graph API.";

            return cause + "\n  Facebook init will report AuthError; analytics and attribution silently stop reaching Facebook.";
        }

        static bool TryGetErrorMessage(string body, out string message)
        {
            message = null;
            if (string.IsNullOrEmpty(body)) return false;
            if (!(MiniJson.Deserialize(body) is Dictionary<string, object> json)) return false;
            if (!json.TryGetValue("error", out object rawError) || !(rawError is Dictionary<string, object> error)) return false;
            if (!error.TryGetValue("message", out object rawMessage) || !(rawMessage is string s)) return false;
            message = s;
            return true;
        }

        static bool TryGetSupportedPlatforms(string body, out List<string> platforms)
        {
            platforms = new List<string>();
            if (string.IsNullOrEmpty(body)) return false;

            if (!(MiniJson.Deserialize(body) is Dictionary<string, object> json)) return false;
            if (!json.TryGetValue("supported_platforms", out object raw) || !(raw is List<object> list)) return false;

            foreach (object entry in list)
            {
                if (entry is string s)
                    platforms.Add(s);
            }

            return true;
        }
    }
}
