#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Presentation.Fx
{
    /// <summary>
    /// ADR-111 cook-time trajectory preview: the lofted throw stepped at the authority rate with the authority
    /// gravity, bounce damping and friction, until it lands, comes to rest or the fuse runs out.
    /// Presentation only; constants mirror <c>GrenadeSimulator</c> and the grenade definition (pinned by tests).
    /// </summary>
    public static class GrenadeArcPreview
    {
        public const float ThrowSpeedMetersPerSecond = 28f;
        public const float LoftDegrees = 8f;
        public const float MaxLaunchPitchDegrees = 80f;
        public const float StepSeconds = 1f / 60f;
        public const int MaxPoints = 160;
        /// <summary>Canonical muzzle offsets from the eye: right, up, forward (GameplayPoseEvaluator).</summary>
        public const float MuzzleRight = 0.18f, MuzzleUp = -0.15f, MuzzleForward = 0.45f;

        public readonly struct Result
        {
            public int Count { get; }
            public bool Landed { get; }
            public Float3 Landing { get; }
            public Float3 LandingNormal { get; }
            public Result(int count, bool landed, Float3 landing, Float3 landingNormal)
            {
                Count = count; Landed = landed; Landing = landing; LandingNormal = landingNormal;
            }
        }

        /// <summary>Raises a unit aim by the throw loft (same rule as the authority spawn).</summary>
        public static Float3 Loft(Float3 unitAim)
        {
            double h = Math.Sqrt(unitAim.X * (double)unitAim.X + unitAim.Z * (double)unitAim.Z);
            if (h < 1e-6) return unitAim;
            double pitch = Math.Atan2(unitAim.Y, h);
            double lofted = Math.Min(pitch + LoftDegrees * Math.PI / 180.0, MaxLaunchPitchDegrees * Math.PI / 180.0);
            if (lofted <= pitch) return unitAim;
            double c = Math.Cos(lofted) / h;
            return new Float3((float)(unitAim.X * c), (float)Math.Sin(lofted), (float)(unitAim.Z * c));
        }

        /// <summary>
        /// Fills <paramref name="points"/> with the predicted path (first point = launch). The first contact
        /// marks the landing; the path then continues through bounces until <paramref name="fuseSeconds"/>.
        /// </summary>
        public static Result Compute(Float3 origin, Float3 unitAim, float fuseSeconds,
            CosmeticGrenadeFlight.SweepProbe? probe, Float3[] points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Length == 0) return default;
            Float3 dir = Loft(unitAim);
            var vel = new Float3(dir.X * ThrowSpeedMetersPerSecond, dir.Y * ThrowSpeedMetersPerSecond, dir.Z * ThrowSpeedMetersPerSecond);
            var pos = origin;
            int n = 0;
            points[n++] = pos;
            bool landed = false;
            Float3 landing = default, landingNormal = default;
            int steps = float.IsFinite(fuseSeconds) && fuseSeconds > 0f ? (int)Math.Ceiling(fuseSeconds / StepSeconds) : 0;
            int limit = Math.Min(points.Length, MaxPoints);
            for (int i = 0; i < steps && n < limit; i++)
            {
                vel = new Float3(vel.X, vel.Y + CosmeticGrenadeFlight.GravityY * StepSeconds, vel.Z);
                var delta = new Float3(vel.X * StepSeconds, vel.Y * StepSeconds, vel.Z * StepSeconds);
                if (probe != null && probe(pos, delta, out var hit, out var normal))
                {
                    if (!landed) { landed = true; landing = hit; landingNormal = normal; }
                    float nd = vel.X * normal.X + vel.Y * normal.Y + vel.Z * normal.Z;
                    if (nd < 0f)
                        vel = new Float3(
                            (vel.X - 2f * nd * normal.X) * CosmeticGrenadeFlight.BounceRetain,
                            (vel.Y - 2f * nd * normal.Y) * CosmeticGrenadeFlight.BounceRetain,
                            (vel.Z - 2f * nd * normal.Z) * CosmeticGrenadeFlight.BounceRetain);
                    if (normal.Y > 0.7f) vel = new Float3(vel.X * CosmeticGrenadeFlight.GroundFriction, vel.Y, vel.Z * CosmeticGrenadeFlight.GroundFriction);
                    pos = new Float3(hit.X + normal.X * 0.02f, hit.Y + normal.Y * 0.02f, hit.Z + normal.Z * 0.02f);
                    points[n++] = pos;
                    if (vel.X * vel.X + vel.Y * vel.Y + vel.Z * vel.Z < 0.25f) break;
                    continue;
                }
                pos = new Float3(pos.X + delta.X, pos.Y + delta.Y, pos.Z + delta.Z);
                // Keep the line light: one vertex every other step in the air.
                if ((i & 1) == 1) points[n++] = pos;
            }
            if (n < limit && (n == 0 || !points[n - 1].Equals(pos))) points[n++] = pos;
            return new Result(n, landed, landing, landingNormal);
        }

        /// <summary>Launch point for a camera eye and aim, mirroring the authority canonical muzzle.</summary>
        public static Float3 Origin(Float3 eye, Float3 forward, Float3 right, Float3 up) =>
            new Float3(
                eye.X + right.X * MuzzleRight + up.X * MuzzleUp + forward.X * MuzzleForward,
                eye.Y + right.Y * MuzzleRight + up.Y * MuzzleUp + forward.Y * MuzzleForward,
                eye.Z + right.Z * MuzzleRight + up.Z * MuzzleUp + forward.Z * MuzzleForward);
    }
}
