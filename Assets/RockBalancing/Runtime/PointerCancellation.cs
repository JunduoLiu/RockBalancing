using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;

namespace RockBalancing
{
    internal static class PointerCancellation
    {
        public static bool IsCanceled(TouchControl touch) => touch.phase.ReadValue() == TouchPhase.Canceled;

        public static bool IsCanceled(PointerEventData pointer)
        {
            // The UI module can raise a click before GameInput reads the canceled touch.
            if (pointer is ExtendedPointerEventData touchEvent && touchEvent.device is Touchscreen screen)
                foreach (var touch in screen.touches)
                    if (touch.touchId.ReadValue() == touchEvent.touchId && IsCanceled(touch))
                        return true;
            return false;
        }
    }
}
