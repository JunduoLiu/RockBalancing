using System.Collections;
using System.Collections.Generic;
using Herlilan.Hilltop;
using RockBalancing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Herlilan.ZenTriptych
{
    // Only the three isolated scene copies opt in; shared gameplay remains untouched.
    [DefaultExecutionOrder(1200)]
    public sealed class ZenPresentation : MonoBehaviour
    {
        public int Mode { get; private set; }
        public int RippleCount { get; private set; }
        public HilltopScene Owner { get; private set; }
        readonly Dictionary<Renderer,Material> originals=new Dictionary<Renderer,Material>();
        readonly Dictionary<Stone,Material> stones=new Dictionary<Stone,Material>();
        readonly HashSet<Stone> settled=new HashSet<Stone>();
        readonly List<Object> owned=new List<Object>();
        GameObject paper,arcade;
        Transform far;
        Renderer meadow,platform;
        Material rock,baseMat;
        Light sun;
        Color sky,equator,ground; Quaternion sunRotation; Color sunColor; float intensity,shadow;
        bool fog,volumeEnabled; Volume volume;
        CameraClearFlags clear; Color background;
        LineRenderer ripple; float rippleAge=10;
        Sprite ring,dot;
        RectTransform move,lift; bool moveHeld,liftHeld;
        readonly List<Outline> controlEdges=new List<Outline>();
        bool ready;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string name=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if(!name.StartsWith("Zen-"))return;
            var owner=Object.FindFirstObjectByType<HilltopScene>();
            if(owner!=null)owner.gameObject.AddComponent<ZenPresentation>();
        }
        IEnumerator Start()
        {
            Owner=GetComponent<HilltopScene>();
            yield return null;
            while(Owner.Session==null)yield return null;
            Owner.GetComponent<HilltopLighting>().enabled=false;
            sun=RenderSettings.sun; sunRotation=sun.transform.rotation; sunColor=sun.color; intensity=sun.intensity;shadow=sun.shadowStrength;
            sky=RenderSettings.ambientSkyColor;equator=RenderSettings.ambientEquatorColor;ground=RenderSettings.ambientGroundColor;fog=RenderSettings.fog;
            clear=Owner.ViewCamera.clearFlags;background=Owner.ViewCamera.backgroundColor;
            volume=Owner.GetComponentInChildren<Volume>();volumeEnabled=volume.enabled;volume.enabled=false;
            far=transform.Find("Distant soft hills");meadow=Owner.Meadow.GetComponent<Renderer>();platform=Owner.Platform.GetComponent<Renderer>();
            rock=Own(new Material(Shader.Find("Herlilan/ZenTriptych/Stone")));
            baseMat=Own(new Material(Shader.Find("Universal Render Pipeline/Lit")));
            foreach(var r in GetComponentsInChildren<Renderer>()) originals[r]=r.sharedMaterial;
            foreach(var r in Owner.Grass.GetComponentsInChildren<Renderer>())
            {
                var m=Own(new Material(r.sharedMaterial));m.SetFloat("_Wind",.035f);m.SetFloat("_Speed",.65f);r.sharedMaterial=m;
            }
            paper=Base("Shallow sand disc",PrimitiveType.Cylinder,new Vector3(.56f,.075f,.52f),new Vector3(0,.005f,0));
            arcade=Base("Floating angular plinth",PrimitiveType.Cylinder,new Vector3(.56f,.075f,.52f),new Vector3(0,.005f,0));
            // The slab's top stays at the original platform's .08m; these are render-only.
            var mf=arcade.GetComponent<MeshFilter>(); var mesh=Own(new Mesh());
            var v=new List<Vector3>();var t=new List<int>();
            for(int y=0;y<2;y++)for(int i=0;i<8;i++){float a=i*Mathf.PI/4;v.Add(new Vector3(Mathf.Cos(a)*.5f,y==0?-1:1,Mathf.Sin(a)*.5f));}
            for(int i=1;i<7;i++){t.Add(8);t.Add(8+i+1);t.Add(8+i);}
            for(int i=0;i<8;i++){int j=(i+1)%8;t.Add(i);t.Add(j);t.Add(i+8);t.Add(j);t.Add(j+8);t.Add(i+8);}
            mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mf.sharedMesh=mesh;
            ripple=new GameObject("Settled wave (visual only)").AddComponent<LineRenderer>();ripple.transform.SetParent(transform,false);
            ripple.loop=true;ripple.positionCount=65;ripple.widthMultiplier=.004f;ripple.material=Own(new Material(Shader.Find("Universal Render Pipeline/Unlit")));
            ripple.sharedMaterial.color=new Color(.25f,.88f,1);ripple.enabled=false;
            BuildUI();ready=true;
            string name=gameObject.scene.name;Select(name.Contains("Paper")?1:name.Contains("Arcade")?2:0);
        }
        T Own<T>(T value) where T:Object {owned.Add(value);return value;}
        GameObject Base(string name,PrimitiveType type,Vector3 scale,Vector3 position)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(transform,false);go.transform.localScale=scale;go.transform.localPosition=position;
            var c=go.GetComponent<Collider>();c.enabled=false;Destroy(c);
            go.GetComponent<Renderer>().sharedMaterial=baseMat;return go;
        }
        public void Select(int mode)
        {
            if(!ready)return;Mode=Mathf.Clamp(mode,0,2);
            paper.SetActive(Mode==1);arcade.SetActive(Mode==2);
            Owner.ViewCamera.clearFlags=Mode==0?clear:CameraClearFlags.SolidColor;
            Owner.ViewCamera.backgroundColor=Mode==1?new Color(.94f,.925f,.89f):new Color(.009f,.018f,.038f);
            RenderSettings.fog=Mode==0&&fog;
            RenderSettings.ambientSkyColor=Mode==0?new Color(.61f,.72f,.85f):Mode==1?new Color(.78f,.77f,.74f):new Color(.29f,.36f,.46f);
            RenderSettings.ambientEquatorColor=Mode==2?new Color(.19f,.24f,.32f):new Color(.56f,.58f,.58f);
            RenderSettings.ambientGroundColor=Mode==2?new Color(.09f,.12f,.18f):new Color(.43f,.46f,.47f);
            sun.transform.rotation=Quaternion.Euler(Mode==0?23:Mode==1?68:32,Mode==2?145:-45,0);
            sun.color=Mode==0?new Color(1,.83f,.62f):Color.white;sun.intensity=Mode==2?1.8f:1.25f;sun.shadowStrength=Mode==0?.55f:.65f;
            rock.SetColor("_BaseColor",Mode==0?new Color(.62f,.59f,.53f):Mode==1?new Color(.22f,.23f,.235f):new Color(.18f,.23f,.30f));
            rock.SetFloat("_Rim",Mode==2?1:0);
            baseMat.color=Mode==1?new Color(.76f,.71f,.60f):new Color(.11f,.16f,.23f);
            baseMat.SetFloat("_Smoothness",Mode==2?.25f:0);
            ripple.enabled=false;rippleAge=10;
            ApplyVisibility();
        }
        void ApplyVisibility()
        {
            if(far!=null)far.gameObject.SetActive(Mode==0);
            meadow.enabled=Mode==0;platform.enabled=Mode==0;
            Owner.Grass.gameObject.SetActive(Mode==0&&(Owner.Input.Occlusion==null||!Owner.Input.Occlusion.IsFaded(Owner.Meadow)));
        }
        void Update()
        {
            if(!ready)return;
            var keys=Keyboard.current;
            if(Application.isFocused&&keys!=null&&!Owner.Input.Modal&&!keys.ctrlKey.isPressed&&!keys.altKey.isPressed)
            {if(keys.f6Key.wasPressedThisFrame)Select(0);if(keys.f7Key.wasPressedThisFrame)Select(1);if(keys.f8Key.wasPressedThisFrame)Select(2);}
            foreach(var s in Owner.Session.GetComponentsInChildren<Stone>())
            {
                if(!stones.ContainsKey(s)){stones[s]=s.Surface.sharedMaterial;s.Surface.sharedMaterial=rock;}
                // One wave on first quiet contact, never a force or collision change.
                if(s.Released&&!s.Discarded&&!settled.Contains(s)&&s.Contacts.Count>0&&s.Body.linearVelocity.sqrMagnitude<.0004f&&s.Body.angularVelocity.sqrMagnitude<.01f)
                {settled.Add(s);if(Mode==2){rippleAge=0;RippleCount++;ripple.transform.position=new Vector3(s.transform.position.x,.083f,s.transform.position.z);}}
            }
            rippleAge+=Time.deltaTime;ripple.enabled=Mode==2&&rippleAge<.65f;
            if(ripple.enabled)for(int i=0;i<65;i++){float a=i*Mathf.PI*2/65;float r=.04f+rippleAge*.5f;ripple.SetPosition(i,ripple.transform.position+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r));}
            ripple.widthMultiplier=.004f*Mathf.Clamp01(1-rippleAge/.65f);
        }
        void BuildUI()
        {
            var controls=Owner.View.Root.Find("Adjustments");move=(RectTransform)controls.Find("Move");lift=(RectTransform)controls.Find("Lift");
            ring=MakeSprite(true);dot=MakeSprite(false);
            move.GetComponent<Image>().sprite=ring;
            lift.GetComponent<Image>().color=Color.clear;
            foreach(var text in lift.GetComponentsInChildren<Text>())text.enabled=false;
            var mp=move.GetComponent<DragPad>();var lp=lift.GetComponent<DragPad>();
            mp.Handle.GetComponent<Image>().sprite=dot;lp.Handle.GetComponent<Image>().sprite=dot;
            mp.Handle.sizeDelta=Vector2.one*9;lp.Handle.sizeDelta=new Vector2(13,3);
            mp.Held+=MoveHeld;lp.Held+=LiftHeld;
            Line(lift,new Vector2(0,0),new Vector2(1,78));
            for(int i=-2;i<=2;i++)Line(lift,new Vector2(0,i*19),new Vector2(i==0?13:7,1));
            // Narrow dual-tone strokes stay readable over moving grass; no background or new raycast target.
            foreach(var graphic in move.GetComponentsInChildren<Image>())AddControlEdge(graphic);
            foreach(var graphic in lift.GetComponentsInChildren<Image>())if(graphic.transform!=lift)AddControlEdge(graphic);
            foreach(var b in new[]{Owner.View.ReleaseButton,Owner.View.DiscardButton,Owner.View.RestartButton,Owner.View.ShareButton})b.GetComponent<Image>().color=Color.clear;
        }
        void AddControlEdge(Image graphic)
        {
            var edge=graphic.gameObject.AddComponent<Outline>();
            edge.effectColor=new Color(.09f,.14f,.11f,.85f);
            edge.effectDistance=new Vector2(.75f,-.75f);edge.useGraphicAlpha=true;
            controlEdges.Add(edge);
        }
        void MoveHeld(bool value){moveHeld=value;}void LiftHeld(bool value){liftHeld=value;}
        void Line(Transform parent,Vector2 p,Vector2 size)
        {
            var go=new GameObject("Zen ink",typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var r=(RectTransform)go.transform;r.sizeDelta=size;r.anchoredPosition=p;go.GetComponent<Image>().raycastTarget=false;
        }
        Sprite MakeSprite(bool hollow)
        {
            var tx=Own(new Texture2D(128,128,TextureFormat.RGBA32,false));var pixels=new Color[128*128];
            for(int y=0;y<128;y++)for(int x=0;x<128;x++){float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(64,64));float a=hollow?Mathf.Clamp01(1.8f-Mathf.Abs(d-61)):Mathf.Clamp01(61-d);pixels[y*128+x]=new Color(1,1,1,a);}
            tx.SetPixels(pixels);tx.Apply();return Own(Sprite.Create(tx,new Rect(0,0,128,128),Vector2.one*.5f));
        }
        void LateUpdate()
        {
            if(!ready)return;ApplyVisibility();
            Color ink=Mode==2?new Color(.81f,.88f,.91f):new Color(.18f,.23f,.22f);
            Color controlInk=Mode==0?new Color(.97f,.97f,.88f):ink;
            foreach(var edge in controlEdges)edge.enabled=Mode==0;
            var c=controlInk;c.a=moveHeld?1:(Mode==0?.84f:.48f);move.GetComponent<Image>().color=c;
            c.a=liftHeld?1:(Mode==0?.84f:.48f);foreach(var i in lift.GetComponentsInChildren<Image>())if(i.transform!=lift)i.color=c;
            c=controlInk;c.a=moveHeld?1:(Mode==0?.95f:.75f);move.GetComponent<DragPad>().Handle.GetComponent<Image>().color=c;
            Owner.View.HeightLabel.color=ink;Owner.View.HeightLabel.fontStyle=FontStyle.Normal;Owner.View.HeightLabel.fontSize=Owner.View.HeightLabel.resizeTextMaxSize=18;
        }
        void OnDestroy()
        {
            if(!ready)return;
            foreach(var pair in stones)if(pair.Key!=null)pair.Key.Surface.sharedMaterial=pair.Value;
            foreach(var pair in originals)if(pair.Key!=null)pair.Key.sharedMaterial=pair.Value;
            RenderSettings.ambientSkyColor=sky;RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;RenderSettings.fog=fog;
            if(sun!=null){sun.transform.rotation=sunRotation;sun.color=sunColor;sun.intensity=intensity;sun.shadowStrength=shadow;}
            if(volume!=null)volume.enabled=volumeEnabled;
            foreach(var o in owned)if(o!=null)Destroy(o);
        }
    }
}



