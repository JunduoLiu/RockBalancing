using System.Collections.Generic;
using UnityEngine;

namespace RockBalancing
{
    // Each vertical line through a convex stone crosses one bottom face. Project
    // those faces and the tower's top faces onto XZ to find open paths between them.
    sealed class ArcadeLandingFaces
    {
        const float ContactTolerance = .0002f;
        const float HeightTolerance = .00002f;
        static readonly IComparer<Projection> HighestFirst = Comparer<Projection>.Create(
            (first, second) => second.High.CompareTo(first.High));
        Collider[] nearby = new Collider[16];
        Dictionary<Collider, Matrix4x4> poses = new(), surveyed = new();
        readonly Projection bottom = new();
        readonly List<Projection> tops = new();
        readonly List<Vector2> opening = new(), mask = new(), intersection = new(), clipped = new();
        readonly Stack<List<Vector2>> polygonPool = new();
        List<List<Vector2>> exposed = new(), remaining = new();
        Collider pendingShape;
        Matrix4x4 pendingPose;

        public int Count { get; private set; }
        public Projection this[int index] => tops[index];

        public void Clear()
        {
            pendingShape = null;
            bottom.Collider = null;
            poses.Clear();
            surveyed.Clear();
            for (int i = 0; i < Count; i++) tops[i].Collider = null;
            Count = 0;
        }

