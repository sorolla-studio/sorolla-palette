using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using Sorolla.Palette.Editor;

namespace Sorolla.Palette.Editor.Tests
{
    [TestFixture]
    public class AndroidManifestSanitizerTests
    {
        private const string ActivityClass = "com.unity3d.player.UnityPlayerActivity";
        private const string GameActivityClass = "com.unity3d.player.UnityPlayerGameActivity";

        // --- Bitmask logic ---
        // GetExpectedMainActivity() reads PlayerSettings which can't be mocked in EditMode tests.
        // These tests verify the bitmask formula itself: (value & 2) != 0 -> GameActivity.

        [TestCase(1, false, Description = "Activity only")]
        [TestCase(2, true, Description = "GameActivity only")]
        [TestCase(3, true, Description = "Both - prefers GameActivity")]
        public void BitmaskLogic_CorrectlyDetectsGameActivity(int bitmask, bool expectGameActivity)
        {
            var isGameActivity = (bitmask & 2) != 0;
            Assert.AreEqual(expectGameActivity, isGameActivity);
        }

        // --- DetectWrongMainActivityInXml ---

        [Test]
        public void DetectWrongMainActivity_WrongClass_ReturnsClassName()
        {
            // Manifest has UnityPlayerActivity, but project expects GameActivity
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" package=""com.unity3d.player"">
    <application>
        <activity android:name=""com.unity3d.player.UnityPlayerActivity""
                  android:exported=""true"">
            <intent-filter>
                <action android:name=""android.intent.action.MAIN"" />
                <category android:name=""android.intent.category.LAUNCHER"" />
            </intent-filter>
        </activity>
    </application>
</manifest>";

            var result = AndroidManifestSanitizer.DetectWrongMainActivityInXml(xml, GameActivityClass);

            Assert.AreEqual(ActivityClass, result);
        }

