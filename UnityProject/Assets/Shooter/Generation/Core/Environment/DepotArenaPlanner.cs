#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// ADR-074 compact TDM arena for <c>depot-yard</c> ("rail crossing", docs/features/tdm-map-layouts.md Map B).
    /// A train stands across the middle of the yard on z = 0: two strings of coupled cars (boxcars plus one low
    /// flatcar you can shoot over but not climb) leave three crossings — a wide centre crossing and one along each
    /// side wall. Each half has a container stack yard on the left, a loading shed (covered route) on the right and
    /// an open middle yard with a signal box. Spawns sit behind parked semi-trailers and a gate booth. Gameplay
    /// collision is mirror-symmetric across z = 0 and every blocking box is a readable object.
    /// </summary>
    public static class DepotArenaPlanner
    {
        public const string GeneratorId = "environment.depot.arena";
        public const string GeneratorVersion = "3.0.0";
        public const ulong StableIdBase = 20000;

        public static readonly ArenaLayout Layout = new ArenaLayout(
            halfExtentX: 32f, halfExtentZ: 40f,
            stagingMinX: -32f, stagingMaxX: 32f, stagingMinZ: -40f, stagingMaxZ: -30f,
            team1Anchor: new Float3(0f, 0f, -34f),
            team1Exits: new[] { new Float3(-19.5f, 0f, -29f), new Float3(0f, 0f, -29f), new Float3(19.5f, 0f, -29f) },
            lanePoints: new[] { new Float3(-30f, 0f, 0f), new Float3(0f, 0f, 0f), new Float3(30f, 0f, 0f) });

        private const float ContainerLength = 6f, ContainerWidth = 2.5f, ContainerHeight = 2.6f, DoubleHeight = 5.2f;
        private const float BoxcarHeight = 3.4f, CarWidth = 3.0f, FlatcarHeight = 1.2f;
        private const float TrailerHeight = 3.2f, TrailerWidth = 2.6f;

        public static CompoundPlan Generate(ulong seed)
        {
            var b = new ArenaBoxBuilder(StableIdBase);
            b.Perimeter(Layout, height: 5f, thickness: 1f);
            BuildTrain(b);
            BuildGateYard(b);
            BuildMiddleYard(b);
            BuildContainerYard(b);
            BuildLoadingShed(b);
            ArenaVisualDetail.AppendDepot(b, Layout);
            return ArenaGeometry.CreatePlan(GeneratorId, GeneratorVersion, seed, Layout, b);
        }

        private static Float3 Toward(float x) => new Float3(x, 0f, 0f);

        /// <summary>The train on z = 0 (self-mirrored): crossings at x in [-32,-28], [-3,3] and [28,32].</summary>
        private static void BuildTrain(ArenaBoxBuilder b)
        {
            var north = new Float3(0f, 0f, 10f);
            AddCar(b, ArenaFeatureKind.Boxcar, -28f, -16.4f, BoxcarHeight, north);
            AddCar(b, ArenaFeatureKind.Flatcar, -16.4f, -3f, FlatcarHeight, north);
            // Tarped machinery on the flatcar's east end: shoot over the bare west half only.
            var load = new Float3(-6f, FlatcarHeight + 1.1f, 0f);
            var loadSize = new Float3(5f, 2.2f, 2.6f);
            b.Add(load, loadSize, ArenaBoxBuilder.TagSolid, mirror: false);
            b.RecordFeature(ArenaFeatureKind.Cargo, load.X, load.Z, loadSize.X, loadSize.Y, loadSize.Z, 1f, mirror: false, baseY: FlatcarHeight);
            AddCar(b, ArenaFeatureKind.Boxcar, 3f, 15.5f, BoxcarHeight, north);
            AddCar(b, ArenaFeatureKind.Boxcar, 15.5f, 28f, BoxcarHeight, north);
        }

        private static void AddCar(ArenaBoxBuilder b, ArenaFeatureKind kind, float x0, float x1, float height, Float3 threat)
            => b.AddVehicle(kind, (x0 + x1) * 0.5f, 0f, x1 - x0, CarWidth, height, alongX: true, threat, mirror: false);

        /// <summary>Spawn front: semi-trailers parked nose to tail, a gate booth on the centre exit, strip dividers.</summary>
        private static void BuildGateYard(ArenaBoxBuilder b)
        {
            b.AddVehicle(ArenaFeatureKind.Trailer, -27.5f, -29f, 9f, TrailerWidth, TrailerHeight, alongX: true, Toward(-27.5f), facing: 1f);
            b.AddVehicle(ArenaFeatureKind.Trailer, -9.25f, -29f, 13.5f, TrailerWidth, TrailerHeight, alongX: true, Toward(-9.25f), facing: -1f);
            b.AddVehicle(ArenaFeatureKind.Trailer, 9.25f, -29f, 13.5f, TrailerWidth, TrailerHeight, alongX: true, Toward(9.25f), facing: 1f);
            b.AddVehicle(ArenaFeatureKind.Trailer, 27.5f, -29f, 9f, TrailerWidth, TrailerHeight, alongX: true, Toward(27.5f), facing: -1f);
            // Gate booth just outside the centre exit hides the staging pocket from the crossing.
            // It is wider than the 5 m gate, so no eye line from the crossing reaches through the gate.
            b.AddVehicle(ArenaFeatureKind.Booth, 0f, -25.3f, 5.4f, 1.6f, 3f, alongX: true, Toward(0f));
            // Gate chute: two trailers parked along Z inside the centre gate; from the gate mouth only the chute shows.
            b.AddVehicle(ArenaFeatureKind.Trailer, -3.8f, -33.65f, 6.7f, TrailerWidth, TrailerHeight, alongX: false, Toward(-3.8f), facing: 1f);
            b.AddVehicle(ArenaFeatureKind.Trailer, 3.8f, -33.65f, 6.7f, TrailerWidth, TrailerHeight, alongX: false, Toward(3.8f), facing: 1f);
            // Pallet stacks reach the trailer row and split the staging strip; the side pockets connect behind them.
            // West stack: open behind; east stacks: open in the middle (offset openings, no strip eye line).
            b.AddGrounded(-16f, -34.3f, 2.4f, 7.4f, 2.4f, ArenaBoxBuilder.TagCover);
            b.AddGrounded(16f, -38f, 2.4f, 4f, 2.4f, ArenaBoxBuilder.TagCover);
            b.AddGrounded(16f, -32.05f, 2.4f, 2.9f, 2.4f, ArenaBoxBuilder.TagCover);
        }

        /// <summary>Open middle yard between the gate and the centre crossing: signal box, fuel bowser, pallets.</summary>
        private static void BuildMiddleYard(ArenaBoxBuilder b)
        {
            // Signal box right beside the track breaks the eye line running along the train.
            b.AddVehicle(ArenaFeatureKind.Booth, 6f, -3f, 2.6f, 2.6f, 3.5f, alongX: true, Toward(6f));
            // Relay cabinet on the crossing approach stops diagonal eye lines through the centre crossing.
            b.AddVehicle(ArenaFeatureKind.Booth, 4f, -7f, 2f, 2f, 2.6f, alongX: true, Toward(4f));
            b.AddVehicle(ArenaFeatureKind.Truck, -9.5f, -17f, 7f, 2.5f, 3f, alongX: false, Toward(-9.5f), facing: 1f);
            b.AddCover(4f, -15f, 2.4f, 1.2f, 1.2f, Toward(4f));
            b.AddCover(-2.5f, -8f, 2.4f, 1.2f, 1.2f, Toward(-2.5f));
            b.AddCover(7f, -22f, 2f, 1.2f, 1.2f, Toward(7f));
            // Relay cabinet in the west alley, against a container stack: caps the eye line along the wall.
            b.AddVehicle(ArenaFeatureKind.Booth, -29.5f, -18f, 2f, 2.4f, 2.6f, alongX: true, Toward(-29.5f));
        }

        /// <summary>Left flank: staggered container stacks; aisles stay open at both ends and twist.</summary>
        private static void BuildContainerYard(ArenaBoxBuilder b)
        {
            var threat = Toward(-22f);
            float[] rowZ = { -6f, -12f, -18f, -24f };
            for (int r = 0; r < rowZ.Length; r++)
            {
                bool patternA = (r % 2) == 0;
                float[] xs = patternA ? new[] { -25f, -15.5f } : new[] { -20.5f };
                for (int k = 0; k < xs.Length; k++)
                {
                    bool stacked = (r == 1) || (r == 2 && k == 1);
                    AddContainer(b, xs[k], rowZ[r], stacked ? DoubleHeight : ContainerHeight, threat);
                }
            }
        }

        /// <summary>Right flank: loading shed with shelving aisles and a dock lip (covered route), plus the east alley.</summary>
        private static void BuildLoadingShed(ArenaBoxBuilder b)
        {
            const float cx = 20f, cz = -15f;
            var doors = new[]
            {
                new ArenaDoor(ArenaSide.South, -2f, 3f), new ArenaDoor(ArenaSide.North, 2f, 3f),
                new ArenaDoor(ArenaSide.West, -1f, 2.4f), new ArenaDoor(ArenaSide.East, -2f, 2.4f)
            };
            b.Shell(cx, cz, 14f, 14f, 0.5f, 4.5f, doors, roof: true);
            b.AddGrounded(cx - 2f, cz - 2.5f, 6f, 1.2f, 2.2f, ArenaBoxBuilder.TagCover);
            b.AddGrounded(cx + 2.5f, cz + 2.5f, 5f, 1.2f, 2.2f, ArenaBoxBuilder.TagCover);
            // Loading-dock lip on the yard side, flanking the west door.
            b.AddCover(cx - 7.6f, cz + 3f, 1.2f, 1.1f, 3f, Toward(cx - 7.6f));
            // East alley between the shed and the wall: a parked forklift trailer and sandbags.
            b.AddVehicle(ArenaFeatureKind.Trailer, 29.5f, -22f, 5f, 2.4f, 2.6f, alongX: false, Toward(29.5f));
            b.AddSandbagWall(30f, -7f, 3f, alongX: true, Toward(30f));
        }

        private static void AddContainer(ArenaBoxBuilder b, float x, float z, float height, Float3 threat)
            => b.AddCover(x, z, ContainerLength, height, ContainerWidth, threat, true, ArenaBoxBuilder.TagSolid);
    }
}
