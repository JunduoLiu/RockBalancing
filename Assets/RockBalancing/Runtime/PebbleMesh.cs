using System.Collections.Generic;
using UnityEngine;

namespace RockBalancing
{
    [System.Serializable]
    public sealed class StoneShapeSettings
    {
        [Range(0, 1)] public float FlatProbability = .85f;
        [Range(.12f, .4f)] public float MinimumThicknessRatio = .18f;
        [Range(.12f, .4f)] public float MaximumThicknessRatio = .28f;
    }

    public static class PebbleMesh
    {
        public static Mesh Create(int seed, StoneShapeSettings settings = null)
        {
            settings = settings ?? new StoneShapeSettings();
            // An independent draw preserves the previous seed-to-shape mapping for
            // irregular stones and makes probability-zero a useful comparison baseline.
            var choice = new System.Random(unchecked(seed ^ 0x5f3759df));
            if (choice.NextDouble() >= Mathf.Clamp01(settings.FlatProbability))
                return CreateIrregular(seed);
            return CreateFlat(seed, settings);
        }

        static Mesh CreateFlat(int seed, StoneShapeSettings settings)
        {
            var rng = new System.Random(seed);
            float Range(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
            float length = Range(.15f, .23f);
            float width = length * Range(.66f, .98f);
            float min = Mathf.Clamp(settings.MinimumThicknessRatio, .12f, .4f);
            float max = Mathf.Clamp(settings.MaximumThicknessRatio, min, .4f);
            float halfHeight = length * Range(min, max) * .5f;
            float cap = Range(.68f, .82f);
            float yaw = Range(0, Mathf.PI * 2);
            const int sides = 10;
            var vertices = new Vector3[sides * 3];
            // Homothetic convex rings form a bevelled pebble with genuine planar
            // support faces. Angle jitter varies its outline without concave dents.
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2 / sides + Range(-.12f, .12f);
                var p = new Vector3(Mathf.Cos(angle) * length * .5f, 0, Mathf.Sin(angle) * width * .5f);
                p = Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0) * p;
                vertices[i] = p * cap + Vector3.down * halfHeight;
                vertices[i + sides] = p;
                vertices[i + 2 * sides] = p * cap + Vector3.up * halfHeight;
            }
            var triangles = new List<int>();
            void Face(int a, int b, int c)
            {
                var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                if (Vector3.Dot(normal, vertices[a] + vertices[b] + vertices[c]) < 0)
                { int swap = b; b = c; c = swap; }
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
            }
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                for (int ring = 0; ring < 2; ring++)
                {
                    int a = ring * sides + i, b = ring * sides + next;
                    Face(a, b, b + sides); Face(a, b + sides, a + sides);
                }
            }
            for (int i = 1; i < sides - 1; i++)
            { Face(0, i, i + 1); Face(2 * sides, 2 * sides + i, 2 * sides + i + 1); }
            var mesh = new Mesh { name = "Flat pebble " + seed, vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        // A small convex hull gives the renderer and PhysX the same contact silhouette.
        // Thirty-two non-coplanar points produce at most sixty triangles.
        static Mesh CreateIrregular(int seed)
        {
            var rng = new System.Random(seed);
            float Range(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
            float length = Range(.15f, .23f);
            var scale = new Vector3(length * .5f, length * Range(.24f,.38f), length * Range(.33f,.49f));
            const int count = 32;
            var vertices = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float y = 1 - 2 * (i + .5f) / count;
                float r = Mathf.Sqrt(1 - y * y);
                float angle = i * 2.39996323f + Range(-.08f,.08f);
                vertices[i] = Vector3.Scale(new Vector3(Mathf.Cos(angle)*r, y, Mathf.Sin(angle)*r), scale) * Range(.94f,1.06f);
            }
            var triangles = new List<int>();
            for (int a = 0; a < count-2; a++)
            for (int b = a+1; b < count-1; b++)
            for (int c = b+1; c < count; c++)
            {
                var normal = Vector3.Cross(vertices[b]-vertices[a], vertices[c]-vertices[a]);
                if (normal.sqrMagnitude < 1e-14f) continue;
                bool positive=false, negative=false;
                for (int d=0; d<count && !(positive && negative); d++)
                {
                    if (d==a || d==b || d==c) continue;
                    float side = Vector3.Dot(normal, vertices[d]-vertices[a]);
                    positive |= side > 1e-10f; negative |= side < -1e-10f;
                }
                if (positive && negative) continue;
                triangles.Add(a);
                triangles.Add(positive ? c : b);
                triangles.Add(positive ? b : c);
            }
            var mesh = new Mesh { name = "Pebble " + seed, vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
        public static float Volume(Mesh mesh)
        {
            var vertices = mesh.vertices; var triangles = mesh.triangles;
            double volume = 0;
            for(int i=0;i<triangles.Length;i+=3)
                volume += Vector3.Dot(vertices[triangles[i]], Vector3.Cross(vertices[triangles[i+1]], vertices[triangles[i+2]]))/6.0;
            return Mathf.Abs((float)volume);
        }
    }
}
