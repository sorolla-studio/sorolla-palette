using System;
using System.Collections;
using UnityEngine;
using Sorolla.Palette.ATT;

namespace Sorolla.Palette
{
    /// <summary>
    ///     Entry point for Palette SDK.
    ///     Auto-initializes at startup - NO MANUAL SETUP REQUIRED.
    ///     MAX SDK handles consent flow (CMP → ATT) automatically.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class SorollaBootstrapper : MonoBehaviour
    {
        static SorollaBootstrapper s_instance;

        void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
        }

        void Start()
        {
            EnsurePersistent();
            StartCoroutine(Initialize());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoInit()
        {
            if (s_instance != null) return;

            Debug.Log("[Palette] Auto-initializing...");

            var go = new GameObject("[Palette SDK]");
            MakePersistent(go);
            s_instance = go.AddComponent<SorollaBootstrapper>();
        }

        static void MakePersistent(GameObject go)
        {
            try
            {
                DontDestroyOnLoad(go);
                Debug.Log("[Palette] Successfully marked GameObject as persistent");
            }
            catch (Exception e)
            {
                // At BeforeSceneLoad, scene context may not be ready on some platforms.
                // EnsurePersistent() in Start() will retry when the scene is valid.
                Debug.LogWarning($"[Palette] DontDestroyOnLoad deferred to Start(): {e.Message}");
            }
        }

        /// <summary>
        ///     Fallback: if MakePersistent failed at BeforeSceneLoad (scene not ready),
        ///     retry now that we're in Start() with a valid scene context.
        /// </summary>
        void EnsurePersistent()
        {
            if (gameObject.scene.name == "DontDestroyOnLoad") return;

            try
            {
                DontDestroyOnLoad(gameObject);
                Debug.Log("[Palette] Successfully marked GameObject as persistent (deferred)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Palette] Failed to persist SDK GameObject: {e.Message}");
            }
        }

        IEnumerator Initialize()
        {
#if UNITY_IOS && !UNITY_EDITOR
    #if !(SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED)
            // No MAX → handle ATT manually (native dialog only, no soft prompt)
            // Wait for the app to be fully visible before requesting ATT.
            // Calling too early (before window scene is active) causes iOS to
            // silently drop the request and return NOT_DETERMINED without showing the dialog.
            yield return null; // let first frame render
            yield return new WaitForSeconds(1f); // ensure app has focus

            var currentStatus = ATTBridge.GetStatus();
            if (currentStatus == ATTBridge.AuthorizationStatus.NotDetermined)
            {
                bool attResponseReceived = false;
                ATTBridge.RequestAuthorization(_ =>
                {
                    attResponseReceived = true;
                });

                // Wait for the user to respond to the ATT dialog
                while (!attResponseReceived)
                    yield return null;
            }

            var finalStatus = ATTBridge.GetStatus();
            bool consent = finalStatus == ATTBridge.AuthorizationStatus.Authorized;
            Debug.Log($"[Palette] Standalone ATT: {finalStatus} (consent={consent})");
            Palette.Initialize(consent);
            yield break;
    #endif
#endif
            // MAX installed: it handles CMP → ATT automatically
            // Non-iOS / Editor: no ATT needed
            Palette.Initialize(consent: true);
            yield break;
        }
    }
}
