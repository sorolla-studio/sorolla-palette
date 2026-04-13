namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     Shared GA4-compatible name sanitization for event names and parameter names.
    ///     Used by Palette (validation gate) and adapter implementations (defensive).
    /// </summary>
    internal static class EventNameSanitizer
    {
        internal const int MaxEventNameLength = 40;
        internal const int MaxParamNameLength = 40;
        internal const int MaxParamsPerEvent = 25;

        internal static readonly string[] ReservedPrefixes = { "firebase_", "google_", "ga_" };

        /// <summary>
        ///     Sanitize a GA4 event name: replace separators with underscores,
        ///     strip invalid chars, ensure starts with a letter, enforce max length.
        ///     Returns null if the result is empty.
        /// </summary>
        internal static string SanitizeEventName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var sanitized = name.Replace(":", "_").Replace("-", "_").Replace(" ", "_").Replace(".", "_");

            var result = new System.Text.StringBuilder();
            foreach (var c in sanitized)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    result.Append(c);
            }

            if (result.Length > 0 && !char.IsLetter(result[0]))
                result.Insert(0, 'e');

            if (result.Length > MaxEventNameLength)
                return result.ToString(0, MaxEventNameLength);

            return result.Length > 0 ? result.ToString() : null;
        }

        /// <summary>
        ///     Sanitize a GA4 parameter name: same rules as event name but no letter-start requirement.
        ///     Returns null if the result is empty.
        /// </summary>
        internal static string SanitizeParamName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var sanitized = name.Replace(":", "_").Replace("-", "_").Replace(" ", "_").Replace(".", "_");

            var result = new System.Text.StringBuilder();
            foreach (var c in sanitized)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    result.Append(c);
            }

            if (result.Length > MaxParamNameLength)
                return result.ToString(0, MaxParamNameLength);

            return result.Length > 0 ? result.ToString() : null;
        }
    }
}
