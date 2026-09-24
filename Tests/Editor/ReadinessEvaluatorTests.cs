using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sorolla.Palette.Editor.Greenlight;
using Sorolla.Palette.Health;

namespace Sorolla.Palette.Editor.Tests
{
    public class ReadinessEvaluatorTests
    {
        static ReadinessContext Context(
            EvalMode mode = EvalMode.Full,
            ReadinessPlatform platform = ReadinessPlatform.Android,
            SdkModule modules = SdkModule.GameAnalytics | SdkModule.Facebook | SdkModule.Firebase |
                                SdkModule.AppLovinMax | SdkModule.Adjust) =>
            new ReadinessContext
            {
                Mode = mode,
                Platform = platform,
                InstalledModules = modules,
                ModulesResolved = true,
            };

        static BuildValidator.ValidationResult Result(
            ReadinessCheck check,
            BuildValidator.ValidationStatus status = BuildValidator.ValidationStatus.Valid,
            string message = "evidence",
            string fix = "fix") =>
            new BuildValidator.ValidationResult(status, message, fix, check);

        [Test]
        public void Catalog_HasTwentyFourUniqueStableIds()
        {
            Assert.AreEqual(24, ReadinessChecks.All.Count);
            Assert.AreEqual(24, ReadinessChecks.All.Select(check => check.Id).Distinct().Count());
        }

        /// <summary>
        ///     A required check that produced nothing must SAY so and hand the studio a bounded action -
        ///     an empty pending row with no text was the shape that read as "still working on it" forever.
        /// </summary>
        [Test]
        public void RequiredMissingEvidence_IsIncomplete_AndStatesTheResidueWithAnAction()
        {
            ReadinessReport report = ReadinessEvaluator.Evaluate(Context(), new List<BuildValidator.ValidationResult>());
            ReadinessRow row = report.Rows.Single(r => r.Check == ReadinessChecks.RequiredSdks);

            Assert.AreEqual(ReadinessDisposition.Omitted, row.Disposition);
            Assert.AreEqual(ReadinessOutcome.Incomplete, report.Outcome);
            Assert.AreEqual(1, row.Findings.Count);
            StringAssert.Contains("No result was produced", row.Findings[0].Evidence);
            StringAssert.Contains("Copy Report", row.Findings[0].Fix);
        }

        /// <summary>
        ///     A producer skip is an ABSENCE of verdict. Where the catalog says the fact is required, it
        ///     used to arrive as an affirmative Pass and count toward green.
        /// </summary>
        [Test]
        public void SkippedOnARequiredRow_IsIncomplete_NotAPass()
        {
            ReadinessReport report = ReadinessEvaluator.Evaluate(Context(), new List<BuildValidator.ValidationResult>
            {
                Result(ReadinessChecks.RequiredSdks, BuildValidator.ValidationStatus.Skipped,
                    "Select Android or iOS to check this", fix: null),
            });
            ReadinessRow row = report.Rows.Single(r => r.Check == ReadinessChecks.RequiredSdks);

            Assert.AreEqual(ReadinessRequirement.Required, row.Requirement);
            Assert.AreEqual(ReadinessOutcome.Incomplete, row.Outcome);
            Assert.IsFalse(row.Informational);
            Assert.AreEqual(0, report.PassCount, "a skipped required check must not count toward green");
            Assert.AreEqual(ReadinessOutcome.Incomplete, report.Outcome);
            // The skip message IS the studio's instruction, so it becomes the action. A state the studio
            // caused must never be answered with "send a report to Sorolla".
            Assert.AreEqual("Select Android or iOS to check this", row.Findings[0].Fix);
            StringAssert.DoesNotContain("Sorolla", row.Findings[0].Fix);
        }

        /// <summary>
        ///     A row nothing reported on never voted, so neither renderer may print it as a pass - it
        ///     carries the model's default Pass outcome only because there is nothing to derive from.
        /// </summary>
        [Test]
        public void RowsThatNeverVoted_AreLabelledByDisposition_NotAsPass()
        {
            ReadinessReport report = ReadinessEvaluator.Evaluate(
                Context(platform: ReadinessPlatform.Android),
                new List<BuildValidator.ValidationResult> { Result(ReadinessChecks.RequiredSdks) });

            ReadinessRow optionalSkipped =
                report.Rows.Single(r => r.Check == ReadinessChecks.GameAnalyticsResourceWhitelist);
            Assert.AreEqual(ReadinessDisposition.OptionalSkipped, optionalSkipped.Disposition);

            string text = GreenlightReportExport.ToText(report);
            StringAssert.Contains("[NotApplicable] build.firebase_config_ios", text);
            StringAssert.Contains("[OptionalSkipped] build.gameanalytics_resource_whitelist", text);
        }

