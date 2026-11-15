#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// T076 compact TDM arena for <c>depot-yard</c> (docs/features/tdm-map-layouts.md, Map B).
    /// Industrial, linear and container-driven: a staggered-boxcar rail siding (left), a central warehouse with
    /// shelving aisles (middle) and a staggered container-stack maze (right), behind a 2 m deep gatehouse wall with
    /// funnelled gates. Everything is built from <see cref="ArenaBoxBuilder"/> helpers so ids are deterministic and
    /// gameplay collision is mirror-symmetric across z = 0.
    /// </summary>
    public static class DepotArenaPlanner
    {
        public const string GeneratorId = "environment.depot.arena";
        public const string GeneratorVersion = "2.0.0";
        public const ulong StableIdBase = 20000;

        public static readonly ArenaLayout Layout = new ArenaLayout(
            halfExtentX: 40f, halfExtentZ: 56f,
            stagingMinX: -40f, stagingMaxX: 40f, stagingMinZ: -56f, stagingMaxZ: -42f,
            team1Anchor: new Float3(0f, 0f, -50f),
            team1Exits: new[] { new Float3(-28f, 0f, -39f), new Float3(0f, 0f, -39f), new Float3(26f, 0f, -39f) },
            lanePoints: new[] { new Float3(-28f, 0f, 0f), new Float3(0f, 0f, 0f), new Float3(26f, 0f, 0f) });

        private const float ContainerLength = 6f, ContainerWidth = 2.5f, ContainerHeight = 2.6f, DoubleHeight = 5.2f;

        public static CompoundPlan Generate(ulong seed)
        {
            var b = new ArenaBoxBuilder(StableIdBase);
            b.Perimeter(Layout, height: 5f, thickness: 1f);
            BuildStaging(b);
            BuildSeparators(b);
            BuildWarehouse(b);
            BuildApron(b);
            BuildRailLane(b);
            BuildContainerLane(b);
            ArenaVisualDetail.AppendDepot(b, Layout);
            return ArenaGeometry.CreatePlan(GeneratorId, GeneratorVersion, seed, Layout, b);
        }

        /// <summary>Gatehouse wall (2 m deep so there is no walkable sliver outside staging), funnelled gates, staging dividers.</summary>
        private static void BuildStaging(ArenaBoxBuilder b)
        {
            const float chuteLength = 6f, chuteHeight = 2.6f;
            var gates = new[] { new ArenaGap(-28f, 4f), new ArenaGap(0f, 2.5f), new ArenaGap(26f, 4f) };
            b.WallAlongX(-41f, -40f, 40f, 2f, 3f, gates);
            // Baffle chutes just inside each gate: a straight 3-4 m funnel, so outside eyes only see down the funnel axis.
            for (int i = 0; i < gates.Length; i++)
            {
                float half = gates[i].Width * 0.5f + 0.25f;
                b.WallAlongZ(gates[i].Center - half, -42f - chuteLength, -42f, 0.5f, chuteHeight);
                b.WallAlongZ(gates[i].Center + half, -42f - chuteLength, -42f, 0.5f, chuteHeight);
            }
            // Staging dividers with offset back openings (no sight line along the staging strip).
            b.WallAlongZ(-13f, -56f, -42f, 0.4f, chuteHeight, new[] { new ArenaGap(-52f, 4f) });
            b.WallAlongZ(13f, -56f, -42f, 0.4f, chuteHeight, new[] { new ArenaGap(-46f, 4f) });
        }

        /// <summary>Full-height lane separators at x = -20 and +20 with 4 m cross connectors at z = -30 and +30.</summary>
        private static void BuildSeparators(ArenaBoxBuilder b)
        {
            var connectors = new[] { new ArenaGap(-30f, 4f), new ArenaGap(30f, 4f) };
            b.WallAlongZ(-20f, -40f, 40f, 0.5f, 3f, connectors, ArenaBoxBuilder.TagSolid, mirror: false);
            b.WallAlongZ(20f, -40f, 40f, 0.5f, 3f, connectors, ArenaBoxBuilder.TagSolid, mirror: false);
        }

        /// <summary>22 x 26 m warehouse, 5 m walls, doors at x = +-3 on both short ends, a side door each long side, shelving aisles, dock lips.</summary>
        private static void BuildWarehouse(ArenaBoxBuilder b)
        {
            var doors = new[]
            {
                new ArenaDoor(ArenaSide.North, -3f, 3f), new ArenaDoor(ArenaSide.North, 3f, 3f),
                new ArenaDoor(ArenaSide.South, -3f, 3f), new ArenaDoor(ArenaSide.South, 3f, 3f),
                new ArenaDoor(ArenaSide.East, 0f, 2.4f), new ArenaDoor(ArenaSide.West, 0f, 2.4f)
            };
            b.Shell(0f, 0f, 22f, 26f, 0.5f, 5f, doors, roof: true, mirror: false);

            // Shelving rows run along X (2.2 m high, 1.2 m deep); a 3 m cross aisle at x = 0 and 3 m end aisles keep every aisle open at both ends.
            float[] rows = { -3f, -7.5f };
            for (int r = 0; r < rows.Length; r++)
            {
                b.AddGrounded(-4.5f, rows[r], 6f, 1.2f, 2.2f, ArenaBoxBuilder.TagCover);
                b.AddGrounded(4.5f, rows[r], 6f, 1.2f, 2.2f, ArenaBoxBuilder.TagCover);
            }

            // Loading-dock lips (1.1 m) outside the N/S doors; they flank the doors, never block them.
            b.AddGrounded(0f, -13.6f, 3f, 1.2f, 1.1f, ArenaBoxBuilder.TagCover);
            b.AddGrounded(-8f, -13.6f, 3f, 1.2f, 1.1f, ArenaBoxBuilder.TagCover);
            b.AddGrounded(8f, -13.6f, 3f, 1.2f, 1.1f, ArenaBoxBuilder.TagCover);
        }

        /// <summary>Yard apron between the gatehouse and the warehouse: flank stacks break the z = -30 connector line, corner stacks cap the side strips.</summary>
        private static void BuildApron(ArenaBoxBuilder b)
        {
            var threat = new Float3(0f, 0f, 0f);
            AddContainer(b, -9f, -30f, true, DoubleHeight, threat);
            AddContainer(b, 9f, -30f, true, DoubleHeight, threat);
            AddContainer(b, -16.75f, -19f, true, ContainerHeight, threat);
            AddContainer(b, 16.75f, -19f, true, ContainerHeight, threat);
            // Half-height pallet stacks beside the central approach.
            b.AddCover(-5f, -22f, 2f, 1.2f, 1f, threat);
            b.AddCover(6f, -35f, 2f, 1.2f, 1f, threat);
            b.AddCover(-6f, -36f, 2f, 1.2f, 1f, threat);
        }

        /// <summary>Rail siding: three staggered boxcars on one track bed, buffers, switch huts that cap the sightline at 60 m.</summary>
        private static void BuildRailLane(ArenaBoxBuilder b)
        {
            var threat = new Float3(-30f, 0f, 0f);
            // Boxcars 3.0 x 3.2 x 12 m, long axis Z, staggered between x = -34 and x = -26.
            b.AddGrounded(-34f, -22f, 3f, 12f, 3.2f);
            b.AddGrounded(-26f, 0f, 3f, 12f, 3.2f, ArenaBoxBuilder.TagSolid, mirror: false);
            // Switch huts / tie stacks that close the remaining straight sight lines.
            b.AddGrounded(-30f, -12f, 5f, 3f, 3f);
            b.AddGrounded(-22.25f, -12f, 4f, 3f, 3f);
            b.AddGrounded(-37.75f, 0f, 4.5f, 6f, 3f, ArenaBoxBuilder.TagSolid, mirror: false);
            // Tool shed behind the z = -30 connector: stops the yard-to-lane diagonal sight line.
            b.AddGrounded(-26f, -32.5f, 3f, 3f, 3f);
            // Rail buffers (1.1 m) at the siding head and trackside crates.
            b.AddCover(-31.5f, -36.5f, 1.6f, 1.1f, 0.8f, threat);
            b.AddCover(-28.5f, -36.5f, 1.6f, 1.1f, 0.8f, threat);
            b.AddCover(-36f, -32f, 2f, 1.2f, 1f, threat);
            b.AddCover(-22.5f, -36.5f, 2f, 1.2f, 1f, threat);
            b.AddCover(-29f, -5.5f, 2f, 1.2f, 1f, threat);
        }

        /// <summary>
        /// Container maze: a centre row on z = 0 plus five staggered rows per half. Gap patterns alternate (A closes the west end,
        /// B closes the east end), so every aisle stays open at both ends, z-lines never run past two rows and the alleys twist.
        /// </summary>
        private static void BuildContainerLane(ArenaBoxBuilder b)
        {
            var threat = new Float3(30f, 0f, 0f);
            // Rows from the centre outwards; the last row (behind the gate yard) is a single container.
            float[] rowZ = { 0f, -6.5f, -13f, -19.5f, -26f, -32.5f };
            for (int r = 0; r < rowZ.Length; r++)
            {
                bool patternA = (r % 2) == 0;
                float[] xs = patternA ? new[] { 23.25f, 33.25f } : new[] { 27.5f, 37f };
                if (r == rowZ.Length - 1) xs = new[] { 30.25f };
                for (int k = 0; k < xs.Length; k++)
                {
                    // Hard sight breaks: a few double-stacked containers.
                    bool stacked = (r == 0 && k == 1) || (r == 2 && k == 1) || (r == 4 && k == 0) || r == rowZ.Length - 1;
                    AddContainer(b, xs[k], rowZ[r], true, stacked ? DoubleHeight : ContainerHeight, threat);
                }
            }
        }

        private static void AddContainer(ArenaBoxBuilder b, float x, float z, bool alongX, float height, Float3 threat)
        {
            float sx = alongX ? ContainerLength : ContainerWidth;
            float sz = alongX ? ContainerWidth : ContainerLength;
            b.AddCover(x, z, sx, height, sz, threat, true, ArenaBoxBuilder.TagSolid);
        }
    }
}
