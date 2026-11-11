using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Herlilan.Prototype.Editor
{
    public static class StoneStatePrototypeMenu
    {
        [MenuItem("Herlilan/Prototype/Open Stone State Prototype")]
        public static void Open()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Exit Play mode before opening the prototype.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Stone state prototype").AddComponent<StoneStatePrototype>();
            var camera = new GameObject("Prototype camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            StoneStatePrototype.ConfigureCamera(camera);
            camera.backgroundColor = new Color(0.12f, 0.16f, 0.2f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            var light = new GameObject("Prototype light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientLight = Color.gray;
            Selection.activeGameObject = camera.gameObject;
        }
    }
}
