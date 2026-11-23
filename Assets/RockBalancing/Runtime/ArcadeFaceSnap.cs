using System.Collections.Generic;
using UnityEngine;

namespace RockBalancing
{
    sealed class ArcadeFaceSnap
    {
        const float CaptureAngle = 3f;
        const float EscapeAngle = 3.4f;
        const float MinimumSupportUp = .01f;
        const float ParallelTolerance = .2f;
        const float FadeSeconds = .24f;
        readonly ArcadeLandingFaces landing = new();
        readonly List<FaceMatch> matches = new();
        Quaternion? rawRotation;
        bool attached;
        double fadeUntil;

        public Stone Pending { get; private set; }
        public IReadOnlyList<FaceMatch> Matches => matches;
        public float Opacity => attached ? 1 : Mathf.Clamp01((float)(fadeUntil - Time.unscaledTimeAsDouble) / FadeSeconds);

        public readonly struct FaceMatch
        {
            public readonly StoneFace HangingFace;
            public readonly Collider Support;
            public readonly StoneFace SupportFace;
            readonly Vector3 target;

            public FaceMatch(StoneFace hangingFace, ArcadeLandingFaces.Projection support)
            {
                HangingFace = hangingFace;
                Support = support.Collider;
                SupportFace = support.Face;
                target = -support.Normal;
            }

            public float Error(Quaternion rotation) => Vector3.Angle(rotation * HangingFace.LocalNormal, target);
            public Quaternion Aligned(Quaternion rotation) =>
                Quaternion.FromToRotation(rotation * HangingFace.LocalNormal, target) * rotation;
        }

        public void EndDrag() => rawRotation = null;

        public void Clear()
        {
            Pending = null;
            matches.Clear();
            landing.Clear();
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
            else Refresh(session);

            var previous = rawRotation ?? stone.transform.rotation;
            var requested = change * previous;
            var match = attached ? matches[0] : default;
            if (attached && match.Error(requested) <= EscapeAngle &&
                session.TryAlignRotation(stone, match.Aligned(requested)))
            {
                rawRotation = requested;
                return;
            }
            bool leaving = attached;
            Fade();

            // The player's rotation can stop partway at a collision. The magnetic
            // correction uses the same sweep and commits only if the full turn fits.
            rawRotation = null;
            if (!session.Adjust(stone, stone.transform.position, requested)) return;
            landing.Update(session, stone);
            if (!TryFindMatch(stone, previous, requested, leaving, out match)) return;
            var aligned = match.Aligned(requested);
            if (!session.TryAlignRotation(stone, aligned)) return;

            landing.Update(session, stone);
            if (!UpdateGroup(match.HangingFace))
            {
                // The correction can move a grazing footprint beyond its support.
                session.TryAlignRotation(stone, requested);
                return;
            }
            attached = true;
            // Start the escape distance from the corrected pose in either direction.
            rawRotation = aligned;
        }

        public void Refresh(GameSession session)
        {
            if (Pending == null) return;
            if (Pending != session.Pending)
            {
                Clear();
                return;
            }
            if (!attached || !landing.Update(session, Pending)) return;
            if (UpdateGroup(matches[0].HangingFace)) return;
            EndDrag();
            Fade();
        }

        bool UpdateGroup(StoneFace hangingFace)
        {
            var orientation = Pending.transform.rotation;
            bool found = false;
            for (int i = 0; i < landing.Count; i++)
            {
                var support = landing[i];
                if (support.Normal.y < MinimumSupportUp) continue;
                var candidate = new FaceMatch(hangingFace, support);
                if (candidate.Error(orientation) > ParallelTolerance || !landing.Reaches(support)) continue;
                if (!found) matches.Clear();
                matches.Add(candidate);
                found = true;
            }
            return found;
        }

        bool TryFindMatch(Stone stone, Quaternion previous, Quaternion requested, bool leaving, out FaceMatch match)
        {
            match = default;
            float bestError = float.PositiveInfinity;
            bool bestEntered = false;
            var hangingFaces = StoneFace.Of(stone.Shape);
            for (int i = 0; i < landing.Count; i++)
            {
                var support = landing[i];
                if (support.Normal.y < MinimumSupportUp) continue;
                foreach (var hangingFace in hangingFaces)
                {
                    var candidate = new FaceMatch(hangingFace, support);
                    float error = candidate.Error(requested);
                    if (error > CaptureAngle) continue;
                    float previousError = candidate.Error(previous);
                    if (error + .02f >= previousError) continue;
                    bool entered = previousError > CaptureAngle;
                    // Give newly reached angles priority during the same sample.
                    if ((!entered && (leaving || bestEntered)) || (entered == bestEntered && error > bestError)) continue;
                    if (!landing.Reaches(support)) continue;
                    bestError = error;
                    bestEntered = entered;
                    match = candidate;
                }
            }
            return match.Support != null;
        }
    }
}
