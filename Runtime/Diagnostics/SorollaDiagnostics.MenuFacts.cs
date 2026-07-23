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
        ///     Header coverage line: what this BUILD has proved (spec 2.1.4). Session count is always
        ///     reported as 1 - the SDK has no cross-launch session counter today; a real counter is
        ///     future SDK work, not a phase-2 gap. The ads word reads off the per-build ledger, so it
        ///     agrees with the coverage matrix and the verdict instead of resetting on every relaunch.
        /// </summary>
        internal static string BuildMenuCoverageLine(out bool thin)
        {
            SorollaQaState state = CaptureQaState();
            SorollaAdsCapability ads = SorollaRuntimeCapabilities.Ads(state.Mode == "full");

            string consent = MenuConsentCell(state, out _);
            int events = 0;
            if (state.Events != null)
            {
                foreach (SorollaQaEvent evt in state.Events)
                    events += evt.Count;
            }

            SorollaCoverageFact proved = ProvedCoverageFacts(state);
            bool adsShown = (proved & (SorollaCoverageFact.Interstitial | SorollaCoverageFact.Rewarded
                | SorollaCoverageFact.AdRevenue)) != 0;

            thin = IsCoverageThin(state, proved);

            string line = $"consent {consent} · session 1 · {events} events";
            return ads.Included ? $"{line} · ads {(adsShown ? "shown" : "not shown")}" : line;
        }

        /// <summary>Everything proved on THIS build: this launch's facts folded into the persisted ledger.</summary>
        internal static SorollaCoverageFact ProvedCoverageFacts() => ProvedCoverageFacts(CaptureQaState());

        internal static SorollaCoverageFact ProvedCoverageFacts(SorollaQaState state) =>
            SorollaCoverageLedger.Merge(SessionCoverageFacts(CaptureSnapshot(), state));

        /// <summary>
        ///     Is the evidence behind an all-green report too thin to call it proved? Derived from the
        ///     per-build coverage ledger, never from a raw event count: a boot sequence alone fires
        ///     consent/IAP/auto-level events, so counting events made a fresh launch look played
        ///     (hungrysnake iOS 2026-07-20). Green needs consent resolved AND a level played to
        ///     completion AND one ad watched to completion on a format this game actually configured -
        ///     one completed ad proves the mediation chain; per-format unit keys are already proved by
        ///     LOAD, so a second format is never demanded. A game with no ad units configured owes no
        ///     ad evidence at all.
        /// </summary>
        internal static bool IsCoverageThin(SorollaQaState state, SorollaCoverageFact proved)
        {
            MenuConsentCell(state, out bool consentUnresolved);
            if (consentUnresolved) return true;
            if ((proved & SorollaCoverageFact.Progression) == 0) return true;
            return !AdCoverageSatisfied(state, proved);
        }

        /// <summary>One completed ad on ANY configured format is enough; no configured format means
        /// nothing to prove (same shape as the build check that only warns when both units are empty).</summary>
        static bool AdCoverageSatisfied(SorollaQaState state, SorollaCoverageFact proved)
        {
            SorollaAdsCapability ads = SorollaRuntimeCapabilities.Ads(state.Mode == "full");
            SorollaConfig config = LoadConfig();
            bool rewardedConfigured = !string.IsNullOrEmpty(config?.rewardedAdUnit?.Current);
            bool interstitialConfigured = !string.IsNullOrEmpty(config?.interstitialAdUnit?.Current);
            return AdCoverageSatisfied(ads, rewardedConfigured, interstitialConfigured, proved);
        }

        internal static bool AdCoverageSatisfied(SorollaAdsCapability ads, bool rewardedConfigured,
            bool interstitialConfigured, SorollaCoverageFact proved)
        {
            if (!ads.Included) return true;
            if (!rewardedConfigured && !interstitialConfigured) return true;
            if (rewardedConfigured && (proved & SorollaCoverageFact.Rewarded) != 0) return true;
            return interstitialConfigured && (proved & SorollaCoverageFact.Interstitial) != 0;
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
            SorollaAdsCapability ads = SorollaRuntimeCapabilities.Ads(state.Mode == "full");
            var rows = new List<SorollaMenuMatrixRow>(8);

            // Coverage is per BUILD, not per launch: a path proved earlier on this same build stays proved
            // across a force-quit (a studio cannot exercise every path in one session). A rebuild starts over.
            SorollaCoverageFact session = SessionCoverageFacts(snap, state);
            SorollaCoverageFact proved = SorollaCoverageLedger.Merge(session);

            bool consentResolved = (proved & SorollaCoverageFact.Consent) != 0;
            rows.Add(new SorollaMenuMatrixRow("Consent", consentResolved,
                Cell(session, SorollaCoverageFact.Consent, MenuConsentCell(state, out _)),
                ads.Included
                    ? "Open the app and resolve the CMP prompt"
                    : "Open the app and complete the consent or ATT prompt if shown"));

            bool progressionSeen = (proved & SorollaCoverageFact.Progression) != 0;
            rows.Add(new SorollaMenuMatrixRow("Progression", progressionSeen,
                Cell(session, SorollaCoverageFact.Progression,
                    $"{snap.ProgressionStartCount} start · {snap.ProgressionEndCount} end"),
                "Play one level to completion"));

            // One observed flow proves the economy family dispatches; earn-without-spend is normal
            // player behavior, not missing coverage (minimum-evidence rule: earn-only owes no spend test).
            bool economySeen = (proved & SorollaCoverageFact.Economy) != 0;
            rows.Add(new SorollaMenuMatrixRow("Economy", economySeen,
                Cell(session, SorollaCoverageFact.Economy,
                    $"{snap.EconomyEarnCount} earn · {snap.EconomySpendCount} spend"),
                "Earn or spend soft currency once"));

            // Only a real game-code event flips this row: the SDK's own test event is tagged as a QA
            // test and excluded from this counter (same DR-33/60 rationale as Economy/Progression -
            // QA smoke tests must not inflate real game-integration coverage). The hint stays silent
            // about that internal button: a studio never sees it, and copy may only reference
            // surfaces studio mode renders.
            bool customSeen = (proved & SorollaCoverageFact.CustomEvent) != 0;
            rows.Add(new SorollaMenuMatrixRow("Custom events", customSeen,
                Cell(session, SorollaCoverageFact.CustomEvent, $"{snap.CustomEventCount} seen"),
                "Trigger a real Palette.TrackEvent call from your game code"));

            AddMaxCoverageRows(rows, state, session, proved, ads);

            // IAP row only when the game has IAP wired at all (tracking attached or a purchase already
            // observed) - a game with no store integration would otherwise show a permanently
            // not-exercised row with no honest way to satisfy it.
            if (state.IapTrackingAttached || state.IapPurchaseCount > 0)
            {
                bool purchaseSeen = (proved & SorollaCoverageFact.IapPurchase) != 0;
                rows.Add(new SorollaMenuMatrixRow("IAP", purchaseSeen,
                    Cell(session, SorollaCoverageFact.IapPurchase, $"{state.IapPurchaseCount} purchase(s) tracked"),
                    "Complete one purchase (sandbox/test track)"));
                rows.Add(SorollaMenuMatrixRow.ManualReminder("Adjust purchase verification",
                    "Check the Adjust dashboard purchase-verification setting; see Documentation~/dashboards/adjust.md"));
            }

            rows.Add(SorollaMenuMatrixRow.ManualReminder("GameAnalytics platform registration",
                "Confirm this build platform exists in the GameAnalytics dashboard; see Documentation~/dashboards/gameanalytics.md"));

            if (state.Mode == "full")
                rows.Add(SorollaMenuMatrixRow.ManualReminder("AppLovin / Adjust app identity",
                    "Confirm both dashboards describe this same game and platform; see Documentation~/dashboards/"));

            // Relaunch persistence is NOT machine-detectable in a single session - always render as a
            // manual reminder, never as exercised/not-exercised (spec section 11 item 3).
            rows.Add(SorollaMenuMatrixRow.ManualReminder("Relaunch persistence",
                "Close and reopen the app; consent/config should persist without re-prompting"));

            return rows;
        }

        static void AddMaxCoverageRows(List<SorollaMenuMatrixRow> rows, SorollaQaState state,
            SorollaCoverageFact session, SorollaCoverageFact proved, SorollaAdsCapability ads)
        {
            if (!ads.Included) return;

            bool interExercised = (proved & SorollaCoverageFact.Interstitial) != 0;
            rows.Add(new SorollaMenuMatrixRow("Ads · interstitial", interExercised,
                Cell(session, SorollaCoverageFact.Interstitial,
                    $"loaded {YesNo(state.InterstitialLoaded)} · shown-to-completion {YesNo(state.InterstitialCompleted)}"),
                "Tap Show interstitial below, or reach one through your own game flow"));

            bool rewardedExercised = (proved & SorollaCoverageFact.Rewarded) != 0;
            rows.Add(new SorollaMenuMatrixRow("Ads · rewarded", rewardedExercised,
                Cell(session, SorollaCoverageFact.Rewarded,
                    $"loaded {YesNo(state.RewardedLoaded)} · shown-to-completion {YesNo(state.RewardedCompleted)}"),
                "Tap Show rewarded below, or reach one through your own game flow"));

            bool revenueSeen = (proved & SorollaCoverageFact.AdRevenue) != 0;
            rows.Add(new SorollaMenuMatrixRow("Ads · revenue", revenueSeen,
                Cell(session, SorollaCoverageFact.AdRevenue, "revenue callback seen"),
                "Use the Show interstitial or Show rewarded button above and let the ad play through"));
        }

        /// <summary>The cell text for a proved row: this launch's own numbers when it saw the fact, otherwise
        /// a plain statement that an earlier launch on this same build proved it (the session counters would
        /// read zero and make a DONE row look wrong).</summary>
        static string Cell(SorollaCoverageFact session, SorollaCoverageFact fact, string sessionText) =>
            (session & fact) != 0 ? sessionText : "verified earlier on this build";

        /// <summary>What THIS launch proved. Facts only ever turn on here; the ledger owns the union.</summary>
        static SorollaCoverageFact SessionCoverageFacts(Snapshot snap, SorollaQaState state)
        {
            SorollaCoverageFact facts = SorollaCoverageFact.None;
            if (state.ConsentStatus == "Obtained" || state.ConsentStatus == "Denied"
                || state.ConsentStatus == "NotApplicable")
                facts |= SorollaCoverageFact.Consent;
            if (snap.ProgressionStartCount > 0 && snap.ProgressionEndCount > 0)
                facts |= SorollaCoverageFact.Progression;
            // One observed flow proves the economy family dispatches; earn-without-spend is normal player
            // behavior, not missing coverage (minimum-evidence rule: earn-only owes no spend test).
            if (snap.EconomyEarnCount > 0 || snap.EconomySpendCount > 0)
                facts |= SorollaCoverageFact.Economy;
            if (snap.CustomEventCount > 0) facts |= SorollaCoverageFact.CustomEvent;
            if (state.InterstitialLoaded && state.InterstitialCompleted) facts |= SorollaCoverageFact.Interstitial;
            if (state.RewardedLoaded && state.RewardedCompleted) facts |= SorollaCoverageFact.Rewarded;
            if (state.AdRevenueSeen) facts |= SorollaCoverageFact.AdRevenue;
            if (state.IapPurchaseCount > 0) facts |= SorollaCoverageFact.IapPurchase;
            return facts;
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
