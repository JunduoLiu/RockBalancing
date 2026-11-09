using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RockBalancing
{
    public sealed class DragPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Action<Vector2> Dragged;
        public Action<bool> Held;
        int pointer = int.MinValue;
        public void OnPointerDown(PointerEventData e)
        {
            if (pointer != int.MinValue) return;
            pointer = e.pointerId;
            Held?.Invoke(true);
        }
        public void OnDrag(PointerEventData e)
        {
            if (pointer == e.pointerId) Dragged?.Invoke(e.delta / Mathf.Max(1, Screen.height));
        }
        public void OnPointerUp(PointerEventData e)
        {
            if (pointer != e.pointerId) return;
            pointer = int.MinValue;
            Held?.Invoke(false);
        }
        public void Cancel()
        {
            if(pointer==int.MinValue)return;
            pointer=int.MinValue;Held?.Invoke(false);
        }
        void OnDisable() { Cancel(); }
    }
}
