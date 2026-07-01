using UnityEngine;

namespace Sorolla.Palette
{
    /// <summary>
    ///     Touch-drag scroll state machine for the diagnostics console. Extracted from the console
    ///     MonoBehaviour so the drag-threshold / active-finger tracking lives in one focused place.
    /// </summary>
    internal sealed class SorollaConsoleScrollDrag
    {
        const float ScrollDragThresholdPixels = 10f;

        int _scrollTouchId = -1;
        bool _scrollTouchDragging;
        bool _ignoreSectionToggleAfterDrag;
        Vector2 _scrollTouchStartPosition;
        Vector2 _lastScrollTouchPosition;

        /// <summary>
        ///     True for the interaction that just crossed the drag threshold, so a section-header
        ///     touch that was actually a scroll gesture doesn't also toggle the section.
        /// </summary>
        public bool IgnoreSectionToggleAfterDrag => _ignoreSectionToggleAfterDrag;

        /// <summary>Advances the scroll state machine, mutating the scroll position in place.</summary>
        public void Update(ref Vector2 scroll, float uiScale)
        {
            int touchCount = SorollaDiagnosticsInput.TouchCount;
            if (touchCount == 0)
            {
                Reset();
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
                        if (Mathf.Abs(totalDelta.y) < ScrollDragThresholdPixels * uiScale)
                            return;

                        _scrollTouchDragging = true;
                        _ignoreSectionToggleAfterDrag = true;
                    }

                    scroll.y += delta.y;
                    scroll.y = Mathf.Max(0f, scroll.y);
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

        public void Reset()
        {
            _scrollTouchId = -1;
            _scrollTouchDragging = false;
            _ignoreSectionToggleAfterDrag = false;
        }
    }
}
