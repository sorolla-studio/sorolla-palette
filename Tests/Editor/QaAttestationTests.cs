using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sorolla.Palette.Editor.Greenlight;
using Sorolla.Palette.Health;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     Cycle 4b scoped attestation, hardened per the close review (C45-01..06): full field validation
    ///     (gate id / version / phase / actor / outcome / evidence / device GUID), atomic + corruption-aware
    ///     storage, and the HEALTHY control proven WITH Unity IAP installed through the real attestation
    ///     producer. All tests run against an INJECTED temp store path (SetUp/TearDown) so they never touch
    ///     live UserSettings evidence.
    /// </summary>
    public class QaAttestationTests
    {
        string _tempStore;

        [SetUp]
        public void SetUp()
        {
            _tempStore = Path.Combine(Path.GetTempPath(), $"sorolla-qa-attest-test-{Guid.NewGuid():N}.json");
            QaAttestationStore.PathOverride = _tempStore;
        }

        [TearDown]
        public void TearDown()
        {
            QaAttestationStore.PathOverride = null;
            if (_tempStore != null && File.Exists(_tempStore)) File.Delete(_tempStore);
            if (_tempStore != null && File.Exists(_tempStore + ".bak")) File.Delete(_tempStore + ".bak");
        }

        static readonly QaBuildIdentity Identity = new QaBuildIdentity("com.sorolla.game", "Android", "full", "1.4.0");
        static readonly DateTime Now = new DateTime(2026, 07, 13, 12, 0, 0, DateTimeKind.Utc);

        static QaAttestationExpectation Expect(
            ProofScope scope = ProofScope.VendorAccepted, string gateId = "g", string version = "1",
            string phase = "QaPass", string deviceGuid = null) =>
            new QaAttestationExpectation(gateId, version, phase, scope, Identity, deviceGuid);

        static QaAttestationRecord Rec(
            string schema = "1", string gateId = "g", string version = "1", string phase = "QaPass",
            string actor = "tester", string appId = "com.sorolla.game", string platform = "Android",
            string mode = "full", string appVersion = "1.4.0", string outcome = "Pass",
            string scope = "VendorAccepted", string note = "did X, observed Y", string deviceGuid = null,
            DateTime? ts = null)
        {
            return new QaAttestationRecord
            {
                schema = schema, gateId = gateId, gateVersion = version, phase = phase, actor = actor,
                timestampUtc = (ts ?? Now.AddHours(-1)).ToString("o"),
                applicationId = appId, platform = platform, mode = mode, appVersion = appVersion,
                outcome = outcome, proofScope = scope, evidenceNote = note, deviceBuildGuid = deviceGuid,
            };
        }

        static AttestationValidity Eval(QaAttestationRecord r, QaAttestationExpectation e) =>
            QaAttestationValidator.Evaluate(r, e, Now, out _);

        // ── Validator: happy path + Invalid (structural) ─────────────────

        [Test] public void MatchingFreshAttestation_IsValid() => Assert.AreEqual(AttestationValidity.Valid, Eval(Rec(), Expect()));
        [Test] public void NullRecord_IsMissing() => Assert.AreEqual(AttestationValidity.Missing, Eval(null, Expect()));
        [Test] public void UnknownSchema_IsInvalid() => Assert.AreEqual(AttestationValidity.Invalid, Eval(Rec(schema: "99"), Expect()));
        [Test] public void WrongGateId_IsInvalid() => Assert.AreEqual(AttestationValidity.Invalid, Eval(Rec(gateId: "other"), Expect()));
        [Test] public void BlankActor_IsInvalid() => Assert.AreEqual(AttestationValidity.Invalid, Eval(Rec(actor: "  "), Expect()));
        [Test] public void NonPassOutcome_IsInvalid() => Assert.AreEqual(AttestationValidity.Invalid, Eval(Rec(outcome: "Fail"), Expect()));
        [Test] public void MissingIdentity_IsInvalid() => Assert.AreEqual(AttestationValidity.Invalid, Eval(Rec(appId: ""), Expect()));
        [Test] public void WrongProofScope_IsInvalid() => Assert.AreEqual(AttestationValidity.Invalid, Eval(Rec(scope: "DeviceDispatch"), Expect(ProofScope.VendorAccepted)));
        [Test] public void VendorGateWithoutEvidenceNote_IsInvalid() => Assert.AreEqual(AttestationValidity.Invalid, Eval(Rec(note: "  "), Expect(ProofScope.VendorAccepted)));
        [Test] public void FutureTimestamp_IsInvalid() => Assert.AreEqual(AttestationValidity.Invalid, Eval(Rec(ts: Now.AddDays(1)), Expect()));

        // ── Validator: Stale (was real, not for THIS build/version/phase) ─

        [Test] public void GateVersionChanged_IsStale() => Assert.AreEqual(AttestationValidity.Stale, Eval(Rec(version: "1"), Expect(version: "2")));
        [Test] public void PhaseMismatch_IsStale() => Assert.AreEqual(AttestationValidity.Stale, Eval(Rec(phase: "QaPass"), Expect(phase: "ReleaseShip")));
        [Test] public void WrongGame_IsStale() => Assert.AreEqual(AttestationValidity.Stale, Eval(Rec(appId: "com.other.game"), Expect()));
        [Test] public void WrongBuildVersion_IsStale() => Assert.AreEqual(AttestationValidity.Stale, Eval(Rec(appVersion: "1.3.0"), Expect()));
        [Test] public void Expired_IsStale() => Assert.AreEqual(AttestationValidity.Stale, Eval(Rec(ts: Now.AddDays(-30)), Expect()));

        // ── Validator: device-session build GUID binding (C45-05) ─────────

        [Test] public void DeviceGate_MatchingGuid_IsValid() =>
            Assert.AreEqual(AttestationValidity.Valid, Eval(Rec(scope: "DeviceDispatch", note: null, deviceGuid: "guid-1"),
                Expect(ProofScope.DeviceDispatch, deviceGuid: "guid-1")));

        [Test] public void DeviceGate_MissingRecordGuid_IsStale() =>
            Assert.AreEqual(AttestationValidity.Stale, Eval(Rec(scope: "DeviceDispatch", note: null, deviceGuid: null),
                Expect(ProofScope.DeviceDispatch, deviceGuid: "guid-1")));

        [Test] public void DeviceGate_NoConnectedBuild_IsStale() =>
            Assert.AreEqual(AttestationValidity.Stale, Eval(Rec(scope: "DeviceDispatch", note: null, deviceGuid: "guid-1"),
                Expect(ProofScope.DeviceDispatch, deviceGuid: null)));

        [Test] public void DeviceGate_GuidMismatch_IsStale() =>
            Assert.AreEqual(AttestationValidity.Stale, Eval(Rec(scope: "DeviceDispatch", note: null, deviceGuid: "guid-1"),
                Expect(ProofScope.DeviceDispatch, deviceGuid: "guid-2")));

        [Test]
        public void DuplicateConflict_IsRejected()
        {
            var records = new List<QaAttestationRecord> { Rec(gateId: "dup"), Rec(gateId: "dup") };
            QaAttestationRecord picked = QaAttestationStore.ForGate(records, "dup");
            Assert.AreEqual(AttestationValidity.Invalid, Eval(picked, Expect(gateId: "dup")));
        }

        // ── Store: isolation, atomicity, corruption (C45-03/04) ───────────

        [Test]
        public void Store_NoFile_IsEmpty_NotCorrupt()
        {
            List<QaAttestationRecord> records = QaAttestationStore.Load(out bool corrupt);
            Assert.IsFalse(corrupt);
            Assert.IsEmpty(records);
        }

        [Test]
        public void Store_CorruptFile_ReportsCorrupt()
        {
            File.WriteAllText(_tempStore, "{ this is not valid json ][");
            QaAttestationStore.Load(out bool corrupt);
            Assert.IsTrue(corrupt, "a corrupt store must be reported, not silently emptied");
        }

        [Test]
        public void Store_RecordThenLoad_RoundTripsAndReplacesPerGate()
        {
            QaAttestationStore.Record(Rec(gateId: "a", note: "first"));
            QaAttestationStore.Record(Rec(gateId: "b", note: "second"));
            QaAttestationStore.Record(Rec(gateId: "a", note: "updated")); // replaces gate a

            List<QaAttestationRecord> records = QaAttestationStore.Load(out bool corrupt);
            Assert.IsFalse(corrupt);
            Assert.AreEqual(2, records.Count);
            Assert.AreEqual("updated", records.Single(r => r.gateId == "a").evidenceNote);
            Assert.IsFalse(File.Exists(_tempStore + ".bak"), "atomic replace must not leave a backup behind");
            Assert.IsFalse(File.Exists(_tempStore + ".tmp"), "atomic replace must not leave a temp behind");
        }

        // ── HEALTHY control WITH Unity IAP (C45-02) ───────────────────────

        [Test]
        public void FullyAttestedBuildWithUnityIap_ReachesHealthy_Synthetic()
        {
            // Every required QaPass gate satisfied at its required proof scope, WITH Unity IAP installed (the
            // iap.store_configured gate is Required and must be satisfiable) → PASS.
            var ctx = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.Android,
                InstalledModules = HealthEnums.AllModuleBits, // includes UnityIap
                IntendedTargets = HealthEnums.AllTargetBits,  // distribution (device gates)
                CommerceTargets = HealthEnums.AllTargetBits,  // commerce (store gate) - Android declared (B2)
                RequestedPhase = GatePhase.QaPass, ModulesResolved = true, Profile = ReportProfile.SorollaFull,
            };
            Assert.AreEqual(Requirement.Required, GateCatalog.Canonical.ById(GateIds.IapStoreConfigured).Requirement(ctx).Value,
                "with Unity IAP installed and the active platform a declared commerce target, the store gate is Required");

            var observations = new List<GateObservation>();
            foreach (GateDefinition def in GateCatalog.Canonical.All)
            {
                if ((def.Phases & GatePhase.QaPass) == 0) continue;
                if (def.Requirement(ctx).Value != Requirement.Required) continue;
                observations.Add(new GateObservation { GateId = def.Id, Outcome = GateOutcome.Pass, ObservedProof = def.RequiredProof });
            }
            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx, observations);
            Assert.IsEmpty(report.ValidationErrors);
            Assert.AreEqual(GateOutcome.Pass, report.Outcome, "an IAP game with everything satisfied must be able to reach HEALTHY");
        }

        [Test]
        public void AttestedManualGates_WithUnityIap_ThroughRealPath_ReachHealthy()
        {
            QaBuildIdentity id = QaBuildIdentity.Current();
            EvalMode mode = id.Mode == "full" ? EvalMode.Full : id.Mode == "prototype" ? EvalMode.Prototype : EvalMode.Unknown;
            EvalPlatform platform = id.Platform == "Android" ? EvalPlatform.Android
                : id.Platform == "IPhonePlayer" ? EvalPlatform.iOS : EvalPlatform.Unknown;
            if (mode == EvalMode.Unknown || platform != EvalPlatform.Android)
                Assert.Ignore("HEALTHY control requires a configured SDK mode + Android build target (ambient).");

            const string deviceGuid = "test-build-guid";
            string[] manualGateIds =
            {
                GateIds.ManualGaPlatformRegistered, GateIds.ManualCrossVendorDashboardDrift,
                GateIds.ManualAdjustPurchaseVerification, GateIds.ManualRelaunchPersistence,
                GateIds.ManualBackgroundResumeCycle, GateIds.IapStoreConfigured,
            };

            // Attest each manual gate through the REAL producer against the current identity (isolated store).
            foreach (string gid in manualGateIds)
                Assert.IsTrue(GreenlightAdapter.AttestManualGate(gid, "qa-bot", "verified on this build", deviceGuid),
                    $"attesting {gid} should succeed with a note + connected build");

            var ctx = new EvaluationContext
            {
                Mode = mode, Platform = platform,
                InstalledModules = SdkModule.GameAnalytics | SdkModule.Facebook | SdkModule.Firebase |
                                   SdkModule.AppLovinMax | SdkModule.Adjust | SdkModule.UnityIap,
                IntendedTargets = HealthEnums.AllTargetBits, CommerceTargets = HealthEnums.AllTargetBits, // B2
                RequestedPhase = GatePhase.QaPass, ModulesResolved = true, Profile = ReportProfile.SorollaFull,
            };

            // The non-manual Required gates (build + device) are supplied as observations at their required
            // proof; the manual + store-config gates come from the real attestation seam.
            var observations = new List<GateObservation>();
            observations.AddRange(GreenlightAdapter.ManualObservations(ctx, deviceGuid)); // real attestation seam
            foreach (GateDefinition def in GateCatalog.Canonical.All)
            {
                if ((def.Phases & GatePhase.QaPass) == 0) continue;
                if (def.Id.StartsWith("manual.") || def.Id == GateIds.IapStoreConfigured) continue;
                if (def.Requirement(ctx).Value != Requirement.Required) continue;
                observations.Add(new GateObservation { GateId = def.Id, Outcome = GateOutcome.Pass, ObservedProof = def.RequiredProof });
            }

            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx, observations);
            Assert.IsEmpty(report.ValidationErrors);
            Assert.AreEqual(GateOutcome.Pass, report.Outcome, "attesting every manual + IAP gate + full evidence must reach HEALTHY");
            Assert.AreEqual(GateOutcome.Pass, report.Rows.Single(r => r.GateId == GateIds.IapStoreConfigured).Outcome,
                "the IAP gate must be a scoped-attestation PASS with Unity IAP installed");
        }

        // ── Adapter seam: store → adapter → emitted observation ───────────

        [Test]
        public void MatchingAttestation_AdapterEmitsPassAtRequiredScope()
        {
            const string gate = GateIds.ManualGaPlatformRegistered;
            ProofScope required = GateCatalog.Canonical.ById(gate).RequiredProof;
            var ctx = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.Android,
                InstalledModules = SdkModule.None, RequestedPhase = GatePhase.QaPass, ModulesResolved = true,
                Profile = ReportProfile.SorollaFull,
            };
            Assert.IsTrue(GreenlightAdapter.AttestManualGate(gate, "qa-bot", "did the dashboard check", null));

            GateObservation obs = GreenlightAdapter.ManualObservations(ctx, null).Single(o => o.GateId == gate);
            Assert.AreEqual(GateOutcome.Pass, obs.Outcome, "a matching attestation must emit PASS");
            Assert.AreEqual(required, obs.ObservedProof, "the emitted observation must carry the gate's required proof scope");
            StringAssert.Contains("did the dashboard check", obs.Evidence,
                "the evidence note must ride into the row for canonical-report provenance (F4)");
        }

        [Test]
        public void StaleAttestation_AdapterEmitsIncomplete_AndGateResolvesIncomplete()
        {
            const string gate = GateIds.ManualGaPlatformRegistered;
            ProofScope required = GateCatalog.Canonical.ById(gate).RequiredProof;
            var ctx = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.Android,
                InstalledModules = SdkModule.None, RequestedPhase = GatePhase.QaPass, ModulesResolved = true,
                Profile = ReportProfile.SorollaFull,
            };
            QaBuildIdentity cur = QaBuildIdentity.Current();
            QaAttestationStore.Record(new QaAttestationRecord
            {
                schema = "1", gateId = gate, gateVersion = GateCatalog.Canonical.ById(gate).Version,
                phase = "QaPass", actor = "tester", timestampUtc = DateTime.UtcNow.ToString("o"),
                applicationId = "com.other.game", // wrong game → stale
                platform = cur.Platform, mode = cur.Mode, appVersion = cur.AppVersion,
                outcome = "Pass", proofScope = required.ToString(), evidenceNote = "note",
            });

            GateObservation obs = GreenlightAdapter.ManualObservations(ctx, null).Single(o => o.GateId == gate);
            Assert.AreEqual(GateOutcome.Incomplete, obs.Outcome, "a stale attestation must not emit PASS");
            Assert.AreEqual(ProofScope.None, obs.ObservedProof);

            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx, new List<GateObservation> { obs });
            Assert.AreEqual(GateOutcome.Incomplete, report.Rows.Single(r => r.GateId == gate).Outcome);
        }

        [Test]
        public void CorruptStore_MakesManualGatesIncomplete()
        {
            File.WriteAllText(_tempStore, "not json");
            var ctx = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.Android,
                InstalledModules = SdkModule.None, RequestedPhase = GatePhase.QaPass, ModulesResolved = true,
                Profile = ReportProfile.SorollaFull,
            };
            List<GateObservation> obs = GreenlightAdapter.ManualObservations(ctx, null).ToList();
            Assert.IsTrue(obs.Count > 0 && obs.All(o => o.Outcome == GateOutcome.Incomplete),
                "a corrupt attestation store must make every manual gate INCOMPLETE, not silently empty/pass");
        }
    }
}
