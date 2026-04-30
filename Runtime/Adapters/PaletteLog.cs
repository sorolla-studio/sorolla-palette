using System.Collections.Generic;
using UnityEngine;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     SDK-owned logging policy. Production-safe health markers, warnings, and errors
    ///     always remain visible; detailed diagnostics are gated by Palette verbose logging.
    /// </summary>
    internal static class PaletteLog
    {
        static readonly HashSet<string> s_onceKeys = new HashSet<string>();

        internal static bool VerboseEnabled { get; private set; }

        internal static void Configure(bool verboseEnabled)
        {
            VerboseEnabled = verboseEnabled;
        }

        internal static void Vital(string message) => Debug.Log(message);

        internal static void Verbose(string message)
        {
            if (VerboseEnabled) Debug.Log(message);
        }

        internal static void Warning(string message) => Debug.LogWarning(message);

        internal static void WarningOnce(string key, string message)
        {
            if (s_onceKeys.Add($"w:{key}")) Debug.LogWarning(message);
        }

        internal static void Error(string message) => Debug.LogError(message);

        internal static string Present(string value) => string.IsNullOrEmpty(value) ? "missing" : "present";
    }
}
