using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.Palette.DebugUI
{
    /// <summary>
    ///     Controls an individual ad card. Self-sufficient - calls Palette API directly.
    /// </summary>
    public class AdCardController : UIComponentBase
    {
        [Header("References")]
        [SerializeField] Image accentBar;
        [SerializeField] StatusBadge statusBadge;
        [SerializeField] Button loadButton;
        [SerializeField] Button showButton;

        [Header("Configuration")]
        [SerializeField] AdType adType;
        [SerializeField] Color accentColor;

        AdStatus _currentStatus = AdStatus.Idle;

        void Awake()
        {
            loadButton.onClick.AddListener(HandleLoadClicked);
            showButton.onClick.AddListener(HandleShowClicked);
        }

        void OnDestroy()
        {
            loadButton.onClick.RemoveListener(HandleLoadClicked);
            showButton.onClick.RemoveListener(HandleShowClicked);
        }

        protected override void SubscribeToEvents() => SorollaDebugEvents.OnAdStatusChanged += HandleAdStatusChanged;

        protected override void UnsubscribeFromEvents() => SorollaDebugEvents.OnAdStatusChanged -= HandleAdStatusChanged;

        void HandleAdStatusChanged(AdType adType, AdStatus status)
        {
            if (adType == this.adType)
            {
                SetStatus(status);
            }
        }

        void HandleLoadClicked()
        {
            DebugPanelManager.Instance?.Log($"Checking {adType} status...", LogSource.Sorolla);

#if MAX_INSTALLED
            // Check if ad is already ready (MAX auto-loads)
            bool isReady = adType == AdType.Rewarded
                ? Palette.IsRewardedAdReady
                : Palette.IsInterstitialAdReady;

            if (isReady)
            {
                SetStatus(AdStatus.Loaded);
                SorollaDebugEvents.RaiseShowToast($"{adType} ready!", ToastType.Success);
                DebugPanelManager.Instance?.Log($"{adType} is ready", LogSource.Sorolla);
            }
            else
            {
                SetStatus(AdStatus.Loading);
                DebugPanelManager.Instance?.Log($"{adType} not ready yet, checking...", LogSource.Sorolla);
                // Start polling for ad readiness
                StartCoroutine(PollAdReadiness());
            }
#else
            SetStatus(AdStatus.Loading);
            // Mock: simulate ad loaded after delay (no SDK available)
            Invoke(nameof(MockAdLoaded), 1f);
#endif
        }

#if MAX_INSTALLED
        System.Collections.IEnumerator PollAdReadiness()
        {
            float timeout = 10f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                yield return new WaitForSeconds(0.5f);
                elapsed += 0.5f;

                bool isReady = adType == AdType.Rewarded
                    ? Palette.IsRewardedAdReady
                    : Palette.IsInterstitialAdReady;

                if (isReady)
                {
                    SetStatus(AdStatus.Loaded);
                    SorollaDebugEvents.RaiseShowToast($"{adType} ready!", ToastType.Success);
                    DebugPanelManager.Instance?.Log($"{adType} loaded", LogSource.Sorolla);
                    yield break;
                }
            }

            // Timeout - ad didn't load
            SetStatus(AdStatus.Failed);
            SorollaDebugEvents.RaiseShowToast($"{adType} load timeout", ToastType.Error);
            DebugPanelManager.Instance?.Log($"{adType} failed to load within {timeout}s", LogSource.Sorolla, LogLevel.Error);
        }
#endif

        void HandleShowClicked()
        {
            SetStatus(AdStatus.Showing);
            DebugPanelManager.Instance?.Log($"Showing {adType}...", LogSource.Sorolla);

#if MAX_INSTALLED
            ShowAdViaSDK();
#else
            // Mock: simulate ad completion (no SDK available)
            Invoke(nameof(MockAdComplete), 2f);
#endif
        }

#if MAX_INSTALLED
        void ShowAdViaSDK()
        {
            switch (adType)
            {
                case AdType.Interstitial:
                    Palette.ShowInterstitialAd(() =>
                    {
                        SetStatus(AdStatus.Idle);
                        SorollaDebugEvents.RaiseShowToast("Interstitial completed", ToastType.Success);
                        DebugPanelManager.Instance?.Log("Interstitial completed", LogSource.Sorolla);
                    });
                    break;

                case AdType.Rewarded:
                    Palette.ShowRewardedAd(
                        () =>
                        {
                            SetStatus(AdStatus.Idle);
                            SorollaDebugEvents.RaiseShowToast("Rewarded ad completed!", ToastType.Success);
                            DebugPanelManager.Instance?.Log("Rewarded ad - reward granted", LogSource.Sorolla);
                        },
                        () =>
                        {
                            SetStatus(AdStatus.Failed);
                            SorollaDebugEvents.RaiseShowToast("Rewarded ad failed", ToastType.Error);
                            DebugPanelManager.Instance?.Log("Rewarded ad failed", LogSource.Sorolla, LogLevel.Error);
                        });
                    break;
            }
        }
#endif

        public void SetStatus(AdStatus status)
        {
            _currentStatus = status;

            switch (status)
            {
                case AdStatus.Idle:
                    statusBadge.SetIdle();
                    break;
                case AdStatus.Loading:
                    statusBadge.SetLoading();
                    break;
                case AdStatus.Loaded:
                    statusBadge.SetLoaded();
                    break;
                case AdStatus.Showing:
                    statusBadge.SetStatus("SHOWING", Theme.accentPurple);
                    break;
                case AdStatus.Failed:
                    statusBadge.SetFailed();
                    break;
            }

            UpdateButtonStates();
        }

        void UpdateButtonStates() => showButton.interactable = _currentStatus == AdStatus.Loaded;

#if !MAX_INSTALLED
        void MockAdLoaded()
        {
            SetStatus(AdStatus.Loaded);
            SorollaDebugEvents.RaiseShowToast($"{adType} ready (mock)", ToastType.Info);
            DebugPanelManager.Instance?.Log($"{adType} loaded (mock)", LogSource.Sorolla);
        }

        void MockAdComplete()
        {
            SetStatus(AdStatus.Idle);
            SorollaDebugEvents.RaiseShowToast($"{adType} completed (mock)", ToastType.Success);
            DebugPanelManager.Instance?.Log($"{adType} completed (mock)", LogSource.Sorolla);
        }
#endif
    }
}
