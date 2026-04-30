using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Sorolla.Palette.DebugUI
{
    /// <summary>
    ///     Controls the Events tab buttons. Self-sufficient - calls Palette tracking API directly.
    /// </summary>
    public class EventsTabController : UIComponentBase
    {
        [FormerlySerializedAs("_startButton")]
        [Header("Progression")]
        [SerializeField] Button startButton;
        [FormerlySerializedAs("_winButton")] [SerializeField] Button winButton;
        [FormerlySerializedAs("_failButton")] [SerializeField] Button failButton;

        [FormerlySerializedAs("_addCoinsButton")]
        [Header("Resources")]
        [SerializeField] Button addCoinsButton;
        [FormerlySerializedAs("_spendCoinsButton")] [SerializeField] Button spendCoinsButton;

        [Header("Custom Events")]
        [SerializeField] Button trackEventButton;
        [SerializeField] Button trackEventValueButton;

        [Header("Purchase")]
        [SerializeField] Button purchaseButton;

        [Header("User Identity")]
        [SerializeField] Button setUserIdButton;
        [SerializeField] Button setUserPropertyButton;

        [Header("Validation")]
        [SerializeField] Button badEventButton;

        void Awake()
        {
            // Progression
            startButton.onClick.AddListener(OnStartClicked);
            winButton.onClick.AddListener(OnWinClicked);
            failButton.onClick.AddListener(OnFailClicked);

            // Resources
            addCoinsButton.onClick.AddListener(OnAddCoinsClicked);
            spendCoinsButton.onClick.AddListener(OnSpendCoinsClicked);

            // Custom Events
            trackEventButton.onClick.AddListener(OnTrackEventClicked);
            trackEventValueButton.onClick.AddListener(OnTrackEventValueClicked);

            // Purchase
            purchaseButton.onClick.AddListener(OnPurchaseClicked);

            // User Identity
            setUserIdButton.onClick.AddListener(OnSetUserIdClicked);
            setUserPropertyButton.onClick.AddListener(OnSetUserPropertyClicked);

            // Validation
            badEventButton.onClick.AddListener(OnBadEventClicked);
        }

        void OnDestroy()
        {
            startButton.onClick.RemoveAllListeners();
            winButton.onClick.RemoveAllListeners();
            failButton.onClick.RemoveAllListeners();
            addCoinsButton.onClick.RemoveAllListeners();
            spendCoinsButton.onClick.RemoveAllListeners();
            trackEventButton.onClick.RemoveAllListeners();
            trackEventValueButton.onClick.RemoveAllListeners();
            purchaseButton.onClick.RemoveAllListeners();
            setUserIdButton.onClick.RemoveAllListeners();
            setUserPropertyButton.onClick.RemoveAllListeners();
            badEventButton.onClick.RemoveAllListeners();
        }

        #region Progression

        const int DebugLevel = 3;
        const int DebugWorld = 1;

        void OnStartClicked()
        {
            Palette.Level.Start(DebugLevel, DebugWorld);

            DebugPanelManager.Instance?.Log("Level.Start (world=1, level=3)", LogSource.GA);
            SorollaDebugEvents.RaiseShowToast("Tracked: Start", ToastType.Success);
        }

        void OnWinClicked()
        {
            Palette.Level.Complete(DebugLevel, DebugWorld, score: 1250);

            DebugPanelManager.Instance?.Log("Level.Complete (score=1250, auto-duration)", LogSource.GA);
            SorollaDebugEvents.RaiseShowToast("Tracked: Complete", ToastType.Success);
        }

        void OnFailClicked()
        {
            Palette.Level.Fail(DebugLevel, DebugWorld, score: 400);

            DebugPanelManager.Instance?.Log("Level.Fail (score=400, auto-duration)", LogSource.GA);
            SorollaDebugEvents.RaiseShowToast("Tracked: Fail", ToastType.Success);
        }

        #endregion

        #region Resources

        void OnAddCoinsClicked()
        {
            Palette.Economy.Earn(CurrencyId.Coins, 100, EconomySource.LevelReward, itemId: "world_1_level_03");

            DebugPanelManager.Instance?.Log("Economy.Earn: +100 coins (LevelReward)", LogSource.GA);
            SorollaDebugEvents.RaiseShowToast("+100 coins", ToastType.Info);
        }

        void OnSpendCoinsClicked()
        {
            Palette.Economy.Spend(CurrencyId.Coins, 50, EconomySink.Booster, itemId: "speed_boost");

            DebugPanelManager.Instance?.Log("Economy.Spend: -50 coins (Booster)", LogSource.GA);
            SorollaDebugEvents.RaiseShowToast("-50 coins", ToastType.Info);
        }

        #endregion

        #region Custom Events

        void OnTrackEventClicked()
        {
            Palette.TrackEvent("booster_used", new Dictionary<string, object>
            {
                { "booster_id", "speed_boost" },
                { "world", "world_1" },
                { "level", "level_03" },
                { "game_mode", "classic" }
            });

            DebugPanelManager.Instance?.Log("Event: booster_used (4 params)", LogSource.GA);
            SorollaDebugEvents.RaiseShowToast("Tracked: booster_used", ToastType.Success);
        }

        void OnTrackEventValueClicked()
        {
            Palette.TrackEvent("post_score", new Dictionary<string, object>
            {
                { "score", 1250 },
                { "level_name", "world_1_level_03" },
                { "level", 3 }
            });

            DebugPanelManager.Instance?.Log("Event: post_score (score=1250)", LogSource.GA);
            SorollaDebugEvents.RaiseShowToast("Tracked: post_score", ToastType.Success);
        }

        #endregion

        #region Purchase

        void OnPurchaseClicked()
        {
            DebugPurchaseTester.Purchase(LogSource.Firebase);
        }

        #endregion

        #region User Identity

        void OnSetUserIdClicked()
        {
            Palette.SetUserId("debug_user_42");

            DebugPanelManager.Instance?.Log("SetUserId: debug_user_42", LogSource.Firebase);
            SorollaDebugEvents.RaiseShowToast("User ID set", ToastType.Success);
        }

        void OnSetUserPropertyClicked()
        {
            Palette.SetUserProperty("player_level", "15");

            DebugPanelManager.Instance?.Log("SetUserProperty: player_level = 15", LogSource.Firebase);
            SorollaDebugEvents.RaiseShowToast("User property set", ToastType.Success);
        }

        #endregion

        #region Validation

        void OnBadEventClicked()
        {
            // Intentionally uses reserved prefix - should be rejected with Debug.LogError
            Palette.TrackEvent("firebase_test", new Dictionary<string, object> { { "key", "value" } });

            DebugPanelManager.Instance?.Log("Sent reserved-prefix event (check for rejection)", LogSource.GA, LogLevel.Warning);
            SorollaDebugEvents.RaiseShowToast("Sent (check log)", ToastType.Warning);
        }

        #endregion
    }
}
