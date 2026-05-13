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

            int touchCount = SorollaDiagnosticsInput.TouchCount;
            if (touchCount == 0 && SorollaDiagnosticsInput.TryGetPointerTap(out Vector2 pointerPosition))
                RegisterTap(pointerPosition);

            for (int i = 0; i < touchCount; i++)
            {
                if (!SorollaDiagnosticsInput.TryGetTouch(i, out SorollaDiagnosticsInputTouch touch))
                    continue;

                if (touch.Phase == SorollaDiagnosticsInputTouchPhase.Began)
                    RegisterTap(touch.position);
            }
        }

        void RegisterTap(Vector2 screenPosition)
        {
            if (_visible) return;

            Rect safeArea = Screen.safeArea;
            float tapArea = 128f * _uiScale;
            bool topLeft = screenPosition.x >= 0f
                && screenPosition.x <= safeArea.xMin + tapArea
                && screenPosition.y <= Screen.height
                && screenPosition.y >= safeArea.yMax - tapArea;
            if (!topLeft) return;

            float now = Time.unscaledTime;
            if (now - _firstTapTime > TapWindowSeconds)
            {
                _firstTapTime = now;
                _tapCount = 0;
            }

            _tapCount++;
            if (_tapCount < RequiredTapCount) return;

            SetVisible(true);
        }

        void CheckTouchScroll()
        {
            int touchCount = SorollaDiagnosticsInput.TouchCount;
            if (touchCount == 0)
            {
                ResetTouchScroll();
                return;
            }

            for (int i = 0; i < touchCount; i++)
            {
                if (!SorollaDiagnosticsInput.TryGetTouch(i, out SorollaDiagnosticsInputTouch touch))
                    continue;

                if (_scrollTouchId == -1 && touch.Phase == SorollaDiagnosticsInputTouchPhase.Began)
                {
                    _scrollTouchId = touch.fingerId;
                    _scrollTouchDragging = false;
                    _ignoreSectionToggleAfterDrag = false;
                    _scrollTouchStartPosition = touch.position;
                    _lastScrollTouchPosition = touch.position;
                    return;
                }

                if (touch.fingerId != _scrollTouchId)
                    continue;

                if (touch.Phase == SorollaDiagnosticsInputTouchPhase.Moved)
                {
                    Vector2 delta = touch.position - _lastScrollTouchPosition;
                    if (!_scrollTouchDragging)
                    {
                        Vector2 totalDelta = touch.position - _scrollTouchStartPosition;
                        if (Mathf.Abs(totalDelta.y) < ScrollDragThresholdPixels * _uiScale)
                            return;

                        _scrollTouchDragging = true;
                        _ignoreSectionToggleAfterDrag = true;
                    }

                    _scroll.y += delta.y;
                    _scroll.y = Mathf.Max(0f, _scroll.y);
                    _lastScrollTouchPosition = touch.position;
                }

                if (touch.Phase == SorollaDiagnosticsInputTouchPhase.Ended || touch.Phase == SorollaDiagnosticsInputTouchPhase.Canceled)
                {
                    _scrollTouchId = -1;
                    _scrollTouchDragging = false;
                }

                return;
            }
        }

        void ResetTouchScroll()
        {
            _scrollTouchId = -1;
            _scrollTouchDragging = false;
            _ignoreSectionToggleAfterDrag = false;
        }
    }
}
