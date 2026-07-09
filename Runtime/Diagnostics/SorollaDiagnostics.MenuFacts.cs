using System.Collections.Generic;
using UnityEngine;

namespace Sorolla.Palette
{
    // Facts for the UI Toolkit debug menu overlay (debug-menu overhaul, phase 2). Kept in the
    // diagnostics layer - the source of truth per the standing brief - never computed in the
    // overlay itself, which stays display-only. Reuses CaptureQaState() (the same struct the QA
    // bridge and "Copy SDK state" serve) instead of re-deriving consent/event facts, so the menu's
    // coverage line and the bridge snapshot cannot drift apart.
    internal static partial class SorollaDiagnostics
    {
        /// <summary>Header context line: SDK version - mode - platform - build type - bundle id (spec 2.1.3).</summary>
        internal static string BuildMenuContextLine()
        {
            SorollaConfig config = LoadConfig();
            Snapshot snapshot = CaptureSnapshot();
            string mode = ModeShortLabel(config, snapshot);
            string build = Debug.isDebugBuild ? "Dev" : "Release";
            return $"SDK {Palette.SdkVersion} · {mode} · {Application.platform} · {build} · {Application.identifier}";
        }

        /// <summary>
        ///     Header coverage line: what THIS session exercised (spec 2.1.4). "thin" mirrors the
        ///     design-tokens.md rule: consent unresolved or 0 events -> amber, honesty callout shown.
        ///     Session count is always reported as 1 - the SDK has no cross-launch session counter
        ///     today (verified: grep found none); a real counter is future SDK work, not a phase-2 gap.
        /// </summary>
        internal static string BuildMenuCoverageLine(out bool thin)
        {
            SorollaQaState state = CaptureQaState();

            string consent = MenuConsentCell(state, out bool consentUnresolved);
            int events = 0;
            if (state.Events != null)
            {
                foreach (SorollaQaEvent evt in state.Events)
                    events += evt.Count;
            }
            bool adsShown = state.InterstitialCompleted || state.RewardedCompleted || state.AdRevenueSeen;

            thin = consentUnresolved || events == 0;

            return $"consent {consent} · session 1 · {events} events · ads {(adsShown ? "shown" : "not shown")}";
        }

        static string MenuConsentCell(SorollaQaState state, out bool unresolved)
        {
            string geo = state.ConsentGeography switch
            {
                "gdpr" => "EU",
                "non_gdpr" => "non-EU",
                _ => "region unknown",
            };
            string status = state.ConsentStatus switch
            {
                "Obtained" => "accept",
                "Denied" => "deny",
                "NotApplicable" => "n/a",
                "Required" => "required",
                _ => "unresolved",
            };
            unresolved = state.ConsentStatus == "Required" || state.ConsentStatus == "Unknown" || string.IsNullOrEmpty(state.ConsentStatus);

            string att = state.Att switch
            {
                "authorized" => "ATT allow",
                "denied" => "ATT deny",
                "restricted" => "ATT restricted",
                "not_determined" => "ATT pending",
                _ => "ATT n/a",
            };

            return $"{geo} {status} + {att}";
        }

        /// <summary>
        ///     Verdict/count-strip counts (spec 2.1.1/2.1.2). Matches the existing IMGUI console's
        ///     "Required" convention (<see cref="DrivesHealth"/>): Observed rows (session activity
        ///     counters) inform the Issues tab but do not flip the header verdict, same as today.
        /// </summary>
        internal static (int fail, int warn, int wait, int pass) ComputeMenuHealthCounts(List<SorollaDiagnosticRow> rows)
        {
            int fail = 0, warn = 0, wait = 0, pass = 0;
            foreach (SorollaDiagnosticRow row in rows)
            {
                if (!DrivesHealth(row)) continue;
                switch (row.Severity)
                {
                    case SorollaDiagnosticSeverity.Fail:
                        fail++;
                        break;
                    case SorollaDiagnosticSeverity.Warning:
                        warn++;
                        break;
                    case SorollaDiagnosticSeverity.Waiting:
                        wait++;
                        break;
                    case SorollaDiagnosticSeverity.Pass:
                        pass++;
                        break;
                }
            }

            return (fail, warn, wait, pass);
        }
    }
}
