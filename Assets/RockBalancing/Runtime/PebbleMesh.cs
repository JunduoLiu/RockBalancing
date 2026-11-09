using System.Collections.Generic;
using UnityEngine;

namespace RockBalancing
{
    public static class PebbleMesh
    {
        // A small convex hull gives the renderer and PhysX the same contact silhouette.
        // Thirty-two non-coplanar points produce at most sixty triangles.
        public static Mesh Create(int seed)
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
