using System;
using System.Collections.Generic;
using System.Linq;

namespace Sorolla.Palette.Health
{
    /// <summary>
    ///     The single aggregation for the SDK health/QA contract. Evaluates the CANONICAL catalog against a
    ///     trusted <see cref="EvaluationContext"/> and the producer's observations - it never trusts the
    ///     observation set alone, so an applicable required gate with no matching observation surfaces as
    ///     INCOMPLETE rather than silently passing. This is the only precedence in
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

            // A null catalog is a malformed boundary input, not a crash: a report with a
            // visible integrity error, never an exception.
            if (catalog == null)
            {
                validationErrors.Add("Null gate catalog supplied.");
                return new HealthReport { Rows = rows, ValidationErrors = validationErrors, Outcome = GateOutcome.Incomplete };
            }

            // 0. Boundary-validate the trusted context. A corrupt/undefined context value must
            //    not silently pass; the whole report degrades to INCOMPLETE via a validation error.
            ValidateContext(context, validationErrors);
            // Null-safe: a null context is recorded above; do NOT dereference it.
            bool contextUsable = context != null;

            // 1. Index observations by gate id, validating each value at the boundary. Unknown ids and
            //    duplicates are validation errors - never ignored, never last-write-wins.
            var byId = new Dictionary<string, List<GateObservation>>();
            foreach (GateObservation obs in observations ?? Array.Empty<GateObservation>())
            {
                if (obs == null) { validationErrors.Add("Null observation supplied."); continue; }
                if (obs.GateId == null) { validationErrors.Add("Observation with a null gate id."); continue; }
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

            // 2. Every gate in the catalog is evaluated: there is no phase/audience selection.
            //    Which rows a surface RENDERS is a frontend concern; what the report CONTAINS
            //    never depends on who is asking. An unusable context means no definition can be trusted →
            //    INCOMPLETE.
            IEnumerable<GateDefinition> selected = contextUsable
                ? catalog.All
                : Array.Empty<GateDefinition>();

            foreach (GateDefinition def in selected)
            {
                RequirementDecision rd = def.Requirement != null
                    ? def.Requirement(context)
                    : new RequirementDecision(Requirement.Unknown, "No requirement predicate defined");

                var row = new GateResult
                {
                    GateId = def.Id,
                    Requirement = rd.Value,
                    RequirementReason = rd.Reason,
                };

                byId.TryGetValue(def.Id, out List<GateObservation> matches);
                int count = matches?.Count ?? 0;

                switch (rd.Value)
                {
                    case Requirement.Unknown:
                        // Could not decide applicability from trusted context - must not silently pass. But a
                        // real observed FAIL survives even here: FAIL is weakened by nothing but
                        // a louder FAIL, so preserve it and its evidence rather than flattening to INCOMPLETE.
                        row.Disposition = GateDisposition.Evaluated;
                        if (count == 1)
                        {
                            row.Evidence = matches[0].Evidence;
                            row.FixHint = matches[0].FixHint;
                            row.Informational = matches[0].Informational;
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
                        // An observation for a NotApplicable gate signals stale/wrong-context evidence.
                        if (count > 0)
                            validationErrors.Add(
                                $"Context mismatch: observation supplied for NotApplicable gate '{def.Id}'.");
                        break;

                    case Requirement.Optional:
                        if (count == 0)
                        {
                            // A real optional skip - NEVER rewritten to NotApplicable, excluded from
                            // affirmative evidence.
                            row.Disposition = GateDisposition.OptionalSkipped;
                            row.Outcome = GateOutcome.Pass; // inert; excluded
                        }
                        else
                        {
                            row.Disposition = GateDisposition.Evaluated;
                            row.Outcome = ResolveObserved(matches, row);
                        }
                        break;

                    default: // Required
                        if (count == 0)
                        {
                            // Omission: a distinct disposition (not "Evaluated") so the comparison
                            // sheet can tell an unmet-required gate from an assessed one; it still resolves to
                            // INCOMPLETE and participates in aggregation.
                            row.Disposition = GateDisposition.Omitted;
                            row.Outcome = GateOutcome.Incomplete;
                        }
                        else
                        {
                            row.Disposition = GateDisposition.Evaluated;
                            row.Outcome = ResolveObserved(matches, row);
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

        static void ValidateContext(EvaluationContext context, List<string> errors)
        {
            if (context == null) { errors.Add("Null evaluation context."); return; }
            if (!context.ModulesResolved)
                errors.Add("Installed-module state could not be resolved from the manifest (unknown ≠ absent).");
        }

        static GateOutcome ResolveObserved(List<GateObservation> matches, GateResult row)
        {
            if (matches.Count > 1)
                return GateOutcome.Incomplete;

            GateObservation only = matches[0];
            row.Evidence = only.Evidence;
            row.FixHint = only.FixHint;
            row.Informational = only.Informational;

            switch (only.Outcome)
            {
                case GateOutcome.Fail:
                    return GateOutcome.Fail;
                case GateOutcome.Incomplete:
                    return GateOutcome.Incomplete;
                case GateOutcome.Pass:
                case GateOutcome.PassWithCaveats:
                    return only.Outcome;
                default:
                    // An undefined/corrupted outcome (already recorded as a validation error) must never reach
                    // the Editor row mapper as an invalid value - coerce it to INCOMPLETE.
                    return GateOutcome.Incomplete;
            }
        }

        /// <summary>
        ///     Precedence FAIL &gt; INCOMPLETE &gt; PASS_WITH_CAVEATS &gt; PASS, plus the no-affirmative-
        ///     evidence floor (no Pass/PassWithCaveats among considered rows → INCOMPLETE) that stops the
        ///     false-green. OptionalSkipped and NotApplicable rows are excluded. Any validation error forces
        ///     at least INCOMPLETE.
        /// </summary>
        static GateOutcome Aggregate(IReadOnlyList<GateResult> rows, bool anyValidationError)
        {
            // Fail closed: aggregate EVERY disposition except the two explicitly-excluded ones. An omitted
            // required row (Incomplete) participates, and an unknown/future disposition is included rather
            // than silently dropped into an excluded default branch.
            List<GateResult> considered = rows.Where(r =>
                r.Disposition != GateDisposition.OptionalSkipped &&
                r.Disposition != GateDisposition.NotApplicable).ToList();

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
