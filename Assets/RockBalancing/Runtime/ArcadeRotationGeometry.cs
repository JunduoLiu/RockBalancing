using UnityEngine;

namespace RockBalancing
{
    internal enum ArcadeAxis { X, Y, Z }

    internal static class ArcadeRotationGeometry
    {
        static readonly ArcadeAxis[] GesturePriority = { ArcadeAxis.X, ArcadeAxis.Y, ArcadeAxis.Z };
        public static int AxisCount => GesturePriority.Length;

        public static void GetTangents(Transform cabinet, Camera camera, Vector3 origin, Vector2 grab, Vector2[] tangents)
        {
            for (int i = 0; i < GesturePriority.Length; i++)
                tangents[i] = PositiveTangent(Project(GesturePriority[i], cabinet, camera, origin, 1), grab);
        }

        public static (Vector3 Axis, Vector2 Tangent) GetDragAxis(Transform cabinet, Vector2 drag, Vector2[] tangents)
        {
            var direction = drag.normalized;
            int choice = 0;
            float alignment = Vector2.Dot(direction, tangents[choice]);
            for (int i = 1; i < GesturePriority.Length; i++)
            {
                float candidate = Vector2.Dot(direction, tangents[i]);
                // Near ties keep the earlier cabinet axis.
                if (Mathf.Abs(candidate) <= Mathf.Abs(alignment) + .001f) continue;
                choice = i;
                alignment = candidate;
            }
            return (cabinet.TransformDirection(Plane(GesturePriority[choice]).Normal), tangents[choice]);
        }

        public static (Vector2 Center, Vector2 U, Vector2 V, Vector2 CameraSide) Project(
            ArcadeAxis axis, Transform cabinet, Camera camera, Vector3 origin, float radius)
        {
            var (normal, radial) = Plane(axis);
            var worldU = cabinet.TransformDirection(radial);
            var worldV = cabinet.TransformDirection(Vector3.Cross(normal, radial));
            Vector2 center = camera.WorldToScreenPoint(origin);
            Vector2 u = (Vector2)camera.WorldToScreenPoint(origin + worldU * radius) - center;
            Vector2 v = (Vector2)camera.WorldToScreenPoint(origin + worldV * radius) - center;
            var front = -camera.transform.forward;
            return (center, u, v, new Vector2(Vector3.Dot(front, worldU), Vector3.Dot(front, worldV)));
        }

        static Vector2 PositiveTangent((Vector2 Center, Vector2 U, Vector2 V, Vector2 CameraSide) ring, Vector2 grab)
        {
            var (center, u, v, cameraSide) = ring;
            var offset = grab - center;
            float determinant = Cross(u, v);
            float scale = Mathf.Max(u.magnitude, v.magnitude);
            Vector2 radial;
            if (Mathf.Abs(determinant) > .001f * scale * scale && offset.magnitude > Mathf.Max(1, .01f * scale))
            {
                // Intersect the line through the grab with the projected ellipse.
                radial = new Vector2(Cross(offset, v), Cross(u, offset)) / determinant;
            }
            else
            {
                // A central grab or edge-on ring uses the tangent of the forward arc.
                radial = cameraSide.sqrMagnitude < .000001f ? Vector2.right : cameraSide;
            }
            return (v * radial.x - u * radial.y).normalized;
        }

        static (Vector3 Normal, Vector3 Radial) Plane(ArcadeAxis axis) => axis switch
        {
            ArcadeAxis.X => (Vector3.right, Vector3.up),
            ArcadeAxis.Y => (Vector3.up, Vector3.forward),
            _ => (Vector3.forward, Vector3.right)
        };

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
