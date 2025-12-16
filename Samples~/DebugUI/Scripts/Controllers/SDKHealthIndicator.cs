using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.DebugUI
{
    /// <summary>
    ///     Shows SDK health status. Self-sufficient - checks SorollaSDK.IsInitialized on start.
    /// </summary>
    public class SDKHealthIndicator : UIComponentBase
    {
        [SerializeField] Image _background;
        [SerializeField] TextMeshProUGUI _sdkNameLabel;
        [SerializeField] Image _statusDot;

        [SerializeField] string _sdkName;
        [SerializeField] bool _isEnabled = true;
        [SerializeField] bool _isHealthy;

        void Start()
        {
            if (SorollaSDK.IsInitialized)
                CheckSDKStatus();
            else
                SorollaSDK.OnInitialized += CheckSDKStatus;
        }

        void OnDestroy()
        {
            SorollaSDK.OnInitialized -= CheckSDKStatus;
        }

        protected override void SubscribeToEvents() => SorollaDebugEvents.OnSDKHealthChanged += HandleSDKHealthChanged;

        protected override void UnsubscribeFromEvents() => SorollaDebugEvents.OnSDKHealthChanged -= HandleSDKHealthChanged;

        void HandleSDKHealthChanged(string sdkName, bool isHealthy)
        {
            if (sdkName == _sdkName)
            {
                SetHealth(isHealthy);
            }
        }

        void CheckSDKStatus()
        {
            // Check actual SDK status based on name
            bool isHealthy = _sdkName.ToLower() switch
            {
                "gameanalytics" or "ga" => SorollaSDK.IsInitialized,
                "sorolla" => SorollaSDK.IsInitialized,
                "firebase" => SorollaSDK.IsInitialized && SorollaSDK.Config != null && SorollaSDK.Config.enableFirebaseAnalytics,
                "crashlytics" => SorollaSDK.IsInitialized && SorollaSDK.Config != null && SorollaSDK.Config.enableCrashlytics,
                "remoteconfig" or "remote config" => SorollaSDK.IsRemoteConfigReady(),
                "max" or "applovin" => SorollaSDK.IsInitialized && SorollaSDK.Config != null && !SorollaSDK.Config.isPrototypeMode,
                "facebook" or "fb" => SorollaSDK.IsInitialized && SorollaSDK.Config != null && SorollaSDK.Config.isPrototypeMode,
                "adjust" => SorollaSDK.IsInitialized && SorollaSDK.Config != null && !SorollaSDK.Config.isPrototypeMode,
                _ => SorollaSDK.IsInitialized,
            };

            _isHealthy = isHealthy;
            UpdateVisual();
        }

        public void Setup(string sdkName, bool enabled, bool healthy)
        {
            _sdkName = sdkName;
            _isEnabled = enabled;
            _isHealthy = healthy;
            UpdateVisual();
        }

        public void SetHealth(bool healthy)
        {
            _isHealthy = healthy;
            UpdateVisual();
        }

        void UpdateVisual()
        {
            _sdkNameLabel.text = _sdkName;
            _sdkNameLabel.color = _isEnabled ? Theme.textPrimary : Theme.textDisabled;
            _statusDot.color = _isEnabled
                ? _isHealthy ? Theme.statusActive : Theme.statusIdle
                : Theme.statusIdle;
        }
    }
}
