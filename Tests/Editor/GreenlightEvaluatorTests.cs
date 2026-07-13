using NUnit.Framework;
using Sorolla.Palette.Editor.Greenlight;
using Sorolla.Palette.Editor.UI;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     Truth table for <see cref="GreenlightEvaluator.ComputeVerdict"/> - the aggregation that was
    ///     the false-green: it used to only react to Fail/Warn, so a zero-evidence report (all Wait
    ///     rows, or no rows) rendered HEALTHY/green. These pin the four-state precedence
    ///     FAIL &gt; INCOMPLETE &gt; ISSUES &gt; HEALTHY and the badge mapping that keeps INCOMPLETE
    ///     visibly non-green.
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
            // Info (e.g. device-not-connected, optional-by-design) does not block HEALTHY.
            GreenlightEvaluator.Report report = ReportOf(CheckRow.Status.Pass, CheckRow.Status.Info);
            Assert.AreEqual(GreenlightEvaluator.Verdict.Healthy, report.Verdict);
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
    }
}
