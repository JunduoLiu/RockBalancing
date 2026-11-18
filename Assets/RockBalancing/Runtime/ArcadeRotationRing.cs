using UnityEngine;
using UnityEngine.UI;

namespace RockBalancing
{
    // Draw the projection of each circle over the glass, using the same cabinet axes as the gesture.
    sealed class ArcadeRotationRing : Graphic
    {
        const int Segments = 64;
        static readonly Vector2[] Circle = CirclePoints();

        RectTransform label;
        Vector2 center, u, v, cameraSide;
        Vector2 labelPoint;
        float unit, radiusOffset;
        ArcadeAxis axis;

        public static ArcadeRotationRing Create(GameView view, ArcadeAxis axis)
        {
            var name = axis.ToString();
            var rect = view.Panel(view.Root, name + " ring", Vector2.zero, Vector2.one, Color.clear, false);
            rect.SetAsFirstSibling();
            var ring = rect.gameObject.AddComponent<ArcadeRotationRing>();
            ring.axis = axis;
            (ring.color, ring.labelPoint, ring.radiusOffset) = axis switch
            {
                ArcadeAxis.X => (new Color(1, .51f, .32f), Vector2.right, 0f),
                ArcadeAxis.Y => (new Color(.47f, .98f, .73f), Vector2.up, .018f),
                _ => (new Color(.58f, .77f, 1), new Vector2(-.707f, .707f), .036f)
            };
            ring.raycastTarget = false;
            ring.label = view.Panel(rect, name + " label", Vector2.one * .5f, Vector2.one * .5f,
                new Color(.07f, .12f, .13f, .94f), false);
            view.Label(ring.label, name, Vector2.zero, Vector2.one, 13).color = ring.color;
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
            float radius = Mathf.Max(.13f, stone.Shape.bounds.extents.magnitude + .035f) + radiusOffset;
            var projection = ArcadeRotationGeometry.Project(axis, cabinet.transform, camera, stone.transform.position, radius);
            var nextCenter = ToLocal(projection.Center);
            var nextU = ToLocal(projection.Center + projection.U) - nextCenter;
            var nextV = ToLocal(projection.Center + projection.V) - nextCenter;
            gameObject.SetActive(true);
            float pixel = 1 / canvas.scaleFactor;
            if (nextCenter == center && nextU == u && nextV == v && projection.CameraSide == cameraSide && pixel == unit)
                return;
            center = nextCenter;
            u = nextU;
            v = nextV;
            cameraSide = projection.CameraSide;
            unit = pixel;
            var offset = u * labelPoint.x + v * labelPoint.y;
            label.sizeDelta = Vector2.one * Mathf.Max(17, 16 * pixel);
            label.anchoredPosition = center + offset + offset.normalized * (11 * pixel);
            SetVerticesDirty();
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
