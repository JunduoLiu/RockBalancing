using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RockBalancing.Tests
{
    public sealed class ObservationTests
    {
        GameObject root;
        GameSession session;
        GameInput input;
        Mouse mouse;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [UnitySetUp] public IEnumerator Setup()
        {
            root = new GameObject("Observation scene", typeof(GameBootstrap));
            mouse = InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
            yield return null; yield return null;
            input = root.GetComponent<GameInput>(); session = input.Session;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            InputSystem.RemoveDevice(mouse); Object.Destroy(root); yield return null;
        }
        void Pose(float pitch, float yaw = 25, float distance = 3.8f)
        {
            typeof(GameInput).GetField("pitch", Private).SetValue(input, pitch);
            typeof(GameInput).GetField("yaw", Private).SetValue(input, yaw);
            typeof(GameInput).GetField("distance", Private).SetValue(input, distance);
            typeof(GameInput).GetMethod("PlaceCamera", Private).Invoke(input, null);
        }
        [UnityTest] public IEnumerator ExactPolesAndNearPolesHaveStableYawDefinedRightAxis()
        {
            var stone = session.Pending; var position = stone.transform.position; var rotation = stone.transform.rotation;
            foreach (float yaw in new[] { 0f, 89.9f, 180f, 270f, 359.9f })
            foreach (float pole in new[] { -90f, 90f })
            {
                Pose(pole, yaw);
                var camera = input.ViewCamera;
                Assert.That(Vector3.Dot(camera.transform.right, Quaternion.Euler(0, yaw, 0) * Vector3.right), Is.GreaterThan(.99999f));
                Assert.That(Vector3.Dot(camera.transform.forward, (position - camera.transform.position).normalized), Is.GreaterThan(.99999f));
                Assert.That(float.IsNaN(camera.transform.rotation.w), Is.False);
                var exact = camera.transform.rotation;
                Pose(pole - Mathf.Sign(pole) * .1f, yaw);
                Assert.That(Quaternion.Angle(exact, camera.transform.rotation), Is.LessThan(.15f));
                Pose(pole + Mathf.Sign(pole) * 20, yaw);
                Assert.That(Quaternion.Angle(exact, camera.transform.rotation), Is.LessThan(.02f), "No travel beyond either pole");
            }
            Assert.That(stone.transform.position, Is.EqualTo(position)); Assert.That(stone.transform.rotation, Is.EqualTo(rotation));
            yield return null;
        }
        [UnityTest] public IEnumerator BelowViewFadesBothOccludersAndRestoresWithoutChangingPhysics()
        {
            var stone = session.Pending; var position = stone.transform.position; var rotation = stone.transform.rotation;
            var pedestal = session.Base.GetComponent<Renderer>(); var ground = session.Ground.GetComponent<Renderer>();
            var baseMaterial = pedestal.sharedMaterial; var groundMaterial = ground.sharedMaterial;
            var baseBounds = session.Base.bounds; var groundBounds = session.Ground.bounds;
            Pose(-90); yield return new WaitForSecondsRealtime(.25f);
            Assert.That(input.ViewCamera.transform.position.y, Is.LessThan(session.Ground.bounds.min.y));
            Assert.That(input.Occlusion.OpacityFor(session.Base), Is.EqualTo(.08f).Within(.001f));
            Assert.That(input.Occlusion.OpacityFor(session.Ground), Is.Zero);
            Assert.That(pedestal.sharedMaterial.shader.name, Is.EqualTo("RockBalancing/ObservationFade"));
            Assert.That(session.Base.enabled && session.Ground.enabled, Is.True);
            Assert.That(session.Base.bounds, Is.EqualTo(baseBounds)); Assert.That(session.Ground.bounds, Is.EqualTo(groundBounds));
            Assert.That(stone.transform.position, Is.EqualTo(position)); Assert.That(stone.transform.rotation, Is.EqualTo(rotation));
            Assert.That(session.CurrentHeightMm, Is.Zero); Assert.That(stone.Released, Is.False);
            Pose(30); yield return new WaitForSecondsRealtime(.05f);
            Assert.That(input.Occlusion.OpacityFor(session.Base), Is.LessThan(.2f), "Clear-time hold suppresses brief threshold crossings");
            Pose(-90); yield return new WaitForSecondsRealtime(.05f);
            Assert.That(input.Occlusion.OpacityFor(session.Base), Is.EqualTo(.08f).Within(.001f));
            Pose(30); yield return new WaitForSecondsRealtime(.4f);
            Assert.That(pedestal.sharedMaterial, Is.SameAs(baseMaterial)); Assert.That(ground.sharedMaterial, Is.SameAs(groundMaterial));
        }
        [UnityTest] public IEnumerator ShareFromBelowIsOpaqueIdenticalAndRestoresFadeEvenOnException()
        {
            var share = root.GetComponent<ShareController>();
            var pedestal = session.Base.GetComponent<Renderer>(); var original = pedestal.sharedMaterial;
            Pose(-90); yield return new WaitForSecondsRealtime(.3f);
            // Capture an opaque reference at the same pose/frame: placement guides
            // intentionally follow the gameplay camera, so a top-view reference differs.
            byte[] expected;
            using (input.Occlusion.OpaqueScene()) { share.Capture(); expected = share.Snapshot.Png; share.Cancel(); }
            var faded = pedestal.sharedMaterial; var view = input.ViewCamera.transform.rotation;
            Assert.That(faded, Is.Not.SameAs(original));
            try { using (input.Occlusion.OpaqueScene()) { Assert.That(pedestal.sharedMaterial, Is.SameAs(original)); throw new InvalidOperationException(); } }
            catch (InvalidOperationException) { }
            Assert.That(pedestal.sharedMaterial, Is.SameAs(faded));
            share.Capture();
            CollectionAssert.AreEqual(expected, share.Snapshot.Png, "Share pixels cannot inherit gameplay fade");
            Assert.That(pedestal.sharedMaterial, Is.SameAs(faded));
            Assert.That(input.ViewCamera.transform.rotation, Is.EqualTo(view));
            share.Cancel();
        }
        [UnityTest] public IEnumerator VisibleStoneCanRotateThroughFadedPedestalFromBelow()
        {
            Pose(-90); yield return new WaitForSecondsRealtime(.3f);
            var stone = session.Pending; var rotation = stone.transform.rotation; var position = stone.transform.position;
            Vector2 point = input.ViewCamera.WorldToScreenPoint(position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left, true)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point + Vector2.right * 45 }.WithButton(MouseButton.Left, true)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point + Vector2.right * 45 }); yield return null;
            Assert.That(Quaternion.Angle(rotation, stone.transform.rotation), Is.GreaterThan(1));
            Assert.That(stone.transform.position, Is.EqualTo(position)); Assert.That(stone.Released, Is.False);
            Assert.That(input.ViewCamera.transform.rotation, Is.EqualTo(Quaternion.Euler(-90,25,0)));
        }
        [UnityTest] public IEnumerator PoleMovementUsesHorizontalHeadingAndFollowKeepsObservationContinuous()
        {
            foreach (float pole in new[] { -90f, 90f })
            {
                Pose(pole, 135); yield return null;
                var stone = session.Pending; var before = stone.transform.position; var camera = input.ViewCamera.transform.position;
                input.HoldPlanar(true); input.MoveStone(Vector2.up); yield return new WaitForSeconds(.15f);
                var motion = stone.transform.position - before;
                Assert.That(motion.y, Is.EqualTo(0).Within(.00001f));
                Assert.That(Vector3.Dot(motion.normalized, Quaternion.Euler(0,135,0)*Vector3.forward), Is.GreaterThan(.999f));
                Assert.That(input.ViewCamera.transform.position, Is.EqualTo(camera));
                input.HoldPlanar(false); yield return null;
                Assert.That(Vector3.Distance(camera,input.ViewCamera.transform.position), Is.LessThan(motion.magnitude));
                yield return new WaitForSeconds(.3f);
            }
        }
        [UnityTest] public IEnumerator OffsetHighTowerFadesBaseEvenWhenPendingIsNotOccludedAndSurvivesReplacement()
        {
            var support = session.Pending;
            support.transform.localScale = new Vector3(1, 12, 1); Physics.SyncTransforms();
            support.transform.position += Vector3.up * (session.BaseTop - support.Shape.bounds.min.y + .001f); Physics.SyncTransforms();
            Assert.That(session.Release(support), Is.True); support.Body.constraints = RigidbodyConstraints.FreezeAll;
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            var pending = session.Pending;
            Assert.That(session.Adjust(pending,new Vector3(.6f,support.HighestPoint()+.4f,0),pending.transform.rotation), Is.True);
            input.FollowSpeed=1000; yield return null;
            Pose(-60,25,6); yield return new WaitForSecondsRealtime(.3f);
            Assert.That(input.Occlusion.OpacityFor(session.Ground), Is.Zero);
            Assert.That(input.Occlusion.OpacityFor(session.Base), Is.LessThan(1));
            var view=input.ViewCamera.transform.rotation;
            session.Remove(pending); yield return null; yield return null;
            Assert.That(input.ViewCamera.transform.rotation,Is.EqualTo(view));
            session.Restart(); yield return null; yield return new WaitForSecondsRealtime(.4f);
            Assert.That(input.Occlusion.OpacityFor(session.Base),Is.EqualTo(1));
            Assert.That(session.Pending,Is.Not.Null);
        }

        [UnityTest] public IEnumerator TouchOrbitReachesBothPolesWithoutMovingOrReleasingStone()
        {
            var touch=InputSystem.AddDevice<Touchscreen>(); touch.MakeCurrent();
            var stone=session.Pending; var position=stone.transform.position; var rotation=stone.transform.rotation;
            var blank=new Vector2(Screen.width*.05f,Screen.height*.55f);
            try
            {
                foreach(float delta in new[]{-1000f,1000f})
                {
                    InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Began,position=blank}); yield return null;
                    InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Moved,position=blank+Vector2.up*delta}); yield return null;
                    InputSystem.QueueStateEvent(touch,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=blank+Vector2.up*delta}); yield return null; yield return null;
                    Assert.That(Vector3.Dot(input.ViewCamera.transform.forward,Vector3.up),Is.EqualTo(Mathf.Sign(delta)).Within(.0001f));
                }
                Assert.That(stone.transform.position,Is.EqualTo(position)); Assert.That(stone.transform.rotation,Is.EqualTo(rotation));
                Assert.That(stone.Released,Is.False);
            }
            finally { InputSystem.RemoveDevice(touch); }
        }
        [UnityTest] public IEnumerator OverlappingStoneEnvelopesResolveIndependentlyOfStoneListOrder()
        {
            var earlier=session.Pending; Assert.That(session.Release(earlier),Is.True);
            earlier.Body.constraints=RigidbodyConstraints.FreezeAll;
            var pending=session.Pending;
            var centre=new Vector3(0,3,0); pending.transform.position=centre; pending.transform.localScale=Vector3.one*5;
            Physics.SyncTransforms();
            float pendingRadius=pending.Shape.bounds.extents.magnitude+input.CameraClearance;
            earlier.transform.position=centre+Vector3.down*(pendingRadius+.1f);
            Physics.SyncTransforms();
            typeof(GameInput).GetField("target",Private).SetValue(input,centre);
            var bounds=earlier.Shape.bounds;
            float along=Vector3.Dot(bounds.center-centre,Vector3.down);
            float radius=bounds.extents.magnitude+input.CameraClearance;
            float side=(bounds.center-centre).sqrMagnitude-along*along;
            float far=along+Mathf.Sqrt(radius*radius-side);
            Pose(-90,0,.1f);
            Assert.That(Vector3.Distance(input.ViewCamera.transform.position,centre),Is.GreaterThanOrEqualTo(far-.001f),
                "Expanding past the pending envelope must recheck earlier stones");
            yield return null;
        }
    }
}
