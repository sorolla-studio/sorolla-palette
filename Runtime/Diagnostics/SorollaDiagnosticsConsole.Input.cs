using UnityEngine;

namespace Sorolla.Palette
{
    internal sealed partial class SorollaDiagnosticsConsole
    {
        void CheckKeyboardToggle()
        {
            if (SorollaDiagnosticsInput.IsKeyboardTogglePressed())
                ToggleVisible();
        }

        void CheckTouchToggle()
        {
            _theme.UpdateUiScale();
            if (_tapUnlock.Poll(_theme.UiScale))
                SetVisible(true);
        }

        void CheckTouchScroll() => _scrollDrag.Update(ref _scroll, _theme.UiScale);

        void ResetTouchScroll() => _scrollDrag.Reset();
    }
}
