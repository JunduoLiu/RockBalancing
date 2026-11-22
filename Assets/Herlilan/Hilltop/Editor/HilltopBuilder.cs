using System;
using System.IO;
using Herlilan.Hilltop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Herlilan.Hilltop.Editor
{
    public static class HilltopBuilder
    {
        public const string Root="Assets/Herlilan/Hilltop";
        public const string ScenePath=Root+"/Scenes/Hilltop.unity";

        [MenuItem("Herlilan/Hilltop/Open playable scene")]
        public static void Open()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(!File.Exists(ScenePath))Create();
            else EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Herlilan/Hilltop/Rebuild generated scene and art")]
        public static void Rebuild()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(!EditorUtility.DisplayDialog("Rebuild Hilltop?","This replaces only the generated Hilltop scene and its generated art assets.","Rebuild","Cancel"))return;
            Create();
        }

        public static void Create()
        {
            foreach(var dir in new[]{"Scenes","Materials","Meshes","Settings","Review"})Directory.CreateDirectory(Root+"/"+dir);
            AssetDatabase.Refresh();
            var active=SceneManager.GetActiveScene();
            if(active.isDirty)throw new InvalidOperationException("Save the current scene before generating Hilltop.");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            scene.name="Hilltop";
            var root=new GameObject("Scene 2 · Hilltop");
            var setup=root.AddComponent<HilltopScene>();
            setup.Typeface=AssetDatabase.LoadAssetAtPath<Font>("Assets/RockBalancing/Fonts/BalanceSans.ttf");

            var stone=Material("River stone","Herlilan/Hilltop/Natural Stone",new Color(.56f,.55f,.49f));
            stone.SetFloat("_Grain",.65f);stone.SetFloat("_Scale",32);stone.SetFloat("_Smoothness",.16f);EditorUtility.SetDirty(stone);
            setup.StoneMaterial=stone;
            var platform=Material("Weathered limestone","Herlilan/Hilltop/Natural Stone",new Color(.62f,.62f,.57f));
            platform.SetFloat("_Grain",.68f);platform.SetFloat("_Scale",14);platform.SetFloat("_Smoothness",.12f);EditorUtility.SetDirty(platform);
            var soil=Material("Mossy hill","Universal Render Pipeline/Lit",new Color(.28f,.49f,.25f));
            soil.SetFloat("_Smoothness",.02f);EditorUtility.SetDirty(soil);
            var grass=Material("Meadow breeze","Herlilan/Hilltop/Meadow Wind",new Color(.26f,.48f,.27f));
            grass.SetColor("_TipColor",new Color(.49f,.68f,.35f));grass.SetFloat("_Wind",.018f);grass.SetFloat("_Speed",.55f);EditorUtility.SetDirty(grass);
            var sky=Material("Soft morning sky","Herlilan/Hilltop/Soft Sky",Color.white);
            sky.SetColor("_Horizon",new Color(.80f,.86f,.91f));sky.SetColor("_Zenith",new Color(.50f,.71f,.84f));sky.SetColor("_Ground",new Color(.72f,.81f,.82f));EditorUtility.SetDirty(sky);

            var ground=MeshObject("Grassy hill",SaveMesh("Hill",HilltopGeometry.Hill()),soil,root.transform);
            setup.Meadow=ground.AddComponent<MeshCollider>();((MeshCollider)setup.Meadow).sharedMesh=ground.GetComponent<MeshFilter>().sharedMesh;
            var slab=MeshObject("Natural summit platform",SaveMesh("Platform",HilltopGeometry.Platform()),platform,root.transform);
            var collision=slab.AddComponent<MeshCollider>();collision.sharedMesh=slab.GetComponent<MeshFilter>().sharedMesh;collision.convex=true;
            setup.Platform=collision;
            var grassRoot=new GameObject("Wind meadow · 26400 blades");grassRoot.transform.SetParent(root.transform,false);setup.Grass=grassRoot.transform;
            for(int patch=0;patch<12;patch++)
            {
                var go=MeshObject("Grass "+patch,SaveMesh("Grass "+patch,HilltopGeometry.Grass(patch)),grass,grassRoot.transform);
                go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            }

            var farRoot=new GameObject("Distant soft hills");farRoot.transform.SetParent(root.transform,false);
            var farMesh=SaveMesh("Distant mound",HilltopGeometry.Hill(6,14,48));
            for(int n=0;n<9;n++)
            {
                float angle=n*Mathf.PI*2/9+.17f;
                var mat=Material("Distant sage "+n,"Universal Render Pipeline/Lit",Color.Lerp(new Color(.36f,.52f,.39f),new Color(.57f,.67f,.53f),(n%3)/2f));
                mat.SetFloat("_Smoothness",0);EditorUtility.SetDirty(mat);
                var go=MeshObject("Horizon ridge "+n,farMesh,mat,farRoot.transform);
                go.transform.position=new Vector3(Mathf.Cos(angle)*(12+n%3),-2.5f-(n%3)*.55f,Mathf.Sin(angle)*(12+n%3));
                go.transform.localScale=new Vector3(1.6f+n%2*.4f,.35f,1.2f+n%3*.2f);
                go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            }

            RenderSettings.skybox=sky;RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.64f,.74f,.78f);
            RenderSettings.ambientEquatorColor=new Color(.54f,.62f,.58f);
            RenderSettings.ambientGroundColor=new Color(.34f,.41f,.32f);
            RenderSettings.ambientIntensity=1;RenderSettings.reflectionIntensity=.38f;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;
            RenderSettings.fogColor=new Color(.77f,.85f,.88f);RenderSettings.fogStartDistance=7;RenderSettings.fogEndDistance=30;
            var sunObject=new GameObject("Soft morning sun",typeof(Light));sunObject.transform.SetParent(root.transform,false);
            sunObject.transform.rotation=Quaternion.Euler(38,-42,0);
            var sun=sunObject.GetComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.98f,.92f);sun.intensity=1.45f;
            sun.shadows=LightShadows.Soft;sun.shadowStrength=.85f;sun.shadowBias=.035f;sun.shadowNormalBias=.16f;
            RenderSettings.sun=sun;

            var cameraObject=new GameObject("Hilltop camera",typeof(Camera),typeof(AudioListener));cameraObject.transform.SetParent(root.transform,false);cameraObject.tag="MainCamera";
            var camera=cameraObject.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.Skybox;
            camera.fieldOfView=22;camera.nearClipPlane=.02f;camera.farClipPlane=120;camera.allowHDR=true;camera.allowMSAA=true;
            camera.transform.SetPositionAndRotation(new Vector3(-1.55f,1.45f,-3.3f),Quaternion.Euler(16,25,0));
            var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;
            data.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
            setup.ViewCamera=camera;

            var volumeObject=new GameObject("Hilltop color and atmosphere",typeof(Volume));volumeObject.transform.SetParent(root.transform,false);
            var volume=volumeObject.GetComponent<Volume>();volume.isGlobal=true;volume.priority=20;
            string profilePath=Root+"/Settings/HilltopVolume.asset";
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if(profile==null){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,profilePath);}
            var tone=Component<Tonemapping>(profile);tone.mode.Override(TonemappingMode.Neutral);
            var adjust=Component<ColorAdjustments>(profile);adjust.postExposure.Override(.25f);adjust.contrast.Override(5);adjust.saturation.Override(0);
            var white=Component<WhiteBalance>(profile);white.temperature.Override(0);white.tint.Override(0);
            var bloom=Component<Bloom>(profile);bloom.intensity.Override(.12f);bloom.threshold.Override(1.15f);bloom.scatter.Override(.55f);
            var vignette=Component<Vignette>(profile);vignette.intensity.Override(.12f);vignette.smoothness.Override(.7f);
            EditorUtility.SetDirty(profile);volume.sharedProfile=profile;

            string rendererPath=Root+"/Settings/HilltopRenderer.asset",pipelinePath=Root+"/Settings/HilltopPipeline.asset";
            if(!File.Exists(rendererPath))AssetDatabase.CopyAsset("Assets/Settings/PC_Renderer.asset",rendererPath);
            if(!File.Exists(pipelinePath))AssetDatabase.CopyAsset("Assets/Settings/PC_RPAsset.asset",pipelinePath);
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            var rendererSettings=new SerializedObject(renderer);
            rendererSettings.FindProperty("m_RenderingMode").intValue=0;
            rendererSettings.ApplyModifiedPropertiesWithoutUndo();
            foreach(var feature in renderer.rendererFeatures)
            {
                if(feature==null)continue;
                var serialized=new SerializedObject(feature);
                serialized.FindProperty("m_Settings.Intensity")?.SetFloat(.38f);
                serialized.FindProperty("m_Settings.Radius")?.SetFloat(.16f);
                var source=serialized.FindProperty("m_Settings.Source");if(source!=null)source.intValue=0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            var settings=new SerializedObject(pipeline);settings.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue=renderer;
            settings.ApplyModifiedPropertiesWithoutUndo();
            pipeline.shadowDistance=12;pipeline.shadowCascadeCount=2;pipeline.msaaSampleCount=4;
            pipeline.supportsCameraDepthTexture=true;pipeline.supportsCameraOpaqueTexture=false;
            EditorUtility.SetDirty(pipeline);setup.Pipeline=pipeline;
            AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new IOException("Hilltop scene could not be saved.");
            AssetDatabase.Refresh();Selection.activeGameObject=root;
            Debug.Log("HILLTOP_CREATED "+ScenePath);
        }

        static T Component<T>(VolumeProfile profile) where T:VolumeComponent
        {
            if(profile.TryGet<T>(out var value))return value;
            value=profile.Add<T>(true);AssetDatabase.AddObjectToAsset(value,profile);return value;
        }
        static Material Material(string name,string shaderName,Color color)
        {
            string path=Root+"/Materials/"+name.Replace(' ','_')+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null)
            {
                var shader=Shader.Find(shaderName);if(shader==null)throw new InvalidOperationException("Missing shader "+shaderName);
                material=new Material(shader){name=name};AssetDatabase.CreateAsset(material,path);
            }
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            EditorUtility.SetDirty(material);return material;
        }
        static Mesh SaveMesh(string name,Mesh mesh)
        {
            string path=Root+"/Meshes/"+name.Replace(' ','_')+".asset";
            mesh.name=name.Replace(' ','_');
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing!=null)
            {
                // Set the mesh data explicitly so an already loaded renderer also
                // receives new GPU buffers when the authoring menu is rerun.
                existing.Clear();existing.indexFormat=mesh.indexFormat;existing.name=mesh.name;
                existing.vertices=mesh.vertices;existing.triangles=mesh.triangles;
                existing.normals=mesh.normals;existing.uv=mesh.uv;existing.colors=mesh.colors;existing.bounds=mesh.bounds;
                existing.UploadMeshData(false);
                Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;
            }
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static GameObject MeshObject(string name,Mesh mesh,Material material,Transform parent)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        static void SetFloat(this SerializedProperty property,float value){property.floatValue=value;}
    }
}
