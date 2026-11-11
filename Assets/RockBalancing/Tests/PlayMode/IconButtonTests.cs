using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RockBalancing.Tests
{
    public sealed class IconButtonTests
    {
        GameObject root;
        GameSession session;
        GameInput input;
        GameView view;
        ShareController share;
        Mouse mouse;
        Touchscreen touchscreen;
        int touchId;

        [UnitySetUp] public IEnumerator Setup()
        {
            root = new GameObject("Icon test scene", typeof(GameBootstrap));
            mouse = InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
            yield return null; yield return null;
            session = root.GetComponentInChildren<GameSession>(); input = root.GetComponent<GameInput>();
            view = root.GetComponent<GameView>(); share = root.GetComponent<ShareController>();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (touchscreen != null) InputSystem.RemoveDevice(touchscreen);
            InputSystem.RemoveDevice(mouse); touchscreen = null; Object.Destroy(root); yield return null;
        }
        void Pointer(Vector2 point, bool down = false) =>
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left, down));
        Vector2 Centre(Component component)
        {
            var rect = (RectTransform)component.transform;
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }
        IEnumerator Tap(Component target, bool touch = false)
        {
            var point = Centre(target);
            if (touch)
            {
                if (touchscreen == null) { touchscreen = InputSystem.AddDevice<Touchscreen>(); touchscreen.MakeCurrent(); yield return null; }
                int id = ++touchId;
                InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Began, position = point });
                yield return null;
                InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = point });
            }
            else { Pointer(point, true); yield return null; Pointer(point); }
            yield return null; yield return null;
        }
        Rect ScreenRect(Component component)
        {
            var corners = new Vector3[4]; ((RectTransform)component.transform).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
        Button[] Actions => new[] { view.RestartButton, view.ShareButton, view.ReleaseButton, view.DiscardButton };

        [UnityTest] public IEnumerator FourLucideIconsHaveTouchTargetsAndNoPermanentCaptions()
        {
            yield return null; Canvas.ForceUpdateCanvases();
            var icons = new System.Collections.Generic.HashSet<Sprite>();
            foreach (var button in Actions)
            {
                var presentation = button.GetComponent<IconButtonPresentation>();
                Assert.That(presentation, Is.Not.Null);
                Assert.That(presentation.Icon.sprite, Is.Not.Null);
                Assert.That(icons.Add(presentation.Icon.sprite), Is.True, "Each action has its own symbol");
                Assert.That(presentation.Icon.raycastTarget, Is.False);
                Assert.That(button.GetComponentsInChildren<Text>(), Is.Empty);
                var bounds = ScreenRect(button);
                Assert.That(bounds.width, Is.GreaterThanOrEqualTo(47.9f));
                Assert.That(bounds.height, Is.GreaterThanOrEqualTo(47.9f));
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(Screen.width));
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(Screen.height));
                foreach (var graphic in presentation.Tooltip.GetComponentsInChildren<Graphic>(true))
                    Assert.That(graphic.raycastTarget, Is.False, "Tooltip must not steal touch ownership");
            }
            Assert.That(ScreenRect(view.RestartButton).Overlaps(ScreenRect(view.ShareButton)), Is.False);
            Assert.That(ScreenRect(view.HeightLabel).Overlaps(ScreenRect(view.RestartButton)), Is.False);
            Assert.That(ScreenRect(view.ReleaseButton).Overlaps(ScreenRect(view.Root.Find("Adjustments"))), Is.False);
            Assert.That(ScreenRect(view.ReleaseButton).Overlaps(ScreenRect(view.DiscardButton)), Is.False);
        }

        [UnityTest] public IEnumerator MouseHoverShowsNamesPressShowsStateAndModalHidesTooltip()
        {
            foreach (var button in Actions)
            {
                Pointer(Centre(button)); yield return null; yield return null;
                var presentation = button.GetComponent<IconButtonPresentation>();
                Assert.That(presentation.Tooltip.gameObject.activeSelf, Is.True);
                Assert.That(presentation.Tooltip.GetComponentInChildren<Text>().text, Is.EqualTo(presentation.ActionName));
                var bounds = ScreenRect(presentation.Tooltip);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(0)); Assert.That(bounds.xMax, Is.LessThanOrEqualTo(Screen.width));
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(0)); Assert.That(bounds.yMax, Is.LessThanOrEqualTo(Screen.height));
                Pointer(Vector2.zero); yield return null;
                Assert.That(presentation.Tooltip.gameObject.activeSelf, Is.False);
            }
            var restart = view.RestartButton; var graphic = restart.targetGraphic;
            Pointer(Centre(restart), true); yield return new WaitForSecondsRealtime(.12f);
            Assert.That(Vector4.Distance(graphic.canvasRenderer.GetColor(), restart.colors.pressedColor), Is.LessThan(.02f));
            Assert.That(restart.GetComponent<IconButtonPresentation>().Tooltip.gameObject.activeSelf, Is.False);
            Pointer(Centre(restart)); yield return null; yield return null;
            Assert.That(input.Modal, Is.True);
            foreach (var button in Actions) Assert.That(button.GetComponent<IconButtonPresentation>().Tooltip.gameObject.activeSelf, Is.False);
        }

        [UnityTest] public IEnumerator MouseAndTouchIconsKeepReleaseDiscardAndRepeatGuards()
        {
            foreach (bool touch in new[] { false, true })
            {
                session.Restart(); yield return null; yield return new WaitForSecondsRealtime(.5f);
                var stone = session.Pending; var rotation = input.ViewCamera.transform.rotation;
                yield return Tap(view.ReleaseButton, touch);
                Assert.That(stone.Released, Is.True, "Release with touch=" + touch); Assert.That(stone.Discarded, Is.False);
                var next = session.Pending;
                yield return Tap(view.DiscardButton, touch);
                Assert.That(session.Pending, Is.SameAs(next), "Cross-button rapid repeat must not affect next supply");
                yield return new WaitForSecondsRealtime(.5f);
                yield return Tap(view.DiscardButton, touch);
                Assert.That(next.Released, Is.True); Assert.That(next.Discarded, Is.True);
                Assert.That(Quaternion.Angle(rotation, input.ViewCamera.transform.rotation), Is.LessThan(.01f));
                foreach (var button in Actions) Assert.That(button.GetComponent<IconButtonPresentation>().Tooltip.gameObject.activeSelf, Is.False);
            }
        }

        [UnityTest] public IEnumerator TouchRestartRetainsTextCancelKeepsGameAndConfirmClears()
        {
            var stone = session.Pending; var rotation = input.ViewCamera.transform.rotation;
            yield return Tap(view.RestartButton, true); Assert.That(input.Modal, Is.True);
            var dialog = view.Root.Find("Restart confirmation/Dialog");
            Assert.That(dialog.GetComponentsInChildren<Text>().Length, Is.EqualTo(4));
            var buttons = dialog.GetComponentsInChildren<Button>();
            yield return Tap(buttons[0], true);
            Assert.That(session.Pending, Is.SameAs(stone)); Assert.That(input.Modal, Is.False);
            Assert.That(stone.Released, Is.False);
            Assert.That(Quaternion.Angle(rotation, input.ViewCamera.transform.rotation), Is.LessThan(.01f));
            yield return Tap(view.RestartButton, true); yield return Tap(buttons[1], true);
            Assert.That(session.Pending, Is.Not.SameAs(stone)); Assert.That(session.Stones.Count, Is.EqualTo(1));
            Assert.That(input.Modal, Is.False);
        }

        [UnityTest] public IEnumerator TouchShareCancelAndCompletedCallbackReturnWithoutSceneActions()
        {
            var stone = session.Pending; var rotation = input.ViewCamera.transform.rotation;
            yield return Tap(view.ShareButton, true);
            var snapshot = share.Snapshot; Assert.That(snapshot, Is.Not.Null);
            Assert.That(session.ShareBusy, Is.True); Assert.That(view.ShareButton.interactable, Is.False);
            Assert.That(view.ReleaseButton.interactable, Is.False); Assert.That(view.DiscardButton.interactable, Is.False);
            yield return Tap(view.ReleaseButton, true);
            Assert.That(session.Pending, Is.SameAs(stone)); Assert.That(stone.Released, Is.False);
            yield return Tap(view.Root.Find("Share preview").GetComponentInChildren<Button>(), true);
            Assert.That(share.Snapshot, Is.Null); Assert.That(input.Modal, Is.False);
            yield return Tap(view.ShareButton, true);
            Assert.That(share.Snapshot, Is.Not.Null);
            share.OnShareClosed(session.Epoch.ToString()); yield return null; yield return null;
            Assert.That(share.Snapshot, Is.Null); Assert.That(input.Modal, Is.False); Assert.That(session.CanShare, Is.True);
            Assert.That(session.Pending, Is.SameAs(stone));
            Assert.That(Quaternion.Angle(rotation, input.ViewCamera.transform.rotation), Is.LessThan(.01f));
            foreach (var button in Actions) Assert.That(button.GetComponent<IconButtonPresentation>().Tooltip.gameObject.activeSelf, Is.False);
        }

        [UnityTest] public IEnumerator StabilizingDisablesAndDimsShareThenRestoresIt()
        {
            double now = 10; session.Clock = () => now;
            var stone = session.Pending; Assert.That(session.Release(stone), Is.True);
            for (int i = 0; i < 150 && !session.Stabilizing; i++) yield return new WaitForFixedUpdate();
            Assert.That(session.Stabilizing, Is.True); yield return null; yield return null;
            Assert.That(view.ShareButton.interactable, Is.False);
            Assert.That(view.ShareButton.GetComponent<IconButtonPresentation>().Icon.color.a, Is.LessThan(.4f));
            yield return Tap(view.ShareButton); Assert.That(share.Snapshot, Is.Null);
            now += 1.1; yield return null; yield return null;
            Assert.That(view.ShareButton.interactable, Is.True);
            Assert.That(view.ShareButton.GetComponent<IconButtonPresentation>().Icon.color.a, Is.EqualTo(1));
            yield return Tap(view.ShareButton); Assert.That(share.Snapshot, Is.Not.Null);
        }
    }
}
