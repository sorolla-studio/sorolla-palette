using System.Collections;
using Sorolla.Palette.ATT;
using UnityEngine;
#if UNITY_IOS && UNITY_IOS_SUPPORT_INSTALLED
using Unity.Advertisement.IosSupport;
#endif

namespace Sorolla.Palette
{
    /// <summary>
    ///     Entry point for Palette SDK.
    ///     Auto-initializes at startup - NO MANUAL SETUP REQUIRED.
    ///     Handles iOS ATT before initializing SDKs.
    ///     In Editor, shows fake dialogs for testing.
    /// </summary>
    public class SorollaBootstrapper : MonoBehaviour
    {
        const string ContextScreenPath = "ContextScreen";
        const float PollInterval = 0.5f;

        static SorollaBootstrapper s_instance;

        void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
        }

        void Start() => StartCoroutine(Initialize());

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoInit()
        {
            if (s_instance != null) return;

            Debug.Log("[Palette] Auto-initializing...");

            var go = new GameObject("[Palette SDK]");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<SorollaBootstrapper>();
        }

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
                Palette.Initialize(status == ATTrackingStatusBinding.AuthorizationTrackingStatus.AUTHORIZED);
            }
#else
            // Android or other - initialize with consent
            Palette.Initialize(true);
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
                Debug.Log("[Palette] Context screen displayed.");
            }
            else
            {
                Debug.LogWarning("[Palette] ContextScreen prefab not found. Triggering ATT directly.");
                ATTrackingStatusBinding.RequestAuthorizationTracking();
            }

            // Wait for user decision
            ATTrackingStatusBinding.AuthorizationTrackingStatus status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
            while (status == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
            {
                yield return new WaitForSeconds(PollInterval);
                status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
            }

            if (contextScreen != null)
                Destroy(contextScreen);

            Debug.Log($"[Palette] ATT decision: {status}");
            Palette.Initialize(status == ATTrackingStatusBinding.AuthorizationTrackingStatus.AUTHORIZED);
        }
#endif

#if UNITY_EDITOR
        IEnumerator ShowContextAndRequestEditor()
        {
            var prefab = Resources.Load<GameObject>(ContextScreenPath);

            if (prefab == null)
            {
                Debug.LogWarning("[Palette] ContextScreen prefab not found. Run Palette > ATT > Create PreATT Popup Prefab");
                Palette.Initialize(true);
                yield break;
            }

            GameObject contextScreen = Instantiate(prefab);
            var canvas = contextScreen.GetComponent<Canvas>();
            if (canvas) canvas.sortingOrder = 999;
            Debug.Log("[Palette] Context screen displayed (Editor).");

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
            Debug.Log("[Palette] ATT flow complete (Editor).");
            Palette.Initialize(true);
        }
#endif
    }
}
