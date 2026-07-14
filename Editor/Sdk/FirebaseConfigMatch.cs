using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Sorolla.Palette.Editor
{
    internal enum FirebaseConfigMatchResult { Match, Mismatch, Unparseable }

    /// <summary>
    ///     Pure, Unity-free parsers + identity match for the Firebase config files. Deliberately free of
    ///     AssetDatabase / PlayerSettings coupling so it is unit-testable on raw strings: the caller
    ///     (<see cref="BuildValidator"/>'s Firebase config check) supplies the file contents and the active
    ///     application identifier, and maps the result to a validation severity.
    ///
    ///     Honest limit (review F9): matching the Android <c>package_name</c> / iOS <c>BUNDLE_ID</c> against
    ///     the active application id catches a wrong-GAME config file. It does NOT prove Firebase PROJECT
    ///     identity - a config with the correct bundle id but pointing at the wrong Firebase project still
    ///     reads as <see cref="FirebaseConfigMatchResult.Match"/>. The candidate canonical gate
    ///     <c>firebase.project_identity</c> stays open until an authoritative expected project id is defined.
    /// </summary>
    internal static class FirebaseConfigMatch
    {
        /// <summary>Android: does the google-services.json carry a client whose package_name is the active
        /// application id? Clients are read as a SET (a multi-app config is valid; one matching client passes).</summary>
        internal static FirebaseConfigMatchResult MatchAndroid(
            string googleServicesJson, string expectedApplicationId, out IReadOnlyCollection<string> foundPackageNames)
        {
            foundPackageNames = Array.Empty<string>();
            if (!TryParseAndroidPackageNames(googleServicesJson, out HashSet<string> names))
                return FirebaseConfigMatchResult.Unparseable;

            foundPackageNames = names;
            return names.Contains(expectedApplicationId)
                ? FirebaseConfigMatchResult.Match
                : FirebaseConfigMatchResult.Mismatch;
        }

        /// <summary>iOS: does the GoogleService-Info.plist's BUNDLE_ID equal the active bundle id?</summary>
        internal static FirebaseConfigMatchResult MatchIos(
            string plistXml, string expectedBundleId, out string foundBundleId)
        {
            foundBundleId = null;
            if (!TryParseIosBundleId(plistXml, out string bundleId))
                return FirebaseConfigMatchResult.Unparseable;

            foundBundleId = bundleId;
            return bundleId == expectedBundleId
                ? FirebaseConfigMatchResult.Match
                : FirebaseConfigMatchResult.Mismatch;
        }

        /// <summary>Extracts every client's <c>package_name</c> as a set. Returns false (→ Unparseable) only on
        /// a genuine structural failure (not JSON, non-object root, or no <c>client</c> array); an entry missing
        /// a package_name is skipped, so a valid-but-empty set is a real "no client matches" (→ Mismatch).</summary>
        internal static bool TryParseAndroidPackageNames(string json, out HashSet<string> packageNames)
        {
            packageNames = new HashSet<string>();
            if (string.IsNullOrWhiteSpace(json))
                return false;

            if (!(MiniJson.Deserialize(StripBom(json)) is Dictionary<string, object> root) ||
                !root.TryGetValue("client", out object clientObj) ||
                !(clientObj is List<object> clients))
                return false;

            foreach (object c in clients)
            {
                if (c is Dictionary<string, object> client &&
                    client.TryGetValue("client_info", out object ciObj) && ciObj is Dictionary<string, object> ci &&
                    ci.TryGetValue("android_client_info", out object aciObj) && aciObj is Dictionary<string, object> aci &&
                    aci.TryGetValue("package_name", out object pkgObj) && pkgObj is string pkg &&
                    !string.IsNullOrWhiteSpace(pkg))
                    packageNames.Add(pkg.Trim());
            }
            return true;
        }

        /// <summary>Reads the <c>BUNDLE_ID</c> string from a GoogleService-Info.plist. DTD processing is
        /// disabled so Apple's plist DOCTYPE never triggers a network fetch. Returns false (→ Unparseable) on
        /// malformed XML or an absent BUNDLE_ID (a valid GoogleService-Info.plist always carries one).</summary>
        internal static bool TryParseIosBundleId(string plistXml, out string bundleId)
        {
            bundleId = null;
            if (string.IsNullOrWhiteSpace(plistXml))
                return false;

            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
                using var reader = XmlReader.Create(new StringReader(StripBom(plistXml)), settings);
                XElement dict = XDocument.Load(reader).Descendants("dict").FirstOrDefault();
                if (dict == null)
                    return false;

                // In a plist dict, <key>NAME</key> is immediately followed by its value element.
                List<XElement> elements = dict.Elements().ToList();
                for (int i = 0; i < elements.Count - 1; i++)
                {
                    if (elements[i].Name.LocalName == "key" && elements[i].Value.Trim() == "BUNDLE_ID" &&
                        elements[i + 1].Name.LocalName == "string")
                    {
                        bundleId = elements[i + 1].Value.Trim();
                        return !string.IsNullOrEmpty(bundleId);
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        static string StripBom(string s) =>
            !string.IsNullOrEmpty(s) && s[0] == '\uFEFF' ? s.Substring(1) : s;
    }
}
