using System.Collections.Generic;
using UnityEngine;

namespace RockBalancing
{
    sealed class ArcadeFaceSnap
    {
        const float CaptureAngle = 3f;
        const float EscapeAngle = 3.4f;
        const float MinimumSupportUp = .01f;
        const float PlaneTolerance = .0015f;
        const float FadeSeconds = .24f;
        Quaternion? rawRotation;
        bool attached;
        double fadeUntil;

        public Stone Pending { get; private set; }
        public FaceMatch Match { get; private set; }
        public float Opacity => attached ? 1 : Mathf.Clamp01((float)(fadeUntil - Time.unscaledTimeAsDouble) / FadeSeconds);

        public readonly struct FaceMatch
        {
            public readonly StoneFace HangingFace;
            public readonly Collider Support;
            public readonly StoneFace SupportFace;

            public FaceMatch(StoneFace hangingFace, Collider support, StoneFace supportFace)
            {
                HangingFace = hangingFace;
                Support = support;
                SupportFace = supportFace;
            }

            Vector3 Target => -Support.transform.TransformDirection(SupportFace.LocalNormal);
            public float Error(Quaternion rotation) => Vector3.Angle(rotation * HangingFace.LocalNormal, Target);
            public Quaternion Aligned(Quaternion rotation) =>
                Quaternion.FromToRotation(rotation * HangingFace.LocalNormal, Target) * rotation;
        }

        public void EndDrag() => rawRotation = null;

        public void Clear()
        {
            Pending = null;
            Match = default;
            attached = false;
            fadeUntil = 0;
            EndDrag();
        }

        void Fade()
        {
            if (!attached) return;
            attached = false;
            fadeUntil = Time.unscaledTimeAsDouble + FadeSeconds;
        }

        public void Rotate(GameSession session, Stone stone, Quaternion change)
        {
            if (stone != Pending)
            {
                Clear();
                Pending = stone;
            }
            var previous = rawRotation ?? stone.transform.rotation;
            var requested = change * previous;

            if (attached && Match.Support != null && Match.Error(requested) <= EscapeAngle &&
                session.TryAlignRotation(stone, Match.Aligned(requested)))
            {
                rawRotation = requested;
                return;
            }
            bool leaving = attached;
            Fade();

            // Keep the game's existing swept contact restrictions for the player's rotation.
            // The magnetic correction uses the same sweep, but commits the whole correction or none.
            rawRotation = null;
            if (!session.Adjust(stone, stone.transform.position, requested)) return;
            if (!TryFindMatch(session, stone, previous, requested, leaving, out var match)) return;
            var aligned = match.Aligned(requested);
            if (!session.TryAlignRotation(stone, aligned)) return;

            Match = match;
            attached = true;
            // Start the escape distance from the corrected pose. The player can
            // cross the detent in either direction without dragging back the correction.
            rawRotation = aligned;
        }

        public void Refresh(GameSession session)
        {
            if (Pending != session.Pending)
            {
                Clear();
                return;
            }
            if (!attached) return;
            if (Match.Support == null || Match.Error(Pending.transform.rotation) > .2f ||
                !Reachable(Pending, Footprint(Pending), Match.Support, Match.SupportFace))
            {
                EndDrag();
                Fade();
            }
        }

