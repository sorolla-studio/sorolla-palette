using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sorolla.Palette.Health;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     Truth table for the populated canonical <see cref="GateCatalog"/> - the mode requirement table.
    ///     Pins the per-gate applicability under mode x platform x installed-modules, including the decided
    ///     Firebase-in-Prototype semantics (SdkRegistry marks Firebase FullRequired: required in Full,
    ///     applicable in Prototype only when installed), and asserts the populated catalog validates clean.
    /// </summary>
    public class GateCatalogTests
    {
        static EvaluationContext Ctx(EvalMode mode, EvalPlatform platform, SdkModule modules = SdkModule.None) =>
            new EvaluationContext { Mode = mode, Platform = platform, InstalledModules = modules };

        static Applicability AppOf(string gateId, EvaluationContext ctx) =>
            GateCatalog.Canonical.ById(gateId).Applicability(ctx).Value;

        // ── Catalog integrity ────────────────────────────────────────────

        [Test]
        public void Canonical_ValidatesClean()
        {
            IReadOnlyList<string> problems =
                GateCatalog.Validate(GateCatalog.Canonical.All, GateCatalog.SupportedContexts);
            Assert.IsEmpty(problems, "Populated canonical catalog must have no duplicate/unreachable gates.");
        }

        [Test]
        public void Canonical_HasNoDuplicateIds()
        {
            List<string> ids = GateCatalog.Canonical.All.Select(d => d.Id).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count());
        }

        [Test]
        public void Canonical_IsPopulated()
        {
            Assert.Greater(GateCatalog.Canonical.All.Count, 0);
        }

        [Test]
        public void EveryGate_CarriesAVersion()
        {
            foreach (GateDefinition def in GateCatalog.Canonical.All)
                Assert.IsFalse(string.IsNullOrEmpty(def.Version), $"Gate '{def.Id}' has no version.");
        }

        // ── Core SDK gates: applicable + required in BOTH modes ────────────

        [Test]
        public void CoreGates_ApplicableInBothModes_EvenWithNoModules()
        {
            foreach (string id in new[]
            {
                GateIds.BuildRequiredSdks, GateIds.BuildGameAnalyticsKeys,
                GateIds.BuildGameAnalyticsCredentials, GateIds.BuildFacebookPlatform,
            })
            {
                Assert.AreEqual(Applicability.Applicable, AppOf(id, Ctx(EvalMode.Prototype, EvalPlatform.Android)), id);
                Assert.AreEqual(Applicability.Applicable, AppOf(id, Ctx(EvalMode.Full, EvalPlatform.iOS)), id);
            }
        }

        [Test]
        public void CoreGate_IsRequired()
        {
            Assert.IsTrue(GateCatalog.Canonical.ById(GateIds.BuildGameAnalyticsKeys).Required);
        }

        // ── Firebase: the decided contradiction ────────────────────────────

        [Test]
        public void Firebase_RequiredInFull()
        {
            Assert.AreEqual(Applicability.Applicable,
                AppOf(GateIds.BuildFirebaseCoherence, Ctx(EvalMode.Full, EvalPlatform.Android)));
            Assert.IsTrue(GateCatalog.Canonical.ById(GateIds.BuildFirebaseCoherence).Required);
        }

        [Test]
        public void Firebase_NotApplicableInBarePrototype()
        {
            // Follows SdkRegistry (FullRequired = optional in Prototype, never uninstalled): a bare
            // Prototype build without Firebase is not flagged.
            Assert.AreEqual(Applicability.NotApplicable,
                AppOf(GateIds.BuildFirebaseCoherence, Ctx(EvalMode.Prototype, EvalPlatform.Android)));
        }

        [Test]
        public void Firebase_ApplicableInPrototype_WhenInstalled()
        {
            // If you ship Firebase in Prototype, it must still be coherent.
            Assert.AreEqual(Applicability.Applicable,
                AppOf(GateIds.BuildFirebaseCoherence, Ctx(EvalMode.Prototype, EvalPlatform.Android, SdkModule.Firebase)));
        }

        [Test]
        public void Firebase_UnknownMode_IsUnknown()
        {
            Assert.AreEqual(Applicability.Unknown,
                AppOf(GateIds.BuildFirebaseCoherence, Ctx(EvalMode.Unknown, EvalPlatform.Android)));
        }

        // ── Full-mode vendors (AppLovin MAX, Adjust) ───────────────────────

        [Test]
        public void FullModeVendors_ApplicableInFull_NotApplicableInBarePrototype()
        {
            foreach (string id in new[]
            {
                GateIds.BuildMaxSettings, GateIds.BuildAdjustSettings,
                GateIds.BuildAdjustResolvedVersion, GateIds.ManualAdjustPurchaseVerification,
            })
            {
                Assert.AreEqual(Applicability.Applicable, AppOf(id, Ctx(EvalMode.Full, EvalPlatform.Android)), id);
                Assert.AreEqual(Applicability.NotApplicable, AppOf(id, Ctx(EvalMode.Prototype, EvalPlatform.Android)), id);
            }
        }

        [Test]
        public void MaxSettings_ApplicableInPrototype_WhenInstalled()
        {
            Assert.AreEqual(Applicability.Applicable,
                AppOf(GateIds.BuildMaxSettings, Ctx(EvalMode.Prototype, EvalPlatform.Android, SdkModule.AppLovinMax)));
        }

        // ── Platform-gated (Android keystore + device facts) ───────────────

        [Test]
        public void AndroidKeystore_ApplicableOnAndroidOnly()
        {
            Assert.AreEqual(Applicability.Applicable, AppOf(GateIds.BuildAndroidKeystore, Ctx(EvalMode.Full, EvalPlatform.Android)));
            Assert.AreEqual(Applicability.NotApplicable, AppOf(GateIds.BuildAndroidKeystore, Ctx(EvalMode.Full, EvalPlatform.iOS)));
            Assert.AreEqual(Applicability.Unknown, AppOf(GateIds.BuildAndroidKeystore, Ctx(EvalMode.Full, EvalPlatform.Unknown)));
        }

        [Test]
        public void DeviceGates_ApplicableOnAndroidOnly()
        {
            foreach (string id in new[] { GateIds.DeviceReady, GateIds.DeviceAdvertisingId, GateIds.DeviceNoSdkErrors })
            {
                Assert.AreEqual(Applicability.Applicable, AppOf(id, Ctx(EvalMode.Full, EvalPlatform.Android)), id);
                Assert.AreEqual(Applicability.NotApplicable, AppOf(id, Ctx(EvalMode.Full, EvalPlatform.iOS)), id);
            }
        }

        [Test]
        public void DeviceReady_RequiresDeviceDispatchProof()
        {
            GateDefinition def = GateCatalog.Canonical.ById(GateIds.DeviceReady);
            Assert.IsTrue(def.Required);
            Assert.AreEqual(ProofScope.DeviceDispatch, def.RequiredProof);
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
                GateDefinition def = GateCatalog.Canonical.ById(id);
                Assert.IsTrue(def.Required, id);
                Assert.AreNotEqual(ProofScope.None, def.RequiredProof, id);
                Assert.IsFalse(def.RequiredProof.HasFlag(ProofScope.Static),
                    $"{id} must require proof a static editor check cannot supply.");
            }
        }
    }
}
