using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sorolla.Palette.Editor;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     Fixture tests for the pure Firebase config parser + active-app match (Cycle 9). These exercise
    ///     <see cref="FirebaseConfigMatch"/> on raw strings only - no AssetDatabase / PlayerSettings - which is
    ///     the whole point of keeping the parse Unity-free. Severity mapping (present-but-mismatched → FAIL,
    ///     zero/many → INCOMPLETE, missing-where-required → block) lives in the editor check and is covered by
    ///     the observed BuildValidator behaviour; here we pin the parse/match outcomes it depends on.
    /// </summary>
    [TestFixture]
    public class FirebaseConfigMatchTests
    {
        const string AppId = "com.sorolla.game";

        static string AndroidJson(params string[] packageNames)
        {
            string clients = string.Join(",", packageNames.Select(p =>
                $"{{\"client_info\":{{\"android_client_info\":{{\"package_name\":\"{p}\"}}}}}}"));
            return $"{{\"project_info\":{{\"project_id\":\"proj-a\"}},\"client\":[{clients}]}}";
        }

        static string Plist(string bundleId, string projectId = "proj-a") =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
            "<plist version=\"1.0\"><dict>\n" +
            "<key>CLIENT_ID</key><string>abc.apps.googleusercontent.com</string>\n" +
            $"<key>PROJECT_ID</key><string>{projectId}</string>\n" +
            $"<key>BUNDLE_ID</key><string>{bundleId}</string>\n" +
            "</dict></plist>";

        // ── Android ───────────────────────────────────────────────────────

        [Test]
        public void Android_Match_SingleClient()
        {
            Assert.AreEqual(FirebaseConfigMatchResult.Match,
                FirebaseConfigMatch.MatchAndroid(AndroidJson(AppId), AppId, out var found));
            CollectionAssert.Contains(found.ToList(), AppId);
        }

        [Test]
        public void Android_Mismatch_WrongGame_NamesBothIds()
        {
            var result = FirebaseConfigMatch.MatchAndroid(AndroidJson("com.other.game"), AppId, out var found);
            Assert.AreEqual(FirebaseConfigMatchResult.Mismatch, result);
            CollectionAssert.Contains(found.ToList(), "com.other.game"); // the check surfaces this as "config package name(s)"
        }

        [Test]
        public void Android_MultiClient_OneMatches_IsMatch()
        {
            Assert.AreEqual(FirebaseConfigMatchResult.Match,
                FirebaseConfigMatch.MatchAndroid(AndroidJson("com.other.game", AppId, "com.third.game"), AppId, out _));
        }

        [Test]
        public void Android_DuplicateClients_SamePackage_DedupedToOne()
        {
            Assert.AreEqual(FirebaseConfigMatchResult.Match,
                FirebaseConfigMatch.MatchAndroid(AndroidJson(AppId, AppId), AppId, out var found));
            Assert.AreEqual(1, found.Count, "duplicate clients with the same package_name must dedupe in the set");
        }

        [Test]
        public void Android_Malformed_IsUnparseable()
        {
            Assert.AreEqual(FirebaseConfigMatchResult.Unparseable,
                FirebaseConfigMatch.MatchAndroid("{ this is not valid json", AppId, out _));
        }

        [Test]
        public void Android_NoClientArray_IsUnparseable()
        {
            // Parses as JSON but is not a recognizable google-services.json - do not read as "no match", read as
            // "cannot verify" so a truncated/wrong file is INCOMPLETE, not a silent Mismatch/pass.
            Assert.AreEqual(FirebaseConfigMatchResult.Unparseable,
                FirebaseConfigMatch.MatchAndroid("{\"project_info\":{\"project_id\":\"x\"}}", AppId, out _));
        }

        [Test]
        public void Android_EmptyClientArray_IsMismatch()
        {
            Assert.AreEqual(FirebaseConfigMatchResult.Mismatch,
                FirebaseConfigMatch.MatchAndroid("{\"client\":[]}", AppId, out var found));
            Assert.AreEqual(0, found.Count);
        }

        [Test]
        public void Android_BomAndWhitespace_IsMatch()
        {
            string withBom = "\uFEFF   " + AndroidJson(AppId);
            Assert.AreEqual(FirebaseConfigMatchResult.Match,
                FirebaseConfigMatch.MatchAndroid(withBom, AppId, out _));
        }

        [Test]
        public void Android_SameIdDifferentProject_IsMatch_DocumentedResidual()
        {
            // The residual (F9): package_name matches but project_info.project_id differs. We CANNOT prove
            // Firebase project identity, so this correctly reads Match. The limitation is documented, not fixed.
            string differentProject =
                $"{{\"project_info\":{{\"project_id\":\"a-DIFFERENT-project\"}},\"client\":" +
                $"[{{\"client_info\":{{\"android_client_info\":{{\"package_name\":\"{AppId}\"}}}}}}]}}";
            Assert.AreEqual(FirebaseConfigMatchResult.Match,
                FirebaseConfigMatch.MatchAndroid(differentProject, AppId, out _));
        }

        [Test]
        public void Android_FedAPlist_IsUnparseable()
        {
            // "wrong active target": feeding the iOS plist to the Android matcher must not silently pass.
            Assert.AreEqual(FirebaseConfigMatchResult.Unparseable,
                FirebaseConfigMatch.MatchAndroid(Plist(AppId), AppId, out _));
        }

        // ── iOS ─────────────────────────────────────────────────────────

        [Test]
        public void Ios_Match()
        {
            Assert.AreEqual(FirebaseConfigMatchResult.Match,
                FirebaseConfigMatch.MatchIos(Plist(AppId), AppId, out string found));
            Assert.AreEqual(AppId, found);
        }

        [Test]
        public void Ios_Mismatch_WrongGame_NamesFoundId()
        {
            var result = FirebaseConfigMatch.MatchIos(Plist("com.other.game"), AppId, out string found);
            Assert.AreEqual(FirebaseConfigMatchResult.Mismatch, result);
            Assert.AreEqual("com.other.game", found);
        }

        [Test]
        public void Ios_Malformed_IsUnparseable()
        {
            Assert.AreEqual(FirebaseConfigMatchResult.Unparseable,
                FirebaseConfigMatch.MatchIos("<plist><dict><key>BUNDLE_ID</key><string>oops", AppId, out _));
        }

        [Test]
        public void Ios_MissingBundleId_IsUnparseable()
        {
            string noBundle =
                "<?xml version=\"1.0\"?><plist version=\"1.0\"><dict>" +
                "<key>CLIENT_ID</key><string>abc</string></dict></plist>";
            Assert.AreEqual(FirebaseConfigMatchResult.Unparseable,
                FirebaseConfigMatch.MatchIos(noBundle, AppId, out _));
        }

        [Test]
        public void Ios_BomAndWhitespaceInValue_IsMatch()
        {
            string plist =
                "\uFEFF<?xml version=\"1.0\" encoding=\"UTF-8\"?><plist version=\"1.0\"><dict>" +
                $"<key>BUNDLE_ID</key><string>  {AppId}  </string></dict></plist>";
            Assert.AreEqual(FirebaseConfigMatchResult.Match,
                FirebaseConfigMatch.MatchIos(plist, AppId, out string found));
            Assert.AreEqual(AppId, found);
        }

        [Test]
        public void Ios_SameIdDifferentProject_IsMatch_DocumentedResidual()
        {
            Assert.AreEqual(FirebaseConfigMatchResult.Match,
                FirebaseConfigMatch.MatchIos(Plist(AppId, projectId: "a-DIFFERENT-project"), AppId, out _));
        }

        [Test]
        public void Ios_FedAJson_IsUnparseable()
        {
            Assert.AreEqual(FirebaseConfigMatchResult.Unparseable,
                FirebaseConfigMatch.MatchIos(AndroidJson(AppId), AppId, out _));
        }
    }
}
