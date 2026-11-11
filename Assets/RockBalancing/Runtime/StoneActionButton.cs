using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RockBalancing
{
    // Bind the intention to the stone at pointer-down, never to a later supply.
    public sealed class StoneActionButton : Button
    {
        public GameInput Input;
        public bool Discard;
        Stone selected;
        int pointer = int.MinValue;

        public override void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || pointer != int.MinValue || !IsInteractable() || !Input.CanAct) return;
            selected = Input.Session.Pending;
            pointer = e.pointerId;
            base.OnPointerDown(e);
        }
        public override void OnPointerClick(PointerEventData e)
        {
            var stone = selected;
            bool valid = pointer == e.pointerId && e.button == PointerEventData.InputButton.Left && e.clickCount <= 1;
            // InputSystemUIInputModule can emit a click for a canceled touch.
            // Check its source before the later scene-input update sees cancellation.
            if (e is ExtendedPointerEventData touchEvent && touchEvent.device is Touchscreen screen)
                foreach (var touch in screen.touches)
                    if (touch.touchId.ReadValue() == touchEvent.touchId && touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled)
                        valid = false;
            Cancel();
            if (valid && IsInteractable()) Input.ActOnStone(stone, Discard);
        }
        public override void OnSubmit(BaseEventData e)
        {
            if (IsInteractable()) Input.ActOnStone(Input.Session.Pending, Discard);
        }
        public void Cancel() { selected = null; pointer = int.MinValue; }
        public override void OnPointerExit(PointerEventData e) { Cancel(); base.OnPointerExit(e); }
        protected override void OnDisable() { Cancel(); base.OnDisable(); }
    }
}
