#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// ADR-074 compact TDM arena for <c>training-compound</c> ("garrison square", docs/features/tdm-map-layouts.md Map A).
    /// Point-symmetric (<see cref="ArenaSymmetry.Rotate180"/>): team 1 plays from -Z and team 2 is the 180° rotation.
    /// The centre is an open parade square with a low flag plinth, not a building. Four corner blocks form a pinwheel
    /// around it: for each team the left route runs through a barracks interior, the right route down a truck-lined
    /// street, and the middle route through the motor-pool garage and the guard-hut arch. Every blocking box is a
    /// readable object (building wall, vehicle, sandbags, tarp fence); there are no free-standing separator walls.
    /// </summary>
    public static class CompoundArenaPlanner
    {
        public const string GeneratorId = "environment.compound.arena";
        public const string GeneratorVersion = "3.0.0";
        public const ulong StableIdBase = 10000;

        private const float Thin = 0.4f;
        private const float BuildingWall = 3.5f;
        private const float TruckHeight = 3.0f, TruckWidth = 2.5f, TruckLength = 7f;

        public static readonly ArenaLayout Layout = new ArenaLayout(
            halfExtentX: 32f, halfExtentZ: 40f,
            stagingMinX: -32f, stagingMaxX: 32f, stagingMinZ: -40f, stagingMaxZ: -30f,
            team1Anchor: new Float3(0f, 0f, -34f),
            team1Exits: new[] { new Float3(-20f, 0f, -29f), new Float3(1.5f, 0f, -22.5f), new Float3(20f, 0f, -29f) },
            lanePoints: new[] { new Float3(-26f, 0f, 0f), new Float3(0f, 0f, 0f), new Float3(26f, 0f, 0f) },
            symmetry: ArenaSymmetry.Rotate180);

        public static CompoundPlan Generate(ulong seed)
        {
            var b = new ArenaBoxBuilder(StableIdBase, ArenaSymmetry.Rotate180);
            b.Perimeter(Layout, height: 5f, thickness: 1f);
            BuildMotorPool(b);
            BuildParadeSquare(b);
            BuildArch(b);
            BuildBarracks(b);
            BuildOffice(b);
            BuildTruckStreet(b);
            ArenaVisualDetail.AppendCompound(b, Layout);
            return ArenaGeometry.CreatePlan(GeneratorId, GeneratorVersion, seed, Layout, b);
        }

        /// <summary>Threat for cover on the team-1 half: toward the parade square.</summary>
        private static Float3 Toward(float x) => new Float3(x, 0f, 0f);

        /// <summary>Spawn yard: drive-through garage in the middle, parked trucks either side, jeep bays splitting the strip.</summary>
        private static void BuildMotorPool(ArenaBoxBuilder b)
        {
            // Garage 14 x 7 with offset drive-through doors (no straight north-south eye line through both).
            var doors = new[] { new ArenaDoor(ArenaSide.South, -1.5f, 3f), new ArenaDoor(ArenaSide.North, 1.5f, 3f) };
            b.Shell(0f, -27f, 14f, 7f, Thin, 4f, doors, roof: true);
            b.AddCover(-5f, -25.5f, 2f, 1.1f, 1.2f, Toward(-5f));
            // Two jeeps parked nose-in either side of the garage's south door form a short chute: from inside the
            // garage only the chute shows, not the spawn pocket.
            b.AddVehicle(ArenaFeatureKind.Truck, -4.3f, -32f, 3f, 2.4f, 2.4f, alongX: false, Toward(-4.3f), facing: 1f);
            b.AddVehicle(ArenaFeatureKind.Truck, 1.3f, -32f, 3f, 2.4f, 2.4f, alongX: false, Toward(1.3f), facing: 1f);
            // Tall parts rack between the doors stops diagonal eye lines through both openings.
            b.AddCover(0f, -27f, 1.4f, 2.4f, 1.4f, Toward(0f));

            // Parked trucks in front of the staging strip, leaving a 7 m exit on each side.
            b.AddVehicle(ArenaFeatureKind.Truck, -28f, -29f, TruckLength, TruckWidth, TruckHeight, alongX: true, Toward(-28f), facing: 1f);
            b.AddVehicle(ArenaFeatureKind.Truck, -13f, -29f, 8f, TruckWidth, TruckHeight, alongX: true, Toward(-13f), facing: -1f);
            b.AddVehicle(ArenaFeatureKind.Truck, 13f, -29f, 8f, TruckWidth, TruckHeight, alongX: true, Toward(13f), facing: 1f);
            b.AddVehicle(ArenaFeatureKind.Truck, 28f, -29f, TruckLength, TruckWidth, TruckHeight, alongX: true, Toward(28f), facing: -1f);

            // Parked trailers reach the truck row and split the staging strip, so a player in a side exit cannot
            // see into the centre pocket. The west pocket connects behind its trailer, the east pocket through a gap
            // between two trailers; the offset openings leave no eye line along the strip.
            b.AddVehicle(ArenaFeatureKind.Trailer, -17f, -34.3f, 7.4f, 2.6f, 2.6f, alongX: false, Toward(-17f));
            b.AddVehicle(ArenaFeatureKind.Trailer, 17f, -38f, 4f, 2.6f, 2.6f, alongX: false, Toward(17f));
            b.AddVehicle(ArenaFeatureKind.Trailer, 17f, -32.05f, 2.9f, 2.6f, 2.6f, alongX: false, Toward(17f));
        }

        /// <summary>Open parade square: low flag plinth on the origin, planters, memorial benches.</summary>
        private static void BuildParadeSquare(ArenaBoxBuilder b)
        {
            b.AddCover(0f, 0f, 2.6f, 1.0f, 2.6f, new Float3(0f, 0f, -10f));
            b.RecordFeature(ArenaFeatureKind.Flagpole, 0f, 0f, 2.6f, 1.0f, 2.6f, 1f, mirror: false);
            b.AddCover(-7f, -5f, 4f, 1.1f, 1.2f, Toward(-7f));
            b.AddCover(7f, -5f, 4f, 1.1f, 1.2f, Toward(7f));
            b.AddSandbagWall(-10f, 4f, 3.5f, alongX: false, Toward(-10f));
            b.AddSandbagWall(10f, 3f, 3.5f, alongX: false, Toward(10f));
            // Tall square-edge objects break the east-west eye lines across the open square.
            b.AddVehicle(ArenaFeatureKind.Trailer, -16f, -2f, 5f, 2.4f, 2.6f, alongX: false, Toward(-16f));
            b.AddVehicle(ArenaFeatureKind.Booth, 13f, -4.5f, 4f, 2.2f, 2.6f, alongX: false, Toward(13f));
        }

        /// <summary>Guard huts framing the middle approach (a visual arch spans them).</summary>
        private static void BuildArch(ArenaBoxBuilder b)
        {
            // Huts are offset so one stands on the garage-door column; its 180° twin closes the other half of
            // the middle column, so no straight eye line runs garage to garage.
            b.AddGrounded(-5f, -15f, 3f, 3f, 3f);
            b.AddGrounded(1.5f, -15f, 3f, 3f, 3f);
            b.Add(new Float3(-1.75f, 4.1f, -15f), new Float3(9.5f, 0.6f, 3f), ArenaBoxBuilder.TagSolid, mirror: true, gameplay: false);
            // Notice-board sheds cap the long north-south columns either side of the arch.
            b.AddVehicle(ArenaFeatureKind.Booth, -11.5f, -19.5f, 4f, 2f, 2.6f, alongX: true, Toward(-11.5f));
            b.AddVehicle(ArenaFeatureKind.Booth, 9.5f, -21f, 3f, 2f, 2.6f, alongX: true, Toward(9.5f));
        }

        /// <summary>Left route: two-room barracks with doors to the spawn, the square and the west alley.</summary>
        private static void BuildBarracks(ArenaBoxBuilder b)
        {
            const float cx = -21f, cz = -15f;
            var doors = new[]
            {
                new ArenaDoor(ArenaSide.South, 0f, 2.4f),
                new ArenaDoor(ArenaSide.North, 3f, 2.4f),
                new ArenaDoor(ArenaSide.East, 4f, 2.4f),
                new ArenaDoor(ArenaSide.West, -4f, 2.4f)
            };
            b.Shell(cx, cz, 12f, 14f, Thin, BuildingWall, doors, roof: true);
            b.WallAlongX(cz, cx - 6f + Thin, cx + 6f - Thin, 0.3f, 3.0f, new[] { new ArenaGap(cx - 3f, 2.4f) });
            b.AddCover(cx + 1f, cz - 3.5f, 3f, 1.0f, 1.0f, Toward(cx + 1f));
            b.AddCover(cx - 1f, cz + 3.5f, 3f, 1.0f, 1.0f, Toward(cx - 1f));
            // West alley: sandbags and a parked trailer cap the alley eye line.
            b.AddSandbagWall(-29.5f, -22f, 3f, alongX: true, Toward(-29.5f));
            b.AddVehicle(ArenaFeatureKind.Trailer, -29.5f, -9f, 5f, 2.4f, 2.6f, alongX: false, Toward(-29.5f));
            b.AddSandbagWall(-17f, -4f, 4f, alongX: true, Toward(-17f));
        }

        /// <summary>Square-side HQ office between the arch and the truck street.</summary>
        private static void BuildOffice(ArenaBoxBuilder b)
        {
            const float cx = 15.5f, cz = -14f;
            var doors = new[]
            {
                new ArenaDoor(ArenaSide.West, 0f, 2.4f),
                new ArenaDoor(ArenaSide.East, -2.5f, 2.4f),
                new ArenaDoor(ArenaSide.North, -1f, 2.4f)
            };
            b.Shell(cx, cz, 9f, 10f, Thin, BuildingWall, doors, roof: true);
            b.AddCover(cx, cz, 2.4f, 1.0f, 1.2f, Toward(cx));
        }

        /// <summary>Right route: an outdoor street of staggered parked trucks and sandbag walls.</summary>
        private static void BuildTruckStreet(ArenaBoxBuilder b)
        {
            b.AddVehicle(ArenaFeatureKind.Truck, 24f, -20f, TruckLength, TruckWidth, TruckHeight, alongX: false, Toward(24f), facing: 1f);
            b.AddVehicle(ArenaFeatureKind.Truck, 29.5f, -9f, TruckLength, TruckWidth, TruckHeight, alongX: false, Toward(29.5f), facing: -1f);
            b.AddSandbagWall(28.5f, -23f, 3f, alongX: true, Toward(28.5f));
            b.AddSandbagWall(23f, -6f, 3.5f, alongX: true, Toward(23f));
        }
    }
}
