using UnityEngine;

namespace Sorolla.Palette
{
    internal enum SorollaDiagnosticsInputTouchPhase
    {
        None,
        Began,
        Moved,
        Stationary,
        Ended,
        Canceled,
    }

    internal readonly struct SorollaDiagnosticsInputTouch
    {
        public readonly int fingerId;
        public readonly Vector2 position;
        public readonly SorollaDiagnosticsInputTouchPhase Phase;

        public SorollaDiagnosticsInputTouch(int fingerId, Vector2 position, SorollaDiagnosticsInputTouchPhase phase)
        {
            this.fingerId = fingerId;
            this.position = position;
            Phase = phase;
        }
    }

    internal interface ISorollaDiagnosticsInputBackend
    {
        bool IsKeyboardTogglePressed();
        bool TryGetPointerTap(out Vector2 screenPosition);
        bool TryGetPointerHold(out Vector2 screenPosition);
        int TouchCount { get; }
        bool TryGetTouch(int index, out SorollaDiagnosticsInputTouch touch);
    }

    internal static class SorollaDiagnosticsInput
    {
        static ISorollaDiagnosticsInputBackend s_inputSystemBackend;

        internal static void RegisterInputSystemBackend(ISorollaDiagnosticsInputBackend backend)
        {
            s_inputSystemBackend = backend;
        }

        internal static bool IsKeyboardTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (s_inputSystemBackend != null)
                return s_inputSystemBackend.IsKeyboardTogglePressed();
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.BackQuote);
#else
            return false;
#endif
        }

        internal static bool TryGetPointerTap(out Vector2 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            if (s_inputSystemBackend != null)
                return s_inputSystemBackend.TryGetPointerTap(out screenPosition);
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0))
            {
                screenPosition = Input.mousePosition;
                return true;
            }
#endif

            screenPosition = default;
            return false;
        }

        internal static bool TryGetPointerHold(out Vector2 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            if (s_inputSystemBackend != null)
                return s_inputSystemBackend.TryGetPointerHold(out screenPosition);
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButton(0))
            {
                screenPosition = Input.mousePosition;
                return true;
            }
#endif

            screenPosition = default;
            return false;
        }

        internal static int TouchCount
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (s_inputSystemBackend != null)
                    return s_inputSystemBackend.TouchCount;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.touchCount;
#else
                return 0;
#endif
            }
        }

        internal static bool TryGetTouch(int index, out SorollaDiagnosticsInputTouch touch)
        {
#if ENABLE_INPUT_SYSTEM
            if (s_inputSystemBackend != null)
                return s_inputSystemBackend.TryGetTouch(index, out touch);
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (index >= 0 && index < Input.touchCount)
            {
                Touch unityTouch = Input.GetTouch(index);
                touch = new SorollaDiagnosticsInputTouch(unityTouch.fingerId, unityTouch.position, ConvertTouchPhase(unityTouch.phase));
                return true;
            }
#endif

            touch = default;
            return false;
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        static SorollaDiagnosticsInputTouchPhase ConvertTouchPhase(TouchPhase phase)
        {
            switch (phase)
            {
                case TouchPhase.Began:
                    return SorollaDiagnosticsInputTouchPhase.Began;
                case TouchPhase.Moved:
                    return SorollaDiagnosticsInputTouchPhase.Moved;
                case TouchPhase.Stationary:
                    return SorollaDiagnosticsInputTouchPhase.Stationary;
                case TouchPhase.Ended:
                    return SorollaDiagnosticsInputTouchPhase.Ended;
                case TouchPhase.Canceled:
                    return SorollaDiagnosticsInputTouchPhase.Canceled;
                default:
                    return SorollaDiagnosticsInputTouchPhase.None;
            }
        }
#endif
    }
}
