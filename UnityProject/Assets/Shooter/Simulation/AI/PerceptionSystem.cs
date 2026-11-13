#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Simulation.AI
{
    public readonly struct SenseTarget
    {
        public EntityId Id { get; }
        public Float3 Point { get; }
        public SenseTarget(EntityId id, Float3 point) { Id = id; Point = point; }
    }

    /// <summary>Bounded vision: global ≤16 rays/tick; deferred sensing ages rather than inventing visibility.</summary>
    public sealed class PerceptionSystem
    {
        public const int MaxVisionRaysPerTick = 16;
        public const float DefaultFovDegrees = 90f;
        public const float DefaultRange = 80f;

        private readonly IWorldQueries world;
        private readonly uint visionMask;
        private int raysUsed;

        public int RaysUsedThisTick => raysUsed;

        public PerceptionSystem(IWorldQueries world, uint visionMask)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.visionMask = visionMask;
        }

        public void BeginTick() => raysUsed = 0;

        public bool TrySense(
            AiBlackboard board,
            Float3 eye,
            float yaw,
            SenseTarget candidate,
            AiCounters counters,
            out bool acquired)
        {
            acquired = false;
            if (!board.Alive || board.State == AiState.Dead) return false;
            if (raysUsed >= MaxVisionRaysPerTick)
            {
                counters.DeferredSenses++;
                board.SenseAgeTicks++;
                return false;
            }

            Float3 to = new Float3(candidate.Point.X - eye.X, candidate.Point.Y - eye.Y, candidate.Point.Z - eye.Z);
            double lenSq = to.LengthSquared;
            if (lenSq < 1e-6) { acquired = true; return true; }
            double len = Math.Sqrt(lenSq);
            if (len > DefaultRange) return true;
            var dir = new Float3((float)(to.X / len), (float)(to.Y / len), (float)(to.Z / len));
            // FOV check in XZ
            float facingX = MathF.Sin(yaw), facingZ = MathF.Cos(yaw);
            float flatLen = MathF.Sqrt(dir.X * dir.X + dir.Z * dir.Z);
            if (flatLen < 1e-5f) return true;
            float nx = dir.X / flatLen, nz = dir.Z / flatLen;
            float dot = facingX * nx + facingZ * nz;
            float cosHalf = MathF.Cos(DefaultFovDegrees * 0.5f * (MathF.PI / 180f));
            if (dot < cosHalf) return true;

            raysUsed++;
            counters.VisionRaysThisTick++;
            var hit = world.Raycast(eye, dir, (float)len, new QueryFilter(visionMask, board.Self, false));
            if (hit.Status == QueryStatus.Hit && hit.Hit.DistanceMeters + 0.05f < len)
            {
                // Occluded — do not acquire.
                board.HasVisibleTarget = false;
                board.VisibleTarget = null;
                return true;
            }
            acquired = true;
            board.HasVisibleTarget = true;
            board.VisibleTarget = candidate.Point;
            board.LastSeenEnemy = candidate.Point;
            board.LastSeenTick = board.StateEnteredTick; // caller overwrites with absolute tick
            board.Confidence = 1f;
            board.SenseAgeTicks = 0;
            return true;
        }
    }
}
