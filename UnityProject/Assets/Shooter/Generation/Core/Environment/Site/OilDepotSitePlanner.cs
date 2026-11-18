#nullable enable
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// ADR-109 rebuild of <c>oil-depot</c>, mirrored across z = 0. Each team spawns in the fenced loading bay between the
    /// drum store and the lab and leaves through two gate lanes or through the rack house onto the tank-farm road.
    /// Left: the tank farm and the manifold house on the dividing line. Middle: the pump house (its own mirror image) with
    /// the switch house and foam store around its apron. Right: the two-storey control rooms joined by a pipe bridge over
    /// the pipe alley (the elevated route, with an underpass). Pipe racks run overhead along the road.
    /// </summary>
    public static class OilDepotSitePlanner
    {
        public const string GeneratorId = "environment.oil.site";
        public const string GeneratorVersion = "1.0.0";
        public const ulong StableIdBase = 30000;
        public const float ControlFloorY = 2.5f;
        public const float BridgeX = 27f;
        public const float BridgeWidth = 4f;

        /// <summary>Team-1 tanks (centre x/z, radius, height); each is mirrored to +Z. Read by the oil dressing.</summary>
        public static readonly float[,] Tanks =
        {
            { -25.5f, -13.5f, 4.5f, 7f },
            { -11.5f, -14f, 3.8f, 6f }
        };

        public static readonly ArenaLayout Layout = new ArenaLayout(
            halfExtentX: 32f, halfExtentZ: 40f,
            stagingMinX: -12f, stagingMaxX: 12f, stagingMinZ: -40f, stagingMaxZ: -31f,
            team1Anchor: new Float3(0f, 0f, -34f),
            team1Exits: new[] { new Float3(-10f, 0f, -27f), new Float3(3f, 0f, -22.5f), new Float3(10f, 0f, -27f) },
            lanePoints: new[] { new Float3(-28f, 0f, 0f), new Float3(1f, 0f, 3f), new Float3(BridgeX, ControlFloorY, 0f) },
            symmetry: ArenaSymmetry.MirrorZ);

        private static SiteRect R(float x0, float z0, float x1, float z1) => new SiteRect(x0, z0, x1, z1);
        private static SiteOpening D(ArenaSide s, float o, float w = 2.4f) => SiteOpening.Door(s, o, w);
        private static SiteOpening W(ArenaSide s, float o, float w = 1.6f) => SiteOpening.Window(s, o, w);
        private static SiteOpening UW(ArenaSide s, float o) => SiteOpening.UpperWindow(s, o, ControlFloorY);

        public static CompoundPlan Generate(ulong seed)
        {
            var s = new SiteBuilder("Oil Depot", StableIdBase, Layout);
            s.Boundary(SiteBoundaryKind.Fence, SiteBoundaryKind.Fence, SiteBoundaryKind.Fence, SiteBoundaryKind.Fence, 5f);

            s.Zone("Loading Bay", SiteZoneKind.Staging, R(-12f, -40f, 12f, -31f));
            s.Zone("West Gate", SiteZoneKind.Alley, R(-12f, -31f, -8f, -24f));
            s.Zone("East Gate", SiteZoneKind.Alley, R(8f, -31f, 12f, -24f));
            s.Zone("Tank Farm Road", SiteZoneKind.Street, R(-32f, -24f, 22f, -19f));
            s.Zone("Tank Farm", SiteZoneKind.Yard, R(-32f, -19f, -8f, -6f));
            s.Zone("Pump Apron", SiteZoneKind.Yard, R(-8f, -19f, 22f, -14f));
            s.Zone("Lab Yard", SiteZoneKind.Yard, R(12f, -31f, 32f, -26f));
            s.Zone("Pipe Alley", SiteZoneKind.Alley, R(22f, -12f, 32f, 12f), mirror: false);

            // Spawn compound: drum store, rack house, lab.
            var drums = s.Building("Drum Store", SiteBuildingKind.Warehouse, R(-32f, -40f, -12f, -31f), 6f,
                new[] { D(ArenaSide.East, 1.5f, 3f), D(ArenaSide.North, 4f, 3f), W(ArenaSide.North, -4f) });
            s.Partition(drums, alongX: false, at: -22f, from: -39.6f, to: -31.4f, doors: new[] { new ArenaGap(-37f, 2.2f) });
            var rack = s.Building("Rack House", SiteBuildingKind.LoadingRack, R(-8f, -31f, 8f, -24f), 5.5f,
                new[] { D(ArenaSide.South, -4f, 3f), D(ArenaSide.North, 3f, 3f) });
            var lab = s.Building("Lab", SiteBuildingKind.Office, R(12f, -40f, 32f, -31f), 5f,
                new[] { D(ArenaSide.West, -2f), D(ArenaSide.North, -4f), W(ArenaSide.North, 4f) });
            s.Partition(lab, alongX: false, at: 22f, from: -39.6f, to: -31.4f, doors: new[] { new ArenaGap(-33f, 2.2f) });

            // Control room (2F); its gallery opens onto the pipe bridge through an upper door.
            var control = s.Building("Control Room", SiteBuildingKind.ControlRoom, R(22f, -26f, 32f, -12f), 7f,
                new[]
                {
                    D(ArenaSide.South, 2.5f), D(ArenaSide.West, -2f), D(ArenaSide.North, -3.5f),
                    new SiteOpening(ArenaSide.North, BridgeX - 27f, BridgeWidth - 1f, ControlFloorY, ControlFloorY + 2.4f),
                    UW(ArenaSide.West, 4f), UW(ArenaSide.West, -4f), UW(ArenaSide.East, 4f)
                },
                upperFloorY: ControlFloorY);
            s.Floor(control, R(22.4f, -15.5f, 31.6f, -12.4f));
            s.Stair(control, 23.4f, -25.5f, 1.8f, ascendingPositiveZ: true);
            s.Stair(control, 30.6f, -25.5f, 1.8f, ascendingPositiveZ: true);
            s.Rail(control, 27f, -15.6f, 5f, 0.2f);

            // Pipe bridge between the two control rooms, over the pipe alley (its own mirror image).
            var bridge = s.Platform("Pipe Bridge", R(BridgeX - BridgeWidth * 0.5f, -12.4f, BridgeX + BridgeWidth * 0.5f, 12.4f), ControlFloorY, mirror: false);
            s.Floor(bridge, R(BridgeX - BridgeWidth * 0.5f, -12.4f, BridgeX + BridgeWidth * 0.5f, 12.4f));
            s.Rail(bridge, BridgeX - BridgeWidth * 0.5f + 0.1f, 0f, 0.2f, 24f);
            s.Rail(bridge, BridgeX + BridgeWidth * 0.5f - 0.1f, 0f, 0.2f, 24f);
            s.Post(bridge, BridgeX - BridgeWidth * 0.5f + 0.2f, -5f, mirror: true);
            s.Post(bridge, BridgeX + BridgeWidth * 0.5f - 0.2f, -5f, mirror: true);

            // Foam store and switch house close the pump apron; the foam store's east door opens onto the pipe alley.
            var foam = s.Building("Foam Store", SiteBuildingKind.Shed, R(8f, -14f, 22f, -6f), 4.5f,
                new[] { D(ArenaSide.West, 0f), D(ArenaSide.South, -3f), D(ArenaSide.East, 1f), D(ArenaSide.North, 3f) });
            s.Partition(foam, alongX: false, at: 15f, from: -13.6f, to: -6.4f, doors: new[] { new ArenaGap(-8f, 2f) });
            var sw = s.Building("Switch House", SiteBuildingKind.Shed, R(0f, -17f, 7f, -11f), 4.5f,
                new[] { D(ArenaSide.West, 0f), D(ArenaSide.North, 2f) });
            s.AgainstFacade(sw, ArenaSide.East, 0f, 2.4f, 1.2f, 2.4f);              // transformer
            s.AgainstFacade(sw, ArenaSide.South, 0f, 3f, 1.6f, 2.4f);               // battery cabinets

            // Pump house on the dividing line (its own mirror image).
            var pump = s.Building("Pump House", SiteBuildingKind.PumpHouse, R(-8f, -6f, 8f, 6f), 4.5f,
                new[]
                {
                    D(ArenaSide.South, -3f), D(ArenaSide.North, -3f),
                    D(ArenaSide.East, -3f), D(ArenaSide.East, 3f), D(ArenaSide.West, -3.5f), D(ArenaSide.West, 3.5f)
                }, mirror: false);
            s.Partition(pump, alongX: true, at: 0f, from: -7.6f, to: 7.6f, doors: new[] { new ArenaGap(4.5f, 2.2f) });
            s.AgainstFacade(pump, ArenaSide.South, 3.5f, 3f, 1.2f, 1.1f, mirror: true);   // valve manifold
            s.AgainstFacade(pump, ArenaSide.West, -5.3f, 1f, 1.2f, 2.4f, mirror: true);   // strainer stack

            // Manifold and meter houses on the dividing line.
            s.Building("Manifold House", SiteBuildingKind.PumpHouse, R(-24f, -4f, -12f, 4f), 4.5f,
                new[] { D(ArenaSide.East, 0f), D(ArenaSide.West, 0f) }, mirror: false);
            s.Building("Meter House", SiteBuildingKind.Shed, R(10f, -2.5f, 16f, 2.5f), 4f,
                new[] { D(ArenaSide.East, 0f, 1.8f) }, mirror: false);

            // Tank farm.
            for (int i = 0; i < Tanks.GetLength(0); i++)
            {
                float cx = Tanks[i, 0], cz = Tanks[i, 1], r = Tanks[i, 2], h = Tanks[i, 3];
                s.Landmark("Tank " + (i + 1), R(cx - r, cz - r, cx + r, cz + r), h, mirror: true, bb =>
                {
                    bb.AddGrounded(cx, cz, r * 1.7f, r * 1.7f, h, ArenaBoxBuilder.TagSolid);
                    bb.AddGrounded(cx + r * 0.85f, cz, 0.7f, r * 1.1f, h, ArenaBoxBuilder.TagSolid);
                    bb.AddGrounded(cx - r * 0.85f, cz, 0.7f, r * 1.1f, h, ArenaBoxBuilder.TagSolid);
                    bb.AddGrounded(cx, cz + r * 0.85f, r * 1.1f, 0.7f, h, ArenaBoxBuilder.TagSolid);
                    bb.AddGrounded(cx, cz - r * 0.85f, r * 1.1f, 0.7f, h, ArenaBoxBuilder.TagSolid);
                    bb.RecordFeature(ArenaFeatureKind.Tank, cx, cz, r * 2f, h, r * 2f, 1f);
                });
            }
            s.InZone("Tank Farm", -18.5f, -7.5f, 2.5f, 1.2f, 1.1f);                   // valve pit
            s.AgainstBoundary(-31f, -9f, 2f, 2.4f, 2.6f);                             // fire-water pump skid on the fence

            // Road and yards.
            s.Overhead("Tank Farm Road", -5f, -23.2f, 34f, 1.2f, 3.6f, 4.6f);         // pipe rack over the south curb
            s.Park("Tank Farm Road", ArenaFeatureKind.Tanker, -24f, -22.6f, 9f, 2.6f, 3.2f, facing: -1f);
            s.Park("Tank Farm Road", ArenaFeatureKind.Tanker, -4f, -20.4f, 8f, 2.6f, 3.2f, facing: 1f);
            s.Park("Tank Farm Road", ArenaFeatureKind.Truck, 14f, -22.6f, 6f, 2.4f, 2.8f, facing: -1f);
            s.Park("Lab Yard", ArenaFeatureKind.Truck, 18f, -28.5f, 6f, 2.4f, 2.8f, facing: 1f);
            s.Park("Pipe Alley", ArenaFeatureKind.Tanker, 23.6f, 0f, 8f, 2.6f, 3.2f, facing: 1f);
            s.AgainstFacade(drums, ArenaSide.North, -6f, 3f, 1.4f, 2.4f);              // drum stack
            s.AgainstFacade(rack, ArenaSide.North, -5f, 2.6f, 1.2f, 1.1f);            // meter cabinet
            s.AgainstFacade(foam, ArenaSide.South, 4f, 1.4f, 1.2f, 1.1f);             // foam drums
            s.AgainstFacade(control, ArenaSide.West, 5f, 1.4f, 1.6f, 1.2f);           // cable drums
            s.AgainstFacade(lab, ArenaSide.North, 5f, 2.5f, 1.2f, 1.1f);              // sample crates

            return s.Build(GeneratorId, GeneratorVersion, seed);
        }
    }
}
