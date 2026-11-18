using UnityEngine;
using UnityEngine.UI;

namespace RockBalancing
{
    // Transparent touch targets stay on top of the actual cabinet controls.
    sealed class ArcadeConsole
    {
        readonly ArcadeCabinet cabinet;
        readonly GameInput input;
        readonly RectTransform root, stick, lift, lower, release;
        Vector2 stickOffset;
        bool raising, lowering;
        public StoneActionButton ReleaseButton { get; }

        public ArcadeConsole(GameView view, RectTransform root, ArcadeCabinet cabinet)
        {
            this.root = root;
            this.cabinet = cabinet;
            input = view.Input;
            stick = Target(view, "Move");
            var pad = stick.gameObject.AddComponent<DragPad>();
            pad.CanBegin = () => input.CanAdjust;
            pad.Held = input.HoldPlanar;
            pad.Changed = value => { stickOffset = value; input.MoveStone(value); };

            lift = CreateHeightButton(view, "Lift", held => { raising = held; UpdateHeight(); });
            lower = CreateHeightButton(view, "Lower", held => { lowering = held; UpdateHeight(); });
            release = Target(view, "Release");
            ReleaseButton = release.gameObject.AddComponent<StoneActionButton>();
            ReleaseButton.Input = input;
            ReleaseButton.transition = Selectable.Transition.None;
        }

        RectTransform Target(GameView view, string name) =>
            view.Panel(root, name, Vector2.zero, Vector2.zero, Color.clear, true);

        RectTransform CreateHeightButton(GameView view, string name, System.Action<bool> held)
        {
            var target = Target(view, name);
            var button = target.gameObject.AddComponent<DragPad>();
            button.CanBegin = () => input.CanAdjust;
            button.Held = held;
            return target;
        }

        void UpdateHeight()
        {
            input.HoldHeight(raising || lowering);
            input.RaiseStone(Vector2.up * ((raising ? 1 : 0) - (lowering ? 1 : 0)));
        }

        public void Layout()
        {
            var camera = input.ViewCamera;
            var liftPoint = ScreenAnchor(camera, cabinet.LiftButton.position);
            var lowerPoint = ScreenAnchor(camera, cabinet.LowerButton.position);
            float keySpacing = Mathf.Abs(liftPoint.x - lowerPoint.x) * root.rect.width;
            float minSize = 48 / root.GetComponentInParent<Canvas>().scaleFactor;
            float unitsPerMetre = root.rect.height / (2 * camera.orthographicSize);
            // The two height keys sit close together. Keep their hit areas separate
            // even on a narrow phone while extending them above and below the deck.
            float keyHeight = Mathf.Max(minSize, .20f * unitsPerMetre);
            float keyWidth = Mathf.Min(keySpacing, keyHeight);
            float stickSize = Mathf.Max(minSize, .24f * unitsPerMetre);
            float releaseSize = Mathf.Max(minSize, .22f * unitsPerMetre);
            Place(stick, camera.WorldToViewportPoint(cabinet.Joystick.position + Vector3.up * .13f),
                new Vector2(stickSize, stickSize));
            Place(lift, liftPoint, new Vector2(keyWidth, keyHeight));
            Place(lower, lowerPoint, new Vector2(keyWidth, keyHeight));
            Place(release, ScreenAnchor(camera, cabinet.ReleaseButton.position), new Vector2(releaseSize, releaseSize));
            cabinet.ShowControls(stickOffset, raising, lowering);
        }

        static Vector2 ScreenAnchor(Camera camera, Vector3 key) =>
            camera.WorldToViewportPoint(key + Vector3.up * .045f);

        static void Place(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
