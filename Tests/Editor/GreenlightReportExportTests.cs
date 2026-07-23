using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sorolla.Palette.Editor.Greenlight;
using Sorolla.Palette.Health;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     The auditable canonical report export (review F4/F9): the copied report must carry EVERY catalog row
    ///     - including the inert NotApplicable/OptionalSkipped rows the UI collapses -
    ///     with its stable id, definition version, requirement + reason, disposition, outcome, and proof, plus a
    ///     build/context fingerprint. These pin that nothing load-bearing is dropped on the way to the clipboard.
    ///     One export format only (the parallel JSON was deleted 2026-07-22 - nothing consumed it).
    /// </summary>
    public class GreenlightReportExportTests
    {
        // A representative context: Full mode on Android, only some vendors installed, so advisory build rows
        // resolve OptionalSkipped (inert) while required rows omit → INCOMPLETE.
        static EvaluationContext MixedContext() => new EvaluationContext
        {
            Mode = EvalMode.Full, Platform = EvalPlatform.Android,
            InstalledModules = SdkModule.GameAnalytics | SdkModule.Facebook,
            ModulesResolved = true,
            Profile = ReportProfile.SorollaFull,
        };

        // Off a mobile build target the device gate and both platform-scoped Firebase config gates are
        // NotApplicable - the sources of that disposition now that the human-attested gates are gone.
        static EvaluationContext OffMobileContext() => new EvaluationContext
        {
            Mode = EvalMode.Full, Platform = EvalPlatform.Unknown,
            InstalledModules = SdkModule.GameAnalytics | SdkModule.Facebook,
            ModulesResolved = true,
            Profile = ReportProfile.SorollaFull,
        };

        static HealthReport Report(EvaluationContext ctx) =>
            HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx, new List<GateObservation>());

        [Test]
        public void Text_PreservesEveryRow_IncludingInert()
        {
            EvaluationContext ctx = OffMobileContext();
            HealthReport health = Report(ctx);
            var fp = GreenlightReportExport.Fingerprint.Capture(null);
            string text = GreenlightReportExport.ToText(health, fp, ctx);

            // Every evaluated row id appears in the export - inert rows are not silently dropped.
            foreach (GateResult r in health.Rows)
                StringAssert.Contains(r.GateId, text, $"row {r.GateId} must appear in the export");

            // Both inert dispositions exist in this context AND survive to the export.
            Assert.IsTrue(health.Rows.Any(r => r.Disposition == GateDisposition.NotApplicable), "context should yield a NotApplicable row");
            Assert.IsTrue(health.Rows.Any(r => r.Disposition == GateDisposition.OptionalSkipped), "context should yield an OptionalSkipped row");
            StringAssert.Contains("disp=NotApplicable", text);
            StringAssert.Contains("disp=OptionalSkipped", text);
        }

        /// <summary>A gate that did not apply to the platform this report judged carries the default Pass
        /// outcome precisely because it never voted. Printing it as [Pass] would turn the audit trail into a
        /// claim the SDK checked something it deliberately skipped, so the line is labelled by its
        /// disposition instead (2026-07-23 platform-scoping pass).</summary>
        [Test]
        public void Text_LabelsNotApplicableRows_NeverAsPass()
        {
            EvaluationContext ctx = OffMobileContext();
            HealthReport health = Report(ctx);
            string text = GreenlightReportExport.ToText(health, GreenlightReportExport.Fingerprint.Capture(null), ctx);

            StringAssert.Contains("[NotApplicable]", text);
            foreach (string line in text.Split('\n'))
                if (line.StartsWith("[Pass]"))
                    StringAssert.DoesNotContain("disp=NotApplicable", line);
        }

        [Test]
        public void Text_CarriesFingerprintAndProof()
        {
            EvaluationContext ctx = MixedContext();
            HealthReport health = Report(ctx);
            var fp = GreenlightReportExport.Fingerprint.Capture("device-guid-xyz");
            string text = GreenlightReportExport.ToText(health, fp, ctx);

            StringAssert.Contains("device-guid-xyz", text);
            StringAssert.Contains(Sorolla.Palette.Palette.SdkVersion, text);
            // B3: assert the resolved commit VALUE reaches the report, not just the "commit" label - the
            // label is present even when the value is empty, which is the failure this guards.
            Assert.IsFalse(string.IsNullOrWhiteSpace(fp.SdkCommit), "the fingerprint must resolve a commit or an explicit unknown");
            StringAssert.Contains($"commit {fp.SdkCommit}", text);            StringAssert.Contains("proof req=", text);
            StringAssert.Contains("disp=", text);
            StringAssert.Contains("reason:", text);
        }

        [Test]
        public void Fingerprint_ResolvesAConcreteSdkCommit()
        {
            // B3: the export must carry the exact source commit (or an honest "unknown"), never imply 4.0.0
            // identifies the build. In this embedded package it resolves to a real hash.
            string commit = SdkProvenance.ResolveSdkCommit();
            Assert.IsFalse(string.IsNullOrEmpty(commit));
            var fp = GreenlightReportExport.Fingerprint.Capture(null);
            Assert.AreEqual(commit, fp.SdkCommit);
        }

        [Test]
        public void Text_ShowsDispositionAndRequirement_SoInertRowsAreNotMistakenForPass()
        {
            EvaluationContext ctx = MixedContext();
            HealthReport health = Report(ctx);
            var fp = GreenlightReportExport.Fingerprint.Capture("device-guid-xyz");
            string text = GreenlightReportExport.ToText(health, fp);

            StringAssert.Contains("disp=OptionalSkipped", text);
            StringAssert.Contains("req=", text);
            StringAssert.Contains("device-guid-xyz", text);
            // every row id renders
            foreach (GateResult r in health.Rows)
                StringAssert.Contains(r.GateId, text);
        }

        [Test]
        public void GeneratesRealSampleFixture_ForColdReview()
        {
            // Emits a REAL canonical export for a representative context to a Temp path so the artifact the
            // supervisor cold-reviews is genuine tool output, not hand-written.
            EvaluationContext ctx = MixedContext();
            HealthReport health = Report(ctx);
            var fp = GreenlightReportExport.Fingerprint.Capture("sample-device-guid");
            string text = GreenlightReportExport.ToText(health, fp, ctx);

            System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sorolla-sample-report.txt"), text);

            Assert.Greater(health.Rows.Count, 0);
            StringAssert.Contains(GreenlightReportExport.Schema, text);
        }

        [Test]
        public void Export_IncludesValidationErrors_AsIntegrityLines()
        {
            var health = new HealthReport
            {
                Rows = new List<GateResult>(),
                Outcome = GateOutcome.Incomplete,
                ValidationErrors = new[] { "Unknown gate id in observations: 'ghost'" },
            };
            var fp = GreenlightReportExport.Fingerprint.Capture(null);
            StringAssert.Contains("ghost", GreenlightReportExport.ToText(health, fp));
        }
    }
}
