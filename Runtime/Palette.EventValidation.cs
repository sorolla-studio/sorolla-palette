using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sorolla.Palette.Adapters;
using UnityEngine;

namespace Sorolla.Palette
{
    public static partial class Palette
    {
        const int MaxEventNameLength = 40;
        const int MaxParamNameLength = 40;
        const int MaxParamsPerEvent = 25;

        static readonly string[] s_reservedPrefixes = { "firebase_", "google_", "ga_" };

        /// <summary>
        ///     Validate and sanitize an event name and its parameters.
        ///     Returns false if the event should be rejected entirely.
        /// </summary>
        static bool ValidateEvent(ref string eventName, Dictionary<string, object> parameters)
        {
            string originalName = eventName;
            eventName = SanitizeEventName(eventName);
            if (eventName == null)
            {
                PaletteLog.Error($"{Tag} Event rejected: '{originalName}' is empty or invalid after sanitization. Use lowercase letters, digits, and underscores (max {MaxEventNameLength} chars).");
                return false;
            }

            foreach (var prefix in s_reservedPrefixes)
            {
                if (eventName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string suggested = eventName.Substring(prefix.Length);
                    PaletteLog.Error($"{Tag} Event rejected: '{eventName}' uses reserved prefix '{prefix}'. Remove the prefix (e.g. '{suggested}') or use a different name.");
                    return false;
                }
            }

            if (parameters != null && !ValidateParams(parameters))
                return false;

            return true;
        }

        /// <summary>
        ///     Validate parameter names, types, and count.
        ///     Returns false if the event should be rejected entirely.
        /// </summary>
        static bool ValidateParams(Dictionary<string, object> parameters)
        {
            if (parameters.Count > MaxParamsPerEvent)
            {
                PaletteLog.Error($"{Tag} Event rejected: {parameters.Count} params exceeds max {MaxParamsPerEvent}. Remove {parameters.Count - MaxParamsPerEvent} param(s).");
                return false;
            }

            foreach (var kvp in parameters)
            {
                var sanitizedKey = SanitizeParameterName(kvp.Key);
                if (sanitizedKey == null)
                {
                    PaletteLog.Error($"{Tag} Event rejected: param name '{kvp.Key}' is invalid after sanitization. Use lowercase letters, digits, and underscores (max {MaxParamNameLength} chars).");
                    return false;
                }

                if (!IsSupportedParamType(kvp.Value))
                {
                    string typeName = kvp.Value?.GetType().Name ?? "null";
                    PaletteLog.Error($"{Tag} Event rejected: param '{kvp.Key}' has unsupported type '{typeName}'. Convert to one of: string, int, long, float, double, bool, enum.");
                    return false;
                }
            }

            return true;
        }

        static string SanitizeEventName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var sanitized = name.Replace(":", "_").Replace("-", "_").Replace(" ", "_").Replace(".", "_");

            var result = new System.Text.StringBuilder();
            foreach (var c in sanitized)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    result.Append(char.ToLowerInvariant(c));
            }

            if (result.Length > 0 && !char.IsLetter(result[0]))
                result.Insert(0, 'e');

            if (result.Length > MaxEventNameLength)
                return result.ToString(0, MaxEventNameLength);

            return result.Length > 0 ? result.ToString() : null;
        }

        static string SanitizeParameterName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var sanitized = name.Replace(":", "_").Replace("-", "_").Replace(" ", "_").Replace(".", "_");

            var result = new System.Text.StringBuilder();
            foreach (var c in sanitized)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    result.Append(char.ToLowerInvariant(c));
            }

            if (result.Length > MaxParamNameLength)
                return result.ToString(0, MaxParamNameLength);

            return result.Length > 0 ? result.ToString() : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsSupportedParamType(object value) =>
            value is string or int or long or float or double or bool or Enum;

        /// <summary>
        ///     Extract the first numeric value from parameters for GA best-effort design event.
        /// </summary>
        static float ExtractFirstNumericValue(Dictionary<string, object> parameters)
        {
            if (parameters == null) return 0f;
            foreach (var kvp in parameters)
            {
                switch (kvp.Value)
                {
                    case int i: return i;
                    case long l: return l;
                    case float f: return f;
                    case double d: return (float)d;
                }
            }
            return 0f;
        }
    }
}
