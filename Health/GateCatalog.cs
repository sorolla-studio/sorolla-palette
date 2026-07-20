using System;
using System.Collections.Generic;
using System.Linq;

namespace Sorolla.Palette.Health
{
    /// <summary>
    ///     Stable gate-id vocabulary - string constants, never magic strings. Producers (the Editor
    ///     greenlight adapter, the on-device path) map their own evidence rows onto these ids; the
    ///     canonical <see cref="GateCatalog"/> owns each id's context-derived requirement and proof scope.
    /// </summary>
    internal static class GateIds
    {
        // Build Health - one id per BuildValidator check category (mapped in the Editor adapter). These
        // are the individual rows, NOT the collapsed one-row summary.
        public const string BuildRequiredSdks = "build.required_sdks";
        public const string BuildSdkVersions = "build.sdk_versions";
        public const string BuildModeConsistency = "build.mode_consistency";
        public const string BuildScopedRegistries = "build.scoped_registries";
        public const string BuildFirebaseCoherence = "build.firebase_coherence";
        public const string BuildConfigSync = "build.config_sync";
        public const string BuildAndroidManifest = "build.android_manifest";
        public const string BuildMaxSettings = "build.max_settings";
        public const string BuildAdjustSettings = "build.adjust_settings";
        public const string BuildEdm4uSettings = "build.edm4u_settings";
        public const string BuildGradleConfig = "build.gradle_config";
        public const string BuildFirebaseConfig = "build.firebase_config";
        public const string BuildGameAnalyticsKeys = "build.gameanalytics_keys";
        public const string BuildFacebookPlatform = "build.facebook_platform";
        public const string BuildVerboseLogging = "build.verbose_logging";
        public const string BuildDevelopmentBuild = "build.development_build";
        public const string BuildAdjustSandboxMode = "build.adjust_sandbox_mode";
        public const string BuildAndroidKeystore = "build.android_keystore";
        public const string BuildGradleJavaHome = "build.gradle_java_home";
        public const string BuildGameAnalyticsResourceWhitelist = "build.gameanalytics_resource_whitelist";
        public const string BuildAddressablesContent = "build.addressables_content";
        public const string BuildSdkPin = "build.sdk_pin";
        public const string BuildGameAnalyticsCredentials = "build.gameanalytics_credentials";

        // Device snapshot facts (require a live on-device dispatch).
        public const string DeviceAdvertisingId = "device.advertising_id";
        public const string DeviceNoSdkErrors = "device.no_sdk_errors";

        // In-app purchases: store-console catalog/test-track config, vendor-attested and per-game. (The old
        // iap.tracking_attached wiring gate was deleted with the 2026-07-20 split - its successor is the
        // per-build variant coverage ledger; the QA-bridge snapshot still reports the raw signal.)
        public const string IapStoreConfigured = "iap.store_configured";

        // Manual / dashboard attestations (require vendor-side or on-device human confirmation).
        public const string ManualGaPlatformRegistered = "manual.ga_platform_registered";
        public const string ManualCrossVendorDashboardDrift = "manual.cross_vendor_dashboard_drift";
        public const string ManualAdjustPurchaseVerification = "manual.adjust_purchase_verification";
        public const string ManualRelaunchPersistence = "manual.relaunch_persistence";
        public const string ManualBackgroundResumeCycle = "manual.background_resume_cycle";
    }

    /// <summary>
    ///     The one canonical, code-defined gate catalog the SDK ships (not a ScriptableObject or YAML, so it
    ///     is grep/diff/compile-checked and has no optional-asset failure mode - DR-133). Each definition
    ///     owns its per-context requirement decision (the mode requirement table lives HERE, not in the
    ///     producer) and its required proof scope. The private gates.yaml workflow references the same string
    ///     ids without any portfolio data shipping here (design note section 4). Definitions are frozen on
    ///     construction (review C3-07).
    /// </summary>
    internal sealed class GateCatalog
    {
        readonly IReadOnlyList<GateDefinition> _definitions;
        readonly Dictionary<string, GateDefinition> _byId;

