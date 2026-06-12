using System;
using System.Collections;
using System.Collections.Generic;
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
            // Null Events array serializes as an empty list; iap object always present.
            Assert.That(json, Does.Contain("\"events\":[]"));
            Assert.That(json, Does.Contain("\"iap\":{"));
            Assert.That(json, Does.Contain("\"tracking_attached\":false"));
            AssertBalanced(json);
        }

        [Test]
        public void EventAggregation_CountsRepeatsAndKeepsLastParams()
        {
            Type diag = RequiredType("Sorolla.Palette.SorollaDiagnostics");
            InvokeStatic(diag, "ClearEventLog");

            RecordEvent(diag, "vitals", "level_start", new Dictionary<string, object> { { "level", "1" } });
            RecordEvent(diag, "vitals", "level_start", new Dictionary<string, object> { { "level", "2" } });
            RecordEvent(diag, "vitals", "earn_virtual_currency", null);

            IList aggregates = CopyAggregates(diag);

            object levelStart = aggregates.Cast<object>().FirstOrDefault(a => Field<string>(a, "Name") == "level_start");
            object earn = aggregates.Cast<object>().FirstOrDefault(a => Field<string>(a, "Name") == "earn_virtual_currency");

            Assert.NotNull(levelStart, "level_start should be aggregated.");
            Assert.NotNull(earn, "earn_virtual_currency should be aggregated.");
            Assert.That(Field<int>(levelStart, "Count"), Is.EqualTo(2), "Repeated event name should accumulate.");
            Assert.That(Field<int>(earn, "Count"), Is.EqualTo(1));

            // Last params reflect the MOST RECENT dispatch (level=2), not the first.
            var lastParams = (Array)levelStart.GetType().GetField("LastParams").GetValue(levelStart);
            string levelValue = null;
            foreach (object line in lastParams)
            {
                if (Field<string>(line, "Key") == "level")
                    levelValue = Field<string>(line, "Value");
            }
            Assert.That(levelValue, Is.EqualTo("2"), "Aggregate should keep the last dispatch's params.");
        }

        [Test]
        public void HealthCounters_ExcludeSelfAndTaggedCustomEvents()
        {
            Type diag = RequiredType("Sorolla.Palette.SorollaDiagnostics");
            int before = StaticInt(diag, "s_customEventCount");

            RecordCustom(diag, "consent_resolved", null);
            Assert.That(StaticInt(diag, "s_customEventCount"), Is.EqualTo(before),
                "SDK-self event must not drive the custom-event health counter (DR-60).");

            RecordCustom(diag, "game_event", new Dictionary<string, object> { { "sorolla_qa_test", true } });
            Assert.That(StaticInt(diag, "s_customEventCount"), Is.EqualTo(before),
                "Tagged QA test event must not drive the health counter (DR-33).");

            RecordCustom(diag, "real_game_event", null);
            Assert.That(StaticInt(diag, "s_customEventCount"), Is.EqualTo(before + 1),
                "A normal game custom event should still count.");
        }

        [Test]
        public void HealthCounters_TestActionScopeExcludesProgression()
        {
            Type diag = RequiredType("Sorolla.Palette.SorollaDiagnostics");
            int before = StaticInt(diag, "s_progressionStartCount");

            InvokeStatic(diag, "BeginTestAction");
            InvokeStatic(diag, "RecordProgression", "start");
            InvokeStatic(diag, "EndTestAction");
            Assert.That(StaticInt(diag, "s_progressionStartCount"), Is.EqualTo(before),
                "Progression fired inside a test-action scope must be excluded.");

            InvokeStatic(diag, "RecordProgression", "start");
            Assert.That(StaticInt(diag, "s_progressionStartCount"), Is.EqualTo(before + 1),
                "Progression outside the scope should count normally.");
        }

        static void RecordCustom(Type diag, string name, IDictionary<string, object> parameters)
        {
            MethodInfo method = diag.GetMethod("RecordCustomEvent", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method, "SorollaDiagnostics.RecordCustomEvent should exist.");
            method.Invoke(null, new object[] { name, parameters });
        }

        static int StaticInt(Type type, string fieldName)
        {
            FieldInfo info = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(info, $"{type.Name}.{fieldName} should exist.");
            return (int)info.GetValue(null);
        }

        static void RecordEvent(Type diag, string source, string name, IDictionary<string, object> parameters)
        {
            MethodInfo record = diag.GetMethod("RecordEventDispatch", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(record, "SorollaDiagnostics.RecordEventDispatch should exist.");
            record.Invoke(null, new object[] { source, name, parameters });
        }

        static IList CopyAggregates(Type diag)
        {
            Type eventType = RequiredType("Sorolla.Palette.SorollaQaEvent");
            Type listType = typeof(List<>).MakeGenericType(eventType);
            var list = (IList)Activator.CreateInstance(listType);
            MethodInfo copy = diag.GetMethod("CopyEventAggregates", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(copy, "SorollaDiagnostics.CopyEventAggregates should exist.");
            copy.Invoke(null, new object[] { list });
            return list;
        }

        static void InvokeStatic(Type type, string methodName, params object[] args)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method, $"{type.Name}.{methodName} should exist.");
            method.Invoke(null, args);
        }

        static T Field<T>(object instance, string fieldName)
        {
            FieldInfo info = instance.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(info, $"{instance.GetType().Name}.{fieldName} should exist.");
            return (T)info.GetValue(instance);
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
