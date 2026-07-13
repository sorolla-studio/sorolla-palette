using System;
using NUnit.Framework;
using Sorolla.Palette.Editor.Greenlight;
using Sorolla.Palette.Editor.UI;
using UnityEngine;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     Truth table for <see cref="GreenlightEvaluator.ComputeVerdict"/> - the aggregation that was
    ///     the false-green: it used to only react to Fail/Warn, so a zero-evidence report (all Wait
    ///     rows, only Info rows, or no rows) rendered HEALTHY/green. These pin the four-state
    ///     precedence FAIL &gt; INCOMPLETE &gt; ISSUES &gt; HEALTHY, the "no affirmative evidence is
    ///     never HEALTHY" rule, the badge mapping that keeps INCOMPLETE visibly non-green, and the
    ///     foldout summary that never claims an incomplete report's rows were "checked". One test
    ///     drives the real <see cref="GreenlightEvaluator.Evaluate"/> end-to-end so the
    ///     evidence-producing rows can't silently misclassify while the synthetic-report tests pass.
    /// </summary>
    [TestFixture]
    public class GreenlightEvaluatorTests
    {
        static GreenlightEvaluator.Report ReportOf(params CheckRow.Status[] statuses)
        {
            var report = new GreenlightEvaluator.Report();
            foreach (CheckRow.Status status in statuses)
                report.Rows.Add(new GreenlightEvaluator.Row { Label = status.ToString(), Status = status });
            GreenlightEvaluator.ComputeVerdict(report);
            return report;
        }

        [Test]
        public void AllPass_IsHealthy()
        {
            GreenlightEvaluator.Report report = ReportOf(CheckRow.Status.Pass, CheckRow.Status.Pass);
            Assert.AreEqual(GreenlightEvaluator.Verdict.Healthy, report.Verdict);
        }

        [Test]
        public void WarnOnly_NoMissingEvidence_IsIssues()
        {
            GreenlightEvaluator.Report report = ReportOf(CheckRow.Status.Pass, CheckRow.Status.Warn);
            Assert.AreEqual(GreenlightEvaluator.Verdict.Issues, report.Verdict);
        }

        [Test]
        public void WarnPlusMissing_IncompleteBeatsIssues()
        {
            // B-01: missing/unverifiable required evidence outranks warnings.
            GreenlightEvaluator.Report report = ReportOf(CheckRow.Status.Warn, CheckRow.Status.Wait);
            Assert.AreEqual(GreenlightEvaluator.Verdict.Incomplete, report.Verdict);
        }

        [Test]
        public void FailPlusMissing_IsFailing()
        {
            // Fail outranks everything, including missing evidence.
            GreenlightEvaluator.Report report = ReportOf(CheckRow.Status.Fail, CheckRow.Status.Wait, CheckRow.Status.Warn);
            Assert.AreEqual(GreenlightEvaluator.Verdict.Failing, report.Verdict);
        }

        [Test]
        public void ZeroRows_IsIncomplete()
        {
            // Nothing evaluated at all must never read as HEALTHY.
            GreenlightEvaluator.Report report = ReportOf();
            Assert.AreEqual(GreenlightEvaluator.Verdict.Incomplete, report.Verdict);
        }

        [Test]
        public void UnverifiableProbesOnly_IsIncomplete()
        {
            // Probes that came back pending/offline surface as Wait rows - never a silent pass.
            GreenlightEvaluator.Report report = ReportOf(CheckRow.Status.Wait, CheckRow.Status.Wait);
            Assert.AreEqual(GreenlightEvaluator.Verdict.Incomplete, report.Verdict);
        }

        [Test]
        public void PassPlusUntickedManual_IsIncomplete()
        {
            // The zero-evidence-adjacent case: everything machine-checkable passes but a required
            // manual gate is still unticked (Wait). Must not render HEALTHY.
            GreenlightEvaluator.Report report = ReportOf(CheckRow.Status.Pass, CheckRow.Status.Pass, CheckRow.Status.Wait);
            Assert.AreEqual(GreenlightEvaluator.Verdict.Incomplete, report.Verdict);
        }

        [Test]
        public void InfoIsNeutral_PassPlusInfo_IsHealthy()
        {
            // Info (e.g. device-not-connected, optional-by-design) does not block HEALTHY when real
            // affirmative (Pass) evidence exists alongside it.
            GreenlightEvaluator.Report report = ReportOf(CheckRow.Status.Pass, CheckRow.Status.Info);
            Assert.AreEqual(GreenlightEvaluator.Verdict.Healthy, report.Verdict);
        }

        [Test]
        public void InfoOnly_IsIncomplete()
        {
            // Neutral rows are not affirmative evidence: an Info-only report has evaluated nothing, so
            // it must read INCOMPLETE, not HEALTHY. Info never fails, but it can't carry a verdict.
            GreenlightEvaluator.Report report = ReportOf(CheckRow.Status.Info, CheckRow.Status.Info);
            Assert.AreEqual(GreenlightEvaluator.Verdict.Incomplete, report.Verdict);
        }

        [Test]
        public void Evaluate_RealPathWithNoEvidence_IsIncompleteAndNonGreen()
        {
            // End-to-end through the REAL Evaluate: no Build Health results, a never-connected device
            // snapshot, and no completed manual evidence. The evidence-producing rows (Build Health,
            // probes, unticked manual gates) must land as Wait, forcing a non-green INCOMPLETE. A
            // suite that only feeds synthetic Reports to the aggregator could pass while these rows
            // silently misclassify.
            var config = ScriptableObject.CreateInstance<SorollaConfig>();
            try
            {
                GreenlightEvaluator.Report report = GreenlightEvaluator.Evaluate(
                    buildHealthResults: null,
                    config: config,
                    snapshotState: new GreenlightDeviceSnapshot.State(),
                    checklist: new GreenlightManualChecklist.State());

                Assert.AreEqual(GreenlightEvaluator.Verdict.Incomplete, report.Verdict,
                    "A report with no run evidence must be INCOMPLETE, not HEALTHY.");
                Assert.Greater(report.WaitCount, 0, "Unrun/unticked evidence rows must surface as Wait.");
                Assert.AreNotEqual(StatusBadge.Severity.Pass, GreenlightEvaluator.BadgeSeverity(report.Verdict),
                    "The badge for a no-evidence report must not be the green Pass pill.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void BadgeSeverity_UnknownVerdict_ThrowsInsteadOfPass()
        {
            // A future/unmapped verdict value must fail loud, never fall through to a green Pass.
            Assert.Throws<ArgumentOutOfRangeException>(
                () => GreenlightEvaluator.BadgeSeverity((GreenlightEvaluator.Verdict)999));
        }

        [Test]
        public void RowSummary_Incomplete_NeverReadsRowsChecked()
        {
            // R-05: an incomplete-only report's foldout must not claim its rows were "checked".
            GreenlightEvaluator.Report waitOnly = ReportOf(CheckRow.Status.Wait, CheckRow.Status.Wait);
            StringAssert.DoesNotContain("rows checked", GreenlightEvaluator.RowSummary(waitOnly));

            GreenlightEvaluator.Report infoOnly = ReportOf(CheckRow.Status.Info, CheckRow.Status.Info);
            StringAssert.DoesNotContain("rows checked", GreenlightEvaluator.RowSummary(infoOnly));
        }

        [Test]
        public void RowSummary_Healthy_ReadsRowsChecked()
        {
            GreenlightEvaluator.Report report = ReportOf(CheckRow.Status.Pass, CheckRow.Status.Pass);
            Assert.AreEqual("2 rows checked", GreenlightEvaluator.RowSummary(report));
        }

        [Test]
        public void RowSummary_Issues_CountsWarnsAsNeedingAttention()
        {
            GreenlightEvaluator.Report report = ReportOf(CheckRow.Status.Pass, CheckRow.Status.Warn);
            Assert.AreEqual("1 of 2 rows need attention", GreenlightEvaluator.RowSummary(report));
        }

        [Test]
        public void Counts_TallyEveryStatus()
        {
            GreenlightEvaluator.Report report = ReportOf(
                CheckRow.Status.Fail,
                CheckRow.Status.Warn, CheckRow.Status.Warn,
                CheckRow.Status.Wait,
                CheckRow.Status.Info,
                CheckRow.Status.Pass, CheckRow.Status.Pass, CheckRow.Status.Pass);

            Assert.AreEqual(1, report.FailCount);
            Assert.AreEqual(2, report.WarnCount);
            Assert.AreEqual(1, report.WaitCount);
            Assert.AreEqual(1, report.InfoCount);
            Assert.AreEqual(3, report.PassCount);
        }

        [Test]
        public void BadgeSeverity_MapsEveryVerdict()
        {
            // Incomplete must be visibly non-green (Wait), and every verdict must map explicitly -
            // no permissive default that could render an unknown state green.
            Assert.AreEqual(StatusBadge.Severity.Pass, GreenlightEvaluator.BadgeSeverity(GreenlightEvaluator.Verdict.Healthy));
            Assert.AreEqual(StatusBadge.Severity.Advisory, GreenlightEvaluator.BadgeSeverity(GreenlightEvaluator.Verdict.Issues));
            Assert.AreEqual(StatusBadge.Severity.Wait, GreenlightEvaluator.BadgeSeverity(GreenlightEvaluator.Verdict.Incomplete));
            Assert.AreEqual(StatusBadge.Severity.Fail, GreenlightEvaluator.BadgeSeverity(GreenlightEvaluator.Verdict.Failing));
        }

        [Test]
        public void VerdictLabel_Incomplete_ReadsIncomplete()
        {
            Assert.AreEqual("INCOMPLETE", GreenlightEvaluator.VerdictLabel(GreenlightEvaluator.Verdict.Incomplete, 0, 0));
        }

        [Test]
        public void VerdictLabel_MapsEveryVerdictExplicitly()
        {
            Assert.AreEqual("HEALTHY", GreenlightEvaluator.VerdictLabel(GreenlightEvaluator.Verdict.Healthy, 0, 0));
            Assert.AreEqual("2 ISSUES", GreenlightEvaluator.VerdictLabel(GreenlightEvaluator.Verdict.Issues, 1, 1));
            Assert.AreEqual("INCOMPLETE", GreenlightEvaluator.VerdictLabel(GreenlightEvaluator.Verdict.Incomplete, 0, 0));
            Assert.AreEqual("FAILING", GreenlightEvaluator.VerdictLabel(GreenlightEvaluator.Verdict.Failing, 0, 0));
        }

        [Test]
        public void VerdictLabel_UnknownVerdict_ThrowsInsteadOfHealthy()
        {
            // R2-01: the label had a permissive `default: return "HEALTHY"` - an unknown verdict must
            // fail loud, not silently read HEALTHY.
            Assert.Throws<ArgumentOutOfRangeException>(
                () => GreenlightEvaluator.VerdictLabel((GreenlightEvaluator.Verdict)999, 0, 0));
        }

        [Test]
        public void ToPlainText_UnknownVerdict_ThrowsInsteadOfExportingHealthy()
        {
            // The copy-report export goes through VerdictLabel; an unknown verdict must not export as
            // a green HEALTHY header.
            var report = new GreenlightEvaluator.Report { Verdict = (GreenlightEvaluator.Verdict)999 };
            Assert.Throws<ArgumentOutOfRangeException>(() => GreenlightEvaluator.ToPlainText(report));
        }
    }
}
