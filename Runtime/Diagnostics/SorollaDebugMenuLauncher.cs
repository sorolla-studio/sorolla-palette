using UnityEngine;

namespace Sorolla.Palette
{
    // Persistent input bridge for the code-created debug menu. UI rendering and diagnostics state
    // remain in their own types; this component only owns launch shortcuts and their gesture state.
    internal sealed class SorollaDebugMenuLauncher : MonoBehaviour
    {
        const float ScaleReferenceShortSide = 470.4f;
        const float MinUiScale = 1f;
        const float MaxUiScale = 2.7f;

        static SorollaDebugMenuLauncher s_instance;

        readonly TapUnlockGesture _tapUnlock = new TapUnlockGesture();

        internal static void Ensure(GameObject host)
        {
            SorollaDiagnostics.EnsureLogBridge();
            SorollaDiagnostics.InstallUnityLogSink();
            if (s_instance != null) return;

            s_instance = host.GetComponent<SorollaDebugMenuLauncher>();
            if (s_instance == null)
                s_instance = host.AddComponent<SorollaDebugMenuLauncher>();
        }

        void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(this);
                return;
            }

            s_instance = this;
        }

        void Update()
        {
            SorollaDiagnostics.UpdatePolling();

            if (SorollaDiagnosticsInput.IsKeyboardTogglePressed())
            {
                _tapUnlock.Reset();
                SorollaDebugMenuOverlay.Toggle();
                return;
            }

            if (SorollaDebugMenuOverlay.IsOpen)
            {
                _tapUnlock.Reset();
                return;
            }

            if (_tapUnlock.Poll(CurrentUiScale()))
                SorollaDebugMenuOverlay.Toggle();
        }

        static float CurrentUiScale()
        {
            float shortSide = Mathf.Min(Screen.width, Screen.height);
            float scale = shortSide > 0f ? shortSide / ScaleReferenceShortSide : MinUiScale;
            return Mathf.Clamp(scale, MinUiScale, MaxUiScale);
        }

        void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
        }

        sealed class TapUnlockGesture
        {
            const int RequiredTapCount = 5;
            const float TapWindowSeconds = 2f;
            const float FinalTapHoldSeconds = 0.8f;
            const float TapAreaSize = 128f;

            enum HoldResult
            {
                NotHolding,
                Holding,
                Unlocked,
            }

            int _tapCount;
            float _firstTapTime;
            bool _holdingPointer;
            int _holdTouchId = -1;
            float _holdStartTime;

            public bool Poll(float uiScale)
            {
                HoldResult hold = UpdateHold(uiScale);
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
                _holdingPointer = false;
                _holdTouchId = -1;
                _holdStartTime = 0f;
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

                _holdingPointer = pointer;
                _holdTouchId = touchId;
                _holdStartTime = now;
            }

            HoldResult UpdateHold(float uiScale)
            {
                if (_holdTouchId < 0 && !_holdingPointer)
                    return HoldResult.NotHolding;

                bool held = _holdingPointer
                    ? SorollaDiagnosticsInput.TryGetPointerHold(out Vector2 position)
                    : TryGetHeldTouch(_holdTouchId, out position);

                if (!held || !IsUnlockArea(position, uiScale))
                {
                    Reset();
                    return HoldResult.NotHolding;
                }

                return Time.unscaledTime - _holdStartTime < FinalTapHoldSeconds
                    ? HoldResult.Holding
                    : HoldResult.Unlocked;
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
                float tapArea = TapAreaSize * uiScale;
                return screenPosition.x >= safeArea.xMax - tapArea
                    && screenPosition.x <= Screen.width
                    && screenPosition.y >= safeArea.yMax - tapArea
                    && screenPosition.y <= Screen.height;
            }
        }
    }
}
