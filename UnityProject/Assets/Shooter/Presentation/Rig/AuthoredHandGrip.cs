#nullable enable
using System;
using UnityEngine;

namespace Shooter.Presentation.Rig
{
    /// <summary>
    /// Where the support hand holds the gun: the axis of the handguard cylinder at the held weapon's left-hand-grip socket
    /// (<see cref="AuthoredHandGrip.MeasureHandguard"/>) and its radius.
    /// </summary>
    public readonly struct SupportSocket
    {
        public bool Active { get; }
        /// <summary>Handguard axis point in the weapon's local frame, metres.</summary>
        public Vector3 Local { get; }
        public float Radius { get; }
        public SupportSocket(Vector3 local, float radius)
        {
            Active = true; Local = local;
            Radius = float.IsFinite(radius) ? Mathf.Clamp(radius, AuthoredHandGrip.MinRadius, AuthoredHandGrip.MaxRadius) : AuthoredHandGrip.DefaultRadius;
        }
    }

    /// <summary>
    /// ADR-129 grip solve shared by the third-person authored body and the licensed first-person arms. Hand frames use
    /// the semantic glove convention (identity at the T-pose bind: fingers along ±X, back of the hand +Y, the finger
    /// curl about +Z). The palm, not the wrist, is laid against the grip so the knuckles sit at its far side, and each
    /// finger curls just far enough to wrap a cylinder of the grip's radius instead of a fixed fist.
    /// </summary>
    public static class AuthoredHandGrip
    {
        public const float MinRadius = .012f, MaxRadius = .05f, DefaultRadius = .025f;
        /// <summary>Default palm depth (hand bone to palm skin) before a mesh measurement, and half a finger's thickness.</summary>
        public const float PalmHalfThickness = .014f, FingerHalfThickness = .009f;
        /// <summary>The grip never sits closer to the wrist than this fraction of the wrist-to-knuckle line.</summary>
        public const float MinContactAlong = .55f;
        private const float MaxSegmentCurl = 110f, MaxTotalCurl = 260f;

        /// <summary>One hand measured once per variant in the T-pose (semantic frame, final scale).</summary>
        public sealed class Geometry
        {
            public bool Left;
            /// <summary>Hand bone to the midpoint of the index and little knuckles.</summary>
            public Vector3 Knuckles;
            /// <summary>Per non-thumb finger (index, middle, ring, little): proximal joint offset from the hand bone and three segment lengths.</summary>
            public readonly Vector3[] Base = new Vector3[4];
            public readonly float[,] Segments = new float[4, 3];
            /// <summary>Hand bone line to the palm skin under the knuckle half of the palm, measured from the mesh (metres).</summary>
            public float PalmSkin = PalmHalfThickness;
            /// <summary>
            /// World rotation of this hand's semantic frame in the T-pose, from its knuckles (fingers along ±X, back +Y). The
            /// T-pose roll comes from the thumb, whose base sits under the palm on some rigs, so this is not identity; every
            /// hand and finger rotation is applied relative to it.
            /// </summary>
            public Quaternion Bind = Quaternion.identity;
            /// <summary>A world T-pose offset from the hand bone in the semantic hand frame.</summary>
            public Vector3 ToFrame(Vector3 world) => Quaternion.Inverse(Bind) * world;
            /// <summary>World rotation for a bone whose T-pose rotation is <paramref name="bind"/>, with the hand frame at <paramref name="frame"/>.</summary>
            public Quaternion Pose(Quaternion frame, Quaternion bind) => frame * Quaternion.Inverse(Bind) * bind;
            /// <summary>Cached curls per handguard radius (mm): finger × segment, cumulative degrees.</summary>
            internal readonly System.Collections.Generic.Dictionary<int, float[]> Curls = new System.Collections.Generic.Dictionary<int, float[]>();

            /// <summary>
            /// Palm skin point that touches a grip of <paramref name="radius"/>, in the semantic hand frame: far enough
            /// back from the knuckles that they sit at the grip's far side, as in a real wrap.
            /// </summary>
            public Vector3 Contact(float radius)
            {
                float k = Mathf.Abs(Knuckles.x);
                float along = Mathf.Max(MinContactAlong * k, k - (radius + FingerHalfThickness) * .95f);
                return new Vector3(Mathf.Sign(Knuckles.x) * along, -PalmSkin, Knuckles.z);
            }
        }

        public static readonly HumanBodyBones[][] FingerChains =
        {
            new[] { HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal },
            new[] { HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal },
            new[] { HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal },
            new[] { HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal },
            new[] { HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal },
            new[] { HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal },
            new[] { HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal },
            new[] { HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal },
        };

