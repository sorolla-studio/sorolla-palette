using System.Collections.Generic;
using System.Linq;
using Sorolla.Palette.Health;
using UnityEditor;

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

        /// <summary>Installed modules from the SDK's own installation detection (manifest/assembly aware),
        /// the same source Build Health's own checks consult.</summary>
        internal static SdkModule DetectInstalledModules()
        {
            SdkModule modules = SdkModule.None;
            if (SdkDetector.IsInstalled(SdkId.GameAnalytics)) modules |= SdkModule.GameAnalytics;
            if (SdkDetector.IsInstalled(SdkId.Facebook)) modules |= SdkModule.Facebook;
            if (SdkDetector.IsInstalled(SdkId.AppLovinMAX)) modules |= SdkModule.AppLovinMax;
            if (SdkDetector.IsInstalled(SdkId.Adjust)) modules |= SdkModule.Adjust;
            if (SdkDetector.IsInstalled(SdkId.FirebaseApp) || SdkDetector.IsInstalled(SdkId.FirebaseAnalytics))
                modules |= SdkModule.Firebase;
            return modules;
        }

        internal static EvaluationContext BuildContext() => new EvaluationContext
        {
            Mode = ToEvalMode(SorollaSettings.Mode),
            Platform = ToEvalPlatform(EditorUserBuildSettings.activeBuildTarget),
            InstalledModules = DetectInstalledModules(),
        };

        // ── Observations ──────────────────────────────────────────────

        internal static List<GateObservation> BuildObservations(
            List<BuildValidator.ValidationResult> buildHealthResults,
            GreenlightDeviceSnapshot.State snapshotState,
            GreenlightManualChecklist.State checklist)
        {
            var observations = new List<GateObservation>();
            observations.AddRange(BuildHealthObservations(buildHealthResults));
            observations.AddRange(GreenlightDeviceSnapshot.ToObservations(snapshotState));
            observations.AddRange(GreenlightManualChecklist.ToObservations(checklist));
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
                if (!CategoryToGateId.TryGetValue(group.Key, out string gateId))
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
                };
            }
        }

        // Error is the most severe row we surface, then unverifiable (missing evidence), then warning, then valid.
        static int StatusPriority(BuildValidator.ValidationStatus status) => status switch
        {
            BuildValidator.ValidationStatus.Error => 0,
            BuildValidator.ValidationStatus.Unverifiable => 1,
            BuildValidator.ValidationStatus.Warning => 2,
            _ => 3, // Valid
        };

        internal static GateOutcome ToOutcome(BuildValidator.ValidationStatus status) => status switch
        {
            BuildValidator.ValidationStatus.Error => GateOutcome.Fail,
            BuildValidator.ValidationStatus.Warning => GateOutcome.PassWithCaveats,
            BuildValidator.ValidationStatus.Unverifiable => GateOutcome.Incomplete,
            _ => GateOutcome.Pass, // Valid
        };

        static string FirstLine(string message) => string.IsNullOrEmpty(message) ? "" : message.Split('\n')[0];

        // ── Display metadata (labels + deep links, keyed by gate id) ───

        internal static string LabelFor(string gateId)
        {
            if (BuildGateLabels.TryGetValue(gateId, out string buildLabel)) return buildLabel;
            if (DeviceLabels.TryGetValue(gateId, out string deviceLabel)) return deviceLabel;
            GreenlightManualChecklist.Descriptor manual = GreenlightManualChecklist.Descriptors
                .FirstOrDefault(d => d.GateId == gateId);
            return manual?.Label ?? gateId;
        }

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
