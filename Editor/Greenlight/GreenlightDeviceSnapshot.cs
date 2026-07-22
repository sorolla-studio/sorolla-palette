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
    ///     Optional "connect device" step: forwards the QA bridge port over USB and pulls
    ///     <c>/qa/snapshot</c>. The transport follows the active build target - Android over
    ///     <c>adb forward</c>, iOS over <c>iproxy</c> (libimobiledevice usbmux) - but both land on the same
    ///     loopback URL, so the fetch/parse path is shared. Never blocks the verdict: no device, no transport
    ///     tool, or an unauthenticated bridge all degrade to WAIT/Info rows, never a Fail. See the studio
    ///     self-serve greenlight plan §Editor window restructure.
    /// </summary>
    static class GreenlightDeviceSnapshot
    {
        const string DeviceForwardSpec = "tcp:18765";
        const string BridgeForwardSpec = "tcp:8765";
        // iproxy takes bare "<localPort> <devicePort>"; the device bridge binds loopback on 8765, so we map
        // local 18765 → device 8765 exactly like the adb forward above, landing on the same SnapshotUrl.
        const string IproxyForwardArgs = "18765 8765";
        const string SnapshotUrl = "http://127.0.0.1:18765/qa/snapshot";
        static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(5);
        // iproxy runs in the foreground until killed; wait for it to bind the local port before the fetch.
        static readonly TimeSpan IproxyBindDelay = TimeSpan.FromMilliseconds(1200);

        internal enum Phase
        {
            NotStarted,
            Running,
            Done,
        }

        internal enum Outcome
        {
            None,
            TransportNotFound, // adb (Android) or iproxy (iOS) not installed on this machine
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
        ///     Kicks off the USB-forward + HTTP GET flow, picking the transport from the active build target
        ///     (adb for Android, iproxy for iOS). Fire-and-forget from a button click; <paramref name="onSettled"/>
        ///     is invoked once on completion so the caller can repaint.
        /// </summary>
        internal static async void Run(State state, Action onSettled)
        {
            state.Phase = Phase.Running;
            state.Outcome = Outcome.None;
            state.DetailMessage = "Connecting...";
            onSettled?.Invoke();

            // The bridge is passwordless (loopback-only is the boundary), so the editor sends no auth header.
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS)
                await RunIosFlow(state);
            else
                await RunAndroidFlow(state);

            onSettled?.Invoke();
        }

        static async Task RunAndroidFlow(State state)
        {
            string adbPath = ResolveAdbPath();
            if (adbPath == null)
            {
                Settle(state, Outcome.TransportNotFound, "adb not found on PATH or in standard Android SDK locations.");
                return;
            }

            if (await RunAdbForward(adbPath, state)) // sets NoDevice on failure
                await FetchSnapshot(state);
        }

        static async Task RunIosFlow(State state)
        {
            string iproxyPath = ResolveMacToolPath("iproxy");
            if (iproxyPath == null)
            {
                Settle(state, Outcome.TransportNotFound,
                    "iproxy not found. Install libimobiledevice (brew install libimobiledevice) to connect an iOS device over USB.");
                return;
            }

            if (!IosDeviceConnected())
            {
                Settle(state, Outcome.NoDevice,
                    "No iOS device detected over USB. Connect the iPhone/iPad, unlock it, and tap Trust This Computer, then re-run.");
                return;
            }

            Process forward = StartIproxyForward(iproxyPath, state); // sets NoDevice on failure
            if (forward == null)
                return;

            try
            {
                await Task.Delay(IproxyBindDelay);
                await FetchSnapshot(state);
            }
            finally
            {
                KillQuietly(forward);
            }
        }

        static void Settle(State state, Outcome outcome, string detail)
        {
            state.Phase = Phase.Done;
            state.Outcome = outcome;
            state.DetailMessage = detail;
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

        // iOS transport tools (iproxy/idevice_id) ship via homebrew's libimobiledevice, and iOS development
        // only happens on macOS. A GUI-launched Unity does not inherit a login shell's PATH, so a bare "iproxy"
        // is invisible even when it works from a terminal - resolve the two brew prefixes explicitly (Apple
        // Silicon then Intel), same lesson as the adb resolver above.
        static string ResolveMacToolPath(string tool)
        {
            foreach (string dir in new[] { "/opt/homebrew/bin", "/usr/local/bin" })
            {
                string candidate = Path.Combine(dir, tool);
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }

        // A quick usbmux presence check so "no device" reads as NoDevice with a clear fix, not a downstream
        // "bridge unreachable". A missing probe tool is not treated as "no device" - fall through to the
        // forward+fetch, which will surface the real reason.
        static bool IosDeviceConnected()
        {
            string ideviceId = ResolveMacToolPath("idevice_id");
            if (ideviceId == null)
                return true;
            try
            {
                var probe = new Process
                {
                    StartInfo = new ProcessStartInfo(ideviceId, "-l")
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    },
                };
                probe.Start();
                string output = probe.StandardOutput.ReadToEnd();
                probe.WaitForExit(3000);
                return !string.IsNullOrWhiteSpace(output);
            }
            catch
            {
                return true;
            }
        }

        // Starts the long-running iproxy usbmux forward. Unlike `adb forward` (which returns once the forward is
        // registered in the adb server), iproxy stays in the foreground until killed, so we return the handle and
        // the caller tears it down after the fetch rather than waiting on exit.
        static Process StartIproxyForward(string iproxyPath, State state)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo(iproxyPath, IproxyForwardArgs)
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    },
                };
                process.Start();
                return process;
            }
            catch (Exception e)
            {
                Settle(state, Outcome.NoDevice, $"iproxy USB forward failed: {e.Message}");
                return null;
            }
        }

        static void KillQuietly(Process process)
        {
            try
            {
                if (!process.HasExited) process.Kill();
            }
            catch
            {
                // Already gone (e.g. the local port was busy so iproxy exited on its own) - nothing to tear down.
            }
            process.Dispose();
        }

        static async Task FetchSnapshot(State state)
        {
            try
            {
                using var client = new HttpClient { Timeout = HttpTimeout };
                HttpResponseMessage response = await client.GetAsync(SnapshotUrl);
                string body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode || string.IsNullOrEmpty(body))
                {
                    state.Phase = Phase.Done;
                    state.Outcome = Outcome.Unreachable;
                    state.DetailMessage = string.IsNullOrEmpty(body)
                        ? "Empty reply - the app may not be booted/foregrounded yet."
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
        ///     Maps the connection state onto neutral device observations. Only a PARSED snapshot produces
        ///     evidence: with no connection (or an unreachable/mismatched one) this emits nothing, so the
        ///     required device gates omit → INCOMPLETE. A build we have never confirmed on device therefore
        ///     cannot pass, and no observation ever claims a proof it does not hold.
        /// </summary>
        internal static List<GateObservation> ToObservations(State state)
        {
            var observations = new List<GateObservation>();
            if (state.Phase != Phase.NotStarted && state.Outcome == Outcome.Parsed)
                AddParsedObservations(observations, state.Snapshot);
            return observations;
        }

        internal enum IdentityResult { Match, Mismatch, Missing }

        static void AddParsedObservations(List<GateObservation> observations, Dictionary<string, object> snapshot)
        {
            // C4-08: an unknown/absent snapshot schema is not parseable with confidence - degrade, never guess.
            // A snapshot we cannot trust (bad schema, wrong game/build) yields NO evidence at all, so the
            // required device gates omit → INCOMPLETE rather than being answered by an untrusted device.
            string schema = GetString(snapshot, "snapshot_schema");
            if (schema != QaSnapshot.SchemaVersion)
            {
                observations.Add(Untrusted(
                    $"Snapshot schema '{schema ?? "(absent)"}' is unsupported (expected {QaSnapshot.SchemaVersion}) - the device build predates identity binding.",
                    "Rebuild and reinstall the game from the current SDK so the snapshot carries build identity."));
                return;
            }

            // C4-03: a snapshot from the wrong game or wrong build must NOT satisfy any device gate. It is
            // still reported - as a reason the evidence is MISSING, never as evidence itself.
            if (CompareIdentity(snapshot, Application.identifier, ExpectedMode(), Application.version,
                    ExpectedPlatform(), out string identityDetail) != IdentityResult.Match)
            {
                observations.Add(Untrusted(identityDetail,
                    "Connect the device running THIS game/build, or rebuild and reinstall from the current source."));
                return;
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
                    FixHint = errorCount > 0 ? "Open Vitals on the device and expand the failing rows under FIX THESE for WHY/SIGNAL/FIX." : null,
                });
            }
        }

        /// <summary>An untrusted snapshot (wrong schema, wrong game/build) reported against the required device
        /// gate as INCOMPLETE with NO observed proof: the studio still sees WHY the device evidence is missing,
        /// and an unusable device can never answer a gate.</summary>
        static GateObservation Untrusted(string evidence, string fix) => new GateObservation
        {
            GateId = GateIds.DeviceNoSdkErrors,
            Outcome = GateOutcome.Incomplete,
            ObservedProof = ProofScope.None,
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
        /// parsed snapshot / no GUID. It identifies the exact binary a copied report describes.</summary>
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
