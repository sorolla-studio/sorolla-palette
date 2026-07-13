using System;
using System.Collections.Generic;
using NUnit.Framework;
using Sorolla.Palette.Editor.Greenlight;
using Sorolla.Palette.Health;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     Cycle 4b minimal scoped attestation: the pure validator truth table (identity binding, freshness,
    ///     schema/scope/timestamp rejection) plus the HEALTHY-control proof - that a build with every required
    ///     gate satisfied at the right proof scope actually aggregates to PASS through the real evaluator.
    ///     That reachability is the whole point of 4b: without it a legitimate green is impossible and Phase 3
    ///     agreement is unattainable.
    /// </summary>
    public class QaAttestationTests
    {
        static readonly QaBuildIdentity Identity = new QaBuildIdentity("com.sorolla.game", "Android", "full", "1.4.0");
        static readonly DateTime Now = new DateTime(2026, 07, 13, 12, 0, 0, DateTimeKind.Utc);

        static QaAttestationRecord Rec(
            string schema = "1", string appId = "com.sorolla.game", string platform = "Android",
            string mode = "full", string appVersion = "1.4.0", string scope = "VendorAccepted",
            DateTime? ts = null)
        {
            return new QaAttestationRecord
            {
                schema = schema, gateId = "g", gateVersion = "1", phase = "QaPass", actor = "tester",
                timestampUtc = (ts ?? Now.AddHours(-1)).ToString("o"),
                applicationId = appId, platform = platform, mode = mode, appVersion = appVersion,
                outcome = "Pass", proofScope = scope,
            };
        }

        static AttestationValidity Eval(QaAttestationRecord r, ProofScope required = ProofScope.VendorAccepted) =>
            QaAttestationValidator.Evaluate(r, Identity, required, Now, out _);

        // ── Validator truth table ─────────────────────────────────────────

        [Test] public void MatchingFreshAttestation_IsValid() => Assert.AreEqual(AttestationValidity.Valid, Eval(Rec()));

        [Test] public void NullRecord_IsMissing() => Assert.AreEqual(AttestationValidity.Missing, Eval(null));

        [Test] public void UnknownSchema_IsInvalid() => Assert.AreEqual(AttestationValidity.Invalid, Eval(Rec(schema: "99")));

        [Test] public void WrongProofScope_IsInvalid() =>
            Assert.AreEqual(AttestationValidity.Invalid, Eval(Rec(scope: "DeviceDispatch"), ProofScope.VendorAccepted));

        [Test] public void FutureTimestamp_IsInvalid() =>
            Assert.AreEqual(AttestationValidity.Invalid, Eval(Rec(ts: Now.AddDays(1))));

        [Test] public void MissingIdentity_IsInvalid() =>
            Assert.AreEqual(AttestationValidity.Invalid, Eval(Rec(appId: "")));

        [Test] public void WrongGame_IsStale() =>
            Assert.AreEqual(AttestationValidity.Stale, Eval(Rec(appId: "com.other.game")));

        [Test] public void WrongBuildVersion_IsStale() =>
            Assert.AreEqual(AttestationValidity.Stale, Eval(Rec(appVersion: "1.3.0")));

        [Test] public void WrongMode_IsStale() =>
            Assert.AreEqual(AttestationValidity.Stale, Eval(Rec(mode: "prototype")));

        [Test] public void Expired_IsStale() =>
            Assert.AreEqual(AttestationValidity.Stale, Eval(Rec(ts: Now.AddDays(-30))));

        [Test] public void DuplicateConflict_IsRejected()
        {
            var records = new List<QaAttestationRecord> { Rec(), Rec() };
            records[0].gateId = records[1].gateId = "dup";
            QaAttestationRecord picked = QaAttestationStore.ForGate(records, "dup");
            Assert.AreEqual(AttestationValidity.Invalid, Eval(picked), "a duplicate/conflicting attestation cannot grandfather a gate");
        }

        // ── HEALTHY control: a fully-satisfied build reaches PASS ─────────

        [Test]
        public void FullyAttestedBuild_ReachesHealthy()
        {
            // Full/Android, every vendor installed EXCEPT Unity IAP (so the IAP gate is NotApplicable). Emit a
            // PASS observation at the gate's required proof scope for every REQUIRED QaPass gate - exactly what
            // a passing Build Health run + a connected device + valid attestations for all five manual gates
            // would produce. The aggregate must be PASS (HEALTHY reachable).
            var ctx = new EvaluationContext
            {
                Mode = EvalMode.Full,
                Platform = EvalPlatform.Android,
                InstalledModules = SdkModule.GameAnalytics | SdkModule.Facebook | SdkModule.Firebase |
                                   SdkModule.AppLovinMax | SdkModule.Adjust, // no UnityIap
                RequestedPhase = GatePhase.QaPass,
                ModulesResolved = true,
            };

            var observations = new List<GateObservation>();
            foreach (GateDefinition def in GateCatalog.Canonical.All)
            {
                if ((def.Phases & GatePhase.QaPass) == 0) continue;
                if (def.Requirement(ctx).Value != Requirement.Required) continue;
                observations.Add(new GateObservation
                {
                    GateId = def.Id, Outcome = GateOutcome.Pass, ObservedProof = def.RequiredProof,
                });
            }

            HealthReport report = HealthEvaluator.Evaluate(GateCatalog.Canonical, ctx, observations);
            Assert.IsEmpty(report.ValidationErrors);
            Assert.AreEqual(GateOutcome.Pass, report.Outcome, "a fully-satisfied build must be able to reach HEALTHY");
        }
    }
}
