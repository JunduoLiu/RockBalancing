using System;
using UnityEngine;

namespace RockBalancing
{
    // Only renderer material instances change. Colliders, stone transforms and the
    // support graph are never touched. The shader is a Resources asset so WebGL
    // does not depend on a stripped runtime Lit transparency variant.
    public sealed class ObservationOcclusion : MonoBehaviour
    {
        sealed class Surface
        {
            public Collider Collider;
            public Renderer Renderer;
            public Material[] Original, Faded;
            public float Opacity = 1, ClearTime;
            public bool Obstructed;
            public float MinimumOpacity;
        }
        GameSession session;
        Surface[] surfaces = Array.Empty<Surface>();
        int opaqueScopes;
        const float RestoreDelay = .18f;
        const float FadeDuration = .16f;

        public void Initialize(GameSession value)
        {
            session = value;
            var shader = Resources.Load<Shader>("ObservationFade");
            surfaces = new[] { Create(value.Base, .08f, shader), Create(value.Ground, 0, shader) };
        }
        static Surface Create(Collider collider, float opacity, Shader shader)
        {
            var surface = new Surface { Collider = collider, MinimumOpacity = opacity };
            if (collider == null) return surface;
            surface.Renderer = collider.GetComponent<Renderer>();
            if (surface.Renderer == null) return surface;
            surface.Original = surface.Renderer.sharedMaterials;
            surface.Faded = new Material[surface.Original.Length];
            for (int i = 0; i < surface.Faded.Length; i++)
            {
                surface.Faded[i] = new Material(shader) { name = "Observation fade (instance)" };
                surface.Faded[i].SetColor("_BaseColor", surface.Original[i].color);
            }
            return surface;
        }
        public bool IsFaded(Collider collider)
        {
            foreach (var surface in surfaces)
                if (surface.Collider == collider && surface.Opacity < .999f) return true;
            return false;
        }
        public float OpacityFor(Collider collider)
        {
            foreach (var surface in surfaces) if (surface.Collider == collider) return surface.Opacity;
            return 1;
        }
        public void UpdateView(Camera camera, float clearance)
        {
            if (session == null) return;
            foreach (var surface in surfaces)
            {
                if (surface.Collider == null || surface.Renderer == null) continue;
                var bounds = surface.Collider.bounds;
                // A wider exit threshold plus a clear-time hold prevents edge flicker.
                bounds.Expand(surface.Obstructed ? .06f : .02f);
                bool blocked = bounds.SqrDistance(camera.transform.position) <= clearance * clearance;
                foreach (var stone in session.Stones)
                {
                    if (blocked) break;
                    if (stone == null || !stone.gameObject.activeInHierarchy) continue;
                    var stoneBounds = stone.Surface.bounds;
                    for (int sample = 0; sample < 9 && !blocked; sample++)
                    {
                        var point = stoneBounds.center;
                        if (sample < 8) point += Vector3.Scale(stoneBounds.extents * .9f,
                            new Vector3((sample & 1) == 0 ? -1 : 1, (sample & 2) == 0 ? -1 : 1, (sample & 4) == 0 ? -1 : 1));
                        var screen = camera.WorldToViewportPoint(point);
                        if (screen.z <= camera.nearClipPlane || screen.x < -.1f || screen.x > 1.1f || screen.y < -.1f || screen.y > 1.1f) continue;
                        var offset = point - camera.transform.position;
                        blocked = bounds.IntersectRay(new Ray(camera.transform.position, offset.normalized), out float entry)
                            && entry < offset.magnitude - .01f;
                    }
                }
                if (blocked) { surface.Obstructed = true; surface.ClearTime = 0; }
                else if ((surface.ClearTime += Time.unscaledDeltaTime) >= RestoreDelay) surface.Obstructed = false;
                float goal = surface.Obstructed ? surface.MinimumOpacity : 1;
                surface.Opacity = Mathf.MoveTowards(surface.Opacity, goal, Time.unscaledDeltaTime / FadeDuration);
                Apply(surface);
            }
        }
        void Apply(Surface surface)
        {
            if (surface.Renderer == null) return;
            bool original = opaqueScopes > 0 || surface.Opacity >= .999f;
            var materials = original ? surface.Original : surface.Faded;
            // Avoid allocating and reassigning the renderer's material array every frame.
            if (surface.Renderer.sharedMaterial != materials[0]) surface.Renderer.sharedMaterials = materials;
            if (!original) foreach (var material in surface.Faded) material.SetFloat("_Opacity", surface.Opacity);
        }
        public IDisposable OpaqueScene()
        {
            opaqueScopes++;
            foreach (var surface in surfaces) Apply(surface);
            return new RestoreScope(this);
        }
        sealed class RestoreScope : IDisposable
        {
            ObservationOcclusion owner;
            public RestoreScope(ObservationOcclusion value) { owner = value; }
            public void Dispose()
            {
                if (owner == null) return;
                owner.opaqueScopes--;
                foreach (var surface in owner.surfaces) owner.Apply(surface);
                owner = null;
            }
        }
        void OnDisable()
        {
            foreach (var surface in surfaces)
                if (surface.Renderer != null) surface.Renderer.sharedMaterials = surface.Original;
        }
        void OnDestroy()
        {
            OnDisable();
            foreach (var surface in surfaces)
                if (surface.Faded != null) foreach (var material in surface.Faded) Destroy(material);
        }
    }
}
