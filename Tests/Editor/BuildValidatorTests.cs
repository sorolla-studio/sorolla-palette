using System.Collections.Generic;
using NUnit.Framework;
using Sorolla.Palette.Editor;

namespace Sorolla.Palette.Editor.Tests
{
    [TestFixture]
    public class BuildValidatorTests
    {
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
        public void RemoveBuildscriptBlock_NestedBraces_MatchesCorrectly()
        {
            var input = @"buildscript {
    repositories {
        google()
    }
    dependencies {
        classpath ""com.android.tools:r8:8.1.56""
    }
}
plugins {
}
";
            var result = BuildValidator.RemoveBuildscriptBlock(input);

            Assert.That(result, Does.Not.Contain("buildscript"));
            Assert.That(result, Does.Contain("plugins"));
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

        [TestCase("org.gradle.jvmargs=-Xmx4096m", true)]
        [TestCase("", true)]
        [TestCase(null, true)]
        [TestCase("org.gradle.java.home=/Library/Java/JavaVirtualMachines/jdk-17.jdk/Contents/Home", false)]
        public void MissingGradleJavaHome_DetectsAbsentProperty(string properties, bool expected)
        {
            Assert.AreEqual(expected, BuildValidator.MissingGradleJavaHome(properties));
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
