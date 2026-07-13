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
        public const string BuildPrototypeModeIntent = "build.prototype_mode_intent";
        public const string BuildVerboseLogging = "build.verbose_logging";
        public const string BuildDevelopmentBuild = "build.development_build";
        public const string BuildAdjustSandboxMode = "build.adjust_sandbox_mode";
        public const string BuildAndroidKeystore = "build.android_keystore";
        public const string BuildGradleJavaHome = "build.gradle_java_home";
        public const string BuildGameAnalyticsResourceWhitelist = "build.gameanalytics_resource_whitelist";
        public const string BuildAddressablesContent = "build.addressables_content";
        public const string BuildSdkPin = "build.sdk_pin";
        public const string BuildAdjustResolvedVersion = "build.adjust_resolved_version";
        public const string BuildGameAnalyticsCredentials = "build.gameanalytics_credentials";

        // Device snapshot facts (require a live on-device dispatch).
        public const string DeviceReady = "device.ready";
        public const string DeviceAdvertisingId = "device.advertising_id";
        public const string DeviceNoSdkErrors = "device.no_sdk_errors";

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

        static IReadOnlyList<GateDefinition> BuildCanonical()
        {
            const GatePhase buildPhase = GatePhase.PreBuild | GatePhase.QaPass;
            const GatePhase qaPhase = GatePhase.QaPass;
            var defs = new List<GateDefinition>();

            // Build Health - core SDKs, required in BOTH modes (GameAnalytics + Facebook are SdkRequirement.Core).
            AddBuild(defs, GateIds.BuildRequiredSdks, Requirements.AlwaysRequired);
            AddBuild(defs, GateIds.BuildGameAnalyticsKeys, Requirements.AlwaysRequired);
            AddBuild(defs, GateIds.BuildGameAnalyticsCredentials, Requirements.AlwaysRequired);
            AddBuild(defs, GateIds.BuildFacebookPlatform, Requirements.AlwaysRequired);

            // Firebase coherence - THE decided contradiction, expressed in the 4-state model. SdkRegistry
            // marks every Firebase module FullRequired ("optional in Prototype, never uninstalled"), so it is
            // Required in Full and Optional in Prototype: a prototype that ships Firebase has its coherence
            // evaluated, a bare prototype skips it cleanly (no penalty), and no real observation is discarded.
            AddBuild(defs, GateIds.BuildFirebaseCoherence, Requirements.FullRequiredElseOptional);

            // Full-mode vendors (AppLovin MAX FullRequired, Adjust FullOnly): Required in Full, Optional in
            // Prototype (evaluated only if the vendor is present).
            AddBuild(defs, GateIds.BuildMaxSettings, Requirements.FullRequiredElseOptional);
            AddBuild(defs, GateIds.BuildAdjustSettings, Requirements.FullRequiredElseOptional);
            AddBuild(defs, GateIds.BuildAdjustResolvedVersion, Requirements.FullRequiredElseOptional);

            // Android keystore - Required on Android; Optional (never NotApplicable) off-Android because
            // BuildValidator still emits a "Skipped (not Android)" Valid result there, and an observation for
            // a NotApplicable gate would be a context-mismatch error.
            AddBuild(defs, GateIds.BuildAndroidKeystore, Requirements.AndroidRequiredElseOptional);

            // Firebase config files follow the SAME requirement as Firebase itself (review C4-05): when
            // Firebase is required (Full), a missing active-platform google-services.json / plist must block
            // release confidence, not sit as a non-blocking advisory warning. Optional in Prototype.
            AddBuild(defs, GateIds.BuildFirebaseConfig, Requirements.FullRequiredElseOptional);

            // Advisory Build Health rows - Optional in both modes. Their OBSERVED outcome still drives
            // precedence (an error -> FAIL, a warning -> caveats); an unobserved conditional check is a real
            // OptionalSkipped, not a false pass and not a NotApplicable lie.
            foreach (string id in new[]
            {
                GateIds.BuildSdkVersions, GateIds.BuildModeConsistency, GateIds.BuildScopedRegistries,
                GateIds.BuildConfigSync, GateIds.BuildAndroidManifest, GateIds.BuildEdm4uSettings,
                GateIds.BuildGradleConfig, GateIds.BuildPrototypeModeIntent,
                GateIds.BuildVerboseLogging, GateIds.BuildDevelopmentBuild, GateIds.BuildAdjustSandboxMode,
                GateIds.BuildGradleJavaHome, GateIds.BuildGameAnalyticsResourceWhitelist,
                GateIds.BuildAddressablesContent, GateIds.BuildSdkPin,
            })
                AddBuild(defs, id, Requirements.AlwaysOptional);

            // Device snapshot - the Android QA bridge is the only transport that ships, so device facts are
            // NotApplicable off-Android (the adapter emits no device observation there). Readiness is required
            // on Android: a build we have never confirmed on device cannot pass, it is INCOMPLETE.
            defs.Add(new GateDefinition(GateIds.DeviceReady, Version, qaPhase, ProofScope.DeviceDispatch,
                Requirements.AndroidRequiredElseNotApplicable));
            defs.Add(new GateDefinition(GateIds.DeviceAdvertisingId, Version, qaPhase, ProofScope.DeviceDispatch,
                Requirements.AndroidOptionalElseNotApplicable));
            defs.Add(new GateDefinition(GateIds.DeviceNoSdkErrors, Version, qaPhase, ProofScope.DeviceDispatch,
                Requirements.AndroidOptionalElseNotApplicable));

            // Manual / dashboard attestations - required, and the required proof (vendor-accepted or an
            // on-device human session) is deliberately something a legacy EditorPrefs check-off cannot supply,
            // so a ticked legacy box resolves to INCOMPLETE, never PASS (B-10). Adjust purchase verification
            // is NotApplicable in Prototype (no Adjust there; the adapter emits it only in Full).
            defs.Add(new GateDefinition(GateIds.ManualGaPlatformRegistered, Version, qaPhase, ProofScope.VendorAccepted,
                Requirements.AlwaysRequired));
            defs.Add(new GateDefinition(GateIds.ManualCrossVendorDashboardDrift, Version, qaPhase, ProofScope.VendorAccepted,
                Requirements.AlwaysRequired));
            defs.Add(new GateDefinition(GateIds.ManualAdjustPurchaseVerification, Version, qaPhase, ProofScope.VendorAccepted,
                Requirements.FullRequiredElseNotApplicable));
            defs.Add(new GateDefinition(GateIds.ManualRelaunchPersistence, Version, qaPhase, ProofScope.DeviceDispatch,
                Requirements.AlwaysRequired));
            defs.Add(new GateDefinition(GateIds.ManualBackgroundResumeCycle, Version, qaPhase, ProofScope.DeviceDispatch,
                Requirements.AlwaysRequired));

            return defs;

            void AddBuild(List<GateDefinition> list, string id, Func<EvaluationContext, RequirementDecision> req) =>
                list.Add(new GateDefinition(id, Version, buildPhase, ProofScope.Static, req));
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
                    RequestedPhase = GatePhase.QaPass,
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
                    if ((rd.Value == Requirement.NotApplicable || rd.Value == Requirement.Unknown) &&
                        string.IsNullOrWhiteSpace(rd.Reason))
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

        internal static readonly Func<EvaluationContext, RequirementDecision> AndroidRequiredElseNotApplicable = ctx =>
            ctx.Platform == EvalPlatform.Unknown ? Unk("build platform is unknown")
            : ctx.Platform == EvalPlatform.Android ? Req("Android-only device transport")
            : Na("no device transport on this platform");

        internal static readonly Func<EvaluationContext, RequirementDecision> AndroidOptionalElseNotApplicable = ctx =>
            ctx.Platform == EvalPlatform.Unknown ? Unk("build platform is unknown")
            : ctx.Platform == EvalPlatform.Android ? Opt("Android device fact")
            : Na("no device transport on this platform");
    }
}
