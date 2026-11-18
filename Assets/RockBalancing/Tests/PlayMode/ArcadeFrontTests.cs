using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RockBalancing.Tests
{
    public sealed class ArcadeFrontTests
    {
        const string ScenePath = "Assets/RockBalancing/Scenes/Arcade.unity";
        Touchscreen touchscreen;
        Scene scene;

        [UnityTearDown]
        public IEnumerator UnloadArcade()
        {
            if (touchscreen != null) InputSystem.RemoveDevice(touchscreen);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator CabinetControlsPlaceRotateAndReleaseTheSameStone()
        {
            Assert.That(Application.CanStreamedLevelBeLoaded(ScenePath), Is.True, "The arcade needs a playable scene.");
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByPath(ScenePath);
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            touchscreen.MakeCurrent();
            yield return null;
            yield return null;

            var root = scene.GetRootGameObjects().Single(go => go.GetComponent<GameBootstrap>() != null);
            var session = root.GetComponentInChildren<GameSession>();
            var camera = root.GetComponent<GameInput>().ViewCamera;
            var stone = session.Pending;
            Assert.That(stone, Is.Not.Null);
            var initial = stone.transform.position;
            var initialCameraPosition = camera.transform.position;
            var initialCameraRotation = camera.transform.rotation;
            float initialFov = camera.fieldOfView;
            var stick = ControlPoint(root, "Move");
            var height = initial.y;
            yield return Press(stick, new Vector2(-30, 30), .24f);
            Assert.That(stone.transform.position.x, Is.GreaterThan(initial.x + .02f), "Screen left moves toward the player's left.");
            Assert.That(stone.transform.position.z, Is.LessThan(initial.z - .02f), "Screen up moves away from the player.");
            Assert.That(stone.transform.position.y, Is.EqualTo(height).Within(.001f));

            var planarPosition = stone.transform.position;
            yield return Press(ControlPoint(root, "Lift"), Vector2.zero, .2f);
            Assert.That(stone.transform.position.y, Is.GreaterThan(height + .03f));
            height = stone.transform.position.y;
            yield return Press(ControlPoint(root, "Lower"), Vector2.zero, .15f);
            Assert.That(stone.transform.position.y, Is.LessThan(height - .03f));
            Assert.That(stone.transform.position.x, Is.EqualTo(planarPosition.x).Within(.001f));
            Assert.That(stone.transform.position.z, Is.EqualTo(planarPosition.z).Within(.001f));
            Assert.That(camera.transform.position, Is.EqualTo(initialCameraPosition));
            Assert.That(camera.transform.rotation, Is.EqualTo(initialCameraRotation));
            Assert.That(camera.fieldOfView, Is.EqualTo(initialFov));

            var position = stone.transform.position;
            var rotation = stone.transform.rotation;
            var viewpoint = camera.transform.position;
            yield return Press(camera.WorldToScreenPoint(position), Vector2.right * 44, .03f);
            Assert.That(Quaternion.Angle(rotation, stone.transform.rotation), Is.InRange(1f, 30f));
            Assert.That(stone.transform.position, Is.EqualTo(position));
            Assert.That(camera.transform.position, Is.EqualTo(viewpoint));
            Assert.That(session.Pending, Is.SameAs(stone));
            Assert.That(stone.Released, Is.False);

            yield return Press(ControlPoint(root, "Release"), Vector2.zero, .03f);
            Assert.That(stone.Released, Is.True);
            Assert.That(session.Pending, Is.Not.Null.And.Not.SameAs(stone));
            Assert.That(camera.transform.position, Is.EqualTo(initialCameraPosition));
            yield return new WaitForSeconds(1);
            Assert.That(stone.transform.position.y, Is.LessThan(position.y));
            Assert.That(session.CurrentHeightMm, Is.GreaterThan(0));
        }

        static Vector2 ControlPoint(GameObject root, string name)
        {
            var control = root.GetComponentsInChildren<RectTransform>()
                .Single(rect => rect.name == name);
            return RectTransformUtility.WorldToScreenPoint(null, control.TransformPoint(control.rect.center));
        }

        IEnumerator Press(Vector2 start, Vector2 delta, float seconds)
        {
            Touch(UnityEngine.InputSystem.TouchPhase.Began, start);
            yield return null;
            if (delta != Vector2.zero)
            {
                Touch(UnityEngine.InputSystem.TouchPhase.Moved, start + delta);
                yield return null;
            }
            yield return new WaitForSeconds(seconds);
            Touch(UnityEngine.InputSystem.TouchPhase.Ended, start + delta);
            yield return null;
            yield return null;
        }

        void Touch(UnityEngine.InputSystem.TouchPhase phase, Vector2 position)
        {
            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = 1,
                phase = phase,
                position = position
            });
        }
    }
}
