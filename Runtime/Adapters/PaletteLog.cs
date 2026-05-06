using System;
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

        internal static event Action<string, LogType> MessageEmitted;

        internal static void Configure(bool verboseEnabled)
        {
            VerboseEnabled = verboseEnabled;
        }

        internal static void Vital(string message)
        {
            Emit(message, LogType.Log);
            Debug.Log(message);
        }

        internal static void Verbose(string message)
        {
            if (!VerboseEnabled) return;

            Emit(message, LogType.Log);
            Debug.Log(message);
        }

        internal static void Warning(string message)
        {
            Emit(message, LogType.Warning);
            Debug.LogWarning(message);
        }

        internal static void WarningOnce(string key, string message)
        {
            if (!s_onceKeys.Add($"w:{key}")) return;

            Emit(message, LogType.Warning);
            Debug.LogWarning(message);
        }

        internal static void Error(string message)
        {
            Emit(message, LogType.Error);
            Debug.LogError(message);
        }

        internal static string Present(string value) => string.IsNullOrEmpty(value) ? "missing" : "present";

        static void Emit(string message, LogType type)
        {
            try
            {
                MessageEmitted?.Invoke(message, type);
            }
            catch
            {
                // Diagnostics must never affect SDK behavior or app logging.
            }
        }
    }
}
