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
            Pointer(point,true);yield return null;Pointer(point+Vector2.right*45,true);yield return null;Pointer(point+Vector2.right*45,false);yield return null;
            Assert.That(session.Pending,Is.SameAs(pending));Assert.That(Quaternion.Angle(rotation,pending.transform.rotation),Is.GreaterThan(1));
            Assert.That(pending.transform.position,Is.EqualTo(position));
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
        sealed class DelayedFactory : IStoneFactory
        {
            public readonly System.Threading.Tasks.TaskCompletionSource<Stone> Supply = new System.Threading.Tasks.TaskCompletionSource<Stone>();
            public System.Threading.Tasks.Task<Stone> CreateAsync(Vector3 position,Transform parent) => Supply.Task;
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
            share.Capture();
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
        [UnityTest] public IEnumerator PanoramaFramesHighTowerAndAdjustmentLocksAutomaticFollow()
        {
            var stone=session.Pending;
            stone.transform.localScale=new Vector3(1,20,1);Physics.SyncTransforms();
            stone.transform.position+=Vector3.up*(session.BaseTop-stone.Shape.bounds.min.y+.001f);Physics.SyncTransforms();
            Assert.That(session.Release(stone),Is.True);stone.Body.constraints=RigidbodyConstraints.FreezeAll;
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();session.RefreshState();
            session.Pending.transform.position=new Vector3(0,stone.HighestPoint()+.25f,0);Physics.SyncTransforms();
            input.ViewCamera.aspect=430f/900f;input.Panorama();yield return null;
            var bottom=input.ViewCamera.WorldToViewportPoint(new Vector3(0,session.BaseTop,0));
            var top=input.ViewCamera.WorldToViewportPoint(session.Pending.transform.position);
            Assert.That(bottom.y,Is.GreaterThan(.25f));Assert.That(top.y,Is.LessThan(.79f));
            Assert.That(top.z,Is.GreaterThan(0));
            input.Adjusting=true;var position=input.ViewCamera.transform.position;
            session.Pending.transform.position+=Vector3.up*2;yield return null;yield return null;
            Assert.That(input.ViewCamera.transform.position,Is.EqualTo(position));
            input.Adjusting=false;session.Restart();yield return null;
            Assert.That(input.ViewCamera.transform.position.y,Is.LessThan(position.y));
        }
    }
}
