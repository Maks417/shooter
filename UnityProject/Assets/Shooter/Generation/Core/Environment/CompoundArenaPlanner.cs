#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// T076 compact TDM arena for <c>training-compound</c> (docs/features/tdm-map-layouts.md, Map A).
    /// Placeholder grammar: perimeter, staging walls with three gates, lane baffles, a central block and scattered cover.
    /// </summary>
    public static class CompoundArenaPlanner
    {
        public const string GeneratorId = "environment.compound.arena";
        public const string GeneratorVersion = "1.0.0";
        public const ulong StableIdBase = 10000;

        public static readonly ArenaLayout Layout = new ArenaLayout(
            halfExtentX: 44f, halfExtentZ: 52f,
            stagingMinX: -44f, stagingMaxX: 44f, stagingMinZ: -52f, stagingMaxZ: -38f,
            team1Anchor: new Float3(0f, 0f, -46f),
            team1Exits: new[] { new Float3(-30f, 0f, -35f), new Float3(0f, 0f, -35f), new Float3(30f, 0f, -35f) },
            lanePoints: new[] { new Float3(-30f, 0f, 0f), new Float3(0f, 0f, 0f), new Float3(30f, 0f, 0f) });

        public static CompoundPlan Generate(ulong seed)
        {
            var b = new ArenaBoxBuilder(StableIdBase);
            b.Perimeter(Layout, height: 5f, thickness: 1f);

            // Spawn wall with one 4 m gate per lane.
            var gates = new[] { new ArenaGap(-30f, 4f), new ArenaGap(0f, 4f), new ArenaGap(30f, 4f) };
            b.WallAlongX(-37f, -44f, 44f, 0.4f, 3f, gates);

            // Central structure: two blocks leave a 4 m corridor through the centre; pillars break the N/S sight lines.
            b.AddGrounded(-5f, 0f, 6f, 6f, 3f, ArenaBoxBuilder.TagSolid, mirror: false);
            b.AddGrounded(5f, 0f, 6f, 6f, 3f, ArenaBoxBuilder.TagSolid, mirror: false);
            b.AddGrounded(0f, -28f, 6f, 1.5f, 2.4f);

            // Staging dividers with offset openings (no long sight line along the staging strip).
            b.WallAlongZ(-14f, -52f, -38f, 0.4f, 2.4f, new[] { new ArenaGap(-48f, 4f) });
            b.WallAlongZ(14f, -52f, -38f, 0.4f, 2.4f, new[] { new ArenaGap(-42f, 4f) });

            // Baffles between spawn wall and centre, open at each lane.
            b.WallAlongX(-20f, -44f, 44f, 0.4f, 2.4f, new[] { new ArenaGap(-30f, 4f), new ArenaGap(0f, 4f), new ArenaGap(30f, 4f) });

            // Lane caps break the long side lanes in the middle.
            b.AddGrounded(-30f, -10f, 8f, 2f, 2.4f);
            b.AddGrounded(30f, -10f, 8f, 2f, 2.4f);

            // Lane separators seal the three lanes between the staging walls (placeholder: no cross connectors yet).
            b.WallAlongZ(-18f, -36f, 36f, 0.4f, 2.4f, null, ArenaBoxBuilder.TagSolid, mirror: false);
            b.WallAlongZ(18f, -36f, 36f, 0.4f, 2.4f, null, ArenaBoxBuilder.TagSolid, mirror: false);

            // Scattered half covers (1.2 m) on a staggered 12 m pitch.
            var threat = new Float3(0f, 0f, 0f);
            float[] rows = { -31f, -14f, 0f };
            for (int iz = 0; iz < rows.Length; iz++)
            for (int ix = -3; ix <= 3; ix++)
            {
                float x = ix * 12f + (iz % 2 == 0 ? 0f : 6f);
                if (Math.Abs(x) > 40f) continue;
                if (rows[iz] == 0f && Math.Abs(x) < 10f) continue; // keep the centre corridors open
                if (CompoundConnectivity.IsBlocked(b.Boxes, x, rows[iz], 1.5f)) continue;
                b.AddCover(x, rows[iz], 2f, 1.2f, 1f, threat);
            }

            return ArenaGeometry.CreatePlan(GeneratorId, GeneratorVersion, seed, Layout, b);
        }
    }
}
