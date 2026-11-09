using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

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
        IEnumerator Tap(Vector2 p){Pointer(p,true);yield return null;Pointer(p,false);yield return null;}
        [UnityTest] public IEnumerator BlankTapReleasesButOrbitAndControlDragsDoNot()
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
            Assert.That(pending.Released,Is.True);Assert.That(session.Pending,Is.Not.SameAs(pending));
        }
        [UnityTest] public IEnumerator StoneSelectionAndRotationDoNotReleaseOnPointerUp()
        {
            var pending=session.Pending;Vector2 point=input.ViewCamera.WorldToScreenPoint(pending.transform.position);
            yield return Tap(point);Assert.That(session.Pending,Is.SameAs(pending));
            var rotation=pending.transform.rotation;
            Pointer(point,true);yield return null;Pointer(point+Vector2.right*45,true);yield return null;Pointer(point+Vector2.right*45,false);yield return null;
            Assert.That(session.Pending,Is.SameAs(pending));Assert.That(Quaternion.Angle(rotation,pending.transform.rotation),Is.GreaterThan(1));
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
            yield return Tap(Blank);Assert.That(pending.Released,Is.True);
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
            Assert.That(pending.Released,Is.True,"A fresh tap after cancellation remains usable");
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
