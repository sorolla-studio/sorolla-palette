using System;
using System.Collections.Generic;
using System.Linq;

namespace Sorolla.Palette.Health
{
    /// <summary>
    ///     The single aggregation for the SDK health/QA contract. Evaluates the CANONICAL catalog against a
    ///     trusted <see cref="EvaluationContext"/> and the producer's observations - it never trusts the
    ///     observation set alone, so an applicable required gate with no matching observation surfaces as
    ///     INCOMPLETE rather than silently passing (review R3-01). This is the only precedence in
    ///     Sorolla.Health; there is deliberately no list-only overload - an omission-blind entry point is
    ///     exactly the unsafe channel this evaluator exists to close.
    /// </summary>
    internal static class HealthEvaluator
    {
        internal static HealthReport Evaluate(
            GateCatalog catalog, EvaluationContext context, IReadOnlyList<GateObservation> observations)
        {
            var rows = new List<GateResult>();
            var validationErrors = new List<string>();

            // A null catalog is a malformed boundary input, not a crash (review F4-03): a report with a
            // visible integrity error, never an exception.
            if (catalog == null)
            {
                validationErrors.Add("Null gate catalog supplied.");
                return new HealthReport { Rows = rows, ValidationErrors = validationErrors, Outcome = GateOutcome.Incomplete };
            }

            // 0. Boundary-validate the trusted context (review C3-06). A corrupt/undefined context value must
            //    not silently pass; the whole report degrades to INCOMPLETE via a validation error.
            ValidateContext(context, validationErrors);
            // Null-safe: a null context is recorded above; do NOT dereference it (review F4-03).
            bool contextUsable =
                context != null && HealthEnums.IsSinglePhase(context.RequestedPhase); // defined single phase (C3-03)

            // 1. Index observations by gate id, validating each value at the boundary (C3-06). Unknown ids and
            //    duplicates are validation errors - never ignored, never last-write-wins.
            var byId = new Dictionary<string, List<GateObservation>>();
            foreach (GateObservation obs in observations ?? Array.Empty<GateObservation>())
            {
                if (obs == null) { validationErrors.Add("Null observation supplied."); continue; }
                if (obs.GateId == null) { validationErrors.Add("Observation with a null gate id."); continue; }
                if (!HealthEnums.IsDefinedOutcome(obs.Outcome))
                    validationErrors.Add($"Observation for '{obs.GateId}' has an undefined outcome value.");
                if (!HealthEnums.HasOnlyDefinedBits(obs.ObservedProof))
                    validationErrors.Add($"Observation for '{obs.GateId}' has undefined proof-scope bits.");

                if (!byId.TryGetValue(obs.GateId, out List<GateObservation> list))
                {
                    list = new List<GateObservation>();
                    byId[obs.GateId] = list;
                }
                list.Add(obs);
            }

            foreach (KeyValuePair<string, List<GateObservation>> pair in byId)
            {
                if (catalog.ById(pair.Key, throwIfMissing: false) == null)
                    validationErrors.Add($"Unknown gate id in observations: '{pair.Key}'");
                if (pair.Value.Count > 1)
                    validationErrors.Add($"Duplicate observations ({pair.Value.Count}) for gate id: '{pair.Key}'");
            }

            // 2. The evaluator - not the caller - selects the definitions that belong to the requested phase
            //    (review C3-03). An unusable phase means no definitions can be trusted → INCOMPLETE.
            IEnumerable<GateDefinition> selected = contextUsable
                ? catalog.All.Where(d => (d.Phases & context.RequestedPhase) != 0)
                : Array.Empty<GateDefinition>();

            foreach (GateDefinition def in selected)
            {
                RequirementDecision rd = def.Requirement != null
                    ? def.Requirement(context)
                    : new RequirementDecision(Requirement.Unknown, "No requirement predicate defined");

                if (!HealthEnums.IsDefinedRequirement(rd.Value))
                {
                    validationErrors.Add($"Gate '{def.Id}' produced an undefined requirement value.");
                    rd = new RequirementDecision(Requirement.Unknown, "Undefined requirement value");
                }

                var row = new GateResult
                {
                    GateId = def.Id,
                    DefinitionVersion = def.Version,
                    Classification = def.Classification,
                    Requirement = rd.Value,
                    RequirementReason = rd.Reason,
                    RequiredProof = def.RequiredProof,
                };

                byId.TryGetValue(def.Id, out List<GateObservation> matches);
                int count = matches?.Count ?? 0;

                // Certified-invariant branch (2026-07-20 split). An Invariant gate is SDK behavior identical
                // for every game, proven once per tagged release on the reference game, so a STUDIO report does
                // not re-prove it. It still appears as a row (full depth is never lost) - it just stops voting.
                // NotApplicable / Unknown requirement decisions fall through to the normal handling below: a
                // gate the context excludes, or cannot decide, is not something a certificate can speak for.
                if (def.Classification == GateClassification.Invariant &&
                    context.Profile == ReportProfile.Studio &&
                    (rd.Value == Requirement.Required || rd.Value == Requirement.Optional))
                {
                    ResolveCertifiedInvariant(context, rd.Value, matches, row);
                    rows.Add(row);
                    continue;
                }

                switch (rd.Value)
                {
                    case Requirement.Unknown:
                        // Could not decide applicability from trusted context - must not silently pass. But a
                        // real observed FAIL survives even here (review F4-06): FAIL is weakened by nothing but
                        // a louder FAIL, so preserve it and its evidence rather than flattening to INCOMPLETE.
                        row.Disposition = GateDisposition.Evaluated;
                        if (count == 1)
                        {
                            row.ObservedProof = matches[0].ObservedProof;
                            row.Evidence = matches[0].Evidence;
                            row.FixHint = matches[0].FixHint;
                            row.Outcome = matches[0].Outcome == GateOutcome.Fail
                                ? GateOutcome.Fail
                                : GateOutcome.Incomplete;
                        }
                        else
                        {
                            row.Outcome = GateOutcome.Incomplete;
                        }
                        break;

                    case Requirement.NotApplicable:
                        row.Disposition = GateDisposition.NotApplicable;
                        row.Outcome = GateOutcome.Pass; // inert; excluded from aggregation
                        // C3-05: an observation for a NotApplicable gate signals stale/wrong-context evidence.
                        if (count > 0)
                            validationErrors.Add(
                                $"Context mismatch: observation supplied for NotApplicable gate '{def.Id}'.");
                        break;

                    case Requirement.Optional:
                        if (count == 0)
                        {
                            // A real optional skip (C3-04) - NEVER rewritten to NotApplicable, excluded from
                            // affirmative evidence.
                            row.Disposition = GateDisposition.OptionalSkipped;
                            row.Outcome = GateOutcome.Pass; // inert; excluded
                        }
                        else
                        {
                            row.Disposition = GateDisposition.Evaluated;
                            row.Outcome = ResolveObserved(def, matches, row);
                        }
                        break;

                    default: // Required
                        if (count == 0)
                        {
                            // Omission (R3-01): a distinct disposition (not "Evaluated") so the comparison
                            // sheet can tell an unmet-required gate from an assessed one; it still resolves to
                            // INCOMPLETE and participates in aggregation.
                            row.Disposition = GateDisposition.Omitted;
                            row.Outcome = GateOutcome.Incomplete;
                        }
                        else
                        {
                            row.Disposition = GateDisposition.Evaluated;
                            row.Outcome = ResolveObserved(def, matches, row);
                        }
                        break;
                }

                rows.Add(row);
            }

            return new HealthReport
            {
                Rows = rows,
                ValidationErrors = validationErrors,
                Outcome = Aggregate(rows, validationErrors.Count > 0),
            };
        }

