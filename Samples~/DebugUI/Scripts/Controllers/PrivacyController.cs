using Sorolla.ATT;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_IOS && !UNITY_EDITOR && UNITY_IOS_SUPPORT_INSTALLED
using Unity.Advertisement.IosSupport;
#endif

namespace Sorolla.DebugUI
{
    /// <summary>
    ///     Controls the Privacy & CMP section.
    ///     Editor: Shows test buttons for ATT/CMP flows.
    ///     Builds: Shows current ATT status and settings access.
    /// </summary>
    public class PrivacyController : UIComponentBase
    {
        [Header("Editor-Only Buttons")]
        [SerializeField] Button showATTButton;
        [SerializeField] Button resetConsentButton;

        [Header("Always Visible")]
        [SerializeField] Button showCMPButton;
        [SerializeField] Button openSettingsButton;

        [Header("Status Display (Builds)")]
        [SerializeField] TextMeshProUGUI attStatusText;
        [SerializeField] GameObject attStatusContainer;

        void Awake()
        {
            showCMPButton.onClick.AddListener(HandleShowCMP);

#if UNITY_EDITOR
            showATTButton.onClick.AddListener(HandleShowATT);
            resetConsentButton.onClick.AddListener(HandleResetConsent);
            openSettingsButton.gameObject.SetActive(false);
            showATTButton.gameObject.SetActive(true);
            resetConsentButton.gameObject.SetActive(true);
            attStatusContainer.SetActive(false);
#else
            openSettingsButton.gameObject.SetActive(true);
            openSettingsButton.onClick.AddListener(HandleOpenSettings);
            showATTButton.gameObject.SetActive(false);
            resetConsentButton.gameObject.SetActive(false);
#if UNITY_IOS && UNITY_IOS_SUPPORT_INSTALLED
            UpdateATTStatus();
            attStatusContainer.SetActive(true);
#else
            attStatusContainer.SetActive(false); // No ATT on non-iOS platforms
#endif
#endif
        }

        void OnDestroy()
        {
            showCMPButton.onClick.RemoveAllListeners();
            openSettingsButton.onClick.RemoveAllListeners();
#if UNITY_EDITOR
            showATTButton.onClick.RemoveAllListeners();
            resetConsentButton.onClick.RemoveAllListeners();
#endif
        }

#if UNITY_IOS && UNITY_IOS_SUPPORT_INSTALLED && !UNITY_EDITOR
        void UpdateATTStatus()
        {
            var status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
            string statusStr = status switch
            {
                ATTrackingStatusBinding.AuthorizationTrackingStatus.AUTHORIZED => "Authorized",
                ATTrackingStatusBinding.AuthorizationTrackingStatus.DENIED => "Denied",
                ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED => "Not Determined",
                ATTrackingStatusBinding.AuthorizationTrackingStatus.RESTRICTED => "Restricted",
                _ => "Unknown"
            };
            attStatusText.text = $"{statusStr}";
        }
#endif

        void HandleOpenSettings()
        {
            DebugPanelManager.Instance?.Log("Opening app settings...", LogSource.Sorolla);
#if UNITY_IOS
            // Open iOS Settings app to this app's settings page
            Application.OpenURL("app-settings:");
#elif UNITY_ANDROID
            // Open Android app settings
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = new AndroidJavaObject("android.content.Intent", "android.settings.APPLICATION_DETAILS_SETTINGS");
                using var uri = new AndroidJavaClass("android.net.Uri").CallStatic<AndroidJavaObject>("parse", "package:" + Application.identifier);
                intent.Call<AndroidJavaObject>("setData", uri);
                currentActivity.Call("startActivity", intent);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PrivacyController] Failed to open settings: {e.Message}");
            }
#endif
        }

#if UNITY_EDITOR
        void HandleShowATT()
        {
            DebugPanelManager.Instance?.Log("Showing PreATT -> ATT flow...", LogSource.Sorolla);

            var prefab = Resources.Load<GameObject>("ContextScreen");
            if (prefab == null)
            {
                DebugPanelManager.Instance?.Log("ContextScreen prefab not found, showing ATT directly", LogSource.Sorolla, LogLevel.Warning);
                ShowFakeATT();
                return;
            }

            GameObject contextScreen = Object.Instantiate(prefab);
            var canvas = contextScreen.GetComponent<Canvas>();
            if (canvas) canvas.sortingOrder = 999;

            var view = contextScreen.GetComponent<ContextScreenView>();
            view.SentTrackingAuthorizationRequest += () =>
            {
                Object.Destroy(contextScreen);
                DebugPanelManager.Instance?.Log("ATT flow completed", LogSource.Sorolla);
            };
        }

        void ShowFakeATT()
        {
            FakeATTDialog.Show(allowed =>
            {
                string result = allowed ? "Allowed" : "Denied";
                DebugPanelManager.Instance?.Log($"ATT Result: {result}", LogSource.Sorolla);
                SorollaDebugEvents.RaiseShowToast($"ATT: {result}", allowed ? ToastType.Success : ToastType.Warning);
            });
        }

        void HandleResetConsent()
        {
            DebugPanelManager.Instance?.Log("Consent reset (mock)", LogSource.Sorolla);
            SorollaDebugEvents.RaiseShowToast("Consent Reset", ToastType.Info);
        }
#endif

        void HandleShowCMP()
        {
            DebugPanelManager.Instance?.Log("Showing CMP dialog...", LogSource.Sorolla);
            FakeCMPDialog.Show(accepted =>
            {
                string result = accepted ? "Accepted" : "Rejected";
                DebugPanelManager.Instance?.Log($"CMP Result: {result}", LogSource.Sorolla);
                SorollaDebugEvents.RaiseShowToast($"CMP: {result}", accepted ? ToastType.Success : ToastType.Warning);
            });
        }
    }
}
