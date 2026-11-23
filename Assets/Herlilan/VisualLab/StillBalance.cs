using System.Collections.Generic;
using System.Threading.Tasks;
using RockBalancing;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Herlilan.VisualLab
{
    [DefaultExecutionOrder(700)]
    public sealed class StillBalance : MonoBehaviour, IStoneFactory
    {
        [SerializeField] Camera viewCamera;
        [SerializeField] MeshCollider platform, meadow;
        [SerializeField] Transform grass;
        GameSession session;
        GameInput input;
        Canvas canvas;
        Stone tappedStone;
        Vector2 tapStart;
        float tapTime;
        RectTransform pads;
        [SerializeField] Shader veilShader;
        [SerializeField] Shader styleShader;
        RectTransform styleButton;
        Material styleMaterial;
        LandscapePass stylePass;
        enum Look { Default, Monochrome, Watercolor, Toon, PenAndInk }
        Look look;
        Material veil;
        Material baseSurface;
        Renderer baseRenderer;
        static readonly Color[] StoneColors = {
            new Color(.33f,.35f,.33f), new Color(.45f,.46f,.42f), new Color(.25f,.29f,.29f)
        };

        void Awake()
        {
            baseRenderer = platform.GetComponent<Renderer>();
            baseSurface = baseRenderer.sharedMaterial;
            session = gameObject.AddComponent<GameSession>();
            input = gameObject.AddComponent<GameInput>();
            input.Session = session;
            input.ViewCamera = viewCamera;
            input.OrbitSensitivity = .2f;
            BuildControls();
            session.Initialize(this, platform, meadow);
        }

        void BuildControls()
        {
            var root = new GameObject("Controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = viewCamera;
            canvas.planeDistance = viewCamera.nearClipPlane + .01f;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(430, 900);
            scaler.matchWidthOrHeight = .5f;
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("Control input", typeof(EventSystem), typeof(InputSystemUIInputModule)).transform.SetParent(transform, false);
            veil = new Material(veilShader) { name = "Still Balance / mist" };
            pads = new GameObject("Adjustments", typeof(RectTransform)).GetComponent<RectTransform>();
            pads.SetParent(root.transform, false);
            pads.anchorMin = pads.anchorMax = pads.pivot = new Vector2(.5f, 0);
            pads.sizeDelta = new Vector2(146, 86);
            AddPad(false, -30);
            AddPad(true, 48);
            styleMaterial = new Material(styleShader);
            stylePass = new LandscapePass(styleMaterial);
            styleButton = Frost(root.transform, "Cycle landscape style", Vector2.one * 52, .48f, true);
            styleButton.anchorMin = styleButton.anchorMax = styleButton.pivot = Vector2.one;
            var button = styleButton.gameObject.AddComponent<Button>();
            button.targetGraphic = styleButton.GetComponent<Image>();
            button.onClick.AddListener(CycleLook);
            for (int layer = 0; layer < 2; layer++)
            {
                var swatch = Frost(styleButton, "Swatch", new Vector2(17, 13), .86f, false);
                swatch.anchoredPosition = Vector2.one * (layer == 0 ? -3 : 3);
                swatch.localRotation = Quaternion.Euler(0, 0, -20);
            }
        }

        void CycleLook()
        {
            look = look == Look.PenAndInk ? Look.Default : look + 1;
            styleMaterial.SetFloat("_Look", (int)look);
        }

        void OnEnable() => RenderPipelineManager.beginCameraRendering += QueueLook;
        void OnDisable() => RenderPipelineManager.beginCameraRendering -= QueueLook;

        void QueueLook(ScriptableRenderContext context, Camera camera)
        {
            if (camera == viewCamera && look != Look.Default)
                camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(stylePass);
        }

        sealed class LandscapePass : ScriptableRenderPass
        {
            readonly Material material;
            public LandscapePass(Material material)
            {
                this.material = material;
                requiresIntermediateTexture = true;
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                var resources = frame.Get<UniversalResourceData>();
                var source = resources.activeColorTexture;
                var descriptor = graph.GetTextureDesc(source);
                descriptor.name = "Still Balance / landscape look";
                descriptor.clearBuffer = false;
                var result = graph.CreateTexture(descriptor);
                graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, result, material, 0), passName: descriptor.name);
                resources.cameraColor = result;
            }
        }

        void AddPad(bool vertical, float offset)
        {
            var surface = Frost(pads, vertical ? "Lift" : "Move", new Vector2(vertical ? 50 : 86, 86), .32f, true);
            surface.anchoredPosition = Vector2.right * offset;
            var pad = surface.gameObject.AddComponent<DragPad>();
            pad.VerticalOnly = vertical;
            pad.CanBegin = () => input.CanAdjust;
            if (vertical) { pad.Held = input.HoldHeight; pad.Changed = input.RaiseStone; }
            else { pad.Held = input.HoldPlanar; pad.Changed = input.MoveStone; }
            pad.Handle = Frost(surface, "Handle", vertical ? new Vector2(26, 14) : Vector2.one * 28, .78f, false);
        }

        RectTransform Frost(Transform parent, string label, Vector2 size, float alpha, bool interactive)
        {
            var image = new GameObject(label, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(parent, false);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.one * .5f;
            image.rectTransform.sizeDelta = size;
            image.material = veil;
            image.color = new Color(1, 1, 1, alpha);
            image.raycastTarget = interactive;
            return image.rectTransform;
        }

        void OnDestroy()
        {
            if (styleMaterial != null) Destroy(styleMaterial);
            if (veil != null) Destroy(veil);
        }

        public Task<Stone> CreateAsync(Vector3 position, Transform parent)
        {
            var bodyObject = new GameObject("River stone", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            bodyObject.transform.SetParent(parent, false);
            bodyObject.transform.position = position;
            var mesh = RiverMesh();
            bodyObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            var shape = bodyObject.GetComponent<MeshCollider>();
            shape.sharedMesh = mesh;
            shape.convex = true;
            shape.isTrigger = true;
            var friction = new PhysicsMaterial("River stone grip") {
                dynamicFriction = .65f, staticFriction = .8f, bounciness = .015f,
                frictionCombine = PhysicsMaterialCombine.Maximum, bounceCombine = PhysicsMaterialCombine.Minimum
            };
            shape.sharedMaterial = friction;
            var material = StoneFactory.MakeMaterial(StoneColors[Random.Range(0, StoneColors.Length)]);
            material.SetFloat("_Smoothness", .08f);
            bodyObject.GetComponent<Renderer>().sharedMaterial = material;
            var body = bodyObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.mass = Mathf.Max(.05f, PebbleMesh.Volume(mesh) * 2400);
            body.linearDamping = .08f;
            body.angularDamping = .3f;
            body.solverIterations = 12;
            body.solverVelocityIterations = 4;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var stone = bodyObject.AddComponent<Stone>();
            stone.OwnedMesh = mesh;
            stone.OwnedMaterial = material;
            stone.OwnedPhysicsMaterial = friction;
            return Task.FromResult(stone);
        }

        static Mesh RiverMesh()
        {
            const int sides = 24;
            float[] widths = { .38f, .84f, 1, .86f, .41f };
            float[] levels = { -1, -.71f, -.03f, .68f, 1 };
            float radius = Random.Range(.08f, .116f), phase = Random.Range(0f, 6.28f);
            var scale = new Vector3(radius, radius * Random.Range(.27f,.38f), radius * Random.Range(.68f,.85f));
            var vertices = new Vector3[sides * levels.Length];
            var faces = new List<int>();
            for (int ring = 0; ring < levels.Length; ring++)
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2 / sides;
                    float width = widths[ring] * (1 + .045f * Mathf.Sin(3 * angle + phase) + .025f * Mathf.Cos(5 * angle - phase));
                    int upper = ring * sides + side, next = ring * sides + (side + 1) % sides;
                    vertices[upper] = Vector3.Scale(scale, new Vector3(width * Mathf.Cos(angle) + .045f * levels[ring], levels[ring], width * Mathf.Sin(angle)));
                    if (ring > 0) faces.AddRange(new[] { upper - sides, upper, next, upper - sides, next, next - sides });
                }
            int last = vertices.Length - sides;
            for (int side = 1; side < sides - 1; side++)
                faces.AddRange(new[] { 0, side, side + 1, last, last + side + 1, last + side });
            var mesh = new Mesh { name = "River pebble", vertices = vertices, triangles = faces.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void Update()
        {
            ReadStoneTap();
            var keys = Keyboard.current;
            if (keys == null || !Application.isFocused || input.Modal || keys.ctrlKey.isPressed || keys.altKey.isPressed || keys.leftMetaKey.isPressed || keys.rightMetaKey.isPressed) return;
            if (keys.spaceKey.wasPressedThisFrame) input.ActOnStone(session.Pending, false);
            if (keys.backspaceKey.wasPressedThisFrame) input.ActOnStone(session.Pending, true);

        }

        void ReadStoneTap()
        {
            var pointer = Pointer.current;
            if (pointer == null || !Application.isFocused || !input.CanAdjust)
            {
                tappedStone = null;
                return;
            }
            var touch = Touchscreen.current;
            if (pointer == touch)
            {
                int contacts = 0;
                foreach (var finger in touch.touches) if (finger.press.isPressed) contacts++;
                if (contacts > 1 || touch.primaryTouch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    tappedStone = null;
                    return;
                }
            }
            Vector2 position = pointer.position.ReadValue();
            if (pointer.press.wasPressedThisFrame)
            {
                tapStart = position;
                tapTime = Time.unscaledTime;
                tappedStone = null;
                if (!RectTransformUtility.RectangleContainsScreenPoint(pads, position, viewCamera) &&
                    !RectTransformUtility.RectangleContainsScreenPoint(styleButton, position, viewCamera) &&
                    Physics.Raycast(viewCamera.ScreenPointToRay(position), out var hit, viewCamera.farClipPlane, ~0, QueryTriggerInteraction.Collide) &&
                    hit.collider == session.Pending.Shape)
                    tappedStone = session.Pending;
            }
            if (Vector2.Distance(position, tapStart) > Mathf.Max(8, Screen.height * .008f)) tappedStone = null;
            if (pointer.press.wasReleasedThisFrame && tappedStone != null)
            {
                input.ActOnStone(tappedStone, Time.unscaledTime - tapTime >= .65f);
                tappedStone = null;
            }
        }

        void LateUpdate()
        {
            grass.gameObject.SetActive(input.Occlusion == null || !input.Occlusion.IsFaded(meadow));
            if (viewCamera.transform.position.y > platform.bounds.max.y + .12f && baseRenderer.sharedMaterial != baseSurface)
                baseRenderer.sharedMaterial = baseSurface;
            float scale = Mathf.Max(.01f, canvas.scaleFactor);
            Rect safe = Screen.safeArea;
            styleButton.anchoredPosition = new Vector2((safe.xMax - Screen.width) / scale - 14, (safe.yMax - Screen.height) / scale - 14);
            pads.anchoredPosition = new Vector2((safe.center.x - Screen.width * .5f) / scale, safe.yMin / scale + 18);
        }
    }
}
