using UnityEditor;
using UnityEngine.UI;

namespace Sorolla.DebugUI
{
    /// <summary>
    ///     Toggle to switch SDK mode (Prototype/Full) in Editor/Debug builds.
    /// </summary>
    public class ModeSwitchController : UIComponentBase
    {
        Button _toggle;
        bool _isPrototype;

        static bool IsPrototype => SorollaSDK.Config == null || SorollaSDK.Config.isPrototypeMode;

        void Awake()
        {

#if !UNITY_EDITOR && !DEBUG
            gameObject.SetActive(false);
            return;
#endif
            _toggle = GetComponent<Button>();
            _toggle.onClick.AddListener(HandleToggleChanged);
        }

        static void HandleToggleChanged()
        {
#if UNITY_EDITOR
            // Actually change the config in Editor
            if (SorollaSDK.Config != null)
            {
                SorollaSDK.Config.isPrototypeMode = !SorollaSDK.Config.isPrototypeMode;
                EditorUtility.SetDirty(SorollaSDK.Config);
            }
#else
            // Do not change SDK behavior in builds
            UnityEngine.Debug.LogWarning("[SorollaSDK Debug UI] Mode switch called in build, but SDK behavior remains unchanged.");
#endif

            // Notify UI to refresh
            SorollaMode newMode = IsPrototype ? SorollaMode.Prototype : SorollaMode.Full;
            SorollaDebugEvents.RaiseModeChanged(newMode);

            // Redirect to default tab when entering prototype (Ads tab disabled)
            if (newMode == SorollaMode.Prototype)
                SorollaDebugEvents.RaiseTabChanged(0);

            // Show warning about SDK behavior
            string modeName = IsPrototype ? "Prototype" : "Full";
            SorollaDebugEvents.RaiseShowToast($"UI switched to {modeName} mode", ToastType.Info);
            DebugPanelManager.Instance?.Log($"Mode: {modeName} (SDK unchanged on build)", LogSource.Sorolla, LogLevel.Warning);
        }
    }
}
