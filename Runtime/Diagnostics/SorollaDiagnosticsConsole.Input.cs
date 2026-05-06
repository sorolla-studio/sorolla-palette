using UnityEngine;

namespace Sorolla.Palette
{
    internal sealed partial class SorollaDiagnosticsConsole
    {
        void CheckKeyboardToggle()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote))
                ToggleVisible();
        }

        void CheckTouchToggle()
        {
            if (Input.touchCount == 0 && Input.GetMouseButtonDown(0))
                RegisterTap(Input.mousePosition);

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began)
                    RegisterTap(touch.position);
            }
        }

        void RegisterTap(Vector2 screenPosition)
        {
            if (_visible) return;

            Rect safeArea = Screen.safeArea;
            float tapArea = 128f * _uiScale;
            bool topLeft = screenPosition.x >= safeArea.xMin
                && screenPosition.x <= safeArea.xMin + tapArea
                && screenPosition.y <= safeArea.yMax
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
            if (Input.touchCount == 0)
            {
                ResetTouchScroll();
                return;
            }

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                if (_scrollTouchId == -1 && touch.phase == TouchPhase.Began)
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

                if (touch.phase == TouchPhase.Moved)
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

                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
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
