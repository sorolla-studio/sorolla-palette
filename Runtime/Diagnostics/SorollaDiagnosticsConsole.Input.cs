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
            if (UpdateUnlockHold())
                return;

            int touchCount = SorollaDiagnosticsInput.TouchCount;
            if (touchCount > 1)
            {
                ResetUnlockGesture();
                return;
            }

            if (touchCount == 0 && SorollaDiagnosticsInput.TryGetPointerTap(out Vector2 pointerPosition))
                RegisterTap(pointerPosition, -1, true);

            for (int i = 0; i < touchCount; i++)
            {
                if (!SorollaDiagnosticsInput.TryGetTouch(i, out SorollaDiagnosticsInputTouch touch))
                    continue;

                if (touch.Phase == SorollaDiagnosticsInputTouchPhase.Began)
                    RegisterTap(touch.position, touch.fingerId, false);
            }
        }

        void RegisterTap(Vector2 screenPosition, int touchId, bool pointer)
        {
            if (_visible) return;

            if (!IsUnlockArea(screenPosition)) return;

            float now = Time.unscaledTime;
            if (now - _firstTapTime > TapWindowSeconds)
            {
                _firstTapTime = now;
                _tapCount = 0;
            }

            _tapCount++;
            if (_tapCount < RequiredTapCount) return;

            _unlockHoldPointer = pointer;
            _unlockHoldTouchId = touchId;
            _unlockHoldStartTime = now;
        }

        bool UpdateUnlockHold()
        {
            if (_unlockHoldTouchId < 0 && !_unlockHoldPointer)
                return false;

            bool held = _unlockHoldPointer
                ? SorollaDiagnosticsInput.TryGetPointerHold(out Vector2 position)
                : TryGetHeldTouch(_unlockHoldTouchId, out position);

            if (!held || !IsUnlockArea(position))
            {
                ResetUnlockGesture();
                return false;
            }

            if (Time.unscaledTime - _unlockHoldStartTime < FinalTapHoldSeconds)
                return true;

            SetVisible(true);
            return true;
        }

        bool TryGetHeldTouch(int fingerId, out Vector2 position)
        {
            int touchCount = SorollaDiagnosticsInput.TouchCount;
            for (int i = 0; i < touchCount; i++)
            {
                if (!SorollaDiagnosticsInput.TryGetTouch(i, out SorollaDiagnosticsInputTouch touch))
                    continue;
                if (touch.fingerId != fingerId)
                    continue;
                if (touch.Phase == SorollaDiagnosticsInputTouchPhase.Ended
                    || touch.Phase == SorollaDiagnosticsInputTouchPhase.Canceled)
                    break;

                position = touch.position;
                return true;
            }

            position = default;
            return false;
        }

        bool IsUnlockArea(Vector2 screenPosition)
        {
            Rect safeArea = Screen.safeArea;
            float tapArea = 128f * _uiScale;
            return screenPosition.x >= safeArea.xMax - tapArea
                && screenPosition.x <= Screen.width
                && screenPosition.y <= Screen.height
                && screenPosition.y >= safeArea.yMax - tapArea;
        }

        void CheckTouchScroll() => _scrollDrag.Update(ref _scroll, _uiScale);

        void ResetTouchScroll() => _scrollDrag.Reset();
    }
}
