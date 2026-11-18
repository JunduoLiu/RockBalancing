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
            pad.RelativeToPress = true;
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
            var stickPoint = camera.WorldToViewportPoint(cabinet.StickGrip);
            var liftPoint = ScreenAnchor(camera, cabinet.LiftButton);
            var lowerPoint = ScreenAnchor(camera, cabinet.LowerButton);
            var releasePoint = ScreenAnchor(camera, cabinet.ReleaseButton);
            float minSize = 48 / root.GetComponentInParent<Canvas>().scaleFactor;
            var keyCenter = cabinet.ReleaseButton.TransformPoint(Vector3.up * .05f);
            var keySide = camera.WorldToViewportPoint(keyCenter + camera.transform.right);
            float unitsPerMetre = root.rect.width * (keySide.x - releasePoint.x);
            float keySize = Mathf.Max(minSize, .20f * unitsPerMetre);
            float stickSize = Mathf.Max(minSize, .24f * unitsPerMetre);
            float releaseSize = Mathf.Max(minSize, .22f * unitsPerMetre);
            // Build neighboring hit areas around the actual controls. Height buttons
            // can grow away from their shared edge when the pair is close on screen.
            float stickEdge = (stickPoint.x + liftPoint.x) * .5f;
            float heightEdge = (liftPoint.x + lowerPoint.x) * .5f;
            float releaseEdge = (lowerPoint.x + releasePoint.x) * .5f;
            Place(stick, stickPoint, stickSize, 0, stickEdge);
            Place(lift, liftPoint, keySize, stickEdge, heightEdge);
            Place(lower, lowerPoint, keySize, heightEdge, releaseEdge);
            Place(release, releasePoint, releaseSize, releaseEdge, 1);
        }

        public void ShowControls() => cabinet.ShowControls(stickOffset, raising, lowering, ReleaseButton.IsPressed);

        static Vector2 ScreenAnchor(Camera camera, Transform key) =>
            camera.WorldToViewportPoint(key.TransformPoint(Vector3.up * .05f));

        void Place(RectTransform rect, Vector2 anchor, float size, float left, float right)
        {
            float width = Mathf.Min(size / root.rect.width, right - left);
            anchor.x = Mathf.Clamp(anchor.x, left + width * .5f, right - width * .5f);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = new Vector2(width * root.rect.width, size);
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
