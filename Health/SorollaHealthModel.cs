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
        /// <summary>An Invariant gate resolved by the SDK release certificate in a Studio report: proven once
        /// on the reference game for this tagged release, so it does not vote here. Excluded from aggregation
        /// like OptionalSkipped/NotApplicable - and only ever reachable when no FAIL was observed locally.</summary>
        CertifiedBySdk,
    }

    /// <summary>
    ///     Who owns proving a gate, and therefore who ever sees it (2026-07-20 invariant/variant split).
    ///     <list type="bullet">
    ///         <item><c>Invariant</c> - SDK behavior identical for every game; certified ONCE per tagged
    ///         release on the reference game, never re-proven per studio.</item>
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
        Invariant,
        Structural,
        Variant,
    }

    /// <summary>Who the report is FOR. <c>SorollaFull</c> evaluates every gate at full depth (the internal
    /// pass); <c>Studio</c> collapses certified invariants to the release certificate. <c>Unknown</c> is a
    /// boundary validation error → INCOMPLETE: a report with no declared audience must not render green.</summary>
    internal enum ReportProfile
    {
        Unknown,
        SorollaFull,
        Studio,
    }

    /// <summary>Whether the SDK source under evaluation is a certified tagged release (tag-as-certificate).
    /// <c>Unknown</c> is legal and behaves as <c>Uncertified</c> - it only changes the evidence wording -
    /// because an unresolvable provenance must fail closed, not crash the report.</summary>
    internal enum SdkCertification
    {
        Unknown,
        Uncertified,
        CertifiedRelease,
    }

    /// <summary>Which validation phase(s) a gate belongs to. A definition with no phase is unreachable.</summary>
    [Flags]
    internal enum GatePhase
    {
        None = 0,
        PreBuild = 1 << 0,
        QaPass = 1 << 1,
        ReleaseShip = 1 << 2,
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
        Firebase = 1 << 2,
        AppLovinMax = 1 << 3,
        Adjust = 1 << 4,
        UnityIap = 1 << 5,
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
        public GatePhase Phases { get; }
        public ProofScope RequiredProof { get; }
        public Func<EvaluationContext, RequirementDecision> Requirement { get; }

        public GateDefinition(
            string id,
            string version,
            GateClassification classification,
            GatePhase phases,
            ProofScope requiredProof,
            Func<EvaluationContext, RequirementDecision> requirement)
        {
            Id = id;
            Version = version;
            Classification = classification;
            Phases = phases;
            RequiredProof = requiredProof;
            Requirement = requirement;
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
    }

    /// <summary>The trusted facts a definition's requirement predicate reads. Provenance is a later
    /// (Cycle 7) addition; that slot is intentionally left undefined here.</summary>
    internal sealed class EvaluationContext
    {
        public EvalMode Mode;
        public EvalPlatform Platform;
        public SdkModule InstalledModules;
        /// <summary>The phase this report is for (review C3-03). The evaluator - not the caller - selects the
        /// definitions that belong to this phase. A single defined phase bit is expected.</summary>
        public GatePhase RequestedPhase;
        /// <summary>Whether the installed-module set was resolved from the trusted source (the package
        /// manifest, review C4-02). Unknown manifest state must NOT be treated as an empty module set - it
        /// forces INCOMPLETE. Defaults true; a producer that cannot read the manifest sets it false.</summary>
        public bool ModulesResolved = true;
        /// <summary>Who this report is for. Defaults <see cref="ReportProfile.Unknown"/>, which is a boundary
        /// validation error → INCOMPLETE: a producer must declare its audience, never inherit one silently.</summary>
        public ReportProfile Profile;
        /// <summary>Whether the SDK source is a certified tagged release (tag-as-certificate). Only consulted
        /// for Invariant gates under the Studio profile. Unknown behaves as Uncertified.</summary>
        public SdkCertification Certification;
        /// <summary>Human-readable provenance behind <see cref="Certification"/> (the resolved tag, branch, or
        /// why it could not be resolved). Quoted into the certified rows' evidence.</summary>
        public string CertificationEvidence;
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
    }

    internal sealed class HealthReport
    {
        public IReadOnlyList<GateResult> Rows;
        public GateOutcome Outcome;
        public IReadOnlyList<string> ValidationErrors;
    }

    /// <summary>Boundary validation helpers (review C3-06): enum type-safety does NOT protect deserialized or
    /// corrupted values, so every value crossing into the evaluator is checked for definedness.</summary>
    internal static class HealthEnums
    {
        internal const ProofScope AllProofBits = ProofScope.Static | ProofScope.DeviceDispatch | ProofScope.VendorAccepted;
        internal const GatePhase AllPhaseBits = GatePhase.PreBuild | GatePhase.QaPass | GatePhase.ReleaseShip;
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
            v == GateClassification.Unknown || v == GateClassification.Invariant ||
            v == GateClassification.Structural || v == GateClassification.Variant;

        /// <summary>A DECLARED audience. Unknown is deliberately NOT accepted: it is the fail-closed default.</summary>
        internal static bool IsDeclaredProfile(ReportProfile v) =>
            v == ReportProfile.SorollaFull || v == ReportProfile.Studio;

        internal static bool IsDefinedCertification(SdkCertification v) =>
            v == SdkCertification.Unknown || v == SdkCertification.Uncertified ||
            v == SdkCertification.CertifiedRelease;

        internal static bool IsDefinedMode(EvalMode v) =>
            v == EvalMode.Unknown || v == EvalMode.Prototype || v == EvalMode.Full;

        internal static bool IsDefinedPlatform(EvalPlatform v) =>
            v == EvalPlatform.Unknown || v == EvalPlatform.Android || v == EvalPlatform.iOS;

        /// <summary>A single, defined, non-None phase bit (the shape a request must take).</summary>
        internal static bool IsSinglePhase(GatePhase v) =>
            v == GatePhase.PreBuild || v == GatePhase.QaPass || v == GatePhase.ReleaseShip;

        internal static bool HasOnlyDefinedBits(ProofScope v) => (v & ~AllProofBits) == 0;
        internal static bool HasOnlyDefinedBits(GatePhase v) => (v & ~AllPhaseBits) == 0;
        internal static bool HasOnlyDefinedBits(SdkModule v) => (v & ~AllModuleBits) == 0;
    }
}
