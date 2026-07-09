#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.Scripting;

namespace Sorolla.Palette
{
    // Registers the Input System UI module factory the same way SorollaDiagnosticsInputSystemBackend
    // registers its touch/keyboard backend: Sorolla.Runtime cannot reference Unity.InputSystem
    // directly, so this gated assembly hands over a delegate at load time.
    [Preserve]
    internal static class SorollaDebugMenuInputSystemModuleFactory
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        [Preserve]
        static void Register()
        {
            SorollaDebugMenuEventSystemFactory.InputSystemModuleFactory = go => go.AddComponent<InputSystemUIInputModule>();
        }
    }
}
#endif