        /// <summary>
        ///     The same skip on a row nothing required stays a neutral notice, and still counts as one of
        ///     the passing evaluated rows in the header counts.
        /// </summary>
        [Test]
        public void SkippedOnAnOptionalRow_StaysInformational_AndCountsAsPass()
        {
            ReadinessReport report = ReadinessEvaluator.Evaluate(Context(), new List<BuildValidator.ValidationResult>
            {
                Result(ReadinessChecks.VersionMismatches, BuildValidator.ValidationStatus.Skipped),
            });
            ReadinessRow row = report.Rows.Single(r => r.Check == ReadinessChecks.VersionMismatches);

            Assert.AreEqual(ReadinessOutcome.Pass, row.Outcome);
            Assert.IsTrue(row.Informational);
            Assert.AreEqual(1, report.PassCount);
        }

        /// <summary>
        ///     An observation against a gate that does not apply here is DISCARDED, and discarding it must
        ///     also mean it cannot fail the report - the reason grading reads this model and not the raw
        ///     producer results.
        /// </summary>
        [Test]
        public void ObservationOnANotApplicableRow_IsDiscarded_AndCannotFail()
        {
            ReadinessReport report = ReadinessEvaluator.Evaluate(
                Context(platform: ReadinessPlatform.Android),
                new List<BuildValidator.ValidationResult>
                {
                    Result(ReadinessChecks.RequiredSdks),
                    Result(ReadinessChecks.FirebaseConfigIos, BuildValidator.ValidationStatus.Error),
                });
            ReadinessRow row = report.Rows.Single(r => r.Check == ReadinessChecks.FirebaseConfigIos);

            Assert.AreEqual(ReadinessDisposition.NotApplicable, row.Disposition);
            Assert.IsEmpty(row.Findings);
            Assert.IsEmpty(report.FailingRows);
            Assert.AreEqual(0, report.FailCount);
        }

        /// <summary>
        ///     One check that threw names itself, grades Incomplete because evaluation failed rather than
        ///     the integration being proven broken, and never grades as a failure.
        /// </summary>
        [Test]
        public void ThrownCheckReportedUnverifiable_IsIncompleteAndNonFailing()
        {
            ReadinessReport report = ReadinessEvaluator.Evaluate(Context(), new List<BuildValidator.ValidationResult>
            {
                Result(ReadinessChecks.RequiredSdks, BuildValidator.ValidationStatus.Unverifiable,
                    "The Required SDKs check failed to run: boom"),
            });
            ReadinessRow row = report.Rows.Single(r => r.Check == ReadinessChecks.RequiredSdks);

            Assert.AreEqual(ReadinessOutcome.Incomplete, row.Outcome);
            Assert.IsEmpty(report.FailingRows);
            StringAssert.Contains("Required SDKs", row.Findings[0].Evidence);
        }

        [Test]
        public void FailureOutranksIncompleteAndWarning()
        {
            var results = new List<BuildValidator.ValidationResult>
            {
                Result(ReadinessChecks.RequiredSdks, BuildValidator.ValidationStatus.Error),
                Result(ReadinessChecks.VersionMismatches, BuildValidator.ValidationStatus.Warning),
            };

            Assert.AreEqual(ReadinessOutcome.Fail, ReadinessEvaluator.Evaluate(Context(), results).Outcome);
        }

        /// <summary>
        ///     The row takes the worst finding, but every finding SURVIVES with the fix it was paired with.
        ///     The old shape kept one result and dropped the rest, so a check that observed three problems
        ///     handed the studio one line and one action.
        /// </summary>
        [Test]
        public void SeveralFindingsForOneCheck_AllSurviveWithTheirOwnFix()
        {
            var results = new List<BuildValidator.ValidationResult>
            {
                Result(ReadinessChecks.RequiredSdks),
                Result(ReadinessChecks.MaxSettings, BuildValidator.ValidationStatus.Error,
                    "AdMob app id is empty", "paste the AdMob id"),
                Result(ReadinessChecks.MaxSettings, BuildValidator.ValidationStatus.Warning,
                    "no purchase event token", "add the purchase token"),
            };

            ReadinessReport report = ReadinessEvaluator.Evaluate(Context(), results);
            ReadinessRow row = report.Rows.Single(r => r.Check == ReadinessChecks.MaxSettings);

            Assert.AreEqual(ReadinessOutcome.Fail, row.Outcome);
            Assert.AreEqual(2, row.Findings.Count);
            Assert.AreEqual("AdMob app id is empty", row.Findings[0].Evidence);
            Assert.AreEqual("paste the AdMob id", row.Findings[0].Fix);
            Assert.AreEqual("no purchase event token", row.Findings[1].Evidence);
            Assert.AreEqual("add the purchase token", row.Findings[1].Fix);

            // ...and both reach the copied report, which renders the same collection.
            string text = GreenlightReportExport.ToText(report);
            StringAssert.Contains("paste the AdMob id", text);
            StringAssert.Contains("add the purchase token", text);
        }

