using UnityEngine;
using UnityEngine.UIElements;

namespace Sorolla.Palette
{
    // Actions tab (mockup 05, spec 2.5 + section 5). Every button's BEHAVIOR ports from the existing
    // IMGUI console's Actions implementation (SorollaDiagnosticsConsole.Actions.cs / .UI.cs DrawActions)
    // via QaActionRegistry - one core, two frontends (console + QA bridge), now three. This file adds
    // no new SDK behavior, only a state-aware UI Toolkit presentation of the same registry calls.
    //
    // Judgment call (stated for the report): the mockup's Events group shows exactly one button,
    // "Fire test event", with illustrative sub-copy "palette_debug_ping". The registry's actual test
    // event name is "sorolla_vitals_test" (QaActionRegistry.DoTrackTestEvent) - "palette_debug_ping"
    // does not exist anywhere in the SDK. Per section 8's message-rewriting rule ("counts, ids are
    // literal and copyable, never paraphrased"), the sub line uses the REAL event name, not the
    // mockup's placeholder string.
    //
    // Team-lead tier-2 parity ruling: port every IMGUI action, don't accept the mockup's narrower
    // Events group as a ceiling. The 4 extra event triggers (Level Start/Complete, Economy Earn/
    // Spend) ship as a compact chip row under "Fire test event" - #1c222d bg / #c3cad6 text /
    // #252c38 border, per the ruling's token spec (from the design project's earlier, non-approved
    // concept; the approved mockup 05 has no chip-row precedent, so this is a team-lead-directed
    // extrapolation, not a source-of-truth screen). CONSENT gets a third row, "Refresh consent
    // status", porting IMGUI's Consent -> Refresh. Once both land, every registry action the IMGUI
    // console exposes has a home here - Legacy console can come out (separate commit).
    internal sealed partial class SorollaDebugMenuOverlay
    {
        internal VisualElement BuildActionsTab()
        {
            var pane = new VisualElement();
            pane.AddToClassList("sorolla-debugmenu-actions-pane");

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("sorolla-debugmenu-issues-scroll");
            var host = new VisualElement();
            scroll.Add(host);
            pane.Add(scroll);

            host.Add(BuildActionGroupTitle("REPORT"));
            host.Add(BuildActionButton(
                "Copy SDK state",
                "Full report: context, consent, adapters, config, events, problems",
                ActionButtonStyle.Primary,
                () => GUIUtility.systemCopyBuffer = SorollaDiagnostics.BuildQaStateSummary()));

            host.Add(BuildActionGroupTitle("ADS TEST"));
            bool rewardedReady = Palette.IsRewardedAdReady;
            host.Add(BuildActionButton(
                "Show rewarded",
                rewardedReady ? "Loaded — ready to show" : "Not loaded yet — waiting for fill",
                rewardedReady ? ActionButtonStyle.Normal : ActionButtonStyle.Disabled,
                () => RunActionAndRefresh(QaActionRegistry.ShowRewarded)));
            bool interstitialReady = Palette.IsInterstitialAdReady;
            host.Add(BuildActionButton(
                "Show interstitial",
                interstitialReady ? "Loaded — ready to show" : "Not loaded yet — waiting for fill",
                interstitialReady ? ActionButtonStyle.Normal : ActionButtonStyle.Disabled,
                () => RunActionAndRefresh(QaActionRegistry.ShowInterstitial)));

            host.Add(BuildActionGroupTitle("CONSENT"));
            host.Add(BuildActionButton(
                "Show privacy options",
                "Re-opens the consent form (UMP)",
                ActionButtonStyle.Normal,
                () => RunActionAndRefresh(QaActionRegistry.OpenPrivacyOptions)));
            host.Add(BuildActionButton(
                "Reset consent",
                "Clears the stored choice; CMP runs again on restart",
                ActionButtonStyle.Normal,
                () => RunActionAndRefresh(QaActionRegistry.ResetConsent)));
            host.Add(BuildActionButton(
                "Refresh consent status",
                Palette.ConsentStatus + (Palette.CanRequestAds ? " · can request ads" : " · cannot request ads"),
                ActionButtonStyle.Normal,
                () => RunActionAndRefresh(QaActionRegistry.RefreshConsent)));

            host.Add(BuildActionGroupTitle("QA BRIDGE"));
            bool bridgeArmed = QaBridgeServer.IsArmed;
            host.Add(BuildActionButton(
                bridgeArmed ? "Bridge running" : "Bridge not running",
                bridgeArmed
                    ? $"127.0.0.1:{QaBridgeServer.Port} — serves this same data as JSON"
                    : "Bind failed or unavailable — tap to retry",
                ActionButtonStyle.Normal,
                RestartQaBridgeAndRefresh));

            host.Add(BuildActionGroupTitle("EVENTS"));
            host.Add(BuildActionButton(
                "Fire test event",
                "sorolla_vitals_test — visible in Console + vendor dashboards",
                ActionButtonStyle.Normal,
                () => RunActionAndRefresh(QaActionRegistry.TrackTestEvent)));
            host.Add(BuildEventChipRow());

            return pane;
        }

