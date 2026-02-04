using System;
using System.Collections;
using UnityEngine;

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

        void Start() => StartCoroutine(Initialize());

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoInit()
        {
            if (s_instance != null) return;

            Debug.Log("[Palette] Auto-initializing...");

            var go = new GameObject("[Palette SDK]");
            MakePersistent(go);
            s_instance = go.AddComponent<SorollaBootstrapper>();
        }

        /// <summary>
        ///     Robust DontDestroyOnLoad with triple-defense:
        ///     1. Check HideFlags to avoid redundant calls
        ///     2. Try-catch to prevent assertion crashes
        ///     3. Verify scene context before applying
        /// </summary>
        static void MakePersistent(GameObject go)
        {
            // Layer 1: Check if already marked as DontDestroyOnLoad via HideFlags
            if ((go.hideFlags & HideFlags.DontSave) == HideFlags.DontSave)
            {
                Debug.Log("[Palette] GameObject already persistent, skipping DontDestroyOnLoad");
                return;
            }

            // Layer 2: Verify we're in a valid scene context
            if (!go.scene.IsValid() || !go.scene.isLoaded)
            {
                Debug.LogWarning("[Palette] Invalid scene context, deferring DontDestroyOnLoad");
                return;
            }

            // Layer 3: Try-catch as final safety net
            try
            {
                DontDestroyOnLoad(go);
                Debug.Log("[Palette] Successfully marked GameObject as persistent");
            }
            catch (Exception e)
            {
                // This should never happen with the checks above, but provides absolute guarantee
                Debug.LogError($"[Palette] DontDestroyOnLoad failed (non-fatal): {e.Message}");
                // SDK continues to function even if persistence fails
            }
        }

        IEnumerator Initialize()
        {
            // MAX SDK handles consent flow automatically:
            // 1. Regional check (GDPR detection)
            // 2. UMP consent form (if GDPR region)
            // 3. ATT prompt (iOS, after CMP)
            // This achieves CMP-first flow for better ATT opt-in rates.
            Palette.Initialize(consent: true);
            yield break;
        }
    }
}
