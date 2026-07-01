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
            UpdateUiScale();
            if (_tapUnlock.Poll(_uiScale))
                SetVisible(true);
        }

        void CheckTouchScroll() => _scrollDrag.Update(ref _scroll, _uiScale);

        void ResetTouchScroll() => _scrollDrag.Reset();
    }
}
