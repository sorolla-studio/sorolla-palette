using Sorolla.Adapters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.DebugUI
{
    /// <summary>
    ///     Controls Adjust SDK attribution display and test event buttons.
    ///     ADID is handled by IdentityCardController with AdjustId type.
    /// </summary>
    public class AdjustCardController : UIComponentBase
    {
        [Header("Display")]
        [SerializeField] TextMeshProUGUI attributionText;

        [Header("Buttons")]
        [SerializeField] Button getAttributionButton;
        [SerializeField] Button trackEventButton;
        [SerializeField] Button trackRevenueButton;

        [Header("Test Configuration")]
        [SerializeField] string testEventToken = "";
        [SerializeField] string revenueEventToken = "";

        void Awake()
        {
            getAttributionButton.onClick.AddListener(FetchAttribution);
            trackEventButton.onClick.AddListener(TrackTestEvent);
            trackRevenueButton.onClick.AddListener(TrackTestRevenue);
        }

        void OnDestroy()
        {
            getAttributionButton.onClick.RemoveListener(FetchAttribution);
            trackEventButton.onClick.RemoveListener(TrackTestEvent);
            trackRevenueButton.onClick.RemoveListener(TrackTestRevenue);
        }

        void FetchAttribution()
        {
            attributionText.text = "Fetching...";

            AdjustAdapter.GetAttribution(attr =>
            {
                if (attr == null)
                {
                    attributionText.text = "No attribution yet";
                    SorollaDebugEvents.RaiseShowToast("No attribution data", ToastType.Warning);
                    return;
                }

#if SOROLLA_ADJUST_ENABLED
                string display = $"Net: {attr.Network ?? "—"}\n" +
                                 $"Camp: {attr.Campaign ?? "—"}\n" +
                                 $"Tracker: {attr.TrackerName ?? "—"}";
                attributionText.text = display;

                DebugPanelManager.Instance?.Log($"Attribution: {attr.Network}/{attr.Campaign}", LogSource.Adjust);
#else
                attributionText.text = "Adjust not enabled";
#endif
                SorollaDebugEvents.RaiseShowToast("Attribution retrieved", ToastType.Success);
                
                var rectTransform = (RectTransform)attributionText.transform;
                while (rectTransform != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
                    rectTransform = rectTransform.parent as RectTransform;
                }
            });
        }

        void TrackTestEvent()
        {
            if (string.IsNullOrEmpty(testEventToken))
            {
                SorollaDebugEvents.RaiseShowToast("No event token configured", ToastType.Error);
                return;
            }

            AdjustAdapter.TrackEvent(testEventToken);

            DebugPanelManager.Instance?.Log($"Event: {testEventToken}", LogSource.Adjust);
            SorollaDebugEvents.RaiseShowToast("Event tracked", ToastType.Success);
        }

        void TrackTestRevenue()
        {
            if (string.IsNullOrEmpty(revenueEventToken))
            {
                SorollaDebugEvents.RaiseShowToast("No revenue token configured", ToastType.Error);
                return;
            }

            AdjustAdapter.TrackRevenue(revenueEventToken, 0.99);

            DebugPanelManager.Instance?.Log($"Revenue: $0.99 ({revenueEventToken})", LogSource.Adjust);
            SorollaDebugEvents.RaiseShowToast("$0.99 revenue tracked", ToastType.Success);
        }
    }
}
