using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RockBalancing
{
    // Presentation only: the existing Button/StoneActionButton owns every action.
    [DefaultExecutionOrder(310)]
    public sealed class IconButtonPresentation : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler
    {
        public Image Icon { get; private set; }
        public RectTransform Tooltip { get; private set; }
        public string ActionName { get; private set; }
        Button button;
        GameView view;
        Color ink;
        bool hovered;

        public void Initialize(GameView owner, Button action, string title, string resource, Color color)
        {
            view = owner;
            button = action;
            ink = color;
            ActionName = title;
            var rect = view.Panel(transform, "Lucide icon", Vector2.one * .5f, Vector2.one * .5f, Color.clear, false);
            Icon = rect.gameObject.AddComponent<Image>();
            Icon.sprite = Resources.Load<Sprite>("Icons/" + resource);
            Icon.preserveAspect = true;
            Icon.raycastTarget = false;
            Icon.color = ink;
            if (Icon.sprite == null) Debug.LogError("Missing action icon: " + resource);

            Tooltip = view.Panel(view.Root, title + " tooltip", Vector2.zero, Vector2.zero,
                new Color(.96f, .95f, .91f, 1), false);
            view.Label(Tooltip, title, Vector2.zero, Vector2.one, 14);
            Tooltip.gameObject.SetActive(false);

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.87f, .94f, .90f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(.48f, .65f, .56f);
            colors.disabledColor = new Color(.8f, .8f, .78f, .55f);
            colors.fadeDuration = .08f;
            button.colors = colors;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            // Touch hover is synthetic and must never leave a stuck label on mobile.
            hovered = !(e is ExtendedPointerEventData extended &&
                extended.pointerType == UIPointerType.Touch);
        }
        public void OnPointerExit(PointerEventData e) { Hide(); }
        public void OnPointerDown(PointerEventData e) { Hide(); }
        void OnDisable() { Hide(); }
        void OnApplicationFocus(bool focused) { if (!focused) Hide(); }
        void Hide()
        {
            hovered = false;
            if (Tooltip != null) Tooltip.gameObject.SetActive(false);
        }
        void OnDestroy()
        {
            if (Tooltip != null) Destroy(Tooltip.gameObject);
        }
        void LateUpdate()
        {
            if (button == null) return;
            if (view.Input.Modal) Hide();
            bool enabled = button.IsInteractable();
            Icon.color = enabled ? ink : new Color(ink.r, ink.g, ink.b, .32f);
            float scale = view.Root.GetComponent<Canvas>().scaleFactor;
            float size = Mathf.Max(26, 26 / Mathf.Max(.01f, scale));
            var bounds = ((RectTransform)transform).rect;
            size = Mathf.Min(size, Mathf.Min(bounds.width, bounds.height) * .6f);
            Icon.rectTransform.sizeDelta = Vector2.one * size;
            Tooltip.gameObject.SetActive(hovered && !view.Input.Modal);
            if (!Tooltip.gameObject.activeSelf) return;
            Tooltip.SetAsLastSibling();
            Tooltip.sizeDelta = new Vector2(112, 32) / Mathf.Min(1, Mathf.Max(.01f, scale));
            var control = (RectTransform)transform;
            var centre = (Vector2)view.Root.InverseTransformPoint(control.TransformPoint(control.rect.center)) + view.Root.rect.size * .5f;
            float halfWidth = Tooltip.sizeDelta.x * .5f, halfHeight = Tooltip.sizeDelta.y * .5f;
            float y = centre.y + bounds.height * .5f + halfHeight + 6;
            if (y + halfHeight > view.Root.rect.height - 4)
                y = centre.y - bounds.height * .5f - halfHeight - 6;
            Tooltip.anchoredPosition = new Vector2(
                Mathf.Clamp(centre.x, halfWidth + 4, view.Root.rect.width - halfWidth - 4),
                Mathf.Clamp(y, halfHeight + 4, view.Root.rect.height - halfHeight - 4));
        }
    }
}
