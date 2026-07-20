using System.Collections.Generic;
using NUnit.Framework;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     The runtime Vitals verdict: one computation shared by the overlay's Report pane and the QA-bridge
    ///     snapshot. Pins the fail-closed rules - a FAIL outranks everything, and an all-green report over a
    ///     session that exercised nothing is NOT PROVEN, never green - plus the ownership routing that decides
    ///     which section a row lands in (and that an unknown group can never fall off the report).
    /// </summary>
    [TestFixture]
    public class VitalsVerdictTests
    {
        static SorollaDiagnosticRow Row(string group, string name, SorollaDiagnosticSeverity severity) =>
            new SorollaDiagnosticRow(group, name, severity, "detail", SorollaDiagnosticKind.Required);

        static List<SorollaDiagnosticRow> Rows(params SorollaDiagnosticRow[] rows) =>
            new List<SorollaDiagnosticRow>(rows);

        [Test]
        public void Fail_OutranksEverything()
        {
            SorollaVitalsVerdictReport report = SorollaDiagnostics.ComputeVerdict(Rows(
                Row("Config", "a", SorollaDiagnosticSeverity.Fail),
                Row("Config", "b", SorollaDiagnosticSeverity.Pass)));

            Assert.AreEqual(SorollaVitalsVerdict.Failing, report.Verdict);
            Assert.AreEqual(1, report.Fail);
        }

        [Test]
        public void WarnOrWait_IsActionNeeded()
        {
            Assert.AreEqual(SorollaVitalsVerdict.ActionNeeded, SorollaDiagnostics.ComputeVerdict(Rows(
                Row("Config", "a", SorollaDiagnosticSeverity.Warning))).Verdict);
            Assert.AreEqual(SorollaVitalsVerdict.ActionNeeded, SorollaDiagnostics.ComputeVerdict(Rows(
                Row("Config", "a", SorollaDiagnosticSeverity.Waiting))).Verdict);
        }

        [Test]
        public void AllPass_IsGreenOnlyWhenCoverageIsNotThin()
        {
            // The "green only after played" rule, expressed against whatever the ambient session coverage is:
            // all rows passing is necessary but NOT sufficient - thin coverage must resolve to NOT PROVEN.
            SorollaVitalsVerdictReport report = SorollaDiagnostics.ComputeVerdict(Rows(
                Row("Config", "a", SorollaDiagnosticSeverity.Pass),
                Row("Ads", "b", SorollaDiagnosticSeverity.Pass)));

            Assert.AreEqual(0, report.NeedsAttention);
            Assert.AreEqual(
                report.CoverageThin ? SorollaVitalsVerdict.NotProven : SorollaVitalsVerdict.Pass,
                report.Verdict);
        }

        [Test]
        public void NotProven_IsNeverTheGreenWord()
        {
            var notProven = new SorollaVitalsVerdictReport(SorollaVitalsVerdict.NotProven, 0, 0, 0, 4, true);
            Assert.AreEqual("NOT PROVEN", SorollaDiagnostics.VerdictWord(notProven));
            Assert.AreNotEqual("HEALTHY", SorollaDiagnostics.VerdictWord(notProven));
            Assert.AreEqual("not_proven", SorollaDiagnostics.VerdictToken(SorollaVitalsVerdict.NotProven));
        }

        [Test]
        public void VerdictTokens_AreStableAndDistinct()
        {
            var tokens = new HashSet<string>();
            foreach (SorollaVitalsVerdict verdict in new[]
            {
                SorollaVitalsVerdict.Failing, SorollaVitalsVerdict.ActionNeeded,
                SorollaVitalsVerdict.NotProven, SorollaVitalsVerdict.Pass,
            })
                Assert.IsTrue(tokens.Add(SorollaDiagnostics.VerdictToken(verdict)), verdict.ToString());
            Assert.AreEqual(4, tokens.Count);
        }

        [Test]
        public void StudioOwnedGroups_RouteToTheStudio()
        {
            foreach (string group in new[] { "Config", "SDKs", "Firebase", "Consent", "Identity", "Activity", "Ads" })
                Assert.AreEqual(SorollaRowOwner.Studio,
                    SorollaDiagnostics.OwnerOf(Row(group, "x", SorollaDiagnosticSeverity.Fail)), group);
        }

        [Test]
        public void BootIsSorollas_ExceptTheModeRow()
        {
            Assert.AreEqual(SorollaRowOwner.Sorolla,
                SorollaDiagnostics.OwnerOf(Row("Boot", "Palette ready", SorollaDiagnosticSeverity.Fail)));
            Assert.AreEqual(SorollaRowOwner.Studio,
                SorollaDiagnostics.OwnerOf(Row("Boot", "Palette mode", SorollaDiagnosticSeverity.Fail)),
                "the mode row is the studio's SorollaConfig, not SDK bring-up");
        }

        [Test]
        public void UnknownGroup_FailsClosedToSorolla()
        {
            // A row added later must land in "send to Sorolla", never silently vanish from the report.
            Assert.AreEqual(SorollaRowOwner.Sorolla,
                SorollaDiagnostics.OwnerOf(Row("Something New", "x", SorollaDiagnosticSeverity.Fail)));
            Assert.AreEqual(SorollaRowOwner.Sorolla,
                SorollaDiagnostics.OwnerOf(Row("Red flags", "SDK errors", SorollaDiagnosticSeverity.Fail)));
        }
    }
}
