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
        RenderTexture renderTarget;
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
            if (renderTarget != null)
            {
                camera.targetTexture = null;
                Object.Destroy(renderTarget);
            }
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator FrontPerspectiveAimsAtTheStandardSupplyPoint()
        {
            FrameAt(390, 844);
            yield return null;
            yield return null;
            AssertFacing(Vector3.forward);
            Assert.That(camera.orthographic, Is.False);

            var supplyPoint = Vector3.up * (session.BaseTop + .28f);
            var center = camera.WorldToViewportPoint(supplyPoint);
            Assert.That(center.x, Is.EqualTo(.5f).Within(.001f));
            Assert.That(center.y, Is.EqualTo(.5f).Within(.001f));
            var near = camera.WorldToViewportPoint(supplyPoint + Vector3.forward * .4f + Vector3.right * .1f);
            var far = camera.WorldToViewportPoint(supplyPoint + Vector3.back * .4f + Vector3.right * .1f);
            Assert.That(.5f - near.x, Is.GreaterThan(.5f - far.x));
        }

        [UnityTest]
        public IEnumerator TheOriginalWorkingWindowFollowsTheTowerUpAndBackDownAcrossScreenSizes()
        {
            FrameAt(900, 430);
            session.Initialize(new StackableStones(), session.Base, session.Ground);
            yield return null;
            yield return null;

            var roof = Part("Roof");
            var glass = Part("Glass Front");
            var platform = Part("Cabinet");
            var cabinet = root.GetComponent<ArcadeCabinet>();
            var originalPane = glass.bounds;
            var originalRoof = roof.bounds;
            var originalPlatform = platform.bounds;
            var originalBase = session.Base.bounds;
            var stoneSize = session.Pending.Shape.bounds.size;
            var farCorner = new Vector3(8, 0, -8);
            var initialReach = cabinet.LimitTravel(session.Pending, farCorner);
            float supplyDepth = camera.WorldToViewportPoint(Vector3.up * (session.BaseTop + .28f)).z;
            var startPosition = camera.transform.position;
            float landscapeFov = camera.fieldOfView;
            AssertWorkingWindow(roof.bounds, glass.bounds, originalPane.size.y);

            for (int i = 0; i < 4; i++)
            {
                var stone = session.Pending;
                Assert.That(session.Adjust(stone, Vector3.up * (session.TowerTop() + .12f), Quaternion.identity), Is.True);
                Assert.That(session.Release(stone), Is.True);
                yield return new WaitForSeconds(.40f);
            }
            yield return new WaitForSeconds(.65f);

            float rise = session.TowerTop() - session.BaseTop;
            Assert.That(rise, Is.GreaterThan(.7f));
            Assert.That(camera.transform.position.y - startPosition.y, Is.EqualTo(rise).Within(.01f));
            Assert.That(camera.fieldOfView, Is.EqualTo(landscapeFov).Within(.001f));
            Assert.That(camera.WorldToViewportPoint(Vector3.up * (session.TowerTop() + .28f)).z,
                Is.EqualTo(supplyDepth).Within(.01f));
            Assert.That(glass.bounds.size.y - originalPane.size.y, Is.EqualTo(rise).Within(.02f));
            Assert.That(glass.bounds.min.y, Is.EqualTo(originalPane.min.y).Within(.001f));
            Assert.That(glass.bounds.size.x, Is.EqualTo(originalPane.size.x).Within(.001f));
            Assert.That(glass.bounds.size.z, Is.EqualTo(originalPane.size.z).Within(.001f));
            Assert.That(platform.bounds, Is.EqualTo(originalPlatform));
            Assert.That(session.Base.bounds, Is.EqualTo(originalBase));
            Assert.That(Vector3.Distance(session.Pending.Shape.bounds.size, stoneSize), Is.LessThan(.001f));
            AssertWorkingWindow(roof.bounds, glass.bounds, originalPane.size.y);

            FrameAt(390, 844);
            yield return null;
            yield return null;
            var highPosition = camera.transform.position;
            float portraitFov = camera.fieldOfView;
            Assert.That(portraitFov, Is.GreaterThan(landscapeFov));
            Assert.That(cabinet.LimitTravel(session.Pending, farCorner), Is.EqualTo(initialReach));
            AssertWorkingWindow(roof.bounds, glass.bounds, originalPane.size.y);

            foreach (var stone in session.Stones.Where(stone => stone.Released))
                stone.Body.position += Vector3.right * 3;
            for (int frame = 0; frame < 30 && session.TowerTop() >= session.BaseTop + .001f; frame++)
                yield return new WaitForFixedUpdate();
            Assert.That(session.TowerTop(), Is.EqualTo(session.BaseTop).Within(.001f));
            yield return null;
            Assert.That(camera.transform.position.y, Is.InRange(startPosition.y + .05f, highPosition.y));
            yield return new WaitForSeconds(1);

            Assert.That(Vector3.Distance(camera.transform.position, startPosition), Is.LessThan(.005f));
            Assert.That(camera.fieldOfView, Is.EqualTo(portraitFov).Within(.001f));
            Assert.That(roof.bounds.center.y, Is.EqualTo(originalRoof.center.y).Within(.005f));
            Assert.That(glass.bounds.size.y, Is.EqualTo(originalPane.size.y).Within(.005f));
            Assert.That(platform.bounds, Is.EqualTo(originalPlatform));
            Assert.That(session.Base.bounds, Is.EqualTo(originalBase));
            Assert.That(cabinet.LimitTravel(session.Pending, farCorner), Is.EqualTo(initialReach));
            AssertInView(session.Pending.transform.position);
            AssertWorkingWindow(roof.bounds, glass.bounds, originalPane.size.y);
        }

        Renderer Part(string name) =>
            root.GetComponentsInChildren<Renderer>().Single(renderer => renderer.name == name);

        [UnityTest]
        public IEnumerator ArcadeRecordKeepsTheHighestStableHeightUntilRestart()
        {
            session.Initialize(new StackableStones(), session.Base, session.Ground);
            yield return null;
            var stone = session.Pending;
            Assert.That(session.Release(stone), Is.True);
            yield return new WaitForSeconds(.40f);
            Assert.That(session.CurrentHeightMm, Is.GreaterThan(180));
            Assert.That(session.ArcadeHighestStableHeightMm, Is.Zero, "A settling tower is not a record.");
            yield return new WaitForSeconds(1f);

            int record = session.CurrentHeightMm;
            Assert.That(session.ArcadeHighestStableHeightMm, Is.EqualTo(record));
            stone.Body.position += Vector3.right * 3;
            yield return new WaitForSeconds(.20f);
            Assert.That(session.CurrentHeightMm, Is.Zero);
            Assert.That(session.ArcadeHighestStableHeightMm, Is.EqualTo(record));

            session.Restart();
            Assert.That(session.ArcadeHighestStableHeightMm, Is.Zero);
        }

        void AssertWorkingWindow(Bounds roof, Bounds pane, float originalHeight)
        {
            foreach (float x in new[] { roof.min.x, roof.max.x })
                foreach (float z in new[] { roof.min.z, roof.max.z })
                    AssertInView(new Vector3(x, roof.max.y, z));
            foreach (float x in new[] { pane.min.x, pane.max.x })
                AssertInView(new Vector3(x, pane.max.y - originalHeight, pane.center.z));
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
            FrameAt(390, 844);
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
            var buttonHeight = cabinet.ReleaseButton.localPosition.y;
            var release = Point(Control("Release"));
            Touch(1, TouchPhase.Began, release);
            yield return null;
            yield return null;
            Assert.That(cabinet.ReleaseButton.localPosition.y, Is.LessThan(buttonHeight - .006f));
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
            Assert.That(cabinet.ReleaseButton.localPosition.y, Is.EqualTo(buttonHeight));
        }

        [UnityTest]
        public IEnumerator HoldingThePhysicalJoystickGripDoesNotMoveTheStoneUntilDragged()
        {
            FrameAt(430, 900);
            yield return null;
            var cabinet = root.GetComponent<ArcadeCabinet>();
            var stone = session.Pending;
            var start = stone.transform.position;
            var grip = camera.WorldToViewportPoint(
                ArcadeFrontTests.MaterialCenter(cabinet.Joystick.GetComponent<Renderer>(), "Vermilion"));
            var knob = new Vector2(grip.x * Screen.width, grip.y * Screen.height);
            Touch(1, TouchPhase.Began, knob);
            yield return new WaitForSeconds(.24f);
            Assert.That(stone.transform.position, Is.EqualTo(start));
            AssertFacing(Vector3.forward);
            Assert.That(Control(Next).gameObject.activeInHierarchy, Is.False);

            var dragged = knob + Vector2.right * Mathf.Max(35, Screen.width * .025f);
            Touch(1, TouchPhase.Moved, dragged);
            yield return new WaitForSeconds(.18f);
            Assert.That(stone.transform.position.x, Is.LessThan(start.x - .02f));
            Touch(1, TouchPhase.Ended, dragged);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LandscapeSideWindowsShowTheSupportAndGapWithTheConsoleOutsideTheFrame()
        {
            FrameAt(900, 430);
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
        public IEnumerator AutomaticLandscapeAspectKeepsTheSideCropStableAndRestoresTheFirstFrontFrame()
        {
            FrameAt(900, 430);
            yield return null;
            yield return null;
            var reference = new Vector3(.25f, session.BaseTop + .04f, .20f);
            var front = camera.WorldToViewportPoint(reference);

            yield return Tap(Point(Control(Next)));
            AssertFacing(Vector3.left);
            var sideViewport = camera.rect;
            var side = camera.WorldToViewportPoint(reference);
            Assert.That(sideViewport.width, Is.InRange(.37f, .42f));
            Assert.That(sideViewport.center.x, Is.EqualTo(.5f).Within(.001f));
            for (int frame = 0; frame < 8; frame++)
            {
                yield return null;
                Assert.That(camera.rect, Is.EqualTo(sideViewport), $"Side viewport changed on frame {frame}.");
                Assert.That(Vector3.Distance(camera.WorldToViewportPoint(reference), side), Is.LessThan(.0001f));
            }

            var previous = Point(Control(Previous));
            Touch(1, TouchPhase.Began, previous);
            yield return null;
            Touch(1, TouchPhase.Ended, previous);
            yield return null;
            yield return null; // The first front frame is placed in LateUpdate after pointer-up.
            AssertFacing(Vector3.forward);
            Assert.That(camera.rect, Is.EqualTo(new Rect(0, 0, 1, 1)));
            Assert.That(Vector3.Distance(camera.WorldToViewportPoint(reference), front), Is.LessThan(.0001f));
        }

        void FrameAt(int width, int height)
        {
            if (renderTarget != null) Object.Destroy(renderTarget);
            renderTarget = new RenderTexture(width, height, 24);
            camera.targetTexture = renderTarget;
            camera.ResetAspect();
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
