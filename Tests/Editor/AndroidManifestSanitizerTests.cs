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
    }
}
