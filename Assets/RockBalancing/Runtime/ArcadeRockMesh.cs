using System.Collections.Generic;
using UnityEngine;

namespace RockBalancing
{
    internal static class ArcadeRockMesh
    {
        internal static Mesh CreateStone(int seed, StoneShapeSettings settings)
        {
            var rng = new System.Random(seed);
            float Range(float low, float high) => Mathf.Lerp(low, high, (float)rng.NextDouble());
            float length = Range(.15f, .23f);
            bool broad = rng.NextDouble() < Mathf.Clamp01(settings.FlatProbability);
            float width = length * (broad ? Range(.65f, .94f) : Range(.42f, .53f));
            for (int attempt = 0; attempt < 24; attempt++)
            {
                var planes = SidePlanes(length, width);
                planes.Add(new Plane(Vector3.down, Vector3.zero));

                var corners = new List<Vector2> { new(-1, -1), new(-1, 1), new(1, -1), new(1, 1) };
                int cuts = rng.Next(4);
                for (int i = 0; i < cuts; i++)
                {
                    int index = rng.Next(corners.Count);
                    var corner = corners[index];
                    corners.RemoveAt(index);
                    float x = corner.x * Range(.82f, 1.18f);
                    float z = corner.y * Range(.82f, 1.18f);
                    planes.Add(CutPlane(new Vector3(x / length, Range(-.5f, .5f) / width, z / width),
                        (Mathf.Abs(x) + Mathf.Abs(z)) * .5f - Range(.20f, .35f)));
                }

                float thickness = width * Range(.31f, .50f);
                bool parallelTop = settings.FlatProbability >= 1 || rng.NextDouble() < .16;
                float slopeX = parallelTop ? 0 : Range(-.45f, .45f) * width / length;
                float slopeZ = parallelTop ? 0 : Range(-.55f, .55f);
                planes.Add(CutPlane(new Vector3(slopeX, 1, slopeZ), thickness));
                if (!parallelTop && cuts < 3 && rng.NextDouble() < .55)
                    planes.Add(CutPlane(new Vector3(-slopeX, 1, -slopeZ), thickness * Range(.78f, 1.22f)));

                var hull = ArcadeStoneHull.FromPlanes(planes);
                if (hull.IsPlayable) return hull.ToMesh("Arcade stone " + seed);
            }

            var fallback = SidePlanes(length, width);
            fallback.Add(new Plane(Vector3.down, Vector3.zero));
            fallback.Add(CutPlane(Vector3.up, width * .42f));
            return ArcadeStoneHull.FromPlanes(fallback).ToMesh("Arcade stone " + seed);
        }

        static List<Plane> SidePlanes(float length, float width) => new()
        {
            CutPlane(Vector3.left, length * .5f), CutPlane(Vector3.right, length * .5f),
            CutPlane(Vector3.back, width * .5f), CutPlane(Vector3.forward, width * .5f)
        };

        static Plane CutPlane(Vector3 normal, float offset) => new(normal.normalized, -offset / normal.magnitude);

        internal static Mesh CreatePedestal()
        {
            var outline = new[]
            {
                new Vector3(-.38f, 0, -.5f), new Vector3(.37f, 0, -.5f),
                new Vector3(.5f, 0, -.39f), new Vector3(.5f, 0, .32f),
                new Vector3(.41f, 0, .46f), new Vector3(.30f, 0, .5f),
                new Vector3(-.38f, 0, .5f), new Vector3(-.5f, 0, .36f),
                new Vector3(-.5f, 0, -.35f)
            };
            return BevelledHull("Arcade base stone", outline, .16f, .89f, .94f, Vector3.zero);
        }

        // Each cap is a scaled, translated copy of the convex outline. Each pair of
        // corresponding edges spans a plane, including after PhysX cooks the convex hull.
        static Mesh BevelledHull(string name, Vector3[] outline, float height, float bottomScale,
            float topScale, Vector3 offset)
        {
            var bottom = new Vector3[outline.Length];
            var top = new Vector3[outline.Length];
            for (int i = 0; i < outline.Length; i++)
            {
                bottom[i] = outline[i] * bottomScale - offset + Vector3.down * (height * .5f);
                top[i] = outline[i] * topScale + offset + Vector3.up * (height * .5f);
            }
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var faceTones = new List<Color32>();
            void Face(byte tone, params Vector3[] polygon)
            {
                int first = vertices.Count;
                vertices.AddRange(polygon);
                for (int i = 0; i < polygon.Length; i++) faceTones.Add(new Color32(tone, tone, tone, 255));
                for (int i = 1; i < polygon.Length - 1; i++)
                {
                    triangles.Add(first);
                    triangles.Add(first + i);
                    triangles.Add(first + i + 1);
                }
            }
            Face(110, bottom);
            for (int i = 0; i < outline.Length; i++)
            {
                int next = (i + 1) % outline.Length;
                // Uniform color per plane keeps small bevels legible in the same lighting band.
                byte alternating = (byte)((i % 2) * 54);
                Face((byte)(64 + alternating), bottom[i], outline[i], outline[next], bottom[next]);
                Face((byte)(192 - alternating), outline[i], top[i], top[next], outline[next]);
            }
            System.Array.Reverse(top);
            Face(255, top);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetColors(faceTones);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
