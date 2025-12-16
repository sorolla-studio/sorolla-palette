using System.Collections;
using Sorolla.ATT;
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
    ///     In Editor, shows fake dialogs for testing.
    /// </summary>
    public class SorollaBootstrapper : MonoBehaviour
    {
        const string ContextScreenPath = "ContextScreen";
        const float PollInterval = 0.5f;

        static SorollaBootstrapper s_instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoInit()
        {
            if (s_instance != null) return;

            Debug.Log("[Sorolla] Auto-initializing...");

            var go = new GameObject("[Sorolla SDK]");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<SorollaBootstrapper>();
        }

        void Start() => StartCoroutine(Initialize());

        IEnumerator Initialize()
        {
#if UNITY_EDITOR
            yield return ShowContextAndRequestEditor();
#elif UNITY_IOS && UNITY_IOS_SUPPORT_INSTALLED
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

#if UNITY_IOS && UNITY_IOS_SUPPORT_INSTALLED
        IEnumerator ShowContextAndRequest()
        {
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
        }
#endif

#if UNITY_EDITOR
        IEnumerator ShowContextAndRequestEditor()
        {
            var prefab = Resources.Load<GameObject>(ContextScreenPath);

            if (prefab == null)
            {
                Debug.LogWarning("[Sorolla] ContextScreen prefab not found. Run Sorolla > ATT > Create PreATT Popup Prefab");
                Sorolla.Initialize(true);
                yield break;
            }

            GameObject contextScreen = Instantiate(prefab);
            var canvas = contextScreen.GetComponent<Canvas>();
            if (canvas) canvas.sortingOrder = 999;
            Debug.Log("[Sorolla] Context screen displayed (Editor).");

            // Wait for ContextScreenView to trigger FakeATTDialog
            var view = contextScreen.GetComponent<ContextScreenView>();
            bool completed = false;

            view.SentTrackingAuthorizationRequest += () =>
            {
                Destroy(contextScreen);
                completed = true;
            };

            // Wait for completion (FakeATTDialog handles the decision)
            while (!completed)
            {
                yield return null;
            }

            // In Editor, we just assume consent for simplicity
            Debug.Log("[Sorolla] ATT flow complete (Editor).");
            Sorolla.Initialize(true);
        }
#endif

        void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
        }
    }
}