        public bool Update(GameSession session, Stone pending)
        {
            Physics.SyncTransforms();
            var shape = pending.Shape;
            var pose = shape.transform.localToWorldMatrix;
            var bounds = shape.bounds;
            var pivot = shape.transform.position;
            var offset = bounds.center - pivot;
            // Query the space that any candidate rotation can occupy. A correction
            // can bring a face into the footprint or reveal a blocker at its edge.
            float radius = (bounds.extents + Vector3.Max(offset, -offset)).magnitude;
            float floor = session.Ground == null ? session.Base.bounds.min.y : session.Ground.bounds.min.y;
            var min = pivot - Vector3.one * radius;
            min.y = Mathf.Min(floor, min.y);
            var column = new Bounds();
            column.SetMinMax(min, pivot + Vector3.one * radius);
            // Include interpolation and touching faces at the boundary of the query.
            column.Expand(.01f);
            int found = Physics.OverlapBoxNonAlloc(column.center, column.extents, nearby,
                Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            while (found == nearby.Length)
            {
                System.Array.Resize(ref nearby, nearby.Length * 2);
                found = Physics.OverlapBoxNonAlloc(column.center, column.extents, nearby,
                    Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            }

            bool changed = pendingShape != shape || pendingPose != pose;
            surveyed.Clear();
            for (int i = 0; i < found; i++)
            {
                var collider = nearby[i];
                nearby[i] = null;
                if (collider != session.Base &&
                    (!collider.TryGetComponent<Stone>(out var support) || support.Session != session || !support.Released)) continue;
                var matrix = collider.transform.localToWorldMatrix;
                surveyed.Add(collider, matrix);
                changed |= !poses.TryGetValue(collider, out var previous) || previous != matrix;
            }
            if (!changed && surveyed.Count == poses.Count) return false;
            (poses, surveyed) = (surveyed, poses);
            pendingShape = shape;
            pendingPose = pose;
            int topCount = 0;
            foreach (var pair in poses)
                topCount = ProjectTops(pair.Key, pair.Value, topCount);
            for (int i = topCount; i < Count; i++) tops[i].Collider = null;
            Count = topCount;
            // Start with the upper faces so a covered stack can reject its lower
            // faces as soon as the first blocker covers the whole opening.
            tops.Sort(0, Count, HighestFirst);
            return true;
        }

        int ProjectTops(Collider collider, Matrix4x4 pose, int count)
        {
            var normalMatrix = pose.inverse.transpose;
            foreach (var face in StoneFace.Of(collider))
            {
                var normal = normalMatrix.MultiplyVector(face.LocalNormal).normalized;
                if (normal.y <= .00001f) continue;
                if (count == tops.Count) tops.Add(new Projection());
                tops[count++].Set(collider, face, pose, normal, upward: true);
            }
            return count;
        }

        public bool ProjectHangingFace(StoneFace face, Quaternion rotation)
        {
            var transform = pendingShape.transform;
            var correction = rotation * Quaternion.Inverse(transform.rotation);
            var pivot = transform.position;
            var pose = Matrix4x4.TRS(pivot, correction, Vector3.one) * Matrix4x4.Translate(-pivot) * pendingPose;
            var normal = pose.inverse.transpose.MultiplyVector(face.LocalNormal).normalized;
            if (normal.y >= -.00001f) return false;
            bottom.Set(pendingShape, face, pose, normal, upward: false);
            return true;
        }

        public bool Reaches(Projection support)
        {
            if (!bottom.Footprint.Overlaps(support.Footprint)) return false;
            Copy(support.Polygon, opening);
            Intersect(opening, bottom.Polygon);
            Clip(opening, bottom.Above(support, ContactTolerance));
            if (!HasArea(opening)) return false;

            var first = RentPolygon();
            Copy(opening, first);
            exposed.Add(first);
            for (int j = 0; j < Count && exposed.Count > 0; j++)
            {
                var blocker = tops[j];
                if (blocker.Collider == support.Collider || blocker.High <= support.Low + HeightTolerance ||
                    blocker.Low > bottom.High + ContactTolerance || !blocker.Footprint.Overlaps(support.Footprint) ||
                    !blocker.Footprint.Overlaps(bottom.Footprint)) continue;
                Copy(blocker.Polygon, mask);
                Clip(mask, blocker.Above(support, -HeightTolerance));
                Clip(mask, bottom.Above(blocker, ContactTolerance));
                if (HasArea(mask)) SubtractMask();
            }
            bool reaches = exposed.Count > 0;
            foreach (var polygon in exposed) polygonPool.Push(polygon);
            exposed.Clear();
            return reaches;
        }

        void SubtractMask()
        {
            foreach (var region in exposed)
            {
                Copy(region, intersection);
                Intersect(intersection, mask);
                if (!HasArea(intersection))
                {
                    remaining.Add(region);
                    continue;
                }

                // Each edge peels off a disjoint exposed piece. The polygon left
                // after all edges lies inside the blocker and is discarded.
                for (int i = 0; i < mask.Count && HasArea(region); i++)
                {
                    var end = mask[(i + 1) % mask.Count];
                    if (mask[i].Equals(end)) continue;
                    var edge = HalfPlane.Along(mask[i], end);
                    var outside = RentPolygon();
                    ClipInto(region, edge.Reverse, outside);
                    if (HasArea(outside)) remaining.Add(outside);
                    else polygonPool.Push(outside);
                    Clip(region, edge);
                }
                polygonPool.Push(region);
            }
            exposed.Clear();
            (exposed, remaining) = (remaining, exposed);
        }

        List<Vector2> RentPolygon() => polygonPool.Count == 0 ? new List<Vector2>() : polygonPool.Pop();

        static void Copy(List<Vector2> source, List<Vector2> output)
        {
            output.Clear();
            output.AddRange(source);
        }

        static bool HasArea(List<Vector2> polygon)
        {
            if (polygon.Count < 3) return false;
            var origin = polygon[0];
            float area = 0;
            for (int i = 2; i < polygon.Count; i++)
            {
                var a = polygon[i - 1] - origin;
                var b = polygon[i] - origin;
                area += a.x * b.y - a.y * b.x;
            }
            return area > 1e-9f;
        }

        void Intersect(List<Vector2> polygon, List<Vector2> boundary)
        {
            for (int i = 0; i < boundary.Count && polygon.Count >= 3; i++)
                Clip(polygon, HalfPlane.Along(boundary[i], boundary[(i + 1) % boundary.Count]));
        }

        void Clip(List<Vector2> polygon, HalfPlane plane)
        {
            if (polygon.Count == 0) return;
            ClipInto(polygon, plane, clipped);
            Copy(clipped, polygon);
        }

        static void ClipInto(List<Vector2> polygon, HalfPlane plane, List<Vector2> output)
        {
            output.Clear();
            var previous = polygon[^1];
            float lastDistance = plane.Distance(previous);
            foreach (var point in polygon)
            {
                float distance = plane.Distance(point);
                if ((lastDistance < 0) != (distance < 0))
                    output.Add(Vector2.Lerp(previous, point, lastDistance / (lastDistance - distance)));
                if (distance >= 0) output.Add(point);
                previous = point;
                lastDistance = distance;
            }
        }

        internal readonly struct HalfPlane
        {
            readonly Vector2 origin, normal;
            readonly float offset;

            public HalfPlane(Vector2 origin, Vector2 normal, float offset)
            {
                this.origin = origin;
                this.normal = normal;
                this.offset = offset;
            }

            public static HalfPlane Along(Vector2 start, Vector2 end) =>
                new(start, Vector2.Perpendicular(end - start), 0);
            public HalfPlane Reverse => new(origin, -normal, -offset);
            public float Distance(Vector2 point) => Vector2.Dot(normal, point - origin) + offset;
        }

        public sealed class Projection
        {
            public Collider Collider;
            public StoneFace Face;
            public Vector3 Normal;
            public Rect Footprint;
            public float Low, High;
            public readonly List<Vector2> Polygon = new();
            Vector3 origin;
            Vector2 gradient;

            public void Set(Collider collider, StoneFace face, Matrix4x4 pose, Vector3 normal, bool upward)
            {
                Collider = collider;
                Face = face;
                Normal = normal;
                origin = pose.MultiplyPoint3x4(face.Corners[0]);
                gradient = -new Vector2(normal.x, normal.z) / normal.y;
                Low = High = origin.y;
                var min = new Vector2(origin.x, origin.z);
                var max = min;
                Polygon.Clear();
                for (int i = 0; i < face.Corners.Length; i++)
                {
                    // Top faces project clockwise on XZ. Keep both sets counterclockwise for clipping.
                    int corner = upward ? face.Corners.Length - 1 - i : i;
                    var point = pose.MultiplyPoint3x4(face.Corners[corner]);
                    var xz = new Vector2(point.x, point.z);
                    min = Vector2.Min(min, xz);
                    max = Vector2.Max(max, xz);
                    Low = Mathf.Min(Low, point.y);
                    High = Mathf.Max(High, point.y);
                    Polygon.Add(xz);
                }
                Footprint = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }

            internal HalfPlane Above(Projection other, float tolerance)
            {
                var reference = new Vector2(other.origin.x, other.origin.z);
                float height = origin.y + Vector2.Dot(gradient, reference - new Vector2(origin.x, origin.z));
                return new HalfPlane(reference, gradient - other.gradient, height - other.origin.y + tolerance);
            }
        }
    }
}
