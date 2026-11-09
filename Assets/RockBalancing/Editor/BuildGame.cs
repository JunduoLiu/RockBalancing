using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RockBalancing.Editor
{
    public static class BuildGame
    {
        public const string ScenePath = "Assets/RockBalancing/Scenes/Balance.unity";
        [MenuItem("Rock Balancing/Create playable scene")]
        public static void CreateScene()
        {
            Directory.CreateDirectory("Assets/RockBalancing/Scenes");
            AssetDatabase.Refresh();
            var current = SceneManager.GetActiveScene();
            if (current.isDirty) throw new System.InvalidOperationException("Save the current scene before creating a playable scene.");
            var mode = string.IsNullOrEmpty(current.path) ? NewSceneMode.Single : NewSceneMode.Additive;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            SceneManager.SetActiveScene(scene);
            var bootstrap=new GameObject("Rock Balancing", typeof(GameBootstrap)).GetComponent<GameBootstrap>();
            ConfigureAssets(bootstrap);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new System.Exception("Could not save playable scene");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }
        public static void BuildWeb()
        {
            PlayerSettings.companyName = "RockBalancing";
            PlayerSettings.productName = "Rock Balance";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.dataCaching = false;
            PlayerSettings.WebGL.template = "PROJECT:RockBalance";
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.defaultWebScreenWidth = 430;
            PlayerSettings.defaultWebScreenHeight = 900;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = "Builds/Web", target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Web build failed: " + report.summary.result);
        }

        [MenuItem("Rock Balancing/Update scene assets")]
        public static void UpdateAssets()
        {
            var bootstrap=Object.FindFirstObjectByType<GameBootstrap>();
            if(bootstrap==null)throw new System.Exception("Open the Balance scene first.");
            ConfigureAssets(bootstrap);
            EditorUtility.SetDirty(bootstrap);
            EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);
            EditorSceneManager.SaveScene(bootstrap.gameObject.scene);
            AssetDatabase.SaveAssets();
        }
        static void ConfigureAssets(GameBootstrap bootstrap)
        {
            bootstrap.Typeface=AssetDatabase.LoadAssetAtPath<Font>("Assets/RockBalancing/Fonts/BalanceSans.ttf");
            bootstrap.StoneMaterial=GetMaterial("Stone","Universal Render Pipeline/Lit");
            bootstrap.GuideMaterial=GetMaterial("Guide","Universal Render Pipeline/Unlit");
        }
        static Material GetMaterial(string name,string shader)
        {
            Directory.CreateDirectory("Assets/RockBalancing/Materials");
            AssetDatabase.Refresh();
            string path="Assets/RockBalancing/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(material,path);}
            return material;
        }

        [MenuItem("Rock Balancing/Portrait preview")]
        public static void PortraitPreview()
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { 0 });
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var kind = assembly.GetType("UnityEditor.GameViewSizeType");
            var size = System.Activator.CreateInstance(sizeType, new object[] { System.Enum.ToObject(kind,1), 430,900,"Rock Balance Phone" });
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[] { size });
            int total=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
            var window=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
            window.GetType().GetProperty("selectedSizeIndex").SetValue(window,total-1);
        }
    }
}
