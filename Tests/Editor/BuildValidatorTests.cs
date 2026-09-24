using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Sorolla.Palette.Editor;
using Sorolla.Palette.Health;
using UnityEngine;
using UnityEngine.TestTools;

namespace Sorolla.Palette.Editor.Tests
{
    [TestFixture]
    public class BuildValidatorTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void FirebaseSuite_IsRequiredInEveryMode(bool isPrototype)
        {
            foreach (SdkId id in new[]
            {
                SdkId.FirebaseApp,
                SdkId.FirebaseAnalytics,
                SdkId.FirebaseCrashlytics,
                SdkId.FirebaseRemoteConfig,
            })
            {
                SdkInfo sdk = SdkRegistry.All[id];
                Assert.AreEqual(SdkRequirement.Core, sdk.Requirement, id.ToString());
                Assert.IsTrue(sdk.IsRequiredFor(isPrototype), id.ToString());
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CompositeFirebaseCapability_RequiresTheCompleteSuite(bool isPrototype)
        {
            EvalMode mode = isPrototype ? EvalMode.Prototype : EvalMode.Full;
            CapabilityState partial =
                CapabilityPolicy.Resolve(mode, SdkModule.FirebaseApp, SdkModule.Firebase);
            CapabilityState complete =
                CapabilityPolicy.Resolve(mode, SdkModule.Firebase, SdkModule.Firebase);

            Assert.IsTrue(partial.Required);
            Assert.IsFalse(partial.Included);
            Assert.IsFalse(partial.Applicable);
            Assert.IsTrue(complete.Required);
            Assert.IsTrue(complete.Included);
            Assert.IsTrue(complete.Applicable);
        }

        // ── Per-check isolation: one thrown check may not take the pass down with it ──

        /// <summary>
        ///     One shared try/catch used to wrap the whole validation pass, so the first check that threw
        ///     ended it: every later check silently never ran, and the exception was attributed to whichever
        ///     row the catch happened to name. A studio saw one unrelated red row and a report missing most
        ///     of its evidence.
        ///
        ///     Three properties in one fixture, because the defect was all three at once: the throwing check
        ///     names ITSELF (with its sub-check part, so two thrown findings on a shared row stay
        ///     distinguishable), what it already PROVED before throwing survives, and the checks after it
        ///     still run.
        /// </summary>
        [Test]
        public void AThrownCheck_NamesItself_KeepsWhatItProved_AndDoesNotStopLaterChecks()
        {
            LogAssert.Expect(LogType.Error, new Regex("build\\.gradle_config.*threw"));
            var results = new List<BuildValidator.ValidationResult>();

            BuildValidator.Run(results, ReadinessChecks.GradleConfig, list =>
            {
                list.Add(new BuildValidator.ValidationResult(
                    BuildValidator.ValidationStatus.Error, "compileOptions are on Java 11",
                    "raise them to 17", ReadinessChecks.GradleConfig));
                throw new InvalidOperationException("boom");
            }, "Java + templates");

            BuildValidator.Run(results, ReadinessChecks.MaxSettings, list => list.Add(
                new BuildValidator.ValidationResult(
                    BuildValidator.ValidationStatus.Valid, "MAX ad units set", null, ReadinessChecks.MaxSettings)));

            Assert.AreEqual(3, results.Count,
                "Expected the proven finding, the thrown finding, and the check that ran after it.");
            Assert.AreEqual("compileOptions are on Java 11", results[0].Message,
                "A finding proven before the throw must survive it - it still fails the report.");

            BuildValidator.ValidationResult thrown = results[1];
            Assert.AreEqual(BuildValidator.ValidationStatus.Unverifiable, thrown.Status,
                "Evaluation failed; that is not evidence the integration is broken, so it must not fail the report.");
            Assert.That(thrown.Message, Does.Contain("Gradle Configuration (Java + templates)"));
            Assert.That(thrown.Message, Does.Contain("boom"));
            Assert.AreEqual(ReadinessChecks.GradleConfig, thrown.Check,
                "The thrown finding is attributed to the check that threw, not to a neighbour.");

            Assert.AreEqual(BuildValidator.ValidationStatus.Valid, results[2].Status);
            Assert.AreEqual(ReadinessChecks.MaxSettings, results[2].Check);
        }

        [Test]
        public void RemoveBuildscriptBlock_WithR8Pin_RemovesBlock()
        {
            var input = @"buildscript {
    repositories {
        google()
        mavenCentral()
    }
    dependencies {
        classpath ""com.android.tools:r8:8.1.56""
    }
}

plugins {
    id 'com.android.application' version '8.10.0' apply false
}

task clean(type: Delete) {
    delete rootProject.buildDir
}
";
            var result = BuildValidator.RemoveBuildscriptBlock(input);

            Assert.That(result, Does.Not.Contain("buildscript"));
            Assert.That(result, Does.Not.Contain("com.android.tools:r8"));
            Assert.That(result, Does.Contain("plugins"));
            Assert.That(result, Does.Contain("task clean"));
        }