        static bool TryFindMatch(GameSession session, Stone stone, Quaternion previous, Quaternion requested,
            bool leaving, out FaceMatch match)
        {
            FaceMatch best = default;
            float bestError = float.PositiveInfinity;
            bool bestEntered = false;
            List<Vector2> footprint = null;
            var hangingFaces = StoneFace.Of(stone.Shape);
            Consider(session.Base);
            foreach (var support in session.Stones)
                if (support != null && support.Released) Consider(support.Shape);
            match = best;
            return match.Support != null;

            void Consider(Collider support)
            {
                if (support == null || !support.enabled || !support.gameObject.activeInHierarchy) return;
                var bounds = stone.Shape.bounds;
                var supportBounds = support.bounds;
                if (bounds.min.x >= supportBounds.max.x || bounds.max.x <= supportBounds.min.x ||
                    bounds.min.z >= supportBounds.max.z || bounds.max.z <= supportBounds.min.z ||
                    bounds.max.y <= supportBounds.min.y) return;

                foreach (var supportFace in StoneFace.Of(support))
                {
                    if (support.transform.TransformDirection(supportFace.LocalNormal).y < MinimumSupportUp) continue;
                    foreach (var hangingFace in hangingFaces)
                    {
                        var candidate = new FaceMatch(hangingFace, support, supportFace);
                        float error = candidate.Error(requested);
                        if (error > CaptureAngle) continue;
                        float previousError = candidate.Error(previous);
                        if (error + .02f >= previousError) continue;
                        bool entered = previousError > CaptureAngle;
                        // A newly reached angle takes precedence over faces that
                        // were already near parallel, especially during breakaway.
                        if ((!entered && (leaving || bestEntered)) || (entered == bestEntered && error > bestError)) continue;
                        footprint ??= Footprint(stone);
                        if (!Reachable(stone, footprint, support, supportFace)) continue;
                        bestError = error;
                        bestEntered = entered;
                        best = candidate;
                    }
                }
            }
        }

        static List<Vector2> Footprint(Stone stone)
        {
            var points = new List<Vector2>();
            foreach (var face in StoneFace.Of(stone.Shape))
                foreach (var corner in face.Corners)
                    points.Add(Horizontal(stone.transform.TransformPoint(corner)));

            points.Sort((a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            var hull = new List<Vector2>();
            for (int i = 0; i < points.Count; i++) Append(points[i], 0);
            int lowerCount = hull.Count;
            for (int i = points.Count - 2; i >= 0; i--) Append(points[i], lowerCount - 1);
            if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);
            return hull;

            void Append(Vector2 point, int start)
            {
                while (hull.Count >= start + 2 && Cross(hull[^1] - hull[^2], point - hull[^1]) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(point);
            }
        }

        static bool Reachable(Stone stone, List<Vector2> footprint, Collider support, StoneFace face)
        {
            if (!support.enabled || !support.gameObject.activeInHierarchy || footprint.Count < 3) return false;
            var normal = support.transform.TransformDirection(face.LocalNormal);
            if (normal.y < MinimumSupportUp) return false;

            var overlap = new List<Vector2>();
            foreach (var corner in face.Corners) overlap.Add(Horizontal(support.transform.TransformPoint(corner)));
            for (int i = 0; i < footprint.Count && overlap.Count >= 3; i++)
                overlap = Clip(overlap, footprint[i], footprint[(i + 1) % footprint.Count]);
            if (overlap.Count < 3) return false;
            var center = Vector2.zero;
            foreach (var point in overlap) center += point;
            center /= overlap.Count;

            var planePoint = support.transform.TransformPoint(face.Corners[0]);
            float castHeight = stone.Shape.bounds.max.y + .002f;
            if (HitsFace(center)) return true;
            foreach (var corner in overlap)
                if (HitsFace(Vector2.Lerp(center, corner, .8f))) return true;
            return false;

            bool HitsFace(Vector2 point)
            {
                float y = planePoint.y - (normal.x * (point.x - planePoint.x) +
                    normal.z * (point.y - planePoint.z)) / normal.y;
                return y < castHeight &&
                    Physics.Raycast(new Vector3(point.x, castHeight, point.y), Vector3.down, out var hit,
                        castHeight - y + PlaneTolerance, ~0, QueryTriggerInteraction.Ignore) &&
                    hit.collider == support && Mathf.Abs(hit.point.y - y) < PlaneTolerance;
            }
        }

        static List<Vector2> Clip(List<Vector2> polygon, Vector2 start, Vector2 end)
        {
            var clipped = new List<Vector2>();
            var edge = end - start;
            var previous = polygon[^1];
            float lastDistance = Cross(edge, previous - start);
            foreach (var point in polygon)
            {
                float distance = Cross(edge, point - start);
                if ((lastDistance < 0) != (distance < 0))
                    clipped.Add(Vector2.Lerp(previous, point, lastDistance / (lastDistance - distance)));
                if (distance >= 0) clipped.Add(point);
                previous = point;
                lastDistance = distance;
            }
            return clipped;
        }

        static Vector2 Horizontal(Vector3 point) => new(point.x, point.z);
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
