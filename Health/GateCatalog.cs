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
        // One gate per platform config file (2026-07-22): they were a single build.firebase_config, and
        // the producer collapses a category to its worst result, so a project with a good
        // google-services.json and a missing GoogleService-Info.plist could only ever narrate one of them.
        public const string BuildFirebaseConfigAndroid = "build.firebase_config_android";
        public const string BuildFirebaseConfigIos = "build.firebase_config_ios";
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
        public const string DeviceNoSdkErrors = "device.no_sdk_errors";

        // Every gate here is machine-checkable: the SDK observes it from the repo or from a live device
        // snapshot. Human/dashboard confirmations (store catalog, vendor registration, relaunch and
        // background/resume sessions) were deleted 2026-07-22 with the attestation mechanism - their only
        // evidence was a person ticking a box, which proved nothing a report could stand behind.
        // Where they went, precisely: the SDK-behavior ones (relaunch persistence, background/resume) are
        // steps in Sorolla's release-candidate run on the reference game. The PER-GAME dashboard ones
        // (a studio's own GameAnalytics platform registration, its store catalog, cross-vendor delivery)
        // cannot be covered by a run on a different game - they belong to the studio, and the honest
        // surfaces for them are the vendor guides in Documentation~/dashboards/ plus any probe the SDK can
        // actually execute (see the GameAnalytics/Facebook credential probes for the shape that qualifies).
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
        // their agreement counts (F1 device applicability now targets-based; F5 store/tracking split).
        const string V2 = "2";

        static IReadOnlyList<GateDefinition> BuildCanonical()
        {
            // EVERY gate reaches EVERY report surface. The old phase axis (PreBuild/QaPass/ReleaseShip) was
            // deleted 2026-07-22: it selected gates by which window asked, so a studio never saw the
            // store-submission checks. That rested on "release approval is not delegated to studios", which is
            // false - studios submit their own games, and a studio that ships in Adjust sandbox loses its live
            // attribution data with nothing anywhere having told it. The only surviving distinction is
            // GateDefinition.ReleaseOnly, which hides NO row: it just keeps the build preprocessor quiet on
            // development builds for a check that is normally unsatisfied there (the release keystore).
            var defs = new List<GateDefinition>();

            // Build Health - core SDKs, required in BOTH modes (GameAnalytics + Facebook are SdkRequirement.Core).
            // required_sdks is a repo-shape rule (Structural); the credential/key rows are per-game inputs (Variant).
            AddBuild(defs, GateIds.BuildRequiredSdks, GateClassification.Structural, Requirements.AlwaysRequired);
            // V2 (2026-07-22): the keys check now covers BOTH platforms and FAILS when the ACTIVE platform's
            // key pair is missing (was: active platform only, always a warning) - a stricter meaning, so its
            // comparison-agreement count restarts.
            defs.Add(new GateDefinition(GateIds.BuildGameAnalyticsKeys, V2, GateClassification.Variant,
                ProofScope.Static, Requirements.AlwaysRequired));
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
            // Firebase is required (Full), a missing google-services.json / plist must block release
            // confidence, not sit as a non-blocking advisory warning. Optional in Prototype. One gate per
            // platform, so both files get their own row and neither can hide the other; the active/sibling
            // asymmetry is severity, and lives in the observation (the producer demotes a non-active
            // platform's Error to a Warning), never in the requirement. Fresh ids, so the version restarts
            // at 1 - the comparison instrument has no agreement history for either.
            defs.Add(new GateDefinition(GateIds.BuildFirebaseConfigAndroid, Version, GateClassification.Variant,
                ProofScope.Static, Requirements.FullRequiredElseOptional));
            defs.Add(new GateDefinition(GateIds.BuildFirebaseConfigIos, Version, GateClassification.Variant,
                ProofScope.Static, Requirements.FullRequiredElseOptional));

            // Keystore is Required on Android at release. ReleaseOnly: a release keystore is normally absent
            // mid-development, so the build preprocessor stays quiet about it on development builds - the ROW
            // is still shown in every window, to everyone. Adjust sandbox mode is deliberately NOT ReleaseOnly:
            // sandbox is a one-time internal check that events reach Adjust, then it goes off and stays off, so
            // any sandbox-on state is worth a console warning on every build, not just release ones.
            defs.Add(new GateDefinition(GateIds.BuildAndroidKeystore, Version, GateClassification.Variant,
                ProofScope.Static, Requirements.AndroidRequiredElseOptional, releaseOnly: true));
            defs.Add(new GateDefinition(GateIds.BuildAdjustSandboxMode, Version, GateClassification.Structural,
                ProofScope.Static, Requirements.AlwaysOptional));

            // Advisory Build Health rows - Optional in both modes. Their OBSERVED outcome still drives
            // precedence (an error -> FAIL, a warning -> caveats); an unobserved conditional check is a real
            // OptionalSkipped, not a false pass and not a NotApplicable lie.
            //
            // BuildSdkPin: a studio pinned to a branch instead of a published tag is running an SDK line
            // Sorolla has not certified, and must see that with the fix ("pin a tagged release"). This is the
            // direct, studio-actionable form of the protection that used to work indirectly, by leaving
            // SDK-invariant rows INCOMPLETE on an uncertified pin; those invariant rows were the human-attested
            // gates and are gone, so the pin check now carries it alone. An embedded/local package reports
            // Skipped, so Sorolla's own working tree is unaffected.
            foreach (string id in new[]
            {
                GateIds.BuildSdkPin,
                GateIds.BuildSdkVersions, GateIds.BuildModeConsistency, GateIds.BuildScopedRegistries,
                GateIds.BuildConfigSync, GateIds.BuildAndroidManifest, GateIds.BuildEdm4uSettings,
                GateIds.BuildGradleConfig, GateIds.BuildVerboseLogging, GateIds.BuildDevelopmentBuild,
                GateIds.BuildGradleJavaHome, GateIds.BuildGameAnalyticsResourceWhitelist,
                GateIds.BuildAddressablesContent,
            })
                AddBuild(defs, id, GateClassification.Structural, Requirements.AlwaysOptional);

            // Device snapshot: applicability follows the ACTIVE build platform - the target the studio is
            // building for IS its intent, so there is nothing extra to declare. Whether SDK errors appear
            // depends on how the game uses the SDK, so a reference-game zero certifies nothing for the next
            // game → Variant (2026-07-20 scope-lens ruling).
            defs.Add(new GateDefinition(GateIds.DeviceNoSdkErrors, V2, GateClassification.Variant,
                ProofScope.DeviceDispatch, Requirements.MobilePlatformRequiredElseNotApplicable));

            return defs;

            void AddBuild(List<GateDefinition> list, string id, GateClassification classification,
                Func<EvaluationContext, RequirementDecision> req) =>
                list.Add(new GateDefinition(id, Version, classification, ProofScope.Static, req));
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
        ///     predicate; undefined proof flag bits; unreachable gates (never Required/Optional under any
        ///     supported context); a non-exhaustive supported-context grid; and
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

        internal static readonly Func<EvaluationContext, RequirementDecision> AndroidRequiredElseOptional = ctx =>
            ctx.Platform == EvalPlatform.Unknown ? Unk("build platform is unknown")
            : ctx.Platform == EvalPlatform.Android ? Req("Android build fact")
            : Opt("evaluated only if reported off-Android");

        // Device evidence applies on the platform being BUILT for - the active build target is the studio's
        // intent, so nothing extra is declared. Off-mobile (desktop/editor targets) there is no device to
        // observe, so the gate is NotApplicable rather than a permanent INCOMPLETE.
        internal static readonly Func<EvaluationContext, RequirementDecision> MobilePlatformRequiredElseNotApplicable = ctx =>
            ctx.Platform == EvalPlatform.Android || ctx.Platform == EvalPlatform.iOS
                ? Req("device evidence required on the active mobile build target")
                : Na("active build target is not a mobile platform");
    }
}
