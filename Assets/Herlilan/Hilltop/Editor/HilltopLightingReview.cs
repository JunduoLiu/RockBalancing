using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Herlilan.Hilltop.Editor
{
    [InitializeOnLoad]
    public static class HilltopLightingReview
    {
        const string Key = "Hilltop.LightingReview";
        static double next, deadline;
        static int stage, dropped;
        static float before;
        static readonly List<string> checks = new List<string>();
        static readonly List<string> errors = new List<string>();
        static string skyJson, profileJson;
        static HilltopLightingReview()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += State;
            Application.logMessageReceived += (message, trace, type) => {
                if (SessionState.GetBool(Key,false) && (type==LogType.Error || type==LogType.Exception)) errors.Add(message);
            };
        }

        [MenuItem("Herlilan/Hilltop/Capture and validate lighting")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before running lighting review.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(HilltopBuilder.ScenePath);
            SessionState.SetBool(Key,true);
            EditorApplication.isPlaying=true;
        }
        static void State(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key,false)) return;
            if (state==PlayModeStateChange.EnteredPlayMode)
            {
                stage=0; dropped=0; checks.Clear(); errors.Clear();
                next=EditorApplication.timeSinceStartup+3; deadline=next+60;
                skyJson=EditorJsonUtility.ToJson(Sky);
                profileJson=ProfileSnapshot();
            }
            if (state==PlayModeStateChange.EnteredEditMode)
            {
                try {
                    Require(RenderSettings.skybox==Sky,"Authored sky restored after Play Mode.");
                    Require(EditorJsonUtility.ToJson(Sky)==skyJson,"Sky asset contents unchanged.");
                    Require(ProfileSnapshot()==profileJson,"Volume asset and all overrides unchanged.");
                } catch (Exception e) { errors.Add(e.Message); }
                SessionState.SetBool(Key,false);
                string report="# Hilltop lighting validation\n\nUnity Play Mode review. Images use a fixed camera and the same stones.\n\n";
                foreach(var check in checks) report+="- PASS: "+check+"\n";
                report+="\nErrors: "+errors.Count+"\n";
                foreach(var error in errors) report+="\n"+error+"\n";
                report+="\nKeyboard shortcut delivery and physical-device interaction still require manual acceptance.\n";
                File.WriteAllText(HilltopBuilder.Root+"/Review/lighting-validation.md",report);
                AssetDatabase.Refresh();
                Debug.Log("HILLTOP_LIGHTING_REVIEW "+(errors.Count==0?"PASS":"FAIL"));
                if(Application.isBatchMode)EditorApplication.Exit(errors.Count==0?0:1);
            }
        }
        static Material Sky => AssetDatabase.LoadAssetAtPath<Material>(HilltopBuilder.Root+"/Materials/Soft_morning_sky.mat");
        static VolumeProfile Profile => AssetDatabase.LoadAssetAtPath<VolumeProfile>(HilltopBuilder.Root+"/Settings/HilltopVolume.asset");
        static string ProfileSnapshot()
        {
            string result=EditorJsonUtility.ToJson(Profile);
            foreach(var component in Profile.components) result+=EditorJsonUtility.ToJson(component);
            return result;
        }
        static void Require(bool condition,string message)
        { if(!condition)throw new InvalidOperationException(message); checks.Add(message); }

        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Lighting review timed out.");
                var scene=Object.FindFirstObjectByType<HilltopScene>();
                var light=Object.FindFirstObjectByType<HilltopLighting>();
                if(scene==null||scene.Session==null||light==null)throw new InvalidOperationException("Lighting did not initialize.");
                switch(stage)
                {
                    case 0:
                        var stone=scene.Session.Pending;
                        Require(stone!=null,"Pending stone exists "+dropped);
                        Require(scene.Session.Adjust(stone,new Vector3(0,scene.Session.TowerTop()+stone.Shape.bounds.extents.y+.016f,0),Quaternion.identity)
                            && scene.Session.Release(stone),"Existing placement and release work "+dropped);
                        dropped++; if(dropped==3)stage=1; next=EditorApplication.timeSinceStartup+1.3; break;
                    case 1:
                        scene.Input.enabled=false;
                        Require(RenderSettings.skybox!=Sky,"Sky uses a runtime copy.");
                        Require(scene.GetComponentInChildren<Volume>().sharedProfile!=Profile,"Volume uses a runtime copy.");
                        stage=2; break;
                    case 2:
                        for(int i=0;i<4;i++)
                        {
                            light.Select(i,true);
                            Require(!light.IsTransitioning && light.Selected==i,"Preset applies immediately: "+HilltopLighting.Names[i]);
                            HilltopReview.Capture(scene,"lighting-"+i,1440,900);
                        }
                        light.TransitionSeconds=1; light.Select(1,true); before=light.Intensity; light.Select(3);
                        stage=3; next=EditorApplication.timeSinceStartup+.3; break;
                    case 3:
                        Require(light.IsTransitioning && light.Intensity<before && light.Intensity>.55f,"Transition renders intermediate lighting.");
                        before=light.Intensity; light.Select(2);
                        Require(Mathf.Abs(light.Intensity-before)<.001f,"Interrupted transition starts from the current look without a jump.");
                        stage=4; next=EditorApplication.timeSinceStartup+1.3; break;
                    case 4:
                        Require(!light.IsTransitioning && Mathf.Abs(light.Intensity-1.65f)<.001f,"Interrupted transition reaches the new preset.");
                        light.Select(3,true); light.CycleSeconds=5; light.AutoCycle=true;
                        stage=5; next=EditorApplication.timeSinceStartup+5.3; break;
                    case 5:
                        Require(light.Selected==0,"Automatic cycle wraps from overcast to morning.");
                        light.SetManual(40,60,.9f,.15f);
                        Require(!light.AutoCycle && !light.IsTransitioning && Mathf.Abs(RenderSettings.sun.intensity-.9f)<.001f,"Manual controls stop cycling and apply immediately.");
                        light.enabled=false;
                        Require(RenderSettings.skybox==Sky && scene.GetComponentInChildren<Volume>().sharedProfile==Profile,"Disabling lighting restores authored assets.");
                        Require(Mathf.Abs(RenderSettings.sun.intensity-1.45f)<.001f,"Disabling lighting restores the original sun.");
                        light.enabled=true;
                        Require(RenderSettings.skybox!=Sky,"Re-enabling lighting recreates isolated runtime assets.");
                        stage=6; next=EditorApplication.timeSinceStartup+.2; break;
                    case 6:
                        EditorApplication.isPlaying=false; stage=7; break;
                }
            }
            catch(Exception e){errors.Add(e.ToString()); EditorApplication.isPlaying=false; stage=7;}
        }
    }
}
