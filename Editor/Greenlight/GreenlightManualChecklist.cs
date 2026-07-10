using System.Collections.Generic;
using Sorolla.Palette.Editor.UI;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor.Greenlight
{
    /// <summary>
    ///     Rows the greenlight verdict cannot machine-check (dashboard-only facts, or facts that need
    ///     a human play session) - "manual gates stay manual, but guided" from the studio self-serve
    ///     greenlight plan. Ticked state persists per-project in EditorPrefs (same project-scoping
    ///     convention as <see cref="BuildValidationProfileSettings"/>). Every row here must carry fix
    ///     text and, where applicable, a deep link - never render as a bare unchecked box.
    /// </summary>
    static class GreenlightManualChecklist
    {
        internal enum Item
        {
            GaPlatformRegistered,
            CrossVendorDashboardDrift,
            AdjustPurchaseVerification,
            StoreSkusConfigured,
            RelaunchPersistence,
            BackgroundResumeCycle,
        }

        internal sealed class State
        {
            public Dictionary<Item, bool> Ticked = new Dictionary<Item, bool>();
        }

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

        internal static List<GreenlightEvaluator.Row> ToRows(SorollaConfig config, State state)
        {
            var rows = new List<GreenlightEvaluator.Row>();

            rows.Add(ManualRow(state, Item.GaPlatformRegistered, "GA Platform Registered",
                "The GA collector accepts events for any platform string with valid credentials - it cannot tell you the active platform is actually added in the dashboard.",
                "GameAnalytics dashboard -> the game -> Settings -> add the active platform if missing.",
                "https://go.gameanalytics.com/login"));

            rows.Add(ManualRow(state, Item.CrossVendorDashboardDrift, "Cross-Vendor Dashboard Drift",
                "MAX FAN and Adjust's Facebook integration both reference a Facebook app id server-side - a probe scoped to the FB app object cannot see another vendor's dashboard config (e.g. a deleted app id still referenced elsewhere).",
                "Confirm the app id in AppLovin MAX's Facebook Audience Network setup and Adjust's Facebook integration both match the live FB app id.",
                "https://dash.applovin.com/"));

            bool isFull = SorollaSettings.Mode == SorollaMode.Full;
            if (isFull)
            {
                rows.Add(ManualRow(state, Item.AdjustPurchaseVerification, "Adjust Purchase Verification (Full mode)",
                    "Server-side receipt verification is an Adjust dashboard toggle, not something the SDK can read back.",
                    "Adjust dashboard -> the app -> Event settings -> confirm purchase verification is ON.",
                    "https://suite.adjust.com/"));
            }

            SorollaQaExpectations expectations = SorollaQaExpectations.Current;
            if (expectations != null && expectations.usesIap)
            {
                rows.Add(ManualRow(state, Item.StoreSkusConfigured, "Store SKUs / Testing Track Configured",
                    $"Store console SKU setup and testing-track membership aren't SDK-readable. Expected: {expectations.ExpectedSkuCount} SKU(s) per the QA Expectations asset.",
                    "Google Play Console / App Store Connect -> confirm every expected SKU exists and this build/tester is on the right testing track.",
                    null));
            }

            rows.Add(ManualRow(state, Item.RelaunchPersistence, "Relaunch Persistence",
                "Whether consent/identity/progress survive a real app kill+relaunch needs a human play session - Vitals already guides this same check at runtime.",
                "Force-quit the app, relaunch, and confirm consent state and player progress persisted (see Vitals' own relaunch-persistence check for the in-app guide).",
                null));

            rows.Add(ManualRow(state, Item.BackgroundResumeCycle, "Background / Resume Cycle",
                "Ad/IAP/session behavior across a backgrounded app needs a human play session, not a static probe.",
                "Background the app mid-session (e.g. during an ad load) and resume - confirm no crash, no stuck loading state, no duplicate session start.",
                null));

            return rows;
        }

        static GreenlightEvaluator.Row ManualRow(State state, Item item, string label, string whyNotProbeable, string fix, string deepLinkUrl)
        {
            bool ticked = state.Ticked.TryGetValue(item, out bool value) && value;
            return new GreenlightEvaluator.Row
            {
                Label = label,
                Status = ticked ? CheckRow.Status.Pass : CheckRow.Status.Wait,
                Detail = ticked ? "Verified by publisher" : whyNotProbeable,
                Fix = fix,
                DeepLinkLabel = deepLinkUrl != null ? "Open Dashboard" : null,
                DeepLinkUrl = deepLinkUrl,
            };
        }
    }
}
