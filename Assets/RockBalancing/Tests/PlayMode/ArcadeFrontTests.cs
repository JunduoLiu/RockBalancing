using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
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
            var cabinet = root.GetComponent<ArcadeCabinet>();
            var stick = ControlPoint(cabinet.Joystick, "Vermilion");
            var height = initial.y;
            yield return Press(stick, new Vector2(-30, 30), .24f);
            Assert.That(stone.transform.position.x, Is.GreaterThan(initial.x + .02f), "Screen left moves toward the player's left.");
            Assert.That(stone.transform.position.z, Is.LessThan(initial.z - .02f), "Screen up moves away from the player.");
            Assert.That(stone.transform.position.y, Is.EqualTo(height).Within(.001f));

            var planarPosition = stone.transform.position;
            yield return Press(ControlPoint(cabinet.LiftButton, "Mint"), Vector2.zero, .2f);
            Assert.That(stone.transform.position.y, Is.GreaterThan(height + .03f));
            height = stone.transform.position.y;
            yield return Press(ControlPoint(cabinet.LowerButton, "Mint"), Vector2.zero, .15f);
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

            yield return Press(ControlPoint(cabinet.ReleaseButton, "Vermilion"), Vector2.zero, .03f);
            Assert.That(stone.Released, Is.True);
            Assert.That(session.Pending, Is.Not.Null.And.Not.SameAs(stone));
            Assert.That(camera.transform.position, Is.EqualTo(initialCameraPosition));
            yield return new WaitForSeconds(1);
            Assert.That(stone.transform.position.y, Is.LessThan(position.y));
            Assert.That(session.CurrentHeightMm, Is.GreaterThan(0));

            Vector2 ControlPoint(Transform part, string material) =>
                camera.WorldToScreenPoint(MaterialCenter(part.GetComponent<Renderer>(), material));
        }

        [UnityTest]
        public IEnumerator ConsoleFloatsClearOfCabinetFront()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByPath(ScenePath);
            yield return null;
            var root = scene.GetRootGameObjects().Single(go => go.GetComponent<GameBootstrap>() != null);
            var cabinet = root.GetComponent<ArcadeCabinet>();
            var chassis = cabinet.GetComponentsInChildren<Renderer>().Single(part => part.name == "Cabinet");
            var deck = cabinet.GetComponentsInChildren<Renderer>().Single(part => part.name == "Console");
            var stick = cabinet.Joystick.GetComponent<Renderer>();
            Assert.That(deck.bounds.min.z, Is.GreaterThan(chassis.bounds.max.z + .08f),
                "The cabinet and controls must have a visible air gap from the side.");
            var surface = deck.gameObject.AddComponent<MeshCollider>();
            var from = new Vector3(.24f, deck.bounds.max.y + .1f, deck.bounds.center.z);
            Assert.That(surface.Raycast(new Ray(from, Vector3.down), out var top, .5f), Is.True);
            Assert.That(top.normal.z, Is.GreaterThan(.15f), "The near edge slopes down toward the player.");
            var knob = MaterialCenter(stick, "Vermilion");
            Assert.That(Vector3.Angle(knob - stick.transform.position, top.normal), Is.LessThan(2f),
                "The neutral joystick must project normally from the visible control surface.");
        }

        [UnityTest]
        public IEnumerator VisibleControlsKeepTheirTouchTargetsWhenTheScreenTurns()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByPath(ScenePath);
            yield return null;
            var root = scene.GetRootGameObjects().Single(go => go.GetComponent<GameBootstrap>() != null);
            var cabinet = root.GetComponent<ArcadeCabinet>();
            var camera = root.GetComponent<GameInput>().ViewCamera;
            Vector3 landscape = default;
            foreach (var size in new[] { new Vector2Int(900, 430), new Vector2Int(390, 844) })
            {
                var target = new RenderTexture(size.x, size.y, 24);
                camera.targetTexture = target;
                try
                {
                    yield return null;
                    yield return null;
                    var position = cabinet.Joystick.position;
                    if (size.x > size.y) landscape = position;
                    else Assert.That(Vector3.Distance(position, landscape), Is.GreaterThan(.05f),
                        "The world-space console adjusts to the change in frame shape.");
                    AssertVisibleTarget(cabinet.Joystick, "Vermilion", "Move");
                    AssertVisibleTarget(cabinet.LiftButton, "Mint", "Lift");
                    AssertVisibleTarget(cabinet.LowerButton, "Mint", "Lower");
                    AssertVisibleTarget(cabinet.ReleaseButton, "Vermilion", "Release");
                }
                finally
                {
                    camera.targetTexture = null;
                    Object.Destroy(target);
                }
            }

            void AssertVisibleTarget(Transform control, string material, string action)
            {
                var point = camera.WorldToViewportPoint(MaterialCenter(control.GetComponent<Renderer>(), material));
                Assert.That(point.x, Is.InRange(.02f, .98f));
                Assert.That(point.y, Is.InRange(.02f, .98f));
                var events = EventSystem.current;
                var pointer = new PointerEventData(events)
                {
                    position = new Vector2(point.x * Screen.width, point.y * Screen.height)
                };
                var hits = new List<RaycastResult>();
                events.RaycastAll(pointer, hits);
                Assert.That(hits.FirstOrDefault().gameObject?.name, Is.EqualTo(action));
            }
        }

        internal static Vector3 MaterialCenter(Renderer renderer, string materialName)
        {
            int index = System.Array.FindIndex(renderer.sharedMaterials, material => material.name == materialName);
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            return renderer.transform.TransformPoint(mesh.GetSubMesh(index).bounds.center);
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
