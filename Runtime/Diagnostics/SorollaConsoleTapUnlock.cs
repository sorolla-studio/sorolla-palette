using UnityEngine;

namespace Sorolla.Palette
{
    /// <summary>
    ///     The hidden-corner unlock gesture for the diagnostics console: RequiredTapCount taps in
    ///     the top-right safe-area corner within TapWindowSeconds, then a FinalTapHoldSeconds hold.
    ///     Extracted from the console MonoBehaviour so the tap/hold state machine stands alone.
    /// </summary>
    internal sealed class SorollaConsoleTapUnlock
    {
        const int RequiredTapCount = 5;
        const float TapWindowSeconds = 2f;
        const float FinalTapHoldSeconds = 0.8f;

        enum HoldResult { NotHolding, Holding, Unlocked }

        int _tapCount;
        float _firstTapTime;
        bool _unlockHoldPointer;
        int _unlockHoldTouchId = -1;
        float _unlockHoldStartTime;

        /// <summary>
        ///     Advances the gesture from the current input. Returns true exactly once, on the frame
        ///     the hold completes, signalling the console to reveal itself. Call only while hidden.
        /// </summary>
        public bool Poll(float uiScale)
        {
            HoldResult hold = UpdateUnlockHold(uiScale);
            if (hold == HoldResult.Unlocked) return true;
            if (hold == HoldResult.Holding) return false;

            int touchCount = SorollaDiagnosticsInput.TouchCount;
            if (touchCount > 1)
            {
                Reset();
                return false;
            }

            if (touchCount == 0 && SorollaDiagnosticsInput.TryGetPointerTap(out Vector2 pointerPosition))
                RegisterTap(pointerPosition, -1, true, uiScale);

            for (int i = 0; i < touchCount; i++)
            {
                if (!SorollaDiagnosticsInput.TryGetTouch(i, out SorollaDiagnosticsInputTouch touch))
                    continue;

                if (touch.Phase == SorollaDiagnosticsInputTouchPhase.Began)
                    RegisterTap(touch.position, touch.fingerId, false, uiScale);
            }

            return false;
        }

        public void Reset()
        {
            _tapCount = 0;
            _firstTapTime = 0f;
            _unlockHoldPointer = false;
            _unlockHoldTouchId = -1;
            _unlockHoldStartTime = 0f;
        }

        void RegisterTap(Vector2 screenPosition, int touchId, bool pointer, float uiScale)
        {
            if (!IsUnlockArea(screenPosition, uiScale)) return;

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

        HoldResult UpdateUnlockHold(float uiScale)
        {
            if (_unlockHoldTouchId < 0 && !_unlockHoldPointer)
                return HoldResult.NotHolding;

            bool held = _unlockHoldPointer
                ? SorollaDiagnosticsInput.TryGetPointerHold(out Vector2 position)
                : TryGetHeldTouch(_unlockHoldTouchId, out position);

            if (!held || !IsUnlockArea(position, uiScale))
            {
                Reset();
                return HoldResult.NotHolding;
            }

            if (Time.unscaledTime - _unlockHoldStartTime < FinalTapHoldSeconds)
                return HoldResult.Holding;

            return HoldResult.Unlocked;
        }

        static bool TryGetHeldTouch(int fingerId, out Vector2 position)
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

        static bool IsUnlockArea(Vector2 screenPosition, float uiScale)
        {
            Rect safeArea = Screen.safeArea;
            float tapArea = 128f * uiScale;
            return screenPosition.x >= safeArea.xMax - tapArea
                && screenPosition.x <= Screen.width
                && screenPosition.y <= Screen.height
                && screenPosition.y >= safeArea.yMax - tapArea;
        }
    }
}
