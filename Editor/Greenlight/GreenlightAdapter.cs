using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sorolla.Palette.Health;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor.Greenlight
{
    /// <summary>
    ///     Maps the Editor greenlight's evidence classes (Build Health results and the device snapshot) onto
    ///     the neutral <see cref="Sorolla.Palette.Health"/> model, and builds the trusted
    ///     <see cref="EvaluationContext"/>, so the single <see cref="HealthEvaluator.Evaluate"/> owns the
    ///     verdict. The adapter is the trusted context evaluator for the Editor path: it never decides a
    ///     gate's applicability or required proof (those live on the catalog) - it only reports what it
    ///     observed, and every observation it can produce is machine-derived. Display metadata (human labels)
    ///     is a side channel here; the observation carries only evidence + fix.
    /// </summary>
    static class GreenlightAdapter
    {
        // ── Context ───────────────────────────────────────────────────

        internal static EvalMode ToEvalMode(SorollaMode mode) => mode switch
        {
            SorollaMode.Prototype => EvalMode.Prototype,
            SorollaMode.Full => EvalMode.Full,
            _ => EvalMode.Unknown, // None / no config
        };

        internal static EvalPlatform ToEvalPlatform(BuildTarget target) => target switch
        {
            BuildTarget.Android => EvalPlatform.Android,
            BuildTarget.iOS => EvalPlatform.iOS,
            _ => EvalPlatform.Unknown,
        };

        /// <summary>
        ///     Installed modules from the package manifest (the SDK's source of truth for package state -
        ///     assembly detection is unsafe during domain reloads, review C4-02). Returns false when the
        ///     manifest is missing/unreadable so the caller can force INCOMPLETE rather than treat a
        ///     temporarily-absent package as uninstalled.
        /// </summary>
        internal static bool TryDetectInstalledModules(out SdkModule modules)
        {
            modules = SdkModule.None;
            Dictionary<string, object> dependencies = ReadManifestDependencies();
            if (dependencies == null)
                return false;

            if (HasPackage(dependencies, SdkId.GameAnalytics)) modules |= SdkModule.GameAnalytics;
            if (HasPackage(dependencies, SdkId.Facebook)) modules |= SdkModule.Facebook;
            if (HasPackage(dependencies, SdkId.AppLovinMAX)) modules |= SdkModule.AppLovinMax;
            if (HasPackage(dependencies, SdkId.Adjust)) modules |= SdkModule.Adjust;
            if (HasPackage(dependencies, SdkId.FirebaseApp)) modules |= SdkModule.FirebaseApp;
            if (HasPackage(dependencies, SdkId.FirebaseAnalytics)) modules |= SdkModule.FirebaseAnalytics;
            if (HasPackage(dependencies, SdkId.FirebaseCrashlytics)) modules |= SdkModule.FirebaseCrashlytics;
            if (HasPackage(dependencies, SdkId.FirebaseRemoteConfig)) modules |= SdkModule.FirebaseRemoteConfig;
            // Unity IAP is not in SdkRegistry (it is a Unity-owned package), so match its package id directly.
            if (dependencies.ContainsKey("com.unity.purchasing")) modules |= SdkModule.UnityIap;
            return true;
        }

        static bool HasPackage(Dictionary<string, object> dependencies, SdkId id) =>
            SdkRegistry.All.TryGetValue(id, out SdkInfo info) && dependencies.ContainsKey(info.PackageId);

        static Dictionary<string, object> ReadManifestDependencies()
        {
            try
            {
                string path = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
                if (!File.Exists(path))
                    return null;
                return MiniJson.Deserialize(File.ReadAllText(path)) is Dictionary<string, object> manifest &&
                       manifest.TryGetValue("dependencies", out object deps) && deps is Dictionary<string, object> d
                    ? d
                    : null;
            }
            catch
            {
                return null;
            }
        }

        internal static EvaluationContext BuildContext()
        {
            bool resolved = TryDetectInstalledModules(out SdkModule modules);
            return new EvaluationContext
            {
                Mode = ToEvalMode(SorollaSettings.Mode),
                Platform = ToEvalPlatform(EditorUserBuildSettings.activeBuildTarget),
                InstalledModules = modules,
                ModulesResolved = resolved,
            };
        }

        /// <summary>
        ///     True when this Build Health category's gate asks a question only a store submission answers, so
        ///     it is normally unsatisfied during development. The catalog's <see cref="GateDefinition.ReleaseOnly"/>
        ///     flag is the single source of truth - this asks it rather than keeping a second list of category
        ///     names. Used ONLY by the build preprocessor, to keep such a warning off development builds; it
        ///     never hides a row from any window (2026-07-22: every gate reaches every surface).
        /// </summary>
        internal static bool IsReleaseOnly(BuildValidator.CheckCategory category) =>
            CategoryToGateId.TryGetValue(category, out string gateId) &&
            GateCatalog.Canonical.ById(gateId, throwIfMissing: false) is GateDefinition def &&
            def.ReleaseOnly;

        // ── Observations ──────────────────────────────────────────────

        /// <summary>
        ///     Builds the neutral observations. Producer-side context guards ensure it never fabricates
        ///     evidence for a gate the context makes NotApplicable (which would be a C3-05 context mismatch):
        ///     device observations are emitted only on a platform that has a shipping transport (Android via
        ///     adb, iOS via iproxy - F10). These are facts about which evidence EXISTS, not requirement
        ///     decisions - the catalog still owns those.
        /// </summary>
        internal static List<GateObservation> BuildObservations(
            EvaluationContext context,
            List<BuildValidator.ValidationResult> buildHealthResults,
            GreenlightDeviceSnapshot.State snapshotState)
        {
            var observations = new List<GateObservation>();
            observations.AddRange(BuildHealthObservations(buildHealthResults, context));

            // Emit device evidence on any platform that has a shipping snapshot collector. Both mobile
            // transports ship now: Android over `adb forward`, iOS over `iproxy` (libimobiledevice USB). Off
            // mobile the device gates are NotApplicable, so emitting there would be a C3-05 mismatch; a mobile
            // target that is never connected still emits a not-connected observation → INCOMPLETE.
            if (context.Platform == EvalPlatform.Android || context.Platform == EvalPlatform.iOS)
                observations.AddRange(GreenlightDeviceSnapshot.ToObservations(snapshotState));

            return observations;
        }

        /// <summary>Vendor-coherence categories whose "not installed" result is vendor ABSENCE, not evidence
        /// of health. When the manifest says the module is absent, the review requires that absence to be an
        /// OptionalSkipped (bare Prototype), not an affirmative PASS (F4-02) - so the adapter emits no
        /// observation for these when the module is not installed, letting the Optional gate skip and the
        /// Required (Full) gate omit → INCOMPLETE.</summary>
        static readonly Dictionary<BuildValidator.CheckCategory, SdkModule> VendorCategoryModule =
            new Dictionary<BuildValidator.CheckCategory, SdkModule>
            {
                [BuildValidator.CheckCategory.FirebaseCoherence] = SdkModule.Firebase,
                [BuildValidator.CheckCategory.FirebaseConfigAndroid] = SdkModule.Firebase,
                [BuildValidator.CheckCategory.FirebaseConfigIos] = SdkModule.Firebase,
                [BuildValidator.CheckCategory.MaxSettings] = SdkModule.AppLovinMax,
                [BuildValidator.CheckCategory.AdjustSettings] = SdkModule.Adjust,
                [BuildValidator.CheckCategory.AdjustSandboxMode] = SdkModule.Adjust,
                // Extended to GameAnalytics/Facebook (product-audit finding F5, 2026-07-21): the same
                // "not installed" result those categories emit is vendor absence, not evidence, exactly
                // like Firebase/MAX/Adjust above - without this the canonical export could show
                // `[Pass] build.gameanalytics_keys - "GameAnalytics not installed"` right beside a failing
                // Required SDKs gate, contradicting the report's own absence-is-not-evidence rule.
                [BuildValidator.CheckCategory.GameAnalyticsSettings] = SdkModule.GameAnalytics,
                [BuildValidator.CheckCategory.GameAnalyticsResourceWhitelist] = SdkModule.GameAnalytics,
                [BuildValidator.CheckCategory.GameAnalyticsCredentialProbe] = SdkModule.GameAnalytics,
                [BuildValidator.CheckCategory.FacebookPlatformConfig] = SdkModule.Facebook,
            };

        /// <summary>One observation per Build Health category actually produced (worst status wins when a
        /// category emits several results), keyed to the per-category gate id. Categories with no gate id
        /// are skipped. Proof scope is Static - Build Health is an editor-time check.</summary>
        static IEnumerable<GateObservation> BuildHealthObservations(
            List<BuildValidator.ValidationResult> results, EvaluationContext context)
        {
            if (results == null)
                yield break; // Build Health never ran: the required core gates omit -> INCOMPLETE.

            IEnumerable<IGrouping<BuildValidator.CheckCategory, BuildValidator.ValidationResult>> byCategory =
                results.GroupBy(r => r.Category);

            foreach (var group in byCategory)
            {
                // F4-02: a vendor "not installed" result is absence, not affirmative evidence - drop it so the
                // gate skips (Optional) or omits (Required) instead of passing on absence.
                if (VendorCategoryModule.TryGetValue(group.Key, out SdkModule module) &&
                    (context.InstalledModules & module) == 0)
                    continue;

                // An unmapped category must not silently disappear (review C4-09): emit it under a sentinel
                // id so the evaluator flags it as an unknown-id validation error, visible + fail-closed.
                bool mapped = CategoryToGateId.TryGetValue(group.Key, out string gateId);
                if (!mapped)
                    gateId = "unmapped:" + group.Key;
                else if (GateCatalog.Canonical.ById(gateId).Requirement(context).Value ==
                         Requirement.NotApplicable)
                    continue;

                BuildValidator.ValidationResult worst = group
                    .OrderBy(r => StatusPriority(r.Status))
                    .First();

                yield return new GateObservation
                {
                    GateId = gateId,
                    Outcome = ToOutcome(worst.Status),
                    ObservedProof = ProofScope.Static,
                    Evidence = FirstLine(worst.Message),
                    FixHint = worst.Fix,
                    // A deliberate skip (F5 residual, 2026-07-21 audit review) must render/export as
                    // neutral end-to-end, not collapse into an affirmative Pass once it reaches a gate row -
                    // Outcome above still maps to Pass for aggregation (non-blocking), this flag is the
                    // separate signal frontends/export use to label it correctly.
                    Informational = worst.Status == BuildValidator.ValidationStatus.Skipped,
                };
            }
        }

        // Error is the most severe row we surface, then unverifiable (missing evidence), then warning, then
        // valid. No permissive default - an undefined ValidationStatus fails closed (review C4-09).
        static int StatusPriority(BuildValidator.ValidationStatus status) => status switch
        {
            BuildValidator.ValidationStatus.Error => 0,
            BuildValidator.ValidationStatus.Unverifiable => 1,
            BuildValidator.ValidationStatus.Warning => 2,
            BuildValidator.ValidationStatus.Valid => 3,
            // Least severe: a deliberate skip (vendor absent, wrong platform/profile) never outranks an
            // actual pass, error, warning, or pending check in the same category (F5, 2026-07-21).
            BuildValidator.ValidationStatus.Skipped => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Undefined ValidationStatus."),
        };

        internal static GateOutcome ToOutcome(BuildValidator.ValidationStatus status) => status switch
        {
            BuildValidator.ValidationStatus.Error => GateOutcome.Fail,
            BuildValidator.ValidationStatus.Warning => GateOutcome.PassWithCaveats,
            BuildValidator.ValidationStatus.Unverifiable => GateOutcome.Incomplete,
            BuildValidator.ValidationStatus.Valid => GateOutcome.Pass,
            // A skip is non-blocking, same gate outcome as a pass (F5) - only the CheckRow-level display
            // (Build Health row list) distinguishes it as a neutral Info notice rather than a green check;
            // it does not change the gate's verdict contribution.
            BuildValidator.ValidationStatus.Skipped => GateOutcome.Pass,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Undefined ValidationStatus."),
        };

        static string FirstLine(string message) => string.IsNullOrEmpty(message) ? "" : message.Split('\n')[0];

        // ── Display metadata (human labels, keyed by gate id) ───

        internal static string LabelFor(string gateId)
        {
            if (BuildGateLabels.TryGetValue(gateId, out string buildLabel)) return buildLabel;
            return DeviceLabels.TryGetValue(gateId, out string deviceLabel) ? deviceLabel : gateId;
        }

        static readonly Dictionary<string, string> DeviceLabels = new Dictionary<string, string>
        {
            [GateIds.DeviceVitals] = "Device Vitals",
        };

        static readonly Dictionary<BuildValidator.CheckCategory, string> CategoryToGateId =
            new Dictionary<BuildValidator.CheckCategory, string>
            {
                [BuildValidator.CheckCategory.RequiredSdks] = GateIds.BuildRequiredSdks,
                [BuildValidator.CheckCategory.VersionMismatches] = GateIds.BuildSdkVersions,
                [BuildValidator.CheckCategory.ModeConsistency] = GateIds.BuildModeConsistency,
                [BuildValidator.CheckCategory.ScopedRegistries] = GateIds.BuildScopedRegistries,
                [BuildValidator.CheckCategory.FirebaseCoherence] = GateIds.BuildFirebaseCoherence,
                [BuildValidator.CheckCategory.ConfigSync] = GateIds.BuildConfigSync,
                [BuildValidator.CheckCategory.AndroidManifest] = GateIds.BuildAndroidManifest,
                [BuildValidator.CheckCategory.MaxSettings] = GateIds.BuildMaxSettings,
                [BuildValidator.CheckCategory.AdjustSettings] = GateIds.BuildAdjustSettings,
                [BuildValidator.CheckCategory.Edm4uSettings] = GateIds.BuildEdm4uSettings,
                [BuildValidator.CheckCategory.GradleConfig] = GateIds.BuildGradleConfig,
                [BuildValidator.CheckCategory.FirebaseConfigAndroid] = GateIds.BuildFirebaseConfigAndroid,
                [BuildValidator.CheckCategory.FirebaseConfigIos] = GateIds.BuildFirebaseConfigIos,
                [BuildValidator.CheckCategory.GameAnalyticsSettings] = GateIds.BuildGameAnalyticsKeys,
                [BuildValidator.CheckCategory.FacebookPlatformConfig] = GateIds.BuildFacebookPlatform,
                [BuildValidator.CheckCategory.VerboseLogging] = GateIds.BuildVerboseLogging,
                [BuildValidator.CheckCategory.DevelopmentBuild] = GateIds.BuildDevelopmentBuild,
                [BuildValidator.CheckCategory.AdjustSandboxMode] = GateIds.BuildAdjustSandboxMode,
                [BuildValidator.CheckCategory.AndroidKeystore] = GateIds.BuildAndroidKeystore,
                [BuildValidator.CheckCategory.GradleJavaHome] = GateIds.BuildGradleJavaHome,
                [BuildValidator.CheckCategory.GameAnalyticsResourceWhitelist] = GateIds.BuildGameAnalyticsResourceWhitelist,
                [BuildValidator.CheckCategory.AddressablesContent] = GateIds.BuildAddressablesContent,
                [BuildValidator.CheckCategory.SdkPin] = GateIds.BuildSdkPin,
                [BuildValidator.CheckCategory.GameAnalyticsCredentialProbe] = GateIds.BuildGameAnalyticsCredentials,
            };

        // Build gate labels reuse BuildValidator's own check names so the greenlight and the Build Health
        // section speak the same language. Declared after CategoryToGateId: static field initializers run in
        // textual order, and BuildLabelMap reads CategoryToGateId.
        static readonly Dictionary<string, string> BuildGateLabels = BuildLabelMap();

        static Dictionary<string, string> BuildLabelMap()
        {
            var map = new Dictionary<string, string>();
            foreach (KeyValuePair<BuildValidator.CheckCategory, string> pair in CategoryToGateId)
                if (BuildValidator.CheckNames.TryGetValue(pair.Key, out string name))
                    map[pair.Value] = name;
            return map;
        }

        // ── Vendor grouping (vendor-grouping cycle spec, supervisor 2026-07-21 ~13:50) ──

        /// <summary>One expandable group per vendor + two catch-alls, per the endorsed restructure. Not a
        /// second verdict/aggregation concept - purely a display bucket the window groups rows into.</summary>
        internal enum VendorGroup
        {
            GameAnalytics,
            Facebook,
            Firebase,
            AppLovinMax,
            Adjust,
            /// <summary>Every Build Health category that isn't vendor-specific: gradle/manifest/registries/
            /// config/mode/versions/logging/dev-build.</summary>
            BuildAndProject,
            /// <summary>Everything with no BuildValidator.CheckCategory at all: the device snapshot and the
            /// synthetic Report Integrity row.</summary>
            DeviceAndQa,
            /// <summary>Parked vendor (roadmap "Parking decisions" - no QA/diagnostics investment): no
            /// BuildValidator category, no gate ever routes here via GroupFor - it exists purely so the
            /// window's one Group list can carry TikTok's toggle+fields uniformly (rewrite cycle, Arthur
            /// ruling 2026-07-21 ~16:45), instead of a bespoke non-Group code path.</summary>
            TikTok,
        }

        static readonly Dictionary<BuildValidator.CheckCategory, VendorGroup> CategoryToGroup =
            new Dictionary<BuildValidator.CheckCategory, VendorGroup>
            {
                [BuildValidator.CheckCategory.GameAnalyticsSettings] = VendorGroup.GameAnalytics,
                [BuildValidator.CheckCategory.GameAnalyticsResourceWhitelist] = VendorGroup.GameAnalytics,
                [BuildValidator.CheckCategory.GameAnalyticsCredentialProbe] = VendorGroup.GameAnalytics,
                [BuildValidator.CheckCategory.FacebookPlatformConfig] = VendorGroup.Facebook,
                [BuildValidator.CheckCategory.FirebaseCoherence] = VendorGroup.Firebase,
                [BuildValidator.CheckCategory.FirebaseConfigAndroid] = VendorGroup.Firebase,
                [BuildValidator.CheckCategory.FirebaseConfigIos] = VendorGroup.Firebase,
                [BuildValidator.CheckCategory.MaxSettings] = VendorGroup.AppLovinMax,
                [BuildValidator.CheckCategory.AdjustSettings] = VendorGroup.Adjust,
                [BuildValidator.CheckCategory.AdjustSandboxMode] = VendorGroup.Adjust,
                // Everything else (RequiredSdks, VersionMismatches, ModeConsistency, ScopedRegistries,
                // ConfigSync, AndroidManifest, Edm4uSettings, GradleConfig, VerboseLogging,
                // DevelopmentBuild, AndroidKeystore, GradleJavaHome, AddressablesContent, SdkPin) falls
                // through to the BuildAndProject default below - grouping key is the category itself, not
                // an explicit enumeration, so a newly added non-vendor category lands here automatically.
            };

        /// <summary>Grouping key is the gate's existing category from the catalog/validators, never label
        /// string-matching. A gate id with no BuildValidator category at all (device.*, or the synthetic
        /// Report Integrity row's null id) is QA/device process, not vendor build state - Device &amp; QA.</summary>
        internal static VendorGroup GroupFor(string gateId)
        {
            if (string.IsNullOrEmpty(gateId) || gateId.StartsWith("device."))
                return VendorGroup.DeviceAndQa;

            foreach (KeyValuePair<BuildValidator.CheckCategory, string> pair in CategoryToGateId)
            {
                if (pair.Value != gateId) continue;
                return CategoryToGroup.TryGetValue(pair.Key, out VendorGroup group) ? group : VendorGroup.BuildAndProject;
            }

            // Unmapped build.* gate id: treat as Build & Project rather than silently dropping it - a
            // grouping bucket, not a verdict, so fail-open here is safe (the unmapped-category-id path in
            // BuildHealthObservations already makes the underlying observation fail loud if it's a truly
            // unknown gate).
            return VendorGroup.BuildAndProject;
        }
    }
}
