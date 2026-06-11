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

            // GA4-reserved exact names are dropped server-side by Firebase, so reject here for ALL
            // vendors (DR-14). eventName is already lowercased by SanitizeEventName, matching the set.
            if (EventNameSanitizer.ReservedEventNames.Contains(eventName))
            {
                PaletteLog.Error($"{Tag} Event rejected: '{eventName}' is a GA4-reserved name and would be dropped by Firebase/GA4. Use a different name.");
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
        ///     Value forwarded as the GameAnalytics design-event value for a custom event.
        ///     Only the documented <c>value</c> key is read: GA design events carry a single numeric
        ///     value, and picking "the first numeric param" made it depend on Dictionary iteration
        ///     order, so the same event could send different values run to run (DR-15). To attach a
        ///     GA design value, include a numeric <c>value</c> param; otherwise no value is sent (0).
        /// </summary>
        static float ExtractDesignEventValue(Dictionary<string, object> parameters)
        {
            if (parameters == null) return 0f;
            if (!parameters.TryGetValue("value", out object v)) return 0f;
            return v switch
            {
                int i => i,
                long l => l,
                float f => f,
                double d => (float)d,
                _ => 0f,
            };
        }
    }
}
