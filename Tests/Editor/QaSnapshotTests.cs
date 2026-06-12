using System;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     Exercises the QA bridge snapshot serializer as a pure function over a constructed
    ///     <c>SorollaQaState</c> (internal; reached by reflection, like the other diagnostics tests).
    /// </summary>
    [TestFixture]
    public class QaSnapshotTests
    {
        [Test]
        public void WriteJson_KnownState_SerializesEverySection()
        {
            object state = NewState();
            Set(state, "SdkVersion", "3.17.1");
            Set(state, "Mode", "full");
            Set(state, "DevelopmentBuild", true);
            Set(state, "BridgeArmed", true);
            Set(state, "Ready", true);
            Set(state, "ConsentStatus", "Obtained");
            Set(state, "ConsentGeography", "gdpr");
            Set(state, "Att", "authorized");
            Set(state, "CanRequestAds", true);
            Set(state, "ConsentSignalsKnown", true);
            Set(state, "AdStorageConsent", true);
            Set(state, "AdPersonalizationConsent", false);
            Set(state, "AdUserDataConsent", false);
            Set(state, "AnalyticsStorageConsent", true);
            Set(state, "TcStringPresent", true);
            Set(state, "PurposeConsents", "111");
            Set(state, "MaxAdapter", "ready");
            Set(state, "AdjustAdapter", "enabled(production)");
            Set(state, "FirebaseAdapter", "ready");
            Set(state, "GameAnalyticsAdapter", "ready");
            Set(state, "FacebookAdapter", "ready");
            Set(state, "AdvertisingIdPresent", true);
            Set(state, "AttributionNetwork", "Organic");
            Set(state, "AdjustEnvironment", "Production");
            Set(state, "RewardedCompleted", true);

            string json = Serialize(state);

            // Header
            Assert.That(json, Does.StartWith("{"));
            Assert.That(json, Does.EndWith("}"));
            Assert.That(json, Does.Contain("\"sdk\":\"3.17.1\""));
            Assert.That(json, Does.Contain("\"mode\":\"full\""));
            Assert.That(json, Does.Contain("\"armed\":true"));
            // Consent + resolved signals (mixed granted/denied)
            Assert.That(json, Does.Contain("\"consent\":{"));
            Assert.That(json, Does.Contain("\"geography\":\"gdpr\""));
            Assert.That(json, Does.Contain("\"analytics_storage\":\"granted\""));
            Assert.That(json, Does.Contain("\"ad_personalization\":\"denied\""));
            Assert.That(json, Does.Contain("\"tc_string_present\":true"));
            Assert.That(json, Does.Contain("\"purpose_consents\":\"111\""));
            // Adapters, identity, ads
            Assert.That(json, Does.Contain("\"adapters\":{"));
            Assert.That(json, Does.Contain("\"adjust\":\"enabled(production)\""));
            Assert.That(json, Does.Contain("\"advertising_id_present\":true"));
            Assert.That(json, Does.Contain("\"rewarded\":{"));
            Assert.That(json, Does.Contain("\"completed\":true"));

            AssertBalanced(json);
        }

        [Test]
        public void WriteJson_UnknownConsentSignals_ReportsUnknown()
        {
            object state = NewState();
            Set(state, "ConsentSignalsKnown", false);

            string json = Serialize(state);

            Assert.That(json, Does.Contain("\"analytics_storage\":\"unknown\""));
            Assert.That(json, Does.Contain("\"ad_storage\":\"unknown\""));
            Assert.That(json, Does.Contain("\"ad_user_data\":\"unknown\""));
        }

        [Test]
        public void WriteJson_NullStrings_EmitJsonNullWithoutBreakingShape()
        {
            object state = NewState(); // all reference fields default to null

            string json = Serialize(state);

            Assert.That(json, Does.Contain("\"sdk\":null"));
            AssertBalanced(json);
        }

        static string Serialize(object state)
        {
            Type snapshotType = RequiredType("Sorolla.Palette.QaSnapshot");
            MethodInfo writeJson = snapshotType.GetMethod("WriteJson", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(writeJson, "QaSnapshot.WriteJson should exist.");

            var sb = new StringBuilder();
            writeJson.Invoke(null, new[] { state, sb });
            return sb.ToString();
        }

        static object NewState()
        {
            Type stateType = RequiredType("Sorolla.Palette.SorollaQaState");
            return Activator.CreateInstance(stateType);
        }

        static void Set(object state, string field, object value)
        {
            FieldInfo info = state.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(info, $"SorollaQaState.{field} should exist.");
            info.SetValue(state, value);
        }

        // Cheap structural sanity check: braces and quotes balance (no DSL/parser dependency in tests).
        static void AssertBalanced(string json)
        {
            int depth = 0;
            int quotes = 0;
            bool inString = false;
            bool escaped = false;
            foreach (char c in json)
            {
                if (inString)
                {
                    if (escaped) { escaped = false; continue; }
                    if (c == '\\') { escaped = true; continue; }
                    if (c == '"') { inString = false; quotes++; }
                    continue;
                }

                if (c == '"') { inString = true; quotes++; }
                else if (c == '{') depth++;
                else if (c == '}') depth--;

                Assert.That(depth, Is.GreaterThanOrEqualTo(0), "Unbalanced closing brace.");
            }

            Assert.That(depth, Is.EqualTo(0), "Unbalanced JSON braces.");
            Assert.That(quotes % 2, Is.EqualTo(0), "Unbalanced JSON quotes.");
        }

        static Type RequiredType(string typeName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName))
                .FirstOrDefault(found => found != null);

            Assert.NotNull(type, $"{typeName} should be loaded.");
            return type;
        }
    }
}
