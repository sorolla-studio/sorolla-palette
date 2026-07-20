using System.Collections.Generic;
using NUnit.Framework;
using Sorolla.Palette.Health;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     Truth table for the ONE shared aggregation (<see cref="HealthEvaluator.Evaluate"/>) on the repaired
    ///     model (review C3-01..C3-07): FAIL survives missing proof; the context-derived 4-state requirement;
    ///     phase selection in the evaluator; OptionalSkipped ≠ NotApplicable; contradictory NotApplicable
    ///     observations are validation errors; boundary validation of corrupted enum/flag values; the
    ///     FAIL &gt; INCOMPLETE &gt; CAVEATS &gt; PASS precedence and the no-affirmative floor; plus the strict
    ///     catalog validation.
    /// </summary>
    public class HealthEvaluatorTests
    {
        static EvaluationContext Ctx(GatePhase phase = GatePhase.QaPass) => new EvaluationContext
        {
            Mode = EvalMode.Full,
            Platform = EvalPlatform.Android,
            InstalledModules = SdkModule.GameAnalytics,
            RequestedPhase = phase,
            Profile = ReportProfile.SorollaFull,
        };

        static GateDefinition Def(
            string id,
            Requirement requirement = Requirement.Required,
            ProofScope proof = ProofScope.None,
            GatePhase phases = GatePhase.QaPass,
            GateClassification classification = GateClassification.Structural)
        {
            return new GateDefinition(id, "1.0.0", classification, phases, proof,
                _ => new RequirementDecision(requirement, "test reason"));
        }

        static GateDefinition DefReq(string id, System.Func<EvaluationContext, RequirementDecision> req,
            ProofScope proof = ProofScope.None, GatePhase phases = GatePhase.QaPass,
            GateClassification classification = GateClassification.Structural) =>
            new GateDefinition(id, "1.0.0", classification, phases, proof, req);

        static GateObservation Obs(string id, GateOutcome outcome, ProofScope proof = ProofScope.Static) =>
            new GateObservation { GateId = id, Outcome = outcome, ObservedProof = proof };

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

        // ── Requirement 4-state (C3-02) ───────────────────────────────────

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

        // ── C3-04: optional-unobserved is OptionalSkipped, not NotApplicable ─

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

        // ── C3-05: observation for a NotApplicable gate is a context mismatch ─

        [Test]
        public void ObservationForNotApplicableGate_IsValidationErrorAndAtLeastIncomplete()
        {
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Def("na", Requirement.NotApplicable), Def("ok")), Ctx(),
                new List<GateObservation> { Obs("na", GateOutcome.Pass), Obs("ok", GateOutcome.Pass) });

            Assert.IsNotEmpty(r.ValidationErrors);
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome, "stale/wrong-context evidence must not be silently dropped");
        }

        // ── C3-01: missing proof must not suppress a known FAIL ────────────

        [Test]
        public void FailWithMissingRequiredProof_StaysFail()
        {
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Def("a", Requirement.Required, ProofScope.VendorAccepted)), Ctx(),
                new List<GateObservation> { Obs("a", GateOutcome.Fail, ProofScope.Static) });
            Assert.AreEqual(GateOutcome.Fail, r.Outcome, "a known FAIL cannot be hidden behind missing extra proof");
        }

        [Test]
        public void AffirmativeWithMissingRequiredProof_DowngradesToIncomplete()
        {
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Def("a", Requirement.Required, ProofScope.DeviceDispatch)), Ctx(),
                new List<GateObservation> { Obs("a", GateOutcome.Pass, ProofScope.Static) });
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
        }

        [Test]
        public void ObservedIncompleteWithMissingProof_StaysIncomplete()
        {
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Def("a", Requirement.Required, ProofScope.DeviceDispatch)), Ctx(),
                new List<GateObservation> { Obs("a", GateOutcome.Incomplete, ProofScope.None) });
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
        }

        [Test]
        public void SatisfiedRequiredProof_KeepsObservedOutcome()
        {
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Def("a", Requirement.Required, ProofScope.DeviceDispatch)), Ctx(),
                new List<GateObservation> { Obs("a", GateOutcome.Pass, ProofScope.Static | ProofScope.DeviceDispatch) });
            Assert.AreEqual(GateOutcome.Pass, r.Outcome);
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

        // ── C3-03: phase selection lives in the evaluator ──────────────────

        [Test]
        public void DefinitionOutsideRequestedPhase_IsExcluded()
        {
            // 'a' is PreBuild-only; requesting QaPass excludes it entirely, leaving only required 'b'.
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Def("a", Requirement.Required, phases: GatePhase.PreBuild), Def("b")),
                Ctx(GatePhase.QaPass),
                new List<GateObservation> { Obs("a", GateOutcome.Pass) });

            Assert.IsNull(Row(r, "a"), "a PreBuild gate must not appear in a QaPass report");
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
        }

        [Test]
        public void UnsupportedRequestedPhase_IsIncomplete()
        {
            HealthReport none = HealthEvaluator.Evaluate(Catalog(Def("a")), Ctx(GatePhase.None),
                new List<GateObservation> { Obs("a", GateOutcome.Pass) });
            Assert.IsNotEmpty(none.ValidationErrors);
            Assert.AreEqual(GateOutcome.Incomplete, none.Outcome);

            HealthReport combined = HealthEvaluator.Evaluate(Catalog(Def("a")),
                Ctx(GatePhase.PreBuild | GatePhase.QaPass),
                new List<GateObservation> { Obs("a", GateOutcome.Pass) });
            Assert.IsNotEmpty(combined.ValidationErrors);
            Assert.AreEqual(GateOutcome.Incomplete, combined.Outcome);
        }

        // ── C3-06: corrupted enum/flag values must not fail open ───────────

        [Test]
        public void InvalidObservationOutcome_IsValidationErrorAndIncomplete_EvenWithAPass()
        {
            HealthReport r = HealthEvaluator.Evaluate(Catalog(Def("a"), Def("b", Requirement.Optional)), Ctx(),
                new List<GateObservation>
                {
                    Obs("a", GateOutcome.Pass),
                    new GateObservation { GateId = "b", Outcome = (GateOutcome)999, ObservedProof = ProofScope.Static },
                });
            Assert.IsNotEmpty(r.ValidationErrors);
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome, "an invalid outcome must not fail open to PASS");
        }

        [Test]
        public void InvalidObservationProofBits_IsValidationError()
        {
            HealthReport r = HealthEvaluator.Evaluate(Catalog(Def("a")), Ctx(),
                new List<GateObservation> { new GateObservation { GateId = "a", Outcome = GateOutcome.Pass, ObservedProof = (ProofScope)0x40 } });
            Assert.IsNotEmpty(r.ValidationErrors);
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
        }

        [Test]
        public void InvalidRequirementValue_IsValidationErrorAndIncomplete()
        {
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(DefReq("a", _ => new RequirementDecision((Requirement)999, "x"))), Ctx(),
                new List<GateObservation> { Obs("a", GateOutcome.Pass) });
            Assert.IsNotEmpty(r.ValidationErrors);
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
        }

        [Test]
        public void UnresolvedModules_IsValidationErrorAndIncomplete()
        {
            // C4-02: unknown manifest state must be INCOMPLETE, never treated as an empty (absent) module set.
            var ctx = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.Android, InstalledModules = SdkModule.None,
                RequestedPhase = GatePhase.QaPass, ModulesResolved = false,
                Profile = ReportProfile.SorollaFull,
            };
            HealthReport r = HealthEvaluator.Evaluate(Catalog(Def("a")), ctx,
                new List<GateObservation> { Obs("a", GateOutcome.Pass) });
            Assert.IsNotEmpty(r.ValidationErrors);
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
        }

        [Test]
        public void InvalidContextMode_IsValidationErrorAndIncomplete()
        {
            var ctx = new EvaluationContext
            {
                Mode = (EvalMode)999, Platform = EvalPlatform.Android,
                InstalledModules = SdkModule.None, RequestedPhase = GatePhase.QaPass,
                Profile = ReportProfile.SorollaFull,
            };
            HealthReport r = HealthEvaluator.Evaluate(Catalog(Def("a")), ctx,
                new List<GateObservation> { Obs("a", GateOutcome.Pass) });
            Assert.IsNotEmpty(r.ValidationErrors);
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
        }

        // ── C3-07: strict catalog validation ──────────────────────────────

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
        public void Validate_EmptyVersion_IsProblem()
        {
            var def = new GateDefinition("a", "", GateClassification.Structural, GatePhase.QaPass, ProofScope.None,
                _ => new RequirementDecision(Requirement.Required));
            Assert.IsNotEmpty(GateCatalog.Validate(new[] { def }, Grid));
        }

        [Test]
        public void Validate_MissingRequirementPredicate_IsProblem()
        {
            var def = new GateDefinition("a", "1", GateClassification.Structural, GatePhase.QaPass, ProofScope.None, null);
            Assert.IsNotEmpty(GateCatalog.Validate(new[] { def }, Grid));
        }

        [Test]
        public void Validate_NoPhase_IsProblem()
        {
            Assert.IsNotEmpty(GateCatalog.Validate(new[] { Def("a", phases: GatePhase.None) }, Grid));
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
                    Mode = EvalMode.Full, Platform = EvalPlatform.Android, RequestedPhase = GatePhase.QaPass,
                    Profile = ReportProfile.SorollaFull,
                },
            };
            Assert.IsNotEmpty(GateCatalog.Validate(new[] { Def("a") }, partialGrid));
        }

        [Test]
        public void ById_UnknownId_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() => Catalog(Def("a")).ById("missing"));
        }

        // ── F4-06: Unknown requirement must not weaken an observed FAIL ────

        [Test]
        public void UnknownRequirement_WithObservedFail_StaysFail_AndKeepsEvidence()
        {
            var obs = new GateObservation
            {
                GateId = "a", Outcome = GateOutcome.Fail, ObservedProof = ProofScope.Static,
                Evidence = "boom", FixHint = "fix",
            };
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Def("a", Requirement.Unknown)), Ctx(), new List<GateObservation> { obs });

            GateResult row = Row(r, "a");
            Assert.AreEqual(GateOutcome.Fail, row.Outcome, "an Unknown requirement must not flatten an observed FAIL");
            Assert.AreEqual("boom", row.Evidence);
            Assert.AreEqual(GateOutcome.Fail, r.Outcome);
        }

        // ── F4-03: malformed boundary inputs produce INCOMPLETE, never throw ─

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
            var obs = new GateObservation { GateId = null, Outcome = GateOutcome.Pass, ObservedProof = ProofScope.Static };
            HealthReport r = HealthEvaluator.Evaluate(Catalog(Def("a")), Ctx(), new List<GateObservation> { obs });
            Assert.IsNotEmpty(r.ValidationErrors);
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
        }

        [Test]
        public void InvalidOutcomeWithValidProof_RowIsIncomplete_NotInvalid()
        {
            var obs = new GateObservation { GateId = "a", Outcome = (GateOutcome)999, ObservedProof = ProofScope.Static };
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Def("a", Requirement.Required, ProofScope.Static)), Ctx(), new List<GateObservation> { obs });
            Assert.AreEqual(GateOutcome.Incomplete, Row(r, "a").Outcome, "an invalid outcome must be coerced, not passed to the UI mapper");
        }

        // ── F4-04: reason mandatory for all four states ───────────────────

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

        // ── IAP gate: installed + no proof → INCOMPLETE by evaluator policy ─

        [Test]
        public void IapModuleRequirement_InstalledButUnobserved_IsOmittedIncomplete()
        {
            var def = DefReq("iap", Requirements.IapStoreConfiguredRequirement, ProofScope.VendorAccepted);
            var ctx = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.Android,
                InstalledModules = SdkModule.UnityIap, CommerceTargets = DistributionTargets.Android,
                RequestedPhase = GatePhase.QaPass, Profile = ReportProfile.SorollaFull,
            };
            HealthReport r = HealthEvaluator.Evaluate(Catalog(def), ctx, new List<GateObservation>());
            Assert.AreEqual(GateDisposition.Omitted, Row(r, "iap").Disposition);
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
        }
        // ── Invariant/variant split: the Studio profile + release certificate ─

        static EvaluationContext StudioCtx(SdkCertification certification) => new EvaluationContext
        {
            Mode = EvalMode.Full,
            Platform = EvalPlatform.Android,
            InstalledModules = SdkModule.GameAnalytics,
            RequestedPhase = GatePhase.QaPass,
            Profile = ReportProfile.Studio,
            Certification = certification,
            CertificationEvidence = "tag v9.9.9",
        };

        static GateDefinition Invariant(string id, Requirement requirement = Requirement.Required) =>
            Def(id, requirement, ProofScope.DeviceDispatch, GatePhase.QaPass, GateClassification.Invariant);

        [Test]
        public void StudioCertified_InvariantIsCollapsed_AndOnlyVariantRowsVote()
        {
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Invariant("inv"), Def("var", classification: GateClassification.Variant)),
                StudioCtx(SdkCertification.CertifiedRelease),
                new List<GateObservation> { Obs("var", GateOutcome.Pass) });

            GateResult inv = Row(r, "inv");
            Assert.AreEqual(GateDisposition.CertifiedBySdk, inv.Disposition);
            StringAssert.Contains("Certified by Sorolla release process", inv.Evidence);
            Assert.AreEqual(GateOutcome.Pass, r.Outcome,
                "a certified invariant must not block a report whose studio-owned rows all pass");
        }

        [Test]
        public void StudioCertified_ObservedFailOnInvariant_StillFails()
        {
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Invariant("inv"), Def("var", classification: GateClassification.Variant)),
                StudioCtx(SdkCertification.CertifiedRelease),
                new List<GateObservation>
                {
                    Obs("inv", GateOutcome.Fail, ProofScope.DeviceDispatch),
                    Obs("var", GateOutcome.Pass),
                });

            Assert.AreEqual(GateDisposition.Evaluated, Row(r, "inv").Disposition);
            Assert.AreEqual(GateOutcome.Fail, r.Outcome, "a certificate must never mask an observed FAIL");
        }

        [Test]
        public void StudioUncertified_RequiredInvariant_IsIncomplete_NeverGreen()
        {
            foreach (SdkCertification certification in new[]
            {
                SdkCertification.Uncertified, SdkCertification.Unknown,
            })
            {
                HealthReport r = HealthEvaluator.Evaluate(
                    Catalog(Invariant("inv"), Def("var", classification: GateClassification.Variant)),
                    StudioCtx(certification),
                    new List<GateObservation> { Obs("var", GateOutcome.Pass) });

                GateResult inv = Row(r, "inv");
                Assert.AreEqual(GateDisposition.Omitted, inv.Disposition, certification.ToString());
                StringAssert.Contains("Pin a tagged Palette release", inv.FixHint);
                Assert.AreEqual(GateOutcome.Incomplete, r.Outcome,
                    $"an uncertified SDK pin ({certification}) must never render green");
            }
        }

        [Test]
        public void StudioUncertified_OptionalInvariant_IsOptionalSkipped()
        {
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Invariant("inv", Requirement.Optional), Def("var", classification: GateClassification.Variant)),
                StudioCtx(SdkCertification.Uncertified),
                new List<GateObservation> { Obs("var", GateOutcome.Pass) });

            Assert.AreEqual(GateDisposition.OptionalSkipped, Row(r, "inv").Disposition);
            Assert.AreEqual(GateOutcome.Pass, r.Outcome);
        }

        [Test]
        public void StudioCertified_NotApplicableInvariant_StaysNotApplicable()
        {
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(
                    Def("inv", Requirement.NotApplicable, ProofScope.DeviceDispatch, GatePhase.QaPass,
                        GateClassification.Invariant),
                    Def("var", classification: GateClassification.Variant)),
                StudioCtx(SdkCertification.CertifiedRelease),
                new List<GateObservation> { Obs("var", GateOutcome.Pass) });

            Assert.AreEqual(GateDisposition.NotApplicable, Row(r, "inv").Disposition,
                "a certificate cannot speak for a gate the context excludes");
        }

        [Test]
        public void SorollaFullProfile_IgnoresCertification_AndEvaluatesInvariantsNormally()
        {
            // The regression guarantee: at full depth a certified release changes nothing - the invariant is
            // still required, still unobserved, still INCOMPLETE.
            var ctx = Ctx();
            ctx.Certification = SdkCertification.CertifiedRelease;
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Invariant("inv"), Def("var", classification: GateClassification.Variant)), ctx,
                new List<GateObservation> { Obs("var", GateOutcome.Pass) });

            Assert.AreEqual(GateDisposition.Omitted, Row(r, "inv").Disposition);
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
        }

        [Test]
        public void UndeclaredProfile_IsValidationErrorAndIncomplete()
        {
            var ctx = Ctx();
            ctx.Profile = ReportProfile.Unknown;
            HealthReport r = HealthEvaluator.Evaluate(Catalog(Def("a")), ctx,
                new List<GateObservation> { Obs("a", GateOutcome.Pass) });
            Assert.IsNotEmpty(r.ValidationErrors);
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome, "a report with no declared audience must not render green");
        }

        [Test]
        public void StudioCertified_OnlyInvariants_HasNoAffirmativeEvidence_IsIncomplete()
        {
            // Collapsed rows do not vote, so a report made only of them has proven nothing locally.
            HealthReport r = HealthEvaluator.Evaluate(
                Catalog(Invariant("inv")), StudioCtx(SdkCertification.CertifiedRelease),
                new List<GateObservation>());
            Assert.AreEqual(GateOutcome.Incomplete, r.Outcome);
        }
    }
}
