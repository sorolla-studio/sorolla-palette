using System;
using System.Collections.Generic;
using Sorolla.Palette.Editor.UI;
using Sorolla.Palette.Health;

namespace Sorolla.Palette.Editor.Greenlight
{
    /// <summary>
    ///     Routes the Editor greenlight through the ONE shared aggregation
    ///     (<see cref="HealthEvaluator.Evaluate"/>): the <see cref="GreenlightAdapter"/> maps Build Health and
    ///     device evidence onto neutral observations + a trusted evaluation context, the shared
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
            public string Label;
            public RowStatus Status;
            public string Detail;
            /// <summary>Fix text shown for non-Pass rows.</summary>
            public string Fix;
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

        /// <summary>One report for every surface: what it CONTAINS never depends on which window asked
        /// (2026-07-22). Internal vs studio is purely a rendering-depth filter in the window.</summary>
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
                GreenlightDeviceSnapshot.BuildGuidOf(snapshotState));
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

                RowStatus status = ToStatus(r.Outcome);
                // A deliberate skip/absence still aggregates as Pass (non-blocking), but must never render
                // as an affirmative green check (F5 residual, 2026-07-21 audit review: this collapsed back
                // into Pass end-to-end once the Build Health row list - which special-cased it locally - was
                // deleted by F12). Display-only override; PassCount below still counts it (the `default`
                // arm covers both Pass and Info), so the verdict/aggregation is untouched.
                if (r.Informational && status == RowStatus.Pass)
                    status = RowStatus.Info;
                report.Rows.Add(new Row
                {
                    GateId = r.GateId,
                    Label = GreenlightAdapter.LabelFor(r.GateId),
                    Status = status,
                    Detail = DetailFor(r),
                    Fix = r.FixHint,
                });

                switch (status)
                {
                    case RowStatus.Fail: report.FailCount++; break;
                    case RowStatus.Warn: report.WarnCount++; break;
                    case RowStatus.Wait: report.WaitCount++; break;
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
                    Status = RowStatus.Wait,
                    Detail = error,
                    Fix = "This is an SDK/report contract error, not a studio config issue - report it to Sorolla.",
                });
                report.WaitCount++;
            }

            return report;
        }

        /// <summary>The row's own message, or null when there is nothing to say. Null, never "": a passing
        /// gate with no evidence line has no second line to render, and an empty string would render one
        /// (blank) instead of none.</summary>
        static string DetailFor(GateResult r)
        {
            if (!string.IsNullOrEmpty(r.Evidence))
                return r.Evidence;
            if (r.Requirement == Requirement.Unknown)
                return r.RequirementReason ?? "Requirement could not be determined";
            return r.Outcome == GateOutcome.Incomplete ? "Required evidence missing or not yet gathered" : null;
        }

        /// <summary>Aggregate/row outcome → display status. Pure mapping, not a precedence computation.</summary>
        static RowStatus ToStatus(GateOutcome outcome) => outcome switch
        {
            GateOutcome.Fail => RowStatus.Fail,
            GateOutcome.PassWithCaveats => RowStatus.Warn,
            GateOutcome.Incomplete => RowStatus.Wait,
            GateOutcome.Pass => RowStatus.Pass,
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
    }
}
