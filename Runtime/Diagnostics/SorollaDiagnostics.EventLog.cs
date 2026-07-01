using System;
using System.Collections.Generic;
using System.Text;
using Sorolla.Palette.Adapters;
using UnityEngine;

namespace Sorolla.Palette
{
    internal static partial class SorollaDiagnostics
    {
        static void EnqueueEvent(string source, string eventName, IDictionary<string, object> parameters)
        {
            if (s_eventLog.Count >= MaxEventLogEntries)
                s_eventLog.Dequeue();

            string name = string.IsNullOrEmpty(eventName) ? "unnamed" : eventName;
            SorollaDiagnosticPayloadLine[] payloadLines = BuildPayloadLines(parameters);
            s_eventLog.Enqueue(new SorollaDiagnosticEventLogEntry(
                unchecked(++s_nextEventId),
                Time.realtimeSinceStartup,
                string.IsNullOrEmpty(source) ? "event" : source,
                name,
                FormatPayload(payloadLines),
                payloadLines));

            UpdateEventAggregate(name, payloadLines);
        }

        static void UpdateEventAggregate(string name, SorollaDiagnosticPayloadLine[] payloadLines)
        {
            if (s_eventAggregates.TryGetValue(name, out SorollaEventAggregate aggregate))
            {
                aggregate.Count++;
                aggregate.LastParams = payloadLines;
                return;
            }

            // Bounded: once the distinct-name cap is hit, stop adding new names. The recency ring still
            // carries the newest events, so a runaway custom-event name space can't grow this unboundedly.
            if (s_eventAggregates.Count >= MaxEventAggregates) return;
            s_eventAggregates.Add(name, new SorollaEventAggregate { Count = 1, LastParams = payloadLines });
        }

        internal static void CopyEventAggregates(List<SorollaQaEvent> target)
        {
            target.Clear();
            lock (s_lock)
            {
                foreach (KeyValuePair<string, SorollaEventAggregate> pair in s_eventAggregates)
                    target.Add(new SorollaQaEvent
                    {
                        Name = pair.Key,
                        Count = pair.Value.Count,
                        LastParams = pair.Value.LastParams,
                    });
            }
        }

        static SorollaDiagnosticPayloadLine[] BuildPayloadLines(IDictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return Array.Empty<SorollaDiagnosticPayloadLine>();

            var lines = new SorollaDiagnosticPayloadLine[parameters.Count];
            int index = 0;
            foreach (KeyValuePair<string, object> item in parameters)
            {
                string key = string.IsNullOrEmpty(item.Key) ? "unnamed" : item.Key;
                lines[index] = new SorollaDiagnosticPayloadLine(key, FormatPayloadValue(key, item.Value));
                index++;
            }
            return lines;
        }

        static string FormatPayload(SorollaDiagnosticPayloadLine[] lines)
        {
            if (lines == null || lines.Length == 0)
                return "{}";

            var sb = new StringBuilder(256);
            sb.Append('{');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(lines[i].Key);
                sb.Append('=');
                sb.Append(lines[i].Value);
            }
            sb.Append('}');
            return SafeSingleLine(sb.ToString(), 320);
        }

        static string FormatPayloadValue(string key, object value)
        {
            if (IsSensitivePayloadKey(key))
                return value == null ? "missing" : "present";
            if (value == null)
                return "null";
            if (value is bool b)
                return b ? "true" : "false";
            if (value is float f)
                return f.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            if (value is double d)
                return d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            if (value is decimal m)
                return m.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

            string text = value.ToString() ?? "";
            if (text.Length > 80)
                text = text.Substring(0, 79) + "...";
            return text.Replace('\n', ' ').Replace('\r', ' ');
        }

        static bool IsSensitivePayloadKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            string normalized = key.ToLowerInvariant();
            string compact = normalized.Replace("_", "").Replace("-", "");
            return normalized.Contains("token")
                || normalized.Contains("secret")
                || normalized.Contains("receipt")
                || compact.Contains("transactionid")
                || compact.Contains("purchasetoken")
                || normalized.Contains("tcf");
        }

        internal static string FormatEventTime(float timeSeconds)
        {
            if (timeSeconds < 0f) timeSeconds = 0f;

            int totalSeconds = Mathf.FloorToInt(timeSeconds);
            int hours = totalSeconds / 3600;
            int minutes = totalSeconds / 60 % 60;
            int seconds = totalSeconds % 60;
            int tenths = Mathf.FloorToInt((timeSeconds - totalSeconds) * 10f);

            if (hours > 0)
                return $"{hours:0}:{minutes:00}:{seconds:00}";

            return $"{minutes:00}:{seconds:00}.{tenths:0}";
        }
    }
}
