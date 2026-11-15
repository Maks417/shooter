#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// ADR-071 compact TDM arena for <c>oil-depot</c>: petroleum tank farm (left), pump house mid,
    /// container/shed CQB (right), and a walkable catwalk flank with underpass (docs/features/tdm-map-layouts.md Map C).
    /// </summary>
    public static class OilArenaPlanner
    {
        public const string GeneratorId = "environment.oil.arena";
        public const string GeneratorVersion = "1.0.0";
        public const ulong StableIdBase = 30000;

        public static readonly ArenaLayout Layout = new ArenaLayout(
            halfExtentX: 42f, halfExtentZ: 54f,
            stagingMinX: -42f, stagingMaxX: 42f, stagingMinZ: -54f, stagingMaxZ: -40f,
            team1Anchor: new Float3(0f, 0f, -48f),
            team1Exits: new[] { new Float3(-28f, 0f, -37f), new Float3(0f, 0f, -37f), new Float3(28f, 0f, -37f) },
            lanePoints: new[] { new Float3(-28f, 0f, 0f), new Float3(0f, 0f, 0f), new Float3(28f, 0f, 0f) });

        private const float ContainerLength = 6f, ContainerWidth = 2.5f, ContainerHeight = 2.6f;
        private const float DeckTopY = 2.25f;
        /// <summary>Match <see cref="CompoundConnectivity.CellSize"/> so each grid cell owns at most one riser.</summary>
        private const float TreadDepth = 1.0f;
        private const float HalfCover = 1.1f;

        public static CompoundPlan Generate(ulong seed)
        {
            var b = new ArenaBoxBuilder(StableIdBase);
            b.Perimeter(Layout, height: 5f, thickness: 1f);
            BuildStaging(b);
            BuildSeparators(b);
            BuildPumpHouse(b);
            BuildMidBlocks(b);
            BuildTankFarm(b);
            BuildContainerLane(b);
            BuildApron(b);
            BuildCatwalkFlank(b);
            ArenaVisualDetail.AppendOil(b, Layout);
            return ArenaGeometry.CreatePlan(GeneratorId, GeneratorVersion, seed, Layout, b);
        }

        private static Float3 Toward(float x) => new Float3(x, 0f, 0f);

        private static void BuildStaging(ArenaBoxBuilder b)
        {
            const float chuteLength = 6f, chuteHeight = 2.6f;
            var gates = new[] { new ArenaGap(-28f, 4f), new ArenaGap(0f, 2.5f), new ArenaGap(28f, 4f) };
            // Deep gatehouse wall (2 m) so there is no walkable sliver outside staging.
            b.WallAlongX(-39f, -42f, 42f, 2f, 3f, gates);
            for (int i = 0; i < gates.Length; i++)
            {
                float half = gates[i].Width * 0.5f + 0.25f;
                b.WallAlongZ(gates[i].Center - half, -40f - chuteLength, -40f, 0.5f, chuteHeight);
                b.WallAlongZ(gates[i].Center + half, -40f - chuteLength, -40f, 0.5f, chuteHeight);
            }
            // Staging dividers with offset rear openings (no strip LOS).
            b.WallAlongZ(-14f, -54f, -40f, 0.4f, chuteHeight, new[] { new ArenaGap(-50f, 4f) });
            b.WallAlongZ(14f, -54f, -40f, 0.4f, chuteHeight, new[] { new ArenaGap(-46f, 4f) });
        }

        private static void BuildSeparators(ArenaBoxBuilder b)
        {
            var connectors = new[] { new ArenaGap(-28f, 4f), new ArenaGap(28f, 4f) };
            b.WallAlongZ(-18f, -38f, 38f, 0.5f, 3f, connectors, ArenaBoxBuilder.TagSolid, mirror: false);
            b.WallAlongZ(18f, -38f, 38f, 0.5f, 3f, connectors, ArenaBoxBuilder.TagSolid, mirror: false);
        }

        private static void BuildPumpHouse(ArenaBoxBuilder b)
        {
            // Symmetric N/S doors; mid-lane LOS is broken by the axis block outside and an on-centre pillar.
            var doors = new[]
            {
                new ArenaDoor(ArenaSide.North, 0f, 2.4f), new ArenaDoor(ArenaSide.South, 0f, 2.4f),
                new ArenaDoor(ArenaSide.East, 0f, 2.4f), new ArenaDoor(ArenaSide.West, 0f, 2.4f)
            };
            b.Shell(0f, 0f, 16f, 14f, 0.45f, 4f, doors, roof: true, mirror: false);
            // Interior pillars on z = 0 break E-W sight; centred console breaks residual N-S through the doors.
            b.AddGrounded(-3.5f, 0f, 1.2f, 1.2f, 3.5f, ArenaBoxBuilder.TagSolid, mirror: false);
            b.AddGrounded(3.5f, 0f, 1.2f, 1.2f, 3.5f, ArenaBoxBuilder.TagSolid, mirror: false);
            b.AddGrounded(0f, 0f, 1.6f, 2.0f, 2.8f, ArenaBoxBuilder.TagCover, mirror: false);
        }

        /// <summary>Plaza blocks that seal the mid-lane eye line outside the pump house.</summary>
        private static void BuildMidBlocks(ArenaBoxBuilder b)
        {
            b.AddGrounded(0f, -21f, 3.5f, 1.6f, 3f);
            b.AddGrounded(-11.5f, -18f, 5f, 3f, 3f);
            b.AddGrounded(11.5f, -18f, 5f, 3f, 3f);
            // Extra N-S sight caps between separator (x=±18) and mid plaza.
            b.AddGrounded(-14f, -30f, 5f, 1.5f, 3.2f);
            b.AddGrounded(14f, -30f, 5f, 1.5f, 3.2f);
            b.AddGrounded(-15f, -8f, 4f, 1.5f, 3.2f);
            b.AddGrounded(15f, -8f, 4f, 1.5f, 3.2f);
            b.AddCover(-6f, -24f, 3f, HalfCover, 1.2f, Toward(-6f));
            b.AddCover(6f, -24f, 3f, HalfCover, 1.2f, Toward(6f));
            b.AddCover(0f, -26f, 2.2f, HalfCover, 1.4f, Toward(0f));
            b.AddCover(-5f, -10f, 3.5f, HalfCover, 0.7f, Toward(-5f));
            b.AddCover(5f, -10f, 3.5f, HalfCover, 0.7f, Toward(5f));
        }

        private static void BuildTankFarm(ArenaBoxBuilder b)
        {
            var threat = Toward(-28f);
            // Keep x≈-28 at z=0 clear for the left lane via point.
            AddOctagonTank(b, -32f, -22f, 3.4f, 5.5f, mirror: true);
            AddOctagonTank(b, -36f, -8f, 3.0f, 5f, mirror: true);
            AddOctagonTank(b, -34f, 0f, 3.2f, 5.5f, mirror: true);
            b.AddCover(-28f, -30f, 2.2f, HalfCover, 1.0f, threat);
            b.AddCover(-38f, -16f, 1.8f, HalfCover, 1.2f, threat);
            b.AddCover(-24f, -14f, 2f, 1.2f, 1f, threat);
            b.AddCover(-30f, -4f, 2f, HalfCover, 1f, threat);
            b.AddGrounded(-21f, -18f, 3f, 4f, 3f);
            b.AddGrounded(-38.5f, -28f, 3f, 3f, 3f);
            // Long-sight caps in the outer tank lane (and mirrored +Z half). Keep (-28,0) open for the lane via.
            b.AddGrounded(-39f, -14f, 2.5f, 4f, 3.2f);
            b.AddGrounded(-24f, -32f, 4f, 2.5f, 3f);
            b.AddGrounded(-28f, -40f, 8f, 1.5f, 3.2f);
            // Full-width chicanes on the lane axis (paths go around; eye line cannot).
            b.AddGrounded(-28f, -16f, 7f, 1.6f, 3.2f);
            b.AddGrounded(-28f, -6f, 7f, 1.6f, 3.2f);
        }

        private static void AddOctagonTank(ArenaBoxBuilder b, float cx, float cz, float radius, float height, bool mirror)
        {
            // Approximate a cylinder as a solid drum plus four cardinal buttresses (mirror-safe).
            b.AddGrounded(cx, cz, radius * 1.7f, radius * 1.7f, height, ArenaBoxBuilder.TagSolid, mirror);
            b.AddGrounded(cx + radius * 0.85f, cz, 0.7f, radius * 1.1f, height, ArenaBoxBuilder.TagSolid, mirror);
            b.AddGrounded(cx - radius * 0.85f, cz, 0.7f, radius * 1.1f, height, ArenaBoxBuilder.TagSolid, mirror);
            b.AddGrounded(cx, cz + radius * 0.85f, radius * 1.1f, 0.7f, height, ArenaBoxBuilder.TagSolid, mirror);
            // When centred on z = 0 the north buttress is self-mirror; skip an extra south copy.
            if (Math.Abs(cz) > 0.0005f)
                b.AddGrounded(cx, cz - radius * 0.85f, radius * 1.1f, 0.7f, height, ArenaBoxBuilder.TagSolid, mirror);
        }

        private static void BuildContainerLane(ArenaBoxBuilder b)
        {
            var threat = Toward(28f);
            // Leave (28,0) open for the right lane via; keep catwalk column at x≈32 clear in mid rows.
            float[] rowZ = { 0f, -7f, -14f, -21f, -28f };
            for (int r = 0; r < rowZ.Length; r++)
            {
                bool patternA = (r % 2) == 0;
                float[] xs = patternA ? new[] { 23.5f, 35f } : new[] { 25.5f, 38f };
                if (r == 0) xs = new[] { 23.5f, 36f };
                if (r == rowZ.Length - 1) xs = new[] { 30f };
                for (int k = 0; k < xs.Length; k++)
                {
                    if (Math.Abs(xs[k] - 32f) < 2.2f && r >= 1 && r <= 3) continue;
                    if (r == 0 && Math.Abs(xs[k] - 28f) < 2f) continue;
                    float h = (r == 2 && k == 0) ? 5.2f : ContainerHeight;
                    b.AddCover(xs[k], rowZ[r], ContainerLength, h, ContainerWidth, threat, true, ArenaBoxBuilder.TagSolid);
                }
            }
            b.AddGrounded(39.5f, -12f, 2.5f, 6f, 3.2f);
            b.AddCover(28f, -12f, 2f, HalfCover, 1.2f, threat);
            // Sight caps on the right lane / separator strip (paths weave; eye runs ≤ 60 m).
            b.AddGrounded(28f, -16f, 7f, 1.6f, 3.2f);
            b.AddGrounded(22f, -6f, 6f, 1.6f, 3.2f);
            b.AddGrounded(24f, -32f, 5f, 1.5f, 3.2f);
        }

        private static void BuildApron(ArenaBoxBuilder b)
        {
            b.AddCover(-10f, -28f, 2.5f, HalfCover, 1.2f, Toward(-10f));
            b.AddCover(10f, -28f, 2.5f, HalfCover, 1.2f, Toward(10f));
            // Tall apron posts break the long E-W eye line across z≈-28 (half-cover alone is below eye height).
            b.AddGrounded(-8f, -28f, 1.5f, 1.5f, 3f);
            b.AddGrounded(8f, -28f, 1.5f, 1.5f, 3f);
            b.AddCover(-6f, -34f, 2f, 1.2f, 1f, Toward(-6f));
            b.AddCover(8f, -34f, 2f, 1.2f, 1f, Toward(8f));
            b.AddCover(-12f, -18f, 1.4f, HalfCover, 1.4f, Toward(-12f));
            b.AddCover(12f, -18f, 1.4f, HalfCover, 1.4f, Toward(12f));
            // Gate-front crates (match compound baffle feel).
            float[] gateXs = { -28f, 0f, 28f };
            for (int i = 0; i < gateXs.Length; i++)
            {
                float g = gateXs[i];
                b.AddCover(g - 6f, -33f, 2.2f, HalfCover, 1.0f, Toward(g - 6f));
                b.AddCover(g + 6f, -33f, 2.2f, HalfCover, 1.0f, Toward(g + 6f));
            }
        }

        private static void BuildCatwalkFlank(ArenaBoxBuilder b)
        {
            const float cx = 32f;
            // Keep stair runs (9 × 1 m) clear of the gatehouse/staging strip (z ≤ -40).
            const float deckZ0 = -22f;
            const float deckZ1 = -8f;
            float deckLen = deckZ1 - deckZ0;
            float deckCz = (deckZ0 + deckZ1) * 0.5f;
            b.AddCatwalkDeck(cx, deckCz, 3.4f, deckLen, DeckTopY, rails: true, mirror: true);

            int steps = Math.Max(1, (int)Math.Ceiling(DeckTopY / CompoundConnectivity.MaxStepMeters - 1e-4f));
            float stairSpan = steps * TreadDepth;
            b.AddStairRun(cx, deckZ0 - stairSpan, 3.4f, DeckTopY, TreadDepth, ascendingPositiveZ: true, mirror: true);
            b.AddStairRun(cx, deckZ1 + stairSpan, 3.4f, DeckTopY, TreadDepth, ascendingPositiveZ: false, mirror: true);

            b.AddGrounded(cx - 1.4f, deckCz, 0.35f, 0.35f, DeckTopY - 0.05f);
            b.AddGrounded(cx + 1.4f, deckCz, 0.35f, 0.35f, DeckTopY - 0.05f);
        }
    }
}
