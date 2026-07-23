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
    ///     Covers Editor integration evaluation, Build Health observation mapping, display-only outcome
    ///     mapping, and the separate connected-device Vitals evidence.
    /// </summary>
    [TestFixture]
    public class GreenlightEvaluatorTests
    {
        static EvaluationContext Ctx() => new EvaluationContext
        {
            Mode = EvalMode.Full,
            Platform = EvalPlatform.Android,
            InstalledModules = HealthEnums.AllModuleBits,

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

        /// <summary>ReleaseOnly is read from the catalog and means only "don't log this on a development
        /// build". The keystore qualifies; the checks a studio must act on - sandbox mode, GA keys - do not,
        /// and no check is release-only merely because it sounds release-shaped.</summary>
        [Test]
        public void IsReleaseOnly_OnlyTheKeystore()
        {
            Assert.IsTrue(GreenlightAdapter.IsReleaseOnly(BuildValidator.CheckCategory.AndroidKeystore));
            Assert.IsFalse(GreenlightAdapter.IsReleaseOnly(BuildValidator.CheckCategory.AdjustSandboxMode));
            Assert.IsFalse(GreenlightAdapter.IsReleaseOnly(BuildValidator.CheckCategory.GameAnalyticsSettings));
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
                Ctx(), results);

            GateObservation fb = obs.Single(o => o.GateId == GateIds.BuildFacebookPlatform);
            Assert.AreEqual(GateOutcome.Fail, fb.Outcome);
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
                Ctx(), results);

            GateObservation manifest = obs.Single(o => o.GateId == GateIds.BuildAndroidManifest);
            Assert.AreEqual(GateOutcome.Fail, manifest.Outcome);
        }

        [Test]
        public void BuildHealth_NullResults_EmitNoBuildObservations()
        {
            List<GateObservation> obs = GreenlightAdapter.BuildObservations(
                Ctx(), null);

            Assert.IsFalse(obs.Any(o => o.GateId.StartsWith("build.")),
                "No Build Health results means the required core build gates omit -> INCOMPLETE, not silent pass.");
        }

        [Test]
        public void EveryBuildValidatorCategory_MapsToACanonicalGate()
        {
            // No BuildValidator category may silently disappear - every one maps to a canonical gate.
            foreach (BuildValidator.CheckCategory cat in
                     System.Enum.GetValues(typeof(BuildValidator.CheckCategory)))
            {
                var results = new List<BuildValidator.ValidationResult>
                {
                    new BuildValidator.ValidationResult(BuildValidator.ValidationStatus.Valid, "x", null, cat),
                };
                List<GateObservation> obs = GreenlightAdapter.BuildObservations(
                    Ctx(), results);
                Assert.IsFalse(obs.Any(o => o.GateId.StartsWith("unmapped:")), $"CheckCategory {cat} is unmapped.");
            }
        }

        /// <summary>Platform scoping's lockstep rule, end to end on the CANONICAL catalog: the gate for the
        /// platform this report does not judge must resolve NotApplicable AND receive no observation. Getting
        /// only half of it right is a fail-loud contract error ("Context mismatch") that pins the whole report
        /// at INCOMPLETE - which is the designed alarm, and exactly what a future producer change could
        /// trip. The synthetic version of the mismatch lives in HealthEvaluatorTests; this is the guard that
        /// the SHIPPED validators and the SHIPPED catalog still agree.</summary>
        [Test]
        public void PlatformScopedGate_ForTheOtherPlatform_IsInertAndUnobserved()
        {
            var androidOnlyObservation = new List<GateObservation>
            {
                new GateObservation
                {
                    GateId = GateIds.BuildFirebaseConfigAndroid,
                    Outcome = GateOutcome.Pass,
                    Evidence = "google-services.json matches the Android application id.",
                },
            };

            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, Ctx(), androidOnlyObservation);

            CollectionAssert.IsEmpty(
                report.ValidationErrors.Where(e => e.Contains("Context mismatch")).ToList(),
                "an Android build supplying only the Android config observation must not trip the mismatch guard");
            GateResult iosRow = report.Rows.Single(r => r.GateId == GateIds.BuildFirebaseConfigIos);
            Assert.AreEqual(GateDisposition.NotApplicable, iosRow.Disposition);
        }

        /// <summary>The other half of the same contract: observing the gate that does NOT apply is the error
        /// the evaluator must shout about, rather than quietly grading a platform this build is not for.</summary>
        [Test]
        public void PlatformScopedGate_ObservedForTheWrongPlatform_IsAContractError()
        {
            var wrongPlatformObservation = new List<GateObservation>
            {
                new GateObservation
                {
                    GateId = GateIds.BuildFirebaseConfigIos,
                    Outcome = GateOutcome.Pass,
                },
            };

            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, Ctx(), wrongPlatformObservation);

            Assert.IsTrue(report.ValidationErrors.Any(e => e.Contains("Context mismatch")));
            Assert.AreEqual(GateOutcome.Incomplete, report.Outcome);
        }

        [Test]
        public void ToOutcome_UndefinedStatus_ThrowsInsteadOfPass()
        {
            // An invalid ValidationStatus must fail closed, not map to a silent PASS.
            Assert.Throws<ArgumentOutOfRangeException>(
                () => GreenlightAdapter.ToOutcome((BuildValidator.ValidationStatus)999));
        }

        [Test]
        public void ValidationErrors_AreRenderedAsVisibleRows()
        {
            // A contract/schema validation error must produce a visible, non-green row, not just flip
            // the badge silently.
            var health = new HealthReport
            {
                Rows = new List<GateResult>(),
                Outcome = GateOutcome.Incomplete,
                ValidationErrors = new[] { "Unknown gate id in observations: 'ghost'" },
            };
            GreenlightEvaluator.Report report = GreenlightEvaluator.ToReport(health);
            Assert.IsTrue(report.Rows.Any(r => r.Status == RowStatus.Wait && r.Detail.Contains("ghost")),
                "The validation error must be a visible Wait row.");
        }

        // ── Bare-vendor absence is not an affirmative observation ───

        [Test]
        public void BareVendorAbsence_EmitsNoObservation()
        {
            var results = new List<BuildValidator.ValidationResult>
            {
                new BuildValidator.ValidationResult(BuildValidator.ValidationStatus.Valid,
                    "Firebase not installed (optional in Prototype)", null, BuildValidator.CheckCategory.FirebaseCoherence),
            };
            var protoCtx = new EvaluationContext
            {
                Mode = EvalMode.Prototype, Platform = EvalPlatform.Android,
                InstalledModules = SdkModule.None,

            };
            List<GateObservation> obs = GreenlightAdapter.BuildObservations(
                protoCtx, results);
            Assert.IsFalse(obs.Any(o => o.GateId == GateIds.BuildFirebaseCoherence),
                "vendor absence must not become an affirmative PASS observation.");
        }

        [Test]
        public void PresentVendor_EmitsObservation()
        {
            var results = new List<BuildValidator.ValidationResult>
            {
                new BuildValidator.ValidationResult(BuildValidator.ValidationStatus.Valid,
                    "Firebase modules OK", null, BuildValidator.CheckCategory.FirebaseCoherence),
            };
            var ctx = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.Android,
                InstalledModules = SdkModule.Firebase,

            };
            List<GateObservation> obs = GreenlightAdapter.BuildObservations(
                ctx, results);
            Assert.IsTrue(obs.Any(o => o.GateId == GateIds.BuildFirebaseCoherence),
                "a present vendor's coherence result must be evaluated.");
        }

        [Test]
        public void Prototype_ExcludesFullOnlyVendorObservationEvenWhenPackageIsPresent()
        {
            var results = new List<BuildValidator.ValidationResult>
            {
                new BuildValidator.ValidationResult(
                    BuildValidator.ValidationStatus.Skipped,
                    "Adjust not required",
                    null,
                    BuildValidator.CheckCategory.AdjustSettings),
            };
            var context = new EvaluationContext
            {
                Mode = EvalMode.Prototype,
                Platform = EvalPlatform.Android,
                InstalledModules = SdkModule.Adjust,
            };

            List<GateObservation> observations = GreenlightAdapter.BuildObservations(
                context, results);

            Assert.IsFalse(observations.Any(o => o.GateId == GateIds.BuildAdjustSettings));
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

        // ── End-to-end: no evidence must never render green ───────────────

        [Test]
        public void Evaluate_RealPathWithNoEvidence_IsIncompleteAndNonGreen()
        {
            // No Build Health results must land INCOMPLETE and a non-green badge.
            GreenlightEvaluator.Report report = GreenlightEvaluator.Evaluate(buildHealthResults: null);

            Assert.AreEqual(GateOutcome.Incomplete, report.Outcome,
                "A report with no run evidence must be INCOMPLETE, not HEALTHY.");
            Assert.Greater(report.WaitCount, 0, "Unrun required evidence must surface as Wait rows.");
            Assert.AreNotEqual(StatusBadge.Severity.Pass, GreenlightEvaluator.BadgeSeverity(report.Outcome),
                "The badge for a no-evidence report must not be the green Pass pill.");
        }

        // ── HEALTHY is reachable from complete integration evidence ───────
        [Test]
        public void EveryRequiredGateObservedGreen_ReadsHealthy()
        {
            var ctx = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.Android,
                InstalledModules = HealthEnums.AllModuleBits,
                ModulesResolved = true,

            };

            var observations = GateCatalog.Canonical.All
                .Where(d => d.Requirement(ctx).Value == Requirement.Required)
                .Select(d => new GateObservation
                {
                    GateId = d.Id, Outcome = GateOutcome.Pass,
                    Evidence = "observed",
                })
                .ToList();

            Assert.IsNotEmpty(observations, "the canonical catalog must have required gates to observe");
            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx, observations);
            Assert.AreEqual(GateOutcome.Pass, report.Outcome,
                "with every required gate observed green, the report must be able to read HEALTHY");
            Assert.AreEqual("HEALTHY", GreenlightEvaluator.VerdictLabel(report.Outcome, 0, 0));
        }

        // ── Integration readiness is decidable before a build ─────────────

        static EvaluationContext MobileContext() => new EvaluationContext
        {
            Mode = EvalMode.Full, Platform = EvalPlatform.Android,
            InstalledModules = HealthEnums.AllModuleBits,
            ModulesResolved = true,
        };

        static List<GateObservation> StaticObservationsAllGreen(EvaluationContext ctx) =>
            GateCatalog.Canonical.All
                .Where(d => d.Requirement(ctx).Value == Requirement.Required)
                .Select(d => new GateObservation
                {
                    GateId = d.Id, Outcome = GateOutcome.Pass,
                    Evidence = "observed",
                })
                .ToList();

        [Test]
        public void CleanStaticSetup_IsReadyBeforeAnyDeviceIsConnected()
        {
            // The pre-build contract: everything decidable without running the game is green, so the
            // integration verdict is green - while the device question stays honestly unanswered.
            EvaluationContext ctx = MobileContext();
            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx, StaticObservationsAllGreen(ctx));

            Assert.AreEqual(GateOutcome.Pass, report.Outcome,
                "a statically clean project must be ready before a device exists");
        }

        [Test]
        public void FailingStaticGate_StillFailsIntegrationReadiness()
        {
            EvaluationContext ctx = MobileContext();
            List<GateObservation> observations = StaticObservationsAllGreen(ctx);
            observations[0] = new GateObservation
            {
                GateId = observations[0].GateId, Outcome = GateOutcome.Fail,
                Evidence = "broken",
            };

            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx, observations);

            Assert.AreEqual(GateOutcome.Fail, report.Outcome);
        }

        [Test]
        public void DeviceRows_DoNotEnterTheHeadlineCounts()
        {
            EvaluationContext ctx = MobileContext();
            GreenlightEvaluator.Report report =
                GreenlightEvaluator.ToReport(HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx,
                    StaticObservationsAllGreen(ctx)));

            Assert.AreEqual(0, report.WaitCount,
                "a never-connected device must not read as a pending integration check");
            Assert.IsFalse(report.Rows.Any(r => r.GateId != null && r.GateId.StartsWith("device.")));
        }
    }
}
