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
        static EvaluationContext Ctx(EvalMode mode, EvalPlatform platform, SdkModule modules = SdkModule.None) =>
            new EvaluationContext
            {
                Mode = mode, Platform = platform, InstalledModules = modules, RequestedPhase = GatePhase.QaPass,
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
        public void DeviceReady_RequiredOnAndroid_NotApplicableOnIOS()
        {
            Assert.AreEqual(Requirement.Required, ReqOf(GateIds.DeviceReady, Ctx(EvalMode.Full, EvalPlatform.Android)));
            Assert.AreEqual(Requirement.NotApplicable, ReqOf(GateIds.DeviceReady, Ctx(EvalMode.Full, EvalPlatform.iOS)));
        }

        [Test]
        public void DeviceReady_RequiresDeviceDispatchProof()
        {
            Assert.AreEqual(ProofScope.DeviceDispatch, GateCatalog.Canonical.ById(GateIds.DeviceReady).RequiredProof);
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