        internal GateCatalog(IEnumerable<GateDefinition> definitions)
        {
            // Defensive copy + freeze so All and ById can never disagree and a definition list cannot be
            // mutated after construction (review C3-07). GateDefinition itself is immutable.
            _definitions = (definitions ?? Array.Empty<GateDefinition>())
                .Where(d => d != null).ToArray();
            _byId = new Dictionary<string, GateDefinition>();
            foreach (GateDefinition d in _definitions)
                if (!string.IsNullOrWhiteSpace(d.Id) && !_byId.ContainsKey(d.Id))
                    _byId[d.Id] = d;
        }

        internal IReadOnlyList<GateDefinition> All => _definitions;

        /// <summary>Looks a definition up by id. Throws on an unknown id by default (no silent null); pass
        /// <paramref name="throwIfMissing"/> = false to probe.</summary>
        internal GateDefinition ById(string id, bool throwIfMissing = true)
        {
            if (id != null && _byId.TryGetValue(id, out GateDefinition def))
                return def;
            if (throwIfMissing)
                throw new KeyNotFoundException($"No gate definition with id '{id}' in the catalog.");
            return null;
        }

        /// <summary>The shipped canonical catalog: the complete mode requirement table.</summary>
        internal static GateCatalog Canonical { get; } = new GateCatalog(BuildCanonical());

        /// <summary>
        ///     Every mode x platform combination the SDK supports, used by <see cref="Validate"/> (a gate
        ///     never Required-or-Optional under any of these is unreachable) and by the requirement-table
        ///     tests. All modules installed so a module-gated definition is reachable in at least one context.
        /// </summary>
        internal static IReadOnlyList<EvaluationContext> SupportedContexts { get; } = BuildSupportedContexts();

        // ── The mode requirement table ────────────────────────────────────

        const string Version = "1"; // per-gate semantic version (R3-03); bump a single gate when its meaning changes.
        // Gates whose MEANING changed carry a bumped version so the comparison instrument restarts exactly
        // their agreement counts (F1 device applicability now targets-based; F5 store/tracking split; F9
        // Firebase config now active-app matching, not mere presence).
        const string V2 = "2";

