#nullable enable
using System;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// ADR-071/ADR-074 compact TDM arena for <c>oil-depot</c> (docs/features/tdm-map-layouts.md Map C): the petroleum
    /// tank farm (left), the pump house (middle) and the walkable catwalk flank (right), shrunk to 64 x 80 m. The catwalk
    /// is now one continuous deck spanning z = 0 with a stair at each end, so both teams contest the same high ground and
    /// the underpass. Spawns sit behind tanker trailers at the loading rack. Gameplay collision mirrors across z = 0.
    /// </summary>
    public static class OilArenaPlanner
    {
        public const string GeneratorId = "environment.oil.arena";
        public const string GeneratorVersion = "2.0.0";
        public const ulong StableIdBase = 30000;

        public static readonly ArenaLayout Layout = new ArenaLayout(
            halfExtentX: 32f, halfExtentZ: 40f,
            stagingMinX: -32f, stagingMaxX: 32f, stagingMinZ: -40f, stagingMaxZ: -30f,
            team1Anchor: new Float3(0f, 0f, -34f),
            team1Exits: new[] { new Float3(-19.5f, 0f, -29f), new Float3(0f, 0f, -29f), new Float3(19.5f, 0f, -29f) },
            lanePoints: new[] { new Float3(-26f, 0f, 0f), new Float3(0f, 0f, 0f), new Float3(28.5f, 0f, 0f) });

        public const float CatwalkX = 22f;
        public const float CatwalkWidth = 3.4f;
        public const float CatwalkHalfLength = 6f;
        public const float DeckTopY = 2.25f;
        /// <summary>Match <see cref="CompoundConnectivity.CellSize"/> so each grid cell owns at most one riser.</summary>
        private const float TreadDepth = 1.0f;
        private const float HalfCover = 1.1f;
        private const float TankerHeight = 3.2f, TankerWidth = 2.6f;

        /// <summary>Team-1 half tank drums (centre x/z, planner radius, height); each is mirrored to +Z.</summary>
        public static readonly float[,] Tanks =
        {
            { -22f, -19f, 3.4f, 5.5f },
            { -27.5f, -8f, 3.0f, 5f }
        };

        public static CompoundPlan Generate(ulong seed)
        {
            var b = new ArenaBoxBuilder(StableIdBase);
            b.Perimeter(Layout, height: 5f, thickness: 1f);
            BuildLoadingRack(b);
            BuildPumpHouse(b);
            BuildApron(b);
            BuildTankFarm(b);
            BuildCatwalkFlank(b);
            ArenaVisualDetail.AppendOil(b, Layout);
            return ArenaGeometry.CreatePlan(GeneratorId, GeneratorVersion, seed, Layout, b);
        }

        private static Float3 Toward(float x) => new Float3(x, 0f, 0f);

        /// <summary>Spawn front: tanker trailers parked at the loading rack, a pump island on the centre exit, strip dividers.</summary>
        private static void BuildLoadingRack(ArenaBoxBuilder b)
        {
            b.AddVehicle(ArenaFeatureKind.Tanker, -27.5f, -29f, 9f, TankerWidth, TankerHeight, alongX: true, Toward(-27.5f), facing: -1f);
            b.AddVehicle(ArenaFeatureKind.Tanker, -9.25f, -29f, 13.5f, TankerWidth, TankerHeight, alongX: true, Toward(-9.25f), facing: 1f);
            b.AddVehicle(ArenaFeatureKind.Tanker, 9.25f, -29f, 13.5f, TankerWidth, TankerHeight, alongX: true, Toward(9.25f), facing: -1f);
            b.AddVehicle(ArenaFeatureKind.Tanker, 27.5f, -29f, 9f, TankerWidth, TankerHeight, alongX: true, Toward(27.5f), facing: 1f);
            // Pump island: wider than the 5 m gate so no eye line from the middle reaches through it.
            b.AddVehicle(ArenaFeatureKind.Booth, 0f, -25.3f, 5.4f, 1.6f, 3f, alongX: true, Toward(0f));
            // Gate chute: two tankers parked along Z inside the centre gate; from the gate mouth only the chute shows.
            b.AddVehicle(ArenaFeatureKind.Tanker, -3.8f, -33.65f, 6.7f, TankerWidth, TankerHeight, alongX: false, Toward(-3.8f), facing: 1f);
            b.AddVehicle(ArenaFeatureKind.Tanker, 3.8f, -33.65f, 6.7f, TankerWidth, TankerHeight, alongX: false, Toward(3.8f), facing: 1f);
            // Drum stacks reach the tanker row and split the staging strip; the side pockets connect behind them.
            // West stack: open behind; east stacks: open in the middle (offset openings, no strip eye line).
            b.AddGrounded(-16f, -34.3f, 2.4f, 7.4f, 2.4f, ArenaBoxBuilder.TagCover);
            b.AddGrounded(16f, -38f, 2.4f, 4f, 2.4f, ArenaBoxBuilder.TagCover);
            b.AddGrounded(16f, -32.05f, 2.4f, 2.9f, 2.4f, ArenaBoxBuilder.TagCover);
        }

        private static void BuildPumpHouse(ArenaBoxBuilder b)
        {
            var doors = new[]
            {
                new ArenaDoor(ArenaSide.North, 0f, 2.4f), new ArenaDoor(ArenaSide.South, 0f, 2.4f),
                new ArenaDoor(ArenaSide.East, 0f, 2.4f), new ArenaDoor(ArenaSide.West, 0f, 2.4f)
            };
            b.Shell(0f, 0f, 14f, 12f, 0.45f, 4f, doors, roof: true, mirror: false);
            // Interior pillars on z = 0 break E-W sight; the centred console breaks N-S sight through the doors.
            b.AddGrounded(-3.5f, 0f, 1.2f, 1.2f, 3.5f, ArenaBoxBuilder.TagSolid, mirror: false);
            b.AddGrounded(3.5f, 0f, 1.2f, 1.2f, 3.5f, ArenaBoxBuilder.TagSolid, mirror: false);
            b.AddGrounded(0f, 0f, 1.6f, 2.0f, 2.8f, ArenaBoxBuilder.TagCover, mirror: false);
        }

        /// <summary>Pump-house apron: valve manifolds and pipe-stand cover.</summary>
        private static void BuildApron(ArenaBoxBuilder b)
        {
            b.AddGrounded(0f, -12.5f, 3.5f, 1.6f, 3f);
            b.AddGrounded(-10f, -16f, 4f, 2f, 3f);
            b.AddGrounded(10f, -12f, 4f, 3f, 3f);
            b.AddCover(-5f, -20f, 3f, HalfCover, 1.2f, Toward(-5f));
            b.AddCover(5.5f, -21f, 3f, HalfCover, 1.2f, Toward(5.5f));
            b.AddCover(-9f, -8f, 1.4f, HalfCover, 3f, Toward(-9f));
            b.AddCover(9f, -8f, 1.4f, HalfCover, 3f, Toward(9f));
            // Valve tower against the pump house's east wall breaks diagonals past its corner.
            b.AddGrounded(8f, -4.5f, 1.4f, 1.4f, 3f);
            b.AddGrounded(13.2f, -3.5f, 2f, 1.4f, 3f);
        }

        private static void BuildTankFarm(ArenaBoxBuilder b)
        {
            for (int i = 0; i < Tanks.GetLength(0); i++)
            {
                AddOctagonTank(b, Tanks[i, 0], Tanks[i, 1], Tanks[i, 2], Tanks[i, 3]);
                b.RecordFeature(ArenaFeatureKind.Tank, Tanks[i, 0], Tanks[i, 1], Tanks[i, 2] * 2f, Tanks[i, 3], Tanks[i, 2] * 2f, 1f);
            }
            var threat = Toward(-24f);
            // Pipe stands and valve pits between the tanks (keep the lane via (-26, 0) open).
            b.AddCover(-29f, -17f, 2f, HalfCover, 1.2f, threat);
            b.AddCover(-17.5f, -13.5f, 2f, 2.6f, 1.5f, threat);
            b.AddCover(-21f, -3f, 2.5f, HalfCover, 1f, threat);
            // Bund-wall stub and pipe rack: tall caps on the tank-farm lane eye lines.
            b.AddGrounded(-15f, -23f, 2f, 4f, 3f);
            b.AddGrounded(-30f, -24f, 3f, 2f, 3f);
            // Pipe rack west of the pump house: catches the diagonal eye lines across the apron.
            b.AddGrounded(-14f, -6.75f, 1.2f, 10.5f, 3f);
            b.RecordFeature(ArenaFeatureKind.PipeRack, -14f, -6.75f, 1.2f, 3f, 10.5f, 1f);
        }

        private static void AddOctagonTank(ArenaBoxBuilder b, float cx, float cz, float radius, float height)
        {
            // Approximate a cylinder as a solid drum plus four cardinal buttresses (mirror-safe).
            b.AddGrounded(cx, cz, radius * 1.7f, radius * 1.7f, height, ArenaBoxBuilder.TagSolid);
            b.AddGrounded(cx + radius * 0.85f, cz, 0.7f, radius * 1.1f, height, ArenaBoxBuilder.TagSolid);
            b.AddGrounded(cx - radius * 0.85f, cz, 0.7f, radius * 1.1f, height, ArenaBoxBuilder.TagSolid);
            b.AddGrounded(cx, cz + radius * 0.85f, radius * 1.1f, 0.7f, height, ArenaBoxBuilder.TagSolid);
            b.AddGrounded(cx, cz - radius * 0.85f, radius * 1.1f, 0.7f, height, ArenaBoxBuilder.TagSolid);
        }

        /// <summary>One catwalk deck across z = 0 (self-mirrored), a stair at each end, pipe-rack cover beneath and beside it.</summary>
        private static void BuildCatwalkFlank(ArenaBoxBuilder b)
        {
            b.AddCatwalkDeck(CatwalkX, 0f, CatwalkWidth, CatwalkHalfLength * 2f, DeckTopY, rails: true, mirror: false);
            int steps = Math.Max(1, (int)Math.Ceiling(DeckTopY / CompoundConnectivity.MaxStepMeters - 1e-4f));
            float stairSpan = steps * TreadDepth;
            // South stair climbs +Z to the deck; its mirror climbs -Z from the north.
            b.AddStairRun(CatwalkX, -CatwalkHalfLength - stairSpan, CatwalkWidth, DeckTopY, TreadDepth, ascendingPositiveZ: true, mirror: true);
            // Deck support posts (outside the underpass corridor).
            b.AddGrounded(CatwalkX - 1.4f, -3.5f, 0.35f, 0.35f, DeckTopY - 0.05f);
            b.AddGrounded(CatwalkX + 1.4f, -3.5f, 0.35f, 0.35f, DeckTopY - 0.05f);

            var threat = Toward(26f);
            // East alley: tanker on a siding plus pipe-stand cover.
            b.AddVehicle(ArenaFeatureKind.Tanker, 29.5f, -16f, 8f, 2.6f, 3.2f, alongX: false, threat, facing: 1f);
            b.AddCover(26f, -7f, 2f, 2.6f, 1.2f, threat);
            // West of the catwalk: pipe-rack stub and valve cover.
            b.AddGrounded(17.5f, -11f, 2f, 6f, 3f);
            b.AddCover(17f, -2.5f, 1.4f, HalfCover, 2f, threat);
            b.AddCover(20.5f, -18.5f, 3f, 2.6f, 1.2f, threat);
        }
    }
}
