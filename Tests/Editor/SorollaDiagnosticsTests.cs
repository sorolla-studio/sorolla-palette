using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Sorolla.Palette.Editor.Tests
{
    [TestFixture]
    public class SorollaDiagnosticsTests
    {
        [Test]
        public void MaxAdRevenueTracked_ReachesDiagnosticsEventLog()
        {
            Type diagnosticsType = RequiredType("Sorolla.Palette.SorollaDiagnostics");
            Type maxAdapterType = RequiredType("Sorolla.Palette.Adapters.MaxAdapter");
            Type revenueInfoType = RequiredType("Sorolla.Palette.Adapters.MaxAdRevenueInfo");

            InvokeStatic(diagnosticsType, "ClearEventLog");

            // Activator.CreateInstance does not fill optional ctor params, so pass all seven
            // (adUnitIdentifier/placement were added to MaxAdRevenueInfo after this test).
            object info = Activator.CreateInstance(revenueInfoType,
                "unit_test_network", 0.42d, "USD", "rewarded", "exact", "unit_test_ad_unit", null);
            InvokeStatic(maxAdapterType, "RecordAdRevenue", info);

            IList events = CopyDiagnosticsEvents(diagnosticsType);

            object adRevenueEvent = events.Cast<object>()
                .FirstOrDefault(entry =>
                    Field<string>(entry, "Source") == "ads" &&
                    Field<string>(entry, "Name") == "ad_revenue");

            Assert.NotNull(adRevenueEvent, "MAX ad revenue should be recorded in SorollaDiagnostics.");
            Assert.That(Field<string>(adRevenueEvent, "Payload"), Does.Contain("unit_test_network"));
            Assert.That(Field<string>(adRevenueEvent, "Payload"), Does.Contain("rewarded"));
            Assert.That(Field<string>(adRevenueEvent, "Payload"), Does.Contain("exact"));
        }

        static IList CopyDiagnosticsEvents(Type diagnosticsType)
        {
            Type eventType = RequiredType("Sorolla.Palette.SorollaDiagnosticEventLogEntry");
            Type listType = typeof(List<>).MakeGenericType(eventType);
            var events = (IList)Activator.CreateInstance(listType);
            InvokeStatic(diagnosticsType, "CopyEventLog", events);
            return events;
        }

        static Type RequiredType(string typeName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName))
                .FirstOrDefault(found => found != null);

            Assert.NotNull(type, $"{typeName} should be loaded.");
            return type;
        }

        static object InvokeStatic(Type type, string methodName, params object[] args)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method, $"{type.FullName}.{methodName} should exist.");
            return method.Invoke(null, args);
        }

        static T Field<T>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(field, $"{instance.GetType().FullName}.{fieldName} should exist.");
            return (T)field.GetValue(instance);
        }
    }
}