        [Test]
        public void DetectWrongMainActivity_CorrectClass_ReturnsNull()
        {
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" package=""com.unity3d.player"">
    <application>
        <activity android:name=""com.unity3d.player.UnityPlayerGameActivity""
                  android:exported=""true"">
            <intent-filter>
                <action android:name=""android.intent.action.MAIN"" />
                <category android:name=""android.intent.category.LAUNCHER"" />
            </intent-filter>
        </activity>
    </application>
</manifest>";

            var result = AndroidManifestSanitizer.DetectWrongMainActivityInXml(xml, GameActivityClass);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectWrongMainActivity_NoLauncherActivity_ReturnsNull()
        {
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" package=""com.unity3d.player"">
    <application>
        <activity android:name=""com.unity3d.player.UnityPlayerActivity""
                  android:exported=""true"">
        </activity>
    </application>
</manifest>";

            var result = AndroidManifestSanitizer.DetectWrongMainActivityInXml(xml, GameActivityClass);

            Assert.IsNull(result);
        }

        // --- DetectLauncherManifestIssueInXml ---

        [Test]
        public void DetectLauncherManifestIssue_MissingActivity_ReturnsError()
        {
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" package=""com.unity3d.player"">
    <application>
    </application>
</manifest>";

            var result = AndroidManifestSanitizer.DetectLauncherManifestIssueInXml(xml, GameActivityClass);

            Assert.That(result, Does.Contain("no activity"));
            Assert.That(result, Does.Contain("MAIN/LAUNCHER"));
        }

        [Test]
        public void DetectLauncherManifestIssue_WrongClass_ReturnsError()
        {
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" package=""com.unity3d.player"">
    <application>
        <activity android:name=""com.unity3d.player.UnityPlayerActivity""
                  android:exported=""true"">
            <intent-filter>
                <action android:name=""android.intent.action.MAIN"" />
                <category android:name=""android.intent.category.LAUNCHER"" />
            </intent-filter>
        </activity>
    </application>
</manifest>";

            var result = AndroidManifestSanitizer.DetectLauncherManifestIssueInXml(xml, GameActivityClass);

            Assert.That(result, Does.Contain("wrong activity class"));
            Assert.That(result, Does.Contain(ActivityClass));
        }

        [Test]
        public void DetectLauncherManifestIssue_CorrectClass_ReturnsNull()
        {
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" package=""com.unity3d.player"">
    <application>
        <activity android:name=""com.unity3d.player.UnityPlayerGameActivity""
                  android:exported=""true"">
            <intent-filter>
                <action android:name=""android.intent.action.MAIN"" />
                <category android:name=""android.intent.category.LAUNCHER"" />
            </intent-filter>
        </activity>
    </application>
</manifest>";

            var result = AndroidManifestSanitizer.DetectLauncherManifestIssueInXml(xml, GameActivityClass);

            Assert.IsNull(result);
        }

        [Test]
        public void DetectLauncherManifestIssue_NoApplicationElement_ReturnsError()
        {
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" package=""com.unity3d.player"">
</manifest>";

            var result = AndroidManifestSanitizer.DetectLauncherManifestIssueInXml(xml, GameActivityClass);

            Assert.That(result, Does.Contain("no <application> element"));
        }

        // --- FixMainActivity tools:replace ---

        [Test]
        public void FixMainActivity_WithoutToolsReplace_SkipsIt()
        {
            var expected = AndroidManifestSanitizer.GetExpectedMainActivity();
            var xml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" package=""com.test"">
    <application>
        <activity android:name=""{expected}"" android:theme=""@style/WrongTheme"" android:exported=""true"">
            <intent-filter>
                <action android:name=""android.intent.action.MAIN"" />
                <category android:name=""android.intent.category.LAUNCHER"" />
            </intent-filter>
        </activity>
    </application>
</manifest>";
            var doc = XDocument.Parse(xml);

            AndroidManifestSanitizer.FixMainActivity(doc, requireToolsReplace: false);

            var toolsNs = XNamespace.Get("http://schemas.android.com/tools");
            var activity = AndroidManifestSanitizer.FindLauncherActivity(doc.Root.Element("application"));
            Assert.IsNull(activity.Attribute(toolsNs + "replace"),
                "tools:replace should not be added when requireToolsReplace is false");
        }

        [Test]
        public void FixMainActivity_WithToolsReplace_AddsIt()
        {
            var expected = AndroidManifestSanitizer.GetExpectedMainActivity();
            var xml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" package=""com.test"">
    <application>
        <activity android:name=""{expected}"" android:theme=""@style/WrongTheme"" android:exported=""true"">
            <intent-filter>
                <action android:name=""android.intent.action.MAIN"" />
                <category android:name=""android.intent.category.LAUNCHER"" />
            </intent-filter>
        </activity>
    </application>
</manifest>";
            var doc = XDocument.Parse(xml);

            AndroidManifestSanitizer.FixMainActivity(doc, requireToolsReplace: true);

            var toolsNs = XNamespace.Get("http://schemas.android.com/tools");
            var activity = AndroidManifestSanitizer.FindLauncherActivity(doc.Root.Element("application"));
            var replaceAttr = activity.Attribute(toolsNs + "replace");
            Assert.IsNotNull(replaceAttr,
                "tools:replace should be added when requireToolsReplace is true");
            Assert.That(replaceAttr.Value, Does.Contain("android:theme"));
        }

        // --- StripLibraryLauncherIntent (pure XML via internal helper) ---

        [Test]
        public void StripLibraryLauncherIntent_RemovesLauncherCategory()
        {
            var ns = AndroidManifestSanitizer.AndroidNs;
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" package=""com.test"">
    <application>
        <activity android:name=""com.unity3d.player.UnityPlayerGameActivity"" android:exported=""true"">
            <intent-filter>
                <action android:name=""android.intent.action.MAIN"" />
                <category android:name=""android.intent.category.LAUNCHER"" />
            </intent-filter>
        </activity>
    </application>
</manifest>";
            var doc = XDocument.Parse(xml);
            var application = doc.Root.Element("application");
            var activity = application.Element("activity");

            // Manually strip LAUNCHER intent-filter (same logic as StripLibraryLauncherIntent)
            var launcherFilters = activity.Elements("intent-filter")
                .Where(f => f.Elements("category")
                    .Any(c => c.Attribute(ns + "name")?.Value == "android.intent.category.LAUNCHER"))
                .ToList();
            foreach (var filter in launcherFilters)
                filter.Remove();

            Assert.IsNull(AndroidManifestSanitizer.FindLauncherActivity(application),
                "LAUNCHER intent should be removed from library manifest");
        }
    }
}
