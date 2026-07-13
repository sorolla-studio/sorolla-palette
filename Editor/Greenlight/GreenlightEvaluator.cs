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
            Incomplete,
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
        ///     Four-state verdict label (FAILING / INCOMPLETE / n ISSUES / HEALTHY). INCOMPLETE means
        ///     required evidence is missing, stale, or unverifiable - the report cannot honestly claim
        ///     HEALTHY, so it must never render green (the false-green this evaluator was hardened
        ///     against). Sorolla Vitals is a separate surface; keep its wording in step when it gains
        ///     the same state.
        /// </summary>
        internal static string VerdictLabel(Verdict verdict, int failCount, int warnCount)
        {
            switch (verdict)
            {
                case Verdict.Failing: return "FAILING";
                case Verdict.Incomplete: return "INCOMPLETE";
                case Verdict.Issues: return $"{failCount + warnCount} ISSUES";
                default: return "HEALTHY";
            }
        }

        /// <summary>
        ///     Verdict → badge severity. INCOMPLETE maps to the non-green Wait pill: missing evidence
        ///     must look unresolved, not passing. No permissive default arm - a future verdict state
        ///     throws here (fails loud) rather than silently rendering green.
        /// </summary>
        internal static StatusBadge.Severity BadgeSeverity(Verdict verdict)
        {
            switch (verdict)
            {
                case Verdict.Failing: return StatusBadge.Severity.Fail;
                case Verdict.Incomplete: return StatusBadge.Severity.Wait;
                case Verdict.Issues: return StatusBadge.Severity.Advisory;
                case Verdict.Healthy: return StatusBadge.Severity.Pass;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(verdict), verdict, "Unhandled greenlight verdict - add a badge severity mapping.");
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
                    // A Pass can still carry a Fix (e.g. GA credential probe's "verify platform
                    // registration manually" reminder) - the probe validated one narrower fact than
                    // the row's label implies, and that residual gap belongs in Fix, not the message.
                    report.Rows.Add(new Row { Label = label, Status = CheckRow.Status.Pass, Detail = FirstLine(result.Message), Fix = result.Fix });
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

        internal static void ComputeVerdict(Report report)
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

            // Precedence FAIL > INCOMPLETE > ISSUES > HEALTHY. Missing/stale/unverifiable required
            // evidence OUTRANKS warnings: a report cannot be "just issues" while a required gate has
            // never produced evidence. Interim "required" definition is deliberately a broad safety
            // floor - ANY Wait row (Build Health not run, a probe pending/unverifiable, a device
            // snapshot that came back NoDevice/Unreachable, an unticked manual gate) plus the
            // zero-rows case (nothing evaluated at all) forces INCOMPLETE. Info stays neutral by
            // policy: the device-not-connected row is Info and optional-by-design, so it does not
            // itself block HEALTHY - the unticked manual gates (Wait) already do. Cycle 4 replaces
            // this floor with a per-row required/proof-scope model on the shared contract.
            bool missingRequiredEvidence = report.Rows.Count == 0 || report.WaitCount > 0;

            report.Verdict = report.FailCount > 0
                ? Verdict.Failing
                : missingRequiredEvidence
                    ? Verdict.Incomplete
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
