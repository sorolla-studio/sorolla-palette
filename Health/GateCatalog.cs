using System;
using System.Collections.Generic;
using System.Linq;

namespace Sorolla.Palette.Health
{
    /// <summary>
    ///     Stable gate-id vocabulary - string constants, never magic strings. The Editor greenlight adapter
    ///     maps Build Health results onto these ids; the canonical <see cref="GateCatalog"/> owns each id's
    ///     context-derived requirement.
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
        // One gate per platform config file: they were a single build.firebase_config, and
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

        // Every gate here is machine-checkable by the Editor. Human/dashboard confirmations (store catalog,
        // vendor registration, relaunch and background/resume sessions) are not gates: their only evidence
        // was a person ticking a box, which proved nothing a report could stand behind.
        // Where they live instead: the SDK-behavior ones (relaunch persistence, background/resume) are
        // steps in Sorolla's release-candidate run on the reference game. The PER-GAME dashboard ones
        // (a studio's own GameAnalytics platform registration, its store catalog, cross-vendor delivery)
        // cannot be covered by a run on a different game - they belong to the studio, and the honest
        // surfaces for them are the vendor guides in Documentation~/dashboards/ plus any probe the SDK can
        // actually execute (see the GameAnalytics/Facebook credential probes for the shape that qualifies).
    }

    /// <summary>
    ///     The one canonical, code-defined gate catalog the SDK ships (not a ScriptableObject or YAML, so it
    ///     is grep/diff/compile-checked and has no optional-asset failure mode). Each definition
    ///     owns its per-context requirement decision (the mode requirement table lives HERE, not in the
    ///     producer). The private gates.yaml workflow references the same string
    ///     ids without any portfolio data shipping here (design note section 4). Definitions are frozen on
    ///     construction.
    /// </summary>
    internal sealed class GateCatalog
    {
        readonly IReadOnlyList<GateDefinition> _definitions;
        readonly Dictionary<string, GateDefinition> _byId;

        internal GateCatalog(IEnumerable<GateDefinition> definitions)
        {
            // Defensive copy + freeze so All and ById can never disagree and a definition list cannot be
            // mutated after construction. GateDefinition itself is immutable.
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


        static IReadOnlyList<GateDefinition> BuildCanonical()
        {
            // EVERY gate reaches EVERY report surface. The one distinction is GateDefinition.ReleaseOnly,
            // which hides NO row: it keeps the build preprocessor quiet on development builds for a check
            // that is normally unsatisfied there (the release keystore).
            var defs = new List<GateDefinition>();

            // Build Health - core SDKs, required in BOTH modes (GameAnalytics + Facebook are SdkRequirement.Core).
            AddBuild(defs, GateIds.BuildRequiredSdks, Requirements.AlwaysRequired);
            // The keys check judges the ACTIVE build target only, and fails when that platform's key pair is
            // missing. The other platform's state is carried by the vendor group caption, not by this gate.
            defs.Add(new GateDefinition(GateIds.BuildGameAnalyticsKeys,
                CapabilityPolicy.Dependent(SdkModule.GameAnalytics)));
            defs.Add(new GateDefinition(GateIds.BuildGameAnalyticsCredentials,
                CapabilityPolicy.Dependent(SdkModule.GameAnalytics)));
            // The Facebook app's platform registration is judged for the active build target only. The Graph
            // response still describes both platforms - the row just stops grading the one this build is not for.
            defs.Add(new GateDefinition(GateIds.BuildFacebookPlatform,
                CapabilityPolicy.Dependent(SdkModule.Facebook)));

            // Firebase is required in Full and validated in Prototype only when at least one Firebase module
            // is actually included. Package availability is owned by BuildRequiredSdks; dependent checks do
            // not duplicate a missing-package failure.
            defs.Add(new GateDefinition(GateIds.BuildFirebaseCoherence,
                CapabilityPolicy.FullSuiteDependent(SdkModule.Firebase)));

            // Full-mode vendors (AppLovin MAX FullRequired, Adjust FullOnly): Required in Full, Optional in
            // Prototype (evaluated only if the vendor is present). Both carry per-game credentials/ad-unit ids.
            // Only the active build target's ad unit ids are graded. Both settings gates are scoped to the
            // package-backed capability, so missing packages are owned by the root package gate.
            defs.Add(new GateDefinition(GateIds.BuildMaxSettings,
                CapabilityPolicy.Dependent(SdkModule.AppLovinMax)));
            defs.Add(new GateDefinition(GateIds.BuildAdjustSettings,
                CapabilityPolicy.Dependent(SdkModule.Adjust)));

            // Firebase config files follow the SAME requirement as Firebase itself: when
            // Firebase is required (Full), a missing google-services.json / plist must block release
            // confidence, not sit as a non-blocking advisory warning. Optional in Prototype. One gate per
            // platform, so each file gets its own row and neither can hide the other.
            // Each gate applies ONLY on its own platform: the row for the platform this build is not for
            // leaves the verdict and the counts entirely (it still prints in the copied report as NotApplicable).
            defs.Add(new GateDefinition(GateIds.BuildFirebaseConfigAndroid,
                Requirements.OnPlatform(EvalPlatform.Android,
                    CapabilityPolicy.FullSuiteDependent(SdkModule.Firebase))));
            defs.Add(new GateDefinition(GateIds.BuildFirebaseConfigIos,
                Requirements.OnPlatform(EvalPlatform.iOS,
                    CapabilityPolicy.FullSuiteDependent(SdkModule.Firebase))));

            // Keystore is Required on Android at release. ReleaseOnly: a release keystore is normally absent
            // mid-development, so the build preprocessor stays quiet about it on development builds - the ROW
            // is still shown in every window, to everyone. Adjust sandbox mode is deliberately NOT ReleaseOnly:
            // sandbox is a one-time internal check that events reach Adjust, then it goes off and stays off, so
            // any sandbox-on state is worth a console warning on every build, not just release ones.
            defs.Add(new GateDefinition(GateIds.BuildAndroidKeystore,
                Requirements.AndroidRequiredElseOptional, releaseOnly: true));
            defs.Add(new GateDefinition(GateIds.BuildAdjustSandboxMode,
                CapabilityPolicy.Dependent(SdkModule.Adjust)));

            // Advisory Build Health rows - Optional in both modes. Their OBSERVED outcome still drives
            // precedence (an error -> FAIL, a warning -> caveats); an unobserved conditional check is a real
            // OptionalSkipped, not a false pass and not a NotApplicable lie.
            //
            // BuildSdkPin: a studio pinned to a branch instead of a published tag is running an SDK line
            // Sorolla has not certified, and must see that with the fix ("pin a tagged release"). An
            // embedded/local package reports Skipped, so Sorolla's own working tree is unaffected.
            foreach (string id in new[]
            {
                GateIds.BuildSdkPin,
                GateIds.BuildSdkVersions, GateIds.BuildModeConsistency, GateIds.BuildScopedRegistries,
                GateIds.BuildConfigSync, GateIds.BuildAndroidManifest, GateIds.BuildEdm4uSettings,
                GateIds.BuildGradleConfig, GateIds.BuildVerboseLogging, GateIds.BuildDevelopmentBuild,
                GateIds.BuildGradleJavaHome, GateIds.BuildGameAnalyticsResourceWhitelist,
                GateIds.BuildAddressablesContent,
            })
                AddBuild(defs, id, Requirements.AlwaysOptional);

            return defs;

            void AddBuild(List<GateDefinition> list, string id,
                Func<EvaluationContext, RequirementDecision> req) =>
                list.Add(new GateDefinition(id, req));
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
                });
            return contexts;
        }

        /// <summary>
        ///     Fails loud on a malformed catalog. Returns the list of problems (empty = valid).
        ///     Rejects: duplicate ids; null/empty/whitespace ids; null/empty versions; missing requirement
        ///     predicate; unreachable gates (never Required/Optional under any supported context); a
        ///     non-exhaustive supported-context grid; and
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

            // The context grid must be exhaustive over the supported mode x platform axes.
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
                if (def.Requirement == null)
                    problems.Add($"Gate '{def.Id}' has no requirement predicate.");
                if (def.Requirement == null || supportedContexts == null || supportedContexts.Count == 0)
                    continue;

                bool reachable = false;
                foreach (EvaluationContext ctx in supportedContexts)
                {
                    RequirementDecision rd = def.Requirement(ctx);
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
    ///     The context-derived requirement predicates the mode requirement table is built from. Each reads
    ///     the trusted <see cref="EvaluationContext"/> only - a producer cannot self-exempt.
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

        internal static readonly Func<EvaluationContext, RequirementDecision> AndroidRequiredElseOptional = ctx =>
            ctx.Platform == EvalPlatform.Unknown ? Unk("build platform is unknown")
            : ctx.Platform == EvalPlatform.Android ? Req("Android build fact")
            : Opt("evaluated only if reported off-Android");

        /// <summary>
        ///     Scopes a gate about ONE platform's artifact to the platform it describes. A report
        ///     judges exactly one platform - the active build target - so a check about the platform NOT being
        ///     built resolves NotApplicable: out of the verdict, out of the counts, out of the rendered rows,
        ///     still printed in the copied report as an audit row. This "the active build target is the
        ///     intent" rule is what makes an iOS-only game
        ///     able to read green without any studio-declared platform list (do not reintroduce one).
        ///     Severity for the ACTIVE platform is delegated untouched to
        ///     <paramref name="whenActive"/>, so mode rules (Full blocks, Prototype advises) still apply there.
        ///     Off-mobile targets resolve NotApplicable because there is no mobile build to judge.
        /// </summary>
        internal static Func<EvaluationContext, RequirementDecision> OnPlatform(
            EvalPlatform platform, Func<EvaluationContext, RequirementDecision> whenActive) => ctx =>
            ctx.Platform == platform ? whenActive(ctx)
            : ctx.Platform == EvalPlatform.Unknown ? Na("active build target is not a mobile platform")
            : Na($"judged only on the {PlatformName(platform)} build target, which is not the active target");

        static string PlatformName(EvalPlatform platform) =>
            platform == EvalPlatform.iOS ? "iOS" : platform == EvalPlatform.Android ? "Android" : "unknown";
    }
}