        /// <summary>
        ///     Resolves one Invariant gate under the Studio profile. Order matters and is the fail-closed part:
        ///     a locally OBSERVED FAIL always survives (a certificate proves the SDK behaved on the reference
        ///     game, it can never overrule this build misbehaving here); only then may the release certificate
        ///     resolve the row. An uncertified/unknown source gets NO excuse - a required invariant omits →
        ///     INCOMPLETE, which is what mechanically stops a game pinned to the development line from ever
        ///     rendering studio-green.
        /// </summary>
        static void ResolveCertifiedInvariant(
            EvaluationContext context, Requirement requirement, List<GateObservation> matches, GateResult row)
        {
            GateObservation observedFail = matches?.FirstOrDefault(m => m != null && m.Outcome == GateOutcome.Fail);
            if (observedFail != null)
            {
                row.Disposition = GateDisposition.Evaluated;
                row.Outcome = GateOutcome.Fail;
                row.ObservedProof = observedFail.ObservedProof;
                row.Evidence = observedFail.Evidence;
                row.FixHint = observedFail.FixHint;
                return;
            }

            if (context.Certification == SdkCertification.CertifiedRelease)
            {
                // Local affirmative observations are deliberately ignored: the tag IS the proof, and mixing a
                // half-observed local session into a certified row would make the certificate look conditional.
                row.Disposition = GateDisposition.CertifiedBySdk;
                row.Outcome = GateOutcome.Pass; // inert; excluded from aggregation
                row.Evidence = "Certified by Sorolla release process — " +
                               (string.IsNullOrWhiteSpace(context.CertificationEvidence)
                                   ? "tagged release"
                                   : context.CertificationEvidence);
                return;
            }

            if (requirement == Requirement.Required)
            {
                row.Disposition = GateDisposition.Omitted;
                row.Outcome = GateOutcome.Incomplete;
                row.Evidence = context.Certification == SdkCertification.Unknown
                    ? "SDK provenance could not be resolved, so no release certificate applies to this build."
                    : "This build does not use a tagged Palette release, so the SDK invariant certificate does not apply.";
                row.FixHint = "Pin a tagged Palette release in Packages/manifest.json";
                return;
            }

            row.Disposition = GateDisposition.OptionalSkipped;
            row.Outcome = GateOutcome.Pass; // inert; excluded
        }