        static IReadOnlyList<GateDefinition> BuildCanonical()
        {
            // Core + advisory build checks and the device/manual/iap gates are prerequisites at RELEASE too,
            // so they carry ReleaseShip - a release report must not drop them (review: ReleaseShip includes
            // core prerequisites, not only release-only checks).
            const GatePhase buildPhase = GatePhase.PreBuild | GatePhase.QaPass | GatePhase.ReleaseShip;
            const GatePhase qaPhase = GatePhase.QaPass | GatePhase.ReleaseShip;
            // Release-ONLY checks: NOT tagged QaPass, so a QA-pass report never selects them and a validator
            // "Skipped (QA Pass profile)" Valid result can never masquerade as a PASS (review C4-04).
            const GatePhase releasePhase = GatePhase.PreBuild | GatePhase.ReleaseShip;
            var defs = new List<GateDefinition>();

            // Build Health - core SDKs, required in BOTH modes (GameAnalytics + Facebook are SdkRequirement.Core).
            // required_sdks is a repo-shape rule (Structural); the credential/key rows are per-game inputs (Variant).
            AddBuild(defs, GateIds.BuildRequiredSdks, GateClassification.Structural, Requirements.AlwaysRequired);
            AddBuild(defs, GateIds.BuildGameAnalyticsKeys, GateClassification.Variant, Requirements.AlwaysRequired);
            AddBuild(defs, GateIds.BuildGameAnalyticsCredentials, GateClassification.Variant, Requirements.AlwaysRequired);
            AddBuild(defs, GateIds.BuildFacebookPlatform, GateClassification.Variant, Requirements.AlwaysRequired);

            // Firebase coherence - THE decided contradiction, expressed in the 4-state model. SdkRegistry
            // marks every Firebase module FullRequired ("optional in Prototype, never uninstalled"), so it is
            // Required in Full and Optional in Prototype: a prototype that ships Firebase has its coherence
            // evaluated, a bare prototype skips it cleanly (no penalty), and no real observation is discarded.
            AddBuild(defs, GateIds.BuildFirebaseCoherence, GateClassification.Structural,
                Requirements.FullRequiredElseOptional);

            // Full-mode vendors (AppLovin MAX FullRequired, Adjust FullOnly): Required in Full, Optional in
            // Prototype (evaluated only if the vendor is present). Both carry per-game credentials/ad-unit ids.
            AddBuild(defs, GateIds.BuildMaxSettings, GateClassification.Variant, Requirements.FullRequiredElseOptional);
            AddBuild(defs, GateIds.BuildAdjustSettings, GateClassification.Variant, Requirements.FullRequiredElseOptional);

            // Firebase config files follow the SAME requirement as Firebase itself (review C4-05): when
            // Firebase is required (Full), a missing active-platform google-services.json / plist must block
            // release confidence, not sit as a non-blocking advisory warning. Optional in Prototype. V2 (F9):
            // the check now also fails when a PRESENT config is for the wrong app id (active-app matching), a
            // stricter meaning than mere presence, so its comparison-agreement count restarts.
            defs.Add(new GateDefinition(GateIds.BuildFirebaseConfig, V2, GateClassification.Variant, buildPhase,
                ProofScope.Static, Requirements.FullRequiredElseOptional));

            // Release-only checks (review C4-04) - PreBuild|ReleaseShip, NOT QaPass. BuildValidator emits a
            // "Skipped (QA Pass profile)" Valid for these in a QA-pass run; because they are not selected in a
            // QaPass report their skip observation is unused, so it can never read as a PASS. Keystore is
            // Required on Android at release; the rest are advisory release-ship checks.
            defs.Add(new GateDefinition(GateIds.BuildAndroidKeystore, Version, GateClassification.Variant,
                releasePhase, ProofScope.Static, Requirements.AndroidRequiredElseOptional));
            foreach (string id in new[]
            {
                GateIds.BuildAdjustSandboxMode, GateIds.BuildSdkPin,
            })
                defs.Add(new GateDefinition(id, Version, GateClassification.Structural, releasePhase,
                    ProofScope.Static, Requirements.AlwaysOptional));

            // Advisory Build Health rows - Optional in both modes. Their OBSERVED outcome still drives
            // precedence (an error -> FAIL, a warning -> caveats); an unobserved conditional check is a real
            // OptionalSkipped, not a false pass and not a NotApplicable lie.
            foreach (string id in new[]
            {
                GateIds.BuildSdkVersions, GateIds.BuildModeConsistency, GateIds.BuildScopedRegistries,
                GateIds.BuildConfigSync, GateIds.BuildAndroidManifest, GateIds.BuildEdm4uSettings,
                GateIds.BuildGradleConfig, GateIds.BuildVerboseLogging, GateIds.BuildDevelopmentBuild,
                GateIds.BuildGradleJavaHome, GateIds.BuildGameAnalyticsResourceWhitelist,
                GateIds.BuildAddressablesContent,
            })
                AddBuild(defs, id, GateClassification.Structural, Requirements.AlwaysOptional);

            // In-app purchases (review C4-10, F2/F5). STORE config is Required only when Unity IAP is installed
            // AND the active platform is an intended release/commerce target (a game shipping on one store must
            // not be forced to prove the other store's config). Its proof is vendor-side (store console),
            // unavailable to the SDK, so with no attestation it resolves to INCOMPLETE (Omitted). NotApplicable
            // when Unity IAP is absent OR the active platform is not an intended target.
            defs.Add(new GateDefinition(GateIds.IapStoreConfigured, V2, GateClassification.Variant, qaPhase,
                ProofScope.VendorAccepted, Requirements.IapStoreConfiguredRequirement));

            // Device snapshot (F1): applicability follows the INTENDED release platform, not the (currently
            // Android-only) transport. On an intended platform the device gates stay applicable; if no evidence
            // collector exists yet (iOS today) the required ones omit → INCOMPLETE (a capability gap), never a
            // NotApplicable exemption. NotApplicable only when the active platform is not an intended target.
            // Both are SDK behavior identical for every game → Invariant (certified per tagged release).
            defs.Add(new GateDefinition(GateIds.DeviceAdvertisingId, V2, GateClassification.Invariant, qaPhase,
                ProofScope.DeviceDispatch, Requirements.IntendedPlatformOptionalElseNotApplicable));
            defs.Add(new GateDefinition(GateIds.DeviceNoSdkErrors, V2, GateClassification.Invariant, qaPhase,
                ProofScope.DeviceDispatch, Requirements.IntendedPlatformRequiredElseNotApplicable));

            // Manual / dashboard attestations - required, and the required proof (vendor-accepted or an
            // on-device human session) is deliberately something a legacy EditorPrefs check-off cannot supply,
            // so a ticked legacy box resolves to INCOMPLETE, never PASS (B-10). Adjust purchase verification
            // is NotApplicable in Prototype (no Adjust there; the adapter emits it only in Full).
            defs.Add(new GateDefinition(GateIds.ManualGaPlatformRegistered, Version, GateClassification.Variant,
                qaPhase, ProofScope.VendorAccepted, Requirements.AlwaysRequired));
            // Cross-vendor dashboard drift is the heaviest manual burden and the same SDK fan-out for every
            // game (per-game routing is already covered by the variant credential gates) → Invariant.
            defs.Add(new GateDefinition(GateIds.ManualCrossVendorDashboardDrift, Version, GateClassification.Invariant,
                qaPhase, ProofScope.VendorAccepted, Requirements.AlwaysRequired));
            defs.Add(new GateDefinition(GateIds.ManualAdjustPurchaseVerification, Version, GateClassification.Variant,
                qaPhase, ProofScope.VendorAccepted, Requirements.FullRequiredElseNotApplicable));
            defs.Add(new GateDefinition(GateIds.ManualRelaunchPersistence, Version, GateClassification.Invariant,
                qaPhase, ProofScope.DeviceDispatch, Requirements.AlwaysRequired));
            defs.Add(new GateDefinition(GateIds.ManualBackgroundResumeCycle, Version, GateClassification.Invariant,
                qaPhase, ProofScope.DeviceDispatch, Requirements.AlwaysRequired));

            return defs;

            void AddBuild(List<GateDefinition> list, string id, GateClassification classification,
                Func<EvaluationContext, RequirementDecision> req) =>
                list.Add(new GateDefinition(id, Version, classification, buildPhase, ProofScope.Static, req));
        }

