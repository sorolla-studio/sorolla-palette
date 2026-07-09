using UnityEngine;

namespace Sorolla.Palette
{
    internal sealed partial class SorollaDiagnosticsConsole
    {
        void CheckKeyboardToggle()
        {
            if (SorollaDiagnosticsInput.IsKeyboardTogglePressed())
                SorollaDebugMenuOverlay.Toggle();
        }

        // Unlock now opens the NEW UI Toolkit overlay directly (Arthur, debug-menu overhaul phase 2
        // scope addition: saves a manual "Open new menu (preview)" tap on every iteration). The old
        // IMGUI console still owns Actions/Console until phases 3-4 port them, so it stays reachable
        // via "Open legacy console" in the new overlay's header - nothing becomes unreachable.
        void CheckTouchToggle()
        {
            _theme.UpdateUiScale();
            if (_tapUnlock.Poll(_theme.UiScale))
                SorollaDebugMenuOverlay.Toggle();
        }

        void CheckTouchScroll() => _scrollDrag.Update(ref _scroll, _theme.UiScale);

        void ResetTouchScroll() => _scrollDrag.Reset();
    }
}