        [Test]
        public void RemoveBuildscriptBlock_WithoutBlock_ReturnsUnchanged()
        {
            var input = @"plugins {
    id 'com.android.application' version '8.10.0' apply false
}

task clean(type: Delete) {
    delete rootProject.buildDir
}
";
            var result = BuildValidator.RemoveBuildscriptBlock(input);

            Assert.AreEqual(input, result);
        }

        [Test]
        public void RemoveBuildscriptBlock_UnbalancedBraces_ReturnsUnchanged()
        {
            var input = @"buildscript {
    repositories {
        google()
";
            var result = BuildValidator.RemoveBuildscriptBlock(input);

            Assert.AreEqual(input, result);
        }

        /// <summary>
        ///     The machine-local JDK path is deleted from the committed template rather than reported (B-16
        ///     shipped it into version control once). Everything else in the file, including a commented-out
        ///     line the studio wrote, survives untouched - a properties file is line-oriented, so this is one
        ///     exact owned line, not arbitrary Gradle being parsed.
        /// </summary>
        [Test]
        public void RemoveGradleJavaHomeLines_DeletesOnlyTheLiveLine()
        {
            const string input = "android.useAndroidX=true\r\n" +
                                 "org.gradle.java.home=/Users/someone/Library/Java/JavaVirtualMachines/jdk-17\r\n" +
                                 "# org.gradle.java.home=/opt/jdk17\r\n" +
                                 "org.gradle.jvmargs=-Xmx4096M\r\n";

            string result = BuildValidator.RemoveGradleJavaHomeLines(input);

            Assert.IsFalse(BuildValidator.HasGradleJavaHomeLine(result));
            Assert.That(result, Does.Contain("android.useAndroidX=true"));
            Assert.That(result, Does.Contain("org.gradle.jvmargs=-Xmx4096M"));
            Assert.That(result, Does.Contain("# org.gradle.java.home=/opt/jdk17"), "A comment is the studio's own note.");
            Assert.That(result, Does.Contain("\r\n"), "Line endings are preserved.");
        }

        [Test]
        public void RemoveGradleJavaHomeLines_WithoutTheLine_ReturnsUnchanged()
        {
            const string input = "android.useAndroidX=true\nunityStreamingAssets=.unity3d\n";

            Assert.AreEqual(input, BuildValidator.RemoveGradleJavaHomeLines(input));
        }

        [TestCase("org.gradle.java.home=/opt/jdk17", true)]
        [TestCase("  org.gradle.java.home = /opt/jdk17", true, TestName = "LeadingWhitespaceAndSpacedAssignment")]
        [TestCase("# org.gradle.java.home=/opt/jdk17", false, TestName = "CommentedOut")]
        [TestCase("org.gradle.jvmargs=-Xmx4096M", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void HasGradleJavaHomeLine_MatchesTheLiveAssignmentOnly(string properties, bool expected)
        {
            Assert.AreEqual(expected, BuildValidator.HasGradleJavaHomeLine(properties));
        }

        [TestCase("sourceCompatibility JavaVersion.VERSION_11", true)]
        [TestCase("targetCompatibility = JavaVersion.VERSION_11", true)]
        [TestCase("sourceCompatibility JavaVersion.VERSION_17", false)]
        [TestCase("VERSION_11", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void HasJava11CompileOptions_DetectsOnlyCompileOptions(string gradle, bool expected)
        {
            Assert.AreEqual(expected, BuildValidator.HasJava11CompileOptions(gradle));
        }

        [TestCase("classpath \"com.android.tools:r8:8.1.56\"", true)]
        [TestCase("implementation \"com.android.tools.build:gradle:8.10.0\"", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void HasR8Pin_DetectsExplicitR8Dependency(string gradle, bool expected)
        {
            Assert.AreEqual(expected, BuildValidator.HasR8Pin(gradle));
        }

        [TestCase("details.useVersion '1.8.22' // kotlin-stdlib", true)]
        [TestCase("implementation \"org.jetbrains.kotlin:kotlin-stdlib:2.0.0\"", false)]
        [TestCase("details.useVersion '1.8.22'", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void ForcesKotlinStdlibVersion_DetectsResolutionStrategy(string gradle, bool expected)
        {
            Assert.AreEqual(expected, BuildValidator.ForcesKotlinStdlibVersion(gradle));
        }

        [Test]
        public void RequiredRegistries_MissingScope_RepairsOnceThenRemainsUnchanged()
        {
            var openUpmScopes = new List<object> { "com.google.external-dependency-manager" };
            var registries = new List<object>
            {
                Registry("package.openupm.com", "https://package.openupm.com", openUpmScopes),
            };

            Assert.IsTrue(SdkInstaller.EnsureRequiredRegistryEntries(registries, isPrototype: true));
            CollectionAssert.Contains(openUpmScopes, "com.gameanalytics");

            string repaired = MiniJson.Serialize(registries, prettyPrint: true);
            Assert.IsFalse(SdkInstaller.EnsureRequiredRegistryEntries(registries, isPrototype: true));
            Assert.AreEqual(repaired, MiniJson.Serialize(registries, prettyPrint: true));
        }

        [Test]
        public void RequiredRegistries_FullMode_MovesMaxScopeOutOfOpenUpm()
        {
            var openUpmScopes = new List<object>
            {
                "com.google.external-dependency-manager",
                "com.gameanalytics",
                "com.applovin",
                "com.applovin",
            };
            var registries = new List<object>
            {
                Registry("package.openupm.com", "https://package.openupm.com", openUpmScopes),
            };

            Assert.IsTrue(SdkInstaller.EnsureRequiredRegistryEntries(registries, isPrototype: false));
            CollectionAssert.DoesNotContain(openUpmScopes, "com.applovin");

            var max = (Dictionary<string, object>)registries.Find(entry =>
                entry is Dictionary<string, object> registry &&
                registry.TryGetValue("url", out object url) &&
                url?.ToString() == "https://unity.packages.applovin.com/");
            Assert.NotNull(max);
            CollectionAssert.Contains((List<object>)max["scopes"], "com.applovin");

            string repaired = MiniJson.Serialize(registries, prettyPrint: true);
            Assert.IsFalse(SdkInstaller.EnsureRequiredRegistryEntries(registries, isPrototype: false));
            Assert.AreEqual(repaired, MiniJson.Serialize(registries, prettyPrint: true));
        }

        static Dictionary<string, object> Registry(string name, string url, List<object> scopes) =>
            new Dictionary<string, object>
            {
                ["name"] = name,
                ["url"] = url,
                ["scopes"] = scopes,
            };

        // ── GameAnalytics whitelist: what the SDK may rewrite for a studio ──

        [TestCase("Coins", "coins")]
        [TestCase("COINS", "coins")]
        [TestCase("Level Reward", "level_reward")]
        [TestCase("LevelReward", "level_reward")]
        [TestCase("level-reward", "level_reward")]
        public void WhitelistEntry_ThatMeansAPaletteValue_IsRewrittenToWhatIsSent(string entry, string sent)
        {
            string[] emitted = { "coins", "gems", "level_reward", "booster" };

            Assert.IsTrue(BuildValidator.TryMatchEmitted(entry, emitted, out string match));
            Assert.AreEqual(sent, match);
        }

        [TestCase("coins")]         // already exactly what Palette sends
        [TestCase("premium_gold")]  // some other integration's currency: not ours to touch
        [TestCase("")]
        [TestCase(null)]
        public void WhitelistEntry_ThatIsCorrectOrForeign_IsLeftAlone(string entry)
        {
            string[] emitted = { "coins", "gems", "level_reward", "booster" };

            Assert.IsFalse(BuildValidator.TryMatchEmitted(entry, emitted, out string match));
            Assert.IsNull(match);
        }

        [Test]
        public void PaletteEconomyVocabulary_IsTheLowerSnakeCaseFormGameAnalyticsMatchesOn()
        {
            // The whole check rests on knowing exactly what goes in GameAnalytics' currency and item-type
            // slots. If the emitted form ever changes, this fails here rather than silently in a dashboard.
            CollectionAssert.Contains(EconomyVocabulary.Currencies(), "coins");
            CollectionAssert.Contains(EconomyVocabulary.ItemTypes(), "level_reward");
            CollectionAssert.Contains(EconomyVocabulary.ItemTypes(), "shop_purchase");
            foreach (string value in EconomyVocabulary.Currencies())
                Assert.AreEqual(value.ToLowerInvariant(), value, value);
        }
    }
}
