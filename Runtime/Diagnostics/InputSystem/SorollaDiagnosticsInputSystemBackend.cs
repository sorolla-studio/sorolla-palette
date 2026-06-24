#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.Scripting;
using InputSystemTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using InputSystemTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Sorolla.Palette
{
    [Preserve]
    internal sealed class SorollaDiagnosticsInputSystemBackend : ISorollaDiagnosticsInputBackend
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        [Preserve]
        static void Register()
        {
            if (!EnhancedTouchSupport.enabled)
                EnhancedTouchSupport.Enable();

            SorollaDiagnosticsInput.RegisterInputSystemBackend(new SorollaDiagnosticsInputSystemBackend());
        }

        public bool IsKeyboardTogglePressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.backquoteKey.wasPressedThisFrame;
        }

        public bool TryGetPointerTap(out Vector2 screenPosition)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                screenPosition = mouse.position.ReadValue();
                return true;
            }

            screenPosition = default;
            return false;
        }

        public bool TryGetPointerHold(out Vector2 screenPosition)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                screenPosition = mouse.position.ReadValue();
                return true;
            }

            screenPosition = default;
            return false;
        }

        public int TouchCount => EnhancedTouchSupport.enabled ? InputSystemTouch.activeTouches.Count : 0;

        public bool TryGetTouch(int index, out SorollaDiagnosticsInputTouch touch)
        {
            if (!EnhancedTouchSupport.enabled)
            {
                touch = default;
                return false;
            }

            var activeTouches = InputSystemTouch.activeTouches;
            if (index < 0 || index >= activeTouches.Count)
            {
                touch = default;
                return false;
            }

            InputSystemTouch inputTouch = activeTouches[index];
            touch = new SorollaDiagnosticsInputTouch(inputTouch.touchId, inputTouch.screenPosition, ConvertTouchPhase(inputTouch.phase));
            return true;
        }

        static SorollaDiagnosticsInputTouchPhase ConvertTouchPhase(InputSystemTouchPhase phase)
        {
            switch (phase)
            {
                case InputSystemTouchPhase.Began:
                    return SorollaDiagnosticsInputTouchPhase.Began;
                case InputSystemTouchPhase.Moved:
                    return SorollaDiagnosticsInputTouchPhase.Moved;
                case InputSystemTouchPhase.Stationary:
                    return SorollaDiagnosticsInputTouchPhase.Stationary;
                case InputSystemTouchPhase.Ended:
                    return SorollaDiagnosticsInputTouchPhase.Ended;
                case InputSystemTouchPhase.Canceled:
                    return SorollaDiagnosticsInputTouchPhase.Canceled;
                default:
                    return SorollaDiagnosticsInputTouchPhase.None;
            }
        }
    }
}
#endif
