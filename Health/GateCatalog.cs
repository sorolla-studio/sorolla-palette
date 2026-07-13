using System;
using System.Collections.Generic;
using System.Linq;

namespace Sorolla.Palette.Health
{
    /// <summary>
    ///     Stable gate-id vocabulary - string constants, never magic strings. Producers (the Editor
    ///     greenlight adapter, the on-device path) map their own evidence rows onto these ids; the
    ///     canonical <see cref="GateCatalog"/> owns each id's applicability, required-ness and proof scope.
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
    ///     owns its per-context applicability, required-ness and required proof scope: the mode requirement
    ///     table lives HERE, not in the producer. The private gates.yaml workflow references the same string
    ///     ids without any portfolio data shipping here (design note section 4).
    /// </summary>
    internal sealed class GateCatalog
    {
        readonly IReadOnlyList<GateDefinition> _definitions;
        readonly Dictionary<string, GateDefinition> _byId;

        internal GateCatalog(IReadOnlyList<GateDefinition> definitions)
        {
            _definitions = definitions ?? Array.Empty<GateDefinition>();
            _byId = _definitions.ToDictionary(d => d.Id, d => d);
        }

        internal IReadOnlyList<GateDefinition> All => _definitions;

        /// <summary>Looks a definition up by id. Throws on an unknown id by default (no silent null); pass
        /// <paramref name="throwIfMissing"/> = false to probe.</summary>
        internal GateDefinition ById(string id, bool throwIfMissing = true)
        {
            if (_byId.TryGetValue(id, out GateDefinition def))
                return def;
            if (throwIfMissing)
                throw new KeyNotFoundException($"No gate definition with id '{id}' in the catalog.");
            return null;
        }

        /// <summary>The shipped canonical catalog: the complete mode requirement table.</summary>
        internal static GateCatalog Canonical { get; } = new GateCatalog(BuildCanonical());

        /// <summary>
        ///     Every mode x platform x profile combination the SDK supports, used by <see cref="Validate"/>
        ///     (a gate applicable under none of these is unreachable) and by the requirement-table tests. All
        ///     modules installed so a module-gated definition is reachable in at least one context.
        /// </summary>
        internal static IReadOnlyList<EvaluationContext> SupportedContexts { get; } = BuildSupportedContexts();

        // ── The mode requirement table ────────────────────────────────────

        const string Version = "1"; // per-gate semantic version (R3-03); bump a single gate when its meaning changes.

