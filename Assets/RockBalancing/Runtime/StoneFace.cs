using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace RockBalancing
{
    // Recover the outline of each load-bearing plane from the same mesh PhysX cooks.
    sealed class StoneFace
    {
        static readonly ConditionalWeakTable<Mesh, StoneFace[]> MeshFaces = new();
        public readonly Vector3 LocalNormal;
        public readonly Vector3[] Corners;

        StoneFace(Vector3 normal, Vector3[] corners)
        {
            LocalNormal = normal;
            Corners = corners;
        }

        public static StoneFace[] Of(Collider collider) => collider is MeshCollider mesh && mesh.sharedMesh != null
            ? MeshFaces.GetValue(mesh.sharedMesh, ReadMesh)
            : System.Array.Empty<StoneFace>();

        static StoneFace[] ReadMesh(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var faces = new List<FaceBuilder>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                var cross = Vector3.Cross(b - a, c - a);
                float area = cross.magnitude;
                if (area <= 1e-9f) continue;
                var normal = cross / area;
                var face = faces.Find(candidate => candidate.ContainsPlane(normal, a));
                if (face == null) faces.Add(face = new FaceBuilder(normal, a));
                face.AddEdge(a, b);
                face.AddEdge(b, c);
                face.AddEdge(c, a);
            }
            return faces.ConvertAll(face => face.ToFace()).ToArray();
        }

        sealed class FaceBuilder
        {
            readonly Vector3 normal, origin;
            readonly List<(Vector3 Start, Vector3 End)> boundary = new();

            public FaceBuilder(Vector3 normal, Vector3 origin)
            {
                this.normal = normal;
                this.origin = origin;
            }

            public bool ContainsPlane(Vector3 direction, Vector3 point) =>
                Vector3.Dot(direction, normal) > .999999f && Mathf.Abs(Vector3.Dot(normal, point - origin)) < .000005f;

            public void AddEdge(Vector3 start, Vector3 end)
            {
                // Compare positions because flat-shaded meshes duplicate vertices at edges.
                int reverse = boundary.FindIndex(edge => edge.Start == end && edge.End == start);
                if (reverse >= 0) boundary.RemoveAt(reverse);
                else boundary.Add((start, end));
            }

            public StoneFace ToFace()
            {
                var corners = new Vector3[boundary.Count];
                var edge = boundary[0];
                for (int i = 0; i < corners.Length; i++)
                {
                    corners[i] = edge.Start;
                    edge = boundary.Find(next => next.Start == edge.End);
                }
                return new StoneFace(normal, corners);
            }
        }
    }
}
