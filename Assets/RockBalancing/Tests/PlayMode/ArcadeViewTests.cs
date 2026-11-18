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

            var diagonal = ScreenPoint(.36125f, .55f);
            yield return BeginOrbit(ScreenPoint(.08f, .55f), diagonal);
            AssertFacing(new Vector3(1, 0, -1).normalized);
            Assert.That(camera.WorldToViewportPoint(Vector3.up * (session.TowerTop() + .28f)).z,
                Is.EqualTo(supplyDepth).Within(.01f));
            AssertWorkingWindow(roof.bounds, glass.bounds, originalPane.size.y);
            Touch(1, TouchPhase.Ended, diagonal);
            yield return new WaitForSecondsRealtime(.55f);
            AssertFacing(Vector3.forward);

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
        public IEnumerator ABlankDragCirclesTheCabinetAtContinuousElevationWithNoFaceArrows()
        {
            var stone = session.Pending;
            var position = stone.transform.position;
            var rotation = stone.transform.rotation;
            var supply = Vector3.up * (session.BaseTop + .28f);
            float distance = Vector3.Distance(camera.transform.position, supply);
            float fieldOfView = camera.fieldOfView;
            var blank = ScreenPoint(.05f, .55f);
            yield return Tap(blank);
            AssertFacing(Vector3.forward);
            yield return Tap(ScreenPoint(.95f, .55f));
            AssertFacing(Vector3.forward);

            Touch(1, TouchPhase.Began, blank);
            yield return null;
            yield return null;
            var point = blank + Vector2.up * (Screen.height * .12f);
            Touch(1, TouchPhase.Moved, point);
            yield return null;
            yield return null;
            AssertFacing(Vector3.forward);
            Assert.That(Elevation(supply), Is.EqualTo(15f).Within(.1f));
            var pitches = new[] { 14.12f, 12f, 9.88f, 9f, 9.88f, 12f, 14.12f, 15f };
            for (int i = 0; i < pitches.Length; i++)
            {
                Touch(1, TouchPhase.Moved, point += Vector2.right * (Screen.width * .09375f));
                yield return null;
                yield return null;
                var face = Quaternion.AngleAxis(45 * (i + 1), Vector3.up) * Vector3.forward;
                AssertFacing(face);
                Assert.That(Elevation(supply), Is.EqualTo(pitches[i]).Within(.15f));
                Assert.That(Vector3.Distance(camera.transform.position, supply), Is.EqualTo(distance).Within(.002f));
                Assert.That(camera.fieldOfView, Is.EqualTo(fieldOfView).Within(.001f));
                Assert.That(camera.orthographic, Is.False);
            }
            Touch(1, TouchPhase.Ended, point);
            yield return null;
            Assert.That(root.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == Next || rect.name == Previous),
                Is.False);
            Assert.That(session.Pending, Is.SameAs(stone));
            Assert.That(stone.transform.position, Is.EqualTo(position));
            Assert.That(stone.transform.rotation, Is.EqualTo(rotation));
        }

        float Elevation(Vector3 at) => Vector3.Angle(camera.transform.position - at,
            Vector3.ProjectOnPlane(camera.transform.position - at, Vector3.up));

        static Vector2 ScreenPoint(float x, float y) => new Vector2(Screen.width * x, Screen.height * y);

        [UnityTest]
        public IEnumerator ASecondFingerCannotOrbitWhileAFrontAdjustmentIsHeld()
        {
            var stone = session.Pending;
            var blank = ScreenPoint(.08f, .55f);
            var moved = ScreenPoint(.26f, .55f);
            foreach (var control in new[] { "Move", "Lift", "Lower", "Stone" })
            {
                Vector2 point = control == "Stone" ? camera.WorldToScreenPoint(stone.transform.position) : Point(Control(control));
                Touch(1, TouchPhase.Began, point);
                yield return null;
                yield return null;
                Touch(2, TouchPhase.Began, blank);
                yield return null;
                Touch(2, TouchPhase.Moved, moved);
                yield return null;
                yield return null;
                AssertFacing(Vector3.forward);

                Touch(1, TouchPhase.Ended, point);
                yield return null;
                Touch(2, TouchPhase.Moved, moved + Vector2.right * 30);
                yield return null;
                yield return null;
                AssertFacing(Vector3.forward);
                Touch(2, TouchPhase.Ended, moved + Vector2.right * 30);
                yield return null;
                yield return null;
                Assert.That(session.Pending, Is.SameAs(stone));
            }

            yield return BeginOrbit(blank, moved);
            Assert.That(Azimuth(), Is.GreaterThan(80f));
            Touch(1, TouchPhase.Ended, moved);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OrbitAndReturnDiscardConsolePressesUntilAFreshFrontPress()
        {
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            var stone = session.Pending;
            var blank = ScreenPoint(.08f, .60f);
            var side = ScreenPoint(.2675f, .60f);
            foreach (var name in new[] { "Move", "Lift", "Lower", "Release" })
            {
                foreach (bool pressWhileReturning in new[] { false, true })
                {
                    var control = Point(Control(name));
                    var position = stone.transform.position;
                    var rotation = stone.transform.rotation;
                    var offset = name == "Move" ? Vector2.right * 30 : Vector2.zero;
                    yield return BeginOrbit(blank, side);
                    AssertFacing(Vector3.right);

                    if (pressWhileReturning)
                    {
                        Touch(1, TouchPhase.Ended, side);
                        yield return new WaitForSecondsRealtime(.10f);
                        Assert.That(Azimuth(), Is.InRange(10f, 89f));
                    }

                    Touch(2, TouchPhase.Began, control);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.UpArrow));
                    yield return null;
                    if (offset != Vector2.zero)
                    {
                        Touch(2, TouchPhase.Moved, control + offset);
                        yield return null;
                    }
                    if (!pressWhileReturning)
                    {
                        AssertFacing(Vector3.right);
                        Touch(1, TouchPhase.Ended, side);
                    }

                    yield return new WaitForSecondsRealtime(.65f);
                    AssertFacing(Vector3.forward);
                    Assert.That(stone.transform.position, Is.EqualTo(position));
                    Assert.That(stone.transform.rotation, Is.EqualTo(rotation));
                    Assert.That(stone.Released, Is.False);

                    Touch(2, TouchPhase.Ended, control + offset);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                    yield return null;
                    Assert.That(stone.Released, Is.False);
                }

                var before = stone.transform.position;
                var point = Point(Control(name));
                yield return Drag(point, name == "Move" ? Vector2.right * 32 : Vector2.zero);
                if (name == "Move") Assert.That(stone.transform.position.x, Is.LessThan(before.x - .01f));
                if (name == "Lift") Assert.That(stone.transform.position.y, Is.GreaterThan(before.y + .02f));
                if (name == "Lower") Assert.That(stone.transform.position.y, Is.LessThan(before.y - .02f));
                if (name == "Release") Assert.That(stone.Released, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator BothDirectionsReturnByTheShorterArcAndAllowARegrab()
        {
            var front = camera.transform.position;
            foreach (int direction in new[] { 1, -1 })
            {
                var blank = ScreenPoint(direction > 0 ? .08f : .92f, .60f);
                var beyondHalf = blank + Vector2.right * (direction * .44f * Screen.width);
                yield return BeginOrbit(blank, beyondHalf);
                float turn = Azimuth();
                Assert.That(direction * turn, Is.InRange(-155f, -140f));

                Touch(1, TouchPhase.Ended, beyondHalf);
                yield return new WaitForSecondsRealtime(.11f);
                float returning = Azimuth();
                Assert.That(direction * returning, Is.InRange(direction * turn + 4f, -25f));

                Touch(2, TouchPhase.Began, blank);
                yield return null;
                yield return null;
                float grabbed = Azimuth();
                Assert.That(direction * grabbed, Is.InRange(direction * returning - .5f, -12f));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(returning, grabbed)), Is.LessThan(30f));
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(Azimuth(), Is.EqualTo(grabbed).Within(.1f));

                var draggedAgain = blank + Vector2.right * (direction * .075f * Screen.width);
                Touch(2, TouchPhase.Moved, draggedAgain);
                yield return null;
                yield return null;
                Assert.That(Mathf.DeltaAngle(grabbed, Azimuth()), Is.EqualTo(direction * 36f).Within(.2f));
                Touch(2, TouchPhase.Ended, draggedAgain);
                yield return new WaitForSecondsRealtime(.65f);

                AssertFacing(Vector3.forward);
                Assert.That(Vector3.Distance(camera.transform.position, front), Is.LessThan(.001f));
            }
        }

        [UnityTest]
        public IEnumerator ThePhoneAndLandscapeViewsKeepTheWorkingWindowAndConsoleInTheSamePlace()
        {
            session.Initialize(new StackableStones(), session.Base, session.Ground);
            var roof = Part("Roof");
            var pane = Part("Glass Front");
            var console = Part("Console");
            float originalHeight = pane.bounds.size.y;
            var blank = ScreenPoint(.08f, .55f);
            var sides = new[] { Vector3.right, Vector3.back, Vector3.left, Vector3.forward };

            foreach (var size in new[] { new Vector2Int(390, 844), new Vector2Int(900, 430) })
            {
                FrameAt(size.x, size.y);
                yield return null;
                yield return null;
                var deckPosition = console.bounds.center;
                var top = Vector3.up * session.TowerTop();
                var stone = session.Pending.Shape.bounds;
                var lowerEdge = stone.center + Vector3.down * stone.extents.y;
                var front = camera.WorldToViewportPoint(top);
                float fieldOfView = camera.fieldOfView;
                var point = blank;
                Touch(1, TouchPhase.Began, point);
                yield return null;

                foreach (var outside in sides)
                {
                    point += Vector2.right * (Screen.width * .1875f);
                    Touch(1, TouchPhase.Moved, point);
                    yield return null;
                    yield return null;
                    AssertFacing(outside);
                    Assert.That(camera.rect, Is.EqualTo(new Rect(0, 0, 1, 1)));
                    Assert.That(camera.fieldOfView, Is.EqualTo(fieldOfView).Within(.001f));
                    Assert.That(console.bounds.center, Is.EqualTo(deckPosition));
                    AssertInView(deckPosition);
                    AssertInView(top);
                    AssertInView(lowerEdge);
                    float gap = camera.WorldToViewportPoint(lowerEdge).y - camera.WorldToViewportPoint(top).y;
                    Assert.That(gap * size.y, Is.GreaterThan(12f));
                    AssertWorkingWindow(roof.bounds, pane.bounds, originalHeight);
                }

                Touch(1, TouchPhase.Ended, point);
                yield return new WaitForSecondsRealtime(.55f);
                AssertFacing(Vector3.forward);
                Assert.That(Vector3.Distance(camera.WorldToViewportPoint(top), front), Is.LessThan(.001f));
            }
        }

        [UnityTest]
        public IEnumerator AFocusLossOrCanceledTouchReturnsTheCabinetAndRequiresAFreshPress()
        {
            var input = root.GetComponent<GameInput>();
            var blank = ScreenPoint(.08f, .55f);
            var side = ScreenPoint(.2675f, .55f);
            yield return BeginOrbit(blank, side);
            input.SetBrowserInputFocus("0");
            yield return new WaitForSecondsRealtime(.6f);
            AssertFacing(Vector3.forward);

            input.SetBrowserInputFocus("1");
            Touch(1, TouchPhase.Moved, side + Vector2.right * 30);
            yield return null;
            yield return null;
            AssertFacing(Vector3.forward);
            Touch(1, TouchPhase.Ended, side + Vector2.right * 30);
            yield return null;
            yield return null;

            yield return BeginOrbit(blank, side);
            AssertFacing(Vector3.right);
            Touch(1, TouchPhase.Canceled, side);
            yield return new WaitForSecondsRealtime(.6f);
            AssertFacing(Vector3.forward);
        }

        float Azimuth() => Vector3.SignedAngle(Vector3.forward,
            Vector3.ProjectOnPlane(camera.transform.position, Vector3.up), Vector3.up);

        IEnumerator BeginOrbit(Vector2 start, Vector2 end)
        {
            Touch(1, TouchPhase.Began, start);
            yield return null;
            Touch(1, TouchPhase.Moved, end);
            yield return null;
            yield return null;
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
            var releaseHeight = stone.transform.position.y;
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

            var side = ScreenPoint(.2675f, .55f);
            yield return BeginOrbit(ScreenPoint(.08f, .55f), side);
            AssertFacing(Vector3.right);
            Assert.That(stone.transform.position.y, Is.LessThan(releaseHeight));
            AssertInView(stone.transform.position);
            Assert.That(Quaternion.Angle(closed, jaw.transform.localRotation), Is.GreaterThan(15));
            Assert.That(head.bounds.center.x, Is.GreaterThan(.10f), "The claw has to let go before returning to supply.");

            yield return new WaitForSeconds(.38f);
            Assert.That(head.bounds.center.x, Is.EqualTo(next.transform.position.x).Within(.005f));
            Assert.That(jaw.bounds.min.y, Is.LessThan(next.HighestPoint()));
            Assert.That(cabinet.ReleaseButton.localPosition.y, Is.EqualTo(buttonHeight));
            AssertFacing(Vector3.right);
            Touch(1, TouchPhase.Ended, side);
            yield return null;
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

            var dragged = knob + Vector2.right * Mathf.Max(35, Screen.width * .025f);
            Touch(1, TouchPhase.Moved, dragged);
            yield return new WaitForSeconds(.18f);
            Assert.That(stone.transform.position.x, Is.LessThan(start.x - .02f));
            Touch(1, TouchPhase.Ended, dragged);
            yield return null;
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
