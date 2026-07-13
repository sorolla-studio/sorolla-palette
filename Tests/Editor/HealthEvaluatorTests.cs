using System.Collections.Generic;
using NUnit.Framework;
using Sorolla.Palette.Health;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     Truth table for the ONE shared aggregation (<see cref="HealthEvaluator.Evaluate"/>): omission of
    ///     a required gate, unknown/duplicate observations, tri-state applicability, the required-vs-observed
    ///     proof gate, the FAIL &gt; INCOMPLETE &gt; CAVEATS &gt; PASS precedence, and the no-affirmative
    ///     floor. Plus the catalog validation. The live Greenlight keeps its interim ComputeVerdict for one
    ///     cycle; its end-to-end coverage lives in GreenlightEvaluatorTests (untouched here).
    /// </summary>
    public class HealthEvaluatorTests
    {
        static EvaluationContext Ctx() => new EvaluationContext
        {
            Mode = EvalMode.Full,
            Platform = EvalPlatform.Android,
            InstalledModules = SdkModule.GameAnalytics,
        };

        static GateDefinition Def(
            string id,
            bool required = true,
            ProofScope proof = ProofScope.None,
            Applicability applicability = Applicability.Applicable)
        {
            return new GateDefinition
            {
                Id = id,
                Version = "1.0.0",
                Phases = GatePhase.PreBuild,
                Required = required,
                RequiredProof = proof,
                Applicability = _ => new ApplicabilityVerdict(
                    applicability, applicability == Applicability.Applicable ? null : "test reason"),
            };
        }

        static GateObservation Obs(string id, GateOutcome outcome, ProofScope proof = ProofScope.Static)
        {
            return new GateObservation { GateId = id, Outcome = outcome, ObservedProof = proof };
        }

        static GateCatalog Catalog(params GateDefinition[] defs) => new GateCatalog(defs);

        [Test]
        public void RequiredGateWithNoObservation_IsIncomplete()
        {
            HealthReport report = HealthEvaluator.Evaluate(Catalog(Def("a")), Ctx(), new List<GateObservation>());
            Assert.AreEqual(GateOutcome.Incomplete, report.Outcome);
        }

        [Test]
        public void UnknownObservationId_IsValidationErrorAndIncomplete()
        {
            HealthReport report = HealthEvaluator.Evaluate(
                Catalog(Def("a")), Ctx(),
                new List<GateObservation> { Obs("a", GateOutcome.Pass), Obs("ghost", GateOutcome.Pass) });

            Assert.IsNotEmpty(report.ValidationErrors);
            Assert.AreEqual(GateOutcome.Incomplete, report.Outcome);
        }

        [Test]
        public void DuplicateObservations_AreValidationErrorAndGateIncomplete()
        {
            HealthReport report = HealthEvaluator.Evaluate(
                Catalog(Def("a")), Ctx(),
                new List<GateObservation> { Obs("a", GateOutcome.Pass), Obs("a", GateOutcome.Pass) });

            Assert.IsNotEmpty(report.ValidationErrors);
            Assert.AreEqual(GateOutcome.Incomplete, report.Outcome);
        }

        [Test]
        public void ApplicabilityUnknown_IsIncomplete()
        {
            HealthReport report = HealthEvaluator.Evaluate(
                Catalog(Def("a", applicability: Applicability.Unknown)), Ctx(),
                new List<GateObservation> { Obs("a", GateOutcome.Pass) });

            Assert.AreEqual(GateOutcome.Incomplete, report.Outcome);
        }

        [Test]
        public void NotApplicableGate_IsExcluded_SolePassElsewhereWins()
        {
            HealthReport report = HealthEvaluator.Evaluate(
                Catalog(Def("na", applicability: Applicability.NotApplicable), Def("ok")), Ctx(),
                new List<GateObservation> { Obs("ok", GateOutcome.Pass) });

            Assert.AreEqual(GateOutcome.Pass, report.Outcome);
        }

        [Test]
        public void NotApplicableOnly_HasNoAffirmativeEvidence_IsIncomplete()
        {
            HealthReport report = HealthEvaluator.Evaluate(
                Catalog(Def("na", applicability: Applicability.NotApplicable)), Ctx(),
                new List<GateObservation>());

            Assert.AreEqual(GateOutcome.Incomplete, report.Outcome);
        }

        [Test]
        public void MissingRequiredProof_IsIncomplete()
        {
            HealthReport report = HealthEvaluator.Evaluate(
                Catalog(Def("a", proof: ProofScope.DeviceDispatch)), Ctx(),
                new List<GateObservation> { Obs("a", GateOutcome.Pass, ProofScope.Static) });

            Assert.AreEqual(GateOutcome.Incomplete, report.Outcome);
        }

        [Test]
        public void SatisfiedRequiredProof_KeepsObservedOutcome()
        {
            HealthReport report = HealthEvaluator.Evaluate(
                Catalog(Def("a", proof: ProofScope.DeviceDispatch)), Ctx(),
                new List<GateObservation>
                {
                    Obs("a", GateOutcome.Pass, ProofScope.Static | ProofScope.DeviceDispatch),
                });

            Assert.AreEqual(GateOutcome.Pass, report.Outcome);
        }

        [Test]
        public void FailOutranksIncomplete()
        {
            // b is required and omitted (→ Incomplete); a fails. FAIL must win.
            HealthReport report = HealthEvaluator.Evaluate(
                Catalog(Def("a"), Def("b")), Ctx(),
                new List<GateObservation> { Obs("a", GateOutcome.Fail) });

            Assert.AreEqual(GateOutcome.Fail, report.Outcome);
        }

        [Test]
        public void IncompleteOutranksCaveats()
        {
            // b omitted (→ Incomplete); a passes with caveats. INCOMPLETE must win.
            HealthReport report = HealthEvaluator.Evaluate(
                Catalog(Def("a"), Def("b")), Ctx(),
                new List<GateObservation> { Obs("a", GateOutcome.PassWithCaveats) });

            Assert.AreEqual(GateOutcome.Incomplete, report.Outcome);
        }

        [Test]
        public void CaveatsOutranksPass()
        {
            HealthReport report = HealthEvaluator.Evaluate(
                Catalog(Def("a"), Def("b")), Ctx(),
                new List<GateObservation> { Obs("a", GateOutcome.Pass), Obs("b", GateOutcome.PassWithCaveats) });

            Assert.AreEqual(GateOutcome.PassWithCaveats, report.Outcome);
        }

        [Test]
        public void AllPass_IsPass()
        {
            HealthReport report = HealthEvaluator.Evaluate(
                Catalog(Def("a"), Def("b")), Ctx(),
                new List<GateObservation> { Obs("a", GateOutcome.Pass), Obs("b", GateOutcome.Pass) });

            Assert.AreEqual(GateOutcome.Pass, report.Outcome);
        }

        [Test]
        public void EmptyCatalog_NoObservations_IsIncompleteByNoAffirmativeFloor()
        {
            HealthReport report = HealthEvaluator.Evaluate(
                GateCatalog.Canonical, Ctx(), new List<GateObservation>());

            Assert.AreEqual(GateOutcome.Incomplete, report.Outcome);
        }

        [Test]
        public void OptionalUnobservedGate_IsExcluded_DoesNotBlockPass()
        {
            HealthReport report = HealthEvaluator.Evaluate(
                Catalog(Def("opt", required: false), Def("req")), Ctx(),
                new List<GateObservation> { Obs("req", GateOutcome.Pass) });

            Assert.AreEqual(GateOutcome.Pass, report.Outcome);
        }

        // ── GateCatalog.Validate ─────────────────────────────────────────

        [Test]
        public void Validate_CanonicalEmptyCatalog_HasNoProblems()
        {
            IReadOnlyList<string> problems =
                GateCatalog.Validate(GateCatalog.Canonical.All, new List<EvaluationContext> { Ctx() });

            Assert.IsEmpty(problems);
        }

        [Test]
        public void Validate_DuplicateIds_IsProblem()
        {
            IReadOnlyList<string> problems =
                GateCatalog.Validate(new[] { Def("dup"), Def("dup") }, new List<EvaluationContext> { Ctx() });

            Assert.IsNotEmpty(problems);
        }

        [Test]
        public void Validate_UnreachableNoPhase_IsProblem()
        {
            GateDefinition def = Def("x");
            def.Phases = GatePhase.None;

            IReadOnlyList<string> problems =
                GateCatalog.Validate(new[] { def }, new List<EvaluationContext> { Ctx() });

            Assert.IsNotEmpty(problems);
        }

        [Test]
        public void Validate_NeverApplicable_IsProblem()
        {
            IReadOnlyList<string> problems = GateCatalog.Validate(
                new[] { Def("y", applicability: Applicability.NotApplicable) },
                new List<EvaluationContext> { Ctx() });

            Assert.IsNotEmpty(problems);
        }

        [Test]
        public void ById_UnknownId_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() => Catalog(Def("a")).ById("missing"));
        }
    }
}
