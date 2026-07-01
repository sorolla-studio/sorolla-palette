using System.Collections.Generic;
using UnityEngine;

namespace Sorolla.Palette.Purchasing
{
    /// <summary>
    ///     Cross-restart purchase dedup for the entire TrackPurchase fan-out (DR-01). Unity IAP v5
    ///     re-delivers an unconfirmed purchase by re-firing OnPurchasePending on the NEXT app launch
    ///     (Unity-documented crash-replay), so an in-memory-only set would let the same purchase
    ///     inflate revenue once per restart while the docs claim crash-replay immunity. The
    ///     committed set is persisted to PlayerPrefs so dedup survives process death and the
    ///     immunity is real.
    ///
    ///     The dedup key (DR-07) prefers the store's original-transaction id when the caller
    ///     supplies one, so an iOS restore / re-delivery of a non-consumable (new TransactionID,
    ///     same OriginalTransactionID) is recognised as already-counted instead of new revenue.
    ///     Assumption (hyper-casual portfolio): products are consumables + one-shot
    ///     non-consumables, NOT auto-renewable subscriptions. Subscriptions share an
    ///     OriginalTransactionID across renewals, so this key would collapse legitimate renewal
    ///     revenue. Revisit before shipping a subscription product.
    /// </summary>
    internal static class PurchaseDedupLedger
    {
        const string ProcessedTxPrefsKey = "sorolla.purchase.processed_tx_ids";
        // Bound the persisted set so PlayerPrefs stays small; FIFO eviction of the oldest ids.
        // Crash-replay re-delivery happens on the immediate next launch, far inside this window,
        // so eviction never reopens the replay hole it closes.
        const int ProcessedTxMax = 512;
        static HashSet<string> s_processedTxIds;
        static readonly List<string> s_processedTxOrder = new List<string>();
        // Keys that passed the dedup check and were queued but have NOT yet been committed to the
        // persisted ledger (commit happens only after the fan-out actually runs - B-5). In-memory
        // only: if a queued fan-out is dropped (queue cap) or never runs, the key never persists, so
        // a next-launch re-delivery re-fires instead of being deduped into permanent revenue loss.
        // Caveat: a same-session re-delivery of such a dropped key stays deduped until next launch;
        // the queue-overflow window that causes this is rare and the next-launch recovery is the
        // safe direction (re-fire over silent loss).
        static readonly HashSet<string> s_inFlightTxIds = new HashSet<string>();

        // Enter-Play-Mode-Options (domain reload disabled) keeps statics between play sessions, so a
        // cached ledger would diverge from PlayerPrefs. Reset so the next run re-reads disk (B-10).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_processedTxIds = null;
            s_processedTxOrder.Clear();
            s_inFlightTxIds.Clear();
        }

        // Dedup phase 1 (BEFORE dispatch): returns true if this key may proceed to the fan-out.
        // Empty key (no transaction id available) cannot be deduped, so it is always allowed through
        // - matching the previous behavior. A key already committed to the persisted ledger OR
        // already queued this session (in-flight) is a duplicate -> false. The slot is NOT persisted
        // here; Commit does that only after the fan-out runs (B-5).
        public static bool TryBegin(string dedupKey)
        {
            if (string.IsNullOrEmpty(dedupKey)) return true;
            LoadProcessedTxIds();
            if (s_processedTxIds.Contains(dedupKey)) return false;
            return s_inFlightTxIds.Add(dedupKey);
        }

        // Dedup phase 2 (AFTER dispatch): commit the key to the persisted, restart-surviving ledger.
        // Called from inside the queued fan-out, so a purchase whose fan-out was dropped or never ran
        // does not burn its dedup slot (B-5). FIFO-evicts the oldest id to bound PlayerPrefs.
        public static void Commit(string dedupKey)
        {
            if (string.IsNullOrEmpty(dedupKey)) return;
            LoadProcessedTxIds();
            s_inFlightTxIds.Remove(dedupKey);
            if (!s_processedTxIds.Add(dedupKey)) return;
            s_processedTxOrder.Add(dedupKey);
            while (s_processedTxOrder.Count > ProcessedTxMax)
            {
                string evicted = s_processedTxOrder[0];
                s_processedTxOrder.RemoveAt(0);
                s_processedTxIds.Remove(evicted);
            }
            PlayerPrefs.SetString(ProcessedTxPrefsKey, string.Join("\n", s_processedTxOrder));
            PlayerPrefs.Save();
        }

        static void LoadProcessedTxIds()
        {
            if (s_processedTxIds != null) return;
            s_processedTxIds = new HashSet<string>();
            string raw = PlayerPrefs.GetString(ProcessedTxPrefsKey, "");
            if (string.IsNullOrEmpty(raw)) return;
            foreach (string id in raw.Split('\n'))
                if (!string.IsNullOrEmpty(id) && s_processedTxIds.Add(id))
                    s_processedTxOrder.Add(id);
        }
    }
}
