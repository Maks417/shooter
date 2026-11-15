#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// T076 compact TDM arena for <c>depot-yard</c> (docs/features/tdm-map-layouts.md, Map B).
    /// Placeholder grammar: perimeter, staging walls with three gates, lane baffles, a central block and scattered cover.
    /// </summary>
    public static class DepotArenaPlanner
    {
        public const string GeneratorId = "environment.depot.arena";
        public const string GeneratorVersion = "1.0.0";
        public const ulong StableIdBase = 20000;

        public static readonly ArenaLayout Layout = new ArenaLayout(
            halfExtentX: 40f, halfExtentZ: 56f,
            stagingMinX: -40f, stagingMaxX: 40f, stagingMinZ: -56f, stagingMaxZ: -42f,
            team1Anchor: new Float3(0f, 0f, -50f),
            team1Exits: new[] { new Float3(-28f, 0f, -39f), new Float3(0f, 0f, -39f), new Float3(26f, 0f, -39f) },
            lanePoints: new[] { new Float3(-28f, 0f, 0f), new Float3(0f, 0f, 0f), new Float3(26f, 0f, 0f) });

        public static CompoundPlan Generate(ulong seed)
        {
            var b = new ArenaBoxBuilder(StableIdBase);
            b.Perimeter(Layout, height: 5f, thickness: 1f);

            // Gatehouse wall with one gate per lane.
            var gates = new[] { new ArenaGap(-28f, 4f), new ArenaGap(0f, 4f), new ArenaGap(26f, 4f) };
            b.WallAlongX(-41f, -40f, 40f, 0.4f, 3f, gates);

            // Central warehouse block (solid placeholder; doors arrive with the real layout).
            b.AddGrounded(0f, 0f, 20f, 8f, 4f, ArenaBoxBuilder.TagSolid, mirror: false);

            // Baffles between gatehouse and warehouse, open at each lane.
            b.WallAlongX(-24f, -40f, 40f, 0.4f, 2.4f, new[] { new ArenaGap(-28f, 4f), new ArenaGap(0f, 4f), new ArenaGap(26f, 4f) });

            // Lane caps break the side lanes in the middle.
            b.AddGrounded(-28f, -12f, 8f, 2f, 2.4f);
            b.AddGrounded(26f, -12f, 8f, 2f, 2.4f);

            // Lane separators seal the three lanes between the gatehouse walls (placeholder: no cross connectors yet).
            b.WallAlongZ(-14f, -41f, 41f, 0.4f, 2.4f, null, ArenaBoxBuilder.TagSolid, mirror: false);
            b.WallAlongZ(14f, -41f, 41f, 0.4f, 2.4f, null, ArenaBoxBuilder.TagSolid, mirror: false);

            // Staging dividers with offset openings (no long sight line along the staging strip).
            b.WallAlongZ(-13f, -56f, -42f, 0.4f, 2.4f, new[] { new ArenaGap(-52f, 4f) });
            b.WallAlongZ(13f, -56f, -42f, 0.4f, 2.4f, new[] { new ArenaGap(-46f, 4f) });

            // Scattered half covers (1.2 m) on a staggered 12 m pitch.
            var threat = new Float3(0f, 0f, 0f);
            float[] rows = { -33f, -14f, 0f };
            for (int iz = 0; iz < rows.Length; iz++)
            for (int ix = -3; ix <= 3; ix++)
            {
                float x = ix * 12f + (iz % 2 == 0 ? 0f : 6f);
                if (Math.Abs(x) > 36f) continue;
                if (rows[iz] == 0f && Math.Abs(x) < 16f) continue; // keep the centre corridors open
                if (CompoundConnectivity.IsBlocked(b.Boxes, x, rows[iz], 1.5f)) continue;
                b.AddCover(x, rows[iz], 2f, 1.2f, 1f, threat);
            }

            // Lane-edge crates close the remaining cover gaps beside the perimeter.
            b.AddCover(-36f, -14f, 2f, 1.2f, 1f, threat);
            b.AddCover(36f, -14f, 2f, 1.2f, 1f, threat);

            return ArenaGeometry.CreatePlan(GeneratorId, GeneratorVersion, seed, Layout, b);
        }
    }
}
