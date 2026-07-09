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

        /// <summary>
        ///     Coverage matrix rows for the Overview tab (spec section 11 item 3). ONLY derived from
        ///     snapshot facts the bridge already serves (CaptureSnapshot/CaptureQaState) - never
        ///     gates.yaml, which stays Sorolla-side. Never claims a gate "passed"; each row is an
        ///     exercised/not-exercised fact plus, when not exercised, a how-to-trigger hint.
        /// </summary>
        internal static List<SorollaMenuMatrixRow> BuildCoverageMatrixRows()
        {
            Snapshot snap = CaptureSnapshot();
            SorollaQaState state = CaptureQaState();
            var rows = new List<SorollaMenuMatrixRow>(8);

            bool consentResolved = state.ConsentStatus == "Obtained" || state.ConsentStatus == "Denied"
                || state.ConsentStatus == "NotApplicable";
            rows.Add(new SorollaMenuMatrixRow("Consent", consentResolved, MenuConsentCell(state, out _),
                "Open the app and resolve the CMP prompt"));

            bool progressionSeen = snap.ProgressionStartCount > 0 && snap.ProgressionEndCount > 0;
            rows.Add(new SorollaMenuMatrixRow("Progression", progressionSeen,
                $"{snap.ProgressionStartCount} start · {snap.ProgressionEndCount} end", "Play one level to completion"));

            bool economySeen = snap.EconomyEarnCount > 0 && snap.EconomySpendCount > 0;
            rows.Add(new SorollaMenuMatrixRow("Economy", economySeen,
                $"{snap.EconomyEarnCount} earn · {snap.EconomySpendCount} spend", "Earn and spend soft currency once each"));

            // Hint verified live (team-lead tier-2 follow-up): Actions -> "Fire test event" does NOT
            // flip this row - DoTrackTestEvent tags the event with the QA-test marker and runs inside
            // a test-action scope, and RecordCustomEvent excludes both from this counter (same
            // DR-33/60 rationale as Economy/Progression: QA smoke tests must not inflate real
            // game-integration coverage). The hint points at a real Palette.TrackEvent call from game
            // code, not the Actions-tab button - matching the Economy/Progression/Custom convention
            // already used elsewhere in this matrix.
            bool customSeen = snap.CustomEventCount > 0;
            rows.Add(new SorollaMenuMatrixRow("Custom events", customSeen,
                customSeen ? $"{snap.CustomEventCount} seen" : "none seen",
                "Trigger a real Palette.TrackEvent call from game code (the Actions test-event button is excluded from this count)"));

            bool interExercised = state.InterstitialLoaded && state.InterstitialCompleted;
            rows.Add(new SorollaMenuMatrixRow("Ads · interstitial", interExercised,
                $"loaded {YesNo(state.InterstitialLoaded)} · shown-to-completion {YesNo(state.InterstitialCompleted)}",
                "Show an interstitial from Actions → Ads test"));

            bool rewardedExercised = state.RewardedLoaded && state.RewardedCompleted;
            rows.Add(new SorollaMenuMatrixRow("Ads · rewarded", rewardedExercised,
                $"loaded {YesNo(state.RewardedLoaded)} · shown-to-completion {YesNo(state.RewardedCompleted)}",
                "Show a rewarded ad from Actions → Ads test"));

            rows.Add(new SorollaMenuMatrixRow("Ads · revenue", state.AdRevenueSeen,
                state.AdRevenueSeen ? "revenue callback seen" : "no revenue callback seen",
                "Show an ad through to an impression"));

            // IAP row only when the game has IAP wired at all (tracking attached or a purchase already
            // observed) - a game with no store integration would otherwise show a permanently
            // not-exercised row with no honest way to satisfy it.
            if (state.IapTrackingAttached || state.IapPurchaseCount > 0)
            {
                bool purchaseSeen = state.IapPurchaseCount > 0;
                rows.Add(new SorollaMenuMatrixRow("IAP", purchaseSeen,
                    purchaseSeen ? $"{state.IapPurchaseCount} purchase(s) tracked" : "tracking attached, no purchase yet",
                    "Complete one purchase (sandbox/test track)"));
            }

            // Relaunch persistence is NOT machine-detectable in a single session - always render as a
            // manual reminder, never as exercised/not-exercised (spec section 11 item 3).
            rows.Add(SorollaMenuMatrixRow.ManualReminder("Relaunch persistence",
                "Close and reopen the app; consent/config should persist without re-prompting"));

            return rows;
        }

        static string YesNo(bool value) => value ? "yes" : "no";
    }

    /// <summary>One coverage-matrix row (Overview tab). <see cref="IsManualReminder"/> rows are never
    /// machine-checkable in-session (e.g. relaunch persistence) and render with neutral treatment
    /// regardless of <see cref="Exercised"/>.</summary>
    internal readonly struct SorollaMenuMatrixRow
    {
        public readonly string Name;
        public readonly bool Exercised;
        public readonly string Cell;
        public readonly string Hint;
        public readonly bool IsManualReminder;

        public SorollaMenuMatrixRow(string name, bool exercised, string cell, string hint)
        {
            Name = name;
            Exercised = exercised;
            Cell = cell;
            Hint = hint;
            IsManualReminder = false;
        }

        SorollaMenuMatrixRow(string name, string cell, string hint, bool manualReminder)
        {
            Name = name;
            Exercised = false;
            Cell = cell;
            Hint = hint;
            IsManualReminder = manualReminder;
        }

        public static SorollaMenuMatrixRow ManualReminder(string name, string hint) =>
            new SorollaMenuMatrixRow(name, "manual check", hint, true);
    }
}
