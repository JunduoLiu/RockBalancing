#if HERLILAN_HILLTOP_STUDIES
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Herlilan.Hilltop;
using RockBalancing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Herlilan.HilltopStudies.Editor
{
    [InitializeOnLoad]
    public static class HilltopStudyReview
    {
        const string Root="Assets/Herlilan/HilltopStudies",Scene="Assets/Herlilan/Hilltop/Scenes/Hilltop.unity",Key="HilltopStudies.Review";
        static IEnumerator job;static double next,deadline;
        static readonly List<string> checks=new List<string>(),errors=new List<string>();
        static readonly Vector3 CameraPosition=new Vector3(-1.55f,1.45f,-3.3f);
        static readonly Quaternion CameraRotation=Quaternion.Euler(16,25,0);
        static HilltopStudyReview()
        {
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;
            Application.logMessageReceived+=(m,t,k)=>{if(SessionState.GetBool(Key,false)&&(k==LogType.Error||k==LogType.Exception))errors.Add(m+"\n"+t);};
        }
        [MenuItem("Herlilan/Hilltop Studies/Enable experiments (original HUD first)")]
        public static void Enable(){SessionState.SetBool(HilltopUIStudy.EnableKey,true);Debug.Log("Hilltop studies enabled for the original Hilltop scene. Play, then F6 original / F7 A1 / F8 A2 / F9 background.");}
        [MenuItem("Herlilan/Hilltop Studies/Disable experiments for next Play")]
        public static void Disable(){SessionState.SetBool(HilltopUIStudy.EnableKey,false);}
        [MenuItem("Herlilan/Hilltop Studies/UI 0 - Original")]
        static void Original(){Object.FindFirstObjectByType<HilltopUIStudy>()?.Select(0);}
        [MenuItem("Herlilan/Hilltop Studies/UI A1 - Quiet marks")]
        static void Quiet(){Object.FindFirstObjectByType<HilltopUIStudy>()?.Select(1);}
        [MenuItem("Herlilan/Hilltop Studies/UI A2 - Sparse ticks")]
        static void Ticks(){Object.FindFirstObjectByType<HilltopUIStudy>()?.Select(2);}
        [MenuItem("Herlilan/Hilltop Studies/Toggle background study")]
        static void Background(){var b=Object.FindFirstObjectByType<HilltopBackgroundStudy>();if(b!=null)b.SetEnabled(!b.Active);}
        [MenuItem("Herlilan/Hilltop Studies/Capture original Hilltop comparisons")]
        public static void Run()
        {
            if(EditorApplication.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Review requires an idle editor with a clean scene.");
            SessionState.SetString(Key+".previous",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            SessionState.SetBool(Key+".enabledBefore",SessionState.GetBool(HilltopUIStudy.EnableKey,false));
            SessionState.SetBool(HilltopUIStudy.EnableKey,true);
            EditorSceneManager.OpenScene(Scene);
            SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){checks.Clear();errors.Clear();job=Review();next=EditorApplication.timeSinceStartup+3;deadline=next+150;}
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                job=null;SessionState.SetBool(Key,false);
                SessionState.SetBool(HilltopUIStudy.EnableKey,SessionState.GetBool(Key+".enabledBefore",false));
                Directory.CreateDirectory(Root+"/Review");
                File.WriteAllText(Root+"/Review/validation.md","# Hilltop original-scene UI and background review\n\nActual Unity Play Mode; original Hilltop.unity. UI samples: background OFF, lighting preset 0, same camera and stone group.\n\n"+
                    string.Join("\n",checks)+"\n\nErrors: "+errors.Count+"\n\n"+string.Join("\n\n",errors)+
                    "\n\nControls are driven by scripted PointerEventData through the existing DragPad handlers. No OS keyboard/mouse or physical touch acceptance. Focus test invokes the existing cancel entry point plus presentation focus clearing, not an OS focus event.\n");
                var previous=SessionState.GetString(Key+".previous","");if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);
                AssetDatabase.Refresh();Debug.Log("HILLTOP_STUDIES_REVIEW "+(errors.Count==0?"PASS":"FAIL"));
                if(Application.isBatchMode)EditorApplication.Exit(errors.Count==0?0:1);
            }
        }
        static void Tick()
        {
            if(job==null||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Study review exceeded 150 seconds.");
                if(!job.MoveNext()){job=null;EditorApplication.isPlaying=false;return;}
                next=EditorApplication.timeSinceStartup+Convert.ToDouble(job.Current);
            }catch(Exception e){errors.Add(e.ToString());job=null;EditorApplication.isPlaying=false;}
        }
        static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);checks.Add("- PASS: "+message);}
        static PointerEventData Event(DragPad pad,Vector2 offset,int id)
        {
            var rect=(RectTransform)pad.transform;var canvas=pad.GetComponentInParent<Canvas>();
            return new PointerEventData(EventSystem.current){pointerId=id,position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center+offset)),button=PointerEventData.InputButton.Left};
        }
        static IEnumerator Review()
        {
            var study=Object.FindFirstObjectByType<HilltopUIStudy>();Check(study!=null&&study.Ready,"Opt-in study attached to original Hilltop scene");
            var s=study.Owner;var lighting=s.GetComponent<HilltopLighting>();var bg=s.GetComponent<HilltopBackgroundStudy>();
            Check(s.gameObject.scene.path==Scene,"Scene path is original Hilltop.unity, not ZenTriptych or a replacement scene");
            Check(!Array.Exists(Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None), b => b.GetType().FullName=="Herlilan.ZenTriptych.ZenPresentation"),"ZenTriptych presentation absent");
            lighting.AutoCycle=false;lighting.Select(0,true);bg.SetEnabled(false);
            for(int i=0;i<3;i++)
            {
                var p=s.Session.Pending;Check(p!=null,"Pending stone "+i);
                Check(s.Session.Adjust(p,new Vector3(0,s.Session.TowerTop()+p.Shape.bounds.extents.y+.016f,0),Quaternion.identity)&&s.Session.Release(p),"Shared placement/release "+i);
                yield return 1.7f;
            }
            Check(s.Session.CurrentHeightMm>0,"Current effective tower height uses original support chain");
            var baseline=WorldSignature(s);
            var move=s.View.Root.Find("Adjustments/Move").GetComponent<DragPad>();
            var lift=s.View.Root.Find("Adjustments/Lift").GetComponent<DragPad>();
            var moveSize=((RectTransform)move.transform).sizeDelta;var liftSize=((RectTransform)lift.transform).sizeDelta;
            var moveCanBegin=move.CanBegin;var liftCanBegin=lift.CanBegin;var moveChanged=move.Changed;var liftChanged=lift.Changed;
            var pending=s.Session.Pending;var pendingPosition=pending.transform.position;var pendingRotation=pending.transform.rotation;
            for(int mode=0;mode<3;mode++)
            {
                study.Select(mode);s.Input.CancelForFocusLoss();study.ClearPresentationFocus();
                yield return mode==1?3.6f:.7f;
                Check(!bg.Active&&WorldSignature(s)==baseline,"UI "+mode+" leaves environment, light, materials and colliders unchanged");
                Check(((RectTransform)move.transform).sizeDelta==moveSize&&((RectTransform)lift.transform).sizeDelta==liftSize,"UI "+mode+" retains both original hot areas");
                Check(move.CanBegin==moveCanBegin&&lift.CanBegin==liftCanBegin&&move.Changed==moveChanged&&lift.Changed==liftChanged,"UI "+mode+" retains existing input delegates");
                if(mode==1)Check(study.MoveReveal<.01f&&study.LiftReveal<.01f&&study.HeightVisibility<.01f,"A1 returns to quiet marks and hides unchanged height");
                Capture(s,"ui-"+mode+"-idle");
                // Hover discovery uses only the additional presentation handler.
                move.GetComponent<StudyHover>().OnPointerEnter(Event(move,Vector2.zero,-71));
                lift.GetComponent<StudyHover>().OnPointerEnter(Event(lift,Vector2.zero,-72));
                yield return .25f;
                if(mode==1)Check(study.MoveReveal>.9f&&study.LiftReveal>.9f,"A1 pointer hover reveals controls");
                move.GetComponent<StudyHover>().OnPointerExit(Event(move,Vector2.zero,-71));
                lift.GetComponent<StudyHover>().OnPointerExit(Event(lift,Vector2.zero,-72));
                Check(s.Input.CanAdjust,"Shared input accepts operation "+mode);
                var me=Event(move,new Vector2(25,15),-71);var le=Event(lift,new Vector2(0,12),-72);
                move.OnPointerDown(me);lift.OnPointerDown(le);
                yield return .32f;
                Check((s.Session.Pending.transform.position-pendingPosition).sqrMagnitude>.00001f,"UI "+mode+" pointer handlers move/lift real pending stone");
                if(mode>0)Check(study.MoveHeld&&study.LiftHeld,"UI "+mode+" active feedback follows held state");
                // Restore the same pending-stone pose for the comparison capture while leaving held feedback active.
                Check(s.Session.Adjust(pending,pendingPosition,pendingRotation),"Restore pending pose for equal-camera comparison "+mode);
                Capture(s,"ui-"+mode+"-active");
                move.OnPointerUp(me);lift.OnPointerUp(le);
                s.Input.CancelForFocusLoss();study.ClearPresentationFocus();
                yield return 1.8f;
                Check(!study.MoveHeld&&!study.LiftHeld&&move.Handle.anchoredPosition==Vector2.zero&&lift.Handle.anchoredPosition==Vector2.zero,"UI "+mode+" release/focus-cancel restores both handles");
                Check(s.Session.Adjust(pending,pendingPosition,pendingRotation),"Restore placement after cancel "+mode);
                yield return .2f;
                move.OnPointerDown(Event(move,new Vector2(-20,0),-73));yield return .16f;
                Check((pending.transform.position-pendingPosition).sqrMagnitude>.00001f,"UI "+mode+" permits repeated operation after cancel");
                move.OnPointerUp(Event(move,Vector2.zero,-73));s.Input.CancelForFocusLoss();study.ClearPresentationFocus();
                Check(s.Session.Adjust(pending,pendingPosition,pendingRotation),"Restore repeated-operation pose "+mode);
                yield return .2f;
            }
            study.Select(0);yield return .3f;
            Check(WorldSignature(s)==baseline,"Returning to original UI leaves Hilltop environment unchanged");
            Check(move.GetComponent<Image>().color.a>.1f&&lift.GetComponent<Image>().color.a>.1f,"Original HUD restores both authored backgrounds");
            // Background test is independent of UI: same A2 and same preset before/after.
            study.Select(2);lighting.Select(0,true);bg.SetEnabled(false);yield return .5f;
            Capture(s,"background-before");
            var near=s.Platform.GetComponent<Renderer>().sharedMaterial;var grassMesh=s.Grass.GetComponentInChildren<MeshFilter>().sharedMesh;
            var sunRotation=RenderSettings.sun.transform.rotation;var sunIntensity=RenderSettings.sun.intensity;var sky=RenderSettings.skybox;
            bg.SetEnabled(true);yield return .5f;Capture(s,"background-after");
            Check(s.Platform.GetComponent<Renderer>().sharedMaterial==near&&s.Grass.GetComponentInChildren<MeshFilter>().sharedMesh==grassMesh,"Background retains contact-platform material and grass geometry");
            Check(RenderSettings.sun.transform.rotation==sunRotation&&RenderSettings.sun.intensity==sunIntensity&&RenderSettings.skybox==sky,"Background preserves lighting controller sun and sky");
            for(int i=0;i<4;i++){lighting.Select(i,true);yield return .12f;Check(lighting.Selected==i&&lighting.enabled,"Background coexists with lighting preset "+i);}
            lighting.Select(0,true);
            for(int i=0;i<4;i++){bg.SetEnabled(false);bg.SetEnabled(true);}
            bg.SetEnabled(false);yield return .3f;
            Check(WorldSignature(s)==baseline,"Background OFF restores all original scenery transforms/material references/property blocks");
            Check(s.Platform.enabled&&s.Meadow.enabled,"Background toggles retain all original gameplay colliders");
            study.Select(0);Capture(s,"restored-original");
        }
        static string WorldSignature(HilltopScene s)
        {
            var text=new StringBuilder();
            foreach(var r in s.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(r.GetComponent<Stone>()!=null||r.name.StartsWith("Far cloud"))continue;
                text.Append(r.name).Append(r.transform.localPosition.ToString("F4")).Append(r.transform.localScale.ToString("F4")).Append(r.transform.localRotation.ToString("F4"));
                text.Append(r.sharedMaterial==null?0:r.sharedMaterial.GetInstanceID());
                var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);text.Append(block.isEmpty);
            }
            text.Append(RenderSettings.sun.transform.rotation.ToString("F4")).Append(RenderSettings.sun.intensity).Append(RenderSettings.sun.color);
            text.Append(RenderSettings.ambientSkyColor).Append(RenderSettings.ambientEquatorColor).Append(RenderSettings.fogColor).Append(RenderSettings.skybox.GetInstanceID());
            text.Append(s.Platform.enabled).Append(s.Meadow.enabled);return text.ToString();
        }
        static void Capture(HilltopScene s,string name)
        {
            var c=s.ViewCamera;var oldP=c.transform.position;var oldR=c.transform.rotation;
            var canvas=s.View.Root.GetComponent<Canvas>();var mode=canvas.renderMode;var wc=canvas.worldCamera;var plane=canvas.planeDistance;
            var old=c.targetTexture;float aspect=c.aspect;var active=RenderTexture.active;
            var rt=new RenderTexture(1440,900,24);rt.Create();var tx=new Texture2D(1440,900,TextureFormat.RGB24,false);
            try
            {
                c.transform.SetPositionAndRotation(CameraPosition,CameraRotation);c.targetTexture=rt;c.aspect=1.6f;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=c;canvas.planeDistance=.3f;Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(c,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;tx.ReadPixels(new Rect(0,0,1440,900),0,0);tx.Apply();Directory.CreateDirectory(Root+"/Review");File.WriteAllBytes(Root+"/Review/"+name+".png",tx.EncodeToPNG());
            }
            finally
            {c.targetTexture=old;c.aspect=aspect;RenderTexture.active=active;canvas.renderMode=mode;canvas.worldCamera=wc;canvas.planeDistance=plane;c.transform.SetPositionAndRotation(oldP,oldR);rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tx);}
        }
    }
}


#endif

