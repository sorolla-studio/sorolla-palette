using System.Collections.Generic;
using NUnit.Framework;
using Sorolla.Palette.Health;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     Truth table for the ONE shared aggregation (<see cref="HealthEvaluator.Evaluate"/>) on the repaired
    ///     model: the context-derived 4-state requirement;
    ///     every definition evaluated (no phase selection); OptionalSkipped ≠ NotApplicable; contradictory NotApplicable
    ///     observations are validation errors; boundary validation of corrupted enum/flag values; the
    ///     FAIL &gt; INCOMPLETE &gt; CAVEATS &gt; PASS precedence and the no-affirmative floor; plus the strict
    ///     catalog validation.
    /// </summary>
    public class HealthEvaluatorTests
    {
        static EvaluationContext Ctx() => new EvaluationContext
        {
            Mode = EvalMode.Full,
            Platform = EvalPlatform.Android,
            InstalledModules = SdkModule.GameAnalytics,
        };

        static GateDefinition Def(string id, Requirement requirement = Requirement.Required) =>
            new GateDefinition(id, _ => new RequirementDecision(requirement, "test reason"));

        static GateDefinition DefReq(string id, System.Func<EvaluationContext, RequirementDecision> req) =>
            new GateDefinition(id, req);

        static GateObservation Obs(string id, GateOutcome outcome) =>
            new GateObservation { GateId = id, Outcome = outcome };

        static GateCatalog Catalog(params GateDefinition[] defs) => new GateCatalog(defs);

        static GateResult Row(HealthReport r, string id)
        {
            foreach (GateResult row in r.Rows)
                if (row.GateId == id) return row;
            return null;
        }

        // ── Omission / duplicates / unknown ids ───────────────────────────

        [Test]
        public void RequiredGateWithNoObservation_IsIncomplete()
        {
            HealthReport r = HealthEvaluator.Evaluate(Catalog(Def("a")), Ctx(), new List<GateObservation>());
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
        }

        [Test]
        public void OmittedRequiredGate_HasOmittedDisposition_AndParticipatesInAggregation()
        {
            // The omission hole: a required gate with no observation must be a distinct Omitted disposition
            // that still resolves to INCOMPLETE and is NOT excluded from aggregation alongside a real Pass.
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Def("missing"), Def("ok")), Ctx(),
                new List<GateObservation> { Obs("ok", GateOutcome.Pass) });

            GateResult omitted = Row(r, "missing");
            Assert.AreEqual(GateDisposition.Omitted, omitted.Disposition);
            Assert.AreEqual(GateOutcome.Incomplete, omitted.Outcome);
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome, "an omitted required gate must not be aggregated away by a sibling Pass");
        }

        [Test]
        public void UnknownObservationId_IsValidationErrorAndIncomplete()
        {
            HealthReport r = HealthEvaluator.Evaluate(Catalog(Def("a")), Ctx(),
                new List<GateObservation> { Obs("a", GateOutcome.Pass), Obs("ghost", GateOutcome.Pass) });
            Assert.IsNotEmpty(r.ValidationErrors);
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
        }

        [Test]
        public void DuplicateObservations_AreValidationErrorAndGateIncomplete()
        {
            HealthReport r = HealthEvaluator.Evaluate(Catalog(Def("a")), Ctx(),
                new List<GateObservation> { Obs("a", GateOutcome.Pass), Obs("a", GateOutcome.Pass) });
            Assert.IsNotEmpty(r.ValidationErrors);
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
        }

        // ── Requirement 4-state ───────────────────────────────────

        [Test]
        public void RequirementUnknown_IsIncomplete()
        {
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Def("a", Requirement.Unknown)), Ctx(),
                new List<GateObservation> { Obs("a", GateOutcome.Pass) });
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
        }

        [Test]
        public void NotApplicableGate_IsExcluded_SolePassElsewhereWins()
        {
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Def("na", Requirement.NotApplicable), Def("ok")), Ctx(),
                new List<GateObservation> { Obs("ok", GateOutcome.Pass) });
            Assert.AreEqual(GateOutcome.Pass, r.Outcome);
            Assert.AreEqual(GateDisposition.NotApplicable, Row(r, "na").Disposition);
        }

        [Test]
        public void NotApplicableOnly_HasNoAffirmativeEvidence_IsIncomplete()
        {
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Def("na", Requirement.NotApplicable)), Ctx(), new List<GateObservation>());
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
        }

        // ── optional-unobserved is OptionalSkipped, not NotApplicable ─

        [Test]
        public void OptionalUnobserved_IsOptionalSkipped_NotNotApplicable_DoesNotBlockPass()
        {
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Def("opt", Requirement.Optional), Def("req")), Ctx(),
                new List<GateObservation> { Obs("req", GateOutcome.Pass) });

            Assert.AreEqual(GateOutcome.Pass, r.Outcome);
            GateResult opt = Row(r, "opt");
            Assert.AreEqual(GateDisposition.OptionalSkipped, opt.Disposition);
            Assert.AreEqual(Requirement.Optional, opt.Requirement, "must NOT be rewritten to NotApplicable");
        }

        [Test]
        public void OptionalObserved_IsEvaluated_AndCanFail()
        {
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Def("opt", Requirement.Optional)), Ctx(),
                new List<GateObservation> { Obs("opt", GateOutcome.Fail) });
            Assert.AreEqual(GateOutcome.Fail, r.Outcome);
        }

        // ── observation for a NotApplicable gate is a context mismatch ─

        [Test]
        public void ObservationForNotApplicableGate_IsValidationErrorAndAtLeastIncomplete()
        {
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Def("na", Requirement.NotApplicable), Def("ok")), Ctx(),
                new List<GateObservation> { Obs("na", GateOutcome.Pass), Obs("ok", GateOutcome.Pass) });

            Assert.IsNotEmpty(r.ValidationErrors);
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome, "stale/wrong-context evidence must not be silently dropped");
        }

        // ── Precedence + floor ────────────────────────────────────────────

        [Test]
        public void FailOutranksIncomplete()
        {
            HealthReport r = HealthEvaluator.Evaluate(Catalog(Def("a"), Def("b")), Ctx(),
                new List<GateObservation> { Obs("a", GateOutcome.Fail) });
            Assert.AreEqual(GateOutcome.Fail, r.Outcome);
        }

        [Test]
        public void IncompleteOutranksCaveats()
        {
            HealthReport r = HealthEvaluator.Evaluate(Catalog(Def("a"), Def("b")), Ctx(),
                new List<GateObservation> { Obs("a", GateOutcome.PassWithCaveats) });
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
        }

        [Test]
        public void CaveatsOutranksPass()
        {
            HealthReport r = HealthEvaluator.Evaluate(Catalog(Def("a"), Def("b")), Ctx(),
                new List<GateObservation> { Obs("a", GateOutcome.Pass), Obs("b", GateOutcome.PassWithCaveats) });
            Assert.AreEqual(GateOutcome.PassWithCaveats, r.Outcome);
        }

        [Test]
        public void AllPass_IsPass()
        {
            HealthReport r = HealthEvaluator.Evaluate(Catalog(Def("a"), Def("b")), Ctx(),
                new List<GateObservation> { Obs("a", GateOutcome.Pass), Obs("b", GateOutcome.Pass) });
            Assert.AreEqual(GateOutcome.Pass, r.Outcome);
        }

        [Test]
        public void EmptyCatalog_NoObservations_IsIncompleteByNoAffirmativeFloor()
        {
            HealthReport r = HealthEvaluator.Evaluate(GateCatalog.Canonical, Ctx(), new List<GateObservation>());
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
        }

        // ── Every gate is evaluated: no phase/audience selection ──

        [Test]
        public void EveryDefinitionIsEvaluated_NoPhaseSelection()
        {
            // The phase axis is gone: what a report CONTAINS never depends on who asked for it. Both gates
            // resolve - 'a' from its observation, 'b' omitted-required - and neither is filtered out.
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Def("a", Requirement.Required), Def("b")),
                Ctx(),
                new List<GateObservation> { Obs("a", GateOutcome.Pass) });

            Assert.IsNotNull(Row(r, "a"));
            Assert.IsNotNull(Row(r, "b"));
            Assert.AreEqual(GateDisposition.Omitted, Row(r, "b").Disposition);
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
        }

        [Test]
        public void UnresolvedModules_IsValidationErrorAndIncomplete()
        {
            // Unknown manifest state must be INCOMPLETE, never treated as an empty (absent) module set.
            var ctx = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.Android, InstalledModules = SdkModule.None,
                ModulesResolved = false,
            };
            HealthReport r = HealthEvaluator.Evaluate(Catalog(Def("a")), ctx,
                new List<GateObservation> { Obs("a", GateOutcome.Pass) });
            Assert.IsNotEmpty(r.ValidationErrors);
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
        }

        // ── Strict catalog validation ─────────────────────────────────────

        static IReadOnlyList<EvaluationContext> Grid => GateCatalog.SupportedContexts;

        [Test]
        public void Validate_Canonical_IsClean()
        {
            Assert.IsEmpty(GateCatalog.Validate(GateCatalog.Canonical.All, Grid));
        }

        [Test]
        public void Validate_DuplicateIds_IsProblem()
        {
            Assert.IsNotEmpty(GateCatalog.Validate(new[] { Def("dup"), Def("dup") }, Grid));
        }

        [Test]
        public void Validate_NullDefinition_IsProblem()
        {
            Assert.IsNotEmpty(GateCatalog.Validate(new GateDefinition[] { null }, Grid));
        }

        [Test]
        public void Validate_EmptyId_IsProblem()
        {
            Assert.IsNotEmpty(GateCatalog.Validate(new[] { Def("  ") }, Grid));
        }

        [Test]
        public void Validate_MissingRequirementPredicate_IsProblem()
        {
            var def = new GateDefinition("a", null);
            Assert.IsNotEmpty(GateCatalog.Validate(new[] { def }, Grid));
        }

        [Test]
        public void Validate_NotApplicableWithoutReason_IsProblem()
        {
            var def = DefReq("a", _ => new RequirementDecision(Requirement.NotApplicable, null));
            Assert.IsNotEmpty(GateCatalog.Validate(new[] { def }, Grid));
        }

        [Test]
        public void Validate_NeverApplicable_IsProblem()
        {
            var def = DefReq("a", _ => new RequirementDecision(Requirement.NotApplicable, "always n/a"));
            Assert.IsNotEmpty(GateCatalog.Validate(new[] { def }, Grid));
        }

        [Test]
        public void Validate_NonExhaustiveGrid_IsProblem()
        {
            var partialGrid = new List<EvaluationContext>
            {
                new EvaluationContext
                {
                    Mode = EvalMode.Full, Platform = EvalPlatform.Android,

                },
            };
            Assert.IsNotEmpty(GateCatalog.Validate(new[] { Def("a") }, partialGrid));
        }

        [Test]
        public void ById_UnknownId_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() => Catalog(Def("a")).ById("missing"));
        }

        // ── Unknown requirement must not weaken an observed FAIL ────

        [Test]
        public void UnknownRequirement_WithObservedFail_StaysFail_AndKeepsEvidence()
        {
            var obs = new GateObservation
            {
                GateId = "a", Outcome = GateOutcome.Fail,
                Evidence = "boom", FixHint = "fix",
            };
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Def("a", Requirement.Unknown)), Ctx(), new List<GateObservation> { obs });

            GateResult row = Row(r, "a");
            Assert.AreEqual(GateOutcome.Fail, row.Outcome, "an Unknown requirement must not flatten an observed FAIL");
            Assert.AreEqual("boom", row.Evidence);
            Assert.AreEqual(GateOutcome.Fail, r.Outcome);
        }

        // ── malformed boundary inputs produce INCOMPLETE, never throw ─

        [Test]
        public void NullCatalog_IsIncomplete_NotThrow()
        {
            HealthReport r = HealthEvaluator.Evaluate(null, Ctx(), new List<GateObservation>());
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
            Assert.IsNotEmpty(r.ValidationErrors);
        }

        [Test]
        public void NullContext_IsIncomplete_NotThrow()
        {
            HealthReport r = HealthEvaluator.Evaluate(Catalog(Def("a")), null, new List<GateObservation>());
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
            Assert.IsNotEmpty(r.ValidationErrors);
        }

        [Test]
        public void NullGateIdObservation_IsValidationError_NotThrow()
        {
            var obs = new GateObservation { GateId = null, Outcome = GateOutcome.Pass };
            HealthReport r = HealthEvaluator.Evaluate(Catalog(Def("a")), Ctx(), new List<GateObservation> { obs });
            Assert.IsNotEmpty(r.ValidationErrors);
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
        }

        [Test]
        public void InvalidOutcome_RowIsIncomplete_NotInvalid()
        {
            var obs = new GateObservation { GateId = "a", Outcome = (GateOutcome)999 };
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Def("a", Requirement.Required)), Ctx(), new List<GateObservation> { obs });
            Assert.AreEqual(GateOutcome.Incomplete, Row(r, "a").Outcome, "an invalid outcome must be coerced, not passed to the UI mapper");
        }

        // ── Reason mandatory for all four states ──────────────────────────

        [Test]
        public void Validate_AnyStateWithoutReason_IsProblem()
        {
            foreach (Requirement req in new[]
            {
                Requirement.Required, Requirement.Optional, Requirement.NotApplicable, Requirement.Unknown,
            })
            {
                var def = DefReq("a", _ => new RequirementDecision(req, null));
                Assert.IsNotEmpty(GateCatalog.Validate(new[] { def }, Grid), $"{req} without a reason must fail Validate.");
            }
        }

        // ── Module-keyed gate: installed + no observation → INCOMPLETE ────

        [Test]
        public void ModuleKeyedRequirement_InstalledButUnobserved_IsOmittedIncomplete()
        {
            // A gate whose applicability keys on an installed module (the shape the Adjust-in-Full and
            // Firebase gates use): installed and unobserved must omit → INCOMPLETE, never pass on silence.
            var def = DefReq("mod", ctx => (ctx.InstalledModules & SdkModule.UnityIap) == 0
                ? new RequirementDecision(Requirement.NotApplicable, "module not installed")
                : new RequirementDecision(Requirement.Required, "module installed"));
            var installed = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.Android,
                InstalledModules = SdkModule.UnityIap,

            };
            HealthReport r = HealthEvaluator.Evaluate(Catalog(def), installed, new List<GateObservation>());
            Assert.AreEqual(GateDisposition.Omitted, Row(r, "mod").Disposition);
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
        }
    }
}