        /// <summary>Measures a hand of a T-posed, +Z-facing humanoid at unit scale; <paramref name="scale"/> is the body scale.</summary>
        public static Geometry? Measure(Animator animator, bool left, float scale)
        {
            var hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (hand == null) return null;
            var geometry = new Geometry { Left = left };
            var index = animator.GetBoneTransform(FingerChains[left ? 0 : 4][0]);
            var middle = animator.GetBoneTransform(FingerChains[left ? 1 : 5][0]);
            var little = animator.GetBoneTransform(FingerChains[left ? 3 : 7][0]);
            if (index == null || middle == null || little == null) return null;
            // Semantic frame from the knuckles: X along the fingers (negated on the left hand), Y the back of the hand.
            var x = (middle.position - hand.position).normalized * (left ? -1f : 1f);
            var y = Vector3.Cross(x, little.position - index.position).normalized;
            if (y.sqrMagnitude < .5f) return null;
            geometry.Bind = Quaternion.LookRotation(Vector3.Cross(x, y), y);
            Vector3 knuckleSum = Vector3.zero; int knuckles = 0;
            for (int f = 0; f < 4; f++)
            {
                var chain = FingerChains[(left ? 0 : 4) + f];
                var proximal = animator.GetBoneTransform(chain[0]);
                var intermediate = animator.GetBoneTransform(chain[1]);
                var distal = animator.GetBoneTransform(chain[2]);
                if (proximal == null || intermediate == null || distal == null) return null;
                geometry.Base[f] = geometry.ToFrame(proximal.position - hand.position) * scale;
                float l1 = Vector3.Distance(proximal.position, intermediate.position), l2 = Vector3.Distance(intermediate.position, distal.position);
                float l3 = distal.childCount > 0 ? Vector3.Distance(distal.position, distal.GetChild(0).position) : l2 * .85f;
                geometry.Segments[f, 0] = l1 * scale; geometry.Segments[f, 1] = l2 * scale; geometry.Segments[f, 2] = Mathf.Max(l3, l2 * .5f) * scale;
                if (f == 0 || f == 3) { knuckleSum += geometry.Base[f]; knuckles++; }
            }
            geometry.Knuckles = knuckleSum / knuckles;
            return geometry;
        }

        /// <summary>
        /// Cumulative curl (degrees) per finger segment that wraps a handguard of <paramref name="radius"/> lying across the
        /// palm at <see cref="Geometry.Contact"/>. Solved in the curl plane (fingers out, back of the hand up);
        /// cached per millimetre of radius.
        /// </summary>
        public static float[] WrapCurls(Geometry hand, float radius)
        {
            int key = Mathf.RoundToInt(radius * 1000f);
            if (hand.Curls.TryGetValue(key, out var cached)) return cached;
            float outward = hand.Left ? -1f : 1f; // fingers point along -X on the left hand
            float contactU = hand.Contact(radius).x * outward;
            var centre = new Vector2(contactU, -hand.PalmSkin - radius);
            float wrap = radius + FingerHalfThickness;
            var curls = new float[12];
            for (int f = 0; f < 4; f++)
            {
                var point = new Vector2(hand.Base[f].x * outward, hand.Base[f].y);
                float previous = 0f;
                for (int s = 0; s < 3; s++)
                {
                    float length = hand.Segments[f, s];
                    float best = previous, bestError = float.MaxValue;
                    for (float angle = previous; angle <= Mathf.Min(previous + MaxSegmentCurl, MaxTotalCurl); angle += 1f)
                    {
                        var end = point + Direction(angle) * length;
                        float error = Mathf.Abs(Vector2.Distance(end, centre) - wrap);
                        // Never through the handguard: an end inside the wrap circle costs double.
                        if (Vector2.Distance(end, centre) < wrap) error *= 2f;
                        if (error < bestError - 1e-5f) { bestError = error; best = angle; }
                    }
                    curls[f * 3 + s] = best;
                    point += Direction(best) * length;
                    previous = best;
                }
            }
            hand.Curls[key] = curls;
            return curls;
        }

        /// <summary>Fingertip positions in the curl plane for <paramref name="curls"/> (review metric for tests).</summary>
        public static float WrapError(Geometry hand, float radius, float[] curls)
        {
            float outward = hand.Left ? -1f : 1f;
            var centre = new Vector2(hand.Contact(radius).x * outward, -hand.PalmSkin - radius);
            float worst = 0f;
            for (int f = 0; f < 4; f++)
            {
                var point = new Vector2(hand.Base[f].x * outward, hand.Base[f].y);
                for (int s = 0; s < 3; s++) point += Direction(curls[f * 3 + s]) * hand.Segments[f, s];
                worst = Mathf.Max(worst, Mathf.Abs(Vector2.Distance(point, centre) - (radius + FingerHalfThickness)));
            }
            return worst;
        }

