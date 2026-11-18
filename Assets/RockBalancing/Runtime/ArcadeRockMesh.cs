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
            bool flat = rng.NextDouble() < Mathf.Clamp01(settings.FlatProbability);
            float length = Range(.15f, .23f);
            float width = length * (flat ? Range(.68f, .96f) : Range(.37f, .48f));
            float min = Mathf.Clamp(settings.MinimumThicknessRatio, .12f, .4f);
            float max = Mathf.Clamp(settings.MaximumThicknessRatio, min, .4f);
            float height = length * (flat ? Range(min, max) : Range(.33f, .45f));
            var heading = Quaternion.Euler(0, Range(0, 360), 0);
            var outline = new Vector3[rng.Next(6, 9)];
            // Ordered points on an ellipse give irregular edge lengths while keeping the hull convex.
            for (int i = 0; i < outline.Length; i++)
            {
                float angle = (i + Range(-.2f, .2f)) * 2 * Mathf.PI / outline.Length;
                outline[i] = heading * new Vector3(Mathf.Cos(angle) * length * .5f, 0, Mathf.Sin(angle) * width * .5f);
            }
            var offset = heading * new Vector3(length * Range(-.04f, .04f), 0, width * Range(-.03f, .03f));
            return BevelledHull("Arcade stone " + seed, outline, height, Range(.65f, .83f), Range(.66f, .85f), offset);
        }

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
