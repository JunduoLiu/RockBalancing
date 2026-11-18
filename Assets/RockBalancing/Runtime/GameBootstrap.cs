using UnityEngine;

namespace RockBalancing
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        public Font Typeface;
        public Material StoneMaterial;
        public Material GuideMaterial;
        public GameObject CabinetModel;
        [Range(1, 179)] public float ArcadeRotationStepDegrees = 15;
        public StoneShapeSettings StoneShapes = new StoneShapeSettings();
        readonly System.Collections.Generic.List<Material> materials=new System.Collections.Generic.List<Material>();
        Mesh pedestalMesh;
        void Start()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            var ground = Surface("Ground", new Vector3(0,-.13f,0), new Vector3(200,.1f,200), new Color(.80f,.84f,.81f));
            if (CabinetModel != null) pedestalMesh = ArcadeRockMesh.CreatePedestal();
            var pedestal = Surface("Base stone", Vector3.zero, pedestalMesh == null ? new Vector3(1, .16f, 1) : Vector3.one,
                new Color(.36f, .40f, .38f), pedestalMesh);
            ArcadeCabinet cabinet = null;
            if (CabinetModel != null)
            {
                ground.GetComponent<Renderer>().enabled = false;
                var floor = Surface("Arcade floor", new Vector3(0, -.74f, 0), new Vector3(200, .1f, 200),
                    new Color(.45f, .53f, .50f));
                var floorShape = floor.GetComponent<Collider>();
                floorShape.enabled = false;
                Destroy(floorShape);
                cabinet = gameObject.AddComponent<ArcadeCabinet>();
                cabinet.Initialize(CabinetModel);
            }
            var cameraObject = new GameObject("Game camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = cabinet == null ? new Color(.86f,.89f,.86f) : new Color32(135, 162, 154, 255);
            camera.fieldOfView = 42;
            camera.nearClipPlane = .02f;
            // Keep the arcade shadow map focused on the cabinet and its contact surfaces.
            camera.farClipPlane = cabinet == null ? 250 : 12;
            var lightObject = new GameObject("Sun", typeof(Light));
            lightObject.transform.SetParent(transform);
            lightObject.transform.rotation = Quaternion.Euler(48, cabinet == null ? -35 : -145, 0);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.shadows = cabinet == null ? LightShadows.Soft : LightShadows.Hard;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.65f,.70f,.73f);
            var sessionObject = new GameObject("Game session");
            sessionObject.transform.SetParent(transform);
            var session = sessionObject.AddComponent<GameSession>();
            session.TrackArcadeRecord = cabinet != null;
            var input = gameObject.AddComponent<GameInput>();
            input.Session = session; input.ViewCamera = camera; input.Cabinet = cabinet;
            if (cabinet != null) input.RotationStepDegrees = ArcadeRotationStepDegrees;
            var view = gameObject.AddComponent<GameView>();
            view.Session = session; view.Input = input; view.Typeface = Typeface;
            view.Build();
            session.Initialize(new StoneFactory(StoneMaterial, StoneShapes, arcade: cabinet != null), pedestal.GetComponent<Collider>(), ground.GetComponent<Collider>());
            if (cabinet != null)
            {
                cabinet.Follow(session);
                cabinet.PlaceCamera(camera);
            }
            var guide=gameObject.AddComponent<PlacementGuide>();
            guide.Template=GuideMaterial; guide.Session=session;
            guide.Arcade = cabinet != null;
            if (cabinet != null) return;
            var share=gameObject.AddComponent<ShareController>();
            share.Session=session;share.View=view;share.Input=input;share.Initialize();
        }
        GameObject Surface(string name, Vector3 position, Vector3 scale, Color color, Mesh mesh = null)
        {
            GameObject go;
            if (mesh == null) go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            else
            {
                go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var collider = go.GetComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.convex = true;
            }
            go.name = name; go.transform.SetParent(transform);
            go.transform.position = position; go.transform.localScale = scale;
            var material=StoneFactory.MakeMaterial(color,StoneMaterial);
            materials.Add(material);
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
        void OnDestroy()
        {
            foreach(var material in materials) if(material!=null)Destroy(material);
            if (pedestalMesh != null) Destroy(pedestalMesh);
        }
    }
}
