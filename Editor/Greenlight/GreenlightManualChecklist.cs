using System.Collections.Generic;
using System.Linq;
using Sorolla.Palette.Health;

namespace Sorolla.Palette.Editor.Greenlight
{
    /// <summary>
    ///     Metadata for the manual gates the greenlight verdict cannot machine-check (dashboard-only facts, or
    ///     facts that need a human play session). Evidence for these comes from a scoped attestation
    ///     (<see cref="QaAttestation"/>, Cycle 4b) recorded against the current build identity - NOT a legacy
    ///     EditorPrefs tick, which carried no scope/actor/timestamp and could never satisfy a gate's required
    ///     proof (B-10). This class now only supplies the display + gate metadata; the adapter turns
    ///     attestations into observations.
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

        internal static Descriptor DescriptorForLabel(string label) =>
            Descriptors.FirstOrDefault(d => d.Label == label);

        internal static Descriptor DescriptorForGate(string gateId) =>
            Descriptors.FirstOrDefault(d => d.GateId == gateId);
    }
}
