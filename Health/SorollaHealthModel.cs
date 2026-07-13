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

    /// <summary>Whether a gate applies to a given build. Tri-state: Unknown means the evaluator could not
    /// decide from the trusted context and MUST resolve to Incomplete, never a silent skip.</summary>
    internal enum Applicability
    {
        Applicable,
        NotApplicable,
        Unknown,
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
    }

    /// <summary>An applicability decision plus the reason/predicate-trace that produced it. The reason is
    /// required whenever the value is not <see cref="Applicability.Applicable"/>.</summary>
    internal readonly struct ApplicabilityVerdict
    {
        public readonly Applicability Value;
        public readonly string Reason;

        public ApplicabilityVerdict(Applicability value, string reason = null)
        {
            Value = value;
            Reason = reason;
        }
    }

    /// <summary>
    ///     A canonical gate definition (registry-owned). Applicability and required proof live HERE, on the
    ///     definition, evaluated from trusted context - a producer cannot self-exempt by declaring its own
    ///     applicability or proof (review R3-02/R3-04). <see cref="Version"/> is a per-gate semantic version
    ///     stamped onto every report row so the comparison instrument can restart exactly one gate's
    ///     agreement count when its definition changes (R3-03).
    /// </summary>
    internal sealed class GateDefinition
    {
        public string Id;
        public string Version;
        public GatePhase Phases;
        public bool Required;
        public ProofScope RequiredProof;
        public Func<EvaluationContext, ApplicabilityVerdict> Applicability;
    }

    /// <summary>
    ///     A producer's report of what it observed for one gate. Deliberately carries NO applicability and
    ///     NO required-proof: those are the definition's, not the producer's. The evaluator, not the
    ///     producer, decides whether the observed proof satisfies what the definition requires.
    /// </summary>
    internal sealed class GateObservation
    {
        public string GateId;
        public GateOutcome Outcome;
        public ProofScope ObservedProof;
        public string Evidence;
        public string FixHint;
    }

    /// <summary>The trusted facts a definition's applicability predicate reads. Provenance is a later
    /// (Cycle 7) addition; this slot is intentionally left undefined here.</summary>
    internal sealed class EvaluationContext
    {
        public EvalMode Mode;
        public EvalPlatform Platform;
        public SdkModule InstalledModules;
    }

    /// <summary>One resolved gate = one row of a <see cref="HealthReport"/>. The evaluator's output, not a
    /// producer input: it joins a definition with its observation under the context.</summary>
    internal sealed class GateResult
    {
        public string GateId;
        public string DefinitionVersion;
        public GateOutcome Outcome;
        public Applicability Applicability;
        public string ApplicabilityReason;
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
}
