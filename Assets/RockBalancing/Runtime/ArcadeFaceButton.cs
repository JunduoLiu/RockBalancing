using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RockBalancing
{
    sealed class ArcadeFaceButton : Button
    {
        GameView view;
        RectTransform rect, chevron;
        int direction;
        int pointer = int.MinValue;

        public void Initialize(GameView owner, int step, Color ink)
        {
            view = owner;
            direction = step;
            rect = (RectTransform)transform;
            rect.pivot = new Vector2(direction < 0 ? 0 : 1, .5f);
            targetGraphic = GetComponent<Image>();
            var colors = this.colors;
            colors.highlightedColor = new Color(.87f, .94f, .90f);
            colors.pressedColor = new Color(.48f, .65f, .56f);
            this.colors = colors;

            var center = Vector2.one * .5f;
            chevron = view.Panel(rect, "Chevron", center, center, Color.clear, false);
            chevron.localScale = new Vector3(direction, 1, 1);
            for (int side = -1; side <= 1; side += 2)
            {
                var stroke = view.Panel(chevron, "Stroke", center, center, ink, false);
                stroke.sizeDelta = new Vector2(12, 2.4f);
                stroke.anchoredPosition = new Vector2(0, side * 3.45f);
                stroke.localRotation = Quaternion.Euler(0, 0, -side * 48);
            }
        }

        public void Layout()
        {
            gameObject.SetActive(view.Input.ShowCabinetArrows);
            if (!gameObject.activeSelf) return;
            float scale = Mathf.Max(.01f, view.Root.GetComponent<Canvas>().scaleFactor);
            float units = 1 / Mathf.Min(1, scale);
            rect.sizeDelta = new Vector2(48, 48) * units;
            rect.anchoredPosition = Vector2.left * (direction * 8 * units);
            chevron.localScale = new Vector3(direction, 1, 1) * units;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || pointer != int.MinValue ||
                !IsInteractable() || !view.Input.CanTurnCabinet) return;
            pointer = eventData.pointerId;
            base.OnPointerDown(eventData);
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || pointer != eventData.pointerId) return;
            pointer = int.MinValue;
            if (eventData is ExtendedPointerEventData touchEvent && touchEvent.device is Touchscreen screen)
                foreach (var touch in screen.touches)
                    if (touch.touchId.ReadValue() == touchEvent.touchId && touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled)
                        return;
            if (IsInteractable()) view.Input.RequestCabinetTurn(direction);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            if (pointer == eventData.pointerId) pointer = int.MinValue;
            base.OnPointerExit(eventData);
        }

        protected override void OnDisable()
        {
            pointer = int.MinValue;
            base.OnDisable();
        }
    }
}
