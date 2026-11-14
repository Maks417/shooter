#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;

namespace Shooter.Simulation.TeamDeathmatch
{
    /// <summary>
    /// T058 / FIX07 spawn ranking: reject blocked/occupied slots, prefer no enemy LOS within 40 m,
    /// then greatest nearest-enemy distance, least-recently-used, then stable slot ID.
    /// </summary>
    public static class SpawnSafetyPolicy
    {
        public const float EnemyLosRangeMeters = 40f;
        public const float ActorSpacingMeters = 2f;
        public const float EyeHeightMeters = 1.65f;

        public static bool TryPick(
            byte team,
            IReadOnlyList<SpawnSlot> slots,
            IMapRuntime map,
            IActorLifecycle lifecycle,
            IReadOnlyList<EntityId> livingActors,
            IReadOnlyDictionary<uint, uint> slotLastUsedActiveTick,
            IReadOnlyList<Float3> provisionalOccupancy,
            out SpawnSlot chosen)
        {
            chosen = default;
            if (slots == null || map == null || lifecycle == null) return false;

            SpawnSlot? best = null;
            bool bestSafeLos = false;
            float bestNearestEnemy = -1f;
            uint bestLru = uint.MaxValue;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.Team != team) continue;
                if (!map.TryValidateSpawn(slot, out _)) continue;
                if (IsOccupied(slot.Position, lifecycle, livingActors, provisionalOccupancy)) continue;

                bool safeLos = !HasEnemyLosWithinRange(slot.Position, team, lifecycle, livingActors, map.WorldQueries, EnemyLosRangeMeters);
                float nearest = NearestEnemyDistance(slot.Position, team, lifecycle, livingActors);
                uint lru = 0;
                if (slotLastUsedActiveTick != null && slotLastUsedActiveTick.TryGetValue(slot.SlotId, out uint used))
                    lru = used;

                if (best == null)
                {
                    best = slot;
                    bestSafeLos = safeLos;
                    bestNearestEnemy = nearest;
                    bestLru = lru;
                    continue;
                }

                int cmp = CompareCandidates(safeLos, bestSafeLos, nearest, bestNearestEnemy, lru, bestLru, slot.SlotId, best.Value.SlotId);
                if (cmp < 0)
                {
                    best = slot;
                    bestSafeLos = safeLos;
                    bestNearestEnemy = nearest;
                    bestLru = lru;
                }
            }

            if (best == null) return false;
            // All remaining candidates are navigable and unoccupied. Prefer safe-LOS ranking;
            // if every candidate is LOS-exposed, still refuse to spawn into exposed pressure.
            if (!bestSafeLos) return false;
            chosen = best.Value;
            return true;
        }

        private static int CompareCandidates(
            bool safeLos,
            bool bestSafeLos,
            float nearest,
            float bestNearest,
            uint lru,
            uint bestLru,
            uint slotId,
            uint bestSlotId)
        {
            if (safeLos != bestSafeLos) return safeLos ? -1 : 1;
            int cmp = bestNearest.CompareTo(nearest); // greater distance first
            if (cmp != 0) return cmp;
            cmp = lru.CompareTo(bestLru); // least-recently-used first
            if (cmp != 0) return cmp;
            return slotId.CompareTo(bestSlotId);
        }

        private static bool IsOccupied(
            Float3 feet,
            IActorLifecycle lifecycle,
            IReadOnlyList<EntityId> livingActors,
            IReadOnlyList<Float3> provisionalOccupancy)
        {
            float min2 = ActorSpacingMeters * ActorSpacingMeters;
            if (provisionalOccupancy != null)
            {
                for (int i = 0; i < provisionalOccupancy.Count; i++)
                {
                    if (Dist2(feet, provisionalOccupancy[i]) < min2) return true;
                }
            }
            for (int i = 0; i < livingActors.Count; i++)
            {
                if (!lifecycle.TryGetActorState(livingActors[i], out var state) || !state.Alive) continue;
                if (Dist2(feet, state.Feet) < min2) return true;
            }
            return false;
        }

        private static bool HasEnemyLosWithinRange(
            Float3 feet,
            byte team,
            IActorLifecycle lifecycle,
            IReadOnlyList<EntityId> livingActors,
            IWorldQueries world,
            float rangeMeters)
        {
            float range2 = rangeMeters * rangeMeters;
            var eye = new Float3(feet.X, feet.Y + EyeHeightMeters, feet.Z);
            for (int i = 0; i < livingActors.Count; i++)
            {
                if (!lifecycle.TryGetActorState(livingActors[i], out var state) || !state.Alive) continue;
                if (state.Team == team) continue;
                float d2 = Dist2(feet, state.Feet);
                if (d2 > range2 || d2 < 1e-6f) continue;
                if (HasClearFireLine(world, eye, state.Feet)) return true;
            }
            return false;
        }

        private static float NearestEnemyDistance(
            Float3 feet,
            byte team,
            IActorLifecycle lifecycle,
            IReadOnlyList<EntityId> livingActors)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < livingActors.Count; i++)
            {
                if (!lifecycle.TryGetActorState(livingActors[i], out var state) || !state.Alive) continue;
                if (state.Team == team) continue;
                float d2 = Dist2(feet, state.Feet);
                if (d2 < best) best = d2;
            }
            return float.IsPositiveInfinity(best) ? float.MaxValue : MathF.Sqrt(best);
        }

        public static bool HasClearFireLine(IWorldQueries world, Float3 eye, Float3 enemyFeet)
        {
            if (world == null) return true;
            var target = new Float3(enemyFeet.X, enemyFeet.Y + EyeHeightMeters, enemyFeet.Z);
            float dx = target.X - eye.X, dy = target.Y - eye.Y, dz = target.Z - eye.Z;
            float len = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 1e-4f) return true;
            var dir = new Float3(dx / len, dy / len, dz / len);
            var filter = new QueryFilter(uint.MaxValue, default, false);
            var hit = world.Raycast(eye, dir, len, filter);
            // NoHit => clear LOS (unsafe). Hit => occluded (safe from that enemy fire line).
            return hit.Status == QueryStatus.NoHit;
        }

        private static float Dist2(Float3 a, Float3 b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return dx * dx + dz * dz;
        }
    }
}
