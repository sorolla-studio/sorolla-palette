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

            // 1. Index observations by gate id. Unknown ids and duplicates are validation errors - never
            //    ignored, never last-write-wins.
            var byId = new Dictionary<string, List<GateObservation>>();
            foreach (GateObservation obs in observations ?? Array.Empty<GateObservation>())
            {
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

            // 2. Definitions drive the report, not the observations - that is what makes omission detectable.
            foreach (GateDefinition def in catalog.All)
            {
                ApplicabilityVerdict av = def.Applicability != null
                    ? def.Applicability(context)
                    : new ApplicabilityVerdict(Applicability.Unknown, "No applicability predicate defined");

                var row = new GateResult
                {
                    GateId = def.Id,
                    DefinitionVersion = def.Version,
                    Applicability = av.Value,
                    ApplicabilityReason = av.Reason,
                    RequiredProof = def.RequiredProof,
                };

                if (av.Value == Applicability.Unknown)
                {
                    // Could not decide applicability from trusted context - must not silently pass.
                    row.Outcome = GateOutcome.Incomplete;
                    rows.Add(row);
                    continue;
                }

                if (av.Value == Applicability.NotApplicable)
                {
                    // Excluded from aggregation; carries its reason. Outcome is inert for excluded rows.
                    row.Outcome = GateOutcome.Pass;
                    rows.Add(row);
                    continue;
                }

                // Applicable.
                byId.TryGetValue(def.Id, out List<GateObservation> matches);
                int count = matches?.Count ?? 0;

                if (count == 0)
                {
                    if (def.Required)
                    {
                        // The omission case (R3-01): a required gate with no evidence is not a pass.
                        row.Outcome = GateOutcome.Incomplete;
                    }
                    else
                    {
                        // Optional + unobserved is a skip, not evidence: exclude it so it can neither block
                        // nor become affirmative.
                        row.Applicability = Applicability.NotApplicable;
                        row.ApplicabilityReason = "Optional gate, no observation supplied";
                        row.Outcome = GateOutcome.Pass;
                    }
                    rows.Add(row);
                    continue;
                }

                if (count > 1)
                {
                    // Duplicate already recorded as a validation error; the gate itself is unresolved.
                    row.Outcome = GateOutcome.Incomplete;
                    rows.Add(row);
                    continue;
                }

                GateObservation only = matches[0];
                row.ObservedProof = only.ObservedProof;
                row.Evidence = only.Evidence;
                row.FixHint = only.FixHint;

                // Proof gate (R3-05): missing any required proof class → INCOMPLETE, regardless of the
                // observed outcome. This is an aggregation rule, not a producer courtesy.
                ProofScope missing = def.RequiredProof & ~only.ObservedProof;
                row.Outcome = missing != ProofScope.None ? GateOutcome.Incomplete : only.Outcome;
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
        ///     Precedence FAIL &gt; INCOMPLETE &gt; PASS_WITH_CAVEATS &gt; PASS, plus the no-affirmative-
        ///     evidence floor (no Pass/PassWithCaveats among considered rows → INCOMPLETE) that stops the
        ///     false-green. NotApplicable rows are excluded; Unknown rows (Outcome == Incomplete) are
        ///     included so an unresolved applicability cannot be silently dropped.
        /// </summary>
        static GateOutcome Aggregate(IReadOnlyList<GateResult> rows, bool anyValidationError)
        {
            List<GateResult> considered =
                rows.Where(r => r.Applicability != Applicability.NotApplicable).ToList();

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
