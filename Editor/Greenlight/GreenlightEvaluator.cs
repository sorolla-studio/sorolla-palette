using System;
using System.Collections.Generic;
using System.Linq;
using Sorolla.Palette.Editor.UI;

namespace Sorolla.Palette.Editor.Greenlight
{
    /// <summary>
    ///     Composes every evidence class the studio self-serve greenlight plan defines (Build Health,
    ///     editor probes, mode-intent, device snapshot, manual/dashboard checklist) into one mechanical
    ///     verdict. Pure evaluator: takes already-computed inputs, never runs a probe or a build check
    ///     itself - <see cref="BuildValidator.RunAllChecks"/> and the probe validators
    ///     (<see cref="FacebookPlatformValidator"/>, <see cref="GameAnalyticsCredentialValidator"/>)
    ///     already run as part of Build Health; this only reads their cached results.
    /// </summary>
    static class GreenlightEvaluator
    {
        internal enum Verdict
        {
            Healthy,
            Issues,
            Failing,
        }

        internal sealed class Row
        {
            public string Label;
            public CheckRow.Status Status;
            public string Detail;
            /// <summary>Fix text shown for non-Pass rows. Manual/dashboard rows must always carry one -
            /// never render as a bare unchecked box (brief requirement).</summary>
            public string Fix;
            public string DeepLinkLabel;
            public string DeepLinkUrl;
        }

        internal sealed class Report
        {
            public readonly List<Row> Rows = new List<Row>();
            public Verdict Verdict;
            public int FailCount;
            public int WarnCount;
            public int WaitCount;
            public int PassCount;
            public int InfoCount;
        }

        /// <summary>
        ///     Same verdict semantics as Sorolla Vitals (FAILING / n ISSUES / HEALTHY) - the two
        ///     surfaces speak one language, see the studio-self-serve-greenlight plan.
        /// </summary>
        internal static string VerdictLabel(Verdict verdict, int failCount, int warnCount)
        {
            switch (verdict)
            {
                case Verdict.Failing: return "FAILING";
                case Verdict.Issues: return $"{failCount + warnCount} ISSUES";
                default: return "HEALTHY";
            }
        }

        internal static Report Evaluate(
            List<BuildValidator.ValidationResult> buildHealthResults,
            SorollaConfig config,
            GreenlightDeviceSnapshot.State snapshotState,
            GreenlightManualChecklist.State checklist)
        {
            var report = new Report();

            AddBuildHealthSummaryRow(report, buildHealthResults);
            AddProbeRow(report, "Facebook Platform (Graph API)", buildHealthResults, BuildValidator.CheckCategory.FacebookPlatformConfig);
            AddProbeRow(report, "GameAnalytics Credentials", buildHealthResults, BuildValidator.CheckCategory.GameAnalyticsCredentialProbe);
            AddModeIntentRow(report, config);
            AddDeviceSnapshotRows(report, snapshotState);
            AddManualRows(report, config, checklist);

            ComputeVerdict(report);
            return report;
        }

        // ── Build Health (a) ──────────────────────────────────────────

        static void AddBuildHealthSummaryRow(Report report, List<BuildValidator.ValidationResult> results)
        {
            if (results == null)
            {
                report.Rows.Add(new Row
                {
                    Label = "Build Health",
                    Status = CheckRow.Status.Wait,
                    Detail = "Not run yet",
                    Fix = "Click Refresh to run Build Health checks.",
                });
                return;
            }

            int errors = results.Count(r => r.Status == BuildValidator.ValidationStatus.Error);
            int warnings = results.Count(r => r.Status == BuildValidator.ValidationStatus.Warning);

            CheckRow.Status status = errors > 0 ? CheckRow.Status.Fail : warnings > 0 ? CheckRow.Status.Warn : CheckRow.Status.Pass;
            string detail = errors > 0
                ? $"{errors} failing check(s) - see Build Health section below"
                : warnings > 0
                    ? $"{warnings} warning(s) - see Build Health section below"
                    : $"{results.Count} checks passing";

            report.Rows.Add(new Row
            {
                Label = "Build Health",
                Status = status,
                Detail = detail,
                Fix = errors > 0 || warnings > 0 ? "Expand the Build Health section below and fix the failing/warning rows." : null,
            });
        }

        // ── Editor probes (b) ─────────────────────────────────────────

