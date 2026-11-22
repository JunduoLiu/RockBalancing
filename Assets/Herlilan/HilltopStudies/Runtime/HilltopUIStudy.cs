#if HERLILAN_HILLTOP_STUDIES
using System.Collections;
using System.Collections.Generic;
using Herlilan.Hilltop;
using RockBalancing;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Herlilan.HilltopStudies
{
    [DefaultExecutionOrder(1500)]
    public sealed class HilltopUIStudy : MonoBehaviour
    {
        public const string EnableKey="Herlilan.HilltopStudies.Enabled";
        public int Direction {get;private set;}
        public HilltopScene Owner {get;private set;}
        public bool Ready {get;private set;}
        public bool MoveHeld {get;private set;}
        public bool LiftHeld {get;private set;}
        public bool MoveHover {get;private set;}
        public bool LiftHover {get;private set;}
        public float MoveReveal {get;private set;}
        public float LiftReveal {get;private set;}
        public float HeightVisibility {get;private set;}
        readonly Dictionary<Graphic,Color> colors=new Dictionary<Graphic,Color>();
        DragPad move,lift;
        StudyGlyph moveGlyph,liftGlyph;
        StudyHover moveHover,liftHover;
        float moveUntil,liftUntil,heightUntil;
        int lastHeight;
        bool lastStabilizing;
        int originalFont,originalMax;
        FontStyle originalStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
#if UNITY_EDITOR
            if(!UnityEditor.SessionState.GetBool(EnableKey,false))return;
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/Herlilan/Hilltop/Scenes/Hilltop.unity")return;
            var owner=Object.FindFirstObjectByType<HilltopScene>();
            if(owner!=null&&owner.GetComponent<HilltopUIStudy>()==null)owner.gameObject.AddComponent<HilltopUIStudy>();
#endif
        }
        IEnumerator Start()
        {
            Owner=GetComponent<HilltopScene>();
            while(Owner.View==null||Owner.Session==null)yield return null;
            yield return null;
            var controls=Owner.View.Root.Find("Adjustments");
            move=controls.Find("Move").GetComponent<DragPad>();lift=controls.Find("Lift").GetComponent<DragPad>();
            foreach(var g in controls.GetComponentsInChildren<Graphic>(true))colors[g]=g.color;
            var height=Owner.View.HeightLabel;originalFont=height.fontSize;originalMax=height.resizeTextMaxSize;originalStyle=height.fontStyle;
            moveGlyph=Glyph(move.transform,false);liftGlyph=Glyph(lift.transform,true);
            moveHover=move.gameObject.AddComponent<StudyHover>();liftHover=lift.gameObject.AddComponent<StudyHover>();
            moveHover.Owner=liftHover.Owner=this;liftHover.Vertical=true;
            move.Held+=OnMove;lift.Held+=OnLift;
            lastHeight=Owner.Session.CurrentHeightMm;lastStabilizing=Owner.Session.Stabilizing;
            gameObject.AddComponent<HilltopBackgroundStudy>().Initialize(Owner);
            Ready=true;Select(0);
        }
        StudyGlyph Glyph(Transform parent,bool vertical)
        {
            var go=new GameObject("Study glyph - visual only",typeof(RectTransform),typeof(CanvasRenderer),typeof(StudyGlyph));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var g=go.GetComponent<StudyGlyph>();g.raycastTarget=false;g.Vertical=vertical;return g;
        }
        public void Select(int direction)
        {
            if(!Ready)return;
            Direction=Mathf.Clamp(direction,0,2);
            moveGlyph.gameObject.SetActive(Direction!=0);liftGlyph.gameObject.SetActive(Direction!=0);
            if(Direction==0)Restore();
            // UI switches never cancel gameplay or change pad state.
            moveUntil=liftUntil=0;heightUntil=Time.unscaledTime+2.2f;
        }
        void OnMove(bool held){MoveHeld=held;moveUntil=Time.unscaledTime+1.1f;if(held)heightUntil=Time.unscaledTime+2.2f;}
        void OnLift(bool held){LiftHeld=held;liftUntil=Time.unscaledTime+1.1f;if(held)heightUntil=Time.unscaledTime+2.2f;}
        public void Hover(bool vertical,bool inside)
        {if(vertical){LiftHover=inside;if(inside)liftUntil=Time.unscaledTime+.8f;}else{MoveHover=inside;if(inside)moveUntil=Time.unscaledTime+.8f;}}
        public void ClearPresentationFocus()
        {MoveHover=LiftHover=false;MoveHeld=LiftHeld=false;moveUntil=liftUntil=0;}
        void OnApplicationFocus(bool focus){if(!focus)ClearPresentationFocus();}
        void Update()
        {
            if(!Ready)return;
            if(Owner.Input.Modal)ClearPresentationFocus();
            var k=Keyboard.current;
            if(Application.isFocused&&k!=null&&!k.ctrlKey.isPressed&&!k.altKey.isPressed&&!Owner.Input.Modal)
            {if(k.f6Key.wasPressedThisFrame)Select(0);if(k.f7Key.wasPressedThisFrame)Select(1);if(k.f8Key.wasPressedThisFrame)Select(2);
             if(k.f9Key.wasPressedThisFrame){var b=GetComponent<HilltopBackgroundStudy>();b.SetEnabled(!b.Active);}}
        }
        void LateUpdate()
        {
            if(!Ready)return;
            if(Owner.Session.CurrentHeightMm!=lastHeight||(lastStabilizing&&!Owner.Session.Stabilizing))heightUntil=Time.unscaledTime+2.2f;
            lastHeight=Owner.Session.CurrentHeightMm;lastStabilizing=Owner.Session.Stabilizing;
            if(Direction==0)return;
            foreach(var pair in colors)if(pair.Key!=null)pair.Key.color=new Color(pair.Value.r,pair.Value.g,pair.Value.b,0);
            float mt=(MoveHeld||MoveHover||Time.unscaledTime<moveUntil)?1:0;
            float lt=(LiftHeld||LiftHover||Time.unscaledTime<liftUntil)?1:0;
            MoveReveal=Mathf.MoveTowards(MoveReveal,mt,Time.unscaledDeltaTime*(mt>0?8:2));
            LiftReveal=Mathf.MoveTowards(LiftReveal,lt,Time.unscaledDeltaTime*(lt>0?8:2));
            moveGlyph.Set(Direction,MoveReveal,move.Handle.anchoredPosition,MoveHeld);
            liftGlyph.Set(Direction,LiftReveal,lift.Handle.anchoredPosition,LiftHeld);
            var label=Owner.View.HeightLabel;
            float target=Direction==2?.72f:(Time.unscaledTime<heightUntil||MoveHeld||LiftHeld? .88f:0);
            HeightVisibility=Mathf.MoveTowards(HeightVisibility,target,Time.unscaledDeltaTime*2);
            label.fontStyle=FontStyle.Normal;label.fontSize=label.resizeTextMaxSize=Direction==1?15:16;
            label.color=new Color(.21f,.31f,.23f,HeightVisibility);
        }
        void Restore()
        {
            foreach(var p in colors)if(p.Key!=null)p.Key.color=p.Value;
            if(Owner!=null&&Owner.View!=null){var h=Owner.View.HeightLabel;h.fontSize=originalFont;h.resizeTextMaxSize=originalMax;h.fontStyle=originalStyle;h.color=new Color(.21f,.31f,.23f,.9f);}
        }
        void OnDisable(){if(!Ready)return;Restore();moveGlyph.gameObject.SetActive(false);liftGlyph.gameObject.SetActive(false);ClearPresentationFocus();}
        void OnDestroy(){if(move!=null)move.Held-=OnMove;if(lift!=null)lift.Held-=OnLift;}
    }

    public sealed class StudyHover : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        public HilltopUIStudy Owner;public bool Vertical;
        public void OnPointerEnter(PointerEventData e){Owner.Hover(Vertical,true);}
        public void OnPointerExit(PointerEventData e){Owner.Hover(Vertical,false);}
        void OnDisable(){if(Owner!=null)Owner.Hover(Vertical,false);}
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StudyGlyph : MaskableGraphic
    {
        public bool Vertical;
        int direction;float reveal;Vector2 offset;bool held;
        public void Set(int d,float r,Vector2 p,bool h){direction=d;reveal=r;offset=p;held=h;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Color ink=new Color(.97f,.96f,.86f,direction==1?Mathf.Lerp(.68f,.96f,reveal):Mathf.Lerp(.78f,1,reveal));
            // Two-tone strokes, no filled control background; all graphics ignore raycasts.
            if(!Vertical)
            {
                if(direction==1)
                {
                    Diamond(vh,Vector2.zero,2.2f,ink);
                    for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;Vector2 u=new Vector2(Mathf.Cos(a),Mathf.Sin(a));float r=Mathf.Lerp(8,24,reveal);Line(vh,u*r,u*(r+Mathf.Lerp(3,7,reveal)),1,ink);}
                    if(reveal>.02f){Line(vh,Vector2.zero,offset,.8f,Fade(ink,reveal*.7f));Diamond(vh,offset,Mathf.Lerp(1,3,reveal),Fade(ink,reveal));}
                }
                else
                {
                    Line(vh,new Vector2(-21,0),new Vector2(-13,0),1,ink);Line(vh,new Vector2(13,0),new Vector2(21,0),1,ink);
                    Line(vh,new Vector2(0,-21),new Vector2(0,-13),1,ink);Line(vh,new Vector2(0,13),new Vector2(0,21),1,ink);
                    Diamond(vh,offset*.6f,3,ink);
                    if(held)Line(vh,Vector2.zero,offset*.6f,1,Fade(ink,.7f));
                }
            }
            else
            {
                if(direction==1)
                {
                    float span=Mathf.Lerp(8,30,reveal);
                    Chevron(vh,new Vector2(0,span),true,ink);Chevron(vh,new Vector2(0,-span),false,ink);
                    if(reveal>.02f){Line(vh,new Vector2(-4,offset.y),new Vector2(4,offset.y),1.2f,Fade(ink,reveal));Line(vh,new Vector2(0,-span+7),new Vector2(0,span-7),.65f,Fade(ink,reveal*.42f));}
                }
                else
                {
                    for(int n=-2;n<=2;n++)Line(vh,new Vector2(-2,n*14),new Vector2(n==0?5:2,n*14),.9f,Fade(ink,.75f));
                    Line(vh,new Vector2(-7,offset.y),new Vector2(7,offset.y),1.4f,ink);
                    Chevron(vh,new Vector2(0,39),true,Fade(ink,.75f));Chevron(vh,new Vector2(0,-39),false,Fade(ink,.75f));
                }
            }
        }
        static Color Fade(Color c,float a){c.a*=a;return c;}
        void Chevron(VertexHelper v,Vector2 p,bool up,Color c){float s=up?1:-1;Line(v,p+new Vector2(-3,-3*s),p,1,c);Line(v,p,p+new Vector2(3,-3*s),1,c);}
        void Diamond(VertexHelper v,Vector2 p,float r,Color c)
        {Quad(v,p+Vector2.left*(r+.7f),p+Vector2.up*(r+.7f),p+Vector2.right*(r+.7f),p+Vector2.down*(r+.7f),new Color(.09f,.15f,.12f,c.a*.7f));
         Quad(v,p+Vector2.left*r,p+Vector2.up*r,p+Vector2.right*r,p+Vector2.down*r,c);}
        void Line(VertexHelper v,Vector2 a,Vector2 b,float width,Color c)
        {
            if((b-a).sqrMagnitude<.01f)return;
            Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized;
            Quad(v,a-n*(width*.5f+.7f),a+n*(width*.5f+.7f),b+n*(width*.5f+.7f),b-n*(width*.5f+.7f),new Color(.09f,.15f,.12f,c.a*.65f));
            Quad(v,a-n*width*.5f,a+n*width*.5f,b+n*width*.5f,b-n*width*.5f,c);
        }
        static void Quad(VertexHelper v,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
        {int n=v.currentVertCount;v.AddVert(a,color,Vector2.zero);v.AddVert(b,color,Vector2.zero);v.AddVert(c,color,Vector2.zero);v.AddVert(d,color,Vector2.zero);v.AddTriangle(n,n+1,n+2);v.AddTriangle(n,n+2,n+3);}
    }
}



#endif

