using System;
using System.Collections.Generic;
using System.Text;
using Sorolla.Palette.Adapters;
using UnityEngine;

namespace Sorolla.Palette
{
    internal static partial class SorollaDiagnostics
    {
        internal static string BuildSummary()
        {
            var rows = new List<SorollaDiagnosticRow>(64);
            BuildRows(rows);

            var sb = new StringBuilder(4096);
            AppendReportHeader(sb, "Sorolla Palette Vitals", BuildReportDetail());

            string currentGroup = null;
            foreach (SorollaDiagnosticRow row in rows)
            {
                if (row.Group != currentGroup)
                {
                    currentGroup = row.Group;
                    sb.AppendLine($"[{currentGroup}]");
                }

                sb.AppendLine($"{SeverityLabel(row.Severity),-7} {row.Name}: {row.Detail}");
            }

            sb.AppendLine();
            sb.AppendLine("[Runtime Problems]");
            AppendRuntimeProblems(sb);

            sb.AppendLine();
            sb.AppendLine("[Recent Events]");
            AppendEventLog(sb);

            return sb.ToString();
        }

        internal static string BuildProblemsSummary()
        {
            var rows = new List<SorollaDiagnosticRow>(64);
            BuildRows(rows);

            var sb = new StringBuilder(2048);
            AppendReportHeader(sb, "Sorolla Vitals Problems", BuildReportDetail(rows));

            bool any = false;
            foreach (SorollaDiagnosticRow row in rows)
            {
                if (!NeedsAttention(row.Severity)) continue;

                any = true;
                sb.AppendLine($"{SeverityLabel(row.Severity),-7} [{row.Group}] {row.Name}: {row.Detail}");
            }

            if (!any)
                sb.AppendLine("No FAIL/WARN/WAIT diagnostics observed.");

            return sb.ToString();
        }

        internal static string BuildConsoleSummary()
        {
            var rows = new List<SorollaDiagnosticRow>(64);
            BuildRows(rows);

            var sb = new StringBuilder(2048);
            AppendReportHeader(sb, "Sorolla Vitals Console", BuildReportDetail(rows));
            sb.AppendLine("[Runtime Problems]");
            AppendRuntimeProblems(sb);
            sb.AppendLine();
            sb.AppendLine("[Events]");
            AppendEventLog(sb);
            return sb.ToString();
        }

        internal static string BuildHeaderContext()
        {
            SorollaConfig config = LoadConfig();
            Snapshot snapshot = CaptureSnapshot();
            bool fullMode = IsFullMode(config, snapshot);

            var sb = new StringBuilder(192);
            AppendContextPart(sb, "SDK " + Palette.SdkVersion);
            AppendContextPart(sb, ModeShortLabel(config, snapshot));
            AppendContextPart(sb, AdjustEnvironmentHeaderLabel(config, fullMode));
            AppendContextPart(sb, ConsentHeaderLabel(fullMode));
            if (Palette.VerboseLogging)
                AppendContextPart(sb, "Verbose logs");
            return sb.ToString();
        }

        internal static bool IsProblemSeverity(SorollaDiagnosticSeverity severity)
        {
            return severity == SorollaDiagnosticSeverity.Fail
                || severity == SorollaDiagnosticSeverity.Warning;
        }

        internal static bool NeedsAttention(SorollaDiagnosticSeverity severity)
        {
            return IsProblemSeverity(severity)
                || severity == SorollaDiagnosticSeverity.Waiting;
        }

        internal static bool DrivesHealth(SorollaDiagnosticRow row)
        {
            return row.Kind == SorollaDiagnosticKind.Required;
        }

        internal static string KindLabel(SorollaDiagnosticKind kind)
        {
            switch (kind)
            {
                case SorollaDiagnosticKind.Observed:
                    return "OBS";
                case SorollaDiagnosticKind.Context:
                    return "CTX";
                default:
                    return "REQ";
            }
        }

        static void AppendReportHeader(StringBuilder sb, string title, string buildDetail)
        {
            sb.AppendLine(title);
            sb.AppendLine($"App: {Application.identifier} {Application.version}");
            sb.AppendLine($"Platform: {Application.platform} | Unity: {Application.unityVersion}");
            sb.AppendLine(buildDetail);
            sb.AppendLine($"Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z");
            sb.AppendLine();
        }

        static string BuildReportDetail()
        {
            return $"Build: {(Debug.isDebugBuild ? "Development" : "Release")} | {BuildHeaderContext()}";
        }

        static string BuildReportDetail(List<SorollaDiagnosticRow> rows)
        {
            return $"Build: {(Debug.isDebugBuild ? "Development" : "Release")} | Mode: {FindDetail(rows, "Boot", "Palette mode")} | Env: {FindDetail(rows, "Config", "Adjust environment")}";
        }

        static string FindDetail(List<SorollaDiagnosticRow> rows, string group, string name)
        {
            foreach (SorollaDiagnosticRow row in rows)
            {
                if (row.Group == group && row.Name == name)
                    return row.Detail;
            }

            return "Unknown";
        }

        static void AppendEventLog(StringBuilder sb)
        {
            var events = new List<SorollaDiagnosticEventLogEntry>(MaxEventLogEntries);
            CopyEventLog(events);
            if (events.Count == 0)
            {
                sb.AppendLine("None observed");
            }
            else
            {
                for (int i = events.Count - 1; i >= 0; i--)
                {
                    SorollaDiagnosticEventLogEntry entry = events[i];
                    sb.AppendLine($"{FormatEventTime(entry.TimeSeconds),8} [{entry.Source}] {entry.Name} {entry.Payload}");
                }
            }
        }

        static void AppendRuntimeProblems(StringBuilder sb)
        {
            var problems = new List<SorollaRuntimeProblem>(MaxRuntimeProblemEntries);
            CopyRuntimeProblems(problems);
            if (problems.Count == 0)
            {
                sb.AppendLine("None observed");
                return;
            }

            for (int i = problems.Count - 1; i >= 0; i--)
            {
                SorollaRuntimeProblem problem = problems[i];
                sb.AppendLine($"{SeverityLabel(problem.Severity),-7} {FormatEventTime(problem.LastTimeSeconds),8} [{problem.Source}] {problem.Type} x{problem.Count}: {problem.Message}");
                sb.AppendLine($"        {problem.TopFrame}");
            }
        }

        internal static string SeverityLabel(SorollaDiagnosticSeverity severity) => severity switch
        {
            SorollaDiagnosticSeverity.Pass => "PASS",
            SorollaDiagnosticSeverity.Warning => "WARN",
            SorollaDiagnosticSeverity.Fail => "FAIL",
            SorollaDiagnosticSeverity.Waiting => "WAIT",
            _ => "INFO",
        };
    }
}
