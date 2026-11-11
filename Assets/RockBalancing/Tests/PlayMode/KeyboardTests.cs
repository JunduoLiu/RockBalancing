using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RockBalancing.Tests
{
    public sealed class KeyboardTests
    {
        GameObject root;
        GameSession session;
        GameInput input;
        Keyboard keyboard;
        Mouse mouse;
        float oldCapture;
        [UnitySetUp] public IEnumerator Setup()
        {
            oldCapture=Time.captureDeltaTime;
            keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
            mouse=InputSystem.AddDevice<Mouse>();mouse.MakeCurrent();
            root=new GameObject("Keyboard scene",typeof(GameBootstrap));
            yield return null;yield return null;
            session=root.GetComponentInChildren<GameSession>();input=root.GetComponent<GameInput>();
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            Time.captureDeltaTime=oldCapture;
            Object.Destroy(root);InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);
            yield return null;
        }
        void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        void Pointer(Vector2 p,bool down)=>InputSystem.QueueStateEvent(mouse,new MouseState{position=p}.WithButton(MouseButton.Left,down));
        IEnumerator Tap(Component control)
        {
            var rect=(RectTransform)control.transform;var p=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            Pointer(p,true);yield return null;Pointer(p,false);yield return null;yield return null;
        }
        IEnumerator Neutral(){Keys();yield return null;yield return null;}
        Vector3 Heading(Vector3 vector)=>Quaternion.Euler(0,(float)typeof(GameInput).GetField("yaw",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(input),0)*vector;
        GameView View=>root.GetComponent<GameView>();
        void PlaceHigh(){var stone=session.Pending;Assert.That(session.Adjust(stone,new Vector3(0,2,0),stone.transform.rotation),Is.True);}

        [UnityTest] public IEnumerator HeldKeyboardMovesPlanarAndHeightTogetherAndStopsOnRelease()
        {
            PlaceHigh();var stone=session.Pending;Keys(Key.W,Key.D,Key.UpArrow);yield return null;yield return null;
            var start=stone.transform.position;var camera=input.ViewCamera.transform.position;var rotation=input.ViewCamera.transform.rotation;
            yield return new WaitForSeconds(.2f);
            var delta=stone.transform.position-start;var horizontal=new Vector3(delta.x,0,delta.z);
            Assert.That(Vector3.Angle(horizontal,Heading(new Vector3(1,0,1))),Is.LessThan(.02f));
            Assert.That(horizontal.magnitude,Is.GreaterThan(.04f));
            Assert.That(delta.y/horizontal.magnitude,Is.EqualTo(input.MaximumHeightSpeed/input.MaximumPlanarSpeed).Within(.02f));
            Assert.That(input.ViewCamera.transform.position,Is.EqualTo(camera));Assert.That(input.ViewCamera.transform.rotation,Is.EqualTo(rotation));
            yield return Neutral();var stopped=stone.transform.position;yield return new WaitForSeconds(.1f);Assert.That(stone.transform.position,Is.EqualTo(stopped));
            Keys(Key.W,Key.S,Key.A,Key.D,Key.UpArrow,Key.DownArrow);yield return new WaitForSeconds(.1f);Assert.That(stone.transform.position,Is.EqualTo(stopped));
            yield return Neutral();Keys(Key.DownArrow);yield return new WaitForSeconds(.1f);
            Assert.That(stone.transform.position.x,Is.EqualTo(stopped.x));Assert.That(stone.transform.position.z,Is.EqualTo(stopped.z));Assert.That(stone.transform.position.y,Is.LessThan(stopped.y));
        }
        [UnityTest] public IEnumerator KeyboardDistanceIsFrameRateIndependentAndDiagonalCapped()
        {
            PlaceHigh();var stone=session.Pending;
            foreach(int fps in new[]{30,60,120})
            foreach(bool diagonal in new[]{false,true})
            {
                yield return Neutral();Time.captureDeltaTime=1f/fps;
                Keys(diagonal?new[]{Key.W,Key.D}:new[]{Key.D});yield return null;yield return null;
                var start=stone.transform.position;
                for(int i=0;i<fps/5;i++)yield return null;
                var delta=stone.transform.position-start;
                Assert.That(delta.magnitude,Is.EqualTo(input.MaximumPlanarSpeed*.2f).Within(.002f),$"{fps} fps, diagonal {diagonal}");
                Assert.That(delta.y,Is.Zero);
            }
        }
        [UnityTest] public IEnumerator KeyboardUsesYawAtBothPolesAndAfterOrbitWithoutChangingHeight()
        {
            PlaceHigh();var stone=session.Pending;
            foreach(float pitch in new[]{-90f,90f})
            foreach(float yaw in new[]{0f,90f,180f,275f})
            {
                yield return Neutral();input.MinimumPitch=input.MaximumPitch=pitch;
                typeof(GameInput).GetField("yaw",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(input,yaw);
                var start=stone.transform.position;Keys(Key.W);yield return new WaitForSeconds(.04f);
                var delta=stone.transform.position-start;
                Assert.That(Vector3.Angle(delta,Heading(Vector3.forward)),Is.LessThan(.02f));Assert.That(delta.y,Is.Zero);
            }
        }
        [UnityTest] public IEnumerator FocusPauseModalAndBrowserFocusRequireNeutralBeforeRestartingInput()
        {
            PlaceHigh();var stone=session.Pending;
            for(int mode=0;mode<4;mode++)
            {
                yield return Neutral();Keys(Key.D,Key.UpArrow);yield return new WaitForSeconds(.05f);
                if(mode==0)input.SendMessage("OnApplicationFocus",false);
                if(mode==1)input.SendMessage("OnApplicationPause",true);
                if(mode==2)input.Modal=true;
                if(mode==3)input.SetBrowserInputFocus("0");
                var stopped=stone.transform.position;yield return new WaitForSeconds(.04f);Assert.That(stone.transform.position,Is.EqualTo(stopped));
                if(mode==0)input.SendMessage("OnApplicationFocus",true);
                if(mode==1)input.SendMessage("OnApplicationPause",false);
                if(mode==2)input.Modal=false;
                if(mode==3){input.SetBrowserInputFocus("1");Keys(Key.D,Key.UpArrow);}
                yield return new WaitForSeconds(.05f);Assert.That(stone.transform.position,Is.EqualTo(stopped),$"mode {mode} cannot inherit held keys");
                yield return Neutral();Keys(Key.D);yield return new WaitForSeconds(.05f);Assert.That(Vector3.Distance(stone.transform.position,stopped),Is.GreaterThan(.005f));
            }
        }
        [UnityTest] public IEnumerator ReleaseDiscardAndRestartCannotTransferHeldKeysToNextStone()
        {
            for(int action=0;action<3;action++)
            {
                yield return Neutral();PlaceHigh();Keys(Key.W,Key.UpArrow);yield return new WaitForSeconds(.05f);
                var old=session.Pending;
                if(action==0)yield return Tap(View.ReleaseButton);
                if(action==1)yield return Tap(View.DiscardButton);
                if(action==2)session.Restart();
                yield return null;var next=session.Pending;Assert.That(next,Is.Not.Null);Assert.That(next,Is.Not.SameAs(old));
                var start=next.transform.position;yield return new WaitForSeconds(.1f);Assert.That(next.transform.position,Is.EqualTo(start));
                yield return Neutral();Keys(Key.D);yield return new WaitForSeconds(.05f);Assert.That(Vector3.Distance(next.transform.position,start),Is.GreaterThan(.005f));
                yield return Neutral();yield return new WaitForSeconds(.45f);
            }
        }
        [UnityTest] public IEnumerator KeyboardAndPadShareSpeedLimitAndReleasingOneSourceKeepsTheOther()
        {
            PlaceHigh();var stone=session.Pending;
            Time.captureDeltaTime=.02f;
            input.HoldPlanar(true);input.MoveStone(Vector2.right);Keys(Key.D);var start=stone.transform.position;
            for(int i=0;i<10;i++)yield return null;
            Assert.That(Vector3.Distance(start,stone.transform.position),Is.EqualTo(input.MaximumPlanarSpeed*.2f).Within(.002f));
            Keys();yield return null;start=stone.transform.position;yield return null;
            Assert.That(Vector3.Distance(start,stone.transform.position),Is.GreaterThan(.005f));
            input.HoldPlanar(false);yield return null;start=stone.transform.position;yield return null;Assert.That(stone.transform.position,Is.EqualTo(start));
        }
        [UnityTest] public IEnumerator KeyboardLiftUsesCollisionBoundaryAndCannotPushExistingTower()
        {
            session.Initialize(new StoneFactory(shapes:new StoneShapeSettings{FlatProbability=1},nextSeed:()=>123),session.Base,session.Ground);
            yield return Neutral();Stone support=null;
            for(int i=0;i<2;i++)
            {
                var stone=session.Pending;var resting=support==null?Vector3.zero:support.transform.position;
                Keys(Key.DownArrow);yield return new WaitForSeconds(.8f);yield return Neutral();
                float top=support==null?session.BaseTop:support.Shape.bounds.max.y;
                Assert.That(stone.Shape.bounds.min.y,Is.GreaterThanOrEqualTo(top-.001f));
                if(support!=null)Assert.That(Vector3.Distance(support.transform.position,resting),Is.LessThan(.001f));
                yield return Tap(View.ReleaseButton);Assert.That(stone.Released,Is.True);yield return new WaitForSeconds(1.2f);support=stone;
                Assert.That(session.CurrentHeightMm,Is.GreaterThan(0));
            }
        }
        [UnityTest] public IEnumerator BrowserShortcutModifiersCancelAnAlreadyHeldMoveUntilNeutral()
        {
            PlaceHigh();Keys(Key.W);yield return new WaitForSeconds(.05f);
            Keys(Key.W,Key.LeftCtrl);yield return null;yield return null;
            var stone=session.Pending;var stopped=stone.transform.position;
            yield return new WaitForSeconds(.05f);Assert.That(stone.transform.position,Is.EqualTo(stopped));
            Keys(Key.W);yield return new WaitForSeconds(.05f);Assert.That(stone.transform.position,Is.EqualTo(stopped));
            yield return Neutral();Keys(Key.W);yield return new WaitForSeconds(.05f);Assert.That(stone.transform.position,Is.Not.EqualTo(stopped));
        }
        sealed class DelayedFactory : IStoneFactory
        {
            public readonly System.Threading.Tasks.TaskCompletionSource<Stone> Supply=new System.Threading.Tasks.TaskCompletionSource<Stone>();
            public System.Threading.Tasks.Task<Stone> CreateAsync(Vector3 position,Transform parent)=>Supply.Task;
        }
        [UnityTest] public IEnumerator WaitingAndLateSupplyNeverInheritKeysPressedWithoutAPendingStone()
        {
            var factory=new DelayedFactory();session.Initialize(factory,session.Base,session.Ground);
            yield return Neutral();Keys(Key.W,Key.UpArrow);yield return new WaitForSeconds(.05f);
            Assert.That(input.CanAdjust,Is.False);Assert.That(session.Pending,Is.Null);
            var task=new StoneFactory(nextSeed:()=>123).CreateAsync(new Vector3(0,session.BaseTop+.28f,0),session.transform);
            factory.Supply.SetResult(task.Result);yield return null;var stone=session.Pending;var start=stone.transform.position;
            yield return new WaitForSeconds(.1f);Assert.That(stone.transform.position,Is.EqualTo(start));
            yield return Neutral();Keys(Key.D);yield return new WaitForSeconds(.05f);Assert.That(stone.transform.position,Is.Not.EqualTo(start));
        }
        [UnityTest] public IEnumerator MouseAndKeyboardCompleteAdjustmentRotationObservationReleaseAndShare()
        {
            var stone=session.Pending;
            Keys(Key.D,Key.UpArrow);yield return new WaitForSeconds(.12f);yield return Neutral();
            var rotation=stone.transform.rotation;var start=stone.transform.position;
            Vector2 p=input.ViewCamera.WorldToScreenPoint(start);
            Keys(Key.W);Pointer(p,true);yield return null;Pointer(p+new Vector2(32,18),true);yield return null;
            Pointer(p+new Vector2(32,18),false);yield return null;yield return Neutral();
            Assert.That(Quaternion.Angle(rotation,stone.transform.rotation),Is.GreaterThan(2));
            Assert.That(Vector3.Distance(start,stone.transform.position),Is.GreaterThan(.001f));Assert.That(stone.Released,Is.False);
            var blank=new Vector2(Screen.width*.05f,Screen.height*.55f);var cameraRotation=input.ViewCamera.transform.rotation;
            Pointer(blank,true);yield return null;Pointer(blank+Vector2.right*80,true);yield return null;Pointer(blank+Vector2.right*80,false);yield return null;
            Assert.That(Quaternion.Angle(cameraRotation,input.ViewCamera.transform.rotation),Is.GreaterThan(5));Assert.That(stone.Released,Is.False);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=blank,scroll=new Vector2(0,120)});yield return null;yield return null;
            yield return Tap(View.ReleaseButton);Assert.That(stone.Released,Is.True);Assert.That(session.Pending,Is.Not.SameAs(stone));
            yield return new WaitForSeconds(1.5f);
            Keys(Key.W,Key.UpArrow);yield return new WaitForSeconds(.06f);yield return Tap(View.ShareButton);
            Assert.That(input.Modal,Is.True);Assert.That(root.GetComponent<ShareController>().Snapshot,Is.Not.Null);
            var next=session.Pending;start=next.transform.position;yield return new WaitForSeconds(.05f);Assert.That(next.transform.position,Is.EqualTo(start));
            yield return Tap(View.Root.Find("Share preview").GetComponentInChildren<Button>());
            yield return new WaitForSeconds(.05f);Assert.That(next.transform.position,Is.EqualTo(start));
            yield return Neutral();yield return Tap(View.RestartButton);
            yield return Tap(View.Root.Find("Restart confirmation/Dialog").GetComponentsInChildren<Button>()[1]);
            Assert.That(session.Pending,Is.Not.SameAs(next));Assert.That(session.Stones.Count,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator KeyboardNeverNavigatesOrSubmitsReleaseAndDialogConsumesMovement()
        {
            Assert.That(EventSystem.current.sendNavigationEvents,Is.False);
            var stone=session.Pending;
            EventSystem.current.SetSelectedGameObject(View.ReleaseButton.gameObject);
            ((StoneActionButton)View.ReleaseButton).OnSubmit(new BaseEventData(EventSystem.current));
            Keys(Key.Enter,Key.Space);yield return null;yield return Neutral();Assert.That(session.Pending,Is.SameAs(stone));
            yield return Tap(View.RestartButton);Assert.That(input.Modal,Is.True);
            var start=stone.transform.position;Keys(Key.W,Key.DownArrow);yield return new WaitForSeconds(.08f);Assert.That(stone.transform.position,Is.EqualTo(start));
            var buttons=View.Root.Find("Restart confirmation/Dialog").GetComponentsInChildren<Button>();yield return Tap(buttons[0]);
            yield return new WaitForSeconds(.05f);Assert.That(stone.transform.position,Is.EqualTo(start));
            yield return Neutral();Keys(Key.D);yield return new WaitForSeconds(.05f);Assert.That(stone.transform.position,Is.Not.EqualTo(start));
        }
    }
}
