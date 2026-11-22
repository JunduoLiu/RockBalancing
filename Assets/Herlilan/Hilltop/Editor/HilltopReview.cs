using System;
using System.Collections.Generic;
using System.IO;
using RockBalancing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace Herlilan.Hilltop.Editor
{
    [InitializeOnLoad]
    public static class HilltopReview
    {
        const string Running="Herlilan.Hilltop.Review",Done="Herlilan.Hilltop.Review.Done",Failed="Herlilan.Hilltop.Review.Failed";
        static double next,deadline;
        static int stage,dropped;
        static Stone released;
        static readonly List<string> checks=new List<string>();
        static readonly List<string> errors=new List<string>();
        static Color32[] windA;

        static HilltopReview()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=State;
            Application.logMessageReceived+=Log;
        }
        static void Log(string message,string trace,LogType type)
        {
            if(SessionState.GetBool(Running,false) && (type==LogType.Error || type==LogType.Exception || type==LogType.Assert))errors.Add(message);
        }
        public static void BuildAndReview(){HilltopBuilder.Create();Begin();}
        [MenuItem("Herlilan/Hilltop/Capture and validate preview")]
        public static void Run()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene(HilltopBuilder.ScenePath);Begin();
        }
        static void Begin()
        {
            SessionState.SetBool(Running,true);SessionState.SetBool(Done,false);SessionState.SetBool(Failed,false);
            UnityEngine.Random.InitState(5401);EditorApplication.isPlaying=true;
        }
        static void State(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                EditorWindow.FocusWindowIfItsOpen(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
                stage=0;dropped=0;checks.Clear();errors.Clear();next=EditorApplication.timeSinceStartup+3;deadline=next+90;
            }
            if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Done,false))
            {
                bool failed=SessionState.GetBool(Failed,false);
                SessionState.SetBool(Running,false);SessionState.SetBool(Done,false);AssetDatabase.Refresh();
                if(Application.isBatchMode)EditorApplication.Exit(failed?1:0);
            }
            else if(state==PlayModeStateChange.EnteredEditMode)SessionState.SetBool(Running,false);
        }
        static void Require(bool condition,string message)
        {if(!condition)throw new InvalidOperationException(message);checks.Add(message);}

        static void Tick()
        {
            if(!SessionState.GetBool(Running,false)||!EditorApplication.isPlaying||SessionState.GetBool(Done,false))return;
            if(EditorApplication.timeSinceStartup<next)return;
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Hilltop review timed out.");
                var scene=Object.FindFirstObjectByType<HilltopScene>();
                if(scene==null || scene.Session==null)throw new InvalidOperationException("Hilltop bootstrap did not start.");
                if(stage==0)
                {
                    Require(scene.Session.Pending!=null,"Shared stone factory provides a pending stone.");
                    Require(scene.Session.Base==scene.Platform && scene.Session.Ground==scene.Meadow,"Shared session uses the visible platform and hill colliders.");
                    Require(QualitySettings.renderPipeline==scene.Pipeline,"Scene-local render pipeline is active.");
                    Require(scene.ViewCamera.GetUniversalAdditionalCameraData().renderPostProcessing,"Camera post-processing is enabled.");
                    Require(!scene.View.ReleaseButton.gameObject.activeSelf,"Minimal preview has no permanent release button.");
                    Capture(scene,"hilltop-empty",1440,900);stage=1;
                }
                else if(stage==1)
                {
                    var stone=scene.Session.Pending;
                    Require(stone!=null,"Next pending stone exists for release "+dropped);
                    Vector3 p=new Vector3(0,scene.Session.TowerTop()+stone.Shape.bounds.extents.y+.016f,0);
                    Require(scene.Session.Adjust(stone,p,Quaternion.identity),"Placement accepts a clear pose "+dropped);
                    // A visual/physics replay is independent of OS input focus.
                    // Real UI actions still use GameInput.ActOnStone and its guards.
                    Require(scene.Session.Release(stone),"Shared game session releases stone "+dropped);
                    released=stone;dropped++;stage=2;next=EditorApplication.timeSinceStartup+1.1;
                }
                else if(stage==2)
                {
                    scene.Session.RefreshState();
                    Require(released!=null && released.Released && !released.Body.isKinematic,"Released stone enters live physics "+dropped);
                    Require(scene.Session.Pending!=null && scene.Session.Pending!=released,"Release supplies a different pending stone "+dropped);
                    if(dropped==1)Require(scene.Session.CurrentHeightMm>0,"Platform contact contributes to current effective tower height.");
                    if(dropped<5){stage=1;next=EditorApplication.timeSinceStartup+.1;}
                    else{stage=3;next=EditorApplication.timeSinceStartup+2;}
                }
                else if(stage==3)
                {
                    scene.Input.enabled=false;
                    windA=Capture(scene,"hilltop-landscape",1440,900);Capture(scene,"hilltop-portrait",720,1200);
                    var data=scene.ViewCamera.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;
                    Capture(scene,"hilltop-without-post",1440,900);data.renderPostProcessing=true;
                    stage=4;next=EditorApplication.timeSinceStartup+1.3;
                }
                else if(stage==4)
                {
                    var windB=Capture(scene,"hilltop-breeze",1440,900);
                    long delta=0;
                    for(int n=0;n<windA.Length;n++)
                        if(windA[n].g>windA[n].r*1.12f && windA[n].g>windA[n].b*1.12f)
                            delta+=Math.Abs(windA[n].g-windB[n].g);
                    Require(delta>10000,"Green meadow pixels change over time with the camera fixed (wind animation).");
                    foreach(var name in new[]{"Herlilan/Hilltop/Natural Stone","Herlilan/Hilltop/Meadow Wind","Herlilan/Hilltop/Soft Sky"})
                    {
                        var shader=Shader.Find(name);Require(shader!=null && shader.isSupported,"Shader supported: "+name);
                        foreach(var message in ShaderUtil.GetShaderMessages(shader))
                            if(message.severity.ToString()=="Error")throw new InvalidOperationException(name+": "+message.message);
                    }
                    scene.Input.enabled=false;
                    var camera=scene.ViewCamera;camera.transform.SetPositionAndRotation(new Vector3(0,-1,0),Quaternion.Euler(-90,0,0));
                    scene.Input.Occlusion.UpdateView(camera,.08f);stage=5;next=EditorApplication.timeSinceStartup+.4;
                }
                else if(stage==5)
                {
                    scene.Input.Occlusion.UpdateView(scene.ViewCamera,.08f);
                    Require(scene.Input.Occlusion.IsFaded(scene.Meadow),"Shared occlusion detects the hill from below.");
                    Require(scene.Platform.enabled && scene.Meadow.enabled,"Occlusion preserves platform and ground collision.");
                    int oldEpoch=scene.Session.Epoch;scene.Session.Restart();
                    Require(scene.Session.Epoch==oldEpoch+1 && scene.Session.Pending!=null,"Shared restart clears the session and supplies a stone.");
                    Require(scene.Session.CurrentHeightMm==0,"Restart resets current effective tower height.");Finish();
                }
            }
            catch(Exception error){errors.Add(error.ToString());Finish();}
        }

        internal static Color32[] Capture(HilltopScene scene,string name,int width,int height)
        {
            var camera=scene.ViewCamera;var canvas=scene.View.Root.GetComponent<Canvas>();
            var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;float oldPlane=canvas.planeDistance;
            var oldTarget=camera.targetTexture;float oldAspect=camera.aspect;var oldActive=RenderTexture.active;
            var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){name="Hilltop review"};target.Create();
            var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;camera.aspect=(float)width/height;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.3f;Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=target });
                RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
                string path=HilltopBuilder.Root+"/Review/"+name+".png";File.WriteAllBytes(path,texture.EncodeToPNG());
                Debug.Log("HILLTOP_CAPTURE "+path);return texture.GetPixels32();
            }
            finally
            {
                RenderTexture.active=oldActive;camera.targetTexture=oldTarget;camera.aspect=oldAspect;
                canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldPlane;
                target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(texture);
            }
        }
        static void Finish()
        {
            SessionState.SetBool(Done,true);SessionState.SetBool(Failed,errors.Count>0);
            string report="# Hilltop Unity review\n\nActual Hilltop Play Mode scene. Screenshots are Unity renders.\n\n"+
                "Automated visual/physics replay uses the shared GameSession placement and release APIs. It does not certify native keyboard, mouse or touch input.\n\n";
            foreach(string check in checks)report+="- PASS: "+check+"\n";
            report+="\nErrors: "+errors.Count+"\n";
            foreach(string error in errors)report+="\n```\n"+error+"\n```\n";
            File.WriteAllText(HilltopBuilder.Root+"/Review/validation.md",report);
            Debug.Log("HILLTOP_REVIEW "+(errors.Count==0?"PASS":"FAIL")+" checks="+checks.Count+" errors="+errors.Count);EditorApplication.isPlaying=false;
        }
    }
}
