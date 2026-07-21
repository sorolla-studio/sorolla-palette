using System;
using System.Collections.Generic;
using System.Linq;
using Sorolla.Palette.Editor.UI;
using Sorolla.Palette.Health;

namespace Sorolla.Palette.Editor.Greenlight
{
    /// <summary>
    ///     Routes the Editor greenlight through the ONE shared aggregation
    ///     (<see cref="HealthEvaluator.Evaluate"/>): the <see cref="GreenlightAdapter"/> maps Build Health,
    ///     device, and manual evidence onto neutral observations + a trusted evaluation context, the shared
    ///     evaluator against the canonical <see cref="GateCatalog"/> produces the verdict, and this class only
    ///     maps the resulting <see cref="GateOutcome"/> to display. There is deliberately no second precedence
    ///     algorithm here - the label/badge/summary helpers MAP the aggregate outcome, they never recompute it
    ///     (design note section 6).
    /// </summary>
    static class GreenlightEvaluator
    {
        internal sealed class Row
        {
            /// <summary>The canonical gate id (<see cref="Sorolla.Palette.Health.GateIds"/>), null for the
            /// synthetic Report Integrity row. Lets the window look up an in-editor remedy action for a
            /// specific gate (product-audit fix cycle ruling 1/5, 2026-07-21 11:55) without re-deriving it
            /// from the display label.</summary>
            public string GateId;
            /// <summary>Carried through so the studio row filter can tell "this manual/Invariant row is a
            /// Sorolla-QA attestation ask" (skip on the studio surface) apart from "this manual/Invariant
            /// row is actually the uncertified-pin fix line, which IS studio-actionable" (F1 ruling,
            /// 2026-07-21 ~12:30) - both share the same GateId/label shape (manual descriptor lookup can't
            /// tell them apart), but only the pin case reaches <see cref="GateDisposition.Omitted"/>.</summary>
            public GateDisposition Disposition;
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
            public GateOutcome Outcome;
            public IReadOnlyList<string> ValidationErrors = Array.Empty<string>();
            public int FailCount;
            public int WarnCount;
            public int WaitCount;
            public int PassCount;
            /// <summary>The full shared result behind the flattened display rows - the canonical report export
            /// (review F4) reads this so it can emit every row (including the inert NotApplicable/OptionalSkipped
            /// ones the UI collapses) with its stable id, version, disposition, and proof.</summary>
            public HealthReport Health;
            public EvaluationContext Context;
            public GreenlightReportExport.Fingerprint Fingerprint;
        }

        internal static Report Evaluate(
            List<BuildValidator.ValidationResult> buildHealthResults,
            GreenlightDeviceSnapshot.State snapshotState)
        {
            EvaluationContext context = GreenlightAdapter.BuildContext();
            List<GateObservation> observations =
                GreenlightAdapter.BuildObservations(context, buildHealthResults, snapshotState);
            HealthReport health = HealthEvaluator.Evaluate(GateCatalog.Canonical, context, observations);
            Report report = ToReport(health);
            report.Health = health;
            report.Context = context;
            report.Fingerprint = GreenlightReportExport.Fingerprint.Capture(
                context, GreenlightDeviceSnapshot.BuildGuidOf(snapshotState));
            return report;
        }

        /// <summary>Maps the shared <see cref="HealthReport"/> to the flat display shape the window renders.
        /// Inert rows (NotApplicable, OptionalSkipped) are excluded from the verdict and not shown.</summary>
        internal static Report ToReport(HealthReport health)
        {
            var report = new Report
            {
                Outcome = health.Outcome,
                ValidationErrors = health.ValidationErrors ?? Array.Empty<string>(),
            };

            foreach (GateResult r in health.Rows)
            {
                // Show evaluated + omitted-required rows; hide the inert ones (optional skips, not-applicable).
                if (r.Disposition == GateDisposition.OptionalSkipped || r.Disposition == GateDisposition.NotApplicable)
                    continue;

                CheckRow.Status status = ToStatus(r.Outcome);
                // A deliberate skip/absence still aggregates as Pass (non-blocking), but must never render
                // as an affirmative green check (F5 residual, 2026-07-21 audit review: this collapsed back
                // into Pass end-to-end once the Build Health row list - which special-cased it locally - was
                // deleted by F12). Display-only override; PassCount below still counts it (the `default`
                // arm covers both Pass and Info), so the verdict/aggregation is untouched.
                if (r.Informational && status == CheckRow.Status.Pass)
                    status = CheckRow.Status.Info;
                (string url, string linkLabel) = GreenlightAdapter.DeepLinkFor(r.GateId);
                report.Rows.Add(new Row
                {
                    GateId = r.GateId,
                    Disposition = r.Disposition,
                    Label = GreenlightAdapter.LabelFor(r.GateId),
                    Status = status,
                    Detail = DetailFor(r),
                    Fix = r.FixHint,
                    DeepLinkUrl = url,
                    DeepLinkLabel = linkLabel,
                });

                switch (status)
                {
                    case CheckRow.Status.Fail: report.FailCount++; break;
                    case CheckRow.Status.Warn: report.WarnCount++; break;
                    case CheckRow.Status.Wait: report.WaitCount++; break;
                    default: report.PassCount++; break;
                }
            }

            // Contract/schema validation errors force the aggregate to INCOMPLETE; render each as an explicit,
            // visible row (review C4-07) so a studio sees the actionable reason instead of a silent non-green
            // badge over passing-looking rows. Included in the copied plain-text report via report.Rows.
            foreach (string error in report.ValidationErrors)
            {
                report.Rows.Add(new Row
                {
                    Label = "Report Integrity",
                    Status = CheckRow.Status.Wait,
                    Detail = error,
                    Fix = "This is an SDK/report contract error, not a studio config issue - report it to Sorolla.",
                });
                report.WaitCount++;
            }

            return report;
        }

