using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Sorolla.Palette.Health;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor.Greenlight
{
    /// <summary>
    ///     Optional "connect device" step: forwards the QA bridge port over adb and pulls
    ///     <c>/qa/snapshot</c>. Never blocks the verdict - no device, no adb, or an unauthenticated
    ///     bridge all degrade to WAIT/Info rows, never a Fail. See the studio self-serve greenlight
    ///     plan §Editor window restructure.
    /// </summary>
    static class GreenlightDeviceSnapshot
    {
        const string DeviceForwardSpec = "tcp:18765";
        const string BridgeForwardSpec = "tcp:8765";
        const string SnapshotUrl = "http://127.0.0.1:18765/qa/snapshot";
        const string PasswordHeader = "X-Sorolla-QA-Password";
        static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(5);

        internal enum Phase
        {
            NotStarted,
            Running,
            Done,
        }

        internal enum Outcome
        {
            None,
            AdbNotFound,
            NoDevice,
            Unreachable,
            Parsed,
        }

        internal sealed class State
        {
            public Phase Phase = Phase.NotStarted;
            public Outcome Outcome = Outcome.None;
            public string DetailMessage;
            public Dictionary<string, object> Snapshot;
        }

        /// <summary>
        ///     Kicks off the adb-forward + HTTP GET flow. Fire-and-forget from a button click;
        ///     <paramref name="onSettled"/> is invoked on completion so the caller can repaint.
        /// </summary>
        internal static async void Run(State state, Action onSettled)
        {
            state.Phase = Phase.Running;
            state.Outcome = Outcome.None;
            state.DetailMessage = "Connecting...";
            onSettled?.Invoke();

            // Same resolution the bridge itself uses (config override else built-in default) -
            // QaBridgeAuth.EffectivePassword() is internal + InternalsVisibleTo("Sorolla.Editor"),
            // see Runtime/Diagnostics/QaBridge/QaBridgeAuth.cs. Always resolves to a non-empty value.
            string password = QaBridgeAuth.EffectivePassword();

            string adbPath = ResolveAdbPath();
            if (adbPath == null)
            {
                state.Phase = Phase.Done;
                state.Outcome = Outcome.AdbNotFound;
                state.DetailMessage = "adb not found on PATH or in standard Android SDK locations.";
                onSettled?.Invoke();
                return;
            }

            bool forwarded = await RunAdbForward(adbPath, state);
            if (!forwarded)
            {
                onSettled?.Invoke();
                return;
            }

            await FetchSnapshot(password, state);
            onSettled?.Invoke();
        }

        static string ResolveAdbPath()
        {
            // Unity's own embedded AndroidPlayer SDK is the single most reliable candidate - every
            // machine with Android build support installed has it, independent of any separately
            // installed Android Studio SDK or shell PATH (a GUI-launched Unity process does not
            // inherit a login shell's PATH on macOS, so homebrew/system adb installs are otherwise
            // invisible here even though they work fine from a terminal).
            string embeddedCandidate = EmbeddedAndroidPlayerAdbPath();
            if (embeddedCandidate != null && File.Exists(embeddedCandidate))
                return embeddedCandidate;

            string sdkRoot = EditorPrefs.GetString("AndroidSdkRoot", "");
            if (!string.IsNullOrEmpty(sdkRoot))
            {
                string candidate = Path.Combine(sdkRoot, "platform-tools", AdbFileName());
                if (File.Exists(candidate)) return candidate;
            }

            foreach (string candidate in StandardAdbLocations())
                if (File.Exists(candidate))
                    return candidate;

            // Fall back to PATH - Process will resolve it if adb is on PATH; a quick "adb version"
            // probe confirms it actually launches rather than guessing.
            try
            {
                var probe = new Process
                {
                    StartInfo = new ProcessStartInfo("adb", "version")
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    },
                };
                probe.Start();
                probe.WaitForExit(2000);
                return probe.ExitCode == 0 ? "adb" : null;
            }
            catch
            {
                return null;
            }
        }

        static string EmbeddedAndroidPlayerAdbPath()
        {
            try
            {
                string playbackEngineDir = BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None);
                return string.IsNullOrEmpty(playbackEngineDir)
                    ? null
                    : Path.Combine(playbackEngineDir, "SDK", "platform-tools", AdbFileName());
            }
            catch
            {
                // Android module not installed / API unavailable on this Unity version - fall through
                // to the other candidates rather than failing the whole resolution.
                return null;
            }
        }

        static string AdbFileName() =>
            Application.platform == RuntimePlatform.WindowsEditor ? "adb.exe" : "adb";

        static IEnumerable<string> StandardAdbLocations()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            yield return Path.Combine(home, "Library", "Android", "sdk", "platform-tools", "adb");
            yield return Path.Combine(home, "Android", "Sdk", "platform-tools", "adb");
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            yield return Path.Combine(localAppData, "Android", "Sdk", "platform-tools", "adb.exe");

            // A GUI-launched Unity process does not inherit a login shell's PATH on macOS, so the
            // PATH-fallback probe below silently misses homebrew/system installs - list them explicitly.
            yield return "/opt/homebrew/bin/adb";
            yield return "/usr/local/bin/adb";
        }

        static async Task<bool> RunAdbForward(string adbPath, State state)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo(adbPath, $"forward {DeviceForwardSpec} {BridgeForwardSpec}")
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    },
                };
                process.Start();
                string stderr = await process.StandardError.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit(5000));

                if (process.ExitCode != 0)
                {
                    state.Phase = Phase.Done;
                    state.Outcome = Outcome.NoDevice;
                    state.DetailMessage = string.IsNullOrEmpty(stderr) ? "No device connected (adb forward failed)." : stderr.Trim();
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                state.Phase = Phase.Done;
                state.Outcome = Outcome.NoDevice;
                state.DetailMessage = $"adb forward failed: {e.Message}";
                return false;
            }
        }

        static async Task FetchSnapshot(string password, State state)
        {
            try
            {
                using var client = new HttpClient { Timeout = HttpTimeout };
                client.DefaultRequestHeaders.Add(PasswordHeader, password);
                HttpResponseMessage response = await client.GetAsync(SnapshotUrl);
                string body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode || string.IsNullOrEmpty(body))
                {
                    state.Phase = Phase.Done;
                    state.Outcome = Outcome.Unreachable;
                    state.DetailMessage = string.IsNullOrEmpty(body)
                        ? "Empty reply - app may not be booted/foregrounded yet, not necessarily an auth failure."
                        : $"Snapshot request failed (HTTP {(int)response.StatusCode}).";
                    return;
                }

                if (!(MiniJson.Deserialize(body) is Dictionary<string, object> json))
                {
                    state.Phase = Phase.Done;
                    state.Outcome = Outcome.Unreachable;
                    state.DetailMessage = "Snapshot response could not be parsed.";
                    return;
                }

                state.Phase = Phase.Done;
                state.Outcome = Outcome.Parsed;
                state.Snapshot = json;
                state.DetailMessage = null;
            }
            catch (Exception e)
            {
                state.Phase = Phase.Done;
                state.Outcome = Outcome.Unreachable;
                state.DetailMessage = $"Could not reach the QA bridge: {e.Message}";
            }
        }

        /// <summary>
        ///     Maps the connection state onto neutral device observations. The device gates require
        ///     <see cref="ProofScope.DeviceDispatch"/>, so a not-connected/unreachable snapshot yields an
        ///     observation with no observed proof - the required-proof gate resolves it to INCOMPLETE (a
        ///     build we have never confirmed on device cannot pass), never a green skip. A parsed snapshot
        ///     carries DeviceDispatch proof and the real outcomes.
        /// </summary>
        internal static List<GateObservation> ToObservations(State state)
        {
            var observations = new List<GateObservation>();

            switch (state.Phase == Phase.NotStarted ? Outcome.None : state.Outcome)
            {
                case Outcome.None:
                    observations.Add(new GateObservation
                    {
                        GateId = GateIds.DeviceReady,
                        Outcome = GateOutcome.Incomplete,
                        ObservedProof = ProofScope.None,
                        Evidence = "Not connected - click Connect Device to pull live /qa/snapshot state.",
                        FixHint = "Connect an Android device with USB debugging enabled and re-run.",
                    });
                    return observations;

                case Outcome.AdbNotFound:
                case Outcome.NoDevice:
                    observations.Add(new GateObservation
                    {
                        GateId = GateIds.DeviceReady,
                        Outcome = GateOutcome.Incomplete,
                        ObservedProof = ProofScope.None,
                        Evidence = state.DetailMessage,
                        FixHint = "Connect an Android device with USB debugging enabled and re-run.",
                    });
                    return observations;

                case Outcome.Unreachable:
                    observations.Add(new GateObservation
                    {
                        GateId = GateIds.DeviceReady,
                        Outcome = GateOutcome.Incomplete,
                        ObservedProof = ProofScope.None,
                        Evidence = state.DetailMessage,
                        FixHint = "Confirm the app is installed, foregrounded, and the QA bridge is armed, then Connect Device again.",
                    });
                    return observations;

                case Outcome.Parsed:
                    AddParsedObservations(observations, state.Snapshot);
                    return observations;

                default:
                    return observations;
            }
        }

        internal enum IdentityResult { Match, Mismatch, Missing }

        static void AddParsedObservations(List<GateObservation> observations, Dictionary<string, object> snapshot)
        {
            // C4-08: an unknown/absent snapshot schema is not parseable with confidence - degrade, never guess.
            string schema = GetString(snapshot, "snapshot_schema");
            if (schema != QaSnapshot.SchemaVersion)
            {
                observations.Add(DeviceReady(GateOutcome.Incomplete,
                    $"Snapshot schema '{schema ?? "(absent)"}' is unsupported (expected {QaSnapshot.SchemaVersion}) - the device build predates identity binding.",
                    "Rebuild and reinstall the game from the current SDK so the snapshot carries build identity."));
                return;
            }

            // C4-03: a snapshot from the wrong game or wrong build must NOT satisfy device readiness.
            IdentityResult identityResult = CompareIdentity(
                snapshot, Application.identifier, ExpectedMode(), Application.version, ExpectedPlatform(), out string identityDetail);
            if (identityResult == IdentityResult.Mismatch)
            {
                observations.Add(DeviceReady(GateOutcome.Fail, identityDetail,
                    "Connect the device running THIS game/build, or rebuild and reinstall from the current source."));
                return;
            }
            if (identityResult == IdentityResult.Missing)
            {
                observations.Add(DeviceReady(GateOutcome.Incomplete, identityDetail,
                    "Rebuild and reinstall the game from the current SDK so the snapshot carries build identity."));
                return;
            }

            bool ready = GetBool(snapshot, "ready");
            observations.Add(DeviceReady(
                ready ? GateOutcome.Pass : GateOutcome.Fail,
                ready ? $"Ready - identity matches ({GetString(snapshot, "mode")}, {Application.identifier})" : "SDK reports not ready",
                ready ? null : "Open the in-app debug menu (Vitals) on this device for the full WHY/SIGNAL/FIX breakdown."));

            if (TryGetObject(snapshot, "identity", out var identity))
            {
                bool adPresent = GetBool(identity, "advertising_id_present");
                observations.Add(new GateObservation
                {
                    GateId = GateIds.DeviceAdvertisingId,
                    Outcome = adPresent ? GateOutcome.Pass : GateOutcome.PassWithCaveats,
                    ObservedProof = ProofScope.DeviceDispatch,
                    Evidence = adPresent ? "Present" : "Not present (expected if ATT/consent denied, or zeroed by OS privacy settings)",
                });
            }

            // C4-08: device-error evidence must be present for the supported schema. A reduced snapshot with
            // no "problems" section, or a malformed sdk_errors value, has not demonstrated that SDK errors
            // were checked → INCOMPLETE, not a permissive false/zero pass.
            if (!TryGetObject(snapshot, "problems", out var problems))
            {
                observations.Add(new GateObservation
                {
                    GateId = GateIds.DeviceNoSdkErrors,
                    Outcome = GateOutcome.Incomplete,
                    ObservedProof = ProofScope.None,
                    Evidence = "Snapshot has no 'problems' section - SDK-error evidence was not demonstrated.",
                });
            }
            else if (!TryGetLong(problems, "sdk_errors", out long errorCount))
            {
                observations.Add(new GateObservation
                {
                    GateId = GateIds.DeviceNoSdkErrors,
                    Outcome = GateOutcome.Incomplete,
                    ObservedProof = ProofScope.None,
                    Evidence = "Snapshot 'sdk_errors' is missing or malformed - SDK-error evidence could not be read.",
                });
            }
            else
            {
                observations.Add(new GateObservation
                {
                    GateId = GateIds.DeviceNoSdkErrors,
                    Outcome = errorCount > 0 ? GateOutcome.Fail : GateOutcome.Pass,
                    ObservedProof = ProofScope.DeviceDispatch,
                    Evidence = errorCount > 0 ? $"{errorCount} error(s) - last: {GetString(problems, "last_sdk_error")}" : "None observed this session",
                    FixHint = errorCount > 0 ? "Open the in-app debug menu (Vitals) Issues tab for WHY/SIGNAL/FIX on each error." : null,
                });
            }
        }

        static GateObservation DeviceReady(GateOutcome outcome, string evidence, string fix) => new GateObservation
        {
            GateId = GateIds.DeviceReady,
            Outcome = outcome,
            ObservedProof = outcome == GateOutcome.Pass || outcome == GateOutcome.Fail ? ProofScope.DeviceDispatch : ProofScope.None,
            Evidence = evidence,
            FixHint = fix,
        };

        /// <summary>
        ///     Compares the snapshot's build identity against the project's expected identity (review C4-03).
        ///     Missing identity fields (an old snapshot) → Missing (INCOMPLETE); any disagreement → Mismatch
        ///     (FAIL). Pure over its inputs so it is unit-testable without ambient editor state.
        /// </summary>
        internal static IdentityResult CompareIdentity(
            Dictionary<string, object> snapshot,
            string expectedAppId, string expectedMode, string expectedAppVersion, string expectedPlatform,
            out string detail)
        {
            detail = null;
            if (!TryGetObject(snapshot, "build", out var build))
            {
                detail = "Snapshot has no build-identity block (device build predates identity binding).";
                return IdentityResult.Missing;
            }

            string appId = GetString(build, "application_id");
            string platform = GetString(build, "platform");
            string appVersion = GetString(build, "app_version");
            string buildGuid = GetString(build, "build_guid");
            string mode = GetString(snapshot, "mode");

            // Platform and build GUID are part of the required identity: a snapshot missing either has not
            // proven which build it came from (review C45-05) - do not accept it silently.
            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(appVersion) ||
                string.IsNullOrEmpty(mode) || string.IsNullOrEmpty(platform) || string.IsNullOrEmpty(buildGuid))
            {
                detail = "Snapshot build identity is incomplete (missing application id / version / mode / platform / build GUID).";
                return IdentityResult.Missing;
            }

            if (!string.IsNullOrEmpty(expectedAppId) && appId != expectedAppId)
            {
                detail = $"Wrong game: snapshot application id '{appId}' != project '{expectedAppId}'.";
                return IdentityResult.Mismatch;
            }
            if (!string.IsNullOrEmpty(expectedMode) && mode != expectedMode)
            {
                detail = $"Wrong mode: snapshot mode '{mode}' != project '{expectedMode}'.";
                return IdentityResult.Mismatch;
            }
            if (!string.IsNullOrEmpty(expectedAppVersion) && appVersion != expectedAppVersion)
            {
                detail = $"Wrong build: snapshot app version '{appVersion}' != project '{expectedAppVersion}'.";
                return IdentityResult.Mismatch;
            }
            if (!string.IsNullOrEmpty(expectedPlatform) && platform != expectedPlatform)
            {
                detail = $"Wrong platform: snapshot platform '{platform}' != expected '{expectedPlatform}'.";
                return IdentityResult.Mismatch;
            }

            detail = "identity matches";
            return IdentityResult.Match;
        }

        /// <summary>The connected snapshot's Unity build GUID (from its build-identity block), or null when no
        /// parsed snapshot / no GUID. Used to bind device-session attestations to the exact build (C45-05).</summary>
        internal static string BuildGuidOf(State state)
        {
            if (state == null || state.Phase == Phase.NotStarted || state.Outcome != Outcome.Parsed)
                return null;
            return TryGetObject(state.Snapshot, "build", out var build) ? GetString(build, "build_guid") : null;
        }

        static string ExpectedMode() => SorollaSettings.Mode switch
        {
            SorollaMode.Full => "full",
            SorollaMode.Prototype => "prototype",
            _ => "unknown",
        };

        // Map the active build target to the RuntimePlatform name the device reports (Application.platform).
        static string ExpectedPlatform() => EditorUserBuildSettings.activeBuildTarget switch
        {
            BuildTarget.Android => "Android",
            BuildTarget.iOS => "IPhonePlayer",
            _ => null, // don't constrain platform for unsupported targets
        };

        static bool GetBool(Dictionary<string, object> dict, string key) =>
            dict != null && dict.TryGetValue(key, out object v) && v is bool b && b;

        // Strict integer read: a missing key or a non-numeric value returns false rather than a permissive
        // zero (review F4-03) so a malformed sdk_errors cannot silently read as "no errors".
        static bool TryGetLong(Dictionary<string, object> dict, string key, out long value)
        {
            value = 0;
            if (dict == null || !dict.TryGetValue(key, out object v) || v == null)
                return false;
            switch (v)
            {
                case long l: value = l; return true;
                case int i: value = i; return true;
                case double d: value = (long)d; return true;
                case string s when long.TryParse(s, out long parsed): value = parsed; return true;
                default: return false;
            }
        }

        static string GetString(Dictionary<string, object> dict, string key) =>
            dict != null && dict.TryGetValue(key, out object v) ? v as string : null;

        static bool TryGetObject(Dictionary<string, object> dict, string key, out Dictionary<string, object> value)
        {
            value = null;
            if (dict != null && dict.TryGetValue(key, out object v) && v is Dictionary<string, object> nested)
            {
                value = nested;
                return true;
            }
            return false;
        }
    }
}
