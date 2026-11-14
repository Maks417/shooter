#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// Shared T058 spawn-slot layout for catalog gameplay and map-package metadata.
    /// Emits ≥12 unique standing-clearance slots per team without rewriting package encoding.
    /// </summary>
    public static class MapSpawnLayout
    {
        public const int SlotsPerTeam = 12;
        public const float MinSlotSpacingMeters = 2f;
        public const float EyeHeightMeters = 1.65f;

        public static List<SpawnCandidate> BuildBothTeams(CompoundPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var slots = new List<SpawnCandidate>(SlotsPerTeam * 2);
            AddTeam(slots, plan, team: 1, plan.PlayerSpawn, plan.ObjectiveCenter, idBase: 100);
            AddTeam(slots, plan, team: 2, plan.ObjectiveCenter, plan.PlayerSpawn, idBase: 200);
            return slots;
        }

        public static bool PositionsUnique(IReadOnlyList<SpawnCandidate> slots, float epsilonMeters = 0.05f)
        {
            float eps2 = epsilonMeters * epsilonMeters;
            for (int i = 0; i < slots.Count; i++)
            for (int j = i + 1; j < slots.Count; j++)
            {
                if (slots[i].Team != slots[j].Team) continue;
                if (Dist2(slots[i].Position, slots[j].Position) < eps2) return false;
            }
            return true;
        }

        private static void AddTeam(
            List<SpawnCandidate> slots,
            CompoundPlan plan,
            byte team,
            Float3 anchor,
            Float3 lookAt,
            uint idBase)
        {
            float clearance = CompoundConnectivity.RequiredClearance;
            var enemyAnchor = team == 1 ? plan.ObjectiveCenter : plan.PlayerSpawn;
            int added = 0;
            for (int ring = 0; ring < 14 && added < SlotsPerTeam; ring++)
            {
                int spokes = ring == 0 ? 1 : 12;
                for (int k = 0; k < spokes && added < SlotsPerTeam; k++)
                {
                    float ang = spokes == 1 ? 0f : (k / (float)spokes) * MathF.PI * 2f;
                    float radius = ring * 2.25f;
                    float x = anchor.X + MathF.Cos(ang) * radius;
                    float z = anchor.Z + MathF.Sin(ang) * radius;
                    if (MathF.Abs(x) > CompoundPlan.MapHalfExtent - 1f || MathF.Abs(z) > CompoundPlan.MapHalfExtent - 1f)
                        continue;
                    if (CompoundConnectivity.IsBlocked(plan.CollisionBoxes, x, z, clearance)) continue;
                    var feet = new Float3(x, MathF.Max(0f, anchor.Y), z);
                    if (!SpacingOk(slots, team, feet, MinSlotSpacingMeters)) continue;
                    // Prefer staging positions that are not open to the enemy staging anchor.
                    if (HasSpawnLos(plan.CollisionBoxes, feet, enemyAnchor) && ring < 2 && radius < 4f)
                        continue;
                    slots.Add(new SpawnCandidate(idBase + (uint)added, team, feet, YawToward(feet, lookAt)));
                    added++;
                }
            }

            // Expand search if rings under-filled; never duplicate the same feet.
            for (int r = 1; r <= 40 && added < SlotsPerTeam; r++)
            {
                for (int a = 0; a < 16 && added < SlotsPerTeam; a++)
                {
                    float ang = a * (MathF.PI / 8f);
                    float x = anchor.X + MathF.Cos(ang) * (2f + r * 1.5f);
                    float z = anchor.Z + MathF.Sin(ang) * (2f + r * 1.5f);
                    if (MathF.Abs(x) > CompoundPlan.MapHalfExtent - 1f || MathF.Abs(z) > CompoundPlan.MapHalfExtent - 1f)
                        continue;
                    if (CompoundConnectivity.IsBlocked(plan.CollisionBoxes, x, z, clearance)) continue;
                    var feet = new Float3(x, MathF.Max(0f, anchor.Y), z);
                    if (!SpacingOk(slots, team, feet, MinSlotSpacingMeters)) continue;
                    slots.Add(new SpawnCandidate(idBase + (uint)added, team, feet, YawToward(feet, lookAt)));
                    added++;
                }
            }

            if (added < SlotsPerTeam)
                throw new InvalidOperationException("Unable to place " + SlotsPerTeam + " unique team-" + team + " spawn slots.");
        }

        private static bool SpacingOk(List<SpawnCandidate> slots, byte team, Float3 feet, float minSpacing)
        {
            float min2 = minSpacing * minSpacing;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Team != team) continue;
                if (Dist2(slots[i].Position, feet) < min2) return false;
            }
            return true;
        }

        public static bool HasSpawnLos(IReadOnlyList<CollisionBox> boxes, Float3 aFeet, Float3 bFeet)
        {
            var a = new Float3(aFeet.X, aFeet.Y + EyeHeightMeters, aFeet.Z);
            var b = new Float3(bFeet.X, bFeet.Y + EyeHeightMeters, bFeet.Z);
            return CompoundConnectivity.HasLineOfSight(boxes, a, b, a.Y);
        }

        private static float Dist2(Float3 a, Float3 b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return dx * dx + dz * dz;
        }

        private static float YawToward(Float3 from, Float3 to)
        {
            float yaw = MathF.Atan2(to.X - from.X, to.Z - from.Z);
            if (yaw >= (float)Math.PI) yaw = (float)(Math.PI - 1e-5);
            if (yaw < -(float)Math.PI) yaw = -(float)Math.PI;
            return yaw;
        }
    }

    public readonly struct SpawnCandidate
    {
        public uint SlotId { get; }
        public byte Team { get; }
        public Float3 Position { get; }
        public float Yaw { get; }

        public SpawnCandidate(uint slotId, byte team, Float3 position, float yaw)
        {
            if (slotId == 0) throw new ArgumentOutOfRangeException(nameof(slotId));
            if (team != 1 && team != 2) throw new ArgumentOutOfRangeException(nameof(team));
            SlotId = slotId;
            Team = team;
            Position = position;
            Yaw = yaw;
        }
    }
}
