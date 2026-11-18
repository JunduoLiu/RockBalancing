using System.Collections;
using System.Linq;
using System.Threading.Tasks;
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

        [UnityTest]
        public IEnumerator CanceledFacePressesCannotTurnAfterBrowserFocusReturns()
        {
            var input = root.GetComponent<GameInput>();
            var arrow = Point(Control(Next));
            Touch(1, TouchPhase.Began, arrow);
            yield return null;
            input.SetBrowserInputFocus("0");
            yield return null;
            input.SetBrowserInputFocus("1");
            Touch(1, TouchPhase.Ended, arrow);
            yield return null;
            yield return null;
            AssertFacing(Vector3.forward);

            Touch(2, TouchPhase.Began, arrow);
            yield return null;
            input.CancelForFocusLoss();
            Touch(2, TouchPhase.Ended, arrow);
            yield return null;
            yield return null;
            AssertFacing(Vector3.forward);

            Touch(3, TouchPhase.Began, arrow);
            yield return null;
            Touch(3, TouchPhase.Canceled, arrow);
            yield return null;
            yield return null;
            AssertFacing(Vector3.forward);

            yield return Tap(arrow, 4);
            AssertFacing(Vector3.left);
        }

        [UnityTest]
        public IEnumerator ReleaseDepressesThePhysicalButtonAndOpensTheClawBeforeItGripsTheNextStone()
        {
            var cabinet = root.GetComponent<ArcadeCabinet>();
            var stone = session.Pending;
            Assert.That(session.Adjust(stone, stone.transform.position + Vector3.right * .24f, stone.transform.rotation), Is.True);
            yield return null;
            yield return null;
            var head = cabinet.GetComponentsInChildren<Renderer>().Single(renderer => renderer.name == "Claw head");
            var jaw = cabinet.GetComponentsInChildren<Renderer>().Single(renderer => renderer.name == "Claw jaw 1");
            var closed = jaw.transform.localRotation;
            var buttonHeight = cabinet.ReleaseButton.position.y;
            var release = Point(Control("Release"));
            Touch(1, TouchPhase.Began, release);
            yield return null;
            yield return null;
            Assert.That(cabinet.ReleaseButton.position.y, Is.LessThan(buttonHeight - .006f));
            Touch(1, TouchPhase.Ended, release);
            yield return null;
            Assert.That(stone.Released, Is.True);
            var next = session.Pending;
            Assert.That(next, Is.Not.Null.And.Not.SameAs(stone));

            yield return Tap(Point(Control(Next)), 2);
            AssertFacing(Vector3.left);
            yield return new WaitForSeconds(.05f);
            Assert.That(Quaternion.Angle(closed, jaw.transform.localRotation), Is.GreaterThan(15));
            Assert.That(head.bounds.center.x, Is.GreaterThan(.10f), "The claw has to let go before returning to supply.");

            yield return new WaitForSeconds(.38f);
            Assert.That(head.bounds.center.x, Is.EqualTo(next.transform.position.x).Within(.005f));
            Assert.That(jaw.bounds.min.y, Is.LessThan(next.HighestPoint()));
            Assert.That(cabinet.ReleaseButton.position.y, Is.EqualTo(buttonHeight));
        }

        [UnityTest]
        public IEnumerator HoldingThePhysicalJoystickGripDoesNotMoveTheStoneUntilDragged()
        {
            camera.aspect = 430f / 900f;
            yield return null;
            var cabinet = root.GetComponent<ArcadeCabinet>();
            var stone = session.Pending;
            var start = stone.transform.position;
            var knob = (Vector2)camera.WorldToScreenPoint(cabinet.Joystick.position + new Vector3(0, .159f, -.013f));
            Touch(1, TouchPhase.Began, knob);
            yield return new WaitForSeconds(.24f);
            Assert.That(stone.transform.position, Is.EqualTo(start));
            AssertFacing(Vector3.forward);
            Assert.That(Control(Next).gameObject.activeInHierarchy, Is.False);

            var dragged = knob + Vector2.right * 35;
            Touch(1, TouchPhase.Moved, dragged);
            yield return new WaitForSeconds(.18f);
            Assert.That(stone.transform.position.x, Is.LessThan(start.x - .02f));
            Touch(1, TouchPhase.Ended, dragged);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LandscapeSideWindowsShowTheSupportAndGapWithTheConsoleOutsideTheFrame()
        {
            camera.aspect = 16f / 9f;
            var cabinet = root.GetComponent<ArcadeCabinet>();
            session.Initialize(new StackableStones(), session.Base, session.Ground);
            yield return null;

            for (int layer = 0; layer < 2; layer++)
            {
                foreach (var arrow in new[] { Next, Previous })
                {
                    yield return Tap(Point(Control(arrow)));
                    var top = Vector3.up * session.TowerTop();
                    var bounds = session.Pending.Shape.bounds;
                    var lowerEdge = bounds.center + Vector3.down * bounds.extents.y;
                    AssertInView(top);
                    AssertInView(lowerEdge);
                    Assert.That(camera.WorldToViewportPoint(lowerEdge).y - camera.WorldToViewportPoint(top).y,
                        Is.GreaterThan(.035f));
                    var frustum = GeometryUtility.CalculateFrustumPlanes(camera);
                    foreach (var control in new[] { cabinet.Joystick, cabinet.LiftButton, cabinet.LowerButton, cabinet.ReleaseButton })
                        Assert.That(GeometryUtility.TestPlanesAABB(frustum, control.GetComponent<Renderer>().bounds),
                            Is.False, $"{control.name} should be outside the side window.");
                    yield return Tap(Point(Control(arrow == Next ? Previous : Next)));
                }

                if (layer != 0) continue;
                var restingStone = session.Pending;
                Assert.That(session.Adjust(restingStone, Vector3.up * (session.BaseTop + .11f), Quaternion.identity), Is.True);
                Assert.That(session.Release(restingStone), Is.True);
                yield return new WaitForSeconds(.35f);
                Assert.That(session.TowerTop(), Is.GreaterThan(session.BaseTop + .15f));
                Assert.That(session.Adjust(session.Pending, Vector3.up * (session.TowerTop() + .28f), Quaternion.identity), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator AHighTowerStillLeavesRoomToGripRotateAndLiftTheNextStone()
        {
            session.Initialize(new StackableStones(), session.Base, session.Ground);
            for (int i = 0; i < 4; i++)
            {
                var stone = session.Pending;
                Assert.That(session.Adjust(stone, Vector3.up * (session.TowerTop() + .11f), Quaternion.identity), Is.True);
                Assert.That(session.Release(stone), Is.True);
                yield return new WaitForSeconds(.35f);
            }

            Assert.That(session.TowerTop(), Is.GreaterThan(.85f));
            var previous = session.Pending;
            Assert.That(session.Adjust(previous, new Vector3(.34f, previous.transform.position.y, 0), Quaternion.identity), Is.True);
            Assert.That(session.Release(previous), Is.True);
            yield return null;
            yield return null;

            var pending = session.Pending;
            Assert.That(pending, Is.Not.SameAs(previous));
            Assert.That(pending.transform.position.y, Is.GreaterThan(1.1f));
            var claw = root.GetComponentsInChildren<Renderer>().Single(renderer => renderer.name == "Claw head");
            Assert.That(claw.bounds.min.y, Is.GreaterThan(pending.HighestPoint()));
            AssertInView(pending.Shape.bounds.max);
            AssertInView(claw.bounds.center);

            var start = pending.transform.position.y;
            var clawStart = claw.bounds.center.y;
            yield return Drag(Point(Control("Lift")), Vector2.zero);
            Assert.That(pending.transform.position.y, Is.GreaterThan(start + .02f));
            Assert.That(claw.bounds.center.y - clawStart, Is.EqualTo(pending.transform.position.y - start).Within(.005f));

            Assert.That(session.AdjustRotationStep(pending, Quaternion.Euler(0, 0, 38)), Is.True);
            yield return null;
            yield return null;
            Assert.That(claw.bounds.min.y, Is.GreaterThan(pending.HighestPoint()));
            Assert.That(root.GetComponentsInChildren<Renderer>()
                .Where(renderer => renderer.name.StartsWith("Claw jaw"))
                .Select(renderer => renderer.bounds.min.y).Min(), Is.LessThan(pending.HighestPoint()));
            var pane = root.GetComponentsInChildren<Renderer>().Single(renderer => renderer.name == "Glass Front");
            Assert.That(pane.bounds.max.y, Is.GreaterThan(claw.bounds.max.y));
            AssertInView(pending.Shape.bounds.max);
        }

        sealed class StackableStones : IStoneFactory
        {
            public Task<Stone> CreateAsync(Vector3 position, Transform parent)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(parent);
                cube.transform.position = position;
                cube.transform.localScale = Vector3.one * .20f;
                cube.GetComponent<Collider>().isTrigger = true;
                cube.AddComponent<Rigidbody>().isKinematic = true;
                return Task.FromResult(cube.AddComponent<Stone>());
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
