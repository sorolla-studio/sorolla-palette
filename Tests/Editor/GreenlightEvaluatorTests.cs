using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sorolla.Palette.Editor.Greenlight;
using Sorolla.Palette.Editor.UI;
using Sorolla.Palette.Health;
using UnityEditor;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     Covers the Cycle-4 cutover: the Editor greenlight now routes through the ONE shared
    ///     <see cref="HealthEvaluator.Evaluate"/> via <see cref="GreenlightAdapter"/>. Tests the adapter's
    ///     row-class → observation mapping, the B-10 rule that a ticked legacy checkmark cannot produce an
    ///     affirmative outcome, the display-only mapping of the aggregate outcome (no recomputed precedence),
    ///     and the end-to-end no-evidence → INCOMPLETE non-green invariant carried over from Cycle 2.
    /// </summary>
    [TestFixture]
    public class GreenlightEvaluatorTests
    {
        static EvaluationContext Ctx() => new EvaluationContext
        {
            Mode = EvalMode.Full,
            Platform = EvalPlatform.Android,
            InstalledModules = HealthEnums.AllModuleBits,
            RequestedPhase = GatePhase.QaPass,
            Profile = ReportProfile.SorollaFull,
        };

        // ── Adapter: context mapping ──────────────────────────────────────

        [Test]
        public void ToEvalMode_MapsEditorModes()
        {
            Assert.AreEqual(EvalMode.Unknown, GreenlightAdapter.ToEvalMode(SorollaMode.None));
            Assert.AreEqual(EvalMode.Prototype, GreenlightAdapter.ToEvalMode(SorollaMode.Prototype));
            Assert.AreEqual(EvalMode.Full, GreenlightAdapter.ToEvalMode(SorollaMode.Full));
        }

        [Test]
        public void ToEvalPlatform_MapsMobileTargets_ElseUnknown()
        {
            Assert.AreEqual(EvalPlatform.Android, GreenlightAdapter.ToEvalPlatform(BuildTarget.Android));
            Assert.AreEqual(EvalPlatform.iOS, GreenlightAdapter.ToEvalPlatform(BuildTarget.iOS));
            Assert.AreEqual(EvalPlatform.Unknown, GreenlightAdapter.ToEvalPlatform(BuildTarget.StandaloneOSX));
        }

        [Test]
        public void RequestedPhaseFor_MapsProfileToPhase()
        {
            // The window's "Validation Profile" (QaPass/Release) selector drives this, making ReleaseShip
            // gates reachable when the studio switches to the Release profile (review point 7).
            Assert.AreEqual(GatePhase.ReleaseShip, GreenlightAdapter.RequestedPhaseFor(true));
            Assert.AreEqual(GatePhase.QaPass, GreenlightAdapter.RequestedPhaseFor(false));
        }

        // ── Adapter: Build Health row-class → observation ─────────────────

        [Test]
        public void BuildHealth_StatusMapsToOutcome()
        {
            Assert.AreEqual(GateOutcome.Fail, GreenlightAdapter.ToOutcome(BuildValidator.ValidationStatus.Error));
            Assert.AreEqual(GateOutcome.PassWithCaveats, GreenlightAdapter.ToOutcome(BuildValidator.ValidationStatus.Warning));
            Assert.AreEqual(GateOutcome.Incomplete, GreenlightAdapter.ToOutcome(BuildValidator.ValidationStatus.Unverifiable));
            Assert.AreEqual(GateOutcome.Pass, GreenlightAdapter.ToOutcome(BuildValidator.ValidationStatus.Valid));
        }

        [Test]
        public void BuildHealth_EmitsOneObservationPerCategory_KeyedToGateId()
        {
            var results = new List<BuildValidator.ValidationResult>
            {
                new BuildValidator.ValidationResult(BuildValidator.ValidationStatus.Error,
                    "boom", "fix it", BuildValidator.CheckCategory.FacebookPlatformConfig),
            };

            List<GateObservation> obs = GreenlightAdapter.BuildObservations(
                Ctx(), results, new GreenlightDeviceSnapshot.State());

            GateObservation fb = obs.Single(o => o.GateId == GateIds.BuildFacebookPlatform);
            Assert.AreEqual(GateOutcome.Fail, fb.Outcome);
            Assert.AreEqual(ProofScope.Static, fb.ObservedProof);
            Assert.AreEqual("fix it", fb.FixHint);
        }

        [Test]
        public void BuildHealth_CollapsesMultipleResultsPerCategory_WorstWins()
        {
            var results = new List<BuildValidator.ValidationResult>
            {
                new BuildValidator.ValidationResult(BuildValidator.ValidationStatus.Valid, "ok", null, BuildValidator.CheckCategory.AndroidManifest),
                new BuildValidator.ValidationResult(BuildValidator.ValidationStatus.Error, "bad", null, BuildValidator.CheckCategory.AndroidManifest),
            };

            List<GateObservation> obs = GreenlightAdapter.BuildObservations(
                Ctx(), results, new GreenlightDeviceSnapshot.State());

            GateObservation manifest = obs.Single(o => o.GateId == GateIds.BuildAndroidManifest);
            Assert.AreEqual(GateOutcome.Fail, manifest.Outcome);
        }

        [Test]
        public void BuildHealth_NullResults_EmitNoBuildObservations()
        {
            List<GateObservation> obs = GreenlightAdapter.BuildObservations(
                Ctx(), null, new GreenlightDeviceSnapshot.State());

            Assert.IsFalse(obs.Any(o => o.GateId.StartsWith("build.")),
                "No Build Health results means the required core build gates omit -> INCOMPLETE, not silent pass.");
        }

        [Test]
        public void EveryBuildValidatorCategory_MapsToACanonicalGate()
        {
            // C4-09: no BuildValidator category may silently disappear - every one maps to a canonical gate.
            foreach (BuildValidator.CheckCategory cat in
                     System.Enum.GetValues(typeof(BuildValidator.CheckCategory)))
            {
                var results = new List<BuildValidator.ValidationResult>
                {
                    new BuildValidator.ValidationResult(BuildValidator.ValidationStatus.Valid, "x", null, cat),
                };
                List<GateObservation> obs = GreenlightAdapter.BuildObservations(
                    Ctx(), results, new GreenlightDeviceSnapshot.State());
                Assert.IsFalse(obs.Any(o => o.GateId.StartsWith("unmapped:")), $"CheckCategory {cat} is unmapped.");
            }
        }

        [Test]
        public void ToOutcome_UndefinedStatus_ThrowsInsteadOfPass()
        {
            // C4-09: an invalid ValidationStatus must fail closed, not map to a silent PASS.
            Assert.Throws<ArgumentOutOfRangeException>(
                () => GreenlightAdapter.ToOutcome((BuildValidator.ValidationStatus)999));
        }

        [Test]
        public void ValidationErrors_AreRenderedAsVisibleRows()
        {
            // C4-07: a contract/schema validation error must produce a visible, non-green row, not just flip
            // the badge silently.
            var health = new HealthReport
            {
                Rows = new List<GateResult>(),
                Outcome = GateOutcome.Incomplete,
                ValidationErrors = new[] { "Unknown gate id in observations: 'ghost'" },
            };
            GreenlightEvaluator.Report report = GreenlightEvaluator.ToReport(health);
            Assert.IsTrue(report.Rows.Any(r => r.Status == CheckRow.Status.Wait && r.Detail.Contains("ghost")),
                "The validation error must be a visible Wait row.");
        }

        // ── Adapter: device row-class → observation ───────────────────────

        [Test]
        public void Device_NotConnected_EmitsNoObservations_SoRequiredDeviceGatesOmit()
        {
            // With device.ready deleted, a never-connected device produces NO evidence at all: the required
            // device gates omit → INCOMPLETE. Silence is the fail-closed answer; a fabricated row is not.
            Assert.IsEmpty(GreenlightDeviceSnapshot.ToObservations(new GreenlightDeviceSnapshot.State()));
        }

        // ── F1: applicability vs collector availability ───────────────────

        [Test]
        public void IosPlatform_NoDeviceEvidence_DeviceGatesAreIncomplete_NotNotApplicable()
        {
            // On an iOS build with NO device evidence supplied, the required device gates omit → INCOMPLETE,
            // they do NOT drop out as NotApplicable (which would let an iOS build read HEALTHY without ever
            // running on its shipping platform). This is the catalog-level requiredness; the adapter supplies
            // real iOS evidence over iproxy (see the F10 tests below), but "never connected" must still land
            // INCOMPLETE, exactly like Android.
            var ctx = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.iOS,
                InstalledModules = SdkModule.GameAnalytics | SdkModule.Facebook,
                RequestedPhase = GatePhase.QaPass, ModulesResolved = true, Profile = ReportProfile.SorollaFull,
            };
            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx, new List<GateObservation>());
            GateResult device = report.Rows.Single(r => r.GateId == GateIds.DeviceNoSdkErrors);
            Assert.AreEqual(Requirement.Required, device.Requirement);
            Assert.AreEqual(GateDisposition.Omitted, device.Disposition);
            Assert.AreEqual(GateOutcome.Incomplete, device.Outcome);
            Assert.AreEqual(GateOutcome.Incomplete, report.Outcome, "an iOS-only build with no device evidence must not be HEALTHY");
        }

        [Test]
        public void NonMobilePlatform_DeviceGate_IsNotApplicableWithReason()
        {
            // Off a mobile build target there is no device to observe: the device gate is NotApplicable WITH a
            // recorded reason, never silently Required. The store gate keys on Unity IAP alone, so it stays
            // Required here (absent = NotApplicable, see IapStoreGate_KeysOnUnityIapInstalled).
            var ctx = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.Unknown,
                InstalledModules = SdkModule.UnityIap,
                RequestedPhase = GatePhase.QaPass, ModulesResolved = true, Profile = ReportProfile.SorollaFull,
            };
            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx, new List<GateObservation>());
            GateResult device = report.Rows.Single(r => r.GateId == GateIds.DeviceNoSdkErrors);
            Assert.AreEqual(GateDisposition.NotApplicable, device.Disposition);
            Assert.IsFalse(string.IsNullOrEmpty(device.RequirementReason), "a NotApplicable gate must record why");
        }

        // ── C4-03: build/game identity binding ────────────────────────────

        static Dictionary<string, object> SnapshotWithIdentity(string appId, string platform, string appVersion, string mode) =>
            new Dictionary<string, object>
            {
                ["mode"] = mode,
                ["build"] = new Dictionary<string, object>
                {
                    ["application_id"] = appId, ["platform"] = platform, ["app_version"] = appVersion,
                    ["build_guid"] = "test-guid", // required identity field (C45-05)
                },
            };

        [Test]
        public void CompareIdentity_MatchingBuild_IsMatch()
        {
            Dictionary<string, object> snap = SnapshotWithIdentity("com.sorolla.game", "Android", "1.0", "full");
            Assert.AreEqual(GreenlightDeviceSnapshot.IdentityResult.Match,
                GreenlightDeviceSnapshot.CompareIdentity(snap, "com.sorolla.game", "full", "1.0", "Android", out _));
        }

        [Test]
        public void CompareIdentity_WrongGame_IsMismatch()
        {
            Dictionary<string, object> snap = SnapshotWithIdentity("com.other.game", "Android", "1.0", "full");
            Assert.AreEqual(GreenlightDeviceSnapshot.IdentityResult.Mismatch,
                GreenlightDeviceSnapshot.CompareIdentity(snap, "com.sorolla.game", "full", "1.0", "Android", out string detail));
            StringAssert.Contains("Wrong game", detail);
        }

        [Test]
        public void CompareIdentity_WrongBuildVersion_IsMismatch()
        {
            Dictionary<string, object> snap = SnapshotWithIdentity("com.sorolla.game", "Android", "0.9", "full");
            Assert.AreEqual(GreenlightDeviceSnapshot.IdentityResult.Mismatch,
                GreenlightDeviceSnapshot.CompareIdentity(snap, "com.sorolla.game", "full", "1.0", "Android", out _));
        }

        [Test]
        public void CompareIdentity_NoBuildBlock_IsMissing()
        {
            var snap = new Dictionary<string, object> { ["mode"] = "full" };
            Assert.AreEqual(GreenlightDeviceSnapshot.IdentityResult.Missing,
                GreenlightDeviceSnapshot.CompareIdentity(snap, "com.sorolla.game", "full", "1.0", "Android", out _));
        }

        [Test]
        public void DeviceSnapshot_UnknownSchema_IsIncomplete()
        {
            // C4-08: an unknown snapshot schema is not parsed permissively.
            var state = new GreenlightDeviceSnapshot.State
            {
                Phase = GreenlightDeviceSnapshot.Phase.Done,
                Outcome = GreenlightDeviceSnapshot.Outcome.Parsed,
                Snapshot = new Dictionary<string, object> { ["snapshot_schema"] = "999" },
            };
            List<GateObservation> obs = GreenlightDeviceSnapshot.ToObservations(state);
            GateObservation untrusted = obs.Single(o => o.GateId == GateIds.DeviceNoSdkErrors);
            Assert.AreEqual(GateOutcome.Incomplete, untrusted.Outcome);
            Assert.AreEqual(ProofScope.None, untrusted.ObservedProof,
                "an untrusted snapshot must never carry device-dispatch proof");
        }

        [Test]
        public void CompareIdentity_WrongPlatform_IsMismatch()
        {
            // C45-05: an iOS snapshot pulled while the project targets Android (or vice-versa) has not proven
            // which build it came from - the platform is part of the required identity.
            Dictionary<string, object> snap = SnapshotWithIdentity("com.sorolla.game", "IPhonePlayer", "1.0", "full");
            Assert.AreEqual(GreenlightDeviceSnapshot.IdentityResult.Mismatch,
                GreenlightDeviceSnapshot.CompareIdentity(snap, "com.sorolla.game", "full", "1.0", "Android", out string detail));
            StringAssert.Contains("Wrong platform", detail);
        }

        // ── F10: iOS device transport (iproxy) - un-gated collector + GUID binding ───

        [Test]
        public void BuildObservations_Ios_NeverConnected_LeavesDeviceGatesUnobserved()
        {
            // F10: iOS has a shipping collector (iproxy), so the device path is un-gated for iOS. A
            // never-connected iOS build supplies no device evidence, so its required device gates omit →
            // INCOMPLETE through the real evaluator.
            var ctx = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.iOS,
                InstalledModules = SdkModule.GameAnalytics | SdkModule.Facebook,
                RequestedPhase = GatePhase.QaPass, ModulesResolved = true, Profile = ReportProfile.SorollaFull,
            };
            List<GateObservation> obs = GreenlightAdapter.BuildObservations(
                ctx, null, new GreenlightDeviceSnapshot.State());
            Assert.IsFalse(obs.Any(o => o.GateId == GateIds.DeviceNoSdkErrors));

            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx, obs);
            GateResult device = report.Rows.Single(r => r.GateId == GateIds.DeviceNoSdkErrors);
            Assert.AreEqual(GateDisposition.Omitted, device.Disposition);
            Assert.AreEqual(GateOutcome.Incomplete, report.Outcome);
        }

        [Test]
        public void BuildObservations_NonMobilePlatform_EmitsNoDeviceObservation()
        {
            // Off a mobile build target the device gates are NotApplicable, so the adapter must emit NO device
            // observation (emitting would be a C3-05 context mismatch).
            var ctx = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.Unknown,
                InstalledModules = SdkModule.GameAnalytics,
                RequestedPhase = GatePhase.QaPass, ModulesResolved = true, Profile = ReportProfile.SorollaFull,
            };
            List<GateObservation> obs = GreenlightAdapter.BuildObservations(
                ctx, null, new GreenlightDeviceSnapshot.State());
            Assert.IsFalse(obs.Any(o => o.GateId.StartsWith("device.")),
                "a non-mobile build target must not emit a device observation.");
        }

        // Parsed iOS snapshot carrying an explicit build GUID, for the attestation-binding tests below.
        static GreenlightDeviceSnapshot.State ParsedIosSnapshot(string buildGuid) => new GreenlightDeviceSnapshot.State
        {
            Phase = GreenlightDeviceSnapshot.Phase.Done,
            Outcome = GreenlightDeviceSnapshot.Outcome.Parsed,
            Snapshot = new Dictionary<string, object>
            {
                ["snapshot_schema"] = "1",
                ["mode"] = "full",
                ["build"] = new Dictionary<string, object>
                {
                    ["application_id"] = "com.sorolla.hungrysnake3d", ["platform"] = "IPhonePlayer",
                    ["app_version"] = "2.8.2", ["build_guid"] = buildGuid,
                },
            },
        };

        static QaAttestationRecord IosDeviceAttestation(string gateId, string deviceBuildGuid) => new QaAttestationRecord
        {
            schema = "1", gateId = gateId, gateVersion = "1", phase = GatePhase.QaPass.ToString(),
            actor = "tester", timestampUtc = DateTime.UtcNow.ToString("o"),
            applicationId = "com.sorolla.hungrysnake3d", platform = "IPhonePlayer", mode = "full", appVersion = "2.8.2",
            outcome = "Pass", proofScope = ProofScope.DeviceDispatch.ToString(),
            evidenceNote = "killed + relaunched; consent and progress persisted", deviceBuildGuid = deviceBuildGuid,
        };

        [Test]
        public void IosSnapshotBuildGuid_BindsDeviceManualAttestation()
        {
            // Blocker 3: the iOS snapshot's build_guid must flow into the device-manual attestation expectation
            // so a device-backed gate can be attested (not Stale-forever). Matching GUID → Valid.
            string guid = GreenlightDeviceSnapshot.BuildGuidOf(ParsedIosSnapshot("6f5d5572536a40db9136bbf61a3bcd89"));
            Assert.AreEqual("6f5d5572536a40db9136bbf61a3bcd89", guid, "BuildGuidOf must read the iOS snapshot's build GUID.");

            var identity = new QaBuildIdentity("com.sorolla.hungrysnake3d", "IPhonePlayer", "full", "2.8.2");
            var expectation = new QaAttestationExpectation(
                GateIds.ManualRelaunchPersistence, "1", GatePhase.QaPass.ToString(),
                ProofScope.DeviceDispatch, identity, guid);
            QaAttestationRecord record = IosDeviceAttestation(GateIds.ManualRelaunchPersistence, guid);

            Assert.AreEqual(AttestationValidity.Valid,
                QaAttestationValidator.Evaluate(record, expectation, DateTime.UtcNow, out _),
                "an iOS device attestation bound to the connected build GUID must validate.");
        }

        [Test]
        public void IosDeviceAttestation_GuidMismatch_IsStale_NeverPass()
        {
            // A device attestation made against a DIFFERENT iOS build (GUID mismatch) must be Stale → INCOMPLETE,
            // never inherited by a different binary at the same app-version.
            string guid = GreenlightDeviceSnapshot.BuildGuidOf(ParsedIosSnapshot("6f5d5572536a40db9136bbf61a3bcd89"));
            var identity = new QaBuildIdentity("com.sorolla.hungrysnake3d", "IPhonePlayer", "full", "2.8.2");
            var expectation = new QaAttestationExpectation(
                GateIds.ManualRelaunchPersistence, "1", GatePhase.QaPass.ToString(),
                ProofScope.DeviceDispatch, identity, "a-different-build-guid");
            QaAttestationRecord record = IosDeviceAttestation(GateIds.ManualRelaunchPersistence, guid);

            Assert.AreEqual(AttestationValidity.Stale,
                QaAttestationValidator.Evaluate(record, expectation, DateTime.UtcNow, out _));
        }

        // ── F4-02: bare-vendor absence is not an affirmative observation ───

        [Test]
        public void BareVendorAbsence_EmitsNoObservation()
        {
            var results = new List<BuildValidator.ValidationResult>
            {
                new BuildValidator.ValidationResult(BuildValidator.ValidationStatus.Valid,
                    "Firebase not installed (optional in Prototype)", null, BuildValidator.CheckCategory.FirebaseCoherence),
            };
            var protoCtx = new EvaluationContext
            {
                Mode = EvalMode.Prototype, Platform = EvalPlatform.Android,
                InstalledModules = SdkModule.None, RequestedPhase = GatePhase.QaPass,
                Profile = ReportProfile.SorollaFull,
            };
            List<GateObservation> obs = GreenlightAdapter.BuildObservations(
                protoCtx, results, new GreenlightDeviceSnapshot.State());
            Assert.IsFalse(obs.Any(o => o.GateId == GateIds.BuildFirebaseCoherence),
                "vendor absence must not become an affirmative PASS observation.");
        }

        [Test]
        public void PresentVendor_EmitsObservation()
        {
            var results = new List<BuildValidator.ValidationResult>
            {
                new BuildValidator.ValidationResult(BuildValidator.ValidationStatus.Valid,
                    "Firebase modules OK", null, BuildValidator.CheckCategory.FirebaseCoherence),
            };
            var ctx = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.Android,
                InstalledModules = SdkModule.Firebase, RequestedPhase = GatePhase.QaPass,
                Profile = ReportProfile.SorollaFull,
            };
            List<GateObservation> obs = GreenlightAdapter.BuildObservations(
                ctx, results, new GreenlightDeviceSnapshot.State());
            Assert.IsTrue(obs.Any(o => o.GateId == GateIds.BuildFirebaseCoherence),
                "a present vendor's coherence result must be evaluated.");
        }

        // ── B-10: unscoped manual evidence is NOT a pass ──────────────────

        [Test]
        public void UnscopedManualEvidence_CannotProduceAffirmativeOutcome()
        {
            // The B-10 invariant through the real evaluator: a manual PASS with no scoped proof (what a
            // legacy tick or a scope-less claim amounts to) is forced to INCOMPLETE by the required-proof
            // gate - it can never become an affirmative PASS.
            var ctx = new EvaluationContext
            {
                Mode = EvalMode.Full,
                Platform = EvalPlatform.Android,
                InstalledModules = HealthEnums.AllModuleBits,
                RequestedPhase = GatePhase.QaPass,
                Profile = ReportProfile.SorollaFull,
            };
            var observations = new List<GateObservation>
            {
                new GateObservation
                {
                    GateId = GateIds.ManualGaPlatformRegistered,
                    Outcome = GateOutcome.Pass,
                    ObservedProof = ProofScope.None,
                },
            };

            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx, observations);
            GateResult row = report.Rows.Single(r => r.GateId == GateIds.ManualGaPlatformRegistered);

            Assert.AreEqual(GateOutcome.Incomplete, row.Outcome, "Ticked-but-unscoped must resolve to INCOMPLETE.");
            Assert.AreNotEqual(GateOutcome.Pass, report.Outcome);
            Assert.AreNotEqual(GateOutcome.PassWithCaveats, report.Outcome);
        }

        // ── Display mapping (maps the aggregate outcome, never recomputes) ─

        [Test]
        public void VerdictLabel_MapsEveryOutcome()
        {
            Assert.AreEqual("HEALTHY", GreenlightEvaluator.VerdictLabel(GateOutcome.Pass, 0, 0));
            Assert.AreEqual("1 ISSUES", GreenlightEvaluator.VerdictLabel(GateOutcome.PassWithCaveats, 0, 1));
            Assert.AreEqual("INCOMPLETE", GreenlightEvaluator.VerdictLabel(GateOutcome.Incomplete, 0, 0));
            Assert.AreEqual("FAILING", GreenlightEvaluator.VerdictLabel(GateOutcome.Fail, 0, 0));
        }

        [Test]
        public void VerdictLabel_UnknownOutcome_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => GreenlightEvaluator.VerdictLabel((GateOutcome)999, 0, 0));
        }

        [Test]
        public void BadgeSeverity_MapsEveryOutcome_IncompleteIsNonGreen()
        {
            Assert.AreEqual(StatusBadge.Severity.Pass, GreenlightEvaluator.BadgeSeverity(GateOutcome.Pass));
            Assert.AreEqual(StatusBadge.Severity.Advisory, GreenlightEvaluator.BadgeSeverity(GateOutcome.PassWithCaveats));
            Assert.AreEqual(StatusBadge.Severity.Wait, GreenlightEvaluator.BadgeSeverity(GateOutcome.Incomplete));
            Assert.AreEqual(StatusBadge.Severity.Fail, GreenlightEvaluator.BadgeSeverity(GateOutcome.Fail));
        }

        [Test]
        public void BadgeSeverity_UnknownOutcome_ThrowsInsteadOfPass()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => GreenlightEvaluator.BadgeSeverity((GateOutcome)999));
        }

        [Test]
        public void RowSummary_Incomplete_NeverReadsRowsChecked()
        {
            var report = new GreenlightEvaluator.Report { Outcome = GateOutcome.Incomplete, WaitCount = 2 };
            report.Rows.Add(new GreenlightEvaluator.Row { Status = CheckRow.Status.Wait });
            report.Rows.Add(new GreenlightEvaluator.Row { Status = CheckRow.Status.Wait });
            StringAssert.DoesNotContain("rows checked", GreenlightEvaluator.RowSummary(report));
        }

        [Test]
        public void RowSummary_Pass_ReadsRowsChecked()
        {
            var report = new GreenlightEvaluator.Report { Outcome = GateOutcome.Pass, PassCount = 2 };
            report.Rows.Add(new GreenlightEvaluator.Row { Status = CheckRow.Status.Pass });
            report.Rows.Add(new GreenlightEvaluator.Row { Status = CheckRow.Status.Pass });
            Assert.AreEqual("2 rows checked", GreenlightEvaluator.RowSummary(report));
        }

        // ── End-to-end: no evidence must never render green ───────────────

        [Test]
        public void Evaluate_RealPathWithNoEvidence_IsIncompleteAndNonGreen()
        {
            // The whole reason the plan exists: no Build Health results, a never-connected device, and no
            // completed manual evidence must land INCOMPLETE and a non-green badge - never HEALTHY - through
            // the real shared evaluator, whatever the ambient editor mode/platform.
            GreenlightEvaluator.Report report = GreenlightEvaluator.Evaluate(
                buildHealthResults: null,
                snapshotState: new GreenlightDeviceSnapshot.State());

            Assert.AreEqual(GateOutcome.Incomplete, report.Outcome,
                "A report with no run evidence must be INCOMPLETE, not HEALTHY.");
            Assert.Greater(report.WaitCount, 0, "Unrun/unticked required evidence must surface as Wait rows.");
            Assert.AreNotEqual(StatusBadge.Severity.Pass, GreenlightEvaluator.BadgeSeverity(report.Outcome),
                "The badge for a no-evidence report must not be the green Pass pill.");
        }
    }
}
