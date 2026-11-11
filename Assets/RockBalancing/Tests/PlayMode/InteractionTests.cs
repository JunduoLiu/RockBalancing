using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RockBalancing.Tests
{
    public sealed class InteractionTests
    {
        GameObject root;
        GameSession session;
        GameInput input;
        Mouse mouse;
        Touchscreen touchscreen;
        [UnitySetUp] public IEnumerator Setup()
        {
            root=new GameObject("Interaction scene",typeof(GameBootstrap));
            mouse=InputSystem.AddDevice<Mouse>();mouse.MakeCurrent();
            yield return null;yield return null;
            session=root.GetComponentInChildren<GameSession>();input=root.GetComponent<GameInput>();
        }
        [UnityTearDown] public IEnumerator TearDown()
        { if(touchscreen!=null)InputSystem.RemoveDevice(touchscreen);InputSystem.RemoveDevice(mouse);Object.Destroy(root);yield return null; }
        void Pointer(Vector2 p,bool down)=>InputSystem.QueueStateEvent(mouse,new MouseState{position=p}.WithButton(MouseButton.Left,down));
        Vector2 Blank=>new Vector2(Screen.width*.05f,Screen.height*.55f);
        void Touch(int id,UnityEngine.InputSystem.TouchPhase phase,Vector2 p)=>InputSystem.QueueStateEvent(touchscreen,new TouchState{touchId=id,phase=phase,position=p});
        IEnumerator Tap(Vector2 p){Pointer(p,true);yield return null;Pointer(p,false);yield return null;yield return null;}
        Vector2 Centre(Component control) => RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)control.transform).TransformPoint(((RectTransform)control.transform).rect.center));
        GameView View => root.GetComponent<GameView>();
        DragPad Pad(bool vertical=false) => System.Array.Find(root.GetComponentsInChildren<DragPad>(), p=>p.VerticalOnly==vertical);
        PointerEventData PadEvent(DragPad pad,Vector2 offset,int id=123)
        {
            var rect=(RectTransform)pad.transform;
            float radius=Mathf.Min(rect.rect.width,rect.rect.height)*.36f;
            return new PointerEventData(EventSystem.current){pointerId=id,position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center+offset*radius))};
        }
        [UnityTest] public IEnumerator OnlyReleaseButtonDropsAndRapidRepeatCannotDropNextStone()
        {
            var pending=session.Pending;
            Assert.That(pending.transform.position.y-session.BaseTop,Is.EqualTo(.28f).Within(.001f),"First supply must use the scaled base surface");
            Pointer(Blank,true);yield return null;
            Pointer(Blank+Vector2.right*80,true);yield return null;
            Pointer(Blank+Vector2.right*80,false);yield return null;
            Assert.That(session.Pending,Is.SameAs(pending));
            var pad=new Vector2(Screen.width*.25f,Screen.height*.08f);
            Pointer(pad,true);yield return null;Pointer(pad+Vector2.right*30,true);yield return null;Pointer(pad+Vector2.right*30,false);yield return null;
            Assert.That(session.Pending,Is.SameAs(pending));
            yield return Tap(Blank);
            Assert.That(session.Pending,Is.SameAs(pending));
            yield return Tap(Centre(View.ReleaseButton));
            Assert.That(pending.Released,Is.True);Assert.That(session.Pending,Is.Not.SameAs(pending));
            var next=session.Pending;
            yield return Tap(Centre(View.ReleaseButton));
            Assert.That(session.Pending,Is.SameAs(next),"Rapid repeat must not release the next stone");
            yield return new WaitForSecondsRealtime(.5f);
            yield return Tap(Centre(View.ReleaseButton));
            Assert.That(next.Released,Is.True,"A fresh deliberate button click continues stacking");
        }
        [UnityTest] public IEnumerator StoneSelectionAndRotationDoNotReleaseOnPointerUp()
        {
            var pending=session.Pending;Vector2 point=input.ViewCamera.WorldToScreenPoint(pending.transform.position);
            yield return Tap(point);Assert.That(session.Pending,Is.SameAs(pending));
            var rotation=pending.transform.rotation;
            var position=pending.transform.position;
            var cameraPosition=input.ViewCamera.transform.position;
            var cameraRotation=input.ViewCamera.transform.rotation;
            Pointer(point,true);yield return null;Pointer(point+Vector2.right*45,true);yield return null;Pointer(point+Vector2.right*45,false);yield return null;
            Assert.That(session.Pending,Is.SameAs(pending));Assert.That(Quaternion.Angle(rotation,pending.transform.rotation),Is.GreaterThan(1));
            Assert.That(pending.transform.position,Is.EqualTo(position));
            Assert.That(Vector3.Distance(cameraPosition,input.ViewCamera.transform.position),Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(cameraRotation,input.ViewCamera.transform.rotation),Is.LessThan(.001f));
        }
        IEnumerator Orbit(Vector2 delta)
        {
            Pointer(Blank,true);yield return null;
            Pointer(Blank+delta,true);yield return null;
            Pointer(Blank+delta,false);yield return null;
        }
        IEnumerator Scroll(float amount)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState{position=Blank,scroll=new Vector2(0,amount)});
            yield return null;yield return null;
        }
        float Elevation(Vector3 centre) => Mathf.Asin((input.ViewCamera.transform.position-centre).normalized.y)*Mathf.Rad2Deg;
        void AssertAimedAt(Vector3 centre)
        {
            var point=input.ViewCamera.WorldToViewportPoint(centre);
            Assert.That(point.x,Is.EqualTo(.5f).Within(.001f));Assert.That(point.y,Is.EqualTo(.5f).Within(.001f));
            Assert.That(point.z,Is.GreaterThan(0));
            Assert.That(Vector3.Dot(input.ViewCamera.transform.up,Vector3.up),Is.GreaterThanOrEqualTo(-.00001f),"Camera cannot invert");
        }
        [UnityTest] public IEnumerator BlankOrbitCirclesPendingThroughFullTurnWithoutChangingStone()
        {
            var stone=session.Pending;var rotation=stone.transform.rotation;
            // Exercise a centre away from the base axis through the public adjustment boundary.
            Assert.That(session.Adjust(stone,new Vector3(.32f,.65f,-.18f),rotation),Is.True);
            input.FollowSpeed=1000;yield return null;
            var centre=stone.transform.position;var initial=input.ViewCamera.transform.position;
            float radius=Vector3.Distance(initial,centre);
            for(int i=0;i<8;i++)
            {
                yield return Orbit(Vector2.right*(45/input.OrbitSensitivity));
                AssertAimedAt(centre);
                Assert.That(Vector3.Distance(input.ViewCamera.transform.position,centre),Is.EqualTo(radius).Within(.002f));
                Assert.That(stone.transform.position,Is.EqualTo(centre));Assert.That(stone.transform.rotation,Is.EqualTo(rotation));
                if(i==3)Assert.That(Vector3.Distance(initial,input.ViewCamera.transform.position),Is.GreaterThan(radius*1.8f));
            }
            Assert.That(Vector3.Distance(initial,input.ViewCamera.transform.position),Is.LessThan(.002f));
            yield return Tap(Blank);Assert.That(stone.Released,Is.False);
        }
        [UnityTest] public IEnumerator PitchLimitsAllowTopViewRemainUprightAndRespondImmediatelyAwayFromBoundary()
        {
            var centre=session.Pending.transform.position;var rotation=session.Pending.transform.rotation;
            yield return Orbit(Vector2.down*1000);
            Assert.That(Elevation(centre),Is.EqualTo(90).Within(.03f));AssertAimedAt(centre);
            yield return Orbit(Vector2.right*90);AssertAimedAt(centre);
            yield return Orbit(Vector2.up*40);
            Assert.That(Elevation(centre),Is.EqualTo(80).Within(.01f));
            yield return Orbit(Vector2.up*1000);
            Assert.That(Elevation(centre),Is.EqualTo(-90).Within(.03f));AssertAimedAt(centre);
            input.MinimumPitch=12;input.MaximumPitch=70;
            yield return Orbit(Vector2.down*1000);Assert.That(Elevation(centre),Is.EqualTo(70).Within(.01f));
            yield return Orbit(Vector2.up*1000);Assert.That(Elevation(centre),Is.EqualTo(12).Within(.01f));
            Assert.That(session.Pending.transform.position,Is.EqualTo(centre));Assert.That(session.Pending.transform.rotation,Is.EqualTo(rotation));
        }
        [UnityTest] public IEnumerator ZoomLimitsAreConfigurableAndNearPlaneStaysOutsideStoneBelowGround()
        {
            var stone=session.Pending;var centre=stone.transform.position;
            input.MinimumZoom=.8f;input.MaximumZoom=6;
            yield return Scroll(100000);
            Assert.That(Vector3.Distance(input.ViewCamera.transform.position,centre),Is.EqualTo(.8f).Within(.002f));
            yield return Orbit(Vector2.down*1000);AssertAimedAt(centre);
            yield return Scroll(-100000);
            Assert.That(Vector3.Distance(input.ViewCamera.transform.position,centre),Is.EqualTo(6).Within(.002f));
            // Safety takes precedence over a zoom limit that would enter geometry.
            input.MinimumZoom=.1f;stone.transform.localScale=Vector3.one*5;Physics.SyncTransforms();
            // The enlarged preview fills the screen at the close limit. Set the
            // boundary directly rather than mistaking a stone drag for blank orbit.
            input.MinimumPitch=input.MaximumPitch=-90;
            yield return Scroll(100000);
            for(int i=0;i<8;i++)
            {
                typeof(GameInput).GetField("yaw",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(input,i*45f);
                yield return null;
                var p=input.ViewCamera.transform.position;
                Assert.That(Vector3.Distance(p,stone.Shape.ClosestPoint(p)),Is.GreaterThanOrEqualTo(input.CameraClearance-.001f));
                Assert.That(p.y,Is.LessThan(session.Ground.bounds.min.y),"Negative pitch is not cancelled by a scenery height clamp");
                var corners=new Vector3[4];
                input.ViewCamera.CalculateFrustumCorners(new Rect(0,0,1,1),input.ViewCamera.nearClipPlane,Camera.MonoOrStereoscopicEye.Mono,corners);
                foreach(var corner in corners)
                {
                    var world=input.ViewCamera.transform.TransformPoint(corner);
                    Assert.That(Vector3.Distance(world,stone.Shape.ClosestPoint(world)),Is.GreaterThan(.001f),"Near-plane corners must clear the stone too");
                }
                AssertAimedAt(centre);
            }
            Assert.That(stone.transform.position,Is.EqualTo(centre));Assert.That(stone.Released,Is.False);
        }
        [UnityTest] public IEnumerator TwoSceneTouchesOnlyZoomAndRemainingFingerCannotOrbitOrRotate()
        {
            touchscreen=InputSystem.AddDevice<Touchscreen>();touchscreen.MakeCurrent();
            var stone=session.Pending;var centre=stone.transform.position;var rotation=stone.transform.rotation;
            var cameraRotation=input.ViewCamera.transform.rotation;float before=Vector3.Distance(input.ViewCamera.transform.position,centre);
            Vector2 a=input.ViewCamera.WorldToScreenPoint(centre), b=a+Vector2.right*(Screen.width*.25f);
            Touch(1,UnityEngine.InputSystem.TouchPhase.Began,a);yield return null;
            Touch(2,UnityEngine.InputSystem.TouchPhase.Began,b);yield return null;
            var moved=b+(b-a)*.5f;
            Touch(2,UnityEngine.InputSystem.TouchPhase.Moved,moved);yield return null;yield return null;
            Assert.That(Vector3.Distance(input.ViewCamera.transform.position,centre),Is.EqualTo(before/1.5f).Within(.01f),
                $"Scene touches: {touchscreen.touches[0].position.ReadValue()} / {touchscreen.touches[1].position.ReadValue()}, screen {Screen.width}x{Screen.height}");
            Assert.That(Quaternion.Angle(cameraRotation,input.ViewCamera.transform.rotation),Is.LessThan(.001f));
            Touch(2,UnityEngine.InputSystem.TouchPhase.Ended,moved);yield return null;
            var cameraPosition=input.ViewCamera.transform.position;
            Touch(1,UnityEngine.InputSystem.TouchPhase.Moved,a+new Vector2(70,70));yield return null;
            Touch(1,UnityEngine.InputSystem.TouchPhase.Ended,a+new Vector2(70,70));yield return null;
            Assert.That(input.ViewCamera.transform.position,Is.EqualTo(cameraPosition));
            Assert.That(stone.transform.position,Is.EqualTo(centre));Assert.That(stone.transform.rotation,Is.EqualTo(rotation));
            Assert.That(stone.Released,Is.False);
        }
        [UnityTest] public IEnumerator MissingOrRemovedPendingKeepsUsableViewAndRestartRestoresInitialPose()
        {
            var initial=input.ViewCamera.transform.position;var initialRotation=input.ViewCamera.transform.rotation;
            yield return Orbit(new Vector2(160,-160));yield return Scroll(800);
            Object.Destroy(session.Pending.gameObject);yield return null;
            var held=input.ViewCamera.transform.position;var heldRotation=input.ViewCamera.transform.rotation;
            yield return null;yield return null;
            Assert.That(input.ViewCamera.transform.position,Is.EqualTo(held));
            Assert.That(input.ViewCamera.transform.rotation,Is.EqualTo(heldRotation));
            yield return Orbit(Vector2.right*80);
            Assert.That(Vector3.Distance(input.ViewCamera.transform.position,held),Is.GreaterThan(.1f));
            session.Restart();yield return null;yield return null;
            Assert.That(Vector3.Distance(input.ViewCamera.transform.position,initial),Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(input.ViewCamera.transform.rotation,initialRotation),Is.LessThan(.001f));
            Assert.That(session.Pending,Is.Not.Null);
        }
        [UnityTest] public IEnumerator BackgroundingDuringTouchCannotReleaseOnReturn()
        {
            var pending=session.Pending;
            var pad=root.GetComponentInChildren<DragPad>();
            pad.OnPointerDown(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){pointerId=123});
            Assert.That(input.Adjusting,Is.True);
            Pointer(Blank,true);yield return null;input.CancelForFocusLoss();
            Assert.That(input.Adjusting,Is.False,"Interrupted adjustment must not leave the camera locked");
            Pointer(Blank,true);yield return null;Pointer(Blank,false);yield return null;
            Assert.That(session.Pending,Is.SameAs(pending));
            yield return Tap(Blank);Assert.That(pending.Released,Is.False);
            yield return Tap(Centre(View.ReleaseButton));Assert.That(pending.Released,Is.True);
        }
        [UnityTest] public IEnumerator PinchAndCancelledTouchCannotBecomeRelease()
        {
            touchscreen=InputSystem.AddDevice<Touchscreen>();touchscreen.MakeCurrent();
            var pending=session.Pending;var a=Blank;var b=Blank+Vector2.right*100;
            Touch(1,UnityEngine.InputSystem.TouchPhase.Began,a);yield return null;
            Touch(2,UnityEngine.InputSystem.TouchPhase.Began,b);yield return null;
            Touch(2,UnityEngine.InputSystem.TouchPhase.Moved,b-Vector2.right*40);yield return null;
            Touch(2,UnityEngine.InputSystem.TouchPhase.Ended,b-Vector2.right*40);yield return null;
            Touch(1,UnityEngine.InputSystem.TouchPhase.Ended,a);yield return null;
            Assert.That(session.Pending,Is.SameAs(pending));
            Touch(3,UnityEngine.InputSystem.TouchPhase.Began,a);yield return null;
            Touch(3,UnityEngine.InputSystem.TouchPhase.Canceled,a);yield return null;
            Assert.That(session.Pending,Is.SameAs(pending));
            Touch(4,UnityEngine.InputSystem.TouchPhase.Began,a);yield return null;
            Touch(4,UnityEngine.InputSystem.TouchPhase.Ended,a);yield return null;
            Assert.That(pending.Released,Is.False,"Even a fresh blank tap never releases");
            Touch(5,UnityEngine.InputSystem.TouchPhase.Began,Centre(View.ReleaseButton));yield return null;
            Touch(5,UnityEngine.InputSystem.TouchPhase.Ended,Centre(View.ReleaseButton));yield return null;
            Assert.That(pending.Released,Is.True,"A fresh release button touch after cancellation remains usable");
        }
        [UnityTest] public IEnumerator JoystickHasDeadZoneProportionalSpeedAndStopsAtCentreOrPointerUp()
        {
            var stone=session.Pending;var pad=Pad();var origin=stone.transform.position;
            pad.OnPointerDown(PadEvent(pad,new Vector2(.05f,0)));
            yield return new WaitForSeconds(.1f);
            Assert.That(stone.transform.position,Is.EqualTo(origin));
            pad.OnDrag(PadEvent(pad,new Vector2(.5f,0)));
            float elapsed=0;var start=stone.transform.position;
            for(int i=0;i<12;i++){yield return null;elapsed+=Time.deltaTime;}
            float halfSpeed=Vector3.Distance(start,stone.transform.position)/elapsed;
            Assert.That(halfSpeed,Is.EqualTo(input.MaximumPlanarSpeed*(.5f-input.JoystickDeadZone)/(1-input.JoystickDeadZone)).Within(.015f));
            pad.OnDrag(PadEvent(pad,Vector2.one));start=stone.transform.position;elapsed=0;
            for(int i=0;i<12;i++){yield return null;elapsed+=Time.deltaTime;}
            Assert.That(Vector3.Distance(start,stone.transform.position)/elapsed,Is.EqualTo(input.MaximumPlanarSpeed).Within(.02f),"Diagonal input is capped");
            Assert.That(stone.transform.position.y,Is.EqualTo(origin.y));
            pad.OnDrag(PadEvent(pad,Vector2.zero));start=stone.transform.position;
            yield return new WaitForSeconds(.1f);Assert.That(stone.transform.position,Is.EqualTo(start));
            pad.OnDrag(PadEvent(pad,Vector2.right));yield return null;
            pad.OnPointerUp(PadEvent(pad,Vector2.right));start=stone.transform.position;
            yield return new WaitForSeconds(.1f);Assert.That(stone.transform.position,Is.EqualTo(start));
            Assert.That(stone.Released,Is.False);
        }
        [UnityTest] public IEnumerator JoystickSpeedIsStableAcrossFrameRates()
        {
            float saved=Time.captureDeltaTime;
            try
            {
                var pad=Pad();var stone=session.Pending;float[] distances=new float[2];
                for(int run=0;run<2;run++)
                {
                    int fps=run==0?30:120;Time.captureDeltaTime=1f/fps;
                    yield return null;
                    pad.OnPointerDown(PadEvent(pad,Vector2.right));var start=stone.transform.position;
                    for(int i=0;i<fps/2;i++)yield return null;
                    pad.OnPointerUp(PadEvent(pad,Vector2.right));distances[run]=Vector3.Distance(start,stone.transform.position);
                }
                Assert.That(distances[0],Is.EqualTo(input.MaximumPlanarSpeed*.5f).Within(.015f));
                Assert.That(distances[1],Is.EqualTo(distances[0]).Within(.015f));
            }
            finally {Time.captureDeltaTime=saved;}
        }
        [UnityTest] public IEnumerator TwoControlTouchesMoveAndLiftWithoutPinchingOrReleasing()
        {
            touchscreen=InputSystem.AddDevice<Touchscreen>();touchscreen.MakeCurrent();
            var stone=session.Pending;var origin=stone.transform.position;
            var a=PadEvent(Pad(),Vector2.right).position;var b=PadEvent(Pad(true),Vector2.up).position;
            Touch(1,UnityEngine.InputSystem.TouchPhase.Began,a);Touch(2,UnityEngine.InputSystem.TouchPhase.Began,b);yield return null;
            yield return null;var camera=input.ViewCamera.transform.position;
            yield return new WaitForSeconds(.15f);
            Assert.That(Vector3.Distance(new Vector3(stone.transform.position.x,origin.y,stone.transform.position.z),origin),Is.GreaterThan(.01f));
            Assert.That(stone.transform.position.y,Is.GreaterThan(origin.y+.01f));
            Assert.That(input.ViewCamera.transform.position,Is.EqualTo(camera),"Controls must lock adjustment follow and must not pinch");
            Touch(1,UnityEngine.InputSystem.TouchPhase.Ended,a);yield return null;
            var p=stone.transform.position;
            Touch(2,UnityEngine.InputSystem.TouchPhase.Moved,b+Vector2.up*10);yield return new WaitForSeconds(.1f);
            Assert.That(stone.transform.position.x,Is.EqualTo(p.x));Assert.That(stone.transform.position.z,Is.EqualTo(p.z));
            Assert.That(stone.transform.position.y,Is.GreaterThan(p.y));
            Touch(2,UnityEngine.InputSystem.TouchPhase.Ended,b);yield return null;
            p=stone.transform.position;yield return new WaitForSeconds(.1f);
            Assert.That(stone.transform.position,Is.EqualTo(p));Assert.That(stone.Released,Is.False);
        }
        [UnityTest] public IEnumerator InterruptedControlsRequireFreshInputAfterFocusModalAndRestart()
        {
            var pad=Pad();
            for(int mode=0;mode<3;mode++)
            {
                pad.OnPointerDown(PadEvent(pad,Vector2.right));yield return null;
                if(mode==0) input.CancelForFocusLoss();
                if(mode==1) {input.Modal=true;input.Modal=false;}
                if(mode==2) session.Restart();
                var stone=session.Pending;var position=stone.transform.position;
                pad.OnDrag(PadEvent(pad,Vector2.right));yield return new WaitForSeconds(.1f);
                Assert.That(stone.transform.position,Is.EqualTo(position));Assert.That(input.Adjusting,Is.False);
                pad.OnPointerUp(PadEvent(pad,Vector2.right));yield return null;
                pad.OnPointerDown(PadEvent(pad,Vector2.right));yield return new WaitForSeconds(.1f);
                Assert.That(Vector3.Distance(stone.transform.position,position),Is.GreaterThan(.01f));
                pad.OnPointerUp(PadEvent(pad,Vector2.right));
            }
        }
        [UnityTest] public IEnumerator ReleaseAndDiscardClearHeldControlsAndOldButtonIntent()
        {
            for(int mode=0;mode<2;mode++)
            {
                yield return new WaitForSecondsRealtime(.5f);
                var pad=Pad();pad.OnPointerDown(PadEvent(pad,Vector2.right));yield return null;
                var old=session.Pending;var button=mode==0?View.ReleaseButton:View.DiscardButton;
                var e=new PointerEventData(EventSystem.current){pointerId=9,button=PointerEventData.InputButton.Left,clickCount=1};
                ((StoneActionButton)button).OnPointerDown(e);((StoneActionButton)button).OnPointerClick(e);
                Assert.That(old.Released,Is.True);Assert.That(old.Discarded,Is.EqualTo(mode==1));
                var next=session.Pending;var p=next.transform.position;
                pad.OnDrag(PadEvent(pad,Vector2.right));yield return new WaitForSecondsRealtime(.5f);
                ((StoneActionButton)button).OnPointerClick(e);
                Assert.That(session.Pending,Is.SameAs(next));Assert.That(next.transform.position,Is.EqualTo(p));
                pad.OnPointerUp(PadEvent(pad,Vector2.right));yield return null;
            }
        }
        [UnityTest] public IEnumerator CanceledControlTouchStopsAndCannotResumeByMovingOldTouch()
        {
            touchscreen=InputSystem.AddDevice<Touchscreen>();touchscreen.MakeCurrent();
            var a=PadEvent(Pad(),Vector2.right).position;
            Touch(1,UnityEngine.InputSystem.TouchPhase.Began,a);yield return new WaitForSeconds(.1f);
            Touch(1,UnityEngine.InputSystem.TouchPhase.Canceled,a);yield return null;
            var stone=session.Pending;var p=stone.transform.position;
            yield return new WaitForSeconds(.1f);Assert.That(stone.transform.position,Is.EqualTo(p));
            Assert.That(stone.Released,Is.False);Assert.That(input.Adjusting,Is.False);
            Touch(2,UnityEngine.InputSystem.TouchPhase.Began,a);yield return new WaitForSeconds(.1f);
            Assert.That(Vector3.Distance(stone.transform.position,p),Is.GreaterThan(.01f));
            Touch(2,UnityEngine.InputSystem.TouchPhase.Ended,a);yield return null;
        }
        [UnityTest] public IEnumerator ButtonRejectsDynamicOverlapThenAllowsLiftAndRelease()
        {
            var stone=session.Pending;
            var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.transform.SetParent(root.transform);
            obstacle.transform.localScale=Vector3.one*.1f;obstacle.transform.position=stone.transform.position-Vector3.up*.03f;
            var body=obstacle.AddComponent<Rigidbody>();body.isKinematic=true;body.interpolation=RigidbodyInterpolation.Interpolate;
            Physics.SyncTransforms();
            yield return Tap(Centre(View.ReleaseButton));
            Assert.That(session.Pending,Is.SameAs(stone));Assert.That(stone.Body.isKinematic,Is.True);
            Assert.That(session.Feedback,Is.Not.Empty);Assert.That(session.Stones.Count,Is.EqualTo(1));
            var pad=Pad(true);pad.OnPointerDown(PadEvent(pad,Vector2.up));yield return new WaitForSeconds(.4f);
            pad.OnPointerUp(PadEvent(pad,Vector2.up));yield return new WaitForSecondsRealtime(.5f);
            yield return Tap(Centre(View.ReleaseButton));Assert.That(stone.Released,Is.True);
        }
        [UnityTest] public IEnumerator AdjustmentAndManualObservationHoldCentreUntilBothEndThenRecenterSmoothly()
        {
            var stone=session.Pending;var centre=stone.transform.position;
            var pad=Pad();var initial=input.ViewCamera.transform.position;
            pad.OnPointerDown(PadEvent(pad,Vector2.right));yield return new WaitForSeconds(.4f);
            Assert.That(input.ViewCamera.transform.position,Is.EqualTo(initial));
            Assert.That(Vector3.Distance(centre,stone.transform.position),Is.GreaterThan(.08f));
            Pointer(Blank,true);yield return null;
            Pointer(Blank+Vector2.right*120,true);yield return null;yield return null;
            AssertAimedAt(centre);
            pad.OnPointerUp(PadEvent(pad,Vector2.right));
            var observed=input.ViewCamera.transform.position;var position=stone.transform.position;
            yield return new WaitForSeconds(.2f);
            Assert.That(input.ViewCamera.transform.position,Is.EqualTo(observed),"Held observation must not drift when the control finger ends");
            Pointer(Blank+Vector2.right*120,false);yield return null;yield return null;
            float full=Vector3.Distance(centre,position), first=Vector3.Distance(observed,input.ViewCamera.transform.position);
            Assert.That(first,Is.GreaterThan(0));Assert.That(first,Is.LessThan(full*.8f),"Recenter must not snap");
            yield return new WaitForSeconds(2);
            Assert.That(Vector3.Distance(input.ViewCamera.transform.position,observed+(position-centre)),Is.LessThan(.006f));
            Assert.That(stone.transform.position,Is.EqualTo(position));Assert.That(stone.Released,Is.False);
        }
        [UnityTest] public IEnumerator TopViewMovementUsesCurrentOrbitHeadingAndControlTouchNeverJoinsPinch()
        {
            yield return Orbit(Vector2.down*1000);
            touchscreen=InputSystem.AddDevice<Touchscreen>();touchscreen.MakeCurrent();
            // Let UI actions bind the newly added device after the mouse gesture.
            yield return null;yield return null;
            var stone=session.Pending;var centre=stone.transform.position;
            var control=PadEvent(Pad(),Vector2.up).position;
            Touch(1,UnityEngine.InputSystem.TouchPhase.Began,control);
            Touch(2,UnityEngine.InputSystem.TouchPhase.Began,Blank);yield return null;yield return null;
            float radius=Vector3.Distance(input.ViewCamera.transform.position,centre);
            Vector3 lastDirection=Vector3.zero;
            for(int i=1;i<=8;i++)
            {
                Touch(2,UnityEngine.InputSystem.TouchPhase.Moved,Blank+Vector2.right*(i*40));
                yield return null;
                var before=stone.transform.position;
                yield return null;
                var delta=stone.transform.position-before;
                var expected=Vector3.Cross(input.ViewCamera.transform.right,Vector3.up).normalized;
                Assert.That(delta.magnitude,Is.GreaterThan(.00001f),$"Step {i}, control {control}, adjusting {input.Adjusting}, available {input.CanAdjust}");
                Assert.That(Vector3.Dot(delta.normalized,expected),Is.GreaterThan(.999f),"Use the same frame's yaw even at 85 degrees");
                if(i>1)Assert.That(Vector3.Dot(lastDirection,delta.normalized),Is.GreaterThan(.97f),"A small orbit step must not reverse movement");
                lastDirection=delta.normalized;
                Assert.That(stone.transform.position.y,Is.EqualTo(centre.y));
                Assert.That(Vector3.Distance(input.ViewCamera.transform.position,centre),Is.EqualTo(radius).Within(.002f));
            }
            // The scene finger may cross the control region but never becomes a joystick.
            Touch(2,UnityEngine.InputSystem.TouchPhase.Moved,Centre(Pad(true)));yield return null;
            Touch(1,UnityEngine.InputSystem.TouchPhase.Ended,control);yield return null;
            var stopped=stone.transform.position;
            Touch(2,UnityEngine.InputSystem.TouchPhase.Moved,PadEvent(Pad(true),Vector2.up).position);yield return new WaitForSeconds(.1f);
            Assert.That(stone.transform.position,Is.EqualTo(stopped));
            Touch(2,UnityEngine.InputSystem.TouchPhase.Ended,Centre(Pad(true)));yield return null;
            Assert.That(stone.Released,Is.False);
        }
        [UnityTest] public IEnumerator ControlDraggedIntoSceneKeepsOwnershipAndPinchWaitsForAllSceneFingers()
        {
            touchscreen=InputSystem.AddDevice<Touchscreen>();touchscreen.MakeCurrent();
            var stone=session.Pending;var centre=stone.transform.position;
            var a=Centre(Pad());var b=Blank;var c=Blank+Vector2.right*100;
            Touch(1,UnityEngine.InputSystem.TouchPhase.Began,a);yield return null;
            Touch(1,UnityEngine.InputSystem.TouchPhase.Moved,a+Vector2.right*200);yield return new WaitForSeconds(.2f);
            Touch(2,UnityEngine.InputSystem.TouchPhase.Began,b);Touch(3,UnityEngine.InputSystem.TouchPhase.Began,c);yield return null;
            Touch(1,UnityEngine.InputSystem.TouchPhase.Moved,new Vector2(Screen.width*.9f,Screen.height*.6f));yield return null;yield return null;
            Assert.That(Vector3.Distance(input.ViewCamera.transform.position,centre),Is.EqualTo(3.8f).Within(.002f),"Moving a control finger into the scene must not pinch");
            Touch(1,UnityEngine.InputSystem.TouchPhase.Ended,a);yield return null;
            Touch(3,UnityEngine.InputSystem.TouchPhase.Moved,c+Vector2.right*50);yield return null;yield return null;
            Assert.That(Vector3.Distance(input.ViewCamera.transform.position,centre),Is.EqualTo(3.8f/1.5f).Within(.01f));
            var camera=input.ViewCamera.transform.position;
            Touch(3,UnityEngine.InputSystem.TouchPhase.Ended,c+Vector2.right*50);yield return null;
            Touch(2,UnityEngine.InputSystem.TouchPhase.Moved,b+Vector2.up*50);yield return new WaitForSeconds(.15f);
            Assert.That(input.ViewCamera.transform.position,Is.EqualTo(camera),"Remaining pinch finger owns the centre until it ends");
            Touch(2,UnityEngine.InputSystem.TouchPhase.Canceled,b);yield return null;
            var stopped=stone.transform.position;
            yield return new WaitForSeconds(.2f);
            Assert.That(stone.transform.position,Is.EqualTo(stopped));Assert.That(input.Adjusting,Is.False);
            Assert.That(Vector3.Distance(camera,input.ViewCamera.transform.position),Is.GreaterThan(.001f),"Cancel releases the observation lock");
            Assert.That(stone.Released,Is.False);
        }
        sealed class NextSupplyFactory : IStoneFactory
        {
            bool first=true;
            public readonly System.Threading.Tasks.TaskCompletionSource<Stone> Next = new System.Threading.Tasks.TaskCompletionSource<Stone>();
            public System.Threading.Tasks.Task<Stone> CreateAsync(Vector3 position,Transform parent)
            {
                if(first || Next.Task.IsCompleted){first=false;return new StoneFactory(nextSeed:()=>123).CreateAsync(position,parent);}
                return Next.Task;
            }
        }
        [UnityTest] public IEnumerator ReleaseAndDiscardWaitInPlaceThenSmoothlyAcquireLateSupplyWithoutOldInput()
        {
            for(int discard=0;discard<2;discard++)
            {
                var factory=new NextSupplyFactory();session.Initialize(factory,session.Base,session.Ground);yield return null;yield return null;
                var stone=session.Pending;var centre=stone.transform.position;
                var pad=Pad();pad.OnPointerDown(PadEvent(pad,Vector2.right));yield return new WaitForSeconds(.25f);
                var camera=input.ViewCamera.transform.position;var rotation=input.ViewCamera.transform.rotation;
                Assert.That(input.ActOnStone(stone,discard==1),Is.True);
                yield return new WaitForSeconds(.2f);
                Assert.That(session.Waiting,Is.True);Assert.That(input.ViewCamera.transform.position,Is.EqualTo(camera));
                Assert.That(input.ViewCamera.transform.rotation,Is.EqualTo(rotation));
                var next=new StoneFactory(nextSeed:()=>456).CreateAsync(centre+new Vector3(-.3f,.5f,0),session.transform).Result;
                var newCentre=next.transform.position;factory.Next.SetResult(next);
                yield return null;yield return null;
                Assert.That(session.Pending,Is.SameAs(next));
                float travel=Vector3.Distance(camera,input.ViewCamera.transform.position);
                Assert.That(travel,Is.GreaterThan(0));Assert.That(travel,Is.LessThan(Vector3.Distance(centre,newCentre)*.8f));
                pad.OnDrag(PadEvent(pad,Vector2.right));yield return new WaitForSeconds(2);
                Assert.That(next.transform.position,Is.EqualTo(newCentre));Assert.That(next.Released,Is.False);
                Assert.That(Vector3.Distance(input.ViewCamera.transform.position,camera+newCentre-centre),Is.LessThan(.02f));
                pad.OnPointerUp(PadEvent(pad,Vector2.right));
                session.Restart();yield return null;yield return null;
                Assert.That(Vector3.Distance(input.ViewCamera.transform.position,camera),Is.LessThan(.002f));
                Assert.That(input.Adjusting,Is.False);
            }
        }
        [UnityTest] public IEnumerator HighTowerOffsetPendingAndCollapseKeepControlsAndGuidesReadable()
        {
            var support=session.Pending;support.transform.localScale=new Vector3(1,20,1);Physics.SyncTransforms();
            support.transform.position+=Vector3.up*(session.BaseTop-support.Shape.bounds.min.y+.001f);Physics.SyncTransforms();
            Assert.That(session.Release(support),Is.True);support.Body.constraints=RigidbodyConstraints.FreezeAll;
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();session.RefreshState();
            var stone=session.Pending;
            Assert.That(session.Adjust(stone,new Vector3(.25f,support.HighestPoint()+.35f,-.15f),stone.transform.rotation),Is.True);
            input.FollowSpeed=10;yield return new WaitForSeconds(.8f);
            AssertAimedAt(stone.transform.position);
            var lines=root.GetComponentsInChildren<LineRenderer>();
            var stem=System.Array.Find(lines,l=>l.name=="Drop guide");
            var ring=System.Array.Find(lines,l=>l.name=="Selected stone outline");
            var footprint=System.Array.Find(lines,l=>l.name=="Drop position");
            Assert.That(stem.enabled && ring.enabled && footprint.enabled,Is.True);
            Assert.That(stem.GetPosition(0),Is.EqualTo(stone.transform.position));
            Assert.That(stem.GetPosition(1).y,Is.LessThan(stone.transform.position.y));
            Assert.That(View.ReleaseButton.IsActive() && View.ReleaseButton.interactable,Is.True);
            Assert.That(Pad().isActiveAndEnabled && Pad(true).isActiveAndEnabled,Is.True);
            yield return Scroll(-100000);yield return null;
            var camera=input.ViewCamera;
            var p=stone.transform.position;
            float pixels=Vector2.Distance(camera.WorldToScreenPoint(p),camera.WorldToScreenPoint(p+camera.transform.right*ring.startWidth));
            Assert.That(pixels,Is.GreaterThanOrEqualTo(1.9f),"Guide retains a visible screen width at the far zoom limit");
            yield return Scroll(100000);yield return Orbit(Vector2.down*1000);
            AssertAimedAt(stone.transform.position);
            // Let the supporting stone topple away; collapse must not take input or target ownership.
            support.Body.constraints=RigidbodyConstraints.None;support.Body.linearVelocity=Vector3.right*2;support.Body.WakeUp();
            yield return new WaitForSeconds(.6f);
            Assert.That(session.Pending,Is.SameAs(stone));AssertAimedAt(stone.transform.position);
            Assert.That(stem.enabled && footprint.enabled,Is.True);Assert.That(View.ReleaseButton.interactable,Is.True);
            var pad=Pad();var before=stone.transform.position;
            pad.OnPointerDown(PadEvent(pad,Vector2.right));yield return new WaitForSeconds(.1f);pad.OnPointerUp(PadEvent(pad,Vector2.right));
            Assert.That(Vector3.Distance(before,stone.transform.position),Is.GreaterThan(.01f));
        }
        [UnityTest] public IEnumerator OffscreenPendingRetainsNonBlockingCueWithoutLandingLabels()
        {
            var stone=session.Pending;var initial=input.ViewCamera.transform.position;
            input.Adjusting=true;
            Assert.That(session.Adjust(stone,stone.transform.position+input.ViewCamera.transform.right*3+Vector3.up,stone.transform.rotation),Is.True);
            yield return null;yield return null;
            var pending=View.Root.Find("Pending direction");
            Assert.That(View.Root.Find("Landing direction"),Is.Null);
            Assert.That(pending.gameObject.activeSelf,Is.True);
            Assert.That(pending.GetComponentInChildren<Text>().text,Does.Contain("→"));
            Assert.That(input.ViewCamera.transform.position,Is.EqualTo(initial),"The cue must not move the locked camera");
            foreach(var marker in new[]{pending})
            {
                var p=Centre(marker);var hits=new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=p},hits);
                Assert.That(hits.Exists(h=>h.gameObject.transform.IsChildOf(marker)),Is.False,"Indicators must not steal scene touches");
                var anchor=((RectTransform)marker).anchorMin;
                Assert.That(anchor.x,Is.InRange(.12f,.88f));Assert.That(anchor.y,Is.InRange(.38f,.83f));
                var corners=new Vector3[4];((RectTransform)marker).GetWorldCorners(corners);
                foreach(var corner in corners)Assert.That(RectTransformUtility.WorldToScreenPoint(null,corner).x,Is.InRange(0f,(float)Screen.width));
            }
            input.Adjusting=false;input.FollowSpeed=10;yield return new WaitForSeconds(.8f);
            Assert.That(pending.gameObject.activeSelf,Is.False,"Recentered pending no longer needs a direction cue");
            AssertAimedAt(stone.transform.position);
            input.Modal=true;yield return null;yield return null;
            Assert.That(pending.gameObject.activeSelf,Is.False);input.Modal=false;
            session.Restart();yield return null;yield return null;
            Assert.That(pending.gameObject.activeSelf,Is.False);
        }
        sealed class DelayedFactory : IStoneFactory
        {
            public readonly System.Threading.Tasks.TaskCompletionSource<Stone> Supply = new System.Threading.Tasks.TaskCompletionSource<Stone>();
            public System.Threading.Tasks.Task<Stone> CreateAsync(Vector3 position,Transform parent) => Supply.Task;
        }
        sealed class DelayedNextFactory : IStoneFactory
        {
            bool first=true;
            public System.Threading.Tasks.Task<Stone> CreateAsync(Vector3 position,Transform parent)
            {
                if(first){first=false;return new StoneFactory(nextSeed:()=>123).CreateAsync(position,parent);}
                return new System.Threading.Tasks.TaskCompletionSource<Stone>().Task;
            }
        }
        [UnityTest] public IEnumerator WaitingForNextDoesNotFollowReleasedStone()
        {
            session.Initialize(new DelayedNextFactory(),session.Base,session.Ground);yield return null;yield return null;
            yield return Orbit(new Vector2(100,-100));
            var stone=session.Pending;var position=input.ViewCamera.transform.position;var rotation=input.ViewCamera.transform.rotation;
            Assert.That(session.Release(stone),Is.True);Assert.That(session.Waiting,Is.True);
            yield return new WaitForSeconds(.4f);
            Assert.That(input.ViewCamera.transform.position,Is.EqualTo(position));
            Assert.That(input.ViewCamera.transform.rotation,Is.EqualTo(rotation));
            session.Remove(stone);yield return null;yield return null;
            Assert.That(input.ViewCamera.transform.position,Is.EqualTo(position));
            yield return Orbit(Vector2.right*100);
            Assert.That(Vector3.Distance(position,input.ViewCamera.transform.position),Is.GreaterThan(.1f));
        }
        [UnityTest] public IEnumerator WaitingDisablesReleaseAndOldPressCannotActOnLateSupply()
        {
            var button=(StoneActionButton)View.ReleaseButton;
            var e=new PointerEventData(EventSystem.current){pointerId=9,button=PointerEventData.InputButton.Left,clickCount=1};
            button.OnPointerDown(e);
            var factory=new DelayedFactory();session.Initialize(factory,session.Base,session.Ground);yield return null;yield return null;
            Assert.That(session.Waiting,Is.True);Assert.That(View.ReleaseButton.interactable,Is.False);
            button.OnPointerClick(e);Assert.That(session.Waiting,Is.True);
            var task=new StoneFactory().CreateAsync(new Vector3(0,session.BaseTop+.28f,0),session.transform);
            factory.Supply.SetResult(task.Result);yield return null;yield return null;
            var stone=session.Pending;Assert.That(stone,Is.Not.Null);
            button.OnPointerClick(e);Assert.That(stone.Released,Is.False);
            Assert.That(View.ReleaseButton.interactable,Is.True);
        }
        [UnityTest] public IEnumerator LiftStopsAtSupportAndButtonContinuesStackingInPlayableScene()
        {
            session.Initialize(new StoneFactory(shapes:new StoneShapeSettings{FlatProbability=1},nextSeed:()=>123),session.Base,session.Ground);
            yield return null;yield return null;
            Stone support=null;
            for(int i=0;i<2;i++)
            {
                var stone=session.Pending;var pad=Pad(true);
                var resting=support==null?Vector3.zero:support.transform.position;
                pad.OnPointerDown(PadEvent(pad,Vector2.down));yield return new WaitForSeconds(.8f);
                pad.OnPointerUp(PadEvent(pad,Vector2.down));
                float top=support==null?session.BaseTop:support.Shape.bounds.max.y;
                Assert.That(stone.Shape.bounds.min.y,Is.GreaterThanOrEqualTo(top-.001f));
                if(support!=null)Assert.That(Vector3.Distance(support.transform.position,resting),Is.LessThan(.001f),"Preview controls cannot push the tower");
                yield return Tap(Centre(View.ReleaseButton));Assert.That(stone.Released,Is.True);
                Assert.That(session.Pending,Is.Not.SameAs(stone));
                yield return new WaitForSeconds(1.2f);support=stone;
                Assert.That(stone,Is.Not.Null);Assert.That(session.CurrentHeightMm,Is.GreaterThan(0));
            }
        }
        [UnityTest] public IEnumerator CancelingReleaseButtonTouchDoesNotDropStone()
        {
            touchscreen=InputSystem.AddDevice<Touchscreen>();touchscreen.MakeCurrent();
            var stone=session.Pending;var p=Centre(View.ReleaseButton);
            Touch(1,UnityEngine.InputSystem.TouchPhase.Began,p);yield return null;
            Touch(1,UnityEngine.InputSystem.TouchPhase.Canceled,p);yield return null;yield return null;
            Assert.That(session.Pending,Is.SameAs(stone));Assert.That(stone.Released,Is.False);
        }
        [UnityTest] public IEnumerator ShareRendersOnceAndRestartInvalidatesSnapshot()
        {
            var share=root.GetComponent<ShareController>();
            // Full-scene sharing is independent from the removed gameplay panorama.
            session.Pending.transform.position += Vector3.right * 3 + Vector3.up * 2;
            Physics.SyncTransforms();
            share.Capture();
            var camera=GameObject.Find("Share capture camera").GetComponent<Camera>();
            var bounds=session.Base.bounds;
            foreach(var stone in session.Stones)bounds.Encapsulate(stone.Surface.bounds);
            for(int i=0;i<8;i++)
            {
                var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                var point=camera.WorldToViewportPoint(corner);
                Assert.That(point.x,Is.InRange(.02f,.98f));Assert.That(point.y,Is.InRange(.02f,.98f));
                Assert.That(point.z,Is.GreaterThan(camera.nearClipPlane));Assert.That(point.z,Is.LessThan(camera.farClipPlane));
            }
            var captured=share.Snapshot;
            Assert.That(captured,Is.Not.Null);Assert.That(captured.HeightMm,Is.Zero);
            Assert.That(captured.Png.Length,Is.GreaterThan(1000));
            var decoded=new Texture2D(1,1);Assert.That(decoded.LoadImage(captured.Png),Is.True);
            Assert.That(decoded.width,Is.EqualTo(720));Assert.That(decoded.height,Is.EqualTo(900));
            var pixels=decoded.GetPixels32();bool varied=false;
            for(int i=1;i<pixels.Length;i+=300)if(!pixels[i].Equals(pixels[0])){varied=true;break;}
            Assert.That(varied,Is.True,"Scene rendering must contain geometry, not only a cleared background");Object.Destroy(decoded);
            share.Capture();Assert.That(share.Snapshot,Is.SameAs(captured));
            session.Restart();Assert.That(share.Snapshot,Is.Null);Assert.That(session.ShareBusy,Is.False);
            share.Capture();Assert.That(session.ShareBusy,Is.True);
            share.OnShareClosed(captured.Epoch.ToString());Assert.That(session.ShareBusy,Is.True,"An old callback cannot end a newer share");
            share.Cancel();Assert.That(session.CanShare,Is.True);
            yield return null;
        }
        [UnityTest] public IEnumerator SimplifiedInterfaceHasNoRemovedControlsOrLandingDirections()
        {
            foreach(var label in View.Root.GetComponentsInChildren<Text>(true))
            {
                Assert.That(label.text,Does.Not.Contain("查看全景"));
                Assert.That(label.text,Does.Not.Contain("ROCK / BALANCE"));
                Assert.That(label.text,Does.Not.Contain("一点耐心"));
                Assert.That(label.text,Does.Not.Contain("摇杆移动"));
                Assert.That(label.text,Does.Not.Contain("落点"));
            }
            Canvas.ForceUpdateCanvases();
            foreach(var label in View.Root.GetComponentsInChildren<Text>())
                if(label.text.Length>0)Assert.That(label.cachedTextGenerator.characterCountVisible,Is.GreaterThanOrEqualTo(label.text.Length),label.text+" must fit without clipping");
            Assert.That(View.Root.Find("Instructions"),Is.Null);
            Assert.That(View.Root.Find("Actions").childCount,Is.EqualTo(2));
            var feedback=View.Root.Find("Temporary feedback");
            Assert.That(feedback.gameObject.activeSelf,Is.False);
            var stone=session.Pending;var start=stone.transform.position;
            input.Adjusting=true;
            foreach(var offset in new[]{Vector3.right*10,Vector3.left*10,Vector3.up*10,Vector3.down*10,Vector3.forward*20})
            {
                stone.transform.position=start+offset;yield return null;
                Assert.That(View.Root.Find("Landing direction"),Is.Null);
                foreach(var label in View.Root.GetComponentsInChildren<Text>(true))
                    Assert.That(label.text,Does.Not.Contain("落点"));
            }
            stone.transform.position=start;input.Adjusting=false;
        }
        [UnityTest] public IEnumerator InvalidReleaseShowsTemporaryNonBlockingFeedbackAndKeepsStone()
        {
            double now=10;session.Clock=()=>now;
            var stone=session.Pending;var valid=stone.transform.position;
            stone.transform.position=session.Base.bounds.center;Physics.SyncTransforms();
            Assert.That(session.Release(stone),Is.False);yield return null;
            var feedback=View.Root.Find("Temporary feedback");
            Assert.That(feedback.gameObject.activeSelf,Is.True);
            Assert.That(feedback.GetComponentInChildren<Text>().text,Does.Contain("移开或抬高"));
            Assert.That(session.Pending,Is.SameAs(stone));Assert.That(stone.Released,Is.False);
            foreach(var graphic in feedback.GetComponentsInChildren<Graphic>())Assert.That(graphic.raycastTarget,Is.False);
            now+=2.1;yield return null;
            Assert.That(feedback.gameObject.activeSelf,Is.False,"Expired feedback leaves no permanent instruction bar");
            stone.transform.position=valid;Physics.SyncTransforms();
            yield return Tap(Centre(View.ReleaseButton));Assert.That(stone.Released,Is.True);
        }
        [UnityTest] public IEnumerator HeaderRestartCanCancelAndConfirmWhileShareStillFramesCompleteScene()
        {
            var initial=input.ViewCamera.transform.position;var initialRotation=input.ViewCamera.transform.rotation;
            var stone=session.Pending;
            Assert.That(session.Adjust(stone,stone.transform.position+Vector3.right*3+Vector3.up*2,stone.transform.rotation),Is.True);
            yield return Orbit(new Vector2(160,-100));yield return Scroll(400);
            var rotation=input.ViewCamera.transform.rotation;
            var share=root.GetComponent<ShareController>();
            yield return Tap(Centre(View.ShareButton));
            Assert.That(share.Snapshot,Is.Not.Null);
            // Capture is synchronous and its temporary camera is destroyed at frame end.
            Assert.That(share.Snapshot.Png.Length,Is.GreaterThan(1000));
            Assert.That(Quaternion.Angle(input.ViewCamera.transform.rotation,rotation),Is.LessThan(.01f));
            var back=View.Root.Find("Share preview").GetComponentInChildren<Button>();
            yield return Tap(Centre(back));Assert.That(share.Snapshot,Is.Null);Assert.That(input.Modal,Is.False);
            var restart=View.Root.Find("Actions").GetComponentInChildren<Button>();
            yield return Tap(Centre(restart));Assert.That(input.Modal,Is.True);
            var dialog=View.Root.Find("Restart confirmation/Dialog");
            var buttons=dialog.GetComponentsInChildren<Button>();
            yield return Tap(Centre(buttons[0]));Assert.That(session.Pending,Is.SameAs(stone));Assert.That(input.Modal,Is.False);
            yield return Tap(Centre(restart));yield return Tap(Centre(buttons[1]));
            Assert.That(session.Pending,Is.Not.SameAs(stone));Assert.That(session.Stones.Count,Is.EqualTo(1));
            Assert.That(input.Modal,Is.False);
            yield return null;
            Assert.That(Vector3.Distance(input.ViewCamera.transform.position,initial),Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(input.ViewCamera.transform.rotation,initialRotation),Is.LessThan(.001f));
        }
    }
}
