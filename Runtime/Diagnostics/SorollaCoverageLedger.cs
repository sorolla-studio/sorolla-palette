using System;
using Sorolla.Palette.Adapters;
using UnityEngine;

namespace Sorolla.Palette
{
    /// <summary>Which coverage facts have been verified for the current build. A SET, not a hierarchy: a
    /// tester exercises them in any order across any number of launches.</summary>
    [Flags]
    internal enum SorollaCoverageFact
    {
        None = 0,
        Consent = 1 << 0,
        Progression = 1 << 1,
        Economy = 1 << 2,
        CustomEvent = 1 << 3,
        Interstitial = 1 << 4,
        Rewarded = 1 << 5,
        AdRevenue = 1 << 6,
        IapPurchase = 1 << 7,
    }

    /// <summary>
    ///     Per-build coverage ledger: coverage a tester already proved on THIS build survives a relaunch.
    ///     Before this, every force-quit reset progression/economy/ads to TO DO, asking the studio to redo
    ///     paths that cannot all be exercised in one launch anyway.
    ///
    ///     Deliberately the smallest mechanism that works: one PlayerPrefs string, "identity|bits". The
    ///     identity is the build's own runtime-readable fingerprint (app version, Unity's per-build GUID, SDK
    ///     version), so ANY rebuild - including one after an SDK repin - starts from an empty ledger. There is
    ///     no expiry and no invalidation beyond that mismatch: a stored fact either belongs to this exact
    ///     build or it is ignored outright.
    /// </summary>
    internal static class SorollaCoverageLedger
    {
        const string PrefsKey = "sorolla.coverage_ledger";

        static SorollaCoverageFact s_cached;
        static bool s_loaded;

        static string Identity => $"{Application.version}|{Application.buildGUID}|{Palette.SdkVersion}";

        /// <summary>
        ///     Folds this session's verified facts into the ledger and returns everything proved on this build.
        ///     Persists only when the merge actually adds something, so a UI refresh loop does not write.
        /// </summary>
        internal static SorollaCoverageFact Merge(SorollaCoverageFact sessionFacts)
        {
            SorollaCoverageFact merged = Load() | sessionFacts;
            if (merged == s_cached) return merged;

            s_cached = merged;
            try
            {
                PlayerPrefs.SetString(PrefsKey, $"{Identity}|{(int)merged}");
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                PaletteLog.Verbose($"[Palette:Coverage] Could not persist the coverage ledger: {e.Message}");
            }
            return merged;
        }

        /// <summary>Test/QA hook: forget everything proved on this build.</summary>
        internal static void Clear()
        {
            s_cached = SorollaCoverageFact.None;
            s_loaded = true;
            PlayerPrefs.DeleteKey(PrefsKey);
        }

        static SorollaCoverageFact Load()
        {
            if (s_loaded) return s_cached;
            s_loaded = true;
            s_cached = SorollaCoverageFact.None;

            string stored = PlayerPrefs.GetString(PrefsKey, null);
            if (string.IsNullOrEmpty(stored)) return s_cached;

            // "identity|bits" - the identity itself contains '|', so split off the LAST field only.
            int lastSeparator = stored.LastIndexOf('|');
            if (lastSeparator <= 0) return s_cached;

            string identity = stored.Substring(0, lastSeparator);
            if (identity != Identity) return s_cached; // a different build: its coverage proves nothing here

            if (int.TryParse(stored.Substring(lastSeparator + 1), out int bits))
                s_cached = (SorollaCoverageFact)bits;
            return s_cached;
        }
    }
}
