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

    /// <summary>How a gate's evidence was (or must be) obtained. A SET, not a hierarchy: a gate may
    /// require several proof classes at once, and an observation carries the ones actually obtained.</summary>
    [Flags]
    internal enum ProofScope
    {
        None = 0,
        Static = 1 << 0,
        DeviceDispatch = 1 << 1,
        VendorAccepted = 1 << 2,
    }

    /// <summary>
    ///     The context-derived requirement decision for a gate (review C3-02). ONE four-state decision
    ///     replaces the old fixed <c>bool Required</c> + separate applicability predicate, so a gate can be
    ///     required in one mode/platform and optional in another without duplicating its id.
    ///     <list type="bullet">
    ///         <item><c>Required</c> - must be observed; omission → INCOMPLETE.</item>
    ///         <item><c>Optional</c> - evaluated if observed; unobserved is a real skip (OptionalSkipped),
    ///         not a pass and not a lie about applicability (C3-04).</item>
    ///         <item><c>NotApplicable</c> - excluded; an observation supplied anyway is a context mismatch
    ///         (C3-05).</item>
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

    /// <summary>What actually happened to a gate in a report row (review C3-04): it was evaluated against an
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

    /// <summary>
    ///     Whether a gate describes project structure or a per-game variant.
    ///     <list type="bullet">
    ///         <item><c>Structural</c> - an SDK-fixed rule over the studio repo: machine-checked in every
    ///         project, silent unless red.</item>
    ///         <item><c>Variant</c> - a per-game input or action; always studio-visible.</item>
    ///         <item><c>Unknown</c> - catalog-invalid; <see cref="GateCatalog.Validate"/> rejects it so a new
    ///         gate cannot slip in unclassified.</item>
    ///     </list>
    /// </summary>
    internal enum GateClassification
    {
        Unknown,
        Structural,
        Variant,
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
    ///     A canonical gate definition (registry-owned) - immutable after construction (review C3-07): all
    ///     fields are get-only and set via the constructor so a definition cannot mutate after the catalog is
    ///     built. The single <see cref="Requirement"/> predicate owns applicability AND required-ness,
    ///     evaluated from trusted context - a producer cannot self-exempt. <see cref="Version"/> is a per-gate
    ///     semantic version stamped onto every report row so the comparison instrument can restart exactly one
    ///     gate's agreement count when its definition changes (R3-03).
    /// </summary>
    internal sealed class GateDefinition
    {
        public string Id { get; }
        public string Version { get; }
        /// <summary>Ownership class - a required constructor argument with NO default, so every call site
        /// must decide and an unclassified gate cannot be added by omission.</summary>
        public GateClassification Classification { get; }
        /// <summary>The gate asks a question only a store submission answers, so it is normally unsatisfied
        /// during development (a release keystore). EVERY gate reaches every report surface - this flag does
        /// NOT hide a row - it only keeps the build preprocessor from logging the warning on development
        /// builds, where the unsatisfied state is expected rather than informative.</summary>
        public bool ReleaseOnly { get; }
        public ProofScope RequiredProof { get; }
        public Func<EvaluationContext, RequirementDecision> Requirement { get; }

        public GateDefinition(
            string id,
            string version,
            GateClassification classification,
            ProofScope requiredProof,
            Func<EvaluationContext, RequirementDecision> requirement,
            bool releaseOnly = false)
        {
            Id = id;
            Version = version;
            Classification = classification;
            RequiredProof = requiredProof;
            Requirement = requirement;
            ReleaseOnly = releaseOnly;
        }
    }

    /// <summary>
    ///     A producer's report of what it observed for one gate. Deliberately carries NO requirement and NO
    ///     required-proof: those are the definition's, not the producer's. The evaluator, not the producer,
    ///     decides whether the observed proof satisfies what the definition requires.
    /// </summary>
    internal sealed class GateObservation
    {
        public string GateId;
        public GateOutcome Outcome;
        public ProofScope ObservedProof;
        public string Evidence;
        public string FixHint;
        /// <summary>The producer observed a deliberate skip (vendor absent, wrong platform/profile) rather
        /// than an affirmative pass - Outcome still maps to Pass for aggregation (a skip is non-blocking,
        /// same as a pass), but frontends and the report export must render/label it as neutral, never as
        /// an affirmative green check (F5 residual, 2026-07-21 audit review: Skipped had collapsed into
        /// Pass end-to-end once the Build Health row list - which special-cased it locally - was deleted
        /// by F12). Purely additive passthrough: does not affect Outcome, Disposition, or verdict
        /// aggregation, only downstream display/export labeling.</summary>
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
        /// manifest, review C4-02). Unknown manifest state must NOT be treated as an empty module set - it
        /// forces INCOMPLETE. Defaults true; a producer that cannot read the manifest sets it false.</summary>
        public bool ModulesResolved = true;
    }

    /// <summary>One resolved gate = one row of a <see cref="HealthReport"/>. The evaluator's output, not a
    /// producer input: it joins a definition with its observation under the context.</summary>
    internal sealed class GateResult
    {
        public string GateId;
        public string DefinitionVersion;
        /// <summary>Carried through from the definition so frontends and the report export can group, collapse,
        /// or route a row without re-looking-up the catalog.</summary>
        public GateClassification Classification;
        public Requirement Requirement;
        public string RequirementReason;
        public GateDisposition Disposition;
        public GateOutcome Outcome;
        public ProofScope RequiredProof;
        public ProofScope ObservedProof;
        public string Evidence;
        public string FixHint;
        /// <summary>Carried through from the matched observation - see <see cref="GateObservation.Informational"/>.</summary>
        public bool Informational;
    }

    internal sealed class HealthReport
    {
        public IReadOnlyList<GateResult> Rows;
        /// <summary>Everything this report saw, static and device evidence together.</summary>
        public GateOutcome Outcome;
        /// <summary>
        ///     The pre-build answer: is this project's SDK INTEGRATION ready? Aggregated over the gates that
        ///     are decidable without running the game, so a device that was never connected cannot hold a
        ///     clean integration below green. Device evidence is a separate question with its own rows and
        ///     its own verdict, and it never votes here (2026-07-23).
        /// </summary>
        public GateOutcome IntegrationOutcome;
        public IReadOnlyList<string> ValidationErrors;
    }

    /// <summary>Boundary validation helpers (review C3-06): enum type-safety does NOT protect deserialized or
    /// corrupted values, so every value crossing into the evaluator is checked for definedness.</summary>
    internal static class HealthEnums
    {
        internal const ProofScope AllProofBits = ProofScope.Static | ProofScope.DeviceDispatch | ProofScope.VendorAccepted;
        internal const SdkModule AllModuleBits = SdkModule.GameAnalytics | SdkModule.Facebook |
                                                 SdkModule.Firebase | SdkModule.AppLovinMax | SdkModule.Adjust |
                                                 SdkModule.UnityIap;

        internal static bool IsDefinedOutcome(GateOutcome v) =>
            v == GateOutcome.Incomplete || v == GateOutcome.Fail ||
            v == GateOutcome.PassWithCaveats || v == GateOutcome.Pass;

        internal static bool IsDefinedRequirement(Requirement v) =>
            v == Requirement.Unknown || v == Requirement.NotApplicable ||
            v == Requirement.Optional || v == Requirement.Required;

        internal static bool IsDefinedClassification(GateClassification v) =>
            v == GateClassification.Unknown || v == GateClassification.Structural ||
            v == GateClassification.Variant;

        internal static bool IsDefinedMode(EvalMode v) =>
            v == EvalMode.Unknown || v == EvalMode.Prototype || v == EvalMode.Full;

        internal static bool IsDefinedPlatform(EvalPlatform v) =>
            v == EvalPlatform.Unknown || v == EvalPlatform.Android || v == EvalPlatform.iOS;

        internal static bool HasOnlyDefinedBits(ProofScope v) => (v & ~AllProofBits) == 0;
        internal static bool HasOnlyDefinedBits(SdkModule v) => (v & ~AllModuleBits) == 0;
    }
}
