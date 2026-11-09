using UnityEngine;

namespace RockBalancing
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        public Font Typeface;
        public Material StoneMaterial;
        public Material GuideMaterial;
        readonly System.Collections.Generic.List<Material> materials=new System.Collections.Generic.List<Material>();
        void Start()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            var ground = Primitive("Ground", new Vector3(0,-.13f,0), new Vector3(200,.1f,200), new Color(.80f,.84f,.81f));
            var pedestal = Primitive("Base stone", new Vector3(0,0,0), new Vector3(1,.16f,1), new Color(.36f,.40f,.38f));
            var cameraObject = new GameObject("Game camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.86f,.89f,.86f);
            camera.fieldOfView = 42;
            camera.nearClipPlane = .02f;
            camera.farClipPlane = 250;
            var lightObject = new GameObject("Sun", typeof(Light));
            lightObject.transform.SetParent(transform);
            lightObject.transform.rotation = Quaternion.Euler(48,-35,0);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.65f,.70f,.73f);
            var sessionObject = new GameObject("Game session");
            sessionObject.transform.SetParent(transform);
            var session = sessionObject.AddComponent<GameSession>();
            var input = gameObject.AddComponent<GameInput>();
            input.Session = session; input.ViewCamera = camera;
            var view = gameObject.AddComponent<GameView>();
            view.Session = session; view.Input = input; view.Typeface = Typeface;
            view.Build();
            session.Initialize(new StoneFactory(StoneMaterial), pedestal.GetComponent<Collider>(), ground.GetComponent<Collider>());
            var guide=gameObject.AddComponent<PlacementGuide>();
            guide.Template=GuideMaterial; guide.Session=session;
            var share=gameObject.AddComponent<ShareController>();
            share.Session=session;share.View=view;share.Input=input;share.Initialize();
        }
        GameObject Primitive(string name, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(transform);
            go.transform.position = position; go.transform.localScale = scale;
            var material=StoneFactory.MakeMaterial(color,StoneMaterial);
            materials.Add(material);
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
        void OnDestroy() { foreach(var material in materials) if(material!=null)Destroy(material); }
    }
}
