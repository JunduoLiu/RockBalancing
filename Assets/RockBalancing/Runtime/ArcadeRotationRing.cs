using UnityEngine;
using UnityEngine.UI;

namespace RockBalancing
{
    // Draw the projection of each circle over the glass, using the same cabinet axes as the gesture.
    sealed class ArcadeRotationRing : Graphic
    {
        public static readonly (string Name, Vector3 Axis, Vector3 Radial, Color Color, Vector2 LabelPoint)[] Axes =
        {
            ("X", Vector3.right, Vector3.up, new Color(1, .51f, .32f), Vector2.right),
            ("Y", Vector3.up, Vector3.forward, new Color(.47f, .98f, .73f), Vector2.up),
            ("Z", Vector3.forward, Vector3.right, new Color(.58f, .77f, 1), new Vector2(-.707f, .707f))
        };
        const int Segments = 64;
        static readonly Vector2[] Circle = CirclePoints();

        RectTransform label;
        Vector2 center, u, v, cameraSide;
        float unit;
        int index;

        public static ArcadeRotationRing Create(GameView view, int index)
        {
            var axis = Axes[index];
            var rect = view.Panel(view.Root, axis.Name + " ring", Vector2.zero, Vector2.one, Color.clear, false);
            rect.SetAsFirstSibling();
            var ring = rect.gameObject.AddComponent<ArcadeRotationRing>();
            ring.index = index;
            ring.color = axis.Color;
            ring.raycastTarget = false;
            ring.label = view.Panel(rect, axis.Name + " label", Vector2.one * .5f, Vector2.one * .5f,
                new Color(.07f, .12f, .13f, .94f), false);
            view.Label(ring.label, axis.Name, Vector2.zero, Vector2.one, 13).color = axis.Color;
            rect.gameObject.SetActive(false);
            return ring;
        }

        public void Show(Stone stone, ArcadeCabinet cabinet, Camera camera)
        {
            if (stone == null)
            {
                gameObject.SetActive(false);
                return;
            }
            var origin = stone.transform.position;
            RingPlane(index, cabinet, out var worldU, out var worldV);
            float radius = Mathf.Max(.13f, stone.Shape.bounds.extents.magnitude + .035f) + index * .018f;
            var nextCenter = ToLocal(camera.WorldToScreenPoint(origin));
            var nextU = ToLocal(camera.WorldToScreenPoint(origin + worldU * radius)) - nextCenter;
            var nextV = ToLocal(camera.WorldToScreenPoint(origin + worldV * radius)) - nextCenter;
            var front = -camera.transform.forward;
            var nextCameraSide = new Vector2(Vector3.Dot(front, worldU), Vector3.Dot(front, worldV));
            gameObject.SetActive(true);
            float pixel = 1 / canvas.scaleFactor;
            if (nextCenter == center && nextU == u && nextV == v && nextCameraSide == cameraSide && pixel == unit)
                return;
            center = nextCenter;
            u = nextU;
            v = nextV;
            cameraSide = nextCameraSide;
            unit = pixel;
            var point = Axes[index].LabelPoint;
            var offset = u * point.x + v * point.y;
            label.sizeDelta = Vector2.one * Mathf.Max(17, 16 * pixel);
            label.anchoredPosition = center + offset + offset.normalized * (11 * pixel);
            SetVerticesDirty();
        }

        public static Vector2 PositiveTangent(int index, ArcadeCabinet cabinet, Camera camera, Vector3 origin,
            Vector2 grab)
        {
            RingPlane(index, cabinet, out var worldU, out var worldV);
            Vector2 midpoint = camera.WorldToScreenPoint(origin);
            var projectedU = (Vector2)camera.WorldToScreenPoint(origin + worldU) - midpoint;
            var projectedV = (Vector2)camera.WorldToScreenPoint(origin + worldV) - midpoint;
            var offset = grab - midpoint;
            float determinant = Cross(projectedU, projectedV);
            float scale = Mathf.Max(projectedU.magnitude, projectedV.magnitude);
            Vector2 radial;
            if (Mathf.Abs(determinant) > .001f * scale * scale && offset.magnitude > Mathf.Max(1, .01f * scale))
            {
                // Intersect the ray from the projected centre through the grab with
                // the ellipse. The coefficients give its radial direction in the ring plane.
                radial = new Vector2(Cross(offset, projectedV), Cross(projectedU, offset)) / determinant;
            }
            else
            {
                // At the centre, and when the front view shows a ring edge-on,
                // use the tangent of its camera-facing arc.
                var front = -camera.transform.forward;
                radial = new Vector2(Vector3.Dot(front, worldU), Vector3.Dot(front, worldV));
                if (radial.sqrMagnitude < .000001f) radial = Vector2.right;
            }
            return (projectedV * radial.x - projectedU * radial.y).normalized;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static void RingPlane(int index, ArcadeCabinet cabinet, out Vector3 u, out Vector3 v)
        {
            var axis = Axes[index];
            u = cabinet.transform.TransformDirection(axis.Radial);
            v = cabinet.transform.TransformDirection(Vector3.Cross(axis.Axis, axis.Radial));
        }

        Vector2 ToLocal(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, null, out var point);
            return point;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Draw(mesh, false);
            Draw(mesh, true);
        }

        void Draw(VertexHelper mesh, bool near)
        {
            var outline = new Color(.07f, .12f, .13f, near ? .93f : .38f);
            var stroke = color;
            stroke.a = near ? 1 : .46f;
            for (int i = 0; i < Segments; i++)
            {
                var a = Circle[i];
                var b = Circle[i + 1];
                if ((Vector2.Dot(a + b, cameraSide) >= 0) != near) continue;
                var start = center + u * a.x + v * a.y;
                var end = center + u * b.x + v * b.y;
                var normal = Vector2.Perpendicular(end - start).normalized * unit;
                Strip(mesh, start, end, normal * 2.4f, outline);
                Strip(mesh, start, end, normal * 1.4f, stroke);
            }
        }

        static void Strip(VertexHelper mesh, Vector2 start, Vector2 end, Vector2 offset, Color color)
        {
            int i = mesh.currentVertCount;
            mesh.AddVert(start - offset, color, Vector2.zero);
            mesh.AddVert(start + offset, color, Vector2.zero);
            mesh.AddVert(end + offset, color, Vector2.zero);
            mesh.AddVert(end - offset, color, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2);
            mesh.AddTriangle(i + 2, i + 3, i);
        }

        static Vector2[] CirclePoints()
        {
            var points = new Vector2[Segments + 1];
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * 2 * Mathf.PI / Segments;
                points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            points[Segments] = points[0];
            return points;
        }
    }
}
