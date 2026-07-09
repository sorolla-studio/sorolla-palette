using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Sorolla.Palette
{
    // Mirrors the SorollaDiagnosticsInput registration pattern: Sorolla.Runtime cannot reference
    // Unity.InputSystem directly (the package may not be installed), so the InputSystem-gated
    // assembly (Sorolla.Runtime.InputSystem) registers a factory delegate here at load time.
    // Legacy input needs no such indirection - UnityEngine.EventSystems.StandaloneInputModule
    // ships with com.unity.ugui, already an unconditional package dependency.
    internal static class SorollaDebugMenuEventSystemFactory
    {
        internal static Action<GameObject> InputSystemModuleFactory;

        // Creates an EventSystem ONLY if none exists in the loaded scenes - never disturbs a
        // game's own EventSystem/input module setup. Returns the created GameObject, or null if
        // an EventSystem already existed (nothing to tear down on close in that case).
        internal static GameObject CreateIfMissing()
        {
#if UNITY_2023_1_OR_NEWER
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return null;
#else
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null) return null;
#endif

            var go = new GameObject("[Palette SDK Debug Menu EventSystem]");
            go.AddComponent<EventSystem>();

            bool addedInputSystemModule = false;
#if ENABLE_INPUT_SYSTEM
            if (InputSystemModuleFactory != null)
            {
                InputSystemModuleFactory(go);
                addedInputSystemModule = true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (!addedInputSystemModule)
                go.AddComponent<StandaloneInputModule>();
#else
            _ = addedInputSystemModule; // legacy input manager disabled project-wide; InputSystemModuleFactory is the only path.
#endif

            return go;
        }
    }
}