        // Tier-2 parity ruling: the 4 remaining IMGUI test-event triggers as a compact chip row,
        // not 4 more full-width cards - "Fire test event" is the one a studio tester reaches for,
        // these are secondary smoke-test variants that don't each need a label+sub+arrow treatment.
        VisualElement BuildEventChipRow()
        {
            var row = new VisualElement();
            row.AddToClassList("sorolla-debugmenu-event-chip-row");

            row.Add(BuildEventChip("Level Start", QaActionRegistry.LevelStart));
            row.Add(BuildEventChip("Level Complete", QaActionRegistry.LevelComplete));
            row.Add(BuildEventChip("Economy Earn", QaActionRegistry.EconomyEarn));
            row.Add(BuildEventChip("Economy Spend", QaActionRegistry.EconomySpend));

            return row;
        }

        VisualElement BuildEventChip(string label, string registryAction)
        {
            var chip = new Button(() => RunActionAndRefresh(registryAction)) { text = label };
            chip.AddToClassList("sorolla-debugmenu-event-chip");
            return chip;
        }

        void RunActionAndRefresh(string registryAction)
        {
            QaActionRegistry.TryInvoke(registryAction, null, out _);
            RefreshAfterAction();
        }

        void RestartQaBridgeAndRefresh()
        {
            QaBridgeServer.Disarm();
            QaBridgeServer.Arm();
            RefreshAfterAction();
        }

        // Goal C: tab badge counts (Issues/Console) and the Console tab's own row list must live-update
        // after an action - a fired test event or an ad-show attempt lands in the event log immediately.
        // Rebuilding all four tab panes on every tap would be wasteful (and would blow away the Overview
        // tab's per-section expand memory); Console's row list + both badge counts is the surface an
        // Actions-tab tap can actually change.
        void RefreshAfterAction()
        {
            RefreshConsoleList();
            RefreshTabBadgeCounts();
        }

        enum ActionButtonStyle
        {
            Primary,
            Normal,
            Disabled,
        }

        static VisualElement BuildActionGroupTitle(string title)
        {
            var label = new Label(title);
            label.AddToClassList("sorolla-debugmenu-action-group-title");
            return label;
        }

        static VisualElement BuildActionButton(string label, string sub, ActionButtonStyle style, System.Action onClick)
        {
            var button = new VisualElement();
            button.AddToClassList("sorolla-debugmenu-action-card");
            button.AddToClassList(ActionCardClass(style));

            var textColumn = new VisualElement();
            textColumn.AddToClassList("sorolla-debugmenu-action-card-text");

            var labelEl = new Label(label);
            labelEl.AddToClassList("sorolla-debugmenu-action-card-label");
            textColumn.Add(labelEl);

            var subEl = new Label(sub);
            subEl.AddToClassList("sorolla-debugmenu-action-card-sub");
            textColumn.Add(subEl);

            button.Add(textColumn);

            if (style != ActionButtonStyle.Disabled)
            {
                // Tier-2 fix: the design source's ⧉ "copy" glyph is not in the runtime font and
                // rendered as an empty tofu box on the primary card. → IS confirmed rendering
                // correctly elsewhere in this same font (every Normal-skin card, pixel-verified in
                // prior captures), so reuse it here instead of trusting a second, unverified glyph.
                var arrow = new Label("→");
                arrow.AddToClassList("sorolla-debugmenu-action-card-arrow");
                button.Add(arrow);

                button.RegisterCallback<ClickEvent>(_ => onClick?.Invoke());
            }

            return button;
        }

        static string ActionCardClass(ActionButtonStyle style)
        {
            switch (style)
            {
                case ActionButtonStyle.Primary: return "sorolla-debugmenu-action-card-primary";
                case ActionButtonStyle.Disabled: return "sorolla-debugmenu-action-card-disabled";
                default: return "sorolla-debugmenu-action-card-normal";
            }
        }
    }
}
