using System;
using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.Palette.DebugUI
{
    /// <summary>
    ///     Controls the Crashlytics section in Tools tab. Self-sufficient - calls Palette API directly.
    /// </summary>
    public class CrashlyticsController : UIComponentBase
    {
        [SerializeField] Button _logExceptionButton;
        [SerializeField] Button _forceCrashButton;

        void Awake()
        {

            _logExceptionButton.onClick.AddListener(HandleLogException);
            _forceCrashButton.onClick.AddListener(HandleForceCrash);
        }

        void OnDestroy()
        {
            _logExceptionButton.onClick.RemoveListener(HandleLogException);
            _forceCrashButton.onClick.RemoveListener(HandleForceCrash);
        }

        void HandleLogException()
        {
            var testException = new Exception("Test exception from Debug Panel");
            Palette.LogException(testException);

            DebugPanelManager.Instance?.Log("Logged test exception to Crashlytics", LogSource.Firebase);
            SorollaDebugEvents.RaiseShowToast("Exception logged", ToastType.Warning);
        }

        void HandleForceCrash()
        {
            Palette.LogCrashlytics("Forcing crash from Debug Panel");

            DebugPanelManager.Instance?.Log("Forcing crash...", LogSource.Firebase, LogLevel.Error);
            SorollaDebugEvents.RaiseShowToast("Forcing crash...", ToastType.Error);

#if UNITY_EDITOR
            Debug.LogError("[DebugUI] Force crash requested - skipped in editor");
#else
            // This will crash the app
            UnityEngine.Diagnostics.Utils.ForceCrash(UnityEngine.Diagnostics.ForcedCrashCategory.FatalError);
#endif
        }
    }
}
