using Sorolla.Palette.Adapters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.Palette.DebugUI
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

            Palette.GetAttribution(attr =>
            {
                if (attr == null)
                {
                    attributionText.text = "No attribution yet";
                    SorollaDebugEvents.RaiseShowToast("No attribution data", ToastType.Warning);
                    return;
                }

                AttributionData attribution = attr.Value;
                string display = $"Net: {attribution.Network ?? "—"}\n" +
                                 $"Camp: {attribution.Campaign ?? "—"}\n" +
                                 $"Tracker: {attribution.TrackerName ?? "—"}";
                attributionText.text = display;

                DebugPanelManager.Instance?.Log($"Attribution: {attribution.Network}/{attribution.Campaign}", LogSource.Adjust);
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

            Palette.TrackEvent($"adjust_test_{testEventToken}");

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

            DebugPanelManager.Instance?.Log(
                "Palette.TrackPurchase is internal since 3.14.1. Wire Palette.AttachPurchaseTracking(storeController) and trigger a real Unity IAP purchase to test revenue.",
                LogSource.Adjust, LogLevel.Warning);
            SorollaDebugEvents.RaiseShowToast("Use AttachPurchaseTracking + real IAP", ToastType.Warning);
        }
    }
}