        static IReadOnlyList<GateDefinition> BuildCanonical()
        {
            var defs = new List<GateDefinition>();

            // Build Health - core SDKs, required in BOTH modes (GameAnalytics + Facebook are SdkRequirement.Core).
            AddBuild(defs, GateIds.BuildRequiredSdks, required: true, Applicabilities.Always);
            AddBuild(defs, GateIds.BuildGameAnalyticsKeys, required: true, Applicabilities.Always);
            AddBuild(defs, GateIds.BuildGameAnalyticsCredentials, required: true, Applicabilities.Always);
            AddBuild(defs, GateIds.BuildFacebookPlatform, required: true, Applicabilities.Always);

            // Firebase coherence - THE decided contradiction. SdkRegistry marks every Firebase module
            // FullRequired ("optional in Prototype, never uninstalled"), so Firebase is required in Full and
            // applicable in Prototype only when actually installed (if you ship it, it must be coherent).
            AddBuild(defs, GateIds.BuildFirebaseCoherence, required: true,
                Applicabilities.FullOrModule(SdkModule.Firebase, "Firebase"));

            // Full-mode vendors (AppLovin MAX FullRequired, Adjust FullOnly): applicable in Full, or in
            // Prototype only if the module is present.
            AddBuild(defs, GateIds.BuildMaxSettings, required: true,
                Applicabilities.FullOrModule(SdkModule.AppLovinMax, "AppLovin MAX"));
            AddBuild(defs, GateIds.BuildAdjustSettings, required: true,
                Applicabilities.FullOrModule(SdkModule.Adjust, "Adjust"));
            AddBuild(defs, GateIds.BuildAdjustResolvedVersion, required: true,
                Applicabilities.FullOrModule(SdkModule.Adjust, "Adjust"));

            // Android-only build facts.
            AddBuild(defs, GateIds.BuildAndroidKeystore, required: true, Applicabilities.AndroidOnly);

            // Advisory Build Health rows - always applicable, not required (an unobserved conditional
            // check is a skip, not an omission). Their OBSERVED outcome still drives precedence:
            // an error -> FAIL, a warning -> PASS_WITH_CAVEATS.
            foreach (string id in new[]
            {
                GateIds.BuildSdkVersions, GateIds.BuildModeConsistency, GateIds.BuildScopedRegistries,
                GateIds.BuildConfigSync, GateIds.BuildAndroidManifest, GateIds.BuildEdm4uSettings,
                GateIds.BuildGradleConfig, GateIds.BuildFirebaseConfig, GateIds.BuildPrototypeModeIntent,
                GateIds.BuildVerboseLogging, GateIds.BuildDevelopmentBuild, GateIds.BuildAdjustSandboxMode,
                GateIds.BuildGradleJavaHome, GateIds.BuildGameAnalyticsResourceWhitelist,
                GateIds.BuildAddressablesContent, GateIds.BuildSdkPin,
            })
                AddBuild(defs, id, required: false, Applicabilities.Always);

            // Device snapshot - the Android QA bridge is the only transport that ships, so device facts are
            // applicable on Android only (iOS transport is out of scope). Readiness is required: a build we
            // have never confirmed on device cannot pass, it is INCOMPLETE.
            defs.Add(Gate(GateIds.DeviceReady, required: true, ProofScope.DeviceDispatch, Applicabilities.AndroidOnly));
            defs.Add(Gate(GateIds.DeviceAdvertisingId, required: false, ProofScope.DeviceDispatch, Applicabilities.AndroidOnly));
            defs.Add(Gate(GateIds.DeviceNoSdkErrors, required: false, ProofScope.DeviceDispatch, Applicabilities.AndroidOnly));

            // Manual / dashboard attestations - required, and the required proof (vendor-accepted or an
            // on-device human session) is deliberately something a legacy EditorPrefs check-off cannot
            // supply, so a ticked legacy box resolves to INCOMPLETE, never PASS (B-10).
            defs.Add(Gate(GateIds.ManualGaPlatformRegistered, required: true, ProofScope.VendorAccepted, Applicabilities.Always));
            defs.Add(Gate(GateIds.ManualCrossVendorDashboardDrift, required: true, ProofScope.VendorAccepted, Applicabilities.Always));
            defs.Add(Gate(GateIds.ManualAdjustPurchaseVerification, required: true, ProofScope.VendorAccepted, Applicabilities.FullOnly));
            defs.Add(Gate(GateIds.ManualRelaunchPersistence, required: true, ProofScope.DeviceDispatch, Applicabilities.Always));
            defs.Add(Gate(GateIds.ManualBackgroundResumeCycle, required: true, ProofScope.DeviceDispatch, Applicabilities.Always));

            return defs;
        }

        static void AddBuild(List<GateDefinition> defs, string id, bool required,
            Func<EvaluationContext, ApplicabilityVerdict> applicability) =>
            defs.Add(Gate(id, required, ProofScope.Static, applicability));

        static GateDefinition Gate(string id, bool required, ProofScope proof,
            Func<EvaluationContext, ApplicabilityVerdict> applicability) => new GateDefinition
        {
            Id = id,
            Version = Version,
            Phases = GatePhase.PreBuild | GatePhase.QaPass,
            Required = required,
            RequiredProof = proof,
            Applicability = applicability,
        };

        static IReadOnlyList<EvaluationContext> BuildSupportedContexts()
        {
            const SdkModule all = SdkModule.GameAnalytics | SdkModule.Facebook | SdkModule.Firebase |
                                  SdkModule.AppLovinMax | SdkModule.Adjust;
            var contexts = new List<EvaluationContext>();
            foreach (EvalMode mode in new[] { EvalMode.Prototype, EvalMode.Full })
            foreach (EvalPlatform platform in new[] { EvalPlatform.Android, EvalPlatform.iOS })
                contexts.Add(new EvaluationContext { Mode = mode, Platform = platform, InstalledModules = all });
            return contexts;
        }

