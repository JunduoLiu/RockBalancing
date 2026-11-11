using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RockBalancing
{
    public sealed class DragPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Action<Vector2> Changed;
        public Action<bool> Held;
        public Func<bool> CanBegin;
        public bool VerticalOnly;
        public RectTransform Handle;
        int pointer = int.MinValue;
        public void OnPointerDown(PointerEventData e)
        {
            if (pointer != int.MinValue || (CanBegin != null && !CanBegin())) return;
            pointer = e.pointerId;
            Held?.Invoke(true);
            ReadOffset(e);
        }
        public void OnDrag(PointerEventData e)
        {
            if (pointer == e.pointerId) ReadOffset(e);
        }
        public void OnPointerUp(PointerEventData e)
        {
            if (pointer != e.pointerId) return;
            Cancel();
        }
        void ReadOffset(PointerEventData e)
        {
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out var p)) return;
            p -= rect.rect.center;
            float radius = Mathf.Max(1, Mathf.Min(rect.rect.width, rect.rect.height) * .36f);
            var value = VerticalOnly ? new Vector2(0, Mathf.Clamp(p.y / radius, -1, 1)) : Vector2.ClampMagnitude(p / radius, 1);
            if (Handle != null) Handle.anchoredPosition = value * radius;
            Changed?.Invoke(value);
        }
        public void Cancel()
        {
            bool wasHeld = pointer != int.MinValue;
            pointer = int.MinValue;
            if (Handle != null) Handle.anchoredPosition = Vector2.zero;
            Changed?.Invoke(Vector2.zero);
            if (wasHeld) Held?.Invoke(false);
        }
        void OnDisable() { Cancel(); }
    }
}
