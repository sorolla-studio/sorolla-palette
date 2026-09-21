using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sorolla.Palette.Health;
using Sorolla.Palette.Editor.UI;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     Regressions for the 4.0.1 trust patch: each of these configurations used to read as a
    ///     non-failure while the integration was provably broken - plus the inverse defect, a row that
    ///     produced no verdict at all on a project that was fine.
    /// </summary>
    [TestFixture]
    public class VendorFalseGreenTests
    {
        static BuildValidator.ValidationResult GradeProbe(string body, string platformName = "Android")
        {
            FacebookPlatformValidator.ProbeResult probe =
                FacebookPlatformValidator.EvaluateResponse(false, 200, body, "123456", platformName, 0);
            return BuildValidator.GradeFacebookPlatform(true, probe.State, probe.Detail);
        }

        [Test]
        public void Facebook_MissingCredentials_IsError()
        {
            BuildValidator.ValidationResult result =
                BuildValidator.GradeFacebookPlatform(false, default, null);

            Assert.AreEqual(BuildValidator.ValidationStatus.Error, result.Status);
            Assert.That(result.Fix, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void Facebook_SuccessWithoutSupportedPlatformsField_IsPlatformMissingError()
        {
            FacebookPlatformValidator.ProbeResult probe =
                FacebookPlatformValidator.EvaluateResponse(false, 200, "{\"id\":\"123456\"}", "123456", "Android", 0);

            Assert.AreEqual(FacebookPlatformValidator.ProbeState.PlatformMissing, probe.State);
            Assert.AreEqual(
                BuildValidator.ValidationStatus.Error,
                BuildValidator.GradeFacebookPlatform(true, probe.State, probe.Detail).Status);
        }

        /// <summary>
        ///     Field absence only means "zero platforms" on a body proven to be THIS app's object. These
        ///     three 200s are not that, and grading them as zero-platform would fail the report on a fact
        ///     never observed.
        /// </summary>
        [TestCase("{\"error\":{\"message\":\"Invalid OAuth access token\",\"code\":190}}", TestName = "GraphErrorWrappedIn200")]
        [TestCase("{\"status\":\"ok\"}", TestName = "CaptivePortalBody")]
        [TestCase("{\"id\":\"999999\"}", TestName = "DifferentAppObject")]
        public void Facebook_SuccessBodyThatIsNotThisAppObject_StaysIncomplete(string body)
        {
            FacebookPlatformValidator.ProbeResult probe =
                FacebookPlatformValidator.EvaluateResponse(false, 200, body, "123456", "Android", 0);

            Assert.AreEqual(FacebookPlatformValidator.ProbeState.Unreachable, probe.State);
            Assert.AreEqual(
                BuildValidator.ValidationStatus.Unverifiable,
                BuildValidator.GradeFacebookPlatform(true, probe.State, probe.Detail).Status);
        }

        [Test]
        public void Facebook_SuccessWithEmptySupportedPlatforms_IsPlatformMissingError()
        {
            Assert.AreEqual(
                BuildValidator.ValidationStatus.Error,
                GradeProbe("{\"id\":\"123456\",\"supported_platforms\":[]}").Status);
        }

        [Test]
        public void Facebook_ActivePlatformRegistered_PassesWithoutGradingTheOtherPlatform()
        {
            BuildValidator.ValidationResult android =
                GradeProbe("{\"supported_platforms\":[\"ANDROID\"]}");
            BuildValidator.ValidationResult ios =
                GradeProbe("{\"supported_platforms\":[\"ANDROID\"]}", "iOS");

            Assert.AreEqual(BuildValidator.ValidationStatus.Valid, android.Status);
            Assert.That(android.Message, Does.Contain("iOS not registered"));
            Assert.AreEqual(BuildValidator.ValidationStatus.Error, ios.Status);
        }

        [Test]
        public void Facebook_UnparseableBody_GradesIncompleteNotAFailure()
        {
            FacebookPlatformValidator.ProbeResult probe = FacebookPlatformValidator.EvaluateResponse(
                false, 200, "<html>proxy interception</html>", "123456", "Android", 0);

            Assert.AreEqual(FacebookPlatformValidator.ProbeState.Unreachable, probe.State);
            Assert.AreEqual(
                BuildValidator.ValidationStatus.Unverifiable,
                BuildValidator.GradeFacebookPlatform(true, probe.State, probe.Detail).Status);
        }

        [Test]
        public void Facebook_TransportFailure_IsUnverifiableWithARetryAction()
        {
            FacebookPlatformValidator.ProbeResult probe =
                FacebookPlatformValidator.EvaluateResponse(true, 0, null, "123456", "Android", 0);
            BuildValidator.ValidationResult result =
                BuildValidator.GradeFacebookPlatform(true, probe.State, probe.Detail);

            Assert.AreEqual(BuildValidator.ValidationStatus.Unverifiable, result.Status);
            Assert.That(result.Fix, Does.Contain("graph.facebook.com"));
        }

        /// <summary>
        ///     A credential pair the Graph API rejects is an Error, and the row names WHICH of the three
        ///     causes Graph collapsed under errorCode 190 plus the asset holding the wrong value. The
        ///     deleted-app case is a recorded field incident: a Facebook app was deleted from the developer
        ///     console while its dead app id stayed referenced in MAX's FAN mediation and Adjust's Facebook
        ///     integration (Documentation~/dashboards/applovin-max.md). The probe sees the app
        ///     object's deletion, which is exactly the half of that incident Palette can observe.
        ///
        ///     This is the EDITOR row's severity. FacebookProbeFailureTests covers the runtime adapter's own
        ///     message builder for the same three causes; the two surfaces have separate producers.
        /// </summary>
        [TestCase("Error validating application. Application has been deleted.",
            "has been deleted", TestName = "DeletedApp")]
        [TestCase("Invalid OAuth access token signature.",
            "client token in FacebookSettings.asset does not match", TestName = "ClientTokenMismatch")]
        [TestCase("Invalid application ID",
            "does not match any Facebook app", TestName = "AppIdMatchesNoFacebookApp")]
        public void Facebook_RejectedCredentials_IsErrorNamingTheCause(string graphMessage, string expectedCause)
        {
            string body = "{\"error\":{\"message\":\"" + graphMessage + "\",\"type\":\"OAuthException\",\"code\":190}}";
            FacebookPlatformValidator.ProbeResult probe =
                FacebookPlatformValidator.EvaluateResponse(false, 400, body, "123456", "Android", 0);

            Assert.AreEqual(FacebookPlatformValidator.ProbeState.CredentialInvalid, probe.State);

            BuildValidator.ValidationResult result =
                BuildValidator.GradeFacebookPlatform(true, probe.State, probe.Detail);

            Assert.AreEqual(BuildValidator.ValidationStatus.Error, result.Status);
            Assert.That(result.Message, Does.Contain(expectedCause));
            Assert.That(result.Fix, Does.Contain("FacebookSettings.asset"));
        }

        /// <summary>
        ///     ...and a non-200 with no credential cause in it is the vendor being down, not the studio's
        ///     credentials being wrong. Grading every failed request as a rejected pair would fail reports
        ///     during a Graph outage.
        /// </summary>
        [TestCase(503, "<html>Service Unavailable</html>", TestName = "VendorOutage")]
        [TestCase(429, "{\"error\":{\"message\":\"Application request limit reached\",\"code\":4}}",
            TestName = "RateLimited")]
        public void Facebook_NonCredentialFailureResponse_StaysIncomplete(int responseCode, string body)
        {
            FacebookPlatformValidator.ProbeResult probe =
                FacebookPlatformValidator.EvaluateResponse(false, responseCode, body, "123456", "Android", 0);

            Assert.AreEqual(FacebookPlatformValidator.ProbeState.Unreachable, probe.State);
            Assert.AreEqual(
                BuildValidator.ValidationStatus.Unverifiable,
                BuildValidator.GradeFacebookPlatform(true, probe.State, probe.Detail).Status);
        }

        /// <summary>
        ///     The active build target's GameAnalytics key pair. The old check asked whether ANY platform had
        ///     keys, so an Android build of a game configured for iOS only read green while GameAnalytics
        ///     dropped 100% of its events (issue #8) - fatal in Prototype, where GA is the sole vendor.
        /// </summary>
        [Test]
        public void GameAnalytics_NoKeyPairForTheActivePlatform_IsErrorNamingThatPlatform()
        {
            BuildValidator.ValidationResult result =
                BuildValidator.GradeGameAnalyticsPlatformKeys(false, "Android");

            Assert.AreEqual(BuildValidator.ValidationStatus.Error, result.Status);
            Assert.That(result.Message, Does.Contain("Android"));
            Assert.That(result.Fix, Does.Contain("game key + secret key"));
        }

        [Test]
        public void GameAnalytics_KeyPairForTheActivePlatform_Passes()
        {
            BuildValidator.ValidationResult result =
                BuildValidator.GradeGameAnalyticsPlatformKeys(true, "iOS");

            Assert.AreEqual(BuildValidator.ValidationStatus.Valid, result.Status);
            Assert.That(result.Message, Does.Contain("iOS"));
        }

        [Test]
        public void Max_MissingActivePlatformAdMobAppId_IsErrorNamingTheField()
        {
            BuildValidator.ValidationResult result = BuildValidator.GradeMaxAdMobAppId("", "Android", "");

            Assert.AreEqual(BuildValidator.ValidationStatus.Error, result.Status);
            // The control that exists (AppLovin 8.6.4): a per-platform App ID field on the AdMob row of the
            // Integration Manager's Mediated Networks list.
            Assert.That(result.Fix, Does.Contain("App ID (Android)"));
            Assert.That(result.Fix, Does.Contain("Integration Manager"));
            // ...and who provisions it, since the studio cannot generate one.
            Assert.That(result.Fix, Does.Contain("Sorolla ops"));
        }

        [Test]
        public void Max_PresentAdMobAppId_ProducesNoFinding()
        {
            Assert.IsNull(BuildValidator.GradeMaxAdMobAppId("ca-app-pub-123~456", "Android", ""));
        }

        /// <summary>
        ///     Null is an unread property (AppLovin version drift), not an empty field: it must not fail a
        ///     build by claiming the studio left the id blank.
        /// </summary>
        [Test]
        public void Max_UnreadableAdMobAppId_IsIncompleteNamingTheFailedRead()
        {
            BuildValidator.ValidationResult result = BuildValidator.GradeMaxAdMobAppId(null, "Android", "");

            Assert.AreEqual(BuildValidator.ValidationStatus.Unverifiable, result.Status);
            Assert.That(result.Message, Does.Contain("Could not read"));
            Assert.That(result.Message, Does.Not.Contain("is empty"));
        }

        /// <summary>
        ///     Producers append into the SHARED result list every other check writes to, so a producer that
        ///     asks whether that list is empty is asking about its neighbours, not about itself. The MAX
        ///     check did exactly that and therefore never emitted its healthy-path pass: the row went to the
        ///     evaluator with zero observations, which a Required row reports as "no result was produced" -
        ///     a correctly configured project told to send a report to Sorolla.
        ///
        ///     Running it BOTH ways and comparing is what makes this state-independent. A single run can
        ///     only assert something that happens to be true of this machine's project; two runs assert the
        ///     property that was actually broken - what the check reports must not depend on what its
        ///     neighbours already reported. The old code fails this in the healthy case (fresh list emits a
        ///     pass, pre-populated emits nothing) and passes it in the broken case, which is exactly why a
        ///     fresh-list-only fixture let the defect through.
        ///
        ///     Note: this exercises the live check, so the MAX settings sanitizers run - the same idempotent
        ///     writes the editor already performs on every validation pass and domain reload.
        /// </summary>
        [Test]
        public void MaxSettings_ReportsTheSame_WhateverElseIsAlreadyInTheSharedList()
        {
            var fresh = new List<BuildValidator.ValidationResult>();
            BuildValidator.CheckMaxSettings(fresh);

            var shared = new List<BuildValidator.ValidationResult>
            {
                Observed(ReadinessChecks.RequiredSdks, BuildValidator.ValidationStatus.Valid, "earlier check"),
                Observed(ReadinessChecks.ConfigSync, BuildValidator.ValidationStatus.Valid, "earlier check"),
            };
            BuildValidator.CheckMaxSettings(shared);
            List<BuildValidator.ValidationResult> emitted = shared.Skip(2).ToList();

            CollectionAssert.IsNotEmpty(fresh,
                "The MAX check produced no observation at all, which the evaluator reports as a missing result.");
            CollectionAssert.AreEqual(
                fresh.Select(Identity).ToList(),
                emitted.Select(Identity).ToList(),
                "The MAX check reported different things depending on what its neighbours had already appended.");
            CollectionAssert.AreEquivalent(
                new[] { ReadinessChecks.MaxSettings },
                emitted.Select(r => r.Check).Distinct().ToList());
        }

        /// <summary>ValidationResult has reference equality, so findings are compared by what they say.</summary>
        static (BuildValidator.ValidationStatus, string, string, ReadinessCheck) Identity(
            BuildValidator.ValidationResult result) =>
            (result.Status, result.Message, result.Fix, result.Check);

        [Test]
        public void Firebase_ConfigCheckApplies_ToAnyInstalledFirebaseModule()
        {
            var crashlyticsOnly = new Dictionary<string, object>
            {
                { SdkRegistry.All[SdkId.FirebaseApp].PackageId, "12.0.0" },
                { SdkRegistry.All[SdkId.FirebaseCrashlytics].PackageId, "12.0.0" },
            };

            Assert.IsTrue(BuildValidator.HasAnyFirebaseModule(crashlyticsOnly));
            Assert.IsFalse(BuildValidator.HasAnyFirebaseModule(new Dictionary<string, object>()));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Firebase_ConfigApplicability_RequiresACompleteSuiteInEveryMode(bool isPrototype)
        {
            EvalMode mode = isPrototype ? EvalMode.Prototype : EvalMode.Full;
            Assert.AreEqual(ReadinessRequirement.Required,
                Applicability(mode, SdkModule.Firebase), "complete suite");
            Assert.AreEqual(ReadinessRequirement.NotApplicable,
                Applicability(mode, SdkModule.FirebaseApp), "incomplete suite");
            Assert.AreEqual(ReadinessRequirement.NotApplicable,
                Applicability(mode, SdkModule.None), "absent suite");
        }

        static ReadinessRequirement Applicability(EvalMode mode, SdkModule modules) =>
            ReadinessChecks.FirebaseConfigAndroid.Requirement(Context(modules, mode)).Value;

        static ReadinessContext Context(SdkModule modules, EvalMode mode = EvalMode.Full) => new ReadinessContext
        {
            Mode = mode,
            Platform = ReadinessPlatform.Android,
            InstalledModules = modules,
            ModulesResolved = true,
        };

        const SdkModule FullSuite = SdkModule.GameAnalytics | SdkModule.Facebook | SdkModule.Firebase |
                                    SdkModule.AppLovinMax | SdkModule.Adjust;

        static BuildValidator.ValidationResult Observed(
            ReadinessCheck check, BuildValidator.ValidationStatus status, string message) =>
            new BuildValidator.ValidationResult(status, message, "fix", check);

        /// <summary>
        ///     The failure-grading rule, asserted on the model the pre-build hook actually reads. Cached vendor
        ///     Errors fail and an unreachable probe does not. That BuildValidatorPreprocessor consumes
        ///     exactly this collection is hand-verified (its one-line call site): OnPreprocessBuild needs a
        ///     BuildReport and live project state. Accepted limitation.
        /// </summary>
        [Test]
        public void EvaluatedReport_FailsOnGradedFailuresOnly()
        {
            FacebookPlatformValidator.ProbeResult unreachable =
                FacebookPlatformValidator.EvaluateResponse(true, 0, null, "123456", "Android", 0);

            ReadinessReport report = ReadinessEvaluator.Evaluate(Context(FullSuite),
                new List<BuildValidator.ValidationResult>
                {
                    BuildValidator.GradeFacebookPlatform(false, default, null),
                    BuildValidator.GradeMaxAdMobAppId("", "Android", ""),
                    BuildValidator.GradeFacebookPlatform(true, unreachable.State, unreachable.Detail),
                });

            CollectionAssert.AreEquivalent(
                new[] { ReadinessChecks.FacebookPlatformConfig.Id, ReadinessChecks.MaxSettings.Id },
                report.FailingRows.Select(r => r.Check.Id).ToList());
        }

        /// <summary>
        ///     PINNING REGRESSION for the grading source: the Firebase config producer observes without
        ///     asking whether its row applies, so on an incomplete required suite it can emit an Error against a
        ///     row the catalog resolves NotApplicable. The report discards it - and because the build-log error pass
        ///     reads that same report, the discarded finding cannot surface at build time with nothing on screen to
        ///     explain it. Reading raw producer results is exactly the shape this pins shut.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void PartialSuite_IsOwnedByRequiredPackagesWithoutDuplicateFirebaseFailure(bool isPrototype)
        {
            EvalMode mode = isPrototype ? EvalMode.Prototype : EvalMode.Full;
            ReadinessReport report = ReadinessEvaluator.Evaluate(
                Context(SdkModule.GameAnalytics | SdkModule.Facebook | SdkModule.FirebaseApp, mode),
                new List<BuildValidator.ValidationResult>
                {
                    Observed(ReadinessChecks.RequiredSdks, BuildValidator.ValidationStatus.Error,
                        "Firebase modules missing."),
                    Observed(ReadinessChecks.FirebaseConfigAndroid, BuildValidator.ValidationStatus.Error,
                        "google-services.json not found."),
                });
            ReadinessRow row = report.Rows.Single(r => r.Check == ReadinessChecks.FirebaseConfigAndroid);

            Assert.AreEqual(ReadinessDisposition.NotApplicable, row.Disposition);
            CollectionAssert.AreEqual(
                new[] { ReadinessChecks.RequiredSdks.Id },
                report.FailingRows.Select(r => r.Check.Id).ToArray());
        }

        /// <summary>
        ///     ...and where the same row IS gradable, the Error fails - so discarding is scoped to
        ///     inapplicability rather than quietly swallowing Firebase failures.
        /// </summary>
        [Test]
        public void ProducerErrorOnAGradableFirebaseRow_Fails()
        {
            ReadinessReport report = ReadinessEvaluator.Evaluate(Context(FullSuite),
                new List<BuildValidator.ValidationResult>
                {
                    Observed(ReadinessChecks.FirebaseConfigAndroid, BuildValidator.ValidationStatus.Error,
                        "google-services.json not found."),
                });

            CollectionAssert.AreEquivalent(
                new[] { ReadinessChecks.FirebaseConfigAndroid.Id },
                report.FailingRows.Select(r => r.Check.Id).ToList());
        }

        [Test]
        public void MissingFirebaseConfig_IsAlwaysAnError()
        {
            BuildValidator.ValidationResult result = BuildValidator.MissingConfig(
                ReadinessChecks.FirebaseConfigAndroid, "missing", "add it");

            Assert.AreEqual(BuildValidator.ValidationStatus.Error, result.Status);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FirebaseVendorUi_HasNoOptionalOrManualInstallState(bool suiteInstalled)
        {
            VendorStatus status = VendorStatusProbe.ResolveFirebaseStatus(false, suiteInstalled);

            Assert.IsFalse(status.Optional);
            Assert.AreEqual(suiteInstalled ? VendorStatus.Phase.Pass : VendorStatus.Phase.Fail, status.State);
            Assert.AreNotEqual("Install", status.ActionLabel);
        }
    }
}