        /// <summary>
        ///     Fails loud on a malformed catalog: a duplicate id, an unreachable definition (no phase), or a
        ///     definition that is never <see cref="Applicability.Applicable"/> under any supported context.
        ///     Returns the list of problems (empty = valid). Pure function so it is unit-testable against
        ///     synthetic catalogs; run as an Editor test over <see cref="Canonical"/>.
        /// </summary>
        internal static IReadOnlyList<string> Validate(
            IEnumerable<GateDefinition> definitions, IReadOnlyList<EvaluationContext> supportedContexts)
        {
            var problems = new List<string>();
            List<GateDefinition> defs = definitions?.ToList() ?? new List<GateDefinition>();

            foreach (IGrouping<string, GateDefinition> group in defs.GroupBy(d => d.Id))
                if (group.Count() > 1)
                    problems.Add($"Duplicate gate id: '{group.Key}' ({group.Count()} definitions)");

            foreach (GateDefinition def in defs)
            {
                if (def.Phases == GatePhase.None)
                    problems.Add($"Unreachable gate '{def.Id}': no phase (Phases == None)");

                if (def.Applicability != null && supportedContexts != null && supportedContexts.Count > 0 &&
                    supportedContexts.All(ctx => def.Applicability(ctx).Value != Applicability.Applicable))
                    problems.Add($"Unreachable gate '{def.Id}': never Applicable under any supported context");
            }

            return problems;
        }
    }

    /// <summary>
    ///     The applicability predicates the mode requirement table is built from. Each reads the trusted
    ///     <see cref="EvaluationContext"/> only - a producer cannot self-exempt. Unknown mode/platform
    ///     resolves to <see cref="Applicability.Unknown"/> (→ INCOMPLETE), never a silent skip.
    /// </summary>
    internal static class Applicabilities
    {
        internal static readonly Func<EvaluationContext, ApplicabilityVerdict> Always =
            _ => new ApplicabilityVerdict(Applicability.Applicable);

        internal static readonly Func<EvaluationContext, ApplicabilityVerdict> FullOnly = ctx =>
            ctx.Mode == EvalMode.Unknown
                ? new ApplicabilityVerdict(Applicability.Unknown, "SDK mode is unknown (no config)")
                : ctx.Mode == EvalMode.Full
                    ? new ApplicabilityVerdict(Applicability.Applicable)
                    : new ApplicabilityVerdict(Applicability.NotApplicable, "Prototype mode: not required");

        internal static readonly Func<EvaluationContext, ApplicabilityVerdict> AndroidOnly = ctx =>
            ctx.Platform == EvalPlatform.Unknown
                ? new ApplicabilityVerdict(Applicability.Unknown, "Build platform is unknown")
                : ctx.Platform == EvalPlatform.Android
                    ? new ApplicabilityVerdict(Applicability.Applicable)
                    : new ApplicabilityVerdict(Applicability.NotApplicable, "Not applicable on this platform");

        /// <summary>Applicable in Full mode, or in Prototype only when the module is actually installed
        /// (a shipped module must be coherent even in Prototype). NotApplicable in a bare Prototype.</summary>
        internal static Func<EvaluationContext, ApplicabilityVerdict> FullOrModule(SdkModule module, string label) => ctx =>
        {
            if (ctx.Mode == EvalMode.Unknown)
                return new ApplicabilityVerdict(Applicability.Unknown, "SDK mode is unknown (no config)");
            if (ctx.Mode == EvalMode.Full)
                return new ApplicabilityVerdict(Applicability.Applicable);
            return (ctx.InstalledModules & module) != 0
                ? new ApplicabilityVerdict(Applicability.Applicable)
                : new ApplicabilityVerdict(Applicability.NotApplicable, $"Prototype mode without {label}: optional");
        };
    }
}
