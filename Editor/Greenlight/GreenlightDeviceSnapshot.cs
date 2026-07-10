using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Sorolla.Palette.Editor.UI;
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
            NoPasswordConfigured,
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

            string password = ResolvePassword();
            if (string.IsNullOrEmpty(password))
            {
                state.Phase = Phase.Done;
                state.Outcome = Outcome.NoPasswordConfigured;
                state.DetailMessage = "No SorollaConfig.qaBridgePassword set on this project - the editor cannot reach the SDK's built-in default from outside the Runtime assembly.";
                onSettled?.Invoke();
                return;
            }

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

        static string ResolvePassword()
        {
            SorollaConfig config = Palette.Config;
            return config != null && !string.IsNullOrEmpty(config.qaBridgePassword) ? config.qaBridgePassword : null;
        }

        static string ResolveAdbPath()
        {
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

        static string AdbFileName() =>
            Application.platform == RuntimePlatform.WindowsEditor ? "adb.exe" : "adb";

        static IEnumerable<string> StandardAdbLocations()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            yield return Path.Combine(home, "Library", "Android", "sdk", "platform-tools", "adb");
            yield return Path.Combine(home, "Android", "Sdk", "platform-tools", "adb");
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            yield return Path.Combine(localAppData, "Android", "Sdk", "platform-tools", "adb.exe");
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

        internal static List<GreenlightEvaluator.Row> ToRows(State state)
        {
            var rows = new List<GreenlightEvaluator.Row>();

            switch (state.Phase == Phase.NotStarted ? Outcome.None : state.Outcome)
            {
                case Outcome.None:
                    rows.Add(new GreenlightEvaluator.Row
                    {
                        Label = "Device Snapshot",
                        Status = CheckRow.Status.Info,
                        Detail = "Not connected - click Connect Device to pull live /qa/snapshot state.",
                    });
                    return rows;

                case Outcome.NoPasswordConfigured:
                    rows.Add(new GreenlightEvaluator.Row
                    {
                        Label = "Device Snapshot",
                        Status = CheckRow.Status.Info,
                        Detail = state.DetailMessage,
                        Fix = "Set SorollaConfig.qaBridgePassword (Palette > Configuration) to enable the editor-side snapshot pull for this project.",
                    });
                    return rows;

                case Outcome.AdbNotFound:
                case Outcome.NoDevice:
                    rows.Add(new GreenlightEvaluator.Row
                    {
                        Label = "Device Snapshot",
                        Status = CheckRow.Status.Wait,
                        Detail = state.DetailMessage,
                        Fix = "Connect an Android device with USB debugging enabled and re-run, or skip - this step is optional.",
                    });
                    return rows;

                case Outcome.Unreachable:
                    rows.Add(new GreenlightEvaluator.Row
                    {
                        Label = "Device Snapshot",
                        Status = CheckRow.Status.Wait,
                        Detail = state.DetailMessage,
                        Fix = "Confirm the app is installed, foregrounded, and the QA bridge is armed, then Connect Device again.",
                    });
                    return rows;

                case Outcome.Parsed:
                    AddParsedRows(rows, state.Snapshot);
                    return rows;

                default:
                    return rows;
            }
        }

        static void AddParsedRows(List<GreenlightEvaluator.Row> rows, Dictionary<string, object> snapshot)
        {
            bool ready = GetBool(snapshot, "ready");
            rows.Add(new GreenlightEvaluator.Row
            {
                Label = "Device Snapshot: Ready",
                Status = ready ? CheckRow.Status.Pass : CheckRow.Status.Fail,
                Detail = ready ? $"mode={GetString(snapshot, "mode")}" : "SDK reports not ready",
                Fix = ready ? null : "Open the in-app debug menu (Vitals) on this device for the full WHY/SIGNAL/FIX breakdown.",
            });

            if (TryGetObject(snapshot, "identity", out var identity))
            {
                bool adPresent = GetBool(identity, "advertising_id_present");
                rows.Add(new GreenlightEvaluator.Row
                {
                    Label = "Device Snapshot: Advertising ID",
                    Status = adPresent ? CheckRow.Status.Pass : CheckRow.Status.Warn,
                    Detail = adPresent ? "Present" : "Not present (expected if ATT/consent denied, or zeroed by OS privacy settings)",
                });
            }

            if (TryGetObject(snapshot, "problems", out var problems))
            {
                long errorCount = GetLong(problems, "sdk_errors");
                rows.Add(new GreenlightEvaluator.Row
                {
                    Label = "Device Snapshot: SDK Errors",
                    Status = errorCount > 0 ? CheckRow.Status.Fail : CheckRow.Status.Pass,
                    Detail = errorCount > 0 ? $"{errorCount} error(s) - last: {GetString(problems, "last_sdk_error")}" : "None observed this session",
                    Fix = errorCount > 0 ? "Open the in-app debug menu (Vitals) Issues tab for WHY/SIGNAL/FIX on each error." : null,
                });
            }
        }

        static bool GetBool(Dictionary<string, object> dict, string key) =>
            dict != null && dict.TryGetValue(key, out object v) && v is bool b && b;

        static long GetLong(Dictionary<string, object> dict, string key) =>
            dict != null && dict.TryGetValue(key, out object v) ? Convert.ToInt64(v) : 0;

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
