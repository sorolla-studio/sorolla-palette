using System;
using System.Text;
using UnityEngine;

namespace Sorolla.Palette
{
    /// <summary>
    /// Pure classification of Unity/runtime log entries into structured runtime-problem fields
    /// (type, source, severity, fingerprint, formatted stack). Stateless: all storage, dedup, and
    /// lock discipline stay in <see cref="SorollaDiagnostics"/>.
    /// </summary>
    internal static class SorollaRuntimeProblemClassifier
    {
        internal static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int newline = text.IndexOfAny(new[] { '\n', '\r' });
            return newline < 0 ? text : text.Substring(0, newline);
        }

        internal static string RuntimeProblemType(string message, LogType type, bool isNullReference, bool isFatal)
        {
            if (isFatal) return "Fatal";
            if (isNullReference) return "NullReferenceException";
            string firstLine = FirstLine(message).Trim();
            int colon = firstLine.IndexOf(':');
            string candidate = colon > 0 ? firstLine.Substring(0, colon).Trim() : firstLine;
            if (candidate.EndsWith("Exception", StringComparison.Ordinal) && candidate.Length <= 80)
                return candidate;
            return type.ToString();
        }

        internal static string RuntimeProblemSource(string message, string stackTrace)
        {
            string combined = (message ?? "") + "\n" + (stackTrace ?? "");
            if (combined.IndexOf("SorollaDiagnostics", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Vitals";
            if (combined.IndexOf("Sorolla.Palette", StringComparison.OrdinalIgnoreCase) >= 0
                || combined.IndexOf("[Palette", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Sorolla SDK";
            if (combined.IndexOf("MaxSdk", StringComparison.OrdinalIgnoreCase) >= 0
                || combined.IndexOf("AppLovin", StringComparison.OrdinalIgnoreCase) >= 0
                || combined.IndexOf("Adjust", StringComparison.OrdinalIgnoreCase) >= 0
                || combined.IndexOf("Firebase", StringComparison.OrdinalIgnoreCase) >= 0
                || combined.IndexOf("Facebook", StringComparison.OrdinalIgnoreCase) >= 0
                || combined.IndexOf("GameAnalytics", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Vendor SDK";
            if (combined.IndexOf("Assembly-CSharp", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Game";
            return "Unity/System";
        }

        internal static string RuntimeProblemTopFrame(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace)) return "No stack trace";

            string[] lines = stackTrace.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (IsUsefulRuntimeFrame(line))
                    return Truncate(line, 140);
            }

            return Truncate(lines[0].Trim(), 140);
        }

        internal static string RuntimeProblemFingerprint(string type, string message, string topFrame)
        {
            return $"{type}|{message}|{topFrame}";
        }

        internal static string FormatStackTrace(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace)) return "No stack trace";

            string[] lines = stackTrace.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder(768);
            int written = 0;
            for (int i = 0; i < lines.Length && written < 12; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (written > 0) sb.AppendLine();
                sb.Append(Truncate(line, 160));
                written++;
            }

            return written == 0 ? "No stack trace" : sb.ToString();
        }

        internal static SorollaDiagnosticSeverity RuntimeProblemSeverity(string type, string source, bool isNullReference, bool isFatal,
            int count)
        {
            if (isFatal || isNullReference || source == "Sorolla SDK" || source == "Vitals" || count >= 3)
                return SorollaDiagnosticSeverity.Fail;
            return SorollaDiagnosticSeverity.Warning;
        }

        internal static string RuntimeProblemSummary(SorollaRuntimeProblem problem)
        {
            return $"{problem.Source}: {problem.Type} x{problem.Count} at {problem.TopFrame}";
        }

        static bool IsUsefulRuntimeFrame(string frame)
        {
            if (string.IsNullOrEmpty(frame)) return false;
            return frame.IndexOf("UnityEngine.", StringComparison.Ordinal) < 0
                && frame.IndexOf("UnityEditor.", StringComparison.Ordinal) < 0
                && frame.IndexOf("System.", StringComparison.Ordinal) < 0
                && frame.IndexOf("Application.CallLogCallback", StringComparison.Ordinal) < 0;
        }

        static string Truncate(string message, int maxLength)
        {
            if (string.IsNullOrEmpty(message)) return "";
            string singleLine = message.Replace('\n', ' ').Replace('\r', ' ');
            return singleLine.Length <= maxLength ? singleLine : singleLine.Substring(0, maxLength - 1) + "...";
        }
    }
}
