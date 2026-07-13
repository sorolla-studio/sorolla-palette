using System.Collections.Generic;
using Sorolla.Palette.Health;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor.Greenlight
{
    /// <summary>
    ///     Rows the greenlight verdict cannot machine-check (dashboard-only facts, or facts that need a
    ///     human play session) - "manual gates stay manual, but guided" from the studio self-serve greenlight
    ///     plan. Ticked state persists per-project in EditorPrefs. A legacy tick is NOT scoped evidence: it
    ///     has no build/device/vendor scope, actor, or timestamp, so it maps to an observation whose observed
    ///     proof cannot satisfy the gate's required proof - the gate resolves to INCOMPLETE, never PASS
    ///     (B-10). The Item metadata (label, why, fix, deep link) drives both the emitted observation and the
    ///     window's display.
    /// </summary>
    static class GreenlightManualChecklist
    {
        internal enum Item
        {
            GaPlatformRegistered,
            CrossVendorDashboardDrift,
            AdjustPurchaseVerification,
            RelaunchPersistence,
            BackgroundResumeCycle,
        }

        internal sealed class State
        {
            public Dictionary<Item, bool> Ticked = new Dictionary<Item, bool>();
        }

        /// <summary>Static description of one manual gate: its neutral gate id, display label, why a machine
        /// cannot check it, the fix, and an optional dashboard deep link.</summary>
        internal sealed class Descriptor
        {
            public Item Item;
            public string GateId;
            public string Label;
            public string Why;
            public string Fix;
            public string DeepLinkUrl;
        }

        internal static readonly IReadOnlyList<Descriptor> Descriptors = new[]
        {
            new Descriptor
            {
                Item = Item.GaPlatformRegistered, GateId = GateIds.ManualGaPlatformRegistered,
                Label = "GA Platform Registered",
                Why = "The GA collector accepts events for any platform string with valid credentials - it cannot tell you the active platform is actually added in the dashboard.",
                Fix = "GameAnalytics dashboard -> the game -> Settings -> add the active platform if missing.",
                DeepLinkUrl = "https://go.gameanalytics.com/login",
            },
            new Descriptor
            {
                Item = Item.CrossVendorDashboardDrift, GateId = GateIds.ManualCrossVendorDashboardDrift,
                Label = "Cross-Vendor Dashboard Drift",
                Why = "AppLovin MAX FAN and Adjust's Facebook integration both reference a Facebook app id server-side - a probe scoped to the FB app object cannot see another vendor's dashboard config (e.g. a deleted app id still referenced elsewhere).",
                Fix = "Confirm the app id in AppLovin MAX's Facebook Audience Network setup and Adjust's Facebook integration both match the live FB app id.",
                DeepLinkUrl = "https://dash.applovin.com/",
            },
            new Descriptor
            {
                Item = Item.AdjustPurchaseVerification, GateId = GateIds.ManualAdjustPurchaseVerification,
                Label = "Adjust Purchase Verification (Full mode)",
                Why = "Server-side receipt verification is an Adjust dashboard toggle, not something the SDK can read back.",
                Fix = "Adjust dashboard -> the app -> Event settings -> confirm purchase verification is ON.",
                DeepLinkUrl = "https://suite.adjust.com/",
            },
            new Descriptor
            {
                Item = Item.RelaunchPersistence, GateId = GateIds.ManualRelaunchPersistence,
                Label = "Relaunch Persistence",
                Why = "Whether consent/identity/progress survive a real app kill+relaunch needs a human play session - Vitals already guides this same check at runtime.",
                Fix = "Force-quit the app, relaunch, and confirm consent state and player progress persisted (see Vitals' own relaunch-persistence check for the in-app guide).",
                DeepLinkUrl = null,
            },
            new Descriptor
            {
                Item = Item.BackgroundResumeCycle, GateId = GateIds.ManualBackgroundResumeCycle,
                Label = "Background / Resume Cycle",
                Why = "Ad/IAP/session behavior across a backgrounded app needs a human play session, not a static probe.",
                Fix = "Background the app mid-session (e.g. during an ad load) and resume - confirm no crash, no stuck loading state, no duplicate session start.",
                DeepLinkUrl = null,
            },
        };

        internal static State Load()
        {
            var state = new State();
            foreach (Item item in (Item[])System.Enum.GetValues(typeof(Item)))
                state.Ticked[item] = EditorPrefs.GetBool(PrefKey(item), false);
            return state;
        }

        internal static void SetTicked(Item item, bool value)
        {
            EditorPrefs.SetBool(PrefKey(item), value);
        }

        static string PrefKey(Item item) => $"Sorolla_Greenlight_{item}_{Application.dataPath.GetHashCode()}";

        /// <summary>
        ///     One observation per manual gate. A ticked box reports PASS but carries <see
        ///     cref="ProofScope.None"/> - a legacy check-off is not scoped attestation, so the catalog's
        ///     required vendor/device proof is unmet and the gate resolves to INCOMPLETE. An unticked box
        ///     reports INCOMPLETE directly. Either way the row surfaces its why/fix so the studio can act.
        /// </summary>
        internal static List<GateObservation> ToObservations(State state)
        {
            var observations = new List<GateObservation>();
            foreach (Descriptor d in Descriptors)
            {
                bool ticked = state != null && state.Ticked.TryGetValue(d.Item, out bool value) && value;
                observations.Add(new GateObservation
                {
                    GateId = d.GateId,
                    Outcome = ticked ? GateOutcome.Pass : GateOutcome.Incomplete,
                    ObservedProof = ProofScope.None, // legacy tick has no build/device/vendor scope
                    Evidence = ticked
                        ? "Marked verified in the editor, but a legacy check-off carries no build/device/vendor scope - re-attest with scoped evidence."
                        : d.Why,
                    FixHint = d.Fix,
                });
            }
            return observations;
        }
    }
}
