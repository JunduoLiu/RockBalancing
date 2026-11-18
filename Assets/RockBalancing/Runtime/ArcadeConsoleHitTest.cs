using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RockBalancing
{
    // Touch picking uses the rendered model without adding colliders to stone physics.
    sealed class ArcadeConsoleHitTest
    {
        readonly List<Part> parts = new List<Part>();

        public ArcadeConsoleHitTest(Transform cabinet, params Transform[] controls)
        {
            foreach (var filter in cabinet.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<Renderer>();
                var mesh = filter.sharedMesh;
                var materials = renderer.sharedMaterials;
                var opaque = new List<int>();
                for (int i = 0; i < mesh.subMeshCount; i++)
                    if (materials[i].renderQueue < (int)RenderQueue.Transparent)
                        opaque.AddRange(mesh.GetTriangles(i));
                if (opaque.Count > 0)
                    parts.Add(new Part(renderer, mesh.vertices, opaque.ToArray(), Array.IndexOf(controls, filter.transform) >= 0));
            }
        }

        public bool TryHitControl(Ray ray, float range, out float distance)
        {
            distance = range;
            bool hitControl = false;
            foreach (var part in parts)
                if (part.Hit(ray, ref distance)) hitControl = part.IsControl;
            return hitControl;
        }

        sealed class Part
        {
            readonly Renderer renderer;
            readonly Vector3[] vertices;
            readonly int[] triangles;
            public bool IsControl { get; }

            public Part(Renderer renderer, Vector3[] vertices, int[] triangles, bool isControl)
            {
                this.renderer = renderer;
                this.vertices = vertices;
                this.triangles = triangles;
                IsControl = isControl;
            }

            public bool Hit(Ray ray, ref float distance)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    !renderer.bounds.IntersectRay(ray, out float boundsDistance) || boundsDistance > distance) return false;

                var toLocal = renderer.worldToLocalMatrix;
                var origin = toLocal.MultiplyPoint3x4(ray.origin);
                // Leave the transformed direction unnormalized so t stays in world units.
                var direction = toLocal.MultiplyVector(ray.direction);
                bool hit = false;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var a = vertices[triangles[i]];
                    var ab = vertices[triangles[i + 1]] - a;
                    var ac = vertices[triangles[i + 2]] - a;
                    var cross = Vector3.Cross(direction, ac);
                    float determinant = Vector3.Dot(ab, cross);
                    if (determinant <= 1e-8f) continue;
                    var fromA = origin - a;
                    float u = Vector3.Dot(fromA, cross) / determinant;
                    if (u < 0 || u > 1) continue;
                    var otherCross = Vector3.Cross(fromA, ab);
                    float v = Vector3.Dot(direction, otherCross) / determinant;
                    if (v < 0 || u + v > 1) continue;
                    float intersection = Vector3.Dot(ac, otherCross) / determinant;
                    if (intersection < 0 || intersection >= distance) continue;
                    distance = intersection;
                    hit = true;
                }
                return hit;
            }
        }
    }
}