        static string DetailFor(GateResult r)
        {
            if (!string.IsNullOrEmpty(r.Evidence))
                return r.Evidence;
            if (r.Requirement == Requirement.Unknown)
                return r.RequirementReason ?? "Requirement could not be determined";
            return r.Outcome == GateOutcome.Incomplete ? "Required evidence missing or not yet gathered" : "";
        }

        /// <summary>Aggregate/row outcome → display status. Pure mapping, not a precedence computation.</summary>
        static CheckRow.Status ToStatus(GateOutcome outcome) => outcome switch
        {
            GateOutcome.Fail => CheckRow.Status.Fail,
            GateOutcome.PassWithCaveats => CheckRow.Status.Warn,
            GateOutcome.Incomplete => CheckRow.Status.Wait,
            GateOutcome.Pass => CheckRow.Status.Pass,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unhandled gate outcome."),
        };

        /// <summary>
        ///     Four-state verdict label (FAILING / INCOMPLETE / n ISSUES / HEALTHY). Maps the aggregate
        ///     outcome; INCOMPLETE means required evidence is missing, stale, or unverifiable - the report
        ///     cannot honestly claim HEALTHY, so it never renders green. No permissive default arm - a
        ///     future/unknown outcome fails loud, never exports as HEALTHY.
        /// </summary>
        internal static string VerdictLabel(GateOutcome outcome, int failCount, int warnCount) => outcome switch
        {
            GateOutcome.Fail => "FAILING",
            GateOutcome.Incomplete => "INCOMPLETE",
            GateOutcome.PassWithCaveats => $"{failCount + warnCount} ISSUES",
            GateOutcome.Pass => "HEALTHY",
            _ => throw new ArgumentOutOfRangeException(
                nameof(outcome), outcome, "Unhandled greenlight outcome - add a label mapping."),
        };

        /// <summary>
        ///     Outcome → badge severity. INCOMPLETE maps to the non-green Wait pill: missing evidence must
        ///     look unresolved, not passing. No permissive default arm - a future outcome throws (fails
        ///     loud) rather than silently rendering green.
        /// </summary>
        internal static StatusBadge.Severity BadgeSeverity(GateOutcome outcome) => outcome switch
        {
            GateOutcome.Fail => StatusBadge.Severity.Fail,
            GateOutcome.Incomplete => StatusBadge.Severity.Wait,
            GateOutcome.PassWithCaveats => StatusBadge.Severity.Advisory,
            GateOutcome.Pass => StatusBadge.Severity.Pass,
            _ => throw new ArgumentOutOfRangeException(
                nameof(outcome), outcome, "Unhandled greenlight outcome - add a badge severity mapping."),
        };

        /// <summary>
        ///     One-line summary for the collapsed rows foldout. Wait rows count as "need attention"
        ///     alongside Fail/Warn, and an INCOMPLETE report NEVER reads "rows checked" - a pending or
        ///     evidence-less report must not imply its rows were affirmatively checked.
        /// </summary>
        internal static string RowSummary(Report report)
        {
            int total = report.Rows.Count;
            int needsAttention = report.FailCount + report.WarnCount + report.WaitCount;

            if (report.Outcome == GateOutcome.Incomplete)
                return needsAttention > 0
                    ? $"{needsAttention} of {total} rows need evidence or attention"
                    : $"{total} rows, evidence incomplete";

            if (needsAttention > 0)
                return $"{needsAttention} of {total} rows need attention";

            return $"{total} rows checked";
        }
    }
}