        /// <summary>
        ///     A multi-line diagnosis is evidence, not decoration: the lines after the first used to be
        ///     dropped on the way into the row.
        /// </summary>
        [Test]
        public void MultiLineEvidence_IsNotTruncatedToItsFirstLine()
        {
            ReadinessReport report = ReadinessEvaluator.Evaluate(Context(), new List<BuildValidator.ValidationResult>
            {
                Result(ReadinessChecks.RequiredSdks, BuildValidator.ValidationStatus.Error,
                    "headline\n  second line with the actual detail"),
            });
            ReadinessRow row = report.Rows.Single(r => r.Check == ReadinessChecks.RequiredSdks);

            StringAssert.Contains("second line with the actual detail", row.Findings[0].Evidence);
            StringAssert.Contains("second line with the actual detail", GreenlightReportExport.ToText(report));
        }

        [Test]
        public void OptionalAbsentCapability_IsNotApplicable()
        {
            ReadinessReport report = ReadinessEvaluator.Evaluate(
                Context(EvalMode.Prototype, modules: SdkModule.GameAnalytics | SdkModule.Facebook),
                new List<BuildValidator.ValidationResult> { Result(ReadinessChecks.RequiredSdks) });

            Assert.AreEqual(
                ReadinessDisposition.NotApplicable,
                report.Rows.Single(r => r.Check == ReadinessChecks.AdjustSettings).Disposition);
        }

        [Test]
        public void GameAnalyticsResourceWhitelist_RemainsAdvisoryInPrototype()
        {
            ReadinessReport report = ReadinessEvaluator.Evaluate(
                Context(EvalMode.Prototype, modules: SdkModule.GameAnalytics | SdkModule.Facebook),
                new List<BuildValidator.ValidationResult>
                {
                    Result(ReadinessChecks.RequiredSdks),
                    Result(ReadinessChecks.GameAnalyticsResourceWhitelist, BuildValidator.ValidationStatus.Skipped),
                });

            ReadinessRow row = report.Rows.Single(r => r.Check == ReadinessChecks.GameAnalyticsResourceWhitelist);
            Assert.AreEqual(ReadinessRequirement.Optional, row.Requirement);
            Assert.AreEqual(ReadinessDisposition.Evaluated, row.Disposition);
            Assert.IsTrue(row.Informational);
        }

        /// <summary>
        ///     The requirement reason is printed verbatim in the studio-facing report, so it must describe
        ///     the mode the report was produced in. A Core capability is required in BOTH modes; the reason
        ///     was hardcoded to "required in Full mode" regardless, which told every Prototype report that a
        ///     Core row was a Full-mode obligation it could ignore.
        /// </summary>
        [Test]
        public void CoreCapability_InPrototype_StatesItIsRequiredInBothModes()
        {
            ReadinessReport report = ReadinessEvaluator.Evaluate(
                Context(EvalMode.Prototype, modules: SdkModule.GameAnalytics | SdkModule.Facebook),
                new List<BuildValidator.ValidationResult> { Result(ReadinessChecks.GameAnalyticsSettings) });

            ReadinessRow row = report.Rows.Single(r => r.Check == ReadinessChecks.GameAnalyticsSettings);

            Assert.AreEqual(ReadinessRequirement.Required, row.Requirement);
            Assert.AreEqual("required in both modes", row.RequirementReason);
        }

        /// <summary>Full mode keeps the Full-mode wording - the fix is mode-aware, not a blanket rename.</summary>
        [Test]
        public void CoreCapability_InFullMode_KeepsFullModeWording()
        {
            ReadinessReport report = ReadinessEvaluator.Evaluate(
                Context(),
                new List<BuildValidator.ValidationResult> { Result(ReadinessChecks.GameAnalyticsSettings) });

            ReadinessRow row = report.Rows.Single(r => r.Check == ReadinessChecks.GameAnalyticsSettings);

            Assert.AreEqual(ReadinessRequirement.Required, row.Requirement);
            Assert.AreEqual("included and required in Full mode", row.RequirementReason);
        }

        [Test]
        public void Export_PreservesSchemaAndStableIds()
        {
            ReadinessReport report = ReadinessEvaluator.Evaluate(
                Context(),
                new List<BuildValidator.ValidationResult> { Result(ReadinessChecks.RequiredSdks) });

            string text = GreenlightReportExport.ToText(report);

            StringAssert.Contains("sorolla.greenlight-report/1", text);
            StringAssert.Contains("build.required_sdks", text);
            StringAssert.Contains("[NotApplicable] build.firebase_config_ios", text);
        }
    }
}
