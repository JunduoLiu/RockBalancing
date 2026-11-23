using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RockBalancing.Tests
{
    public sealed class ArcadeRotationTests
    {
        const string ScenePath = "Assets/RockBalancing/Scenes/Arcade.unity";
        Touchscreen touchscreen;
        Scene scene;
        GameObject root;
        GameInput input;
        GameSession session;
        Stone stone;

        [UnitySetUp]
        public IEnumerator LoadArcade()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByPath(ScenePath);
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            touchscreen.MakeCurrent();
            yield return null;
            yield return null;
            root = scene.GetRootGameObjects().Single(go => go.GetComponent<GameBootstrap>() != null);
            input = root.GetComponent<GameInput>();
            session = input.Session;
            stone = session.Pending;
        }

        [UnityTearDown]
        public IEnumerator UnloadArcade()
        {
            if (touchscreen != null) InputSystem.RemoveDevice(touchscreen);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator SettingsPreservePoseAndRingsLastUntilTheStoneIsLetGo()
        {
            var center = StoneCenter();
            yield return Tap(center, Vector2.right * 27);
            var freePose = stone.transform.rotation;
            ExpectPose(Quaternion.AngleAxis(-12.15f, input.ViewCamera.transform.up));

            yield return SetStepMode();
            ExpectPose(freePose);
            Assert.That(VisibleRingCount(), Is.Zero);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, center);
            Assert.That(VisibleRingCount(), Is.EqualTo(3));
            Assert.That(input.IsOperating, Is.True);
            ExpectPose(freePose);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Ended, center);
            Assert.That(VisibleRingCount(), Is.Zero);
            ExpectPose(freePose);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, center);
            Assert.That(VisibleRingCount(), Is.EqualTo(3));
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Canceled, center);
            Assert.That(VisibleRingCount(), Is.Zero);
            Assert.That(session.Pending, Is.SameAs(stone));
            Assert.That(stone.Released, Is.False);
        }

        [UnityTest]
        public IEnumerator FreeDragAlignsThePendingStoneHighAboveTheBase()
        {
            var position = new Vector3(0, session.BaseTop + .53f, 0);
            var heading = Quaternion.AngleAxis(32, Vector3.up);
            var nearLevel = Quaternion.Euler(1.6f, 0, -1.4f) * heading;
            var drag = Vector2.down * 26;
            var dragTurn = Quaternion.AngleAxis(drag.y * input.RotationSensitivity, input.ViewCamera.transform.right);
            Assert.That(session.Adjust(stone, position, Quaternion.Inverse(dragTurn) * nearLevel), Is.True);
            yield return null;

            var grab = StoneCenter();
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, grab);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, grab + drag);
            Assert.That(Vector3.Angle(stone.transform.up, Vector3.up), Is.LessThan(.1f));
            Assert.That(Vector3.Angle(stone.transform.right, heading * Vector3.right), Is.LessThan(.1f));
            Assert.That(stone.transform.position, Is.EqualTo(position));
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Ended, grab + drag);
            ExpectPose(heading);
        }

        [UnityTest]
        public IEnumerator MatchedFacesStayLitUntilReleaseAndTheStoneFallsNormally()
        {
            var position = new Vector3(0, session.BaseTop + .36f, 0);
            var drag = Vector2.down * 26;
            var dragTurn = Quaternion.AngleAxis(drag.y * input.RotationSensitivity, input.ViewCamera.transform.right);
            Assert.That(session.Adjust(stone, position, Quaternion.Inverse(dragTurn) *
                Quaternion.AngleAxis(2, Vector3.right)), Is.True);
            yield return null;

            var grab = StoneCenter();
            yield return Tap(grab, drag);
            var outline = MatchOutline();
            Assert.That(outline, Is.Not.Null, "The cabinet shows paired outlines after the drag ends.");
            Assert.That(outline.canvasRenderer.GetAlpha(), Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(MatchOutline(), Is.SameAs(outline));
            Assert.That(outline.canvasRenderer.GetAlpha(), Is.EqualTo(1));

            yield return Tap(ControlPoint("Release"));
            Assert.That(MatchOutline(), Is.Null, "The previous pair leaves when the pending stone changes.");
            Assert.That(stone.Released, Is.True);
            Assert.That(stone.Body.isKinematic, Is.False);
            Assert.That(session.Pending, Is.Not.SameAs(stone));
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            Assert.That(stone.Body.position.y, Is.LessThan(position.y - .01f));
            Assert.That(stone.Body.position.y, Is.GreaterThan(session.BaseTop + .1f));
        }

        [UnityTest]
        public IEnumerator FreeDragCanCrossOrReverseOutOfADetentOnASlopedRock()
        {
            UseKnownFlatStones();
            var support = stone;
            var slope = Quaternion.Euler(14, 0, -8);
            float low = support.OwnedMesh.vertices.Min(vertex => (slope * vertex).y);
            var supportPosition = new Vector3(0, session.BaseTop - low + .002f, 0);
            Assert.That(session.Adjust(support, supportPosition, slope), Is.True);
            Assert.That(session.Release(support), Is.True);
            // Preserve a known tilted contact while this test isolates rotation on the next stone.
            support.Body.constraints = RigidbodyConstraints.FreezeAll;
            support.Body.Sleep();
            stone = session.Pending;
            yield return null;

            var position = new Vector3(0, session.BaseTop + .43f, 0);
            var drag = Vector2.down * 26;
            var dragTurn = Quaternion.AngleAxis(drag.y * input.RotationSensitivity, input.ViewCamera.transform.right);
            Assert.That(session.Adjust(stone, position, Quaternion.Inverse(dragTurn) *
                Quaternion.AngleAxis(2, Vector3.right) * slope), Is.True);
            yield return null;

            var grab = StoneCenter();
            var caught = grab + drag;
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, grab);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, caught);
            Assert.That(Vector3.Angle(stone.transform.up, support.transform.up), Is.LessThan(.1f));
            var poseAtFirstCatch = stone.transform.rotation;
            var outline = MatchOutline();
            Assert.That(outline, Is.Not.Null);

            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, caught + Vector2.down * 11);
            Assert.That(Quaternion.Angle(stone.transform.rotation, poseAtFirstCatch), Is.EqualTo(4.95f).Within(.1f));
            yield return new WaitForSecondsRealtime(.05f);
            Assert.That(outline.canvasRenderer.GetAlpha(), Is.LessThan(1));
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, caught + Vector2.down * 4);
            Assert.That(Vector3.Angle(stone.transform.up, support.transform.up), Is.LessThan(.1f));
            var poseAtSecondCatch = stone.transform.rotation;
            Assert.That(outline.canvasRenderer.GetAlpha(), Is.EqualTo(1));

            var reversed = caught + Vector2.up * 7;
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, reversed);
            Assert.That(Quaternion.Angle(stone.transform.rotation, poseAtSecondCatch), Is.EqualTo(4.95f).Within(.1f));
            var departingPose = stone.transform.rotation;
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Ended, reversed);
            yield return new WaitForSecondsRealtime(.3f);
            ExpectPose(departingPose);
            Assert.That(stone.transform.position, Is.EqualTo(position));
            Assert.That(MatchOutline(), Is.Null);
        }

        [UnityTest]
        public IEnumerator FreeDragStopsAgainstTheBaseAndCanReverse()
        {
            UseKnownFlatStones();
            yield return null;
            var drag = Vector2.down * 55;
            var fullTurn = Quaternion.AngleAxis(drag.y * input.RotationSensitivity, input.ViewCamera.transform.right);
            float startBottom = stone.OwnedMesh.vertices.Min(vertex => vertex.y);
            float turnedBottom = stone.OwnedMesh.vertices.Min(vertex => (fullTurn * vertex).y);
            Assert.That(turnedBottom, Is.LessThan(startBottom - .006f));
            var position = new Vector3(0, session.BaseTop - (startBottom + turnedBottom) * .5f, 0);
            Assert.That(session.Adjust(stone, position, Quaternion.identity), Is.True);
            yield return null;

            var grab = StoneCenter();
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, grab);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, grab + drag);
            var stopped = stone.transform.rotation;
            Assert.That(Quaternion.Angle(stopped, Quaternion.identity), Is.InRange(3f, 21f));
            Assert.That(session.Feedback, Is.Not.Empty);

            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, grab + drag + Vector2.up * 10);
            Assert.That(Quaternion.Angle(stone.transform.rotation, Quaternion.identity),
                Is.LessThan(Quaternion.Angle(stopped, Quaternion.identity)));
            Assert.That(stone.transform.position, Is.EqualTo(position));
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Ended, grab + drag + Vector2.up * 10);
        }

        [UnityTest]
        public IEnumerator OneDragCrossesAndReversesMultipleStepsOnEachCabinetAxis()
        {
            UseKnownFlatStones();
            yield return null;
            var center = StoneCenter();
            var diagonalGrab = (Vector2)input.ViewCamera.WorldToScreenPoint(
                stone.transform.position + new Vector3(-.009f, .009f, 0));
            float distance = Mathf.Max(40, .05f * Screen.height);

            foreach (var (mode, step) in new[] { ("15°", 15f), ("30°", 30f) })
            {
                yield return SetStepMode(mode);
                yield return DragAndReverse(center, Vector2.down * distance, Vector3.right, step);
                yield return DragAndReverse(center, Vector2.left * distance, Vector3.up, step);
                yield return DragAndReverse(diagonalGrab, new Vector2(1, -1).normalized * distance, Vector3.forward, step);
            }
            Assert.That(session.Pending, Is.SameAs(stone));
            Assert.That(stone.Released, Is.False);
        }

        IEnumerator DragAndReverse(Vector2 grab, Vector2 increment, Vector3 axis, float step)
        {
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, grab);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, grab + increment);
            ExpectPose(Quaternion.AngleAxis(step, axis));
            // A fast sample may cross several ticks in one frame.
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, grab + increment * 3.5f);
            ExpectPose(Quaternion.AngleAxis(step * 4, axis));
            Assert.That(VisibleRingCount(), Is.EqualTo(3));
            Assert.That(input.IsOperating, Is.True);

            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, grab + increment * 2);
            ExpectPose(Quaternion.AngleAxis(step * 3, axis));
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, grab - increment * .1f);
            ExpectPose(Quaternion.identity);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Ended, grab - increment * .1f);
        }

        [UnityTest]
        public IEnumerator ABlockedStepKeepsTheEntirePreviousPose()
        {
            // Use a repeatable production pebble and a release height where only
            // the last part of the attempted step would cross the base.
            var factory = new StoneFactory(shapes: new StoneShapeSettings { FlatProbability = 1 }, nextSeed: () => 123);
            session.Initialize(factory, session.Base, session.Ground);
            stone = session.Pending;
            yield return null;
            yield return SetStepMode();

            float BottomAt(float degrees) => stone.OwnedMesh.vertices
                .Min(vertex => (Quaternion.AngleAxis(degrees, Vector3.right) * vertex).y);
            float firstSafe = BottomAt(6);
            float lastBlocked = BottomAt(15);
            Assert.That(lastBlocked, Is.LessThan(firstSafe - .002f), "The base must block only part of the rotation.");
            float height = session.BaseTop - (firstSafe + lastBlocked) * .5f;
            var position = new Vector3(0, height, 0);
            Assert.That(session.Adjust(stone, position, Quaternion.identity), Is.True);
            yield return null;

            var center = StoneCenter();
            float distance = Mathf.Max(40, .05f * Screen.height);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, center);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, center + Vector2.down * distance);
            ExpectPose(Quaternion.identity);
            Assert.That(session.Feedback, Is.Not.Empty);
            Assert.That(VisibleRingCount(), Is.EqualTo(3));
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, center + Vector2.left * distance);
            ExpectPose(Quaternion.identity);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Ended, center + Vector2.left * distance);
            Assert.That(stone.transform.position, Is.EqualTo(position));
            Assert.That(session.Pending, Is.SameAs(stone));
        }

        [UnityTest]
        public IEnumerator ALongDragStopsAtAnIntermediateCollisionAndCanReverse()
        {
            var factory = new StoneFactory(shapes: new StoneShapeSettings { FlatProbability = 1 },
                nextSeed: () => 123, arcade: true);
            session.Initialize(factory, session.Base, session.Ground);
            stone = session.Pending;
            yield return null;
            yield return SetStepMode("30°");

            float BottomAt(float degrees) => stone.OwnedMesh.vertices
                .Min(vertex => (Quaternion.AngleAxis(degrees, Vector3.right) * vertex).y);
            float firstTick = BottomAt(30);
            float blockedTick = BottomAt(60);
            float clearance = (firstTick + blockedTick) * .5f;
            Assert.That(firstTick - blockedTick, Is.GreaterThan(.004f));
            Assert.That(BottomAt(180), Is.GreaterThan(clearance + .002f), "The far endpoint must clear the base.");
            var position = new Vector3(0, session.BaseTop - clearance, 0);
            Assert.That(session.Adjust(stone, position, Quaternion.identity), Is.True);
            yield return null;

            var center = StoneCenter();
            var drag = Vector2.down * Mathf.Max(40, .05f * Screen.height);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, center);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, center + drag * 5);
            ExpectPose(Quaternion.AngleAxis(30, Vector3.right));
            Assert.That(session.Feedback, Is.Not.Empty);

            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, center + drag * 5.2f);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, center + drag * 4.35f);
            ExpectPose(Quaternion.identity);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Ended, center + drag * 4.35f);
            Assert.That(stone.transform.position, Is.EqualTo(position));
            Assert.That(session.Pending, Is.SameAs(stone));
        }

        [UnityTest]
        public IEnumerator SwitchingStepSizesAndReturningToFreePreservesThePose()
        {
            yield return SetStepMode();
            var drag = Vector2.down * Mathf.Max(40, .05f * Screen.height);
            yield return Tap(StoneCenter(), drag);
            var stepPose = Quaternion.AngleAxis(15, Vector3.right);
            ExpectPose(stepPose);

            yield return SetStepMode("30°");
            ExpectPose(stepPose);
            yield return Tap(StoneCenter(), drag);
            stepPose = Quaternion.AngleAxis(45, Vector3.right);
            ExpectPose(stepPose);

            yield return SetStepMode("自由");
            ExpectPose(stepPose);
            var center = StoneCenter();
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, center);
            Assert.That(VisibleRingCount(), Is.Zero);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, center + Vector2.right * 27);
            ExpectPose(Quaternion.AngleAxis(-12.15f, input.ViewCamera.transform.up) * stepPose);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Ended, center + Vector2.right * 27);
            Assert.That(session.Pending, Is.SameAs(stone));
        }

        [UnityTest]
        public IEnumerator HoldingAfterAStepLocksTheFrontUntilAFreshBlankDrag()
        {
            yield return SetStepMode();
            var blank = new Vector2(Screen.width * .08f, Screen.height * .55f);
            var side = blank + Vector2.right * (Screen.width * .1875f);
            var front = input.ViewCamera.transform.position;
            var center = StoneCenter();
            var dragged = center + Vector2.down * Mathf.Max(40, .05f * Screen.height);

            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, center);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, dragged);
            var pose = Quaternion.AngleAxis(15, Vector3.right);
            ExpectPose(pose);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, blank, id: 2);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, side, id: 2);
            Assert.That(input.ViewCamera.transform.position, Is.EqualTo(front));
            Assert.That(VisibleRingCount(), Is.EqualTo(3));

            yield return Touch(UnityEngine.InputSystem.TouchPhase.Ended, dragged);
            Assert.That(VisibleRingCount(), Is.Zero);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, side + Vector2.right * 25, id: 2);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Ended, side + Vector2.right * 25, id: 2);
            Assert.That(input.ViewCamera.transform.position, Is.EqualTo(front));

            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, blank);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, side);
            Assert.That(input.ViewCamera.transform.position.x, Is.GreaterThan(1f));
            center = StoneCenter();
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, center, id: 2);
            Assert.That(VisibleRingCount(), Is.Zero);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, center + Vector2.right * 44, id: 2);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Ended, center + Vector2.right * 44, id: 2);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Ended, side);
            ExpectPose(pose);
            Assert.That(session.Pending, Is.SameAs(stone));
        }

        IEnumerator SetStepMode(string mode = "15°")
        {
            yield return Tap(ControlPoint("设置"));
            yield return Tap(ControlPoint(mode));
            yield return Tap(ControlPoint("完成"));
        }

        void UseKnownFlatStones()
        {
            var factory = new StoneFactory(root.GetComponent<GameBootstrap>().StoneMaterial,
                shapes: new StoneShapeSettings { FlatProbability = 1 }, nextSeed: () => 123, arcade: true);
            session.Initialize(factory, session.Base, session.Ground);
            stone = session.Pending;
        }

        int VisibleRingCount() => root.GetComponentsInChildren<Graphic>()
            .Count(graphic => graphic.name == "X ring" || graphic.name == "Y ring" || graphic.name == "Z ring");

        Graphic MatchOutline() => root.GetComponentsInChildren<Graphic>()
            .SingleOrDefault(graphic => graphic.name == "Matched faces");

        void ExpectPose(Quaternion pose) =>
            Assert.That(Quaternion.Angle(stone.transform.rotation, pose), Is.LessThan(.1f));

        Vector2 StoneCenter() => input.ViewCamera.WorldToScreenPoint(stone.transform.position);

        RectTransform Control(string name) =>
            (RectTransform)root.GetComponentsInChildren<Selectable>().Single(control => control.name == name).transform;

        Vector2 ControlPoint(string name)
        {
            var control = Control(name);
            return RectTransformUtility.WorldToScreenPoint(null, control.TransformPoint(control.rect.center));
        }

        IEnumerator Tap(Vector2 point, Vector2 delta = default)
        {
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, point);
            if (delta != Vector2.zero) yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, point + delta);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Ended, point + delta);
        }

        IEnumerator Touch(UnityEngine.InputSystem.TouchPhase phase, Vector2 position, int id = 1)
        {
            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = id,
                phase = phase,
                position = position
            });
            yield return null;
            yield return null;
        }
    }
}
