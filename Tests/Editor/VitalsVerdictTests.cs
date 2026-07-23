using System.Collections.Generic;
using NUnit.Framework;
using Sorolla.Palette.Health;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     The runtime Vitals verdict: one computation shared by the overlay's Report pane and the QA-bridge
    ///     snapshot. Pins the fail-closed rules - a FAIL outranks everything, and an all-green report over a
    ///     session that exercised nothing is NOT PROVEN, never green - plus the ownership routing that decides
    ///     which section a row lands in (and that an unknown group can never fall off the report).
    /// </summary>
    [TestFixture]
    public class VitalsVerdictTests
    {
        static SorollaDiagnosticRow Row(string group, string name, SorollaDiagnosticSeverity severity) =>
            new SorollaDiagnosticRow(group, name, severity, "detail", SorollaDiagnosticKind.Required);

        static List<SorollaDiagnosticRow> Rows(params SorollaDiagnosticRow[] rows) =>
            new List<SorollaDiagnosticRow>(rows);

        [Test]
        public void Fail_OutranksEverything()
        {
            SorollaVitalsVerdictReport report = SorollaDiagnostics.ComputeVerdict(Rows(
                Row("Config", "a", SorollaDiagnosticSeverity.Fail),
                Row("Config", "b", SorollaDiagnosticSeverity.Pass)));

            Assert.AreEqual(SorollaVitalsVerdict.Failing, report.Verdict);
            Assert.AreEqual(1, report.Fail);
        }

        [Test]
        public void WarnOrWait_IsActionNeeded()
        {
            Assert.AreEqual(SorollaVitalsVerdict.ActionNeeded, SorollaDiagnostics.ComputeVerdict(Rows(
                Row("Config", "a", SorollaDiagnosticSeverity.Warning))).Verdict);
            Assert.AreEqual(SorollaVitalsVerdict.ActionNeeded, SorollaDiagnostics.ComputeVerdict(Rows(
                Row("Config", "a", SorollaDiagnosticSeverity.Waiting))).Verdict);
        }

        [Test]
        public void AllPass_IsGreenOnlyWhenCoverageIsNotThin()
        {
            // The "green only after played" rule, expressed against whatever the ambient session coverage is:
            // all rows passing is necessary but NOT sufficient - thin coverage must resolve to NOT PROVEN.
            SorollaVitalsVerdictReport report = SorollaDiagnostics.ComputeVerdict(Rows(
                Row("Config", "a", SorollaDiagnosticSeverity.Pass),
                Row("Ads", "b", SorollaDiagnosticSeverity.Pass)));

            Assert.AreEqual(0, report.NeedsAttention);
            Assert.AreEqual(
                report.CoverageThin ? SorollaVitalsVerdict.NotProven : SorollaVitalsVerdict.Pass,
                report.Verdict);
        }

        [Test]
        public void EmptyLedger_IsAlwaysThin_EvenWithEventsFlowing()
        {
            // The defect this pins (hungrysnake iOS 2026-07-20): coverage-thin used to be "0 events",
            // and a boot sequence alone fires enough events to defeat it. Coverage now comes from the
            // per-build ledger, so a build that proved nothing is thin no matter what the counters say.
            SorollaCoverageLedger.Clear();
            try
            {
                SorollaVitalsVerdictReport report = SorollaDiagnostics.ComputeVerdict(Rows(
                    Row("Config", "a", SorollaDiagnosticSeverity.Pass)));

                Assert.IsTrue(report.CoverageThin, "an empty per-build ledger is thin coverage");
                Assert.AreEqual(SorollaVitalsVerdict.NotProven, report.Verdict);
            }
            finally
            {
                SorollaCoverageLedger.Clear();
            }
        }

        [Test]
        public void NotProven_IsNeverTheGreenWord()
        {
            var notProven = new SorollaVitalsVerdictReport(SorollaVitalsVerdict.NotProven, 0, 0, 0, 4, true);
            Assert.AreEqual("NOT PROVEN", SorollaDiagnostics.VerdictWord(notProven));
            Assert.AreNotEqual("HEALTHY", SorollaDiagnostics.VerdictWord(notProven));
            Assert.AreEqual("not_proven", SorollaDiagnostics.VerdictToken(SorollaVitalsVerdict.NotProven));
        }

        [Test]
        public void VerdictTokens_AreStableAndDistinct()
        {
            var tokens = new HashSet<string>();
            foreach (SorollaVitalsVerdict verdict in new[]
            {
                SorollaVitalsVerdict.Failing, SorollaVitalsVerdict.ActionNeeded,
                SorollaVitalsVerdict.NotProven, SorollaVitalsVerdict.Pass,
            })
                Assert.IsTrue(tokens.Add(SorollaDiagnostics.VerdictToken(verdict)), verdict.ToString());
            Assert.AreEqual(4, tokens.Count);
        }

        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, false, true)]
        [TestCase(true, true, true)]
        public void AdsCapability_IsOwnedByModeAndCompiledPackage(
            bool fullMode, bool maxCompiled, bool required)
        {
            CapabilityState ads = CapabilityPolicy.Resolve(
                fullMode ? EvalMode.Full : EvalMode.Prototype,
                maxCompiled ? SdkModule.AppLovinMax : SdkModule.None,
                SdkModule.AppLovinMax);

            Assert.AreEqual(required, ads.Required);
            Assert.AreEqual(maxCompiled, ads.Included);
        }

        [Test]
        public void FullOnlyCapability_IsExcludedFromPrototypeEvenWhenCompiled()
        {
            CapabilityState adjust = CapabilityPolicy.Resolve(
                EvalMode.Prototype, SdkModule.Adjust, SdkModule.Adjust);

            Assert.IsTrue(adjust.Included);
            Assert.IsFalse(adjust.Required);
            Assert.IsFalse(adjust.Applicable);
        }

        // ── TEST YOUR GAME rows: the one list behind both the matrix and NOT PROVEN ──

        static readonly CapabilityState Excluded = new CapabilityState(false, false, false);
        static readonly CapabilityState Included = new CapabilityState(false, true, true);

        static List<string> RowNames(in SorollaCoverageInputs inputs)
        {
            var names = new List<string>();
            foreach (SorollaMenuMatrixRow row in SorollaDiagnostics.BuildCoverageRows(inputs))
                names.Add(row.Name);
            return names;
        }

        [Test]
        public void PrototypeWithNoOptionals_OwesProgressionAndNothingElse()
        {
            var inputs = new SorollaCoverageInputs
            {
                Ads = Excluded, Adjust = Excluded, Iap = Excluded,
                // Stale ad unit ids in a config the game no longer ships MAX for prove nothing.
                InterstitialConfigured = true, RewardedConfigured = true,
            };

            Assert.AreEqual(new[] { "Progression" }, RowNames(inputs).ToArray());
            Assert.IsTrue(SorollaDiagnostics.IsCoverageThin(inputs), "progression is not proved yet");

            inputs.Proved = SorollaCoverageFact.Progression;
            Assert.IsFalse(SorollaDiagnostics.IsCoverageThin(inputs));
        }

        [Test]
        public void EveryVisibleToDoRow_MakesCoverageThin()
        {
            // The row list IS the verdict input: no row may be visible-but-not-voting, and none may vote
            // while invisible. Proving them one at a time must walk coverage to satisfied exactly when the
            // last visible row flips.
            var inputs = new SorollaCoverageInputs
            {
                Ads = Included, Adjust = Included, Iap = Included,
                InterstitialConfigured = true, RewardedConfigured = true,
                State = new SorollaQaState { IapTrackingAttached = true },
                Proved = SorollaCoverageFact.None,
            };

            List<string> visible = RowNames(inputs);
            CollectionAssert.AreEqual(new[]
            {
                "Progression", "Consent", "Ads · interstitial", "Ads · rewarded",
                "IAP wiring", "IAP purchase", "Adjust purchase verification",
            }, visible);

            foreach (SorollaCoverageFact fact in new[]
            {
                SorollaCoverageFact.Progression, SorollaCoverageFact.Consent,
                SorollaCoverageFact.Interstitial, SorollaCoverageFact.Rewarded,
                SorollaCoverageFact.IapPurchase,
            })
            {
                Assert.IsTrue(SorollaDiagnostics.IsCoverageThin(inputs), $"still owes {fact}");
                inputs.Proved |= fact;
            }

            Assert.IsTrue(SorollaDiagnostics.IsCoverageThin(inputs), "still owes the Adjust verification row");
            inputs.Proved |= SorollaCoverageFact.AdjustPurchaseVerification;
            Assert.IsFalse(SorollaDiagnostics.IsCoverageThin(inputs), "every visible row is proved");
        }

        [Test]
        public void AdRows_AppearPerConfiguredFormat_AndEachMustBeProved()
        {
            var inputs = new SorollaCoverageInputs
            {
                Ads = Included, Adjust = Excluded, Iap = Excluded,
                InterstitialConfigured = false, RewardedConfigured = true,
                Proved = SorollaCoverageFact.Progression | SorollaCoverageFact.Consent,
            };

            CollectionAssert.DoesNotContain(RowNames(inputs), "Ads · interstitial");
            CollectionAssert.Contains(RowNames(inputs), "Ads · rewarded");
            Assert.IsTrue(SorollaDiagnostics.IsCoverageThin(inputs));

            inputs.Proved |= SorollaCoverageFact.Rewarded;
            Assert.IsFalse(SorollaDiagnostics.IsCoverageThin(inputs));

            // Both formats configured: one proved format never stands in for the other.
            inputs.InterstitialConfigured = true;
            Assert.IsTrue(SorollaDiagnostics.IsCoverageThin(inputs));
            inputs.Proved |= SorollaCoverageFact.Interstitial;
            Assert.IsFalse(SorollaDiagnostics.IsCoverageThin(inputs));
        }

        [Test]
        public void AdjustVerificationRow_NeedsBothAdjustAndIap_AndCompletesOnTheCallback()
        {
            var inputs = new SorollaCoverageInputs
            {
                Ads = Excluded, Adjust = Included, Iap = Excluded,
                Proved = SorollaCoverageFact.Progression,
            };
            CollectionAssert.DoesNotContain(RowNames(inputs), "Adjust purchase verification");

            inputs.Adjust = Excluded;
            inputs.Iap = Included;
            inputs.State = new SorollaQaState { IapTrackingAttached = true };
            CollectionAssert.DoesNotContain(RowNames(inputs), "Adjust purchase verification");

            inputs.Adjust = Included;
            CollectionAssert.Contains(RowNames(inputs), "Adjust purchase verification");

            inputs.Proved |= SorollaCoverageFact.IapPurchase;
            Assert.IsTrue(SorollaDiagnostics.IsCoverageThin(inputs), "no verification answer observed yet");
            inputs.Proved |= SorollaCoverageFact.AdjustPurchaseVerification;
            Assert.IsFalse(SorollaDiagnostics.IsCoverageThin(inputs));
        }

        [Test]
        public void ExcludedCapabilities_DoNotCreateCoverageDebt()
        {
            var inputs = new SorollaCoverageInputs
            {
                State = new SorollaQaState { ConsentStatus = "Unknown", IapTrackingAttached = false },
                Ads = Excluded, Adjust = Excluded, Iap = Excluded,
                Proved = SorollaCoverageFact.Progression,
            };

            Assert.IsFalse(SorollaDiagnostics.IsCoverageThin(inputs));
        }

        [Test]
        public void IncludedIap_RequiresWiringAndPurchase()
        {
            var inputs = new SorollaCoverageInputs
            {
                State = new SorollaQaState { IapTrackingAttached = false },
                Ads = Excluded, Adjust = Excluded, Iap = Included,
                Proved = SorollaCoverageFact.Progression,
            };

            Assert.IsTrue(SorollaDiagnostics.IsCoverageThin(inputs), "wiring not attached, no purchase");

            inputs.State.IapTrackingAttached = true;
            Assert.IsTrue(SorollaDiagnostics.IsCoverageThin(inputs), "wired but no purchase");

            inputs.Proved |= SorollaCoverageFact.IapPurchase;
            Assert.IsFalse(SorollaDiagnostics.IsCoverageThin(inputs));
        }

        [Test]
        public void NotTestedRows_AreToDo_NeverErrors()
        {
            // A capability nobody exercised is a TO DO row, never a red row: nothing about the row list
            // reaches the issue sections, which key on diagnostic-row severity.
            var inputs = new SorollaCoverageInputs
            {
                Ads = Included, Adjust = Included, Iap = Included,
                InterstitialConfigured = true, RewardedConfigured = true,
                State = new SorollaQaState(),
                Proved = SorollaCoverageFact.None,
            };

            foreach (SorollaMenuMatrixRow row in SorollaDiagnostics.BuildCoverageRows(inputs))
            {
                Assert.IsFalse(row.Exercised, row.Name);
                Assert.IsNotEmpty(row.Hint, $"{row.Name} must say how to complete it");
            }
        }

        [Test]
        public void EveryAttentionRow_CarriesASolution()
        {
            // The other half of the four-state rule: a Palette mechanism that was attempted and failed
            // surfaces as an issue row WITH a fix, never as a bare red word.
            var rows = new List<SorollaDiagnosticRow>();
            SorollaDiagnostics.BuildRows(rows);

            foreach (SorollaDiagnosticRow row in rows)
            {
                if (!SorollaDiagnostics.NeedsAttention(row.Severity) || row.Group == "Red flags") continue;
                Assert.IsTrue(row.HasStructuredDiagnosis, $"{row.Group}/{row.Name} has no why/signal/fix");
            }
        }

        [Test]
        public void StudioOwnedGroups_RouteToTheStudio()
        {
            foreach (string group in new[] { "Config", "SDKs", "Firebase", "Consent", "Identity", "Activity", "Ads" })
                Assert.AreEqual(SorollaRowOwner.Studio,
                    SorollaDiagnostics.OwnerOf(Row(group, "x", SorollaDiagnosticSeverity.Fail)), group);
        }

        [Test]
        public void BootIsSorollas_ExceptTheModeRow()
        {
            Assert.AreEqual(SorollaRowOwner.Sorolla,
                SorollaDiagnostics.OwnerOf(Row("Boot", "Palette ready", SorollaDiagnosticSeverity.Fail)));
            Assert.AreEqual(SorollaRowOwner.Studio,
                SorollaDiagnostics.OwnerOf(Row("Boot", "Palette mode", SorollaDiagnosticSeverity.Fail)),
                "the mode row is the studio's SorollaConfig, not SDK bring-up");
            Assert.AreEqual(SorollaRowOwner.Studio,
                SorollaDiagnostics.OwnerOf(Row("Boot", "Network reachability", SorollaDiagnosticSeverity.Fail)),
                "the studio owns its test device network");
        }

        [Test]
        public void UnknownGroup_FailsClosedToSorolla()
        {
            // A row added later must land in "send to Sorolla", never silently vanish from the report.
            Assert.AreEqual(SorollaRowOwner.Sorolla,
                SorollaDiagnostics.OwnerOf(Row("Something New", "x", SorollaDiagnosticSeverity.Fail)));
            Assert.AreEqual(SorollaRowOwner.Sorolla,
                SorollaDiagnostics.OwnerOf(Row("Red flags", "SDK errors", SorollaDiagnosticSeverity.Fail)));
        }

        [Test]
        public void StudioRuntimeProblems_AreNotVitalsRows()
        {
            var rows = new List<SorollaDiagnosticRow>();

            SorollaDiagnostics.BuildRows(rows);

            Assert.IsFalse(rows.Exists(row => row.Name == "Runtime problems"),
                "Vitals grades SDK state only; arbitrary Unity and studio-game exceptions belong in the hidden console.");
        }
    }
}
