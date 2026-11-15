#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Generation.Environment;

namespace Shooter.Content.Maps
{
    /// <summary>Builds Game-v1 catalog/gameplay entries from the frozen compound/depot planner seeds.</summary>
    public static class CompoundCatalogFactory
    {
        public static MapGameplayDefinition BuildGameplay(MapId mapId, ulong seed)
        {
            // Catalog maps always use the approved recipe seed (ProductMapPlanner); the legacy seed parameter is retained for callers.
            var plan = ProductMapPlanner.Generate(mapId.Value);
            ProductMapPlanner.ValidateOrThrow(mapId.Value, plan);

            var bounds = new Aabb(
                new Float3(-CompoundPlan.MapHalfExtent, -2f, -CompoundPlan.MapHalfExtent),
                new Float3(CompoundPlan.MapHalfExtent, 40f, CompoundPlan.MapHalfExtent));

            var candidates = MapSpawnLayout.BuildBothTeams(plan);
            var spawns = new List<SpawnSlot>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                spawns.Add(new SpawnSlot(c.SlotId, c.Team, c.Position, c.Yaw));
            }

            var patrol = Subsample(plan.RouteCorners, 64);
            if (patrol.Count == 0) patrol.Add(Feet(plan.PlayerSpawn));
            // Combat destinations include flank/covered-route anchors for T057 consumers.
            var combat = new List<Float3>();
            var destinations = ProductMapPlanner.CombatDestinations(plan);
            for (int i = 0; i < destinations.Count; i++) combat.Add(Feet(destinations[i]));

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
