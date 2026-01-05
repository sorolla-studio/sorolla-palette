using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.Palette.DebugUI
{
    /// <summary>
    ///     Shows SDK health status. Self-sufficient - checks Palette.IsInitialized on start.
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
            if (Palette.IsInitialized)
                CheckSDKStatus();
            else
                Palette.OnInitialized += CheckSDKStatus;
        }

        void OnDestroy()
        {
            Palette.OnInitialized -= CheckSDKStatus;
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
                "gameanalytics" or "ga" => Palette.IsInitialized,
                "sorolla" => Palette.IsInitialized,
                "firebase" => Palette.IsInitialized && Palette.Config != null && Palette.Config.enableFirebaseAnalytics,
                "crashlytics" => Palette.IsInitialized && Palette.Config != null && Palette.Config.enableCrashlytics,
                "remoteconfig" or "remote config" => Palette.IsRemoteConfigReady(),
                "max" or "applovin" => Palette.IsInitialized && Palette.Config != null && !Palette.Config.isPrototypeMode,
                "facebook" or "fb" => Palette.IsInitialized && Palette.Config != null && Palette.Config.isPrototypeMode,
                "adjust" => Palette.IsInitialized && Palette.Config != null && !Palette.Config.isPrototypeMode,
                _ => Palette.IsInitialized,
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
