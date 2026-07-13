using System.Linq;
using NUnit.Framework;
using Sorolla.Palette.Health;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     Truth table for the populated canonical <see cref="GateCatalog"/> - the mode requirement table on
    ///     the repaired 4-state model (review C3-02). Pins each gate's context-derived requirement
    ///     (Required | Optional | NotApplicable | Unknown) under mode x platform, including the decided
    ///     Firebase-in-Prototype semantics: Required in Full, Optional in Prototype (evaluated if present,
    ///     cleanly skipped if not) - never NotApplicable, which would discard a real Firebase observation.
    /// </summary>
    public class GateCatalogTests
    {
        static EvaluationContext Ctx(EvalMode mode, EvalPlatform platform, SdkModule modules = SdkModule.None,
            DistributionTargets targets = HealthEnums.AllTargetBits,
            DistributionTargets commerce = HealthEnums.AllTargetBits) =>
            new EvaluationContext
            {
                Mode = mode, Platform = platform, InstalledModules = modules, IntendedTargets = targets,
                CommerceTargets = commerce, RequestedPhase = GatePhase.QaPass,
            };

        static Requirement ReqOf(string gateId, EvaluationContext ctx) =>
            GateCatalog.Canonical.ById(gateId).Requirement(ctx).Value;

        // ── Catalog integrity ────────────────────────────────────────────

        [Test]
        public void Canonical_HasNoDuplicateIds()
        {
            var ids = GateCatalog.Canonical.All.Select(d => d.Id).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count());
        }

        [Test]
        public void Canonical_IsPopulated()
        {
            Assert.Greater(GateCatalog.Canonical.All.Count, 0);
        }

        [Test]
        public void EveryGate_CarriesAVersionAndAPhase()
        {
            foreach (GateDefinition def in GateCatalog.Canonical.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(def.Version), $"Gate '{def.Id}' has no version.");
                Assert.AreNotEqual(GatePhase.None, def.Phases, $"Gate '{def.Id}' has no phase.");
            }
        }

        // ── Core SDK gates: Required in BOTH modes ─────────────────────────

        [Test]
        public void CoreGates_RequiredInBothModes_EvenWithNoModules()
        {
            foreach (string id in new[]
            {
                GateIds.BuildRequiredSdks, GateIds.BuildGameAnalyticsKeys,
                GateIds.BuildGameAnalyticsCredentials, GateIds.BuildFacebookPlatform,
            })
            {
                Assert.AreEqual(Requirement.Required, ReqOf(id, Ctx(EvalMode.Prototype, EvalPlatform.Android)), id);
                Assert.AreEqual(Requirement.Required, ReqOf(id, Ctx(EvalMode.Full, EvalPlatform.iOS)), id);
            }
        }

        // ── Firebase: the decided contradiction (Optional-in-Prototype) ────

        [Test]
        public void Firebase_RequiredInFull()
        {
            Assert.AreEqual(Requirement.Required, ReqOf(GateIds.BuildFirebaseCoherence, Ctx(EvalMode.Full, EvalPlatform.Android)));
        }

        [Test]
        public void Firebase_OptionalInPrototype_NotNotApplicable()
        {
            // The C3-04-correct expression: Optional preserves a real Firebase observation and skips cleanly
            // when absent - never NotApplicable (which would discard evidence).
            Requirement req = ReqOf(GateIds.BuildFirebaseCoherence, Ctx(EvalMode.Prototype, EvalPlatform.Android));
            Assert.AreEqual(Requirement.Optional, req);
        }

        [Test]
        public void Firebase_UnknownMode_IsUnknown()
        {
            Assert.AreEqual(Requirement.Unknown, ReqOf(GateIds.BuildFirebaseCoherence, Ctx(EvalMode.Unknown, EvalPlatform.Android)));
        }

        // ── Full-mode vendors ──────────────────────────────────────────────

        [Test]
        public void FullModeVendorSettings_RequiredInFull_OptionalInPrototype()
        {
            foreach (string id in new[]
            {
                GateIds.BuildMaxSettings, GateIds.BuildAdjustSettings, GateIds.BuildAdjustResolvedVersion,
            })
            {
                Assert.AreEqual(Requirement.Required, ReqOf(id, Ctx(EvalMode.Full, EvalPlatform.Android)), id);
                Assert.AreEqual(Requirement.Optional, ReqOf(id, Ctx(EvalMode.Prototype, EvalPlatform.Android)), id);
            }
        }

        [Test]
        public void FirebaseConfig_RequiredInFull_OptionalInPrototype()
        {
            // C4-05: the Firebase config-files gate follows Firebase's own requirement, not the advisory list.
            Assert.AreEqual(Requirement.Required, ReqOf(GateIds.BuildFirebaseConfig, Ctx(EvalMode.Full, EvalPlatform.Android)));
            Assert.AreEqual(Requirement.Optional, ReqOf(GateIds.BuildFirebaseConfig, Ctx(EvalMode.Prototype, EvalPlatform.Android)));
        }

        [Test]
        public void AdjustPurchaseVerificationManual_RequiredInFull_NotApplicableInPrototype()
        {
            Assert.AreEqual(Requirement.Required,
                ReqOf(GateIds.ManualAdjustPurchaseVerification, Ctx(EvalMode.Full, EvalPlatform.Android)));
            Assert.AreEqual(Requirement.NotApplicable,
                ReqOf(GateIds.ManualAdjustPurchaseVerification, Ctx(EvalMode.Prototype, EvalPlatform.Android)));
        }

        // ── Platform-gated ─────────────────────────────────────────────────

        [Test]
        public void AndroidKeystore_RequiredOnAndroid_OptionalOffAndroid()
        {
            // Optional (not NotApplicable) off-Android: BuildValidator still emits a "Skipped" result there,
            // and an observation for a NotApplicable gate would be a context-mismatch error.
            Assert.AreEqual(Requirement.Required, ReqOf(GateIds.BuildAndroidKeystore, Ctx(EvalMode.Full, EvalPlatform.Android)));
            Assert.AreEqual(Requirement.Optional, ReqOf(GateIds.BuildAndroidKeystore, Ctx(EvalMode.Full, EvalPlatform.iOS)));
            Assert.AreEqual(Requirement.Unknown, ReqOf(GateIds.BuildAndroidKeystore, Ctx(EvalMode.Full, EvalPlatform.Unknown)));
        }

        [Test]
        public void DeviceGates_ApplicabilityFollowsIntendedTarget_NotCollector()
        {
            // F1: device evidence is Required on an INTENDED release platform - INCLUDING iOS, where no
            // collector exists yet (it will omit → INCOMPLETE downstream, not vanish as NotApplicable).
            // NotApplicable only when the active platform is not a declared target; Unknown when undeclared.
            Assert.AreEqual(Requirement.Required,
                ReqOf(GateIds.DeviceReady, Ctx(EvalMode.Full, EvalPlatform.Android, targets: DistributionTargets.Android)));
            Assert.AreEqual(Requirement.Required,
                ReqOf(GateIds.DeviceReady, Ctx(EvalMode.Full, EvalPlatform.iOS, targets: DistributionTargets.iOS)));
            Assert.AreEqual(Requirement.NotApplicable,
                ReqOf(GateIds.DeviceReady, Ctx(EvalMode.Full, EvalPlatform.Android, targets: DistributionTargets.iOS)));
            Assert.AreEqual(Requirement.Unknown,
                ReqOf(GateIds.DeviceReady, Ctx(EvalMode.Full, EvalPlatform.Android, targets: DistributionTargets.None)));
            // no_sdk_errors follows the same rule and is Required (not merely Optional) on an intended platform,
            // so an intended platform with no collector is INCOMPLETE, not a green skip.
            Assert.AreEqual(Requirement.Required,
                ReqOf(GateIds.DeviceNoSdkErrors, Ctx(EvalMode.Full, EvalPlatform.iOS, targets: DistributionTargets.iOS)));
        }

        [Test]
        public void DeviceReady_RequiresDeviceDispatchProof()
        {
            Assert.AreEqual(ProofScope.DeviceDispatch, GateCatalog.Canonical.ById(GateIds.DeviceReady).RequiredProof);
        }

        [Test]
        public void ReleaseOnlyGates_ExcludedFromQaPass_TaggedReleaseShip()
        {
            // C4-04: a QA-pass report must not select release-only checks, so a "Skipped (QA Pass profile)"
            // Valid can never read as PASS.
            foreach (string id in new[]
            {
                GateIds.BuildAndroidKeystore, GateIds.BuildAdjustSandboxMode,
                GateIds.BuildSdkPin, GateIds.BuildPrototypeModeIntent,
            })
            {
                GatePhase phases = GateCatalog.Canonical.ById(id).Phases;
                Assert.AreEqual(GatePhase.None, phases & GatePhase.QaPass, $"{id} must not be a QaPass gate.");
                Assert.AreNotEqual(GatePhase.None, phases & GatePhase.ReleaseShip, $"{id} must be a ReleaseShip gate.");
            }
        }

        [Test]
        public void ReleaseShipPhase_ReachesReleaseOnlyGatesAndCore_QaPassExcludesReleaseOnly()
        {
            // Item 7 end-to-end: the Release profile makes release-only gates reachable through the real
            // evaluator, while QA Pass excludes them; and ReleaseShip keeps the core prerequisites.
            var release = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.Android,
                InstalledModules = HealthEnums.AllModuleBits, RequestedPhase = GatePhase.ReleaseShip,
            };
            HealthReport releaseReport = HealthEvaluator.Evaluate(
                GateCatalog.Canonical, release, new System.Collections.Generic.List<GateObservation>());
            Assert.IsTrue(releaseReport.Rows.Any(r => r.GateId == GateIds.BuildAndroidKeystore),
                "release-only keystore reachable under ReleaseShip");
            Assert.IsTrue(releaseReport.Rows.Any(r => r.GateId == GateIds.BuildRequiredSdks),
                "core prerequisite present under ReleaseShip");

            var qa = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.Android,
                InstalledModules = HealthEnums.AllModuleBits, RequestedPhase = GatePhase.QaPass,
            };
            HealthReport qaReport = HealthEvaluator.Evaluate(
                GateCatalog.Canonical, qa, new System.Collections.Generic.List<GateObservation>());
            Assert.IsFalse(qaReport.Rows.Any(r => r.GateId == GateIds.BuildAndroidKeystore),
                "release-only keystore excluded under QaPass");
        }

        [Test]
        public void IapStoreGate_KeysOnCommerceTargets_NotDistribution()
        {
            // B2: store config is Required only when Unity IAP is installed AND the active platform is a
            // declared COMMERCE target - independent of distribution. A game distributing its app on Android
            // (distribution=Android) while selling IAP only on iOS (commerce=iOS) has a NotApplicable Android
            // store gate, even though its Android device gates apply.
            Assert.AreEqual(Requirement.Required, ReqOf(GateIds.IapStoreConfigured,
                Ctx(EvalMode.Full, EvalPlatform.Android, SdkModule.UnityIap, targets: DistributionTargets.Android, commerce: DistributionTargets.Android)));
            // app ships on Android but sells IAP only on iOS → Android store gate NotApplicable with a reason.
            Requirement shipsAndroidSellsIos = GateCatalog.Canonical.ById(GateIds.IapStoreConfigured)
                .Requirement(Ctx(EvalMode.Full, EvalPlatform.Android, SdkModule.UnityIap, targets: DistributionTargets.Android, commerce: DistributionTargets.iOS)).Value;
            Assert.AreEqual(Requirement.NotApplicable, shipsAndroidSellsIos);
            // Unity IAP absent → NotApplicable regardless of commerce.
            Assert.AreEqual(Requirement.NotApplicable, ReqOf(GateIds.IapStoreConfigured,
                Ctx(EvalMode.Full, EvalPlatform.Android, SdkModule.None, commerce: DistributionTargets.Android)));
            // installed + on-platform but commerce undeclared → Unknown (fail closed), not a silent skip.
            Assert.AreEqual(Requirement.Unknown, ReqOf(GateIds.IapStoreConfigured,
                Ctx(EvalMode.Full, EvalPlatform.Android, SdkModule.UnityIap, commerce: DistributionTargets.None)));
            Assert.AreEqual(ProofScope.VendorAccepted, GateCatalog.Canonical.ById(GateIds.IapStoreConfigured).RequiredProof);
        }

        [Test]
        public void IapTrackingGate_IsSeparateFromStore_DeviceProof_RequiredWhenIapInstalled()
        {
            // F5: the wiring gate is a DIFFERENT id, Required whenever Unity IAP is installed (code wiring is
            // platform-independent), proven by device dispatch - NOT the store's vendor attestation.
            Assert.AreEqual(Requirement.Required, ReqOf(GateIds.IapTrackingAttached,
                Ctx(EvalMode.Full, EvalPlatform.iOS, SdkModule.UnityIap, DistributionTargets.iOS)));
            Assert.AreEqual(Requirement.NotApplicable, ReqOf(GateIds.IapTrackingAttached,
                Ctx(EvalMode.Full, EvalPlatform.Android, SdkModule.None, DistributionTargets.Android)));
            Assert.AreEqual(ProofScope.DeviceDispatch,
                GateCatalog.Canonical.ById(GateIds.IapTrackingAttached).RequiredProof);
            Assert.AreNotEqual(
                GateCatalog.Canonical.ById(GateIds.IapStoreConfigured).RequiredProof,
                GateCatalog.Canonical.ById(GateIds.IapTrackingAttached).RequiredProof,
                "store config (vendor) and tracking wiring (device) must require different proof classes");
        }

        // ── Manual gates require unscoped-tick-defeating proof ─────────────

        [Test]
        public void ManualGates_RequireVendorOrDeviceProof_NotStatic()
        {
            foreach (string id in new[]
            {
                GateIds.ManualGaPlatformRegistered, GateIds.ManualCrossVendorDashboardDrift,
                GateIds.ManualAdjustPurchaseVerification, GateIds.ManualRelaunchPersistence,
                GateIds.ManualBackgroundResumeCycle,
            })
            {
                ProofScope proof = GateCatalog.Canonical.ById(id).RequiredProof;
                Assert.AreNotEqual(ProofScope.None, proof, id);
                Assert.IsFalse(proof.HasFlag(ProofScope.Static),
                    $"{id} must require proof a static editor check cannot supply.");
            }
        }
    }
}
