using System;
using UnityEngine;
#if UNITY_IOS && UNITY_IOS_SUPPORT_INSTALLED
using Unity.Advertisement.IosSupport;
#endif

namespace Sorolla.ATT
{
    /// <summary>
    ///     Controls an iOS App Tracking Transparency context screen.
    ///     Shows a "soft prompt" explaining why tracking is needed before the system ATT dialog.
    ///     
    ///     Usage:
    ///     1. Create a UI prefab with this component
    ///     2. Add a button that calls RequestAuthorizationTracking()
    ///     3. Save the prefab to Resources/ContextScreen
    ///     
    ///     The SorollaBootstrapper will automatically load and display this prefab on iOS.
    /// </summary>
    public class ContextScreenView : MonoBehaviour
    {
        /// <summary>
        ///     Event invoked after the ContinueButton is clicked
        ///     and after the tracking authorization request has been sent.
        ///     Subscribe to this event to destroy the GameObject after it's no longer needed.
        /// </summary>
        public event Action SentTrackingAuthorizationRequest;

        /// <summary>
        ///     Call this from your "Continue" or "Allow" button.
        ///     Triggers the native iOS ATT permission dialog.
        /// </summary>
        public void RequestAuthorizationTracking()
        {
#if UNITY_IOS && UNITY_IOS_SUPPORT_INSTALLED
            Debug.Log("[Sorolla:ATT] Requesting iOS ATT dialog.");
            ATTrackingStatusBinding.RequestAuthorizationTracking();
            SentTrackingAuthorizationRequest?.Invoke();
#else
            Debug.LogWarning("[Sorolla:ATT] ATT only available on iOS.");
            SentTrackingAuthorizationRequest?.Invoke();
#endif
        }
    }
}
