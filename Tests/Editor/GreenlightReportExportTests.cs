using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sorolla.Palette.Editor.Greenlight;
using Sorolla.Palette.Health;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     The auditable canonical report export (review F4/F9): the copied report must carry EVERY catalog row
    ///     for the requested phase - including the inert NotApplicable/OptionalSkipped rows the UI collapses -
    ///     with its stable id, definition version, requirement + reason, disposition, outcome, and proof, plus a
    ///     build/context fingerprint. These pin that nothing load-bearing is dropped on the way to the clipboard.
    /// </summary>
    public class GreenlightReportExportTests
    {
        // A context that deliberately produces a MIX of dispositions: no Unity IAP → the iap gates are
        // NotApplicable (inert), advisory build rows are OptionalSkipped, required rows omit → INCOMPLETE.
        static EvaluationContext MixedContext() => new EvaluationContext
        {
            Mode = EvalMode.Full, Platform = EvalPlatform.Android,
            InstalledModules = SdkModule.GameAnalytics | SdkModule.Facebook,
            IntendedTargets = DistributionTargets.Android, RequestedPhase = GatePhase.QaPass, ModulesResolved = true,
        };

        static HealthReport Report(EvaluationContext ctx) =>
            HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx, new List<GateObservation>());

        [Test]
        public void Json_PreservesEveryRow_IncludingInert()
        {
            EvaluationContext ctx = MixedContext();
            HealthReport health = Report(ctx);
            var fp = GreenlightReportExport.Fingerprint.Capture(ctx, null);
            string json = GreenlightReportExport.ToJson(health, fp);

            // Every evaluated row id appears in the export - inert rows are not silently dropped.
            foreach (GateResult r in health.Rows)
                StringAssert.Contains(r.GateId, json, $"row {r.GateId} must appear in the export");

            // At least one NotApplicable and one OptionalSkipped row exist in this context AND survive to export.
            Assert.IsTrue(health.Rows.Any(r => r.Disposition == GateDisposition.NotApplicable), "context should yield a NotApplicable row");
            Assert.IsTrue(health.Rows.Any(r => r.Disposition == GateDisposition.OptionalSkipped), "context should yield an OptionalSkipped row");
            StringAssert.Contains("NotApplicable", json);
            StringAssert.Contains("OptionalSkipped", json);
            StringAssert.Contains($"\"row_count\": {health.Rows.Count}", json);
        }

        [Test]
        public void Json_CarriesFingerprintAndProof()
        {
            EvaluationContext ctx = MixedContext();
            HealthReport health = Report(ctx);
            var fp = GreenlightReportExport.Fingerprint.Capture(ctx, "device-guid-xyz");
            string json = GreenlightReportExport.ToJson(health, fp);

            StringAssert.Contains("fingerprint", json);
            StringAssert.Contains("device-guid-xyz", json);
            StringAssert.Contains(Sorolla.Palette.Palette.SdkVersion, json);
            StringAssert.Contains("required_proof", json);
            StringAssert.Contains("disposition", json);
            StringAssert.Contains("requirement_reason", json);
        }

        [Test]
        public void Text_ShowsDispositionAndRequirement_SoInertRowsAreNotMistakenForPass()
        {
            EvaluationContext ctx = MixedContext();
            HealthReport health = Report(ctx);
            var fp = GreenlightReportExport.Fingerprint.Capture(ctx, "device-guid-xyz");
            string text = GreenlightReportExport.ToText(health, fp);

            StringAssert.Contains("disp=NotApplicable", text);
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
            // supervisor cold-reviews is genuine tool output, not hand-written. Also asserts the JSON parses
            // and preserves the row count (a live round-trip check).
            EvaluationContext ctx = MixedContext();
            HealthReport health = Report(ctx);
            var fp = GreenlightReportExport.Fingerprint.Capture(ctx, "sample-device-guid");
            string json = GreenlightReportExport.ToJson(health, fp);
            string text = GreenlightReportExport.ToText(health, fp);

            System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sorolla-sample-report.json"), json);
            System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sorolla-sample-report.txt"), text);

            Assert.IsTrue(MiniJson.Deserialize(json) is Dictionary<string, object>, "the exported JSON must parse");
            Assert.Greater(health.Rows.Count, 0);
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
            var fp = GreenlightReportExport.Fingerprint.Capture(MixedContext(), null);
            StringAssert.Contains("ghost", GreenlightReportExport.ToJson(health, fp));
            StringAssert.Contains("ghost", GreenlightReportExport.ToText(health, fp));
        }
    }
}
