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
            view.Label(dialog, "设置", new Vector2(.07f, .67f), new Vector2(.93f, .94f), 24);

            var row = view.Panel(dialog, "刻度旋转", new Vector2(.07f, .36f), new Vector2(.93f, .65f),
                new Color(.89f, .91f, .87f), true);
            view.Label(row, "刻度旋转", new Vector2(.06f, 0), new Vector2(.74f, 1),
                18, TextAnchor.MiddleLeft);
            var state = view.Panel(row, "Rotation setting state", new Vector2(.76f, .12f),
                new Vector2(.95f, .88f), paper, false);
            var stateBox = state.GetComponent<Image>();
            var stateText = view.Label(state, "关", Vector2.zero, Vector2.one, 16);

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = row.GetComponent<Image>();
            toggle.isOn = input.StepRotationEnabled;
            UpdateState(toggle.isOn);
            toggle.onValueChanged.AddListener(UpdateState);
            void UpdateState(bool enabled)
            {
                input.SetStepRotation(enabled);
                stateText.text = enabled ? "开" : "关";
                stateText.color = enabled ? Color.white : ink;
                stateBox.color = enabled ? new Color(.17f, .42f, .34f) : paper;
            }

            view.ButtonAt(dialog, "完成", new Vector2(.24f, .07f), new Vector2(.76f, .33f), () => Show(false));
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
