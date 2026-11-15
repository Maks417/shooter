#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Simulation.AI
{
    /// <summary>
    /// Optional non-frozen extension of IActorLifecycle that exposes the authoritative stance, so bots
    /// can aim inside a crouched/sliding target's real hit volumes and from their own real eye height.
    /// </summary>
    public interface IActorStanceSource
    {
        bool TryGetStance(EntityId entity, out Stance stance);
    }

    public readonly struct SenseTarget
    {
        public EntityId Id { get; }
        public Float3 Point { get; }
        public byte Team { get; }
        public bool Alive { get; }
        /// <summary>Authoritative health (used only to judge a losing trade); 100 when unknown.</summary>
        public ushort Health { get; }

        public SenseTarget(EntityId id, Float3 point, byte team = 0, bool alive = true, ushort health = 100)
        {
            Id = id;
            Point = point;
            Team = team;
            Alive = alive;
            Health = health;
        }
    }

    /// <summary>Bounded vision: global ≤16 rays/tick; deferred sensing ages rather than inventing visibility.</summary>
    public sealed class PerceptionSystem
    {
        public const int MaxVisionRaysPerTick = 16;
        /// <summary>Bot vision cone (T084: 120°, peripheral awareness wider than the 80° player camera).</summary>
        public const float DefaultFovDegrees = 120f;
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

        /// <summary>
        /// Non-vision AI ray (cover probes) charged to the same global per-tick budget. Returns false,
        /// without casting, when the budget is exhausted.
        /// </summary>
        public bool TryBudgetedRay(Float3 origin, Float3 unitDirection, float maxMeters, EntityId self, out RayHit hit)
        {
            if (raysUsed >= MaxVisionRaysPerTick) { hit = default; return false; }
            raysUsed++;
            hit = world.Raycast(origin, unitDirection, maxMeters, new QueryFilter(visionMask, self, false));
            return true;
        }

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
            if (!IsOpponent(board, candidate))
            {
                if (candidate.Alive && candidate.Id != board.Self && IsAlliedTeam(board.Team, candidate.Team))
                    counters.AlliedCandidatesSkipped++;
                return true;
            }
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
            board.VisibleTargetId = candidate.Id;
            board.VisibleTarget = candidate.Point;
            board.LastSeenEnemy = candidate.Point;
            board.Confidence = 1f;
            board.SenseAgeTicks = 0;
            return true;
        }

        /// <summary>
        /// Team 0 remains a diagnostic "unspecified" hostility for single-target plans.
        /// Assigned teams (1/2) only acquire living opposite-team actors.
        /// </summary>
        public static bool IsOpponent(AiBlackboard board, SenseTarget candidate)
        {
            if (!candidate.Alive || candidate.Id == board.Self) return false;
            return !IsAlliedTeam(board.Team, candidate.Team);
        }

        public static bool IsAlliedTeam(byte selfTeam, byte candidateTeam)
        {
            if (selfTeam == 0 || candidateTeam == 0) return false;
            return selfTeam == candidateTeam;
        }
    }
}
