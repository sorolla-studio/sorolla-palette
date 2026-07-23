using System;
using System.Collections.Generic;

namespace Sorolla.Palette.Health
{
    /// <summary>
    ///     The neutral SDK health/QA result vocabulary, shared by the Editor greenlight surface and the
    ///     runtime Vitals surface. Lives in the leaf <c>Sorolla.Health</c> assembly (no engine, no editor
    ///     references) so both consumers depend on ONE contract and there is exactly one aggregation
    ///     (<see cref="HealthEvaluator"/>). All types are internal - friend-visible to Sorolla.Runtime,
    ///     Sorolla.Editor, Sorolla.Editor.Tests; never public studio API.
    /// </summary>
    // Row and aggregate outcome share one enum: a report outcome is just the aggregation of gate outcomes.
    internal enum GateOutcome
    {
        Incomplete,
        Fail,
        PassWithCaveats,
        Pass,
    }

    /// <summary>
    ///     The context-derived requirement decision for a gate. ONE four-state decision
    ///     replaces the old fixed <c>bool Required</c> + separate applicability predicate, so a gate can be
    ///     required in one mode/platform and optional in another without duplicating its id.
    ///     <list type="bullet">
    ///         <item><c>Required</c> - must be observed; omission → INCOMPLETE.</item>
    ///         <item><c>Optional</c> - evaluated if observed; unobserved is a real skip (OptionalSkipped),
    ///         not a pass and not a lie about applicability.</item>
    ///         <item><c>NotApplicable</c> - excluded; an observation supplied anyway is a context
    ///         mismatch.</item>
    ///         <item><c>Unknown</c> - the evaluator could not decide from trusted context → INCOMPLETE.</item>
    ///     </list>
    /// </summary>
    internal enum Requirement
    {
        Unknown,
        NotApplicable,
        Optional,
        Required,
    }

    /// <summary>What actually happened to a gate in a report row: it was evaluated against an
    /// observation, a required gate was omitted (no observation), an applicable-but-optional gate was
    /// skipped, or it was excluded as not applicable. Only OptionalSkipped and NotApplicable are excluded from
    /// aggregation; Evaluated and Omitted both participate (an omitted required row still resolves to
    /// INCOMPLETE), and any unknown disposition fails closed by participating.</summary>
    internal enum GateDisposition
    {
        Evaluated,
        Omitted,
        OptionalSkipped,
        NotApplicable,
    }

    /// <summary>Neutral build mode owned by this assembly - NOT the Editor-only <c>SorollaMode</c>. Each
    /// producer maps its own mode source into this (Editor build path, on-device derivation).</summary>
    internal enum EvalMode
    {
        Unknown,
        Prototype,
        Full,
    }

    internal enum EvalPlatform
    {
        Unknown,
        Android,
        iOS,
    }

    [Flags]
    internal enum SdkModule
    {
        None = 0,
        GameAnalytics = 1 << 0,
        Facebook = 1 << 1,
        FirebaseApp = 1 << 2,
        FirebaseAnalytics = 1 << 3,
        FirebaseCrashlytics = 1 << 4,
        FirebaseRemoteConfig = 1 << 5,
        AppLovinMax = 1 << 6,
        Adjust = 1 << 7,
        UnityIap = 1 << 8,
        Firebase = FirebaseApp | FirebaseAnalytics | FirebaseCrashlytics | FirebaseRemoteConfig,
    }

    /// <summary>A requirement decision plus the reason/predicate-trace that produced it. The reason is
    /// mandatory for ALL four states (validated by the catalog) - an audit trail must not go silent exactly
    /// when a gate is Required.</summary>
    internal readonly struct RequirementDecision
    {
        public readonly Requirement Value;
        public readonly string Reason;

        public RequirementDecision(Requirement value, string reason = null)
        {
            Value = value;
            Reason = reason;
        }
    }

    /// <summary>
    ///     A canonical gate definition (registry-owned) - immutable after construction: all
    ///     fields are get-only and set via the constructor so a definition cannot mutate after the catalog is
    ///     built. The single <see cref="Requirement"/> predicate owns applicability AND required-ness,
    ///     evaluated from trusted context - a producer cannot self-exempt.
    /// </summary>
    internal sealed class GateDefinition
    {
        public string Id { get; }
        /// <summary>The gate asks a question only a store submission answers, so it is normally unsatisfied
        /// during development (a release keystore). EVERY gate reaches every report surface - this flag does
        /// NOT hide a row - it only keeps the build preprocessor from logging the warning on development
        /// builds, where the unsatisfied state is expected rather than informative.</summary>
        public bool ReleaseOnly { get; }
        public Func<EvaluationContext, RequirementDecision> Requirement { get; }

        public GateDefinition(
            string id,
            Func<EvaluationContext, RequirementDecision> requirement,
            bool releaseOnly = false)
        {
            Id = id;
            Requirement = requirement;
            ReleaseOnly = releaseOnly;
        }
    }

    /// <summary>
    ///     A producer's report of what it observed for one gate. Deliberately carries no requirement:
    ///     that belongs to the definition, not the producer.
    /// </summary>
    internal sealed class GateObservation
    {
        public string GateId;
        public GateOutcome Outcome;
        public string Evidence;
        public string FixHint;
        /// <summary>The producer observed a deliberate skip (vendor absent, wrong platform/profile) rather
        /// than an affirmative pass - Outcome still maps to Pass for aggregation (a skip is non-blocking,
        /// same as a pass), but frontends and the report export must render/label it as neutral, never as
        /// an affirmative green check. Purely additive passthrough: does not affect Outcome, Disposition,
        /// or verdict aggregation, only downstream display/export labeling.</summary>
        public bool Informational;
    }

    /// <summary>The trusted facts a definition's requirement predicate reads. Provenance is a later
    /// (Cycle 7) addition; that slot is intentionally left undefined here.</summary>
    internal sealed class EvaluationContext
    {
        public EvalMode Mode;
        public EvalPlatform Platform;
        public SdkModule InstalledModules;
        /// <summary>Whether the installed-module set was resolved from the trusted source (the package
        /// manifest). Unknown manifest state must NOT be treated as an empty module set - it
        /// forces INCOMPLETE. Defaults true; a producer that cannot read the manifest sets it false.</summary>
        public bool ModulesResolved = true;
    }

    /// <summary>One resolved gate = one row of a <see cref="HealthReport"/>. The evaluator's output, not a
    /// producer input: it joins a definition with its observation under the context.</summary>
    internal sealed class GateResult
    {
        public string GateId;
        public Requirement Requirement;
        public string RequirementReason;
        public GateDisposition Disposition;
        public GateOutcome Outcome;
        public string Evidence;
        public string FixHint;
        /// <summary>Carried through from the matched observation - see <see cref="GateObservation.Informational"/>.</summary>
        public bool Informational;
    }

    internal sealed class HealthReport
    {
        public IReadOnlyList<GateResult> Rows;
        /// <summary>The Editor's pre-build integration answer. Runtime Vitals is a separate authority.</summary>
        public GateOutcome Outcome;
        public IReadOnlyList<string> ValidationErrors;
    }

    internal static class HealthEnums
    {
        internal const SdkModule AllModuleBits = SdkModule.GameAnalytics | SdkModule.Facebook |
                                                 SdkModule.Firebase | SdkModule.AppLovinMax | SdkModule.Adjust |
                                                 SdkModule.UnityIap;
    }
}
