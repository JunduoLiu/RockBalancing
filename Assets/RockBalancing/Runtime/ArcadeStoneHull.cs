using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RockBalancing
{
    internal sealed class ArcadeStoneHull
    {
        const float PositionTolerance = .000002f;

        readonly List<Vector3> points = new();
        readonly List<Face> faces = new();

        sealed class Face
        {
            public readonly Vector3 Normal;
            public readonly int[] Corners;
            public readonly float Area;

            public Face(Vector3 normal, int[] corners, float area)
            {
                Normal = normal;
                Corners = corners;
                Area = area;
            }

            public bool HasEdge(int a, int b)
            {
                for (int i = 0; i < Corners.Length; i++)
                    if (Corners[i] == b && Corners[(i + 1) % Corners.Length] == a) return true;
                return false;
            }
        }

        internal static ArcadeStoneHull FromPlanes(List<Plane> planes)
        {
            var hull = new ArcadeStoneHull();
            // Intersections of three cut planes form the vertices of the convex hull.
            // Retain only corners inside every plane, so edges are shared before tessellation.
            for (int a = 0; a < planes.Count - 2; a++)
            for (int b = a + 1; b < planes.Count - 1; b++)
            for (int c = b + 1; c < planes.Count; c++)
            {
                if (!TryIntersect(planes[a], planes[b], planes[c], out var point)) continue;
                if (planes.Any(plane => plane.GetDistanceToPoint(point) > PositionTolerance)) continue;
                if (hull.points.Any(existing => (existing - point).sqrMagnitude < PositionTolerance * PositionTolerance))
                    continue;
                hull.points.Add(point);
            }

            foreach (var plane in planes) hull.AddFace(plane);
            return hull;
        }

        static bool TryIntersect(Plane a, Plane b, Plane c, out Vector3 point)
        {
            var bc = Vector3.Cross(b.normal, c.normal);
            float determinant = Vector3.Dot(a.normal, bc);
            if (Mathf.Abs(determinant) < .00001f)
            {
                point = default;
                return false;
            }

            var ca = Vector3.Cross(c.normal, a.normal);
            var ab = Vector3.Cross(a.normal, b.normal);
            point = -(a.distance * bc + b.distance * ca + c.distance * ab) / determinant;
            return true;
        }

        void AddFace(Plane plane)
        {
            var corners = new List<int>();
            var center = Vector3.zero;
            for (int i = 0; i < points.Count; i++)
            {
                if (Mathf.Abs(plane.GetDistanceToPoint(points[i])) > PositionTolerance) continue;
                corners.Add(i);
                center += points[i];
            }
            if (corners.Count < 3) return;

            center /= corners.Count;
            var x = (points[corners[0]] - center).normalized;
            var y = Vector3.Cross(plane.normal, x);
            float Angle(int index)
            {
                var delta = points[index] - center;
                return Mathf.Atan2(Vector3.Dot(delta, y), Vector3.Dot(delta, x));
            }
            int[] boundary = corners.OrderBy(Angle).ToArray();
            float area = 0;
            for (int i = 1; i < boundary.Length - 1; i++)
                area += Vector3.Cross(points[boundary[i]] - points[boundary[0]],
                    points[boundary[i + 1]] - points[boundary[0]]).magnitude * .5f;
            if (area > 0) faces.Add(new Face(plane.normal, boundary, area));
        }

        internal bool IsPlayable => faces.Count >= 5 && faces.Count <= 9
            && HasPlayableDimensions() && HasPlayableFaces() && HasSupportFace();

        bool HasPlayableDimensions()
        {
            // The cut hull is constructed along its own length (X), height (Y) and width (Z).
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points) bounds.Encapsulate(point);
            float thicknessRatio = bounds.size.y / bounds.size.z;
            return thicknessRatio >= .25f && thicknessRatio <= .60f && bounds.size.z <= bounds.size.x;
        }

        bool HasPlayableFaces()
        {
            float totalArea = faces.Sum(face => face.Area);
            foreach (var face in faces)
            {
                if (face.Area < totalArea * .03f) return false;
                for (int edge = 0; edge < face.Corners.Length; edge++)
                {
                    int a = face.Corners[edge];
                    int b = face.Corners[(edge + 1) % face.Corners.Length];
                    var neighbor = faces.Find(other => other.HasEdge(a, b));
                    if (neighbor == null) return false;
                    float interiorAngle = 180f - Vector3.Angle(face.Normal, neighbor.Normal);
                    if (interiorAngle < 25f || interiorAngle > 155f) return false;
                }
            }
            return true;
        }

        bool HasSupportFace()
        {
            float signedVolume = 0;
            var centroidSum = Vector3.zero;
            foreach (var face in faces)
            {
                var origin = points[face.Corners[0]];
                for (int i = 1; i < face.Corners.Length - 1; i++)
                {
                    var b = points[face.Corners[i]];
                    var c = points[face.Corners[i + 1]];
                    float volume = Vector3.Dot(origin, Vector3.Cross(b, c));
                    signedVolume += volume;
                    centroidSum += (origin + b + c) * volume;
                }
            }
            if (signedVolume <= 0) return false;

            var centerOfMass = centroidSum / (4 * signedVolume);
            foreach (var face in faces)
            {
                var projected = centerOfMass - face.Normal *
                    Vector3.Dot(centerOfMass - points[face.Corners[0]], face.Normal);
                bool inside = true;
                for (int i = 0; i < face.Corners.Length && inside; i++)
                {
                    var a = points[face.Corners[i]];
                    var b = points[face.Corners[(i + 1) % face.Corners.Length]];
                    inside = Vector3.Dot(Vector3.Cross((b - a).normalized, projected - a), face.Normal) > PositionTolerance;
                }
                if (inside) return true;
            }
            return false;
        }

        internal Mesh ToMesh(string name)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var normals = new List<Vector3>();
            var colors = new List<Color32>();
            var minimum = points.Aggregate(Vector3.Min);
            var maximum = points.Aggregate(Vector3.Max);
            var center = (minimum + maximum) * .5f;
            foreach (var face in faces)
            {
                int first = vertices.Count;
                byte tone = (byte)Mathf.Lerp(110, 255, Mathf.Clamp01((face.Normal.y + 1) * .5f));
                foreach (int corner in face.Corners)
                {
                    vertices.Add(points[corner] - center);
                    normals.Add(face.Normal);
                    colors.Add(new Color32(tone, tone, tone, 255));
                }
                for (int i = 1; i < face.Corners.Length - 1; i++)
                {
                    triangles.Add(first);
                    triangles.Add(first + i);
                    triangles.Add(first + i + 1);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }
    }
}
