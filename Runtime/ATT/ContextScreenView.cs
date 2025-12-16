using System;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_IOS && UNITY_IOS_SUPPORT_INSTALLED
using Unity.Advertisement.IosSupport;
#endif

namespace Sorolla.ATT
{
    /// <summary>
    ///     Controls an iOS App Tracking Transparency context screen.
    ///     Shows a "soft prompt" explaining why tracking is needed before the system ATT dialog.
    ///     Usage:
    ///     1. Create a UI prefab with this component
    ///     2. Add a button that calls RequestAuthorizationTracking()
    ///     3. Save the prefab to Resources/ContextScreen
    ///     The SorollaBootstrapper will automatically load and display this prefab on iOS.
    /// </summary>
    public class ContextScreenView : MonoBehaviour
    {
        [SerializeField] Button button;

        /// <summary>
        ///     Event invoked after the ContinueButton is clicked
        ///     and after the tracking authorization request has been sent.
        ///     Subscribe to this event to destroy the GameObject after it's no longer needed.
        /// </summary>
        public event Action SentTrackingAuthorizationRequest;

        void Awake() => button.onClick.AddListener(RequestAuthorizationTracking);

        /// <summary>
        ///     Call this from your "Continue" or "Allow" button.
        ///     Triggers the native iOS ATT permission dialog (or Fake ATT in Editor).
        /// </summary>
        public void RequestAuthorizationTracking()
        {
#if UNITY_IOS && UNITY_IOS_SUPPORT_INSTALLED && !UNITY_EDITOR
            Debug.Log("[Sorolla:ATT] Requesting iOS ATT dialog.");
            ATTrackingStatusBinding.RequestAuthorizationTracking();
            SentTrackingAuthorizationRequest?.Invoke();
#else
            // Editor, Android, and other platforms: show fake ATT for testing
            Debug.Log("[Sorolla:ATT] Showing Fake ATT dialog (non-iOS).");
            FakeATTDialog.Show(_ => SentTrackingAuthorizationRequest?.Invoke());
#endif
        }
    }
}
