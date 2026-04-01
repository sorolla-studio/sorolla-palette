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
    }
}
