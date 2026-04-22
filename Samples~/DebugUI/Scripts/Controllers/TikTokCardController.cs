using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.Palette.DebugUI
{
    /// <summary>
    ///     Controls TikTok Business SDK test buttons for event tracking and purchase.
    /// </summary>
    public class TikTokCardController : UIComponentBase
    {
        [Header("Buttons")]
        [SerializeField] Button trackEventButton;
        [SerializeField] Button trackPurchaseButton;

        [Header("Test Configuration")]
        [SerializeField] string customEventName = "LevelComplete";

        void Awake()
        {
            trackEventButton.onClick.AddListener(TrackTestEvent);
            trackPurchaseButton.onClick.AddListener(TrackTestPurchase);
        }

        void OnDestroy()
        {
            trackEventButton.onClick.RemoveListener(TrackTestEvent);
            trackPurchaseButton.onClick.RemoveListener(TrackTestPurchase);
        }

        void TrackTestEvent()
        {
            Palette.TrackEvent(customEventName);

            DebugPanelManager.Instance?.Log($"Event: {customEventName}", LogSource.TikTok);
            SorollaDebugEvents.RaiseShowToast($"Tracked: {customEventName}", ToastType.Success);
        }

        void TrackTestPurchase()
        {
            DebugPanelManager.Instance?.Log(
                "Palette.TrackPurchase is internal since 3.14.1. Wire Palette.AttachPurchaseTracking(storeController) and trigger a real Unity IAP purchase to test fan-out to TikTok.",
                LogSource.TikTok, LogLevel.Warning);
            SorollaDebugEvents.RaiseShowToast("Use AttachPurchaseTracking + real IAP", ToastType.Warning);
        }
    }
}
