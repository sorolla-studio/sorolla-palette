using Sorolla.Palette.Adapters;
using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.Palette.DebugUI
{
    /// <summary>
    ///     Controls TikTok Business SDK test buttons for event tracking, purchase, and ad revenue.
    /// </summary>
    public class TikTokCardController : UIComponentBase
    {
        [Header("Buttons")]
        [SerializeField] Button trackEventButton;
        [SerializeField] Button trackPurchaseButton;
        [SerializeField] Button trackAdRevenueButton;

        [Header("Test Configuration")]
        [SerializeField] string customEventName = "LevelComplete";

        void Awake()
        {
            trackEventButton.onClick.AddListener(TrackTestEvent);
            trackPurchaseButton.onClick.AddListener(TrackTestPurchase);
            trackAdRevenueButton.onClick.AddListener(TrackTestAdRevenue);
        }

        void OnDestroy()
        {
            trackEventButton.onClick.RemoveListener(TrackTestEvent);
            trackPurchaseButton.onClick.RemoveListener(TrackTestPurchase);
            trackAdRevenueButton.onClick.RemoveListener(TrackTestAdRevenue);
        }

        void TrackTestEvent()
        {
            TikTokAdapter.TrackEvent(customEventName);

            DebugPanelManager.Instance?.Log($"Event: {customEventName}", LogSource.TikTok);
            SorollaDebugEvents.RaiseShowToast($"Tracked: {customEventName}", ToastType.Success);
        }

        void TrackTestPurchase()
        {
            TikTokAdapter.TrackPurchase(0.99);

            DebugPanelManager.Instance?.Log("Purchase: $0.99 USD", LogSource.TikTok);
            SorollaDebugEvents.RaiseShowToast("$0.99 purchase tracked", ToastType.Success);
        }

        void TrackTestAdRevenue()
        {
            TikTokAdapter.TrackAdRevenue(0.01);

            DebugPanelManager.Instance?.Log("Ad Revenue: $0.01 USD", LogSource.TikTok);
            SorollaDebugEvents.RaiseShowToast("$0.01 ad revenue tracked", ToastType.Success);
        }
    }
}