        static IReadOnlyList<EvaluationContext> BuildSupportedContexts()
        {
            var contexts = new List<EvaluationContext>();
            foreach (EvalMode mode in new[] { EvalMode.Prototype, EvalMode.Full })
            foreach (EvalPlatform platform in new[] { EvalPlatform.Android, EvalPlatform.iOS })
                contexts.Add(new EvaluationContext
                {
                    Mode = mode,
                    Platform = platform,
                    InstalledModules = HealthEnums.AllModuleBits,
                    // Both stores declared for BOTH axes so the active platform is always a distribution and a
                    // commerce target: this makes the target-gated device and store gates reachable
                    // (Required/Optional) in the reachability check.
                    IntendedTargets = HealthEnums.AllTargetBits,
                    CommerceTargets = HealthEnums.AllTargetBits,
                    RequestedPhase = GatePhase.QaPass,
                    // Reachability is checked at FULL depth against an UNCERTIFIED source: the strictest
                    // context, where no gate can be excused by the release certificate.
                    Profile = ReportProfile.SorollaFull,
                    Certification = SdkCertification.Uncertified,
                });
            return contexts;
        }

        /// <summary>
        ///     Fails loud on a malformed catalog (review C3-07). Returns the list of problems (empty = valid).
        ///     Rejects: duplicate ids; null/empty/whitespace ids; null/empty versions; missing requirement
        ///     predicate; undefined phase/proof flag bits; unreachable gates (no phase, or never
        ///     Required/Optional under any supported context); a non-exhaustive supported-context grid; and
        ///     NotApplicable/Unknown decisions without a reason. Pure function so it is unit-testable against
        ///     synthetic catalogs; run as an Editor test over <see cref="Canonical"/>.
        /// </summary>
        internal static IReadOnlyList<string> Validate(
            IEnumerable<GateDefinition> definitions, IReadOnlyList<EvaluationContext> supportedContexts)
        {
            var problems = new List<string>();
            List<GateDefinition> defs = definitions?.ToList() ?? new List<GateDefinition>();

            if (defs.Any(d => d == null))
                problems.Add("Catalog contains a null definition.");
            List<GateDefinition> present = defs.Where(d => d != null).ToList();

            foreach (IGrouping<string, GateDefinition> group in present.GroupBy(d => d.Id))
                if (group.Count() > 1)
                    problems.Add($"Duplicate gate id: '{group.Key}' ({group.Count()} definitions)");

            // The context grid must be exhaustive over the supported mode x platform axes (review C3-07).
            bool gridExhaustive = supportedContexts != null &&
                (from EvalMode m in new[] { EvalMode.Prototype, EvalMode.Full }
                 from EvalPlatform p in new[] { EvalPlatform.Android, EvalPlatform.iOS }
                 select (m, p)).All(combo =>
                    supportedContexts.Any(c => c.Mode == combo.m && c.Platform == combo.p));
            if (!gridExhaustive)
                problems.Add("Supported-context grid is missing or not exhaustive over mode x platform.");

            foreach (GateDefinition def in present)
            {
                if (string.IsNullOrWhiteSpace(def.Id))
                    problems.Add("Gate with a null/empty/whitespace id.");
                if (string.IsNullOrWhiteSpace(def.Version))
                    problems.Add($"Gate '{def.Id}' has a null/empty version.");
                if (def.Requirement == null)
                    problems.Add($"Gate '{def.Id}' has no requirement predicate.");
                if (def.Phases == GatePhase.None)
                    problems.Add($"Unreachable gate '{def.Id}': no phase (Phases == None)");
                if (!HealthEnums.HasOnlyDefinedBits(def.Phases))
                    problems.Add($"Gate '{def.Id}' has undefined phase flag bits.");
                if (!HealthEnums.HasOnlyDefinedBits(def.RequiredProof))
                    problems.Add($"Gate '{def.Id}' has undefined proof-scope flag bits.");
                // Every gate must be classified: an unclassified gate has no declared owner, so no frontend
                // can decide whether a studio must see it.
                if (!HealthEnums.IsDefinedClassification(def.Classification) ||
                    def.Classification == GateClassification.Unknown)
                    problems.Add($"Gate '{def.Id}' is unclassified (GateClassification.Unknown).");
                // An Invariant gate is certified once per release on the reference GAME - it must therefore
                // rest on evidence a studio's repo cannot produce. A Static-proof-only invariant would be a
                // repo-shape rule mislabelled, i.e. the invariant/structural conflation this split removed.
                if (def.Classification == GateClassification.Invariant &&
                    (def.RequiredProof & ~ProofScope.Static) == ProofScope.None)
                    problems.Add($"Invariant gate '{def.Id}' requires only Static proof - it is Structural, not Invariant.");

                if (def.Requirement == null || supportedContexts == null || supportedContexts.Count == 0)
                    continue;

                bool reachable = false;
                foreach (EvaluationContext ctx in supportedContexts)
                {
                    RequirementDecision rd = def.Requirement(ctx);
                    if (!HealthEnums.IsDefinedRequirement(rd.Value))
                    {
                        problems.Add($"Gate '{def.Id}' returns an undefined requirement value under a supported context.");
                        continue;
                    }
                    if (rd.Value == Requirement.Required || rd.Value == Requirement.Optional)
                        reachable = true;
                    // Reason mandatory for ALL four states (shape-GO refinement #1): an audit trail that goes
                    // silent exactly when a gate is Required is backwards.
                    if (string.IsNullOrWhiteSpace(rd.Reason))
                        problems.Add($"Gate '{def.Id}' has a {rd.Value} decision without a reason.");
                }
                if (!reachable)
                    problems.Add($"Unreachable gate '{def.Id}': never Required or Optional under any supported context");
            }

            return problems;
        }
    }

