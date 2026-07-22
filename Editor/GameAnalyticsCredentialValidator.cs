using System;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine.Networking;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Validates a GameAnalytics game key/secret key pair by HMAC-signing an <c>init</c> call
    ///     against the GA Collection API (POST /v2/{game_key}/init, Authorization =
    ///     Base64(HMAC-SHA256(body, secret_key))). Async, non-blocking, cached until the key pair
    ///     changes - mirrors <see cref="FacebookPlatformValidator"/>'s pattern exactly.
    ///
    ///     SCOPE: this probe proves the game key + secret key are a live, matched GA credential pair.
    ///     It does NOT and CANNOT prove the active platform is registered in the GA dashboard - the
    ///     GA collector's init/events endpoints accept any platform string with valid credentials
    ///     (confirmed live, greenlight probe spike 2026-07-10). Never widen this probe's claimed scope;
    ///     platform registration is not provable from here, so the passing row says so and points at the
///     dashboard - the SDK never claims a fact it cannot observe.
    /// </summary>
    static class GameAnalyticsCredentialValidator
    {
        internal enum ProbeState
        {
            NotStarted,
            Pending,
            CredentialsValid,
            CredentialInvalid,
            Unreachable,
        }

        internal readonly struct ProbeResult
        {
            internal readonly ProbeState State;
            internal readonly string GameKey;
            internal readonly string Detail;
            internal readonly double TimestampSeconds;

            internal ProbeResult(ProbeState state, string gameKey, string detail, double timestampSeconds)
            {
                State = state;
                GameKey = gameKey;
                Detail = detail ?? "";
                TimestampSeconds = timestampSeconds;
            }
        }

        const int TimeoutSeconds = 3;
        const string CollectorHost = "https://api.gameanalytics.com/v2";

        static ProbeResult s_lastResult = new ProbeResult(ProbeState.NotStarted, null, null, 0);
        static string s_lastRequestKey;
        static bool s_requestInFlight;

        /// <summary>Fired on the main thread once a probe settles, so the window can refresh Build Health.</summary>
        internal static event Action OnProbeSettled;

        internal static ProbeResult Current => s_lastResult;

        static string ActivePlatformName() =>
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS ? "ios" : "android";

        /// <summary>
        ///     Kicks off an init probe for this game key/secret key pair if one has not already run or
        ///     is not already in flight. No-ops otherwise; read <see cref="Current"/> for the (possibly
        ///     still pending) result.
        /// </summary>
        internal static void EnsureChecked(string gameKey, string secretKey)
        {
            string key = $"{gameKey}|{secretKey}";
            if (s_requestInFlight || s_lastRequestKey == key)
                return;

            s_lastRequestKey = key;
            s_requestInFlight = true;
            s_lastResult = new ProbeResult(ProbeState.Pending, gameKey,
                "Checking GameAnalytics credentials...", EditorApplication.timeSinceStartup);

            string platform = ActivePlatformName();
            string body = "{\"platform\":\"" + platform + "\",\"os_version\":\"" + platform + " 1.0\",\"sdk_version\":\"rest api v2\"}";
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            string authHeader = ComputeAuthHeader(bodyBytes, secretKey);

            var request = new UnityWebRequest($"{CollectorHost}/{Uri.EscapeDataString(gameKey)}/init", "POST")
            {
                uploadHandler = new UploadHandlerRaw(bodyBytes),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TimeoutSeconds,
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", authHeader);

            UnityWebRequestAsyncOperation op = request.SendWebRequest();
            op.completed += _ =>
            {
                s_requestInFlight = false;
                s_lastResult = Evaluate(request, gameKey);
                request.Dispose();
                OnProbeSettled?.Invoke();
            };
        }

        static string ComputeAuthHeader(byte[] bodyBytes, string secretKey)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            byte[] hash = hmac.ComputeHash(bodyBytes);
            return Convert.ToBase64String(hash);
        }

        static ProbeResult Evaluate(UnityWebRequest request, string gameKey)
        {
            double now = EditorApplication.timeSinceStartup;

            bool networkOrProtocolError = request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.DataProcessingError;

            if (networkOrProtocolError)
            {
                return new ProbeResult(ProbeState.Unreachable, gameKey,
                    "Could not reach the GameAnalytics collector (offline, or the endpoint is blocked). Re-run the check (Refresh) when online.", now);
            }

            long responseCode = request.responseCode;

            // Truncated for on-screen/copied-report display only (F13.6, 2026-07-21 audit) - the raw
            // gameKey still flows into ProbeResult's cache-matching field below, unchanged. A game key
            // isn't a secret credential by itself (the secret key never renders), but it identifies the
            // specific GA project and doesn't need to render in full next to a "don't paste secrets" note.
            string displayKey = gameKey.Length > 8 ? gameKey.Substring(0, 8) + "…" : gameKey;

            if (responseCode == 401)
            {
                return new ProbeResult(ProbeState.CredentialInvalid, gameKey,
                    $"GameAnalytics game key/secret key pair for {displayKey} was rejected by the collector (HTTP 401).\n" +
                    "  Events will fail to submit; GA will silently show no data for this build.", now);
            }

            if (responseCode != 200)
            {
                return new ProbeResult(ProbeState.Unreachable, gameKey,
                    $"GameAnalytics init request failed unexpectedly (HTTP {responseCode}). Re-run the check (Refresh) when online.", now);
            }

            // Scoped to what this probe actually proved - the platform-registration reminder lives
            // once, in the caller's Fix text (BuildValidationGameAnalyticsCredential.cs), not here too.
            return new ProbeResult(ProbeState.CredentialsValid, gameKey,
                $"GameAnalytics credentials for {displayKey} are a live, matched pair.", now);
        }
    }
}
