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
            yield return Tap(ControlPoint("设置"));
            var toggle = Control("刻度旋转").GetComponent<Toggle>();
            Assert.That(toggle.isOn, Is.False, "Free rotation is the initial setting.");
            yield return Tap(ControlPoint("完成"));

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
        public IEnumerator OneDragCrossesAndReversesMultipleStepsOnEachCabinetAxis()
        {
            yield return SetStepMode();
            var center = StoneCenter();
            var diagonalGrab = (Vector2)input.ViewCamera.WorldToScreenPoint(
                stone.transform.position + new Vector3(-.016f, .017f, 0));
            float distance = Mathf.Max(40, .05f * Screen.height);

            foreach (float step in new[] { 15f, 30f })
            {
                input.RotationStepDegrees = step;
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
        public IEnumerator DeveloperStepSizeAppliesAndReturningToFreePreservesThePose()
        {
            input.RotationStepDegrees = 30;
            yield return SetStepMode();
            yield return Tap(StoneCenter(), Vector2.down * Mathf.Max(40, .05f * Screen.height));
            var stepPose = Quaternion.AngleAxis(30, Vector3.right);
            ExpectPose(stepPose);

            yield return SetStepMode();
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
        public IEnumerator HoldingAfterAStepLocksTheFrontUntilAFreshArrowPress()
        {
            yield return SetStepMode();
            var next = Control("Next cabinet face");
            var previous = Control("Previous cabinet face");
            var rightEdge = ControlPoint("Next cabinet face");
            var leftEdge = ControlPoint("Previous cabinet face");
            var front = input.ViewCamera.transform.position;
            var center = StoneCenter();
            var dragged = center + Vector2.down * Mathf.Max(40, .05f * Screen.height);

            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, center);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, dragged);
            var pose = Quaternion.AngleAxis(15, Vector3.right);
            ExpectPose(pose);
            Assert.That(next.gameObject.activeInHierarchy || previous.gameObject.activeInHierarchy, Is.False);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, leftEdge, id: 2);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Ended, leftEdge, id: 2);
            Assert.That(input.ViewCamera.transform.position, Is.EqualTo(front));
            Assert.That(VisibleRingCount(), Is.EqualTo(3));

            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, rightEdge, id: 3);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Ended, dragged);
            Assert.That(next.gameObject.activeInHierarchy && previous.gameObject.activeInHierarchy, Is.True);
            Assert.That(VisibleRingCount(), Is.Zero);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Ended, rightEdge, id: 3);
            Assert.That(input.ViewCamera.transform.position, Is.EqualTo(front));

            yield return Tap(rightEdge);
            Assert.That(input.ViewCamera.transform.position.x, Is.LessThan(-1f));
            center = StoneCenter();
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, center);
            Assert.That(VisibleRingCount(), Is.Zero);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, center + Vector2.right * 44);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Ended, center + Vector2.right * 44);
            ExpectPose(pose);
            Assert.That(session.Pending, Is.SameAs(stone));
        }

        IEnumerator SetStepMode()
        {
            yield return Tap(ControlPoint("设置"));
            yield return Tap(ControlPoint("刻度旋转"));
            yield return Tap(ControlPoint("完成"));
        }

        int VisibleRingCount() => root.GetComponentsInChildren<Graphic>()
            .Count(graphic => graphic.name == "X ring" || graphic.name == "Y ring" || graphic.name == "Z ring");

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
