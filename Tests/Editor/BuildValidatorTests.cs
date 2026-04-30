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
    }
}
