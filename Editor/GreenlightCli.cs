using System;
using System.Collections.Generic;
using System.IO;
using Sorolla.Palette.Editor.Greenlight;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Headless entry point for the canonical greenlight report, so CI and command-line workflows get
    ///     the exact text the window's Copy Report button copies - same checks, same evaluator, same export:
    ///     <code>
    ///     Unity -batchmode -projectPath &lt;project&gt; -buildTarget &lt;target&gt;
    ///           -executeMethod Sorolla.Palette.Editor.GreenlightCli.Report
    ///           [-sorollaReportPath /abs/path.txt] -logFile &lt;log&gt;
    ///     </code>
    ///     Runs the window's refresh sequence, waits (bounded) for the credential probes to settle so the
    ///     report carries real probe verdicts instead of Pending, writes the report, then
    ///     exits 0. Exits 1 when the report cannot be produced. Do not pass -quit: the entry point owns the
    ///     editor lifetime because the probe needs update pumping after -executeMethod returns.
    ///
    ///     The editor is only killed in batch mode. This method is reachable from an interactive editor
    ///     (menu, test, manual -executeMethod), where exiting would take the user's session down with it;
    ///     there it logs the outcome and leaves the editor running.
    /// </summary>
    public static class GreenlightCli
    {
        const double ProbeTimeoutSeconds = 30;
        const string PendingPathKey = "Sorolla.Palette.GreenlightCli.PendingPath";

        static string s_path;
        static double s_deadline;
        static List<BuildValidator.ValidationResult> s_results;

        public static void Report()
        {
            s_path = ArgAfter("-sorollaReportPath") ??
                     Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                         "sorolla-greenlight-report.txt");
            try
            {
                // Project mutation happens exactly once, here. The settle retries below only re-read
                // state: an asynchronous probe settling is not a reason to repeat repairs on the project.
                foreach (string repair in BuildValidator.ResolveRequiredPackages())
                    Debug.Log($"[Palette] Greenlight CLI: auto-fixed {repair}");
                foreach (string repair in BuildValidator.RunSafeAutoFixes())
                    Debug.Log($"[Palette] Greenlight CLI: auto-fixed {repair}");
                s_results = BuildValidator.RunAllChecks();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Palette] Greenlight CLI: check run failed: {e}");
                Finish(1);
                return;
            }

            SessionState.SetString(PendingPathKey, s_path);
            WaitForProbe();
        }

        /// <summary>
        ///     A package change during the run (the SDK version sync on a project's first open, or a repair
        ///     above) reloads the domain, which drops the update subscription and every static: the batch run
        ///     then idles forever with no report and no exit code. SessionState survives the reload, so the
        ///     pending report re-arms here. Repairs do not run again; the first settle tick re-runs the checks,
        ///     which claims the probes afresh.
        /// </summary>
        [InitializeOnLoadMethod]
        static void ResumeAfterDomainReload()
        {
            string path = SessionState.GetString(PendingPathKey, "");
            if (path.Length == 0) return;
            s_path = path;
            WaitForProbe();
        }

        static void WaitForProbe()
        {
            s_deadline = EditorApplication.timeSinceStartup + ProbeTimeoutSeconds;
            EditorApplication.update += WaitForProbeThenWrite;
        }

        /// <summary>
        ///     Whether the CLI must keep waiting instead of writing the report. Pure so the settle
        ///     sequencing is testable without an editor loop or a live probe.
        /// </summary>
        internal static bool ShouldKeepWaiting(bool probePending, double now, double deadline) =>
            probePending && now < deadline;

        static bool AnyProbePending() =>
            GameAnalyticsCredentialValidator.Current.State ==
            GameAnalyticsCredentialValidator.ProbeState.Pending ||
            FacebookPlatformValidator.Current.State == FacebookPlatformValidator.ProbeState.Pending;

        static void WaitForProbeThenWrite()
        {
            // The WHOLE body is guarded, including the settle-time check re-run. An exception escaping
            // this update delegate would leave the handler subscribed and throw again every tick: a
            // batch run would hang until an external timeout with no report and no exit code, which is
            // worse than the failure it came from. Only the two early returns below leave the handler
            // subscribed, and they are the deliberate "still waiting" path.
            try
            {
                if (ShouldKeepWaiting(AnyProbePending(), EditorApplication.timeSinceStartup, s_deadline))
                    return;

                // Nothing is Pending, but the validators also render "Checking..." for NotStarted, so an
                // unclaimed probe would land in the report as Incomplete. Re-running the checks is what
                // CLAIMS such a probe; if that claim put one back to Pending and there is budget left, keep
                // waiting and discard this run. Waiting blanket-style on NotStarted instead would burn the
                // full timeout on every prototype run whose validator never claims (missing credentials).
                s_results = BuildValidator.RunAllChecks();
                if (ShouldKeepWaiting(AnyProbePending(), EditorApplication.timeSinceStartup, s_deadline))
                    return;

                EditorApplication.update -= WaitForProbeThenWrite;
                ReadinessReport report = GreenlightEvaluator.Evaluate(s_results);
                File.WriteAllText(s_path, GreenlightReportExport.ToText(report));
                Debug.Log($"[Palette] Greenlight CLI: report written to {s_path}");
                Finish(0);
            }
            catch (Exception e)
            {
                // Unsubscribe FIRST: this is terminal for the run either way, and a repeat throw is the
                // hang. Removing a handler that is already removed is a no-op, so the late-failure path
                // (which unsubscribed above) is safe too.
                EditorApplication.update -= WaitForProbeThenWrite;
                Debug.LogError($"[Palette] Greenlight CLI: report generation failed: {e}");
                Finish(1);
            }
        }

        /// <summary>
        ///     Only batch mode owns the editor lifetime. Called from an interactive editor, exiting would
        ///     kill the user's session, so the outcome is logged instead.
        /// </summary>
        static void Finish(int exitCode)
        {
            SessionState.EraseString(PendingPathKey);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
                return;
            }

            Debug.Log($"[Palette] Greenlight CLI: finished with exit code {exitCode}; interactive editor left running.");
        }

        static string ArgAfter(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }
    }
}
