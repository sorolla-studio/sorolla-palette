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

        /// <summary>ReleaseOnly is read from the catalog and means only "don't log this on a development
        /// build". The keystore qualifies; the checks a studio must act on - sandbox mode, GA keys - do not,
        /// and no check is release-only merely because it sounds release-shaped.</summary>
        [Test]
        public void IsReleaseOnly_OnlyTheKeystore()
        {
            Assert.IsTrue(GreenlightAdapter.IsReleaseOnly(BuildValidator.CheckCategory.AndroidKeystore));
            Assert.IsFalse(GreenlightAdapter.IsReleaseOnly(BuildValidator.CheckCategory.AdjustSandboxMode));
            Assert.IsFalse(GreenlightAdapter.IsReleaseOnly(BuildValidator.CheckCategory.GameAnalyticsSettings));
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

        /// <summary>Platform scoping's lockstep rule, end to end on the CANONICAL catalog: the gate for the
        /// platform this report does not judge must resolve NotApplicable AND receive no observation. Getting
        /// only half of it right is a fail-loud contract error ("Context mismatch") that pins the whole report
        /// at INCOMPLETE - which is the designed alarm, and exactly what a future producer change could
        /// trip. The synthetic version of the mismatch lives in HealthEvaluatorTests; this is the guard that
        /// the SHIPPED validators and the SHIPPED catalog still agree.</summary>
        [Test]
        public void PlatformScopedGate_ForTheOtherPlatform_IsInertAndUnobserved()
        {
            var androidOnlyObservation = new List<GateObservation>
            {
                new GateObservation
                {
                    GateId = GateIds.BuildFirebaseConfigAndroid,
                    Outcome = GateOutcome.Pass,
                    ObservedProof = ProofScope.Static,
                    Evidence = "google-services.json matches the Android application id.",
                },
            };

            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, Ctx(), androidOnlyObservation);

            CollectionAssert.IsEmpty(
                report.ValidationErrors.Where(e => e.Contains("Context mismatch")).ToList(),
                "an Android build supplying only the Android config observation must not trip the mismatch guard");
            GateResult iosRow = report.Rows.Single(r => r.GateId == GateIds.BuildFirebaseConfigIos);
            Assert.AreEqual(GateDisposition.NotApplicable, iosRow.Disposition);
        }

        /// <summary>The other half of the same contract: observing the gate that does NOT apply is the error
        /// the evaluator must shout about, rather than quietly grading a platform this build is not for.</summary>
        [Test]
        public void PlatformScopedGate_ObservedForTheWrongPlatform_IsAContractError()
        {
            var wrongPlatformObservation = new List<GateObservation>
            {
                new GateObservation
                {
                    GateId = GateIds.BuildFirebaseConfigIos,
                    Outcome = GateOutcome.Pass,
                    ObservedProof = ProofScope.Static,
                },
            };

            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, Ctx(), wrongPlatformObservation);

            Assert.IsTrue(report.ValidationErrors.Any(e => e.Contains("Context mismatch")));
            Assert.AreEqual(GateOutcome.Incomplete, report.Outcome);
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
            Assert.IsTrue(report.Rows.Any(r => r.Status == RowStatus.Wait && r.Detail.Contains("ghost")),
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

        [TestCase("failing", "Fail")]
        [TestCase("action_needed", "PassWithCaveats")]
        [TestCase("not_proven", "Incomplete")]
        [TestCase("pass", "Pass")]
        public void RuntimeVitalsVerdict_IsTheEditorDeviceObservation(string verdict, string expected)
        {
            var snapshot = new Dictionary<string, object> { ["verdict"] = verdict };
            GateObservation observation = GreenlightDeviceSnapshot.VitalsObservation(snapshot);

            Assert.AreEqual(GateIds.DeviceVitals, observation.GateId);
            Assert.AreEqual(expected, observation.Outcome.ToString());
            Assert.AreEqual(ProofScope.DeviceDispatch, observation.ObservedProof);
        }

        [Test]
        public void UnknownRuntimeVitalsVerdict_FailsClosed()
        {
            GateObservation observation = GreenlightDeviceSnapshot.VitalsObservation(
                new Dictionary<string, object> { ["verdict"] = "future_value" });

            Assert.AreEqual(GateOutcome.Incomplete, observation.Outcome);
            Assert.AreEqual(ProofScope.None, observation.ObservedProof);
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
                ModulesResolved = true,
            };
            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx, new List<GateObservation>());
            GateResult device = report.Rows.Single(r => r.GateId == GateIds.DeviceVitals);
            Assert.AreEqual(Requirement.Required, device.Requirement);
            Assert.AreEqual(GateDisposition.Omitted, device.Disposition);
            Assert.AreEqual(GateOutcome.Incomplete, device.Outcome);
            Assert.AreEqual(GateOutcome.Incomplete, report.Outcome, "an iOS-only build with no device evidence must not be HEALTHY");
        }

        [Test]
        public void NonMobilePlatform_DeviceGate_IsNotApplicableWithReason()
        {
            // Off a mobile build target there is no device to observe: the device gate is NotApplicable WITH a
            // recorded reason, never silently Required.
            var ctx = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.Unknown,
                InstalledModules = SdkModule.UnityIap,
                ModulesResolved = true,
            };
            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx, new List<GateObservation>());
            GateResult device = report.Rows.Single(r => r.GateId == GateIds.DeviceVitals);
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
                GreenlightDeviceSnapshot.CompareIdentity(snap, "com.sorolla.game", "full", "1.0", "Android", "test-guid", out _));
        }

        [Test]
        public void CompareIdentity_WrongGame_IsMismatch()
        {
            Dictionary<string, object> snap = SnapshotWithIdentity("com.other.game", "Android", "1.0", "full");
            Assert.AreEqual(GreenlightDeviceSnapshot.IdentityResult.Mismatch,
                GreenlightDeviceSnapshot.CompareIdentity(snap, "com.sorolla.game", "full", "1.0", "Android", "test-guid", out string detail));
            StringAssert.Contains("Wrong game", detail);
        }

        [Test]
        public void CompareIdentity_WrongBuildVersion_IsMismatch()
        {
            Dictionary<string, object> snap = SnapshotWithIdentity("com.sorolla.game", "Android", "0.9", "full");
            Assert.AreEqual(GreenlightDeviceSnapshot.IdentityResult.Mismatch,
                GreenlightDeviceSnapshot.CompareIdentity(snap, "com.sorolla.game", "full", "1.0", "Android", "test-guid", out _));
        }

        [Test]
        public void CompareIdentity_DifferentNonemptyBuildGuid_IsMismatch()
        {
            Dictionary<string, object> snap = SnapshotWithIdentity("com.sorolla.game", "Android", "1.0", "full");
            Assert.AreEqual(GreenlightDeviceSnapshot.IdentityResult.Mismatch,
                GreenlightDeviceSnapshot.CompareIdentity(
                    snap, "com.sorolla.game", "full", "1.0", "Android", "different-guid", out string detail));
            StringAssert.Contains("Wrong build", detail);
        }

        [Test]
        public void CompareIdentity_NoBuildBlock_IsMissing()
        {
            var snap = new Dictionary<string, object> { ["mode"] = "full" };
            Assert.AreEqual(GreenlightDeviceSnapshot.IdentityResult.Missing,
                GreenlightDeviceSnapshot.CompareIdentity(snap, "com.sorolla.game", "full", "1.0", "Android", "test-guid", out _));
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
            GateObservation untrusted = obs.Single(o => o.GateId == GateIds.DeviceVitals);
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
                GreenlightDeviceSnapshot.CompareIdentity(snap, "com.sorolla.game", "full", "1.0", "Android", "test-guid", out string detail));
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
                ModulesResolved = true,
            };
            List<GateObservation> obs = GreenlightAdapter.BuildObservations(
                ctx, null, new GreenlightDeviceSnapshot.State());
            Assert.IsFalse(obs.Any(o => o.GateId == GateIds.DeviceVitals));

            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx, obs);
            GateResult device = report.Rows.Single(r => r.GateId == GateIds.DeviceVitals);
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
                ModulesResolved = true,
            };
            List<GateObservation> obs = GreenlightAdapter.BuildObservations(
                ctx, null, new GreenlightDeviceSnapshot.State());
            Assert.IsFalse(obs.Any(o => o.GateId.StartsWith("device.")),
                "a non-mobile build target must not emit a device observation.");
        }

        // Parsed iOS snapshot carrying an explicit build GUID, for the report-identity test below.
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

        [Test]
        public void IosSnapshotBuildGuid_IdentifiesTheBuildAReportDescribes()
        {
            // F10/blocker 3: iOS has the same transport parity as Android, and the connected build's GUID is
            // what ties a copied report to the exact binary it was taken from - so it must be read out of the
            // iOS snapshot, not just the Android one.
            string guid = GreenlightDeviceSnapshot.BuildGuidOf(ParsedIosSnapshot("6f5d5572536a40db9136bbf61a3bcd89"));
            Assert.AreEqual("6f5d5572536a40db9136bbf61a3bcd89", guid, "BuildGuidOf must read the iOS snapshot's build GUID.");

            var fingerprint = GreenlightReportExport.Fingerprint.Capture(guid);
            Assert.AreEqual(guid, fingerprint.DeviceBuildGuid);
        }

        [Test]
        public void NoConnectedDevice_ReportSaysSo_RatherThanImplyingOne()
        {
            var fingerprint = GreenlightReportExport.Fingerprint.Capture(GreenlightDeviceSnapshot.BuildGuidOf(new GreenlightDeviceSnapshot.State()));
            Assert.AreEqual("(no device connected)", fingerprint.DeviceBuildGuid);
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
                InstalledModules = SdkModule.None,

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
                InstalledModules = SdkModule.Firebase,

            };
            List<GateObservation> obs = GreenlightAdapter.BuildObservations(
                ctx, results, new GreenlightDeviceSnapshot.State());
            Assert.IsTrue(obs.Any(o => o.GateId == GateIds.BuildFirebaseCoherence),
                "a present vendor's coherence result must be evaluated.");
        }

        [Test]
        public void Prototype_ExcludesFullOnlyVendorObservationEvenWhenPackageIsPresent()
        {
            var results = new List<BuildValidator.ValidationResult>
            {
                new BuildValidator.ValidationResult(
                    BuildValidator.ValidationStatus.Skipped,
                    "Adjust not required",
                    null,
                    BuildValidator.CheckCategory.AdjustSettings),
            };
            var context = new EvaluationContext
            {
                Mode = EvalMode.Prototype,
                Platform = EvalPlatform.Android,
                InstalledModules = SdkModule.Adjust,
            };

            List<GateObservation> observations = GreenlightAdapter.BuildObservations(
                context, results, new GreenlightDeviceSnapshot.State());

            Assert.IsFalse(observations.Any(o => o.GateId == GateIds.BuildAdjustSettings));
        }

        // ── B-10: a claim without the required proof is NOT a pass ─────────

        [Test]
        public void UnscopedEvidence_CannotProduceAffirmativeOutcome()
        {
            // The B-10 invariant through the real evaluator: a PASS claim carrying no proof of the class the
            // gate requires (here: device dispatch) is forced to INCOMPLETE - asserting health never
            // substitutes for observing it.
            var ctx = new EvaluationContext
            {
                Mode = EvalMode.Full,
                Platform = EvalPlatform.Android,
                InstalledModules = HealthEnums.AllModuleBits,

            };
            var observations = new List<GateObservation>
            {
                new GateObservation
                {
                    GateId = GateIds.DeviceVitals,
                    Outcome = GateOutcome.Pass,
                    ObservedProof = ProofScope.None,
                },
            };

            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx, observations);
            GateResult row = report.Rows.Single(r => r.GateId == GateIds.DeviceVitals);

            Assert.AreEqual(GateOutcome.Incomplete, row.Outcome, "Claimed-but-unproven must resolve to INCOMPLETE.");
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

        // ── End-to-end: no evidence must never render green ───────────────

        [Test]
        public void Evaluate_RealPathWithNoEvidence_IsIncompleteAndNonGreen()
        {
            // The whole reason the plan exists: no Build Health results and a never-connected device must
            // land INCOMPLETE and a non-green badge - never HEALTHY - through the real shared evaluator,
            // whatever the ambient editor mode/platform.
            GreenlightEvaluator.Report report = GreenlightEvaluator.Evaluate(
                buildHealthResults: null,
                snapshotState: new GreenlightDeviceSnapshot.State());

            Assert.AreEqual(GateOutcome.Incomplete, report.Outcome,
                "A report with no run evidence must be INCOMPLETE, not HEALTHY.");
            Assert.Greater(report.WaitCount, 0, "Unrun required evidence must surface as Wait rows.");
            Assert.AreNotEqual(StatusBadge.Severity.Pass, GreenlightEvaluator.BadgeSeverity(report.Outcome),
                "The badge for a no-evidence report must not be the green Pass pill.");
        }

        // ── HEALTHY is reachable, and only from machine-observed evidence ──

        /// <summary>The counterpart to the test above, and the point of the 2026-07-22 deletion: when every
        /// required gate HAS been observed green, the report reads HEALTHY. Before, six gates could only be
        /// satisfied by a human ticking a box, so a report that had genuinely proven everything the SDK can
        /// check still rendered INCOMPLETE - a permanently non-green verdict teaches people to ignore it.
        /// Scope limit, stated so it is not over-read: this synthesizes each gate's required proof, so it
        /// proves the EVALUATOR can reach HEALTHY over the real catalog, not that a producer can actually
        /// supply every proof. "No gate needs proof only a human can give" is pinned separately, by
        /// GateCatalogTests.EveryGate_IsObservableByTheSdk_NoHumanAttestationOnly.</summary>
        [Test]
        public void EveryRequiredGateObservedGreen_ReadsHealthy()
        {
            var ctx = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.Android,
                InstalledModules = HealthEnums.AllModuleBits,
                ModulesResolved = true,

            };

            var observations = GateCatalog.Canonical.All
                .Where(d => d.Requirement(ctx).Value == Requirement.Required)
                .Select(d => new GateObservation
                {
                    GateId = d.Id, Outcome = GateOutcome.Pass, ObservedProof = d.RequiredProof,
                    Evidence = "observed",
                })
                .ToList();

            Assert.IsNotEmpty(observations, "the canonical catalog must have required gates to observe");
            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx, observations);
            Assert.AreEqual(GateOutcome.Pass, report.Outcome,
                "with every required gate observed green, the report must be able to read HEALTHY");
            Assert.AreEqual("HEALTHY", GreenlightEvaluator.VerdictLabel(report.Outcome, 0, 0));
        }

        // ── Integration readiness is decidable before a build (2026-07-23) ──

        static EvaluationContext MobileContext() => new EvaluationContext
        {
            Mode = EvalMode.Full, Platform = EvalPlatform.Android,
            InstalledModules = HealthEnums.AllModuleBits,
            ModulesResolved = true,
        };

        static List<GateObservation> StaticObservationsAllGreen(EvaluationContext ctx) =>
            GateCatalog.Canonical.All
                .Where(d => d.Requirement(ctx).Value == Requirement.Required &&
                            (d.RequiredProof & ProofScope.DeviceDispatch) == 0)
                .Select(d => new GateObservation
                {
                    GateId = d.Id, Outcome = GateOutcome.Pass, ObservedProof = d.RequiredProof,
                    Evidence = "observed",
                })
                .ToList();

        [Test]
        public void CleanStaticSetup_IsIntegrationReady_BeforeAnyDeviceIsConnected()
        {
            // The pre-build contract: everything decidable without running the game is green, so the
            // integration verdict is green - while the device question stays honestly unanswered.
            EvaluationContext ctx = MobileContext();
            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx, StaticObservationsAllGreen(ctx));

            Assert.AreEqual(GateOutcome.Pass, report.IntegrationOutcome,
                "a statically clean project must be able to read ready before a device exists");
            Assert.AreEqual(GateOutcome.Incomplete, report.Outcome,
                "the combined answer still has no device evidence");
            GateResult device = report.Rows.Single(r => r.GateId == GateIds.DeviceVitals);
            Assert.AreEqual(GateOutcome.Incomplete, device.Outcome, "the device row keeps its own honest state");
        }

        [Test]
        public void FailingDeviceEvidence_DoesNotDragDownIntegrationReadiness()
        {
            EvaluationContext ctx = MobileContext();
            List<GateObservation> observations = StaticObservationsAllGreen(ctx);
            observations.Add(new GateObservation
            {
                GateId = GateIds.DeviceVitals, Outcome = GateOutcome.Fail,
                ObservedProof = ProofScope.DeviceDispatch, Evidence = "runtime vitals failing",
            });

            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx, observations);

            Assert.AreEqual(GateOutcome.Pass, report.IntegrationOutcome, "the integration itself is still ready");
            Assert.AreEqual(GateOutcome.Fail, report.Outcome, "the combined answer reports the device failure");
        }

        [Test]
        public void FailingStaticGate_StillFailsIntegrationReadiness()
        {
            EvaluationContext ctx = MobileContext();
            List<GateObservation> observations = StaticObservationsAllGreen(ctx);
            observations[0] = new GateObservation
            {
                GateId = observations[0].GateId, Outcome = GateOutcome.Fail,
                ObservedProof = ProofScope.Static, Evidence = "broken",
            };

            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx, observations);

            Assert.AreEqual(GateOutcome.Fail, report.IntegrationOutcome);
        }

        [Test]
        public void DeviceRows_DoNotEnterTheHeadlineCounts()
        {
            EvaluationContext ctx = MobileContext();
            GreenlightEvaluator.Report report =
                GreenlightEvaluator.ToReport(HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx,
                    StaticObservationsAllGreen(ctx)));

            Assert.AreEqual(0, report.WaitCount,
                "a never-connected device must not read as a pending integration check");
            Assert.IsTrue(report.Rows.Any(r => r.IsDeviceEvidence),
                "the device row is still rendered, under its own group");
        }
    }
}
