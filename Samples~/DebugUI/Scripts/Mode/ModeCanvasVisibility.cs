using UnityEngine;

namespace Sorolla.Palette.DebugUI
{
    /// <summary>
    ///     Controls visibility of UI elements based on SDK mode (Prototype vs Full).
    ///     Uses CanvasGroup to hide without disabling, preserving event subscriptions.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ModeCanvasVisibility : ModeComponentBase
    {
        [SerializeField] VisibilityMode mode = VisibilityMode.AlwaysVisible;

        CanvasGroup _canvasGroup;

        void Awake() => _canvasGroup = GetComponent<CanvasGroup>();

        protected override void ApplyTheme()
        {
            bool shouldShow = mode switch
            {
                VisibilityMode.AlwaysVisible => true,
                VisibilityMode.PrototypeOnly => IsPrototype,
                VisibilityMode.FullOnly => !IsPrototype,
                _ => true,
            };

            _canvasGroup.alpha = shouldShow ? 1f : 0f;
            _canvasGroup.interactable = shouldShow;
            _canvasGroup.blocksRaycasts = shouldShow;
        }

        enum VisibilityMode
        {
            AlwaysVisible,
            PrototypeOnly,
            FullOnly,
        }
    }
}
