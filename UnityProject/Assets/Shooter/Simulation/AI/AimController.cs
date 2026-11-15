#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Simulation.AI
{
    /// <summary>
    /// T082 aim model (ai.md "bounded angular acceleration, seeded uncertainty and accuracy recovery"):
    /// bots turn onto a target with a time-optimal bounded-acceleration profile and aim at a seeded,
    /// drifting error point that shrinks while they stay on the same target.
    /// </summary>
    public static class AimController
    {
        /// <summary>Wrap into [-π, π) for differences (not for contract output; use <see cref="NormalizeYaw"/>).</summary>
        public static float WrapPi(float angle)
        {
            double d = angle;
            while (d >= Math.PI) d -= 2 * Math.PI;
            while (d < -Math.PI) d += 2 * Math.PI;
            return (float)d;
        }

        /// <summary>Yaw inside the C0 half-open interval [-π, π); float π and -π both fall outside it.</summary>
        public static float NormalizeYaw(float yaw)
        {
            float w = WrapPi(yaw);
            if ((double)w >= Math.PI || (double)w < -Math.PI) w = -3.1415925f;
            return w;
        }

        public static float ClampPitch(float pitch) =>
            pitch > AiTuning.MaxPitch ? AiTuning.MaxPitch : pitch < -AiTuning.MaxPitch ? -AiTuning.MaxPitch : pitch;

        /// <summary>
        /// Exact yaw/pitch so the hitscan ray (canonical muzzle: eye + 0.18 m right, 0.15 m down, parallel to aim)
        /// passes through <paramref name="target"/>.
        /// </summary>
        public static void IdealAngles(Float3 eye, Float3 target, out float yaw, out float pitch)
        {
            float dx = target.X - eye.X, dy = target.Y - eye.Y, dz = target.Z - eye.Z;
            float bearing = MathF.Sqrt(dx * dx + dz * dz);
            if (bearing > 1e-4f)
            {
                float rx = dz / bearing, rz = -dx / bearing;
                dx -= rx * CombatAiDirector.MuzzleRightOffset;
                dz -= rz * CombatAiDirector.MuzzleRightOffset;
            }
            dy += CombatAiDirector.MuzzleDownOffset;
            yaw = CombatAiDirector.ContractYaw(dx, dz);
            float flat = MathF.Sqrt(dx * dx + dz * dz);
            pitch = flat < 1e-5f ? 0 : ClampPitch(-MathF.Atan2(dy, flat));
        }

        /// <summary>Plain look angles (no muzzle correction) toward a point.</summary>
        public static void LookAngles(Float3 eye, Float3 target, out float yaw, out float pitch)
        {
            float dx = target.X - eye.X, dy = target.Y - eye.Y, dz = target.Z - eye.Z;
            yaw = CombatAiDirector.ContractYaw(dx, dz);
            float flat = MathF.Sqrt(dx * dx + dz * dz);
            pitch = flat < 1e-5f ? 0 : ClampPitch(-MathF.Atan2(dy, flat));
        }

        /// <summary>
        /// One tick of a bounded-rate, bounded-acceleration approach (decelerates to stop on the goal).
        /// <paramref name="rate"/> is the persistent angular velocity in rad/s.
        /// </summary>
        public static float Step(float current, float desired, ref float rate, float maxRate, float maxAccel, bool wrap)
        {
            const float dt = AiTuning.TickSeconds;
            float diff = wrap ? WrapPi(desired - current) : desired - current;
            float mag = Math.Abs(diff);
            if (mag < 1e-5f) { rate = 0; return desired; }
            float sign = diff > 0 ? 1f : -1f;
            float goalRate = sign * Math.Min(maxRate, MathF.Sqrt(2f * maxAccel * mag));
            float dv = maxAccel * dt;
            float change = goalRate - rate;
            rate += change > dv ? dv : change < -dv ? -dv : change;
            float step = rate * dt;
            if (step * diff > 0 && Math.Abs(step) >= mag)
            {
                float r = diff / dt;
                rate = r > maxRate ? maxRate : r < -maxRate ? -maxRate : r;
                return desired;
            }
            return current + step;
        }

        /// <summary>Turn the board's yaw/pitch one tick toward the desired angles.</summary>
        public static void Turn(AiBlackboard board, float desiredYaw, float desiredPitch, bool combat)
        {
            float rate = board.YawRate;
            float yaw = Step(board.Yaw, desiredYaw, ref rate,
                combat ? AiTuning.CombatTurnRate : AiTuning.TravelTurnRate,
                combat ? AiTuning.CombatTurnAccel : AiTuning.TravelTurnAccel, wrap: true);
            board.YawRate = rate;
            board.Yaw = NormalizeYaw(yaw);
            float prate = board.PitchRate;
            board.Pitch = ClampPitch(Step(board.Pitch, desiredPitch, ref prate, AiTuning.PitchRate, AiTuning.PitchAccel, wrap: false));
            board.PitchRate = prate;
        }

        /// <summary>Per-bot skill factor in [0.8, 1.25): higher is sloppier and slower to settle.</summary>
        public static float Personality(EntityId self) =>
            0.8f + 0.45f * AiNoise.Unit(self.Value, 0, AiNoise.SaltPersonality);

        /// <summary>
        /// Current seeded aim error (radians) for a target at <paramref name="distance"/>. Large on acquisition,
        /// recovering over ~0.7 s; widened by own movement, target motion and recent hits (flinch).
        /// </summary>
        public static void ErrorOffset(AiBlackboard board, uint tick, float distance, float targetSpeed, out float yawError, out float pitchError)
        {
            float personality = Personality(board.Self);
            // Recovery starts when the reaction delay ends (first possible shot), not at sighting: the opening
            // trigger pulls are warning shots the player can see and hear before the bot settles (T084).
            uint start = Math.Max(board.AcquiredTick, board.ReactionReadyTick);
            uint since = tick > start ? tick - start : 0u;
            float recovery = AiTuning.AccuracyRecoveryTicks * personality;
            float k = Math.Max(0f, 1f - since / recovery);
            float settled = AiTuning.SettledErrorMeters + AiTuning.SettledErrorPerMeter * Math.Max(distance, 0f);
            float meters = settled + Math.Max(0f, AiTuning.AcquireErrorMeters - settled) * k;
            if (board.SpeedMps > 1f) meters += AiTuning.MovingErrorMeters;
            meters += AiTuning.TargetMotionErrorMeters * Math.Min(1f, targetSpeed / 5f);
            if (tick < board.FlinchUntilTick)
                meters += AiTuning.FlinchErrorMeters * (board.FlinchUntilTick - tick) / (float)AiTuning.FlinchTicks;
            meters *= personality;
            float angle = MathF.Atan2(meters, Math.Max(distance, 1f));
            if (angle < AiTuning.MinErrorRadians) angle = AiTuning.MinErrorRadians;
            if (angle > AiTuning.MaxErrorRadians) angle = AiTuning.MaxErrorRadians;

            ulong bucket = (tick / AiTuning.ErrorBucketTicks) ^ ((ulong)board.AcquiredTick << 24);
            float phi = AiNoise.Signed(board.Self.Value, bucket, AiNoise.SaltErrorAngle) * MathF.PI;
            float radius = 0.5f + 0.5f * AiNoise.Unit(board.Self.Value, bucket, AiNoise.SaltErrorAngle + 1);
            yawError = angle * radius * MathF.Cos(phi);
            pitchError = angle * radius * MathF.Sin(phi) * 0.8f;
        }

        /// <summary>Angular distance between two aim directions given as yaw/pitch pairs.</summary>
        public static float AngleBetween(float yawA, float pitchA, float yawB, float pitchB)
        {
            float dy = WrapPi(yawA - yawB) * MathF.Cos((pitchA + pitchB) * 0.5f);
            float dp = pitchA - pitchB;
            return MathF.Sqrt(dy * dy + dp * dp);
        }

        /// <summary>Fire alignment tolerance: about a torso half-width at the target, never tighter than ~1.5°.</summary>
        public static float FireTolerance(float distance) =>
            Math.Max(AiTuning.FireToleranceMinRadians, MathF.Atan2(AiTuning.FireToleranceMeters, Math.Max(distance, 1f)));
    }
}