    /// <summary>
    ///     The context-derived requirement predicates the mode requirement table is built from (review
    ///     C3-02). Each reads the trusted <see cref="EvaluationContext"/> only - a producer cannot self-exempt.
    ///     Unknown mode/platform resolves to <see cref="Requirement.Unknown"/> (→ INCOMPLETE), never a silent
    ///     skip. NotApplicable / Unknown decisions always carry a reason.
    /// </summary>
    internal static class Requirements
    {
        static RequirementDecision Req(string r) => new RequirementDecision(Requirement.Required, r);
        static RequirementDecision Opt(string r) => new RequirementDecision(Requirement.Optional, r);
        static RequirementDecision Na(string r) => new RequirementDecision(Requirement.NotApplicable, r);
        static RequirementDecision Unk(string r) => new RequirementDecision(Requirement.Unknown, r);

        internal static readonly Func<EvaluationContext, RequirementDecision> AlwaysRequired =
            _ => Req("required in both modes");

        internal static readonly Func<EvaluationContext, RequirementDecision> AlwaysOptional =
            _ => Opt("advisory check");

        internal static readonly Func<EvaluationContext, RequirementDecision> FullRequiredElseOptional = ctx =>
            ctx.Mode == EvalMode.Unknown ? Unk("SDK mode is unknown (no config)")
            : ctx.Mode == EvalMode.Full ? Req("required in Full mode")
            : Opt("optional in Prototype (evaluated if present)");

