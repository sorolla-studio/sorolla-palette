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

        /// <summary>
        ///     Plain-text report of the full QA bridge snapshot (<see cref="SorollaQaState"/>, the same
        ///     data <c>/qa/snapshot</c> serves), for the console's "Copy SDK state" action. Studios paste
        ///     this to Sorolla when they can't self-fix a red row. Must run on the Unity main thread
        ///     (delegates to <see cref="CaptureQaState"/>).
        /// </summary>
        internal static string BuildQaStateSummary()
        {
            SorollaQaState state = CaptureQaState();

            var sb = new StringBuilder(2048);
            AppendReportHeader(sb, "Sorolla SDK State", $"Mode: {state.Mode} | Build: {(state.DevelopmentBuild ? "Development" : "Release")}", state.DeviceWallClock);

            sb.AppendLine("[Header]");
            sb.AppendLine($"sdk: {state.SdkVersion}");
            sb.AppendLine($"armed: {state.BridgeArmed}");
            sb.AppendLine($"ready: {state.Ready}");
            sb.AppendLine($"device_wall_clock: {state.DeviceWallClock}");
            sb.AppendLine();

            sb.AppendLine("[Consent]");
            sb.AppendLine($"status: {state.ConsentStatus} | geography: {state.ConsentGeography} | att: {state.Att}");
            sb.AppendLine($"can_request_ads: {state.CanRequestAds} | form_shown_this_session: {state.ConsentFormShownThisSession}");
            sb.AppendLine($"signals known: {state.ConsentSignalsKnown} | ad_storage: {state.AdStorageConsent} | ad_personalization: {state.AdPersonalizationConsent} | ad_user_data: {state.AdUserDataConsent} | analytics_storage: {state.AnalyticsStorageConsent}");
            sb.AppendLine($"iabtcf tc_string_present: {state.TcStringPresent}");
            sb.AppendLine();

            sb.AppendLine("[Remote Config]");
            sb.AppendLine($"status: {state.RemoteConfigStatus} | fetch_seen: {state.RemoteConfigFetchSeen} | fetch_success: {state.RemoteConfigFetchSuccess}");
            if (state.RemoteConfigValues != null)
            {
                foreach (SorollaQaRcValue value in state.RemoteConfigValues)
                    sb.AppendLine($"  {value.Key} = {value.Value} ({value.Source})");
            }
            sb.AppendLine();

            sb.AppendLine("[Adapters]");
            sb.AppendLine($"max: {state.MaxAdapter}");
            sb.AppendLine($"adjust: {state.AdjustAdapter}");
            sb.AppendLine($"firebase: {state.FirebaseAdapter}");
            sb.AppendLine($"gameanalytics: {state.GameAnalyticsAdapter}");
            sb.AppendLine($"facebook: {state.FacebookAdapter}");
            sb.AppendLine($"crashlytics_ready: {state.CrashlyticsReady} | crashlytics_outcome: {state.CrashlyticsOutcome}");
            sb.AppendLine();

            sb.AppendLine("[Identity]");
            sb.AppendLine($"advertising_id_present: {state.AdvertisingIdPresent} | advertising_id_zeroed: {state.AdvertisingIdZeroed}");
            sb.AppendLine($"adjust_adid_present: {state.AdjustAdidPresent} | adjust_environment: {state.AdjustEnvironment}");
            sb.AppendLine($"attribution_network: {state.AttributionNetwork}");
            sb.AppendLine($"fb_att_enabled: {state.FacebookAttEnabled} | fb_att_applied: {state.FacebookAttApplied}");
            sb.AppendLine();

            sb.AppendLine("[Ads]");
            sb.AppendLine($"interstitial: loaded={state.InterstitialLoaded}, completed={state.InterstitialCompleted}");
            sb.AppendLine($"rewarded: loaded={state.RewardedLoaded}, completed={state.RewardedCompleted}");
            sb.AppendLine($"revenue_seen: {state.AdRevenueSeen}");
            sb.AppendLine();

            sb.AppendLine("[IAP]");
            sb.AppendLine($"tracking_attached: {state.IapTrackingAttached} | purchase_count: {state.IapPurchaseCount} | duplicate_count: {state.IapDuplicateCount}");
            sb.AppendLine($"verification: {state.IapVerification} | last_issue: {state.IapLastIssue}");
            sb.AppendLine();

            sb.AppendLine("[Events]");
            if (state.Events == null || state.Events.Length == 0)
            {
                sb.AppendLine("None observed");
            }
            else
            {
                foreach (SorollaQaEvent evt in state.Events)
                    sb.AppendLine($"{evt.Name} x{evt.Count}");
            }
            sb.AppendLine();

            sb.AppendLine("[Problems]");
            sb.AppendLine($"sdk_warnings: {state.SdkWarningCount} | sdk_errors: {state.SdkErrorCount} | last_sdk_error: {state.LastSdkError}");
            sb.AppendLine($"runtime_unique: {state.RuntimeProblemUniqueCount} | runtime_total: {state.RuntimeProblemTotalCount} | runtime_top: {state.RuntimeProblemSummary}");

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

        static void AppendReportHeader(StringBuilder sb, string title, string buildDetail, string deviceWallClock = null)
        {
            sb.AppendLine(title);
            sb.AppendLine($"App: {Application.identifier} {Application.version}");
            sb.AppendLine($"Platform: {Application.platform} | Unity: {Application.unityVersion}");
            sb.AppendLine(buildDetail);
            if (!string.IsNullOrEmpty(deviceWallClock))
                sb.AppendLine($"Device time: {deviceWallClock}");
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
