using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        public IEnumerator ParallelTowerTopFacesShareOneDetentAndBothOutlines()
        {
            UseKnownFlatStones();
            var slope = Quaternion.AngleAxis(16, Vector3.right);
            var left = PlaceSupport(-.12f, slope);
            var right = PlaceSupport(.12f, slope);
            var initial = Quaternion.AngleAxis(2, Vector3.right);
            var position = new Vector3(0, session.BaseTop + .39f, 0);
            Assert.That(session.Adjust(stone, position, initial), Is.True);
            yield return null;

            var grab = StoneCenter();
            var caught = grab + Vector2.down * 26;
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, grab);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, caught);
            ExpectPose(slope);
            ExpectSupportOutline(left, true);
            ExpectSupportOutline(right, true);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Ended, caught);
            yield return MovePendingAcross(-.18f);
            ExpectPose(slope);
            ExpectSupportOutline(left, true);
            ExpectSupportOutline(right, false);
            yield return MovePendingAcross(.18f);
            ExpectPose(slope);
            ExpectSupportOutline(left, false);
            ExpectSupportOutline(right, true);
            yield return MovePendingAcross(0);
            ExpectSupportOutline(left, true);
            ExpectSupportOutline(right, true);

            grab = StoneCenter();
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, grab);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, grab + Vector2.down * 12);
            ExpectPose(Quaternion.AngleAxis(21.4f, Vector3.right));
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Ended, grab + Vector2.down * 12);
        }

        [UnityTest]
        public IEnumerator AContinuousDragVisitsCloseTowerTopAnglesInBothDirections() =>
            VisitCloseTowerTopAngles(18f, samples: 5);

        [UnityTest]
        public IEnumerator AOnePixelDragCatchesADetentItPassesBetweenSamples() =>
            VisitCloseTowerTopAngles(16.22f, samples: 1);

        IEnumerator VisitCloseTowerTopAngles(float secondAngle, int samples)
        {
            UseKnownFlatStones();
            var firstSlope = Quaternion.AngleAxis(16, Vector3.right);
            var secondSlope = Quaternion.AngleAxis(secondAngle, Vector3.right);
            var left = PlaceSupport(-.12f, firstSlope);
            var right = PlaceSupport(.12f, secondSlope);
            Assert.That(session.Adjust(stone, new Vector3(0, session.BaseTop + .39f, 0),
                Quaternion.AngleAxis(2, Vector3.right)), Is.True);
            yield return null;

            var grab = StoneCenter();
            var cursor = grab + Vector2.down * 26;
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, grab);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, cursor);
            ExpectPose(firstSlope);
            ExpectSupportOutline(left, true);
            ExpectSupportOutline(right, false);

            for (int i = 0; i < samples; i++)
            {
                cursor += Vector2.down;
                yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, cursor);
            }
            ExpectPose(secondSlope);
            ExpectSupportOutline(left, false);
            ExpectSupportOutline(right, true);

            for (int i = 0; i < samples; i++)
            {
                cursor += Vector2.up;
                yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, cursor);
            }
            ExpectPose(firstSlope);
            ExpectSupportOutline(left, true);
            ExpectSupportOutline(right, false);

            cursor += Vector2.up * 12;
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, cursor);
            Assert.That(Vector3.Angle(stone.transform.up, Vector3.up), Is.LessThan(12f));
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Ended, cursor);
        }

        [UnityTest]
        public IEnumerator ASlowFreeDragEntersASlopedDetent()
        {
            UseKnownFlatStones();
            var slope = Quaternion.AngleAxis(16, Vector3.right);
            var support = PlaceSupport(-.06f, slope);
            Assert.That(session.Adjust(stone, new Vector3(0, session.BaseTop + .39f, 0),
                Quaternion.AngleAxis(30.9f, Vector3.right)), Is.True);
            yield return null;

            var grab = StoneCenter();
            var approach = grab + Vector2.up * 26;
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, grab);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, approach);
            ExpectPose(Quaternion.AngleAxis(19.2f, Vector3.right));
            for (int i = 1; i <= 30; i++)
                yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, approach + Vector2.up * (i / 30f));

            ExpectPose(slope);
            ExpectSupportOutline(support, true);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Ended, approach + Vector2.up);
        }

        [UnityTest]
        public IEnumerator ASupportBelowOnlyAnAdjacentFaceDoesNotLightTheBottomFace()
        {
            UseBoxStones(new Vector3(.18f, .025f, .1f), new Vector3(.20f, .04f, .10f));
            var slope = Quaternion.AngleAxis(60, Vector3.right);
            var supportRotation = slope * Quaternion.AngleAxis(20, Vector3.up);
            float top = stone.OwnedMesh.bounds.max.y;
            float topOffset = stone.OwnedMesh.vertices.Where(vertex => vertex.y == top)
                .Min(vertex => (supportRotation * vertex).z);
            // The tilted pending bottom ends at Z=.0077. Its adjacent face extends
            // to Z=.0423, overlapping a sliver of the support top beginning at .02.
            PlaceSupport(0, supportRotation, z: .02f - topOffset);
            var position = new Vector3(0, session.BaseTop + .39f, 0);
            Assert.That(session.Adjust(stone, position, Quaternion.AngleAxis(46.3f, Vector3.right)), Is.True);
            yield return null;

            yield return Tap(StoneCenter(), Vector2.down * 26);

            ExpectPose(Quaternion.AngleAxis(58, Vector3.right));
            Assert.That(MatchOutline(), Is.Null);
        }

        [UnityTest]
        public IEnumerator ABlockedCorrectionStillAllowsTheNextSupportAngle()
        {
            var size = new Vector3(.20f, .04f, .10f);
            UseBoxStones(size, size, size);
            var clearSlope = Quaternion.AngleAxis(16, Vector3.right);
            var blockedSlope = Quaternion.AngleAxis(19, Vector3.right);
            var clear = PlaceSupport(-.12f, clearSlope);
            var blocked = PlaceSupport(.12f, blockedSlope);
            var position = new Vector3(0, session.BaseTop + .39f, 0);
            Assert.That(session.Adjust(stone, position, Quaternion.AngleAxis(6.3f, Vector3.right)), Is.True);

            float BottomAt(float degrees) => stone.OwnedMesh.vertices
                .Min(vertex => (Quaternion.AngleAxis(degrees, Vector3.right) * vertex).y);
            float obstacleTop = position.y + (BottomAt(18) + BottomAt(19)) * .5f;
            var obstacle = new GameObject("Blocks the steeper correction", typeof(BoxCollider));
            obstacle.transform.SetParent(root.transform);
            obstacle.transform.position = new Vector3(0, obstacleTop - .01f, .044f);
            obstacle.GetComponent<BoxCollider>().size = new Vector3(.03f, .02f, .03f);
            yield return null;

            yield return Tap(StoneCenter(), Vector2.down * 26);

            ExpectPose(clearSlope);
            ExpectSupportOutline(clear, true);
            ExpectSupportOutline(blocked, false);
        }

        [UnityTest]
        public IEnumerator AContinuousDragVisitsSeparateTowerTopAnglesAndCanReverse()
        {
            UseKnownFlatStones();
            var firstSlope = Quaternion.AngleAxis(16, Vector3.right);
            var secondSlope = Quaternion.AngleAxis(32, Vector3.right);
            var left = PlaceSupport(-.12f, firstSlope);
            var right = PlaceSupport(.12f, secondSlope);
            var position = new Vector3(0, session.BaseTop + .39f, 0);
            Assert.That(session.Adjust(stone, position, Quaternion.AngleAxis(2, Vector3.right)), Is.True);
            yield return null;

            var grab = StoneCenter();
            var firstCatch = grab + Vector2.down * 26;
            var secondCatch = firstCatch + Vector2.down * 34;
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Began, grab);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, firstCatch);
            ExpectPose(firstSlope);
            ExpectSupportOutline(left, true);
            ExpectSupportOutline(right, false);

            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, firstCatch + Vector2.down * 12);
            ExpectPose(Quaternion.AngleAxis(21.4f, Vector3.right));
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, secondCatch);
            ExpectPose(secondSlope);
            ExpectSupportOutline(left, false);
            ExpectSupportOutline(right, true);

            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, secondCatch + Vector2.up * 12);
            ExpectPose(Quaternion.AngleAxis(26.6f, Vector3.right));
            var reverseCatch = secondCatch + Vector2.up * 32;
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Moved, reverseCatch);
            ExpectPose(firstSlope);
            yield return Touch(UnityEngine.InputSystem.TouchPhase.Ended, reverseCatch);
            yield return new WaitForSecondsRealtime(.3f);
            ExpectPose(firstSlope);
            ExpectSupportOutline(left, true);
            ExpectSupportOutline(right, false);
            Assert.That(stone.transform.position, Is.EqualTo(position));

            yield return MovePendingAcross(.18f);
            yield return new WaitForSecondsRealtime(.3f);
            ExpectPose(firstSlope);
            Assert.That(MatchOutline(), Is.Null);
            yield return Tap(StoneCenter(), Vector2.up * 26);
            ExpectPose(Quaternion.AngleAxis(4.3f, Vector3.right));
            yield return Tap(StoneCenter(), Vector2.down * 22);
            ExpectPose(Quaternion.AngleAxis(14.2f, Vector3.right));
            Assert.That(MatchOutline(), Is.Null);
            yield return Tap(StoneCenter(), Vector2.down * 39);
            ExpectPose(secondSlope);
            ExpectSupportOutline(left, false);
            ExpectSupportOutline(right, true);

            yield return Tap(ControlPoint("Release"));
            Assert.That(MatchOutline(), Is.Null);
            Assert.That(stone.Released, Is.True);
            Assert.That(session.Pending, Is.Not.SameAs(stone));
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            Assert.That(stone.Body.position.y, Is.LessThan(position.y - .01f));
        }

        IEnumerator MovePendingAcross(float target)
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            try
            {
                float direction = Mathf.Sign(target - stone.transform.position.x);
                // The cabinet faces world -Z, so A moves east and D moves west.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(direction > 0 ? Key.A : Key.D));
                float deadline = Time.realtimeSinceStartup + 4;
                while (direction * (target - stone.transform.position.x) > 0 && Time.realtimeSinceStartup < deadline)
                    yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                yield return null;
                Assert.That(stone.transform.position.x, Is.EqualTo(target).Within(.04f));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest]
        public IEnumerator APartlyCoveredSupportStillCatchesThroughANarrowOpenStrip()
        {
            var slope = Quaternion.AngleAxis(16, Vector3.right);
            PlaceCoveredSupport(slope, .004f);
            yield return null;
            yield return Tap(StoneCenter(), Vector2.down * 26);
            ExpectPose(slope);
        }

        [UnityTest]
        public IEnumerator ACompletelyCoveredSupportOffersNoDetent()
        {
            var slope = Quaternion.AngleAxis(16, Vector3.right);
            PlaceCoveredSupport(slope, -.004f);
            yield return null;
            yield return Tap(StoneCenter(), Vector2.down * 26);
            ExpectPose(Quaternion.AngleAxis(13.7f, Vector3.right));
            Assert.That(MatchOutline(), Is.Null);
        }

        void PlaceCoveredSupport(Quaternion slope, float exposedWidth)
        {
            // The second generated slab spans the first in depth. Position its edge
            // to leave either a thin vertical path or complete cover.
            var seeds = new[] { 504, 368, 123 };
            int next = 0;
            session.Initialize(new StoneFactory(root.GetComponent<GameBootstrap>().StoneMaterial,
                new StoneShapeSettings { FlatProbability = 1 }, () => seeds[Mathf.Min(next++, seeds.Length - 1)],
                arcade: true), session.Base, session.Ground);
            var lower = PlaceSupport(-.04f, slope);
            float lowerLeft = lower.transform.position.x + lower.OwnedMesh.bounds.min.x;
            float coverX = lowerLeft - stone.OwnedMesh.bounds.min.x + exposedWidth;
            PlaceSupport(coverX, Quaternion.identity, lower.HighestPoint());
            Assert.That(session.Adjust(stone, new Vector3(-.04f, session.BaseTop + .39f, 0),
                Quaternion.AngleAxis(2, Vector3.right)), Is.True);
        }

        Stone PlaceSupport(float x, Quaternion rotation, float? restHeight = null, float z = 0)
        {
            var support = session.Pending;
            float bottom = support.OwnedMesh.vertices.Min(vertex => (rotation * vertex).y);
            Assert.That(session.Adjust(support, new Vector3(x, support.transform.position.y, z), rotation), Is.True);
            Assert.That(session.Adjust(support, new Vector3(x, (restHeight ?? session.BaseTop) - bottom + .002f, z),
                rotation), Is.True);
            Assert.That(session.Release(support), Is.True);
            // Keep a reproducible tower top while the next stone is positioned and rotated.
            support.Body.constraints = RigidbodyConstraints.FreezeAll;
            support.Body.interpolation = RigidbodyInterpolation.None;
            support.Body.Sleep();
            stone = session.Pending;
            return support;
        }

        void ExpectSupportOutline(Stone support, bool visible)
        {
            var mesh = VisibleOutlineMesh();
            float top = support.OwnedMesh.vertices.Max(vertex => vertex.y);
            var corners = support.OwnedMesh.vertices.Where(vertex => Mathf.Abs(vertex.y - top) < .00001f)
                .Select(vertex => (Vector2)input.ViewCamera.WorldToScreenPoint(support.transform.TransformPoint(vertex))).ToArray();
            // Sample the outside half of the face, away from the pending stone and neighboring support.
            bool left = support.transform.position.x < 0;
            float outer = left ? corners.Max(corner => corner.x) : corners.Min(corner => corner.x);
            float middle = (corners.Max(corner => corner.x) + corners.Min(corner => corner.x)) * .5f;
            float low = corners.Min(corner => corner.y) - 3;
            float high = corners.Max(corner => corner.y) + 3;
            bool found = mesh.Any(point => point.x >= Mathf.Min(middle, outer) - 2 &&
                point.x <= Mathf.Max(middle, outer) + 2 && point.y >= low && point.y <= high);
            Assert.That(found, Is.EqualTo(visible), $"Outline on tower stone at x={support.transform.position.x:F2}");
        }

        Vector2[] VisibleOutlineMesh()
        {
            Canvas.ForceUpdateCanvases();
            var outline = MatchOutline();
            if (outline == null || outline.canvasRenderer.GetAlpha() < .99f) return System.Array.Empty<Vector2>();
            var mesh = outline.canvasRenderer.GetMesh();
            return mesh.vertices.Select(vertex => RectTransformUtility.WorldToScreenPoint(null,
                outline.rectTransform.TransformPoint(vertex))).ToArray();
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

        void UseBoxStones(params Vector3[] sizes)
        {
            session.Initialize(new BoxStones(root.GetComponent<GameBootstrap>().StoneMaterial, sizes),
                session.Base, session.Ground);
            stone = session.Pending;
        }

        sealed class BoxStones : IStoneFactory
        {
            readonly StoneFactory factory;
            readonly Vector3[] sizes;
            int next;

            public BoxStones(Material material, Vector3[] sizes)
            {
                factory = new StoneFactory(material, nextSeed: () => 123, arcade: true);
                this.sizes = sizes;
            }

            public async Task<Stone> CreateAsync(Vector3 position, Transform parent)
            {
                var stone = await factory.CreateAsync(position, parent);
                var half = sizes[Mathf.Min(next++, sizes.Length - 1)] * .5f;
                var mesh = ArcadeStoneHull.FromPlanes(new List<Plane>
                {
                    new(Vector3.left, -half.x), new(Vector3.right, -half.x),
                    new(Vector3.down, -half.y), new(Vector3.up, -half.y),
                    new(Vector3.back, -half.z), new(Vector3.forward, -half.z)
                }).ToMesh("Test slab");
                Object.Destroy(stone.OwnedMesh);
                stone.OwnedMesh = mesh;
                stone.GetComponent<MeshFilter>().sharedMesh = mesh;
                ((MeshCollider)stone.Shape).sharedMesh = mesh;
                return stone;
            }
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
