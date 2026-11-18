using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace RockBalancing.Tests
{
    public sealed class ArcadeViewTests
    {
        const string ScenePath = "Assets/RockBalancing/Scenes/Arcade.unity";
        const string Previous = "Previous cabinet face";
        const string Next = "Next cabinet face";
        Touchscreen touchscreen;
        Keyboard keyboard;
        Scene scene;
        GameObject root;
        Camera camera;
        GameSession session;

        [UnitySetUp]
        public IEnumerator OpenArcade()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByPath(ScenePath);
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            touchscreen.MakeCurrent();
            yield return null;
            yield return null;
            root = scene.GetRootGameObjects().Single(go => go.GetComponent<GameBootstrap>() != null);
            camera = root.GetComponent<GameInput>().ViewCamera;
            session = root.GetComponentInChildren<GameSession>();
        }

        [UnityTearDown]
        public IEnumerator UnloadArcade()
        {
            if (touchscreen != null) InputSystem.RemoveDevice(touchscreen);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator BothScreenEdgesReachAdjacentWindowsAndWrapAroundTheSameStone()
        {
            var stone = session.Pending;
            var position = stone.transform.position;
            var rotation = stone.transform.rotation;
            var rightEdge = Control(Next);
            Assert.That(Point(rightEdge).x, Is.GreaterThan(Screen.width * .8f));
            Assert.That(Point(Control(Previous)).x, Is.LessThan(Screen.width * .2f));
            foreach (var face in new[] { Vector3.left, Vector3.back, Vector3.right, Vector3.forward })
            {
                yield return Tap(Point(rightEdge));
                AssertFacing(face);
            }
            yield return Tap(Point(Control(Previous)));
            AssertFacing(Vector3.right);
            yield return Tap(Point(rightEdge));
            AssertFacing(Vector3.forward);
            Assert.That(session.Pending, Is.SameAs(stone));
            Assert.That(stone.transform.position, Is.EqualTo(position));
            Assert.That(stone.transform.rotation, Is.EqualTo(rotation));
        }

        [UnityTest]
        public IEnumerator ASecondFingerCannotTurnWhileAnyFrontAdjustmentIsHeld()
        {
            var leftEdge = Control(Previous);
            var rightEdge = Control(Next);
            var left = Point(leftEdge);
            var right = Point(rightEdge);
            var stone = session.Pending;
            foreach (var control in new[] { "Move", "Lift", "Lower", "Stone" })
            {
                Vector2 point = control == "Stone" ? camera.WorldToScreenPoint(stone.transform.position) : Point(Control(control));
                Touch(1, TouchPhase.Began, point);
                yield return null;
                yield return null;
                Assert.That(leftEdge.gameObject.activeInHierarchy || rightEdge.gameObject.activeInHierarchy,
                    Is.False, $"Holding {control} should immediately hide both arrows.");
                yield return Tap(left, 2);
                yield return Tap(right, 3);
                AssertFacing(Vector3.forward);
                Assert.That(session.Pending, Is.SameAs(stone));

                // A finger that landed at the edge while the arrow was hidden
                // cannot navigate by lifting after the primary operation ends.
                Touch(4, TouchPhase.Began, right);
                yield return null;
                Touch(1, TouchPhase.Ended, point);
                yield return null;
                yield return null;
                Assert.That(leftEdge.gameObject.activeInHierarchy && rightEdge.gameObject.activeInHierarchy, Is.True);
                Touch(4, TouchPhase.Ended, right);
                yield return null;
                yield return null;
                AssertFacing(Vector3.forward);
            }
            yield return Tap(right);
            AssertFacing(Vector3.left);
        }

        [UnityTest]
        public IEnumerator ObservationWindowsLeaveTheStoneUntouchedAndCanFollowItsRelease()
        {
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            var stone = session.Pending;
            var position = stone.transform.position;
            var rotation = stone.transform.rotation;
            var stick = Point(Control("Move"));
            var lift = Point(Control("Lift"));
            var lower = Point(Control("Lower"));
            var release = Point(Control("Release"));
            var next = Point(Control(Next));

            foreach (var face in new[] { Vector3.left, Vector3.back, Vector3.right })
            {
                yield return Tap(next);
                AssertFacing(face);
                yield return Drag(stick, new Vector2(30, 30));
                yield return Drag(lift, Vector2.zero);
                yield return Drag(lower, Vector2.zero);
                yield return Drag(camera.WorldToScreenPoint(position), Vector2.right * 44);
                yield return Tap(release);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.UpArrow));
                yield return null;
                yield return new WaitForSeconds(.1f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(session.Pending, Is.SameAs(stone));
                Assert.That(stone.transform.position, Is.EqualTo(position));
                Assert.That(stone.transform.rotation, Is.EqualTo(rotation));
                Assert.That(stone.Released, Is.False);
            }

            yield return Tap(next);
            AssertFacing(Vector3.forward);
            Touch(1, TouchPhase.Began, release);
            yield return null;
            Touch(1, TouchPhase.Ended, release);
            yield return null;
            Assert.That(stone.Released, Is.True);
            var releaseHeight = stone.transform.position.y;

            yield return Tap(next);
            AssertFacing(Vector3.left);
            Assert.That(session.Pending, Is.Not.SameAs(stone));
            yield return new WaitForFixedUpdate();
            Assert.That(stone.transform.position.y, Is.LessThan(releaseHeight));
            var fallingStone = camera.WorldToViewportPoint(stone.transform.position);
            Assert.That(fallingStone.x, Is.InRange(.01f, .99f));
            Assert.That(fallingStone.y, Is.InRange(.01f, .99f));
        }

        [UnityTest]
        public IEnumerator PhoneFramingShowsTheFrontConsoleAndKeepsTheGapClearThroughBothSideWindows()
        {
            camera.aspect = 390f / 844f;
            yield return null;
            var cabinet = root.GetComponent<ArcadeCabinet>();
            var controls = new[] { cabinet.Joystick, cabinet.LiftButton, cabinet.LowerButton, cabinet.ReleaseButton };
            foreach (var control in controls)
                AssertInView(control.GetComponentInChildren<Renderer>().bounds.center);
            var claw = cabinet.GetComponentsInChildren<Renderer>().Single(renderer => renderer.name == "Claw head");
            AssertInView(claw.bounds.center);
            var top = session.Base.bounds.center + Vector3.up * session.Base.bounds.extents.y;
            AssertInView(top);

            foreach (var arrow in new[] { Next, Previous })
            {
                yield return Tap(Point(Control(arrow)));
                var frustum = GeometryUtility.CalculateFrustumPlanes(camera);
                foreach (var control in controls)
                    Assert.That(GeometryUtility.TestPlanesAABB(frustum, control.GetComponentInChildren<Renderer>().bounds),
                        Is.False, $"{control.name} should be outside the side frame.");
                var stone = session.Pending.Shape.bounds;
                var lowerEdge = stone.center + Vector3.down * stone.extents.y;
                AssertInView(top);
                AssertInView(lowerEdge);
                Assert.That(camera.WorldToViewportPoint(lowerEdge).y - camera.WorldToViewportPoint(top).y, Is.GreaterThan(.035f));
                yield return Tap(Point(Control(arrow == Next ? Previous : Next)));
            }
        }

        void AssertInView(Vector3 point)
        {
            var pixel = camera.WorldToViewportPoint(point);
            Assert.That(pixel.x, Is.InRange(.01f, .99f));
            Assert.That(pixel.y, Is.InRange(.01f, .99f));
            Assert.That(pixel.z, Is.GreaterThan(camera.nearClipPlane));
        }

        void AssertFacing(Vector3 outside)
        {
            var heading = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            Assert.That(Vector3.Dot(heading, -outside), Is.GreaterThan(.999f));
            Assert.That(Vector3.Dot(camera.transform.position, outside), Is.GreaterThan(1),
                "The camera should observe from outside the glass.");
        }

        RectTransform Control(string name)
        {
            var control = root.GetComponentsInChildren<RectTransform>(true).SingleOrDefault(rect => rect.name == name);
            Assert.That(control, Is.Not.Null, $"Missing cabinet interaction: {name}");
            return control;
        }

        static Vector2 Point(RectTransform rect) =>
            RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));

        IEnumerator Tap(Vector2 point, int id = 1)
        {
            Touch(id, TouchPhase.Began, point);
            yield return null;
            Touch(id, TouchPhase.Ended, point);
            yield return null;
            yield return null;
        }

        IEnumerator Drag(Vector2 point, Vector2 delta)
        {
            Touch(1, TouchPhase.Began, point);
            yield return null;
            Touch(1, TouchPhase.Moved, point + delta);
            yield return new WaitForSeconds(.1f);
            Touch(1, TouchPhase.Ended, point + delta);
            yield return null;
            yield return null;
        }

        void Touch(int id, TouchPhase phase, Vector2 position)
        {
            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = id,
                phase = phase,
                position = position
            });
        }
    }
}