        static void ValidateContext(EvaluationContext context, List<string> errors)
        {
            if (context == null) { errors.Add("Null evaluation context."); return; }
            if (!HealthEnums.IsDefinedMode(context.Mode))
                errors.Add("Evaluation context has an undefined mode value.");
            if (!HealthEnums.IsDefinedPlatform(context.Platform))
                errors.Add("Evaluation context has an undefined platform value.");
            if (!HealthEnums.HasOnlyDefinedBits(context.InstalledModules))
                errors.Add("Evaluation context has undefined installed-module bits.");
            if (!HealthEnums.IsSinglePhase(context.RequestedPhase))
                errors.Add("Evaluation context requests an unknown/unsupported phase.");
            // An undeclared audience is a boundary error: the evaluator must not guess whether certified
            // invariants may be collapsed. Unknown CERTIFICATION, by contrast, is legal - it behaves as
            // uncertified and only changes the evidence wording.
            if (!HealthEnums.IsDeclaredProfile(context.Profile))
                errors.Add("Evaluation context does not declare a report profile (SorollaFull or Studio).");
            if (!HealthEnums.IsDefinedCertification(context.Certification))
                errors.Add("Evaluation context has an undefined SDK certification value.");
            if (!context.ModulesResolved)
                errors.Add("Installed-module state could not be resolved from the manifest (unknown ≠ absent).");
        }

        /// <summary>
        ///     Resolve a single observation against the definition's required proof (review C3-01). An
        ///     observed FAIL stays FAIL and an observed INCOMPLETE stays INCOMPLETE regardless of missing
        ///     proof - a known-broken requirement is not hidden behind missing extra proof. Missing required
        ///     proof downgrades ONLY affirmative claims (Pass / PassWithCaveats) to INCOMPLETE. A duplicate is
        ///     already a validation error and cannot resolve to a pass.
        /// </summary>
        static GateOutcome ResolveObserved(GateDefinition def, List<GateObservation> matches, GateResult row)
        {
            if (matches.Count > 1)
                return GateOutcome.Incomplete;

            GateObservation only = matches[0];
            row.ObservedProof = only.ObservedProof;
            row.Evidence = only.Evidence;
            row.FixHint = only.FixHint;

            switch (only.Outcome)
            {
                case GateOutcome.Fail:
                    return GateOutcome.Fail;
                case GateOutcome.Incomplete:
                    return GateOutcome.Incomplete;
                case GateOutcome.Pass:
                case GateOutcome.PassWithCaveats:
                    // Affirmative claims need their proof; missing required proof downgrades to INCOMPLETE.
                    ProofScope missing = def.RequiredProof & ~only.ObservedProof;
                    return missing != ProofScope.None ? GateOutcome.Incomplete : only.Outcome;
                default:
                    // An undefined/corrupted outcome (already recorded as a validation error) must never reach
                    // the Editor row mapper as an invalid value (review F4-03) - coerce it to INCOMPLETE.
                    return GateOutcome.Incomplete;
            }
        }

        /// <summary>
        ///     Precedence FAIL &gt; INCOMPLETE &gt; PASS_WITH_CAVEATS &gt; PASS, plus the no-affirmative-
        ///     evidence floor (no Pass/PassWithCaveats among considered rows → INCOMPLETE) that stops the
        ///     false-green. Only <see cref="GateDisposition.Evaluated"/> rows are considered; NotApplicable
        ///     OptionalSkipped and CertifiedBySdk rows are excluded. Any validation error forces at least INCOMPLETE.
        /// </summary>
        static GateOutcome Aggregate(IReadOnlyList<GateResult> rows, bool anyValidationError)
        {
            // Fail closed: aggregate EVERY disposition except the two explicitly-excluded ones. An omitted
            // required row (Incomplete) participates, and an unknown/future disposition is included rather
            // than silently dropped into an excluded default branch (review challenge, point 3).
            List<GateResult> considered = rows.Where(r =>
                r.Disposition != GateDisposition.OptionalSkipped &&
                r.Disposition != GateDisposition.NotApplicable &&
                r.Disposition != GateDisposition.CertifiedBySdk).ToList();

            if (considered.Any(r => r.Outcome == GateOutcome.Fail))
                return GateOutcome.Fail;

            bool anyIncomplete = anyValidationError || considered.Any(r => r.Outcome == GateOutcome.Incomplete);
            bool anyAffirmative = considered.Any(r =>
                r.Outcome == GateOutcome.Pass || r.Outcome == GateOutcome.PassWithCaveats);

            if (anyIncomplete || !anyAffirmative)
                return GateOutcome.Incomplete;

            if (considered.Any(r => r.Outcome == GateOutcome.PassWithCaveats))
                return GateOutcome.PassWithCaveats;

            return GateOutcome.Pass;
        }
    }
}
