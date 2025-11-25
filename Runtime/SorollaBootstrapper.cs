using System.Collections;
using UnityEngine;
#if UNITY_IOS && UNITY_IOS_SUPPORT_INSTALLED
using Unity.Advertisement.IosSupport;
#endif

namespace Sorolla
{
    /// <summary>
    ///     Entry point for Sorolla SDK.
    ///     Auto-initializes at startup - NO MANUAL SETUP REQUIRED.
    ///     Handles iOS ATT before initializing SDKs.
    /// </summary>
    public class SorollaBootstrapper : MonoBehaviour
    {
        private const string ContextScreenPath = "ContextScreen";
        private const float PollInterval = 0.5f;

        private static SorollaBootstrapper s_instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInit()
        {
            if (s_instance != null) return;

            Debug.Log("[Sorolla] Auto-initializing...");

            var go = new GameObject("[Sorolla SDK]");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<SorollaBootstrapper>();
        }

        private void Start()
        {
            StartCoroutine(Initialize());
        }

        private IEnumerator Initialize()
        {
#if UNITY_IOS && UNITY_IOS_SUPPORT_INSTALLED
            var status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();

            if (status == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
            {
                yield return ShowContextAndRequest();
            }
            else
            {
                Sorolla.Initialize(status == ATTrackingStatusBinding.AuthorizationTrackingStatus.AUTHORIZED);
            }
#else
            // Android or other - initialize with consent
            Sorolla.Initialize(true);
            yield break;
#endif
        }

        private IEnumerator ShowContextAndRequest()
        {
#if UNITY_IOS && UNITY_IOS_SUPPORT_INSTALLED
            GameObject contextScreen = null;
            var prefab = Resources.Load<GameObject>(ContextScreenPath);

            if (prefab != null)
            {
                contextScreen = Instantiate(prefab);
                var canvas = contextScreen.GetComponent<Canvas>();
                if (canvas) canvas.sortingOrder = 999;
                Debug.Log("[Sorolla] Context screen displayed.");
            }
            else
            {
                Debug.LogWarning($"[Sorolla] ContextScreen prefab not found. Triggering ATT directly.");
                ATTrackingStatusBinding.RequestAuthorizationTracking();
            }

            // Wait for user decision
            var status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
            while (status == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
            {
                yield return new WaitForSeconds(PollInterval);
                status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
            }

            if (contextScreen != null)
                Destroy(contextScreen);

            Debug.Log($"[Sorolla] ATT decision: {status}");
            Sorolla.Initialize(status == ATTrackingStatusBinding.AuthorizationTrackingStatus.AUTHORIZED);
#else
            yield break;
#endif
        }

        private void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
        }
    }
}
