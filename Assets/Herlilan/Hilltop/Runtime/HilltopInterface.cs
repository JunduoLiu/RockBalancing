using RockBalancing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Herlilan.Hilltop
{
    // Scene-local presentation. No shared UI, input or gameplay source is modified.
    [DefaultExecutionOrder(500)]
    public sealed class HilltopInterface : MonoBehaviour
    {
        HilltopScene scene;
        RectTransform controls,move,lift;
        GameObject actions;
        Sprite disc,pill;
        Texture2D discTexture,pillTexture;
        Renderer platformRenderer;
        Material platformMaterial;
        bool minimal;
        public void Initialize(HilltopScene value)
        {
            scene=value;minimal=scene.MinimalControls;
            platformRenderer=scene.Platform.GetComponent<Renderer>();platformMaterial=platformRenderer.sharedMaterial;
            disc=Shape(false,out discTexture);pill=Shape(true,out pillTexture);
            var root=scene.View.Root;
            controls=(RectTransform)root.Find("Adjustments");
            move=(RectTransform)controls.Find("Move");lift=(RectTransform)controls.Find("Lift");
            actions=root.Find("Actions").gameObject;
            foreach(var label in controls.GetComponentsInChildren<Text>())label.gameObject.SetActive(false);
            Round(move,disc,new Color(.96f,.96f,.86f,.25f));
            Round(lift,pill,new Color(.96f,.96f,.86f,.29f));
            Round(move.GetComponent<DragPad>().Handle,disc,new Color(.98f,.98f,.88f,.86f));
            Round(lift.GetComponent<DragPad>().Handle,disc,new Color(.98f,.98f,.88f,.88f));
            move.GetComponent<DragPad>().Handle.sizeDelta=Vector2.one*30;
            lift.GetComponent<DragPad>().Handle.sizeDelta=Vector2.one*23;
            foreach(var button in new[]{scene.View.ReleaseButton,scene.View.DiscardButton,scene.View.RestartButton,scene.View.ShareButton})
                Round((RectTransform)button.transform,disc,new Color(.97f,.97f,.88f,.8f));
            var height=scene.View.HeightLabel;
            height.color=new Color(.21f,.31f,.23f,.9f);height.alignment=TextAnchor.MiddleCenter;
            height.fontStyle=FontStyle.Normal;
            Label(lift,"+",new Vector2(.5f,.87f));Label(lift,"−",new Vector2(.5f,.13f));
        }

        void Label(Transform parent,string text,Vector2 anchor)
        {
            var go=new GameObject("Lift "+text,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=anchor;rect.sizeDelta=new Vector2(30,24);
            var label=go.GetComponent<Text>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.text=text;label.fontSize=17;
            label.alignment=TextAnchor.MiddleCenter;label.color=new Color(.96f,.97f,.87f,.92f);label.raycastTarget=false;
        }

        void Update()
        {
            if(scene==null || scene.Session==null)return;
            var keys=Keyboard.current;
            if(keys==null || !Application.isFocused || keys.ctrlKey.isPressed || keys.altKey.isPressed)return;
            if(keys.tabKey.wasPressedThisFrame)minimal=!minimal;
            // Visual-preview shortcuts call the existing protected action entry point.
            if(keys.spaceKey.wasPressedThisFrame)scene.Input.ActOnStone(scene.Session.Pending,false);
            if(keys.backspaceKey.wasPressedThisFrame)scene.Input.ActOnStone(scene.Session.Pending,true);
            if(keys.rKey.wasPressedThisFrame && !scene.Input.Modal)scene.View.RestartButton.onClick.Invoke();
        }

        void LateUpdate()
        {
            if(scene==null || scene.View==null || controls==null)return;
            // Reframe below the pending stone so the planted tower and summit
            // remain in view. The shared input resets the camera each frame.
            if(scene.Input.enabled)scene.ViewCamera.transform.position+=Vector3.down*.12f;
            float width=scene.View.Root.rect.width;
            float height=scene.View.Root.rect.height;
            controls.anchorMin=controls.anchorMax=Vector2.zero;controls.pivot=Vector2.zero;
            controls.anchoredPosition=new Vector2(28,Mathf.Max(30,height*.065f));controls.sizeDelta=new Vector2(194,114);
            Place(move,new Vector2(0,0),new Vector2(114,114));
            Place(lift,new Vector2(137,0),new Vector2(45,114));
            actions.SetActive(!minimal);
            scene.View.ReleaseButton.gameObject.SetActive(!minimal);
            scene.View.DiscardButton.gameObject.SetActive(!minimal);
            var heightRect=(RectTransform)scene.View.HeightLabel.transform;
            heightRect.anchorMin=heightRect.anchorMax=new Vector2(.5f,1);heightRect.pivot=new Vector2(.5f,1);
            heightRect.anchoredPosition=new Vector2(0,-Mathf.Max(22,height*.035f));heightRect.sizeDelta=new Vector2(190,40);
            scene.View.HeightLabel.fontSize=scene.View.HeightLabel.resizeTextMaxSize=scene.Session.Stabilizing?18:24;
            // Grass follows the shared ground occlusion state when viewing from below.
            bool visible=scene.Input.Occlusion==null || !scene.Input.Occlusion.IsFaded(scene.Meadow);
            if(scene.Grass!=null && scene.Grass.gameObject.activeSelf!=visible)scene.Grass.gameObject.SetActive(visible);
            // The shared AABB test includes the bottom corners of a resting pebble.
            // Keep the summit visible from above; retain its normal fade from below.
            if(scene.ViewCamera.transform.position.y>scene.Platform.bounds.max.y+.12f && platformRenderer.sharedMaterial!=platformMaterial)
                platformRenderer.sharedMaterial=platformMaterial;
        }

        static void Place(RectTransform rect,Vector2 position,Vector2 size)
        {rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=Vector2.zero;rect.anchoredPosition=position;rect.sizeDelta=size;}
        static void Round(RectTransform rect,Sprite sprite,Color color)
        {var image=rect.GetComponent<Image>();image.sprite=sprite;image.type=Image.Type.Simple;image.color=color;image.preserveAspect=false;}
        static Sprite Shape(bool capsule,out Texture2D texture)
        {
            int w=96,h=capsule?240:96;texture=new Texture2D(w,h,TextureFormat.RGBA32,false){name="Hilltop UI shape",filterMode=FilterMode.Bilinear};
            var pixels=new Color[w*h];float radius=w*.5f-1;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                float dx=x+.5f-w*.5f,dy=y+.5f-h*.5f;
                dy=Mathf.Max(0,Mathf.Abs(dy)-(h-w)*.5f);
                float distance=Mathf.Sqrt(dx*dx+dy*dy);
                pixels[y*w+x]=new Color(1,1,1,Mathf.Clamp01(radius-distance));
            }
            texture.SetPixels(pixels);texture.Apply();return Sprite.Create(texture,new Rect(0,0,w,h),Vector2.one*.5f,100);
        }
        void OnDestroy()
        {if(disc!=null)Destroy(disc);if(pill!=null)Destroy(pill);if(discTexture!=null)Destroy(discTexture);if(pillTexture!=null)Destroy(pillTexture);}
    }
}