        static void AddProbeRow(Report report, string label, List<BuildValidator.ValidationResult> results, BuildValidator.CheckCategory category)
        {
            BuildValidator.ValidationResult result = results?.FirstOrDefault(r => r.Category == category);
            if (result == null)
            {
                report.Rows.Add(new Row { Label = label, Status = CheckRow.Status.Wait, Detail = "Not run yet" });
                return;
            }

            switch (result.Status)
            {
                case BuildValidator.ValidationStatus.Error:
                    report.Rows.Add(new Row { Label = label, Status = CheckRow.Status.Fail, Detail = FirstLine(result.Message), Fix = result.Fix });
                    return;
                case BuildValidator.ValidationStatus.Warning:
                    report.Rows.Add(new Row { Label = label, Status = CheckRow.Status.Warn, Detail = FirstLine(result.Message), Fix = result.Fix });
                    return;
                case BuildValidator.ValidationStatus.Unverifiable:
                    // Pending or offline - never rendered as a pass (Build Health's own honesty rule).
                    report.Rows.Add(new Row { Label = label, Status = CheckRow.Status.Wait, Detail = FirstLine(result.Message) });
                    return;
                default:
                    report.Rows.Add(new Row { Label = label, Status = CheckRow.Status.Pass, Detail = FirstLine(result.Message) });
                    return;
            }
        }

        static string FirstLine(string message) => string.IsNullOrEmpty(message) ? "" : message.Split('\n')[0];

        // ── Mode intent (c) ───────────────────────────────────────────

        static void AddModeIntentRow(Report report, SorollaConfig config)
        {
            SorollaQaExpectations expectations = SorollaQaExpectations.Current;
            if (expectations == null || expectations.intendedMode == SorollaQaIntendedMode.Unspecified)
            {
                report.Rows.Add(new Row
                {
                    Label = "Mode Intent",
                    Status = CheckRow.Status.Info,
                    Detail = expectations == null
                        ? "No QA Expectations asset - no mode-intent check"
                        : "Intended mode not set on the QA Expectations asset - no mode-intent check",
                    Fix = "Optional: set Intended Mode on Assets/Resources/SorollaQaExpectations.asset to catch a build shipping in the wrong mode for this game.",
                });
                return;
            }

            SorollaMode actualMode = SorollaSettings.Mode;
            bool actualIsFull = actualMode == SorollaMode.Full;
            bool intendedIsFull = expectations.intendedMode == SorollaQaIntendedMode.Full;

            if (actualMode == SorollaMode.None)
            {
                report.Rows.Add(new Row
                {
                    Label = "Mode Intent",
                    Status = CheckRow.Status.Fail,
                    Detail = "Build mode is unknown (no SorollaConfig) - cannot compare against the declared intent",
                    Fix = "Palette > Configuration, then create the config asset.",
                });
                return;
            }

            // Prototype is a first-class release path (FB UA tests) - only a genuine intended-vs-actual
            // mismatch fails; being in Prototype is never itself a failure.
            bool matches = actualIsFull == intendedIsFull;
            report.Rows.Add(new Row
            {
                Label = "Mode Intent",
                Status = matches ? CheckRow.Status.Pass : CheckRow.Status.Fail,
                Detail = matches
                    ? $"Build is in {actualMode}, matches the declared intent"
                    : $"Build is in {actualMode}, but the QA Expectations asset declares {expectations.intendedMode}",
                Fix = matches ? null : "Switch the SDK mode in Palette > Configuration to match the declared intent, or update Intended Mode on the QA Expectations asset if the intent changed.",
            });
        }

        // ── Device snapshot (d) ───────────────────────────────────────

        static void AddDeviceSnapshotRows(Report report, GreenlightDeviceSnapshot.State state)
        {
            report.Rows.AddRange(GreenlightDeviceSnapshot.ToRows(state));
        }

        // ── Manual / dashboard checklist (e) ──────────────────────────

        static void AddManualRows(Report report, SorollaConfig config, GreenlightManualChecklist.State checklist)
        {
            report.Rows.AddRange(GreenlightManualChecklist.ToRows(config, checklist));
        }

        // ── Verdict ────────────────────────────────────────────────────

        static void ComputeVerdict(Report report)
        {
            foreach (Row row in report.Rows)
            {
                switch (row.Status)
                {
                    case CheckRow.Status.Fail: report.FailCount++; break;
                    case CheckRow.Status.Warn: report.WarnCount++; break;
                    case CheckRow.Status.Wait: report.WaitCount++; break;
                    case CheckRow.Status.Info: report.InfoCount++; break;
                    default: report.PassCount++; break;
                }
            }

            report.Verdict = report.FailCount > 0
                ? Verdict.Failing
                : report.WarnCount > 0
                    ? Verdict.Issues
                    : Verdict.Healthy;
        }

        /// <summary>Plain-text report for the "Copy greenlight report" button - pasteable into chat.</summary>
        internal static string ToPlainText(Report report)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Palette Greenlight: {VerdictLabel(report.Verdict, report.FailCount, report.WarnCount)}");
            sb.AppendLine($"({report.FailCount} fail, {report.WarnCount} warn, {report.WaitCount} wait, {report.InfoCount} info, {report.PassCount} pass)");
            sb.AppendLine();
            foreach (Row row in report.Rows)
            {
                sb.AppendLine($"[{row.Status.ToString().ToUpperInvariant()}] {row.Label}: {row.Detail}");
                if (!string.IsNullOrEmpty(row.Fix))
                    sb.AppendLine($"    Fix: {row.Fix}");
            }
            return sb.ToString();
        }
    }
}
