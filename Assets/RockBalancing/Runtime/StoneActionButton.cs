using UnityEngine.EventSystems;
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
        internal bool IsPressed => pointer != int.MinValue;

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
            bool valid = pointer == e.pointerId && e.button == PointerEventData.InputButton.Left && e.clickCount <= 1 &&
                !PointerCancellation.IsCanceled(e);
            Cancel();
            if (valid && IsInteractable()) Input.ActOnStone(stone, Discard);
        }
        public override void OnSubmit(BaseEventData e)
        {
            // Release/discard require an explicit pointer click, never Enter/Space.
        }
        public void Cancel() { selected = null; pointer = int.MinValue; }
        public override void OnPointerExit(PointerEventData e) { Cancel(); base.OnPointerExit(e); }
        protected override void OnDisable() { Cancel(); base.OnDisable(); }
    }
}
