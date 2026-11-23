using UnityEngine;
using UnityEngine.UI;

namespace RockBalancing
{
    // A glass overlay lets the player see the face even when the stone hides its own underside.
    [RequireComponent(typeof(CanvasRenderer))]
    sealed class ArcadeFaceOutline : Graphic
    {
        const float Dash = 5, DashCycle = 8, VisibilitySample = 4;
        static readonly Color Solid = new(.47f, 1, .89f);
        static readonly Color Hidden = new(.47f, 1, .89f, .49f);
        static readonly Color Shadow = new(.05f, .18f, .18f, .88f);
        static readonly Color HiddenShadow = new(.05f, .18f, .18f, .40f);

        Camera projectionCamera;
        Stone pending;
        ArcadeFaceSnap.FaceMatch match;
        Matrix4x4 pendingPose, supportPose, view, projection;
        Vector2Int screen;
        float unitsPerPixel;

        public static ArcadeFaceOutline Create(GameView view)
        {
            var rect = view.Panel(view.Root, "Matched faces", Vector2.zero, Vector2.one, Color.clear, false);
            rect.SetAsFirstSibling();
            var outline = rect.gameObject.AddComponent<ArcadeFaceOutline>();
            outline.raycastTarget = false;
            rect.gameObject.SetActive(false);
            return outline;
        }

        public void Show(ArcadeFaceSnap snap, Camera camera)
        {
            var pair = snap.Match;
            if (snap.Pending == null || pair.Support == null || snap.Opacity <= 0)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            canvasRenderer.SetAlpha(snap.Opacity);
            float scale = 1 / canvas.scaleFactor;
            var size = new Vector2Int(camera.pixelWidth, camera.pixelHeight);
            var nextPending = snap.Pending.transform.localToWorldMatrix;
            var nextSupport = pair.Support.transform.localToWorldMatrix;
            if (pending == snap.Pending && match.Support == pair.Support && match.HangingFace == pair.HangingFace &&
                match.SupportFace == pair.SupportFace && pendingPose == nextPending && supportPose == nextSupport &&
                view == camera.worldToCameraMatrix && projection == camera.projectionMatrix && screen == size &&
                unitsPerPixel == scale) return;

            projectionCamera = camera;
            pending = snap.Pending;
            match = pair;
            pendingPose = nextPending;
            supportPose = nextSupport;
            view = camera.worldToCameraMatrix;
            projection = camera.projectionMatrix;
            screen = size;
            unitsPerPixel = scale;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (pending == null || match.Support == null || projectionCamera == null) return;
            Physics.SyncTransforms();
            DrawFace(mesh, pendingPose, match.HangingFace);
            DrawFace(mesh, supportPose, match.SupportFace);
        }

        void DrawFace(VertexHelper mesh, Matrix4x4 pose, StoneFace face)
        {
            float travelled = 0;
            for (int i = 0; i < face.Corners.Length; i++)
            {
                var a = pose.MultiplyPoint3x4(face.Corners[i]);
                var b = pose.MultiplyPoint3x4(face.Corners[(i + 1) % face.Corners.Length]);
                var start = projectionCamera.WorldToScreenPoint(a);
                var end = projectionCamera.WorldToScreenPoint(b);
                if (start.z <= projectionCamera.nearClipPlane || end.z <= projectionCamera.nearClipPlane) continue;
                float pixels = Vector2.Distance(start, end);
                if (pixels < .01f) continue;

                int samples = Mathf.CeilToInt(pixels / VisibilitySample);
                Vector2 origin = ToCanvas(start);
                Vector2 direction = (ToCanvas(end) - origin) / pixels;
                var normal = Vector2.Perpendicular(direction).normalized * unitsPerPixel;
                for (int sample = 0; sample < samples; sample++)
                {
                    float from = pixels * sample / samples;
                    float to = pixels * (sample + 1) / samples;
                    var point = Vector3.Lerp(a, b, (sample + .5f) / samples);
                    if (BehindPending(point))
                        DrawDashes(mesh, origin, direction, normal, from, to, travelled);
                    else
                        Stroke(mesh, origin + direction * from, origin + direction * to, normal, false);
                }
                travelled += pixels;
            }
        }

        bool BehindPending(Vector3 point)
        {
            var ray = projectionCamera.ScreenPointToRay(projectionCamera.WorldToScreenPoint(point));
            // Stop short of the surveyed edge so a silhouette does not shadow itself.
            float distance = Vector3.Distance(ray.origin, point) - .0006f;
            return pending.Shape.Raycast(ray, out _, distance);
        }

        Vector2 ToCanvas(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, null, out var point);
            return point;
        }

        static void DrawDashes(VertexHelper mesh, Vector2 origin, Vector2 direction, Vector2 normal,
            float from, float to, float travelled)
        {
            int first = Mathf.FloorToInt((travelled + from) / DashCycle);
            int last = Mathf.FloorToInt((travelled + to) / DashCycle);
            for (int dash = first; dash <= last; dash++)
            {
                float start = dash * DashCycle - travelled;
                float a = Mathf.Max(from, start);
                float b = Mathf.Min(to, start + Dash);
                if (b > a) Stroke(mesh, origin + direction * a, origin + direction * b, normal, true);
            }
        }

        static void Stroke(VertexHelper mesh, Vector2 start, Vector2 end, Vector2 normal, bool hidden)
        {
            Strip(mesh, start, end, normal * (hidden ? 1.02f : 1.5f), hidden ? HiddenShadow : Shadow);
            Strip(mesh, start, end, normal * (hidden ? .52f : .86f), hidden ? Hidden : Solid);
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
    }
}
