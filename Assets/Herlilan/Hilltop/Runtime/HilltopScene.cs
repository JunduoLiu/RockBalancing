using System.Threading.Tasks;
using RockBalancing;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Herlilan.Hilltop
{
    [DefaultExecutionOrder(-100)]
    public sealed class HilltopScene : MonoBehaviour
    {
        public Camera ViewCamera;
        public Collider Platform;
        public Collider Meadow;
        public Transform Grass;
        public Material StoneMaterial;
        public Font Typeface;
        public UniversalRenderPipelineAsset Pipeline;
        public StoneShapeSettings Shapes=new StoneShapeSettings();
        public bool MinimalControls=true;
        public GameSession Session { get; private set; }
        public GameInput Input { get; private set; }
        public GameView View { get; private set; }
        RenderPipelineAsset previousPipeline;
        bool pipelineChanged;
        int previousFrameRate;
        bool previousBackground;

        void Awake()
        {
            previousPipeline=QualitySettings.renderPipeline;
            previousFrameRate=Application.targetFrameRate;previousBackground=Application.runInBackground;
            if(Pipeline!=null){QualitySettings.renderPipeline=Pipeline;pipelineChanged=true;}
            Application.targetFrameRate=60;
            Application.runInBackground=true;
        }

        void Start()
        {
            if(ViewCamera==null || Platform==null || Meadow==null || StoneMaterial==null)
            { Debug.LogError("Hilltop scene references are incomplete.",this);enabled=false;return; }
            Session=new GameObject("Shared game session").AddComponent<GameSession>();
            Session.transform.SetParent(transform,false);
            Input=gameObject.AddComponent<GameInput>();Input.Session=Session;Input.ViewCamera=ViewCamera;
            // Preserve the shared observation range, movement, collision and release rules.
            Input.FollowSpeed=2;Input.OrbitSensitivity=.2f;
            View=gameObject.AddComponent<GameView>();View.Session=Session;View.Input=Input;View.Typeface=Typeface;View.Build();
            Session.Initialize(new HilltopStoneFactory(StoneMaterial,Shapes),Platform,Meadow);
            var share=gameObject.AddComponent<ShareController>();share.Session=Session;share.View=View;share.Input=Input;share.Initialize();
            var presentation=gameObject.AddComponent<HilltopInterface>();presentation.Initialize(this);
            gameObject.AddComponent<HilltopLighting>().Initialize(this);
        }

        void OnDestroy()
        {
            // Never persist a pipeline override into the team's project settings.
            if(pipelineChanged && QualitySettings.renderPipeline==Pipeline) QualitySettings.renderPipeline=previousPipeline;
            Application.targetFrameRate=previousFrameRate;Application.runInBackground=previousBackground;
        }

        sealed class HilltopStoneFactory : IStoneFactory
        {
            readonly StoneFactory shared;
            int sequence;
            static readonly Color[] colors={
                new Color(.62f,.62f,.58f),new Color(.44f,.51f,.51f),new Color(.70f,.66f,.59f),
                new Color(.54f,.59f,.57f),new Color(.64f,.59f,.54f),new Color(.40f,.47f,.49f)};
            public HilltopStoneFactory(Material material,StoneShapeSettings shapes){shared=new StoneFactory(material,shapes);}
            public async Task<Stone> CreateAsync(Vector3 position,Transform parent)
            {
                var stone=await shared.CreateAsync(position,parent);
                stone.OwnedMaterial.color=colors[sequence++%colors.Length];
                return stone;
            }
        }
    }
}