        internal static readonly Func<EvaluationContext, RequirementDecision> FullRequiredElseNotApplicable = ctx =>
            ctx.Mode == EvalMode.Unknown ? Unk("SDK mode is unknown (no config)")
            : ctx.Mode == EvalMode.Full ? Req("required in Full mode")
            : Na("not applicable in Prototype (vendor absent)");

        internal static readonly Func<EvaluationContext, RequirementDecision> AndroidRequiredElseOptional = ctx =>
            ctx.Platform == EvalPlatform.Unknown ? Unk("build platform is unknown")
            : ctx.Platform == EvalPlatform.Android ? Req("Android build fact")
            : Opt("evaluated only if reported off-Android");

        // ── F1: applicability follows the INTENDED release platform, not the evidence collector ──
        // On an intended platform a required device gate stays applicable and omits → INCOMPLETE when no
        // collector exists (a capability gap); NotApplicable only when the active platform is not a target.

        internal static readonly Func<EvaluationContext, RequirementDecision> IntendedPlatformRequiredElseNotApplicable = ctx =>
            ctx.IntendedTargets == DistributionTargets.None ? Unk("intended release targets are not declared")
            : ctx.Platform == EvalPlatform.Unknown ? Unk("build platform is unknown")
            : HealthEnums.TargetsInclude(ctx.IntendedTargets, ctx.Platform)
                ? Req("device evidence required on an intended release platform")
                : Na("active platform is not an intended release target");

        internal static readonly Func<EvaluationContext, RequirementDecision> IntendedPlatformOptionalElseNotApplicable = ctx =>
            ctx.IntendedTargets == DistributionTargets.None ? Unk("intended release targets are not declared")
            : ctx.Platform == EvalPlatform.Unknown ? Unk("build platform is unknown")
            : HealthEnums.TargetsInclude(ctx.IntendedTargets, ctx.Platform)
                ? Opt("device fact on an intended release platform")
                : Na("active platform is not an intended release target");

        // ── F2/B2: store config is Required only when Unity IAP is installed AND the active platform is a
        // declared COMMERCE target (distinct from distribution: a game can ship the app on Android but sell IAP
        // only on iOS). Keys on CommerceTargets, not IntendedTargets. NotApplicable when the active platform
        // sells no IAP; Unknown when commerce targets are undeclared. ──
        internal static readonly Func<EvaluationContext, RequirementDecision> IapStoreConfiguredRequirement = ctx =>
            (ctx.InstalledModules & SdkModule.UnityIap) == 0 ? Na("Unity IAP not installed")
            : ctx.CommerceTargets == DistributionTargets.None ? Unk("commerce (IAP) targets are not declared")
            : ctx.Platform == EvalPlatform.Unknown ? Unk("build platform is unknown")
            : HealthEnums.TargetsInclude(ctx.CommerceTargets, ctx.Platform)
                ? Req("Unity IAP installed and active platform is a declared commerce target")
                : Na("active platform is not a declared commerce target");
    }
}
