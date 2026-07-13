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
    ///     Maps the Editor greenlight's evidence classes (Build Health results, the device snapshot, the
    ///     manual checklist) onto the neutral <see cref="Sorolla.Palette.Health"/> model, and builds the
    ///     trusted <see cref="EvaluationContext"/>, so the single <see cref="HealthEvaluator.Evaluate"/> owns
    ///     the verdict. The adapter is the trusted context evaluator for the Editor path: it never decides a
    ///     gate's applicability or required proof (those live on the catalog) - it only reports what it
    ///     observed. Display metadata (human labels, dashboard deep links) is a side channel here; the
    ///     observation carries only evidence + fix.
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
            if (HasPackage(dependencies, SdkId.FirebaseApp) || HasPackage(dependencies, SdkId.FirebaseAnalytics) ||
                HasPackage(dependencies, SdkId.FirebaseCrashlytics) || HasPackage(dependencies, SdkId.FirebaseRemoteConfig))
                modules |= SdkModule.Firebase;
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
                // The greenlight is the studio's QA-pass self-check; the evaluator selects gates for this phase.
                RequestedPhase = GatePhase.QaPass,
            };
        }

        // ── Observations ──────────────────────────────────────────────

        /// <summary>
        ///     Builds the neutral observations. Producer-side context guards ensure it never fabricates
        ///     evidence for a gate the context makes NotApplicable (which would be a C3-05 context mismatch):
        ///     device observations are emitted only on Android (the adb bridge is Android-only), and the
        ///     Adjust purchase-verification manual row only in Full mode (no Adjust in Prototype). These are
        ///     facts about which evidence EXISTS, not requirement decisions - the catalog still owns those.
        /// </summary>
        internal static List<GateObservation> BuildObservations(
            EvaluationContext context,
            List<BuildValidator.ValidationResult> buildHealthResults,
            GreenlightDeviceSnapshot.State snapshotState,
            GreenlightManualChecklist.State checklist)
        {
            var observations = new List<GateObservation>();
            observations.AddRange(BuildHealthObservations(buildHealthResults));

            if (context.Platform == EvalPlatform.Android)
                observations.AddRange(GreenlightDeviceSnapshot.ToObservations(snapshotState));

            foreach (GateObservation manual in GreenlightManualChecklist.ToObservations(checklist))
            {
                if (manual.GateId == GateIds.ManualAdjustPurchaseVerification && context.Mode != EvalMode.Full)
                    continue; // no Adjust in Prototype - the gate is NotApplicable there
                observations.Add(manual);
            }

            return observations;
        }

        /// <summary>One observation per Build Health category actually produced (worst status wins when a
        /// category emits several results), keyed to the per-category gate id. Categories with no gate id
        /// are skipped. Proof scope is Static - Build Health is an editor-time check.</summary>
        static IEnumerable<GateObservation> BuildHealthObservations(List<BuildValidator.ValidationResult> results)
        {
            if (results == null)
                yield break; // Build Health never ran: the required core gates omit -> INCOMPLETE.

            IEnumerable<IGrouping<BuildValidator.CheckCategory, BuildValidator.ValidationResult>> byCategory =
                results.GroupBy(r => r.Category);

            foreach (var group in byCategory)
            {
                // An unmapped category must not silently disappear (review C4-09): emit it under a sentinel
                // id so the evaluator flags it as an unknown-id validation error, visible + fail-closed.
                bool mapped = CategoryToGateId.TryGetValue(group.Key, out string gateId);
                if (!mapped)
                    gateId = "unmapped:" + group.Key;

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
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Undefined ValidationStatus."),
        };

        internal static GateOutcome ToOutcome(BuildValidator.ValidationStatus status) => status switch
        {
            BuildValidator.ValidationStatus.Error => GateOutcome.Fail,
            BuildValidator.ValidationStatus.Warning => GateOutcome.PassWithCaveats,
            BuildValidator.ValidationStatus.Unverifiable => GateOutcome.Incomplete,
            BuildValidator.ValidationStatus.Valid => GateOutcome.Pass,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Undefined ValidationStatus."),
        };

        static string FirstLine(string message) => string.IsNullOrEmpty(message) ? "" : message.Split('\n')[0];

        // ── Display metadata (labels + deep links, keyed by gate id) ───

        internal static string LabelFor(string gateId)
        {
            if (BuildGateLabels.TryGetValue(gateId, out string buildLabel)) return buildLabel;
            if (DeviceLabels.TryGetValue(gateId, out string deviceLabel)) return deviceLabel;
            if (MiscLabels.TryGetValue(gateId, out string miscLabel)) return miscLabel;
            GreenlightManualChecklist.Descriptor manual = GreenlightManualChecklist.Descriptors
                .FirstOrDefault(d => d.GateId == gateId);
            return manual?.Label ?? gateId;
        }

        static readonly Dictionary<string, string> MiscLabels = new Dictionary<string, string>
        {
            [GateIds.IapStoreConfigured] = "IAP Store / Purchase Verification",
        };

        internal static (string url, string label) DeepLinkFor(string gateId)
        {
            GreenlightManualChecklist.Descriptor manual = GreenlightManualChecklist.Descriptors
                .FirstOrDefault(d => d.GateId == gateId);
            return manual != null && !string.IsNullOrEmpty(manual.DeepLinkUrl)
                ? (manual.DeepLinkUrl, "Open Dashboard")
                : (null, null);
        }

        static readonly Dictionary<string, string> DeviceLabels = new Dictionary<string, string>
        {
            [GateIds.DeviceReady] = "Device Snapshot: Ready",
            [GateIds.DeviceAdvertisingId] = "Device Snapshot: Advertising ID",
            [GateIds.DeviceNoSdkErrors] = "Device Snapshot: SDK Errors",
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
                [BuildValidator.CheckCategory.FirebaseConfig] = GateIds.BuildFirebaseConfig,
                [BuildValidator.CheckCategory.GameAnalyticsSettings] = GateIds.BuildGameAnalyticsKeys,
                [BuildValidator.CheckCategory.FacebookPlatformConfig] = GateIds.BuildFacebookPlatform,
                [BuildValidator.CheckCategory.PrototypeModeIntent] = GateIds.BuildPrototypeModeIntent,
                [BuildValidator.CheckCategory.VerboseLogging] = GateIds.BuildVerboseLogging,
                [BuildValidator.CheckCategory.DevelopmentBuild] = GateIds.BuildDevelopmentBuild,
                [BuildValidator.CheckCategory.AdjustSandboxMode] = GateIds.BuildAdjustSandboxMode,
                [BuildValidator.CheckCategory.AndroidKeystore] = GateIds.BuildAndroidKeystore,
                [BuildValidator.CheckCategory.GradleJavaHome] = GateIds.BuildGradleJavaHome,
                [BuildValidator.CheckCategory.GameAnalyticsResourceWhitelist] = GateIds.BuildGameAnalyticsResourceWhitelist,
                [BuildValidator.CheckCategory.AddressablesContent] = GateIds.BuildAddressablesContent,
                [BuildValidator.CheckCategory.SdkPin] = GateIds.BuildSdkPin,
                [BuildValidator.CheckCategory.AdjustResolvedVersion] = GateIds.BuildAdjustResolvedVersion,
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
    }
}
