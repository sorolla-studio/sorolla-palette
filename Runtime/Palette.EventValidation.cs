using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sorolla.Palette.Adapters;

namespace Sorolla.Palette
{
    public static partial class Palette
    {
        // Name/param sanitization rules and limits live in the shared
        // EventNameSanitizer (Sorolla.Palette.Adapters) so the Palette validation
        // gate and the adapter implementations cannot drift apart.

        /// <summary>
        ///     Validate and sanitize an event name and its parameters.
        ///     Returns false if the event should be rejected entirely.
        /// </summary>
        static bool ValidateEvent(ref string eventName, Dictionary<string, object> parameters)
        {
            string originalName = eventName;
            eventName = EventNameSanitizer.SanitizeEventName(eventName);
            if (eventName == null)
            {
                PaletteLog.Error($"{Tag} Event rejected: '{originalName}' is empty or invalid after sanitization. Use lowercase letters, digits, and underscores (max {EventNameSanitizer.MaxEventNameLength} chars).");
                return false;
            }

            foreach (var prefix in EventNameSanitizer.ReservedPrefixes)
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
            if (parameters.Count > EventNameSanitizer.MaxParamsPerEvent)
            {
                PaletteLog.Error($"{Tag} Event rejected: {parameters.Count} params exceeds max {EventNameSanitizer.MaxParamsPerEvent}. Remove {parameters.Count - EventNameSanitizer.MaxParamsPerEvent} param(s).");
                return false;
            }

            foreach (var kvp in parameters)
            {
                var sanitizedKey = EventNameSanitizer.SanitizeParamName(kvp.Key);
                if (sanitizedKey == null)
                {
                    PaletteLog.Error($"{Tag} Event rejected: param name '{kvp.Key}' is invalid after sanitization. Use lowercase letters, digits, and underscores (max {EventNameSanitizer.MaxParamNameLength} chars).");
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
