using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

namespace RockBalancing
{
    public sealed class ShareController : MonoBehaviour
    {
        public GameSession Session;
        public GameView View;
        public GameInput Input;
        public ShareSnapshot Snapshot { get; private set; }
        GameObject preview;
        Texture2D image;
        int serial;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void RB_OpenShare(byte[] bytes,int length,int height,int epoch,string receiver);
        [DllImport("__Internal")] static extern void RB_CancelShare();
#endif
        public void Initialize()
        {
            View.ShareButton.gameObject.SetActive(true);
            View.ShareButton.onClick.AddListener(Capture);
            Session.Restarted += Cancel;
        }
        public void Capture()
        {
            if(!Session.BeginShare()) return;
            Input.Modal=true;Input.CancelGesture();
            int epoch=Session.Epoch;
            int request=++serial;
            try
            {
                // All measurement and rendering happens synchronously in this input turn.
                int height=Session.CurrentHeightMm;
                image=CaptureScene();
                Snapshot=new ShareSnapshot(epoch,height,image.EncodeToPNG());
                if(request!=serial || epoch!=Session.Epoch) { Cancel(); return; }
#if UNITY_WEBGL && !UNITY_EDITOR
                RB_OpenShare(Snapshot.Png,Snapshot.Png.Length,height,epoch,gameObject.name);
#else
                ShowPreview();
#endif
            }
            catch(Exception exception)
            {
                Debug.LogWarning("Share capture failed: "+exception.Message);
                Cancel();
            }
        }
        Texture2D CaptureScene()
        {
            var bounds=Session.Base.bounds;
            foreach(var stone in Session.Stones) if(stone!=null)bounds.Encapsulate(stone.Surface.bounds);
            var go=new GameObject("Share capture camera");
            var camera=go.AddComponent<Camera>();
            camera.CopyFrom(Input.ViewCamera);
            camera.enabled=false; camera.orthographic=true;
            camera.transform.rotation=Quaternion.Euler(18,25,0);
            var inverse=Quaternion.Inverse(camera.transform.rotation);
            float halfHeight=0,depth=0;
            const float aspect=720f/900f;
            for(int i=0;i<8;i++)
            {
                var point=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                var local=inverse*(point-bounds.center);
                halfHeight=Mathf.Max(halfHeight,Mathf.Abs(local.y),Mathf.Abs(local.x)/aspect);
                depth=Mathf.Max(depth,Mathf.Abs(local.z));
            }
            camera.orthographicSize=Mathf.Max(.65f,halfHeight*1.15f);
            camera.aspect=aspect;
            camera.transform.position=bounds.center-camera.transform.forward*(depth+5);
            camera.nearClipPlane=.01f;camera.farClipPlane=depth*2+15;
            var target=RenderTexture.GetTemporary(720,900,24,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=target;
                // A share always shows the complete, opaque scene, even from below.
                using (Input.Occlusion?.OpaqueScene()) camera.Render();
                RenderTexture.active=target;
                var texture=new Texture2D(720,900,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,720,900),0,0);texture.Apply();
                return texture;
            }
            finally
            {
                camera.targetTexture=null;RenderTexture.active=previous;
                RenderTexture.ReleaseTemporary(target);Destroy(go);
            }
        }
        void ShowPreview()
        {
            Input.Modal=true;Input.CancelGesture();
            preview=View.Panel(View.Root,"Share preview",Vector2.zero,Vector2.one,new Color(.96f,.95f,.91f,1),true).gameObject;
            View.Label(preview.transform,Snapshot.HeightMm+" mm",new Vector2(.06f,.85f),new Vector2(.94f,.97f),42);
            var rect=View.Panel(preview.transform,"Snapshot",new Vector2(.06f,.2f),new Vector2(.94f,.83f),Color.clear,false);
            var raw=rect.gameObject.AddComponent<RawImage>();raw.texture=image;
            var fit=rect.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=720f/900f;
            View.ButtonAt(preview.transform,"返回游戏",new Vector2(.1f,.07f),new Vector2(.9f,.15f),Cancel);
        }
        public void OnShareClosed(string epoch)
        {
            if(int.TryParse(epoch,out int value) && value==Session.Epoch) Cancel();
        }
        public void Cancel()
        {
            serial++;
#if UNITY_WEBGL && !UNITY_EDITOR
            RB_CancelShare();
#endif
            if(preview!=null)Destroy(preview);
            if(image!=null)Destroy(image);
            preview=null;image=null;Snapshot=null;
            Input.Modal=false;
            Session.EndShare(Session.Epoch);
        }
        void OnDestroy() { if(Session!=null)Session.Restarted-=Cancel; if(image!=null)Destroy(image); }
    }
    public sealed class ShareSnapshot
    {
        public int Epoch { get; }
        public int HeightMm { get; }
        public byte[] Png { get; }
        public ShareSnapshot(int epoch,int height,byte[] png) { Epoch=epoch;HeightMm=height;Png=png; }
    }
}
