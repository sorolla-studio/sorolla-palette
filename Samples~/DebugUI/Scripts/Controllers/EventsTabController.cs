using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Sorolla.DebugUI
{
    /// <summary>
    ///     Controls the Events tab buttons. Self-sufficient - calls SorollaSDK tracking API directly.
    /// </summary>
    public class EventsTabController : UIComponentBase
    {
        [FormerlySerializedAs("_startButton")]
        [Header("Progression Buttons")]
        [SerializeField] Button startButton;
        [FormerlySerializedAs("_winButton")] [SerializeField] Button winButton;
        [FormerlySerializedAs("_failButton")] [SerializeField] Button failButton;

        [FormerlySerializedAs("_addCoinsButton")]
        [Header("Resource Buttons")]
        [SerializeField] Button addCoinsButton;
        [FormerlySerializedAs("_spendCoinsButton")] [SerializeField] Button spendCoinsButton;


        [Header("Custom Event Buttons")]
        [SerializeField] Button designButton;
        [SerializeField] Button designValueButton;

        void Awake()
        {

            // Progression
            startButton.onClick.AddListener(() => TrackProgression(ProgressionStatus.Start));
            winButton.onClick.AddListener(() => TrackProgression(ProgressionStatus.Complete));
            failButton.onClick.AddListener(() => TrackProgression(ProgressionStatus.Fail));

            // Resources
            addCoinsButton.onClick.AddListener(() => TrackResource(ResourceFlowType.Source, "coins", 100));
            spendCoinsButton.onClick.AddListener(() => TrackResource(ResourceFlowType.Sink, "coins", 50));

            // Custom
            designButton.onClick.AddListener(() => TrackDesign("npc_talk"));
            designValueButton.onClick.AddListener(() => TrackDesign("tuto_step", 3));
        }

        void OnDestroy()
        {
            startButton.onClick.RemoveAllListeners();
            winButton.onClick.RemoveAllListeners();
            failButton.onClick.RemoveAllListeners();
            addCoinsButton.onClick.RemoveAllListeners();
            spendCoinsButton.onClick.RemoveAllListeners();
            designButton.onClick.RemoveAllListeners();
            designValueButton.onClick.RemoveAllListeners();
        }

        void TrackProgression(ProgressionStatus status)
        {
            string levelName = "Level_01";
            SorollaSDK.TrackProgression(status, levelName);

            string statusName = status.ToString();
            DebugPanelManager.Instance?.Log($"Progression: {statusName} ({levelName})", LogSource.GA);
            SorollaDebugEvents.RaiseShowToast($"Tracked: {statusName}", ToastType.Success);
        }

        void TrackResource(ResourceFlowType flowType, string currency, float amount)
        {
            SorollaSDK.TrackResource(flowType, currency, amount, "debug", "test_item");

            string action = flowType == ResourceFlowType.Source ? "+" : "-";
            DebugPanelManager.Instance?.Log($"Resource: {action}{amount} {currency}", LogSource.GA);
            SorollaDebugEvents.RaiseShowToast($"{action}{amount} {currency}", ToastType.Info);
        }

        void TrackDesign(string eventName, float value = 0)
        {
            SorollaSDK.TrackDesign(eventName, value);

            DebugPanelManager.Instance?.Log($"Design Event: {eventName}", LogSource.GA);
            SorollaDebugEvents.RaiseShowToast($"Tracked: {eventName}", ToastType.Success);
        }
    }

}
