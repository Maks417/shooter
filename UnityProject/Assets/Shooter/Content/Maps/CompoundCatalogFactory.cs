#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Generation.Environment;

namespace Shooter.Content.Maps
{
    /// <summary>Builds Game-v1 catalog/gameplay entries from the frozen compound planner seeds.</summary>
    public static class CompoundCatalogFactory
    {
        public static MapGameplayDefinition BuildGameplay(MapId mapId, ulong seed)
        {
            var plan = mapId.Value == "depot-yard" ? DepotPlanner.Generate(seed) : CompoundPlanner.Generate(seed);
            if (mapId.Value != "depot-yard")
            {
                var report = CompoundValidation.Validate(plan);
                if (!report.Passed) throw new InvalidOperationException(report.Diagnostic);
            }

            var bounds = new Aabb(
                new Float3(-CompoundPlan.MapHalfExtent, -2f, -CompoundPlan.MapHalfExtent),
                new Float3(CompoundPlan.MapHalfExtent, 40f, CompoundPlan.MapHalfExtent));

            var spawns = new List<SpawnSlot>(16);
            // Team 1 clustered near player spawn; team 2 near objective.
            spawns.Add(new SpawnSlot(1, 1, Feet(plan.PlayerSpawn), YawToward(plan.PlayerSpawn, plan.ObjectiveCenter)));
            for (int i = 0; i < plan.BotSpawns.Count && CountTeam(spawns, 1) < 6; i++)
            {
                var p = plan.BotSpawns[i];
                float dPlayer = Dist2(p, plan.PlayerSpawn);
                float dObj = Dist2(p, plan.ObjectiveCenter);
                if (dPlayer <= dObj)
                    spawns.Add(new SpawnSlot((uint)(spawns.Count + 1), 1, Feet(p), YawToward(p, plan.ObjectiveCenter)));
            }
            while (CountTeam(spawns, 1) < 6)
            {
                int n = CountTeam(spawns, 1);
                var offset = new Float3(plan.PlayerSpawn.X + 2f * n, plan.PlayerSpawn.Y, plan.PlayerSpawn.Z + 1.5f * n);
                if (!CompoundConnectivity.IsBlocked(plan.CollisionBoxes, offset.X, offset.Z, CompoundConnectivity.RequiredClearance))
                    spawns.Add(new SpawnSlot((uint)(100 + n), 1, Feet(offset), YawToward(offset, plan.ObjectiveCenter)));
                else
                    spawns.Add(new SpawnSlot((uint)(100 + n), 1, Feet(plan.PlayerSpawn), YawToward(plan.PlayerSpawn, plan.ObjectiveCenter)));
            }

            spawns.Add(new SpawnSlot(201, 2, Feet(plan.ObjectiveCenter), YawToward(plan.ObjectiveCenter, plan.PlayerSpawn)));
            for (int i = 0; i < plan.BotSpawns.Count && CountTeam(spawns, 2) < 6; i++)
            {
                var p = plan.BotSpawns[i];
                float dPlayer = Dist2(p, plan.PlayerSpawn);
                float dObj = Dist2(p, plan.ObjectiveCenter);
                if (dObj < dPlayer)
                    spawns.Add(new SpawnSlot((uint)(210 + CountTeam(spawns, 2)), 2, Feet(p), YawToward(p, plan.PlayerSpawn)));
            }
            while (CountTeam(spawns, 2) < 6)
            {
                int n = CountTeam(spawns, 2);
                var offset = new Float3(plan.ObjectiveCenter.X - 2f * n, plan.ObjectiveCenter.Y, plan.ObjectiveCenter.Z - 1.5f * n);
                if (!CompoundConnectivity.IsBlocked(plan.CollisionBoxes, offset.X, offset.Z, CompoundConnectivity.RequiredClearance))
                    spawns.Add(new SpawnSlot((uint)(220 + n), 2, Feet(offset), YawToward(offset, plan.PlayerSpawn)));
                else
                    spawns.Add(new SpawnSlot((uint)(220 + n), 2, Feet(plan.ObjectiveCenter), YawToward(plan.ObjectiveCenter, plan.PlayerSpawn)));
            }

            var patrol = Subsample(plan.RouteCorners, 64);
            if (patrol.Count == 0) patrol.Add(Feet(plan.PlayerSpawn));
            var combat = new List<Float3> { Feet(plan.ObjectiveCenter), Feet(plan.PlayerSpawn) };

            return new MapGameplayDefinition(
                mapId,
                plan.CollisionRevision,
                plan.CollisionHash,
                plan.NavRevision,
                plan.NavHash,
                bounds,
                spawns,
                patrol,
                combat);
        }

        private static int CountTeam(List<SpawnSlot> spawns, byte team)
        {
            int n = 0;
            for (int i = 0; i < spawns.Count; i++) if (spawns[i].Team == team) n++;
            return n;
        }

        private static float Dist2(Float3 a, Float3 b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return dx * dx + dz * dz;
        }

        private static float YawToward(Float3 from, Float3 to)
        {
            float dx = to.X - from.X, dz = to.Z - from.Z;
            float yaw = MathF.Atan2(dx, dz);
            // Contract aim interval is [-pi, pi).
            if (yaw >= (float)Math.PI) yaw = (float)(Math.PI - 1e-5);
            if (yaw < -(float)Math.PI) yaw = -(float)Math.PI;
            return yaw;
        }

        private static Float3 Feet(Float3 p) => new Float3(p.X, MathF.Max(0f, p.Y), p.Z);

        private static List<Float3> Subsample(IReadOnlyList<Float3> source, int max)
        {
            var list = new List<Float3>(Math.Min(max, source.Count));
            if (source.Count == 0) return list;
            if (source.Count <= max)
            {
                for (int i = 0; i < source.Count; i++) list.Add(Feet(source[i]));
                return list;
            }
            for (int i = 0; i < max; i++)
            {
                int idx = (int)((long)i * (source.Count - 1) / (max - 1));
                list.Add(Feet(source[idx]));
            }
            return list;
        }
    }
}
