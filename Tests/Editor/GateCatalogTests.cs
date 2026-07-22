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
                Profile = ReportProfile.SorollaFull,
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
                GateIds.BuildMaxSettings, GateIds.BuildAdjustSettings,
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
        public void DeviceGates_ApplicabilityFollowsActiveBuildTarget()
        {
            // The active build target IS the studio's declared intent (platform declarations deleted
            // 2026-07-20): device evidence is Required on either mobile target, NotApplicable off mobile.
            Assert.AreEqual(Requirement.Required,
                ReqOf(GateIds.DeviceNoSdkErrors, Ctx(EvalMode.Full, EvalPlatform.Android)));
            Assert.AreEqual(Requirement.Required,
                ReqOf(GateIds.DeviceNoSdkErrors, Ctx(EvalMode.Full, EvalPlatform.iOS)));
            Assert.AreEqual(Requirement.NotApplicable,
                ReqOf(GateIds.DeviceNoSdkErrors, Ctx(EvalMode.Full, EvalPlatform.Unknown)));
        }

        [Test]
        public void DeviceGates_RequireDeviceDispatchProof()
        {
            Assert.AreEqual(ProofScope.DeviceDispatch,
                GateCatalog.Canonical.ById(GateIds.DeviceNoSdkErrors).RequiredProof);
        }

        [Test]
        public void ReleaseOnlyGates_ExcludedFromQaPass_TaggedReleaseShip()
        {
            // C4-04: a QA-pass report must not select store-submission checks - the validator always runs
            // them, and phase selection is what keeps them out of a studio/QA report.
            foreach (string id in new[]
            {
                GateIds.BuildAndroidKeystore, GateIds.BuildAdjustSandboxMode,
            })
            {
                GatePhase phases = GateCatalog.Canonical.ById(id).Phases;
                Assert.AreEqual(GatePhase.None, phases & GatePhase.QaPass, $"{id} must not be a QaPass gate.");
                Assert.AreNotEqual(GatePhase.None, phases & GatePhase.ReleaseShip, $"{id} must be a ReleaseShip gate.");
            }
        }

        /// <summary>`build.sdk_pin` is deliberately NOT release-only (2026-07-22). A studio pinned to a branch
        /// is on an SDK line Sorolla never certified; it must see that in its own window, with the fix, not
        /// only in Sorolla's release-phase report. This is the direct replacement for the deleted indirect
        /// mechanism (uncertified pin → invariant rows INCOMPLETE), so if this gate ever loses the QaPass
        /// phase, nothing tells a studio it is on the development line.</summary>
        [Test]
        public void SdkPinGate_IsVisibleToStudios_NotReleaseOnly()
        {
            GatePhase phases = GateCatalog.Canonical.ById(GateIds.BuildSdkPin).Phases;
            Assert.AreNotEqual(GatePhase.None, phases & GatePhase.QaPass, "the pin check must reach a studio report");

            var studioCtx = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.Android,
                InstalledModules = HealthEnums.AllModuleBits, RequestedPhase = GatePhase.QaPass,
                Profile = ReportProfile.Studio, Certification = SdkCertification.Uncertified,
            };
            HealthReport report = HealthEvaluator.Evaluate(
                GateCatalog.Canonical, studioCtx,
                new System.Collections.Generic.List<GateObservation>
                {
                    new GateObservation
                    {
                        GateId = GateIds.BuildSdkPin, Outcome = GateOutcome.PassWithCaveats,
                        ObservedProof = ProofScope.Static, Evidence = "pinned to master",
                        FixHint = "Pin com.sorolla.sdk to a published tag",
                    },
                });
            GateResult row = report.Rows.Single(r => r.GateId == GateIds.BuildSdkPin);
            Assert.AreEqual(GateOutcome.PassWithCaveats, row.Outcome);
            Assert.AreNotEqual(GateOutcome.Pass, report.Outcome,
                "a branch-pinned studio build must not render a clean green verdict");
        }

        [Test]
        public void ReleaseShipPhase_ReachesReleaseOnlyGatesAndCore_QaPassExcludesReleaseOnly()
        {
            // End-to-end: the release phase (what the internal Sorolla window asks for) makes release-only
            // gates reachable through the real evaluator, while the QA-pass phase (a studio window) excludes
            // them; and ReleaseShip keeps the core prerequisites.
            var release = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.Android,
                InstalledModules = HealthEnums.AllModuleBits, RequestedPhase = GatePhase.ReleaseShip,
                Profile = ReportProfile.SorollaFull,
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
                Profile = ReportProfile.SorollaFull,
            };
            HealthReport qaReport = HealthEvaluator.Evaluate(
                GateCatalog.Canonical, qa, new System.Collections.Generic.List<GateObservation>());
            Assert.IsFalse(qaReport.Rows.Any(r => r.GateId == GateIds.BuildAndroidKeystore),
                "release-only keystore excluded under QaPass");
        }

        // ── Every gate is machine-checkable (2026-07-22 deletion) ──────────

        /// <summary>The catalog may only contain gates the SDK can OBSERVE for itself: a static repo/config
        /// fact, or a live device snapshot. A gate whose only possible evidence is a human ticking a box was
        /// ceremony, not proof - the six such gates were deleted 2026-07-22, and this pins that none returns.
        /// A vendor-dashboard fact can come back only with a real probe behind it (the GameAnalytics and
        /// Facebook credential probes are the shape to copy).</summary>
        [Test]
        public void EveryGate_IsObservableByTheSdk_NoHumanAttestationOnly()
        {
            foreach (GateDefinition def in GateCatalog.Canonical.All)
            {
                Assert.AreNotEqual(ProofScope.None, def.RequiredProof, def.Id);
                // HasFlag, not equality: RequiredProof is [Flags] and the evaluator demands EVERY required
                // bit, so `VendorAccepted | Static` would still be unsatisfiable without a human claim.
                Assert.IsFalse(def.RequiredProof.HasFlag(ProofScope.VendorAccepted),
                    $"Gate '{def.Id}' requires vendor-accepted proof, which only an out-of-band human claim " +
                    "can supply. Either give it a probe the SDK can run, or it does not belong in the catalog.");
            }
        }

        /// <summary>Counterpart to the deleted `InvariantGates_AreExactlyTheCertifiedSet`: the Invariant set
        /// is currently EMPTY, and that is load-bearing rather than incidental. An Invariant gate is excused
        /// by the release certificate under the Studio profile, so adding one silently would hand studios a
        /// row that passes on a tag instead of on evidence. Adding one must be deliberate: update this test,
        /// and re-read `certification-run.md`, which is the pass that would have to certify it.</summary>
        [Test]
        public void InvariantGates_AreCurrentlyEmpty_AddingOneIsADeliberateChange()
        {
            CollectionAssert.IsEmpty(
                GateCatalog.Canonical.All
                    .Where(d => d.Classification == GateClassification.Invariant)
                    .Select(d => d.Id).ToList());
        }

        [Test]
        public void EveryGate_IsClassified()
        {
            foreach (GateDefinition def in GateCatalog.Canonical.All)
                Assert.AreNotEqual(GateClassification.Unknown, def.Classification, $"Gate '{def.Id}' is unclassified.");
        }

        [Test]
        public void InvariantGates_AreNeverStaticProofOnly()
        {
            // A Static-only invariant would be a repo-shape rule mislabelled - the conflation this split removed.
            foreach (GateDefinition def in GateCatalog.Canonical.All
                         .Where(d => d.Classification == GateClassification.Invariant))
                Assert.AreNotEqual(ProofScope.None, def.RequiredProof & ~ProofScope.Static,
                    $"Invariant gate '{def.Id}' requires only Static proof.");
        }

        [Test]
        public void DeletedGates_AreGone()
        {
            // No shims: a deleted id must not resolve, so a stale producer fails loud. The first four went
            // 2026-07-20 (circular evidence); the six human-attested ones went 2026-07-22 with the
            // attestation mechanism itself.
            foreach (string id in new[]
            {
                "device.ready", "iap.tracking_attached", "build.adjust_resolved_version", "build.prototype_mode_intent",
                "iap.store_configured", "manual.ga_platform_registered", "manual.cross_vendor_dashboard_drift",
                "manual.adjust_purchase_verification", "manual.relaunch_persistence", "manual.background_resume_cycle",
            })
                Assert.IsNull(GateCatalog.Canonical.ById(id, throwIfMissing: false), id);
        }

        [Test]
        public void Canonical_Has24Gates()
        {
            // 30 before the 2026-07-22 deletion of the six human-attested gates. (The old assertion here
            // said 31 and was RED against a 30-gate catalog - a stale count nobody had reconciled.)
            Assert.AreEqual(24, GateCatalog.Canonical.All.Count);
        }
    }
}
