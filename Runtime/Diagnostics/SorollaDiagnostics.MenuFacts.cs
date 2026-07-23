using System.Collections.Generic;
using Sorolla.Palette.Health;
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
            CapabilityState ads = SorollaRuntimeCapabilities.Max(state.Mode == "full");

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

            string line = $"session 1 · {events} events";
            if (!ads.Included) return line;

            string consent = MenuConsentCell(state);
            return $"consent {consent} · {line} · ads {(adsShown ? "shown" : "not shown")}";
        }

        /// <summary>Everything proved on THIS build: this launch's facts folded into the persisted ledger.</summary>
        internal static SorollaCoverageFact ProvedCoverageFacts() => ProvedCoverageFacts(CaptureQaState());

        internal static SorollaCoverageFact ProvedCoverageFacts(SorollaQaState state) =>
            SorollaCoverageLedger.Merge(SessionCoverageFacts(CaptureSnapshot(), state));

        /// <summary>
        ///     Is the evidence behind an all-green report too thin to call it proved? Read straight off the
        ///     TEST YOUR GAME rows: every applicable row that is still TO DO makes the build NOT PROVEN, and
        ///     a row that is not applicable is not shown and owes nothing. One derivation for both, so the
        ///     matrix a studio reads and the verdict it gets can never disagree.
        /// </summary>
        internal static bool IsCoverageThin(SorollaQaState state, SorollaCoverageFact proved) =>
            IsCoverageThin(CaptureCoverageInputs(state, proved));

        internal static bool IsCoverageThin(in SorollaCoverageInputs inputs)
        {
            foreach (SorollaMenuMatrixRow row in BuildCoverageRows(inputs))
                if (!row.Exercised)
                    return true;
            return false;
        }

        static string MenuConsentCell(SorollaQaState state)
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
        ///     Coverage matrix rows for the Overview tab. ONLY derived from snapshot facts the bridge already
        ///     serves (CaptureSnapshot/CaptureQaState) - never gates.yaml, which stays Sorolla-side. Never
        ///     claims a gate "passed"; each row is an exercised/not-exercised fact plus, when not exercised, a
        ///     how-to-trigger hint.
        /// </summary>
        internal static List<SorollaMenuMatrixRow> BuildCoverageMatrixRows() =>
            BuildCoverageRows(CaptureCoverageInputs());

        /// <summary>
        ///     The ONE list of things this game still owes evidence for. A row exists only when the capability
        ///     behind it is part of THIS game (2026-07-23): a Prototype game with no optional packages owes
        ///     exactly one row - progression. Every row here votes: still TO DO means NOT PROVEN, which is why
        ///     nothing that a studio cannot machine-complete may be added to this list.
        /// </summary>
        internal static List<SorollaMenuMatrixRow> BuildCoverageRows(in SorollaCoverageInputs inputs)
        {
            SorollaCoverageFact session = inputs.Session;
            SorollaCoverageFact proved = inputs.Proved;
            var rows = new List<SorollaMenuMatrixRow>(7);

            rows.Add(new SorollaMenuMatrixRow("Progression",
                (proved & SorollaCoverageFact.Progression) != 0,
                Cell(session, SorollaCoverageFact.Progression, inputs.ProgressionCell),
                "Play one level to completion"));

            // Consent belongs to the mediation stack: without MAX there is no CMP flow to resolve.
            if (inputs.Ads.Included)
                rows.Add(new SorollaMenuMatrixRow("Consent",
                    (proved & SorollaCoverageFact.Consent) != 0,
                    Cell(session, SorollaCoverageFact.Consent, inputs.ConsentCell),
                    "Open the app and resolve the CMP prompt",
                    QaActionRegistry.ResetConsent,
                    "Reset consent"));

            // One format proves nothing about the other: each configured unit id is its own mediation
            // chain, so each configured format owes its own completed ad. A format with no unit id for
            // the active platform is not part of this build and gets no row.
            if (inputs.Ads.Included && inputs.InterstitialConfigured)
                rows.Add(new SorollaMenuMatrixRow("Ads · interstitial",
                    (proved & SorollaCoverageFact.Interstitial) != 0,
                    Cell(session, SorollaCoverageFact.Interstitial, inputs.InterstitialCell),
                    "Tap Show interstitial below, or reach one through your own game flow",
                    QaActionRegistry.ShowInterstitial,
                    "Show interstitial"));

            if (inputs.Ads.Included && inputs.RewardedConfigured)
                rows.Add(new SorollaMenuMatrixRow("Ads · rewarded",
                    (proved & SorollaCoverageFact.Rewarded) != 0,
                    Cell(session, SorollaCoverageFact.Rewarded, inputs.RewardedCell),
                    "Tap Show rewarded below, or reach one through your own game flow",
                    QaActionRegistry.ShowRewarded,
                    "Show rewarded"));

            if (inputs.Iap.Included)
            {
                rows.Add(new SorollaMenuMatrixRow("IAP wiring", inputs.State.IapTrackingAttached,
                    inputs.State.IapTrackingAttached ? "AttachPurchaseTracking wired" : "not attached",
                    "Call Palette.AttachPurchaseTracking(store) before store.Connect(); remove Unity IAP if this game has no purchases"));
                rows.Add(new SorollaMenuMatrixRow("IAP purchase",
                    (proved & SorollaCoverageFact.IapPurchase) != 0,
                    Cell(session, SorollaCoverageFact.IapPurchase, inputs.IapPurchaseCell),
                    "Complete one purchase (sandbox/test track)"));

                // A verified purchase completes this, and so does the deterministic environment mismatch a
                // sandbox purchase gets - that answer proves the call round-tripped. A REJECTED verification
                // does not: the row stays owed and points at the issue row carrying the fix.
                if (inputs.Adjust.Applicable)
                    rows.Add(new SorollaMenuMatrixRow("Adjust purchase verification",
                        (proved & SorollaCoverageFact.AdjustPurchaseVerification) != 0,
                        Cell(session, SorollaCoverageFact.AdjustPurchaseVerification, inputs.State.IapVerification),
                        ClassifyPurchaseVerification(inputs.State.IapVerification) == PurchaseVerificationState.Failed
                            ? $"Adjust rejected the verification for this purchase ({inputs.State.IapVerification}); see Purchase verification under FIX THESE"
                            : "Complete one purchase; Adjust answers the verification call for it"));
            }

            return rows;
        }

        /// <summary>Reads the live session facts the row list needs. Kept apart from the row list itself so
        /// the rows stay a pure function of stated facts (and so tests state them directly).</summary>
        static SorollaCoverageInputs CaptureCoverageInputs()
        {
            SorollaQaState state = CaptureQaState();
            // Coverage is per BUILD, not per launch: a path proved earlier on this same build stays proved
            // across a force-quit (a studio cannot exercise every path in one session). A rebuild starts over.
            return CaptureCoverageInputs(state, ProvedCoverageFacts(state));
        }

        static SorollaCoverageInputs CaptureCoverageInputs(SorollaQaState state, SorollaCoverageFact proved)
        {
            Snapshot snap = CaptureSnapshot();
            bool fullMode = state.Mode == "full";
            SorollaConfig config = LoadConfig();
            return new SorollaCoverageInputs
            {
                State = state,
                Session = SessionCoverageFacts(snap, state),
                Proved = proved,
                Ads = SorollaRuntimeCapabilities.Max(fullMode),
                Adjust = SorollaRuntimeCapabilities.Adjust(fullMode),
                Iap = SorollaRuntimeCapabilities.UnityIap(fullMode),
                // Only the ACTIVE platform's unit ids: a game shipping Android with no iOS units owes no
                // iOS-format evidence (.Current already resolves per build target).
                InterstitialConfigured = !string.IsNullOrEmpty(config?.interstitialAdUnit?.Current),
                RewardedConfigured = !string.IsNullOrEmpty(config?.rewardedAdUnit?.Current),
                ProgressionCell = $"{snap.ProgressionStartCount} start · {snap.ProgressionEndCount} end",
                ConsentCell = MenuConsentCell(state),
                InterstitialCell =
                    $"loaded {YesNo(state.InterstitialLoaded)} · shown-to-completion {YesNo(state.InterstitialCompleted)}",
                RewardedCell =
                    $"loaded {YesNo(state.RewardedLoaded)} · shown-to-completion {YesNo(state.RewardedCompleted)}",
                IapPurchaseCell = $"{state.IapPurchaseCount} purchase(s) tracked",
            };
        }

        /// <summary>
        ///     What Adjust's answer proves. A verified purchase does, and so does the deterministic
        ///     environment mismatch a sandbox purchase gets back - that answer means the call round-tripped,
        ///     which is the whole claim of the row. A rejection or an unknown status proves nothing and
        ///     leaves the requirement owed; the Activity row carries the failure and its fix.
        /// </summary>
        internal static SorollaCoverageFact VerificationCoverage(string detail) =>
            ClassifyPurchaseVerification(detail) switch
            {
                PurchaseVerificationState.Verified => SorollaCoverageFact.AdjustPurchaseVerification,
                PurchaseVerificationState.EnvironmentMismatch => SorollaCoverageFact.AdjustPurchaseVerification,
                _ => SorollaCoverageFact.None,
            };

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
            facts |= VerificationCoverage(state.IapVerification);
            return facts;
        }

        static string YesNo(bool value) => value ? "yes" : "no";
    }

    /// <summary>
    ///     One TEST YOUR GAME row: a thing this build still owes evidence for, or has proved. Every row the
    ///     studio can see is machine-completable and votes in the verdict - permanent "go check a dashboard"
    ///     reminders were removed 2026-07-23 (they could never turn DONE, so they either sat in the list
    ///     forever or, worse, had to be exempted from the verdict, which is the drift this row list exists
    ///     to prevent).
    /// </summary>
    internal readonly struct SorollaMenuMatrixRow
    {
        public readonly string Name;
        public readonly bool Exercised;
        public readonly string Cell;
        public readonly string Hint;
        public readonly string Action;
        public readonly string ActionLabel;

        public SorollaMenuMatrixRow(
            string name, bool exercised, string cell, string hint,
            string action = null, string actionLabel = null)
        {
            Name = name;
            Exercised = exercised;
            Cell = cell;
            Hint = hint;
            Action = action;
            ActionLabel = actionLabel;
        }
    }

    /// <summary>
    ///     The stated facts the TEST YOUR GAME row list is a pure function of. Capturing them in one place
    ///     keeps the row list free of live lookups, so the same rows a device renders can be produced from
    ///     declared facts in a test.
    /// </summary>
    internal struct SorollaCoverageInputs
    {
        public SorollaQaState State;
        /// <summary>What THIS launch proved (drives whether a cell shows live numbers or "verified earlier").</summary>
        public SorollaCoverageFact Session;
        /// <summary>Everything proved on this build - the session facts folded into the per-build ledger.</summary>
        public SorollaCoverageFact Proved;
        public CapabilityState Ads;
        public CapabilityState Adjust;
        public CapabilityState Iap;
        public bool InterstitialConfigured;
        public bool RewardedConfigured;
        public string ProgressionCell;
        public string ConsentCell;
        public string InterstitialCell;
        public string RewardedCell;
        public string IapPurchaseCell;
    }
}
