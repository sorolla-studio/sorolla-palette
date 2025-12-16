using UnityEngine;

namespace Sorolla.DebugUI
{
    /// <summary>
    ///     Main manager for the debug panel. Handles initialization and provides
    ///     public API for showing/hiding panel and triggering actions.
    /// </summary>
    public class DebugPanelManager : MonoBehaviour
    {

        [Header("References")]
        [SerializeField] GameObject panelRoot;
        [SerializeField] TabController tabController;
        [SerializeField] ToastController toastController;
        [SerializeField] LogController logController;

        [Header("Settings")]
        [SerializeField] bool showOnStart = true;
        [SerializeField] KeyCode toggleKey = KeyCode.BackQuote;

        public static DebugPanelManager Instance { get; private set; }

        public bool IsVisible { get; private set; } = true;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            SetVisible(showOnStart);
            EnsureEventSystem();
        }

        void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current == null && Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
                DontDestroyOnLoad(eventSystem); 
            }
        }

        void Update()
        {
            // Toggle with key
            if (Input.GetKeyDown(toggleKey))
            {
                TogglePanel();
            }

            // Mobile: Triple tap to toggle
#if UNITY_IOS || UNITY_ANDROID
            if (Input.touchCount == 3)
            {
                bool allBegan = true;
                foreach (Touch touch in Input.touches)
                {
                    if (touch.phase != TouchPhase.Began)
                    {
                        allBegan = false;
                        break;
                    }
                }
                if (allBegan)
                {
                    TogglePanel();
                }
            }
#endif
        }

        // === PUBLIC API ===

        public void TogglePanel() => SetVisible(!IsVisible);

        public void SetVisible(bool visible)
        {
            IsVisible = visible;
            panelRoot.SetActive(visible);
        }

        public void ShowToast(string message, ToastType type = ToastType.Info) => SorollaDebugEvents.RaiseShowToast(message, type);

        public void Log(string message, LogSource source = LogSource.UI, LogLevel level = LogLevel.Info) => logController.Log(message, source, level);

        public void SwitchToTab(int tabIndex) => SorollaDebugEvents.RaiseTabChanged(tabIndex);

        public void UpdateSDKHealth(string sdkName, bool isHealthy) => SorollaDebugEvents.RaiseSDKHealthChanged(sdkName, isHealthy);

        public void UpdateAdStatus(AdType adType, AdStatus status) => SorollaDebugEvents.RaiseAdStatusChanged(adType, status);
    }
}
