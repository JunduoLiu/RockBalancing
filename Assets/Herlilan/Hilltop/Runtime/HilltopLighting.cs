using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Herlilan.Hilltop
{
    // Runtime-only copies: experimenting never edits the authored sky or volume.
    public sealed class HilltopLighting : MonoBehaviour
    {
        public static readonly string[] Names = { "Morning", "Noon", "Golden hour", "Overcast" };
        [Range(.1f, 8)] public float TransitionSeconds = 2.5f;
        [Range(5, 30)] public float CycleSeconds = 10;
        public bool AutoCycle;
        public int Selected { get; private set; }
        public bool IsTransitioning { get; private set; }
        public float Elevation => current.angles.x;
        public float Azimuth => current.angles.y;
        public float Intensity => current.intensity;
        public float Exposure => current.exposure;

        struct Look
        {
            public Vector2 angles;
            public Color sun, sky, equator, ground, horizon, zenith, below, fog;
            public float intensity, shadow, exposure, temperature, saturation, fogEnd;
        }

        HilltopScene scene;
        Light sun;
        Volume volume;
        Material originalSky, runtimeSky;
        VolumeProfile originalProfile, runtimeProfile;
        ColorAdjustments color;
        WhiteBalance white;
        Look original, current, from, target;
        Quaternion originalRotation;
        float elapsed, cycleElapsed;
        bool ready;

        public void Initialize(HilltopScene owner)
        {
            if (ready) return;
            scene = owner;
            sun = RenderSettings.sun;
            volume = owner.GetComponentInChildren<Volume>();
            if (sun == null || volume == null || RenderSettings.skybox == null) return;
            originalSky = RenderSettings.skybox;
            originalProfile = volume.sharedProfile;
            runtimeSky = new Material(originalSky) { name = "Hilltop lighting preview sky" };
            runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            if (originalProfile != null)
                foreach (var component in originalProfile.components)
                    runtimeProfile.components.Add(Instantiate(component));
            if (!runtimeProfile.TryGet(out color)) color = runtimeProfile.Add<ColorAdjustments>(true);
            if (!runtimeProfile.TryGet(out white)) white = runtimeProfile.Add<WhiteBalance>(true);
            volume.sharedProfile = runtimeProfile;
            RenderSettings.skybox = runtimeSky;
            originalRotation = sun.transform.rotation;
            original = new Look {
                angles = new Vector2(sun.transform.eulerAngles.x, sun.transform.eulerAngles.y),
                sun = sun.color, intensity = sun.intensity, shadow = sun.shadowStrength,
                sky = RenderSettings.ambientSkyColor, equator = RenderSettings.ambientEquatorColor,
                ground = RenderSettings.ambientGroundColor, fog = RenderSettings.fogColor,
                horizon = runtimeSky.GetColor("_Horizon"), zenith = runtimeSky.GetColor("_Zenith"),
                below = runtimeSky.GetColor("_Ground"), fogEnd = RenderSettings.fogEndDistance,
                exposure = color.postExposure.value, temperature = white.temperature.value,
                saturation = color.saturation.value
            };
            current = original;
            ready = true;
            Select(0, true);
        }

        static Color C(float r, float g, float b) => new Color(r, g, b);
        static Look Preset(int index)
        {
            switch (index)
            {
                case 1: return new Look {
                    angles = new Vector2(72, -20), sun = C(1, .99f, .96f), intensity = 1.85f, shadow = .8f,
                    sky = C(.66f,.77f,.86f), equator = C(.57f,.66f,.60f), ground = C(.35f,.43f,.32f),
                    horizon = C(.76f,.87f,.95f), zenith = C(.36f,.65f,.87f), below = C(.70f,.81f,.83f),
                    fog = C(.76f,.86f,.91f), fogEnd = 38, exposure = .1f, temperature = -3, saturation = 2 };
                case 2: return new Look {
                    angles = new Vector2(16, -115), sun = C(1,.72f,.43f), intensity = 1.65f, shadow = .86f,
                    sky = C(.61f,.63f,.75f), equator = C(.61f,.56f,.48f), ground = C(.31f,.35f,.29f),
                    horizon = C(.95f,.73f,.57f), zenith = C(.51f,.58f,.76f), below = C(.72f,.68f,.60f),
                    fog = C(.86f,.73f,.63f), fogEnd = 25, exposure = .3f, temperature = 8, saturation = -3 };
                case 3: return new Look {
                    angles = new Vector2(48, 20), sun = C(.85f,.91f,1), intensity = .55f, shadow = .25f,
                    sky = C(.72f,.77f,.81f), equator = C(.63f,.68f,.65f), ground = C(.39f,.45f,.38f),
                    horizon = C(.80f,.84f,.85f), zenith = C(.64f,.72f,.77f), below = C(.72f,.78f,.77f),
                    fog = C(.77f,.82f,.82f), fogEnd = 23, exposure = .4f, temperature = -5, saturation = -8 };
                default: return new Look {
                    angles = new Vector2(30, -42), sun = C(1,.94f,.81f), intensity = 1.35f, shadow = .75f,
                    sky = C(.64f,.74f,.78f), equator = C(.54f,.62f,.58f), ground = C(.34f,.41f,.32f),
                    horizon = C(.84f,.87f,.86f), zenith = C(.50f,.71f,.84f), below = C(.72f,.81f,.82f),
                    fog = C(.79f,.85f,.85f), fogEnd = 30, exposure = .25f, temperature = 0, saturation = 0 };
            }
        }

        public void Select(int index, bool instant = false)
        {
            if (!ready) return;
            Selected = Mathf.Clamp(index, 0, Names.Length - 1);
            from = current; target = Preset(Selected); elapsed = 0; cycleElapsed = 0; IsTransitioning = !instant;
            if (instant) { elapsed = TransitionSeconds; current = target; Apply(current); }
        }

        public void SetManual(float elevation, float azimuth, float intensity, float exposure)
        {
            if (!ready) return;
            AutoCycle = false;
            current.angles = new Vector2(Mathf.Clamp(elevation, 5, 85), Mathf.Clamp(azimuth, -180, 180));
            current.intensity = Mathf.Clamp(intensity, 0, 3);
            current.exposure = Mathf.Clamp(exposure, -1, 1.5f);
            from = target = current; elapsed = TransitionSeconds; IsTransitioning = false;
            Apply(current);
        }

        void Update()
        {
            if (!ready) return;
            var keys = Keyboard.current;
            if (Application.isFocused && keys != null && !scene.Input.Modal &&
                !keys.ctrlKey.isPressed && !keys.altKey.isPressed && !keys.leftMetaKey.isPressed && !keys.rightMetaKey.isPressed)
            {
                for (int i = 0; i < 4; i++)
                    if (keys[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) { AutoCycle = false; Select(i); }
                if (keys.lKey.wasPressedThisFrame) { AutoCycle = !AutoCycle; cycleElapsed = 0; }
            }
            if (AutoCycle)
            {
                cycleElapsed += Time.unscaledDeltaTime;
                if (cycleElapsed >= Mathf.Max(CycleSeconds, TransitionSeconds + 1)) Select((Selected + 1) % Names.Length);
            }
            if (!IsTransitioning) return;
            elapsed = Mathf.Min(elapsed + Time.unscaledDeltaTime, TransitionSeconds);
            current = Blend(from, target, Mathf.SmoothStep(0, 1, elapsed / Mathf.Max(.01f, TransitionSeconds)));
            Apply(current);
            if (elapsed >= TransitionSeconds) IsTransitioning = false;
        }

        static Look Blend(Look a, Look b, float t) => new Look {
            angles = new Vector2(Mathf.LerpAngle(a.angles.x,b.angles.x,t),Mathf.LerpAngle(a.angles.y,b.angles.y,t)),
            sun = Color.Lerp(a.sun,b.sun,t), sky = Color.Lerp(a.sky,b.sky,t), equator = Color.Lerp(a.equator,b.equator,t),
            ground = Color.Lerp(a.ground,b.ground,t), horizon = Color.Lerp(a.horizon,b.horizon,t),
            zenith = Color.Lerp(a.zenith,b.zenith,t), below = Color.Lerp(a.below,b.below,t), fog = Color.Lerp(a.fog,b.fog,t),
            intensity = Mathf.Lerp(a.intensity,b.intensity,t), shadow = Mathf.Lerp(a.shadow,b.shadow,t),
            exposure = Mathf.Lerp(a.exposure,b.exposure,t), temperature = Mathf.Lerp(a.temperature,b.temperature,t),
            saturation = Mathf.Lerp(a.saturation,b.saturation,t), fogEnd = Mathf.Lerp(a.fogEnd,b.fogEnd,t)
        };

        void Apply(Look value)
        {
            sun.transform.rotation = Quaternion.Euler(value.angles.x,value.angles.y,0);
            sun.color = value.sun; sun.intensity = value.intensity; sun.shadowStrength = value.shadow;
            RenderSettings.ambientSkyColor = value.sky; RenderSettings.ambientEquatorColor = value.equator;
            RenderSettings.ambientGroundColor = value.ground; RenderSettings.fogColor = value.fog;
            RenderSettings.fogEndDistance = value.fogEnd;
            runtimeSky.SetColor("_Horizon",value.horizon); runtimeSky.SetColor("_Zenith",value.zenith);
            runtimeSky.SetColor("_Ground",value.below);
            color.postExposure.Override(value.exposure); color.saturation.Override(value.saturation);
            white.temperature.Override(value.temperature);
        }

        void OnEnable() { if (scene != null && !ready) Initialize(scene); }
        void OnDisable()
        {
            if (!ready) return;
            if (sun != null) { Apply(original); sun.transform.rotation = originalRotation; }
            if (RenderSettings.skybox == runtimeSky) RenderSettings.skybox = originalSky;
            if (volume != null && volume.sharedProfile == runtimeProfile) volume.sharedProfile = originalProfile;
            foreach (var component in runtimeProfile.components) Destroy(component);
            Destroy(runtimeProfile); Destroy(runtimeSky); ready = false;
        }
    }
}
