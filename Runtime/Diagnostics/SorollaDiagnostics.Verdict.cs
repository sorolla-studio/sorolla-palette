using System.Collections.Generic;

namespace Sorolla.Palette
{
    /// <summary>
    ///     The ONE runtime Vitals verdict (2026-07-20 studio-UX simplification). Before this, three UI sites
    ///     rebuilt the same fail/warn/wait thresholds inline and each could drift; the verdict now lives here,
    ///     with the presentation layers reading it. It also folds in the coverage-thin flag, because the
    ///     honest answer to "all rows green but this session exercised nothing" is NOT PROVEN, never green.
    /// </summary>
    internal enum SorollaVitalsVerdict
    {
        /// <summary>At least one required row is FAIL.</summary>
        Failing,
        /// <summary>No FAIL, but rows need attention (WARN/WAIT).</summary>
        ActionNeeded,
        /// <summary>Every row passes, but the session exercised too little to claim it - amber, never green.</summary>
        NotProven,
        /// <summary>Every row passes AND the session exercised enough to mean it.</summary>
        Pass,
    }

    /// <summary>Who owns a diagnostic row: the studio (their game/config/credentials) or Sorolla (the SDK
    /// itself). Ownership picks the SECTION and the fix wording - it never hides a red row.</summary>
    internal enum SorollaRowOwner
    {
        Studio,
        Sorolla,
    }

    /// <summary>The folded verdict + the counts behind it, so a frontend renders one computation.</summary>
    internal readonly struct SorollaVitalsVerdictReport
    {
        public readonly SorollaVitalsVerdict Verdict;
        public readonly int Fail;
        public readonly int Warn;
        public readonly int Wait;
        public readonly int Pass;
        public readonly bool CoverageThin;

        public SorollaVitalsVerdictReport(
            SorollaVitalsVerdict verdict, int fail, int warn, int wait, int pass, bool coverageThin)
        {
            Verdict = verdict;
            Fail = fail;
            Warn = warn;
            Wait = wait;
            Pass = pass;
            CoverageThin = coverageThin;
        }

        public int NeedsAttention => Fail + Warn + Wait;
    }

    internal static partial class SorollaDiagnostics
    {
        /// <summary>
        ///     Folds the health counts and the session-coverage flag into one verdict. Fail-closed by
        ///     construction: a FAIL outranks everything, and an all-green report over a session that exercised
        ///     nothing resolves to <see cref="SorollaVitalsVerdict.NotProven"/> - green requires both.
        /// </summary>
        internal static SorollaVitalsVerdictReport ComputeVerdict(List<SorollaDiagnosticRow> rows)
        {
            (int fail, int warn, int wait, int pass) = ComputeMenuHealthCounts(rows);
            SorollaQaState state = CaptureQaState();
            bool thin = IsCoverageThin(state, ProvedCoverageFacts(state));

            SorollaVitalsVerdict verdict =
                fail > 0 ? SorollaVitalsVerdict.Failing
                : warn + wait > 0 ? SorollaVitalsVerdict.ActionNeeded
                : thin ? SorollaVitalsVerdict.NotProven
                : SorollaVitalsVerdict.Pass;

            return new SorollaVitalsVerdictReport(verdict, fail, warn, wait, pass, thin);
        }

        /// <summary>The word shown in the verdict hero / compact chip.</summary>
        internal static string VerdictWord(in SorollaVitalsVerdictReport report) => report.Verdict switch
        {
            SorollaVitalsVerdict.Failing => "FAILING",
            SorollaVitalsVerdict.ActionNeeded => $"{report.Warn + report.Wait} ISSUES",
            SorollaVitalsVerdict.NotProven => "NOT PROVEN",
            _ => "HEALTHY",
        };

        /// <summary>The one-line meaning under the verdict word - what it says about THIS build, in the
        /// studio's terms.</summary>
        internal static string VerdictMeaning(in SorollaVitalsVerdictReport report) => report.Verdict switch
        {
            SorollaVitalsVerdict.Failing => "Something is broken in this build - fix the rows below.",
            SorollaVitalsVerdict.ActionNeeded => "This build is not clean yet - work the rows below.",
            SorollaVitalsVerdict.NotProven =>
                "Every check passes, but this session barely exercised the game - play it, then re-check.",
            _ => "Everything checked passes and this session exercised the game.",
        };

        /// <summary>Stable machine token for the QA-bridge snapshot (agents key on this, not the display word).</summary>
        internal static string VerdictToken(SorollaVitalsVerdict verdict) => verdict switch
        {
            SorollaVitalsVerdict.Failing => "failing",
            SorollaVitalsVerdict.ActionNeeded => "action_needed",
            SorollaVitalsVerdict.NotProven => "not_proven",
            _ => "pass",
        };

        /// <summary>
        ///     A cheap change detector for the facts a report pane renders: the verdict counts plus the
        ///     per-build coverage bits. A live view compares it against the previous tick and rebuilds only
        ///     when it moved, so a fact landing while the report is on screen redraws it (an interstitial
        ///     completing used to need a close/reopen) without a full tree rebuild every tick.
        /// </summary>
        internal static int ComputeFactsFingerprint(List<SorollaDiagnosticRow> rows)
        {
            SorollaVitalsVerdictReport report = ComputeVerdict(rows);
            int hash = (int)report.Verdict;
            hash = hash * 31 + report.Fail;
            hash = hash * 31 + report.Warn;
            hash = hash * 31 + report.Wait;
            hash = hash * 31 + report.Pass;
            hash = hash * 31 + (report.CoverageThin ? 1 : 0);
            hash = hash * 31 + (int)ProvedCoverageFacts();
            return hash;
        }

        /// <summary>Convenience for the bridge: capture the current rows and return the verdict token.</summary>
        internal static string CurrentVerdictToken()
        {
            var rows = new List<SorollaDiagnosticRow>(64);
            BuildRows(rows);
            return VerdictToken(ComputeVerdict(rows).Verdict);
        }

        /// <summary>
        ///     Who owns a row. Deliberately a group-level mapping plus a tiny name-override list rather than a
        ///     field on <see cref="SorollaDiagnosticRow"/>: the row struct is constructed from ~60 sites, and a
        ///     new required field there is 60 chances to get ownership wrong. UNKNOWN groups default to
        ///     Sorolla - fail-closed, so a row added later lands in "send to Sorolla" instead of vanishing from
        ///     a studio's report.
        /// </summary>
        internal static SorollaRowOwner OwnerOf(in SorollaDiagnosticRow row)
        {
            // Boot is SDK bring-up (Sorolla's), EXCEPT the mode row - which is the studio's SorollaConfig.
            if (row.Group == "Boot")
                return row.Name == "Palette mode" || row.Name == "Network reachability"
                    ? SorollaRowOwner.Studio
                    : SorollaRowOwner.Sorolla;

            // Red flags are SDK complaints, so they default to Sorolla - but once a producer has DIAGNOSED
            // one as the game's own configuration (e.g. a remote-config key that exists nowhere), the fix is
            // the studio's and the row belongs in their list, not in "send to Sorolla".
            if (row.Group == "Red flags")
                return row.HasStructuredDiagnosis ? SorollaRowOwner.Studio : SorollaRowOwner.Sorolla;

            switch (row.Group)
            {
                case "Config":
                case "SDKs":
                case "Firebase":
                case "Consent":
                case "Identity":
                case "Activity":
                case "Ads":
                    return SorollaRowOwner.Studio;
                default:
                    return SorollaRowOwner.Sorolla;
            }
        }
    }
}
