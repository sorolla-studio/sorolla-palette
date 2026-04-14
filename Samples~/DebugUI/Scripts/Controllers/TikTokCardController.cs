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
            Palette.TrackPurchase(0.99, "USD");

            DebugPanelManager.Instance?.Log("Purchase: $0.99 USD", LogSource.TikTok);
            SorollaDebugEvents.RaiseShowToast("$0.99 purchase tracked", ToastType.Success);
        }
    }
}
