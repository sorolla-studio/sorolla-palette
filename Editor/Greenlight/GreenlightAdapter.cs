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

        /// <summary>
        ///     Overrides the audience the next context is built for. Sorolla-side only: it lets an internal
        ///     session review a studio game as the studio will see it (and lets the reference-game
        ///     certification run simulate a tagged pin). Null = use the resolved provenance. It can only make
        ///     a report SHOW more or less; it never changes whether an observed failure counts.
        /// </summary>
        internal static ReportProfile? ForcedProfile;

        /// <summary>Overrides the resolved certification alongside <see cref="ForcedProfile"/> (simulating a
        /// tagged pin from an embedded working tree). Null = use the resolved provenance.</summary>
        internal static SdkCertification? ForcedCertification;

        internal static EvaluationContext BuildContext()
        {
            bool resolved = TryDetectInstalledModules(out SdkModule modules);
            SdkProvenance.Origin origin = SdkProvenance.ResolveOrigin();
            ReportProfile profile = ForcedProfile ?? origin.Profile;
            SdkCertification certification = ForcedCertification ?? origin.Certification;
            string certificationEvidence = ForcedProfile == null && ForcedCertification == null
                ? origin.Evidence
                : $"{origin.Evidence} [overridden for review: profile={profile}, certification={certification}]";
            return new EvaluationContext
            {
                Profile = profile,
                Certification = certification,
                CertificationEvidence = certificationEvidence,
                Mode = ToEvalMode(SorollaSettings.Mode),
                Platform = ToEvalPlatform(EditorUserBuildSettings.activeBuildTarget),
                InstalledModules = modules,
                ModulesResolved = resolved,
                // Follow the active Build Health profile (the "Validation Profile" QaPass/Release selector in
                // the window, SorollaWindow's Build Health section) so ReleaseShip-tagged gates are reachable
                // under Release and a profile-"Skipped" check maps to the right phase, not hard-coded QaPass.
                RequestedPhase = RequestedPhaseFor(BuildValidationProfileSettings.IsRelease),
            };
        }

        /// <summary>Maps the Build Health validation profile to the health phase the greenlight requests.
        /// QaPass by default; Release makes the ReleaseShip-tagged gates reachable.</summary>
        internal static GatePhase RequestedPhaseFor(bool isRelease) =>
            isRelease ? GatePhase.ReleaseShip : GatePhase.QaPass;

        /// <summary>
        ///     Records a scoped attestation for a manual gate against the current build identity + gate version
        ///     + active phase + required proof scope, plus the tester identity, the human evidence note and (for
        ///     device-session gates) the connected build GUID (the greenlight "Attest" action, C45-06/05/C1).
        ///     <paramref name="actor"/> is the exported tester identity - null/blank falls back to the machine
        ///     username. Returns false if the gate is unknown, a device gate has no connected build to bind to,
        ///     or a vendor gate has no evidence note - the affirmation must be honest.
        /// </summary>
        internal static bool AttestManualGate(string gateId, string actor, string evidenceNote, string deviceBuildGuid)
        {
            GateDefinition def = GateCatalog.Canonical.ById(gateId, throwIfMissing: false);
            if (def == null) return false;

            bool deviceGate = (def.RequiredProof & ProofScope.DeviceDispatch) != 0;
            bool vendorGate = (def.RequiredProof & ProofScope.VendorAccepted) != 0;
            if (deviceGate && string.IsNullOrEmpty(deviceBuildGuid)) return false; // must bind to a connected build
            if (vendorGate && string.IsNullOrWhiteSpace(evidenceNote)) return false; // dashboard claim needs a note

            QaBuildIdentity id = QaBuildIdentity.Current();
            QaAttestationStore.Record(new QaAttestationRecord
            {
                schema = QaAttestationValidator.Schema,
                gateId = gateId,
                gateVersion = def.Version,
                phase = RequestedPhaseFor(BuildValidationProfileSettings.IsRelease).ToString(),
                actor = string.IsNullOrWhiteSpace(actor) ? Environment.UserName : actor.Trim(),
                timestampUtc = DateTime.UtcNow.ToString("o"),
                applicationId = id.ApplicationId,
                platform = id.Platform,
                mode = id.Mode,
                appVersion = id.AppVersion,
                outcome = "Pass",
                proofScope = def.RequiredProof.ToString(),
                evidenceNote = evidenceNote,
                deviceBuildGuid = deviceGate ? deviceBuildGuid : null,
            });
            return true;
        }

        // ── Observations ──────────────────────────────────────────────

        /// <summary>
        ///     Builds the neutral observations. Producer-side context guards ensure it never fabricates
        ///     evidence for a gate the context makes NotApplicable (which would be a C3-05 context mismatch):
        ///     device observations are emitted only on a platform that has a shipping transport (Android via
        ///     adb, iOS via iproxy - F10); and the Adjust purchase-verification manual row only in Full mode
        ///     (no Adjust in Prototype). These are facts about which evidence EXISTS, not requirement
        ///     decisions - the catalog still owns those.
        /// </summary>
        internal static List<GateObservation> BuildObservations(
            EvaluationContext context,
            List<BuildValidator.ValidationResult> buildHealthResults,
            GreenlightDeviceSnapshot.State snapshotState)
        {
            var observations = new List<GateObservation>();
            observations.AddRange(BuildHealthObservations(buildHealthResults, context.InstalledModules));

            string deviceBuildGuid = null;
            // Emit device evidence on any platform that has a shipping snapshot collector. Both mobile
            // transports ship now: Android over `adb forward`, iOS over `iproxy` (libimobiledevice USB). Off
            // mobile the device gates are NotApplicable, so emitting there would be a C3-05 mismatch; a mobile
            // target that is never connected still emits a not-connected DeviceReady → INCOMPLETE.
            if (context.Platform == EvalPlatform.Android || context.Platform == EvalPlatform.iOS)
            {
                observations.AddRange(GreenlightDeviceSnapshot.ToObservations(snapshotState));
                // Binds device-session manual attestations (relaunch/background) to the exact connected build on
                // iOS as well as Android (C45-05, F10) - the snapshot's build_guid is the shared identity.
                deviceBuildGuid = GreenlightDeviceSnapshot.BuildGuidOf(snapshotState);
            }

            observations.AddRange(ManualObservations(context, deviceBuildGuid));
            return observations;
        }

        /// <summary>
        ///     One observation per applicable manual gate, sourced from scoped attestations (Cycle 4b, hardened
        ///     per C45-01/04/05). A valid attestation - exact gate id + current gate version + requested phase
        ///     + matching app identity + (for device gates) the connected build GUID, with a Pass outcome and
        ///     required evidence - carries the gate's required proof scope → PASS. Stale (wrong build/version/
        ///     phase/expired) or invalid → INCOMPLETE; an unattested gate → INCOMPLETE with guidance. A corrupt
        ///     attestation store makes every manual gate INCOMPLETE with an integrity note (C45-04). The Adjust
        ///     gate is only relevant in Full mode.
        /// </summary>
        internal static IEnumerable<GateObservation> ManualObservations(EvaluationContext context, string deviceBuildGuid)
        {
            List<QaAttestationRecord> records = QaAttestationStore.Load(out bool corrupt);
            QaBuildIdentity identity = QaBuildIdentity.Current();
            string phase = context.RequestedPhase.ToString();
            DateTime now = DateTime.UtcNow;

            foreach (GreenlightManualChecklist.Descriptor d in GreenlightManualChecklist.Descriptors)
            {
                if (d.GateId == GateIds.ManualAdjustPurchaseVerification && context.Mode != EvalMode.Full)
                    continue; // no Adjust in Prototype - the gate is NotApplicable there
                if (d.GateId == GateIds.IapStoreConfigured && (context.InstalledModules & SdkModule.UnityIap) == 0)
                    continue; // Unity IAP not installed - the iap gate is NotApplicable there

                if (corrupt)
                {
                    yield return new GateObservation
                    {
                        GateId = d.GateId, Outcome = GateOutcome.Incomplete, ObservedProof = ProofScope.None,
                        Evidence = "The attestation store is unreadable/corrupt - its evidence cannot be trusted.",
                        FixHint = "Re-attest this gate to rewrite a valid attestation store.",
                    };
                    continue;
                }

                GateDefinition def = GateCatalog.Canonical.ById(d.GateId, throwIfMissing: false);
                ProofScope required = def?.RequiredProof ?? ProofScope.None;
                var expectation = new QaAttestationExpectation(
                    d.GateId, def?.Version, phase, required, identity, deviceBuildGuid);

                QaAttestationRecord record = QaAttestationStore.ForGate(records, d.GateId);
                AttestationValidity validity =
                    QaAttestationValidator.Evaluate(record, expectation, now, out string reason);

                yield return validity == AttestationValidity.Valid
                    ? new GateObservation
                    {
                        GateId = d.GateId, Outcome = GateOutcome.Pass, ObservedProof = required,
                        // Carry the human evidence note into the row (safe, length-limited) so the canonical
                        // report export shows provenance beyond "attested by X at T" (review F4).
                        Evidence = WithNote(reason, record.evidenceNote),
                    }
                    : new GateObservation
                    {
                        GateId = d.GateId,
                        Outcome = GateOutcome.Incomplete,
                        ObservedProof = ProofScope.None,
                        Evidence = validity == AttestationValidity.Missing ? d.Why : reason,
                        FixHint = validity == AttestationValidity.Missing ? d.Fix : "Re-attest against the current build.",
                    };
            }
        }

        /// <summary>Appends a length-limited evidence note to an attestation's provenance line for the export
        /// (review F4). Empty notes leave the line unchanged; long notes are truncated to keep the report tidy
        /// and avoid dumping arbitrary operator text.</summary>
        const int MaxNoteLength = 280;

        static string WithNote(string reason, string note)
        {
            if (string.IsNullOrWhiteSpace(note)) return reason;
            string trimmed = note.Trim();
            if (trimmed.Length > MaxNoteLength) trimmed = trimmed.Substring(0, MaxNoteLength) + "…";
            return $"{reason} — note: {trimmed}";
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
                [BuildValidator.CheckCategory.FirebaseConfig] = SdkModule.Firebase,
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
            List<BuildValidator.ValidationResult> results, SdkModule installedModules)
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
                    (installedModules & module) == 0)
                    continue;

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

        /// <summary>Gate ids whose fix is something THIS editor can do directly (open the exact vendor
        /// settings asset/window the fix hint tells you to edit), rather than a step in a store console or
        /// on a device - product-audit fix cycle ruling 1, 2026-07-21 11:55: "any row fix the editor can
        /// perform/open becomes a button on the row, prose only where the editor genuinely can't act."
        /// Same pattern as the attest rows' "Open Dashboard" deep link, just an in-editor action instead of
        /// a URL.</summary>
        static readonly Dictionary<string, (string Label, Action Action)> EditorActions =
            new Dictionary<string, (string, Action)>
            {
                [GateIds.BuildGameAnalyticsKeys] = ("Open GA Settings", SdkConfigDetector.OpenGameAnalyticsSettings),
                [GateIds.BuildGameAnalyticsResourceWhitelist] = ("Open GA Settings", SdkConfigDetector.OpenGameAnalyticsSettings),
                [GateIds.BuildFacebookPlatform] = ("Open FB Settings", SdkConfigDetector.OpenFacebookSettings),
            };

        internal static (string label, Action action) EditorActionFor(string gateId) =>
            gateId != null && EditorActions.TryGetValue(gateId, out (string Label, Action Action) entry)
                ? (entry.Label, entry.Action)
                : (null, null);

        static readonly Dictionary<string, string> DeviceLabels = new Dictionary<string, string>
        {
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
    }
}
