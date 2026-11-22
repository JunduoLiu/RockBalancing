using System;
using System.IO;
using System.Collections.Generic;
using Herlilan.Hilltop;
using RockBalancing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace Herlilan.ZenTriptych.Editor
{
 [InitializeOnLoad]
 public static class ZenReview
 {
  const string Root="Assets/Herlilan/ZenTriptych",Key="ZenTriptych.Review";
  static int stage,dropped;static double next;static string previous;
  static List<string> checks=new List<string>();static List<string> errors=new List<string>();
  static ZenReview(){EditorApplication.delayCall+=RequestedReview;EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;
   Application.logMessageReceived+=(m,t,k)=>{if(SessionState.GetBool(Key,false)&&(k==LogType.Error||k==LogType.Exception))errors.Add(m);};}
  static void RequestedReview(){
   string request=Root+"/Review/run-request.txt";
   if(!File.Exists(request)||File.ReadAllText(request).Trim()!="run")return;
   if(EditorApplication.isPlayingOrWillChangePlaymode||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty){File.WriteAllText(request,"blocked: editor is playing or current scene is dirty");return;}
   File.WriteAllText(request,"started");Run();
  }
  [MenuItem("Herlilan/Zen Triptych/Open Wind")] static void Wind(){Open("Wind");}
  [MenuItem("Herlilan/Zen Triptych/Open Paper")] static void Paper(){Open("Paper");}
  [MenuItem("Herlilan/Zen Triptych/Open Arcade")] static void Arcade(){Open("Arcade");}
  static void Open(string name){if(EditorApplication.isPlaying)return;if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(Root+"/Scenes/Zen-"+name+".unity");}
  [MenuItem("Herlilan/Zen Triptych/Run comparison and checks")]
  public static void Run(){
   if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
   if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
   SessionState.SetString(Key+".previous",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
   Open("Wind");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
  }
  static void State(PlayModeStateChange s){
   if(!SessionState.GetBool(Key,false))return;
   if(s==PlayModeStateChange.EnteredPlayMode){stage=0;dropped=0;checks.Clear();errors.Clear();next=EditorApplication.timeSinceStartup+4;}
   if(s==PlayModeStateChange.EnteredEditMode){
    SessionState.SetBool(Key,false);
    File.WriteAllText(Root+"/Review/validation.md","# Actual Unity Play Mode validation\n\n"+string.Join("\n",checks)+"\n\nErrors: "+errors.Count+"\n"+string.Join("\n",errors)+"\n\nScripted session APIs only. Native mouse/keyboard/touch are not certified.\n");
    previous=SessionState.GetString(Key+".previous","");if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);
    AssetDatabase.Refresh();Debug.Log("ZEN_REVIEW "+(errors.Count==0?"PASS":"FAIL"));
   }
  }
  static void Check(bool b,string m){if(!b)throw new Exception(m);checks.Add("- PASS: "+m);}
  static void Tick(){
   if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
   try{
    var z=Object.FindFirstObjectByType<ZenPresentation>();Check(z!=null,"Isolated presentation loaded");
    var s=z.Owner;
    if(stage==0){
     var p=s.Session.Pending;Check(p!=null,"Pending stone generated");
     Check(s.Session.Adjust(p,p.transform.position+Vector3.up*.04f, p.transform.rotation),"Raise");
     Check(s.Session.Adjust(p,p.transform.position+Vector3.right*.02f, p.transform.rotation),"Move");
     Check(s.Session.Adjust(p,new Vector3(0,s.Session.TowerTop()+p.Shape.bounds.extents.y+.016f,0),Quaternion.identity),"Lower to placement");
     Check(s.Session.Release(p),"Release through shared session");dropped++;next=EditorApplication.timeSinceStartup+2;
     if(dropped==3)stage=1;
    }else if(stage==1){
     s.Session.RefreshState();Check(s.Session.CurrentHeightMm>0,"Current effective tower height > 0");
     s.Input.enabled=false;
     s.ViewCamera.transform.SetPositionAndRotation(new Vector3(-1.55f,1.45f,-3.3f),Quaternion.Euler(16,25,0));
     stage=2;z.Select(0);next=EditorApplication.timeSinceStartup+.2;
    }else if(stage>=2&&stage<=4){
     Capture(s,new[]{"wind","paper","arcade"}[stage-2]);
     int h=s.Session.CurrentHeightMm;
     for(int i=0;i<12;i++)z.Select(i%3);
     Check(s.Platform.enabled&&s.Meadow.enabled,"Colliders retained across repeated switches");
     Check(h==s.Session.CurrentHeightMm,"Visual switches leave height unchanged");
     stage++;if(stage<=4)z.Select(stage-2);next=EditorApplication.timeSinceStartup+.25;
    }else{
     z.Select(0);Check(s.Grass.gameObject.activeSelf,"Wind environment restores");
     Check(s.View.Root.Find("Adjustments/Move").GetComponent<RectTransform>().rect.width>=114,"Move hit area preserved");
     s.Session.Restart();Check(s.Session.Pending!=null&&s.Session.CurrentHeightMm==0,"Restart");
     EditorApplication.isPlaying=false;
    }
   }catch(Exception e){errors.Add(e.ToString());EditorApplication.isPlaying=false;}
  }
  static void Capture(HilltopScene s,string name){
   var c=s.ViewCamera;var canvas=s.View.Root.GetComponent<Canvas>();
   var mode=canvas.renderMode;var wc=canvas.worldCamera;var plane=canvas.planeDistance;
   var old=c.targetTexture;float aspect=c.aspect;var active=RenderTexture.active;
   var rt=new RenderTexture(1440,900,24);rt.Create();var tx=new Texture2D(1440,900,TextureFormat.RGB24,false);
   try{
    c.targetTexture=rt;c.aspect=1.6f;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=c;canvas.planeDistance=.3f;Canvas.ForceUpdateCanvases();
    RenderPipeline.SubmitRenderRequest(c,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
    RenderTexture.active=rt;tx.ReadPixels(new Rect(0,0,1440,900),0,0);tx.Apply();File.WriteAllBytes(Root+"/Review/"+name+".png",tx.EncodeToPNG());
   }finally{c.targetTexture=old;c.aspect=aspect;RenderTexture.active=active;canvas.renderMode=mode;canvas.worldCamera=wc;canvas.planeDistance=plane;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tx);}
  }
 }
}


