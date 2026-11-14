using UnityEngine;
#if ENABLE_INPUT_SYSTEM && BP_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Single-pointer input (mouse or first touch) that works with either the new
    /// Input System package or the legacy Input Manager, whichever the project enables.
    /// </summary>
    public static class PointerInput
    {
        public static bool Down { get; private set; }
        public static bool Held { get; private set; }
        public static bool Up { get; private set; }
        public static Vector2 Position { get; private set; }

        static int _frame = -1;

        public static void Poll()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;

#if ENABLE_INPUT_SYSTEM && BP_INPUT_SYSTEM
            var p = Pointer.current;
            if (p == null) { Down = Held = Up = false; return; }
            Position = p.position.ReadValue();
            Down = p.press.wasPressedThisFrame;
            Held = p.press.isPressed;
            Up = p.press.wasReleasedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                Position = t.position;
                Down = t.phase == TouchPhase.Began;
                Up = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
                Held = !Up;
            }
            else
            {
                Position = Input.mousePosition;
                Down = Input.GetMouseButtonDown(0);
                Held = Input.GetMouseButton(0);
                Up = Input.GetMouseButtonUp(0);
            }
#else
            Down = Held = Up = false;
#endif
        }
    }
}
