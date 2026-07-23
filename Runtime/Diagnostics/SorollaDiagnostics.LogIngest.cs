using System;
using UnityEngine;

namespace Sorolla.Palette
{
    internal static partial class SorollaDiagnostics
    {
        static void RecordPaletteLog(string message, LogType type)
        {
            if (string.IsNullOrEmpty(message)) return;

            lock (s_lock)
            {
                if (type == LogType.Warning)
                {
                    s_paletteWarningCount++;
                    s_lastPaletteWarning = SafeDetail(message);
                }
                else if (type == LogType.Error || type == LogType.Exception)
                {
                    s_paletteErrorCount++;
                    s_lastPaletteError = SafeDetail(message);
                }
            }
        }

        static void RecordUnityLog(string message, string stackTrace, LogType type)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (message.StartsWith("[Palette", StringComparison.Ordinal)) return;

            bool isException = type == LogType.Exception || type == LogType.Error;
            bool isNullReference = message.IndexOf("NullReferenceException", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isFatal = message.IndexOf("FATAL EXCEPTION", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("F AndroidRuntime", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isException && !isNullReference && !isFatal) return;

            lock (s_lock)
            {
                RecordRuntimeProblem(message, stackTrace, type, isNullReference, isFatal);
            }
        }

        static void RecordRuntimeProblem(string message, string stackTrace, LogType type, bool isNullReference, bool isFatal)
        {
            float now = Time.realtimeSinceStartup;
            string safeMessage = SafeDetail(SorollaRuntimeProblemClassifier.FirstLine(message));
            string safeStack = SorollaRuntimeProblemClassifier.FormatStackTrace(stackTrace);
            string problemType = SorollaRuntimeProblemClassifier.RuntimeProblemType(message, type, isNullReference, isFatal);
            string source = SorollaRuntimeProblemClassifier.RuntimeProblemSource(message, stackTrace);
            string topFrame = SorollaRuntimeProblemClassifier.RuntimeProblemTopFrame(stackTrace);
            string fingerprint = SorollaRuntimeProblemClassifier.RuntimeProblemFingerprint(problemType, safeMessage, topFrame);

            for (int i = 0; i < s_runtimeProblems.Count; i++)
            {
                SorollaRuntimeProblem existing = s_runtimeProblems[i];
                if (existing.Fingerprint != fingerprint) continue;

                int nextCount = existing.Count + 1;
                SorollaDiagnosticSeverity severity = SorollaRuntimeProblemClassifier.RuntimeProblemSeverity(problemType, source, isNullReference, isFatal, nextCount);
                s_runtimeProblems[i] = existing.WithRepeat(now, severity);
                return;
            }

            if (s_runtimeProblems.Count >= MaxRuntimeProblemEntries)
                s_runtimeProblems.RemoveAt(0);

            SorollaDiagnosticSeverity initialSeverity = SorollaRuntimeProblemClassifier.RuntimeProblemSeverity(problemType, source, isNullReference, isFatal, 1);
            var problem = new SorollaRuntimeProblem(
                unchecked(++s_nextRuntimeProblemId),
                fingerprint,
                now,
                now,
                1,
                initialSeverity,
                source,
                problemType,
                safeMessage,
                topFrame,
                safeStack);
            s_runtimeProblems.Add(problem);
        }

    }
}
