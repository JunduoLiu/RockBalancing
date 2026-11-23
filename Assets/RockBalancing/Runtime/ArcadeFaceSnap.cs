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
        const float HandoffMargin = .05f;
        const float FadeSeconds = .24f;
        readonly ArcadeLandingFaces landing = new();
        readonly List<(FaceMatch Match, Quaternion Approach, float Progress, float Error, bool Entered)> candidates = new();
        List<FaceMatch> matches = new(), trialMatches = new();
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
            internal readonly Vector3 Target;

            public FaceMatch(StoneFace hangingFace, ArcadeLandingFaces.Projection support)
            {
                HangingFace = hangingFace;
                Support = support.Collider;
                SupportFace = support.Face;
                Target = -support.Normal;
            }

            public float Error(Quaternion rotation)
            {
                var normal = rotation * HangingFace.LocalNormal;
                return Mathf.Atan2(Vector3.Cross(normal, Target).magnitude, Vector3.Dot(normal, Target)) * Mathf.Rad2Deg;
            }

            public Quaternion Aligned(Quaternion rotation) =>
                Quaternion.FromToRotation(rotation * HangingFace.LocalNormal, Target) * rotation;
        }

        readonly struct RotationArc
        {
            readonly Quaternion start, end;
            readonly Vector3 axis;
            readonly float degrees;

            public RotationArc(Quaternion start, Quaternion end)
            {
                this.start = start;
                this.end = end;
                var turn = end * Quaternion.Inverse(start);
                var imaginary = new Vector3(turn.x, turn.y, turn.z);
                if (turn.w < 0) imaginary = -imaginary;
                float magnitude = imaginary.magnitude;
                axis = magnitude > 0 ? imaginary / magnitude : Vector3.zero;
                degrees = 2 * Mathf.Atan2(magnitude, Mathf.Abs(turn.w)) * Mathf.Rad2Deg;
            }

            public float ClosestProgress(FaceMatch match)
            {
                var normal = start * match.HangingFace.LocalNormal;
                // The component along the rotation axis is constant. In the plane
                // across the axis, the closest point lines up with the target.
                var perpendicular = normal - axis * Vector3.Dot(normal, axis);
                float turn = Mathf.Atan2(Vector3.Dot(Vector3.Cross(axis, perpendicular), match.Target),
                    Vector3.Dot(perpendicular, match.Target)) * Mathf.Rad2Deg;
                if (turn > 0 && turn < degrees) return turn / degrees;
                return match.Error(end) < match.Error(start) ? 1 : 0;
            }

            public Quaternion At(float progress) => Quaternion.AngleAxis(degrees * progress, axis) * start;
        }

        public void EndDrag() => rawRotation = null;

        public void Clear()
        {
            Pending = null;
            matches.Clear();
            trialMatches.Clear();
            candidates.Clear();
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
            if (attached)
            {
                var current = matches[0];
                float error = current.Error(requested);
                if (error <= EscapeAngle)
                {
                    if (TryCandidates(session, stone, previous, requested,
                        Mathf.Min(CaptureAngle, error - HandoffMargin), current, enteringOnly: false)) return;
                    if (TryAttach(session, stone, requested, current, requireMatch: false))
                    {
                        rawRotation = requested;
                        return;
                    }
                }
            }
            bool leaving = attached;
            Fade();

            // The player's rotation can stop partway at a collision. The magnetic
            // correction uses the same sweep and commits only if the full turn fits.
            rawRotation = null;
            if (!session.Adjust(stone, stone.transform.position, requested)) return;
            landing.Update(session, stone);
            TryCandidates(session, stone, previous, requested, CaptureAngle, default, enteringOnly: leaving);
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
            if (PrepareGroup(matches[0].HangingFace, Pending.transform.rotation, default))
            {
                (matches, trialMatches) = (trialMatches, matches);
                return;
            }
            EndDrag();
            Fade();
        }

        bool TryAttach(GameSession session, Stone stone, Quaternion requested, FaceMatch match, bool requireMatch)
        {
            var aligned = match.Aligned(requested);
            // A rejected correction leaves the stone and published outlines untouched.
            if (!PrepareGroup(match.HangingFace, aligned, requireMatch ? match : default) ||
                !session.TryAlignRotation(stone, aligned)) return false;
            (matches, trialMatches) = (trialMatches, matches);
            attached = true;
            return true;
        }

        bool PrepareGroup(StoneFace hangingFace, Quaternion orientation, FaceMatch required)
        {
            trialMatches.Clear();
            if (!landing.ProjectHangingFace(hangingFace, orientation)) return false;
            bool foundRequired = required.Support == null;
            int closest = 0;
            float leastError = float.PositiveInfinity;
            for (int i = 0; i < landing.Count; i++)
            {
                var support = landing[i];
                if (support.Normal.y < MinimumSupportUp) continue;
                var candidate = new FaceMatch(hangingFace, support);
                float error = candidate.Error(orientation);
                if (error > ParallelTolerance || !landing.Reaches(support)) continue;
                if (error < leastError)
                {
                    closest = trialMatches.Count;
                    leastError = error;
                }
                trialMatches.Add(candidate);
                foundRequired |= candidate.Support == required.Support && candidate.SupportFace == required.SupportFace;
            }
            if (!foundRequired || trialMatches.Count == 0) return false;
            (trialMatches[0], trialMatches[closest]) = (trialMatches[closest], trialMatches[0]);
            return true;
        }

        bool TryCandidates(GameSession session, Stone stone, Quaternion previous, Quaternion requested,
            float capture, FaceMatch current, bool enteringOnly)
        {
            if (capture <= 0) return false;
            bool handingOff = current.HangingFace != null;
            var arc = handingOff ? new RotationArc(previous, requested) : default;
            float departure = handingOff ? arc.ClosestProgress(current) : 0;
            if (departure >= 1) return false;
            candidates.Clear();
            var hangingFaces = StoneFace.Of(stone.Shape);
            var actual = stone.transform.rotation;
            for (int i = 0; i < landing.Count; i++)
            {
                var support = landing[i];
                if (support.Normal.y < MinimumSupportUp) continue;
                foreach (var hangingFace in hangingFaces)
                {
                    var candidate = new FaceMatch(hangingFace, support);
                    float error = candidate.Error(requested);
                    if (error > CaptureAngle) continue;
                    if (hangingFace == current.HangingFace && candidate.Error(actual) <= ParallelTolerance) continue;
                    var approach = requested;
                    float progress = 1;
                    if (handingOff)
                    {
                        // The sample may already have passed a nearby detent. Consider
                        // the part of the path that moves away from the current one.
                        float closest = arc.ClosestProgress(candidate);
                        if (closest > departure && closest < 1)
                        {
                            var closestPose = arc.At(closest);
                            float closestError = candidate.Error(closestPose);
                            if (closestError + HandoffMargin < current.Error(closestPose))
                            {
                                progress = closest;
                                approach = closestPose;
                                error = closestError;
                            }
                        }
                    }
                    if (progress == 1 && error > capture) continue;
                    float previousError = candidate.Error(previous);
                    if (error >= previousError) continue;
                    bool entered = previousError > CaptureAngle;
                    if (enteringOnly && !entered) continue;
                    candidates.Add((candidate, approach, progress, error, entered));
                }
            }
            candidates.Sort((first, second) =>
            {
                int pathOrder = first.Progress.CompareTo(second.Progress);
                if (pathOrder != 0) return pathOrder;
                int priority = second.Entered.CompareTo(first.Entered);
                return priority == 0 ? first.Error.CompareTo(second.Error) : priority;
            });
            foreach (var candidate in candidates)
            {
                if (!TryAttach(session, stone, candidate.Approach, candidate.Match, requireMatch: true)) continue;
                // Start the escape distance from the corrected pose in either direction.
                rawRotation = stone.transform.rotation;
                return true;
            }
            return false;
        }
    }
}
