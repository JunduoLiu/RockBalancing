using UnityEngine;
using UnityEngine.UI;

namespace RockBalancing
{
    sealed class ArcadeSettings
    {
        readonly GameInput input;
        readonly GameObject panel;
        readonly RectTransform dialog, root;
        public Button MenuButton { get; }

        public ArcadeSettings(GameView view, Transform actions, Color paper, Color ink)
        {
            input = view.Input;
            root = view.Root;
            MenuButton = view.ButtonAt(actions, "设置", Vector2.zero, Vector2.zero, () => Show(true));
            MenuButton.GetComponentInChildren<Text>().resizeTextMaxSize = 14;
            panel = view.Panel(view.Root, "Arcade settings", Vector2.zero, Vector2.one,
                new Color(.1f, .15f, .16f, .85f), true).gameObject;
            dialog = view.Panel(panel.transform, "Settings dialog", Vector2.one * .5f,
                Vector2.one * .5f, paper, true);
            view.Label(dialog, "设置", new Vector2(.07f, .73f), new Vector2(.93f, .94f), 24);
            view.Label(dialog, "旋转", new Vector2(.07f, .58f), new Vector2(.93f, .73f), 17, TextAnchor.MiddleLeft);

            float configuredStep = Mathf.Clamp(input.RotationStepDegrees, 1, 179);
            var modes = configuredStep == 15 || configuredStep == 30
                ? new[] { 0f, 15f, 30f }
                : new[] { 0f, 15f, 30f, configuredStep };
            var buttons = new Button[modes.Length];
            float width = .86f / modes.Length;
            for (int i = 0; i < modes.Length; i++)
            {
                int choice = i;
                string name = modes[i] == 0 ? "自由" : $"{modes[i]:0.##}°";
                float left = .07f + i * width;
                buttons[i] = view.ButtonAt(dialog, name, new Vector2(left + .008f, .36f),
                    new Vector2(left + width - .008f, .57f), () => SelectMode(choice));
                buttons[i].GetComponentInChildren<Text>().resizeTextMaxSize = 18;
            }

            SelectMode(0);
            void SelectMode(int selection)
            {
                input.SetRotationMode(modes[selection]);
                for (int i = 0; i < buttons.Length; i++)
                {
                    bool selected = i == selection;
                    buttons[i].targetGraphic.color = selected ? new Color(.17f, .42f, .34f) : new Color(.89f, .91f, .87f);
                    var label = buttons[i].GetComponentInChildren<Text>();
                    label.color = selected ? Color.white : ink;
                    label.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
                }
            }

            view.ButtonAt(dialog, "完成", new Vector2(.24f, .07f), new Vector2(.76f, .30f), () => Show(false));
            panel.SetActive(false);
        }

        public void Layout(float size, float gap, float scale)
        {
            var menu = (RectTransform)MenuButton.transform;
            menu.pivot = Vector2.zero;
            menu.sizeDelta = Vector2.one * size;
            menu.anchoredPosition = new Vector2(size + gap, 0);
            dialog.sizeDelta = new Vector2(
                Mathf.Min(root.rect.width * .88f, Mathf.Max(320, 260 / scale)),
                Mathf.Min(root.rect.height * .9f, Mathf.Max(220, 185 / scale)));
        }

        void Show(bool open)
        {
            input.Modal = open;
            panel.SetActive(open);
        }
    }
}