        private static Vector2 Direction(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), -Mathf.Sin(r));
        }

        /// <summary>
        /// Wrist (hand bone) target that lays the palm contact against a grip cylinder of <paramref name="radius"/> whose
        /// axis passes through <paramref name="axisWorld"/>; the back of the hand (<paramref name="handFrame"/> +Y) faces
        /// away from it. <paramref name="handScale"/> is the drawn hand's (and grip's) scale over the measured one
        /// (first-person near field); <paramref name="radius"/> is unscaled.
        /// </summary>
        public static Vector3 SupportWrist(Geometry hand, Quaternion handFrame, Vector3 axisWorld, float radius, float handScale = 1f) =>
            axisWorld + handFrame * Vector3.up * (radius * handScale) - handFrame * (hand.Contact(radius) * handScale);

        /// <summary>Hand rotation in the semantic glove convention whose fingers point along <paramref name="fingers"/> and whose back faces <paramref name="back"/>.</summary>
        public static Quaternion HandFrame(Vector3 fingers, Vector3 back, bool left)
        {
            var x = (left ? -fingers : fingers).normalized;
            var y = Vector3.ProjectOnPlane(back, x).normalized;
            if (y.sqrMagnitude < 1e-6f) return Quaternion.identity;
            return Quaternion.LookRotation(Vector3.Cross(x, y), y);
        }

        /// <summary>Analytic two-bone solve: the root and middle joints turn so the end reaches the target, bending toward the pole.</summary>
        public static void SolveLimb(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole, Quaternion endRotation, float w)
        {
            if (w <= 1e-4f) return;
            Vector3 origin = upper.position;
            float a = Vector3.Distance(origin, lower.position), b = Vector3.Distance(lower.position, end.position);
            Vector3 toTarget = target - origin;
            if (toTarget.sqrMagnitude < 1e-8f || a < 1e-4f || b < 1e-4f) return;
            float c = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(a - b) + 1e-4f, a + b - 1e-4f);
            Vector3 dir = toTarget.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(pole - origin, dir);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.ProjectOnPlane(lower.position - origin, dir);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.down;
            bend.Normalize();
            float cosA = Mathf.Clamp((a * a + c * c - b * b) / (2f * a * c), -1f, 1f);
            Vector3 middle = origin + dir * (a * cosA) + bend * (a * Mathf.Sqrt(1f - cosA * cosA));
            var upperTurn = Quaternion.FromToRotation(lower.position - origin, middle - origin);
            upper.rotation = Quaternion.Slerp(Quaternion.identity, upperTurn, w) * upper.rotation;
            var lowerTurn = Quaternion.FromToRotation(end.position - lower.position, origin + dir * c - lower.position);
            lower.rotation = Quaternion.Slerp(Quaternion.identity, lowerTurn, w) * lower.rotation;
            end.rotation = Quaternion.Slerp(end.rotation, endRotation, w);
        }

        /// <summary>
        /// The handguard as a cylinder from the weapon's own vertices around the left-hand-grip socket (±3 cm along the
        /// bore, rails and sights above the socket ignored): radius = its half width, axis = that far above its underside.
        /// </summary>
        public static (Shooter.Contracts.Float3 axis, float radius) MeasureHandguard(System.Collections.Generic.IReadOnlyList<Shooter.Contracts.Float3> positions, Shooter.Contracts.Float3 socket)
        {
            float halfWidth = 0f, bottom = float.MaxValue; int count = 0;
            for (int i = 0; i < positions.Count; i++)
            {
                var p = positions[i];
                if (Math.Abs(p.Z - socket.Z) > .03f || Math.Abs(p.X - socket.X) > .06f || p.Y > socket.Y + .02f || p.Y < socket.Y - .08f) continue;
                halfWidth = Math.Max(halfWidth, Math.Abs(p.X - socket.X));
                bottom = Math.Min(bottom, p.Y);
                count++;
            }
            if (count == 0) return (socket, DefaultRadius);
            float radius = Mathf.Clamp(halfWidth, MinRadius, MaxRadius);
            return (new Shooter.Contracts.Float3(socket.X, bottom + radius, socket.Z), radius);
        }
    }
}
