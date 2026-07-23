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
                Mode = mode, Platform = platform, InstalledModules = modules,

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
        public void EveryGate_CarriesAVersion()
        {
            foreach (GateDefinition def in GateCatalog.Canonical.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(def.Version), $"Gate '{def.Id}' has no version.");
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
            // C4-05: on its OWN platform, a Firebase config-file gate follows Firebase's own requirement,
            // not the advisory list - a missing config file blocks in Full mode and advises in Prototype.
            Assert.AreEqual(Requirement.Required,
                ReqOf(GateIds.BuildFirebaseConfigAndroid, Ctx(EvalMode.Full, EvalPlatform.Android)));
            Assert.AreEqual(Requirement.Optional,
                ReqOf(GateIds.BuildFirebaseConfigAndroid, Ctx(EvalMode.Prototype, EvalPlatform.Android)));
            Assert.AreEqual(Requirement.Required,
                ReqOf(GateIds.BuildFirebaseConfigIos, Ctx(EvalMode.Full, EvalPlatform.iOS)));
            Assert.AreEqual(Requirement.Optional,
                ReqOf(GateIds.BuildFirebaseConfigIos, Ctx(EvalMode.Prototype, EvalPlatform.iOS)));
        }

        /// <summary>Platform scoping (2026-07-23): a report judges ONE platform, the active build target, so
        /// the config gate for the other platform is NotApplicable - out of the verdict and the counts - in
        /// BOTH modes. This is what lets a game shipping one platform read green; before it, the other
        /// platform's missing file was a warning that could never be cleared. Off-mobile neither applies.</summary>
        [Test]
        public void FirebaseConfig_AppliesOnlyToTheActiveBuildTarget()
        {
            foreach (EvalMode mode in new[] { EvalMode.Full, EvalMode.Prototype })
            {
                Assert.AreEqual(Requirement.NotApplicable,
                    ReqOf(GateIds.BuildFirebaseConfigIos, Ctx(mode, EvalPlatform.Android)), mode.ToString());
                Assert.AreEqual(Requirement.NotApplicable,
                    ReqOf(GateIds.BuildFirebaseConfigAndroid, Ctx(mode, EvalPlatform.iOS)), mode.ToString());
                Assert.AreEqual(Requirement.NotApplicable,
                    ReqOf(GateIds.BuildFirebaseConfigAndroid, Ctx(mode, EvalPlatform.Unknown)), mode.ToString());
                Assert.AreEqual(Requirement.NotApplicable,
                    ReqOf(GateIds.BuildFirebaseConfigIos, Ctx(mode, EvalPlatform.Unknown)), mode.ToString());
            }
        }

        /// <summary>A NotApplicable decision must say WHY: it is the row a reader of the copied report sees
        /// instead of a verdict, and the catalog's own validator rejects a reasonless decision. This pins the
        /// reason for the platform-scoped gates specifically, since theirs is the one a studio asks about
        /// ("where did my Android checks go?").</summary>
        [Test]
        public void PlatformScopedGates_ExplainWhyTheyDoNotApply()
        {
            foreach (string id in new[] { GateIds.BuildFirebaseConfigAndroid, GateIds.BuildFirebaseConfigIos })
            foreach (EvalPlatform platform in new[] { EvalPlatform.Android, EvalPlatform.iOS, EvalPlatform.Unknown })
            {
                RequirementDecision decision =
                    GateCatalog.Canonical.ById(id).Requirement(Ctx(EvalMode.Full, platform));
                Assert.IsFalse(string.IsNullOrWhiteSpace(decision.Reason), $"{id} on {platform}");
            }
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
                ReqOf(GateIds.DeviceVitals, Ctx(EvalMode.Full, EvalPlatform.Android)));
            Assert.AreEqual(Requirement.Required,
                ReqOf(GateIds.DeviceVitals, Ctx(EvalMode.Full, EvalPlatform.iOS)));
            Assert.AreEqual(Requirement.NotApplicable,
                ReqOf(GateIds.DeviceVitals, Ctx(EvalMode.Full, EvalPlatform.Unknown)));
        }

        [Test]
        public void DeviceGates_RequireDeviceDispatchProof()
        {
            Assert.AreEqual(ProofScope.DeviceDispatch,
                GateCatalog.Canonical.ById(GateIds.DeviceVitals).RequiredProof);
        }

        /// <summary>ReleaseOnly means ONE thing: the build preprocessor stays quiet about this check on a
        /// development build, because the check asks a question only a store submission answers. It must never
        /// come to mean "hidden from studios" again - that is what the deleted phase axis did, and it is why a
        /// studio could ship in Adjust sandbox without a single warning. Only the keystore qualifies: sandbox
        /// mode is switched on once, deliberately, to verify events reach Adjust, so a sandbox-on project is
        /// worth a console warning on every build.</summary>
        [Test]
        public void OnlyTheKeystoreIsReleaseOnly()
        {
            Assert.IsTrue(GateCatalog.Canonical.ById(GateIds.BuildAndroidKeystore).ReleaseOnly,
                "a release keystore is normally unset mid-development; don't nag on dev builds");

            foreach (GateDefinition def in GateCatalog.Canonical.All.Where(d => d.Id != GateIds.BuildAndroidKeystore))
                Assert.IsFalse(def.ReleaseOnly, $"{def.Id} must not be release-only.");
        }

        /// <summary>Every gate reaches every report: no gate is selected away by who is asking (2026-07-22).
        /// This is the regression guard for the phase axis - a studio's window must contain the store-submission
        /// checks (sandbox mode, keystore) alongside the core ones, because studios submit their own games.</summary>
        [Test]
        public void StudioReport_ContainsReleaseSubmissionGates()
        {
            var studioCtx = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.Android,
                InstalledModules = HealthEnums.AllModuleBits,

            };
            HealthReport studioReport = HealthEvaluator.Evaluate(
                GateCatalog.Canonical, studioCtx, new System.Collections.Generic.List<GateObservation>());

            foreach (string id in new[]
            {
                GateIds.BuildAdjustSandboxMode, GateIds.BuildAndroidKeystore, GateIds.BuildRequiredSdks,
            })
                Assert.IsTrue(studioReport.Rows.Any(r => r.GateId == id), $"{id} must reach a studio report");
        }

        /// <summary>`build.sdk_pin` must stay studio-visible: a studio pinned to a branch is on an SDK line
        /// Sorolla never certified, and must see that with the fix. This is the direct replacement for the
        /// deleted indirect mechanism (uncertified pin → invariant rows INCOMPLETE).</summary>
        [Test]
        public void SdkPinGate_IsVisibleToStudios_NotReleaseOnly()
        {
            Assert.IsFalse(GateCatalog.Canonical.ById(GateIds.BuildSdkPin).ReleaseOnly);

            var studioCtx = new EvaluationContext
            {
                Mode = EvalMode.Full, Platform = EvalPlatform.Android,
                InstalledModules = HealthEnums.AllModuleBits,

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

        /// <summary>Gate versions are the comparison instrument's restart signal: bumping one restarts exactly
        /// that gate's agreement count. The GA keys gate was bumped when it stopped being an
        /// active-platform-only check, so pin it - a silent meaning change with a stale version corrupts the
        /// comparison. The Firebase config gates instead got NEW ids when they split per platform, which
        /// restarts their counts by construction, so both start at version 1.</summary>
        [Test]
        public void PlatformCoverageChanges_CarryTheRightRestartSignal()
        {
            // Platform scoping (2026-07-23) changed what a green row MEANS on all five: each judges the
            // active build target only, so each restarts its own agreement count. Adjust is deliberately
            // absent - its token was never per-platform, so its meaning did not change.
            Assert.AreEqual("3", GateCatalog.Canonical.ById(GateIds.BuildGameAnalyticsKeys).Version);
            Assert.AreEqual("2", GateCatalog.Canonical.ById(GateIds.BuildFacebookPlatform).Version);
            Assert.AreEqual("2", GateCatalog.Canonical.ById(GateIds.BuildMaxSettings).Version);
            Assert.AreEqual("2", GateCatalog.Canonical.ById(GateIds.BuildFirebaseConfigAndroid).Version);
            Assert.AreEqual("2", GateCatalog.Canonical.ById(GateIds.BuildFirebaseConfigIos).Version);
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

        [Test]
        public void EveryGate_IsClassified()
        {
            foreach (GateDefinition def in GateCatalog.Canonical.All)
                Assert.AreNotEqual(GateClassification.Unknown, def.Classification, $"Gate '{def.Id}' is unclassified.");
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
                // Split into build.firebase_config_android + build.firebase_config_ios (2026-07-22).
                "build.firebase_config",
            })
                Assert.IsNull(GateCatalog.Canonical.ById(id, throwIfMissing: false), id);
        }

        [Test]
        public void Canonical_Has25Gates()
        {
            // 30 before the 2026-07-22 deletion of the six human-attested gates; 24 after, then 25 once the
            // Firebase config gate split into one per platform so both files get their own row. Platform
            // scoping (2026-07-23) added none: a gate that only ever resolves NotApplicable off its own
            // platform earns its id from being judged somewhere, not from appearing in the audit trail.
            Assert.AreEqual(25, GateCatalog.Canonical.All.Count);
        }
    }
}
