using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sorolla.Palette
{
    // The registry owns the action catalog and invocation. This partial only supplies presentation
    // metadata and the two local diagnostics utilities that are not bridge actions.
    internal sealed partial class SorollaDebugMenuOverlay
    {
        enum ActionGroup
        {
            Ads,
            Consent,
            Events,
        }

        enum ActionButtonStyle
        {
            Primary,
            Normal,
        }

        readonly struct ActionPresentation
        {
            public readonly ActionGroup Group;
            public readonly string Label;
            public readonly string Detail;

            public ActionPresentation(ActionGroup group, string label, string detail)
            {
                Group = group;
                Label = label;
                Detail = detail;
            }
        }

        Label _rewardedActionDetail;
        Label _interstitialActionDetail;
        Label _consentActionDetail;
        Label _bridgeActionLabel;
        Label _bridgeActionDetail;

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
                "Refresh diagnostics",
                "Refresh identifiers and rebuild the Overview and Issues facts",
                ActionButtonStyle.Normal,
                RefreshDiagnostics).Root);
            host.Add(BuildActionButton(
                "Copy diagnostics summary",
                "Complete diagnostic rows, runtime problems, and recent events",
                ActionButtonStyle.Normal,
                () => GUIUtility.systemCopyBuffer = SorollaDiagnostics.BuildSummary()).Root);
            host.Add(BuildActionButton(
                "Copy SDK state",
                "Full report: context, consent, adapters, config, events, problems",
                ActionButtonStyle.Primary,
                () => GUIUtility.systemCopyBuffer = SorollaDiagnostics.BuildQaStateSummary()).Root);

            ActionGroup? currentGroup = null;
            foreach (string actionName in QaActionRegistry.ActionNames)
            {
                ActionPresentation presentation = PresentationFor(actionName);
                if (currentGroup != presentation.Group)
                {
                    if (presentation.Group == ActionGroup.Events)
                        AddBridgeUtility(host);

                    host.Add(BuildActionGroupTitle(GroupLabel(presentation.Group)));
                    currentGroup = presentation.Group;
                }

                ActionCard card = BuildActionButton(
                    presentation.Label,
                    presentation.Detail,
                    ActionButtonStyle.Normal,
                    () => RunActionAndRefresh(actionName));
                BindDynamicActionDetail(actionName, card.Detail);
                host.Add(card.Root);
            }

            RefreshActionState();
            return pane;
        }

        void AddBridgeUtility(VisualElement host)
        {
            host.Add(BuildActionGroupTitle("QA BRIDGE"));
            ActionCard bridge = BuildActionButton(
                "Bridge",
                string.Empty,
                ActionButtonStyle.Normal,
                RestartQaBridgeAndRefresh);
            _bridgeActionLabel = bridge.Label;
            _bridgeActionDetail = bridge.Detail;
            host.Add(bridge.Root);
        }

        void BindDynamicActionDetail(string actionName, Label detail)
        {
            switch (actionName)
            {
                case QaActionRegistry.ShowRewarded:
                    _rewardedActionDetail = detail;
                    break;
                case QaActionRegistry.ShowInterstitial:
                    _interstitialActionDetail = detail;
                    break;
                case QaActionRegistry.RefreshConsent:
                    _consentActionDetail = detail;
                    break;
            }
        }

        static ActionPresentation PresentationFor(string actionName)
        {
            switch (actionName)
            {
                case QaActionRegistry.ShowRewarded:
                    return new ActionPresentation(ActionGroup.Ads, "Show rewarded", string.Empty);
                case QaActionRegistry.ShowInterstitial:
                    return new ActionPresentation(ActionGroup.Ads, "Show interstitial", string.Empty);
                case QaActionRegistry.OpenPrivacyOptions:
                    return new ActionPresentation(ActionGroup.Consent, "Show privacy options", "Re-opens the consent form");
                case QaActionRegistry.ResetConsent:
                    return new ActionPresentation(ActionGroup.Consent, "Reset consent", "Re-opens the CMP and records a consent reset");
                case QaActionRegistry.RefreshConsent:
                    return new ActionPresentation(ActionGroup.Consent, "Refresh consent status", string.Empty);
                case QaActionRegistry.TrackTestEvent:
                    return new ActionPresentation(ActionGroup.Events, "Fire test event", "sorolla_vitals_test — visible in Console + vendor dashboards");
                case QaActionRegistry.LevelStart:
                    return new ActionPresentation(ActionGroup.Events, "Level start", "Fires level_start for the test level");
                case QaActionRegistry.LevelComplete:
                    return new ActionPresentation(ActionGroup.Events, "Level complete", "Fires level_end for the test level");
                case QaActionRegistry.EconomyEarn:
                    return new ActionPresentation(ActionGroup.Events, "Economy earn", "Fires an economy earn event");
                case QaActionRegistry.EconomySpend:
                    return new ActionPresentation(ActionGroup.Events, "Economy spend", "Fires an economy spend event");
                default:
                    throw new ArgumentOutOfRangeException(nameof(actionName), actionName, "QA action has no debug-menu presentation.");
            }
        }

        static string GroupLabel(ActionGroup group)
        {
            switch (group)
            {
                case ActionGroup.Ads: return "ADS TEST";
                case ActionGroup.Consent: return "CONSENT";
                default: return "EVENTS";
            }
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

        void RefreshDiagnostics()
        {
            SorollaDiagnostics.RefreshIdentifiers();
            RefreshDiagnosticViews();
            RefreshAfterAction();
        }

        void RefreshAfterAction()
        {
            RefreshActionState();
            RefreshConsoleList(true);
            RefreshTabBadgeCounts();
        }

        void RefreshActionState()
        {
            if (_rewardedActionDetail != null)
                _rewardedActionDetail.text = Palette.IsRewardedAdReady
                    ? "Loaded — ready to show"
                    : "Not loaded — tap to probe";
            if (_interstitialActionDetail != null)
                _interstitialActionDetail.text = Palette.IsInterstitialAdReady
                    ? "Loaded — ready to show"
                    : "Not loaded — tap to probe";
            if (_consentActionDetail != null)
                _consentActionDetail.text = Palette.ConsentStatus
                    + (Palette.CanRequestAds ? " · can request ads" : " · cannot request ads");

            if (_bridgeActionLabel == null || _bridgeActionDetail == null) return;
            bool bridgeArmed = QaBridgeServer.IsArmed;
            _bridgeActionLabel.text = bridgeArmed ? "Bridge running" : "Bridge not running";
            _bridgeActionDetail.text = bridgeArmed
                ? $"127.0.0.1:{QaBridgeServer.Port} — serves this same data as JSON"
                : "Bind failed or unavailable — tap to retry";
        }

        static VisualElement BuildActionGroupTitle(string title)
        {
            var label = new Label(title);
            label.AddToClassList("sorolla-debugmenu-action-group-title");
            return label;
        }

        readonly struct ActionCard
        {
            public readonly VisualElement Root;
            public readonly Label Label;
            public readonly Label Detail;

            public ActionCard(VisualElement root, Label label, Label detail)
            {
                Root = root;
                Label = label;
                Detail = detail;
            }
        }

        static ActionCard BuildActionButton(string label, string detail, ActionButtonStyle style, Action onClick)
        {
            var root = new VisualElement();
            root.AddToClassList("sorolla-debugmenu-action-card");
            root.AddToClassList(ActionCardClass(style));

            var textColumn = new VisualElement();
            textColumn.AddToClassList("sorolla-debugmenu-action-card-text");

            var labelElement = new Label(label);
            labelElement.AddToClassList("sorolla-debugmenu-action-card-label");
            textColumn.Add(labelElement);

            var detailElement = new Label(detail);
            detailElement.AddToClassList("sorolla-debugmenu-action-card-sub");
            textColumn.Add(detailElement);
            root.Add(textColumn);

            var arrow = new Label("→");
            arrow.AddToClassList("sorolla-debugmenu-action-card-arrow");
            root.Add(arrow);
            root.RegisterCallback<ClickEvent>(_ => onClick?.Invoke());

            return new ActionCard(root, labelElement, detailElement);
        }

        static string ActionCardClass(ActionButtonStyle style) =>
            style == ActionButtonStyle.Primary
                ? "sorolla-debugmenu-action-card-primary"
                : "sorolla-debugmenu-action-card-normal";
    }
}
