using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sorolla.Palette.Editor.Greenlight;
using Sorolla.Palette.Editor.UI;
using Sorolla.Palette.Health;
using UnityEditor;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     Covers the Cycle-4 cutover: the Editor greenlight now routes through the ONE shared
    ///     <see cref="HealthEvaluator.Evaluate"/> via <see cref="GreenlightAdapter"/>. Tests the adapter's
    ///     row-class → observation mapping, the B-10 rule that a ticked legacy checkmark cannot produce an
    ///     affirmative outcome, the display-only mapping of the aggregate outcome (no recomputed precedence),
    ///     and the end-to-end no-evidence → INCOMPLETE non-green invariant carried over from Cycle 2.
    /// </summary>
    [TestFixture]
    public class GreenlightEvaluatorTests
    {
        static EvaluationContext Ctx() => new EvaluationContext
        {
            Mode = EvalMode.Full,
            Platform = EvalPlatform.Android,
            InstalledModules = HealthEnums.AllModuleBits,
            RequestedPhase = GatePhase.QaPass,
        };

        // ── Adapter: context mapping ──────────────────────────────────────

        [Test]
        public void ToEvalMode_MapsEditorModes()
        {
            Assert.AreEqual(EvalMode.Unknown, GreenlightAdapter.ToEvalMode(SorollaMode.None));
            Assert.AreEqual(EvalMode.Prototype, GreenlightAdapter.ToEvalMode(SorollaMode.Prototype));
            Assert.AreEqual(EvalMode.Full, GreenlightAdapter.ToEvalMode(SorollaMode.Full));
        }

        [Test]
        public void ToEvalPlatform_MapsMobileTargets_ElseUnknown()
        {
            Assert.AreEqual(EvalPlatform.Android, GreenlightAdapter.ToEvalPlatform(BuildTarget.Android));
            Assert.AreEqual(EvalPlatform.iOS, GreenlightAdapter.ToEvalPlatform(BuildTarget.iOS));
            Assert.AreEqual(EvalPlatform.Unknown, GreenlightAdapter.ToEvalPlatform(BuildTarget.StandaloneOSX));
        }

        // ── Adapter: Build Health row-class → observation ─────────────────

        [Test]
        public void BuildHealth_StatusMapsToOutcome()
        {
            Assert.AreEqual(GateOutcome.Fail, GreenlightAdapter.ToOutcome(BuildValidator.ValidationStatus.Error));
            Assert.AreEqual(GateOutcome.PassWithCaveats, GreenlightAdapter.ToOutcome(BuildValidator.ValidationStatus.Warning));
            Assert.AreEqual(GateOutcome.Incomplete, GreenlightAdapter.ToOutcome(BuildValidator.ValidationStatus.Unverifiable));
            Assert.AreEqual(GateOutcome.Pass, GreenlightAdapter.ToOutcome(BuildValidator.ValidationStatus.Valid));
        }

        [Test]
        public void BuildHealth_EmitsOneObservationPerCategory_KeyedToGateId()
        {
            var results = new List<BuildValidator.ValidationResult>
            {
                new BuildValidator.ValidationResult(BuildValidator.ValidationStatus.Error,
                    "boom", "fix it", BuildValidator.CheckCategory.FacebookPlatformConfig),
            };

            List<GateObservation> obs = GreenlightAdapter.BuildObservations(
                Ctx(), results, new GreenlightDeviceSnapshot.State(), new GreenlightManualChecklist.State());

            GateObservation fb = obs.Single(o => o.GateId == GateIds.BuildFacebookPlatform);
            Assert.AreEqual(GateOutcome.Fail, fb.Outcome);
            Assert.AreEqual(ProofScope.Static, fb.ObservedProof);
            Assert.AreEqual("fix it", fb.FixHint);
        }

        [Test]
        public void BuildHealth_CollapsesMultipleResultsPerCategory_WorstWins()
        {
            var results = new List<BuildValidator.ValidationResult>
            {
                new BuildValidator.ValidationResult(BuildValidator.ValidationStatus.Valid, "ok", null, BuildValidator.CheckCategory.AndroidManifest),
                new BuildValidator.ValidationResult(BuildValidator.ValidationStatus.Error, "bad", null, BuildValidator.CheckCategory.AndroidManifest),
            };

            List<GateObservation> obs = GreenlightAdapter.BuildObservations(
                Ctx(), results, new GreenlightDeviceSnapshot.State(), new GreenlightManualChecklist.State());

            GateObservation manifest = obs.Single(o => o.GateId == GateIds.BuildAndroidManifest);
            Assert.AreEqual(GateOutcome.Fail, manifest.Outcome);
        }

        [Test]
        public void BuildHealth_NullResults_EmitNoBuildObservations()
        {
            List<GateObservation> obs = GreenlightAdapter.BuildObservations(
                Ctx(), null, new GreenlightDeviceSnapshot.State(), new GreenlightManualChecklist.State());

            Assert.IsFalse(obs.Any(o => o.GateId.StartsWith("build.")),
                "No Build Health results means the required core build gates omit -> INCOMPLETE, not silent pass.");
        }

        [Test]
        public void EveryBuildValidatorCategory_MapsToACanonicalGate()
        {
            // C4-09: no BuildValidator category may silently disappear - every one maps to a canonical gate.
            foreach (BuildValidator.CheckCategory cat in
                     System.Enum.GetValues(typeof(BuildValidator.CheckCategory)))
            {
                var results = new List<BuildValidator.ValidationResult>
                {
                    new BuildValidator.ValidationResult(BuildValidator.ValidationStatus.Valid, "x", null, cat),
                };
                List<GateObservation> obs = GreenlightAdapter.BuildObservations(
                    Ctx(), results, new GreenlightDeviceSnapshot.State(), new GreenlightManualChecklist.State());
                Assert.IsFalse(obs.Any(o => o.GateId.StartsWith("unmapped:")), $"CheckCategory {cat} is unmapped.");
            }
        }

        [Test]
        public void ToOutcome_UndefinedStatus_ThrowsInsteadOfPass()
        {
            // C4-09: an invalid ValidationStatus must fail closed, not map to a silent PASS.
            Assert.Throws<ArgumentOutOfRangeException>(
                () => GreenlightAdapter.ToOutcome((BuildValidator.ValidationStatus)999));
        }

        [Test]
        public void ValidationErrors_AreRenderedAsVisibleRows()
        {
            // C4-07: a contract/schema validation error must produce a visible, non-green row, not just flip
            // the badge silently.
            var health = new HealthReport
            {
                Rows = new List<GateResult>(),
                Outcome = GateOutcome.Incomplete,
                ValidationErrors = new[] { "Unknown gate id in observations: 'ghost'" },
            };
            GreenlightEvaluator.Report report = GreenlightEvaluator.ToReport(health);
            Assert.IsTrue(report.Rows.Any(r => r.Status == CheckRow.Status.Wait && r.Detail.Contains("ghost")),
                "The validation error must be a visible Wait row.");
        }

        // ── Adapter: device row-class → observation ───────────────────────

        [Test]
        public void Device_NotConnected_EmitsIncompleteReadyWithNoProof()
        {
            List<GateObservation> obs = GreenlightDeviceSnapshot.ToObservations(new GreenlightDeviceSnapshot.State());
            GateObservation ready = obs.Single(o => o.GateId == GateIds.DeviceReady);
            Assert.AreEqual(GateOutcome.Incomplete, ready.Outcome);
            Assert.AreEqual(ProofScope.None, ready.ObservedProof);
        }

        // ── B-10: a ticked legacy checkmark is NOT evidence ───────────────

        [Test]
        public void TickedManualCheckmark_ObservesPassButNoScopedProof()
        {
            var state = new GreenlightManualChecklist.State();
            state.Ticked[GreenlightManualChecklist.Item.GaPlatformRegistered] = true;

            List<GateObservation> obs = GreenlightManualChecklist.ToObservations(state);
            GateObservation ga = obs.Single(o => o.GateId == GateIds.ManualGaPlatformRegistered);
            Assert.AreEqual(GateOutcome.Pass, ga.Outcome);
            Assert.AreEqual(ProofScope.None, ga.ObservedProof, "A legacy tick carries no scoped proof.");
        }

        [Test]
        public void TickedManualCheckmark_CannotProduceAffirmativeOutcome()
        {
            // The B-10 invariant end-to-end through the real evaluator: a ticked manual gate reports PASS
            // but with no scoped proof, so the required-proof gate forces its row to INCOMPLETE - it can
            // never become an affirmative PASS.
            var ctx = new EvaluationContext
            {
                Mode = EvalMode.Full,
                Platform = EvalPlatform.Android,
                InstalledModules = HealthEnums.AllModuleBits,
                RequestedPhase = GatePhase.QaPass,
            };
            var observations = new List<GateObservation>
            {
                new GateObservation
                {
                    GateId = GateIds.ManualGaPlatformRegistered,
                    Outcome = GateOutcome.Pass,
                    ObservedProof = ProofScope.None,
                },
            };

            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx, observations);
            GateResult row = report.Rows.Single(r => r.GateId == GateIds.ManualGaPlatformRegistered);

            Assert.AreEqual(GateOutcome.Incomplete, row.Outcome, "Ticked-but-unscoped must resolve to INCOMPLETE.");
            Assert.AreNotEqual(GateOutcome.Pass, report.Outcome);
            Assert.AreNotEqual(GateOutcome.PassWithCaveats, report.Outcome);
        }

        // ── Display mapping (maps the aggregate outcome, never recomputes) ─

        [Test]
        public void VerdictLabel_MapsEveryOutcome()
        {
            Assert.AreEqual("HEALTHY", GreenlightEvaluator.VerdictLabel(GateOutcome.Pass, 0, 0));
            Assert.AreEqual("1 ISSUES", GreenlightEvaluator.VerdictLabel(GateOutcome.PassWithCaveats, 0, 1));
            Assert.AreEqual("INCOMPLETE", GreenlightEvaluator.VerdictLabel(GateOutcome.Incomplete, 0, 0));
            Assert.AreEqual("FAILING", GreenlightEvaluator.VerdictLabel(GateOutcome.Fail, 0, 0));
        }

        [Test]
        public void VerdictLabel_UnknownOutcome_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => GreenlightEvaluator.VerdictLabel((GateOutcome)999, 0, 0));
        }

        [Test]
        public void BadgeSeverity_MapsEveryOutcome_IncompleteIsNonGreen()
        {
            Assert.AreEqual(StatusBadge.Severity.Pass, GreenlightEvaluator.BadgeSeverity(GateOutcome.Pass));
            Assert.AreEqual(StatusBadge.Severity.Advisory, GreenlightEvaluator.BadgeSeverity(GateOutcome.PassWithCaveats));
            Assert.AreEqual(StatusBadge.Severity.Wait, GreenlightEvaluator.BadgeSeverity(GateOutcome.Incomplete));
            Assert.AreEqual(StatusBadge.Severity.Fail, GreenlightEvaluator.BadgeSeverity(GateOutcome.Fail));
        }

        [Test]
        public void BadgeSeverity_UnknownOutcome_ThrowsInsteadOfPass()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => GreenlightEvaluator.BadgeSeverity((GateOutcome)999));
        }

        [Test]
        public void RowSummary_Incomplete_NeverReadsRowsChecked()
        {
            var report = new GreenlightEvaluator.Report { Outcome = GateOutcome.Incomplete, WaitCount = 2 };
            report.Rows.Add(new GreenlightEvaluator.Row { Status = CheckRow.Status.Wait });
            report.Rows.Add(new GreenlightEvaluator.Row { Status = CheckRow.Status.Wait });
            StringAssert.DoesNotContain("rows checked", GreenlightEvaluator.RowSummary(report));
        }

        [Test]
        public void RowSummary_Pass_ReadsRowsChecked()
        {
            var report = new GreenlightEvaluator.Report { Outcome = GateOutcome.Pass, PassCount = 2 };
            report.Rows.Add(new GreenlightEvaluator.Row { Status = CheckRow.Status.Pass });
            report.Rows.Add(new GreenlightEvaluator.Row { Status = CheckRow.Status.Pass });
            Assert.AreEqual("2 rows checked", GreenlightEvaluator.RowSummary(report));
        }

        [Test]
        public void ToPlainText_UnknownOutcome_ThrowsInsteadOfExportingHealthy()
        {
            var report = new GreenlightEvaluator.Report { Outcome = (GateOutcome)999 };
            Assert.Throws<ArgumentOutOfRangeException>(() => GreenlightEvaluator.ToPlainText(report));
        }

        // ── End-to-end: no evidence must never render green ───────────────

        [Test]
        public void Evaluate_RealPathWithNoEvidence_IsIncompleteAndNonGreen()
        {
            // The whole reason the plan exists: no Build Health results, a never-connected device, and no
            // completed manual evidence must land INCOMPLETE and a non-green badge - never HEALTHY - through
            // the real shared evaluator, whatever the ambient editor mode/platform.
            GreenlightEvaluator.Report report = GreenlightEvaluator.Evaluate(
                buildHealthResults: null,
                snapshotState: new GreenlightDeviceSnapshot.State(),
                checklist: new GreenlightManualChecklist.State());

            Assert.AreEqual(GateOutcome.Incomplete, report.Outcome,
                "A report with no run evidence must be INCOMPLETE, not HEALTHY.");
            Assert.Greater(report.WaitCount, 0, "Unrun/unticked required evidence must surface as Wait rows.");
            Assert.AreNotEqual(StatusBadge.Severity.Pass, GreenlightEvaluator.BadgeSeverity(report.Outcome),
                "The badge for a no-evidence report must not be the green Pass pill.");
        }
    }
}
