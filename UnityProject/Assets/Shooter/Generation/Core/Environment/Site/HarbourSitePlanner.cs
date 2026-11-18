#nullable enable
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// ADR-109 "Harbour" for <c>depot-yard</c>: a working port, mirrored across z = 0. Each team spawns inside its transit
    /// shed and leaves through roller doors onto the dock road. Left: the quay along the water with cargo and a crane.
    /// Middle: the container terminal and Fish Lane to the pier square under the portal crane. Right: Customs Lane and the
    /// two-storey harbour office whose gallery overlooks the customs house. A row of shared buildings (fish market,
    /// customs house) stands on the dividing line, so the halves meet through doors and around the crane, never down one street.
    /// </summary>
    public static class HarbourSitePlanner
    {
        public const string GeneratorId = "environment.harbour.site";
        public const string GeneratorVersion = "1.0.0";
        public const ulong StableIdBase = 20000;
        public const float OfficeFloorY = 2.5f;

        public static readonly ArenaLayout Layout = new ArenaLayout(
            halfExtentX: 32f, halfExtentZ: 40f,
            stagingMinX: -12f, stagingMaxX: 12f, stagingMinZ: -39f, stagingMaxZ: -31f,
            team1Anchor: new Float3(0f, 0f, -34f),
            team1Exits: new[] { new Float3(-21f, 0f, -30f), new Float3(-9f, 0f, -26.5f), new Float3(9f, 0f, -26.5f) },
            lanePoints: new[] { new Float3(-30.5f, 0f, 0f), new Float3(5f, 0f, 0f), new Float3(27f, 0f, 4f) },
            symmetry: ArenaSymmetry.MirrorZ);

        private static SiteRect R(float x0, float z0, float x1, float z1) => new SiteRect(x0, z0, x1, z1);
        private static SiteOpening D(ArenaSide s, float o, float w = 2.4f) => SiteOpening.Door(s, o, w);
        private static SiteOpening W(ArenaSide s, float o, float w = 1.6f) => SiteOpening.Window(s, o, w);
        private static SiteOpening UW(ArenaSide s, float o) => SiteOpening.UpperWindow(s, o, OfficeFloorY);

        public static CompoundPlan Generate(ulong seed)
        {
            var s = new SiteBuilder("Harbour", StableIdBase, Layout);
            s.Boundary(SiteBoundaryKind.BuildingBacks, SiteBoundaryKind.BuildingBacks, SiteBoundaryKind.Fence, SiteBoundaryKind.Water, 6f);

            s.Zone("Transit Shed", SiteZoneKind.Staging, R(-12f, -39f, 12f, -31f));
            s.Zone("Quay", SiteZoneKind.Quay, R(-32f, -32f, -22f, -7f));
            s.Zone("Dock Road", SiteZoneKind.Street, R(-22f, -29f, 32f, -23f));
            s.Zone("Container Terminal", SiteZoneKind.Yard, R(-22f, -23f, -6f, -8f));
            s.Zone("Fish Lane", SiteZoneKind.Street, R(-6f, -23f, -1f, -8f));
            s.Zone("Customs Lane", SiteZoneKind.Alley, R(16f, -23f, 20f, -8f));
            s.Zone("Pier Square", SiteZoneKind.Quay, R(-8f, -7f, 12f, 7f), mirror: false);
            s.Zone("Bonded Gate", SiteZoneKind.Street, R(24f, -7f, 32f, 7f), mirror: false);

            // Spawn row.
            var shed = s.Building("Transit Shed", SiteBuildingKind.Warehouse, R(-14f, -40f, 14f, -29f), 7f,
                new[] { D(ArenaSide.North, -9f, 4f), D(ArenaSide.North, 9f, 4f), D(ArenaSide.West, 1.5f, 3f), D(ArenaSide.East, -2.5f, 3f) });
            var loft = s.Building("Net Loft", SiteBuildingKind.Shed, R(-24f, -40f, -14f, -32f), 5f,
                new[] { D(ArenaSide.North, 3f), D(ArenaSide.East, 3f, 2.6f), W(ArenaSide.West, 0f) });
            var ice = s.Building("Ice Plant", SiteBuildingKind.Warehouse, R(14f, -40f, 32f, -31f), 6f,
                new[] { D(ArenaSide.North, -2f, 3f), D(ArenaSide.West, -1.5f, 3f) });
            s.Partition(ice, alongX: false, at: 22f, from: -39.6f, to: -31.4f, doors: new[] { new ArenaGap(-33f, 2.2f) });

            // Second row: chandlery, harbour office (2F).
            var chandlery = s.Building("Chandlery", SiteBuildingKind.Shop, R(-1f, -23f, 16f, -8f), 6f,
                new[] { D(ArenaSide.West, 3f), D(ArenaSide.South, 0f), D(ArenaSide.East, -3f), W(ArenaSide.West, -3f), W(ArenaSide.South, 5f) });
            var office = s.Building("Harbour Office", SiteBuildingKind.Office, R(20f, -23f, 32f, -8f), 7f,
                new[]
                {
                    D(ArenaSide.South, -2f), D(ArenaSide.West, 2f), D(ArenaSide.North, 2f),
                    UW(ArenaSide.North, -3f), UW(ArenaSide.North, 4f), UW(ArenaSide.West, -2f), UW(ArenaSide.West, 4.5f)
                },
                upperFloorY: OfficeFloorY);
            s.Floor(office, R(20.4f, -12.5f, 31.6f, -8.4f));
            s.Stair(office, 21.4f, -22.5f, 1.8f, ascendingPositiveZ: true);
            s.Stair(office, 30.6f, -22.5f, 1.8f, ascendingPositiveZ: true);
            s.Rail(office, 26f, -12.6f, 7f, 0.2f);

            // Shared row on the dividing line (each is its own mirror image).
            var market = s.Building("Fish Market", SiteBuildingKind.Shed, R(-24f, -7f, -8f, 7f), 6f,
                new[]
                {
                    D(ArenaSide.South, 4f), D(ArenaSide.North, 4f),
                    D(ArenaSide.West, -3.5f), D(ArenaSide.West, 3.5f), D(ArenaSide.East, -3.5f), D(ArenaSide.East, 3.5f)
                }, mirror: false);
            s.Partition(market, alongX: true, at: 0f, from: -23.6f, to: -8.4f, doors: new[] { new ArenaGap(-11f, 2.2f) });
            var customs = s.Building("Customs House", SiteBuildingKind.Customs, R(12f, -7f, 24f, 7f), 6f,
                new[]
                {
                    D(ArenaSide.South, 0f), D(ArenaSide.North, 0f),
                    D(ArenaSide.West, -3.5f), D(ArenaSide.West, 3.5f), D(ArenaSide.East, -3.5f), D(ArenaSide.East, 3.5f)
                }, mirror: false);
            s.Partition(customs, alongX: true, at: 0f, from: 12.4f, to: 23.6f, doors: new[] { new ArenaGap(14.5f, 2.2f) });

            // Landmarks.
            // Portal crane machinery stands against the fish market; the open half of the pier is offset from Fish Lane.
            s.Landmark("Portal Crane", R(-8f, -2f, -1f, 2f), 4f, mirror: false,
                bb => bb.AddGrounded(-4.5f, 0f, 7f, 4f, 4f, ArenaBoxBuilder.TagSolid, mirror: false));
            // Pilot house on the quay: the quay crossing is a 3 m strip along the water.
            s.Building("Pilot House", SiteBuildingKind.Office, R(-29f, -3f, -24f, 3f), 4.5f,
                new[] { D(ArenaSide.West, 0f, 2f) }, mirror: false);
            s.Landmark("Customs Booth", 28f, 0f, 4f, 3f, 3f, mirror: false, feature: ArenaFeatureKind.Booth);
            s.Landmark("Quay Crane", R(-31f, -22f, -25f, -18f), 4f, mirror: true,
                bb => bb.AddGrounded(-28f, -20f, 6f, 4f, 4f, ArenaBoxBuilder.TagSolid, mirror: true));

            // Quay cargo and bollards.
            s.InZone("Quay", -29.5f, -13f, 4f, 3f, 2.6f, ArenaFeatureKind.Cargo);
            s.InZone("Quay", -25f, -27f, 3f, 3f, 2.6f, ArenaFeatureKind.Cargo);
            s.InZone("Quay", -31f, -8.5f, 1.4f, 2f, 1.1f);
            s.AgainstFacade(market, ArenaSide.West, -5.5f, 1.2f, 2.5f, 1.1f, mirror: true);

            // Container terminal: rows on the yard grid, aisles >= 2.6 m.
            // 6.0 x 2.5 m envelopes (one or two high) are drawn as corrugated containers by MapSolidsMesh.
            s.InZone("Container Terminal", -18f, -20.5f, 6f, 2.5f, 2.6f);
            s.InZone("Container Terminal", -9.2f, -20.5f, 6f, 2.5f, 5.2f);
            s.InZone("Container Terminal", -15f, -15.5f, 6f, 2.5f, 5.2f);
            s.InZone("Container Terminal", -18.5f, -10.5f, 6f, 2.5f, 2.6f);
            s.InZone("Container Terminal", -9.2f, -10.5f, 6f, 2.5f, 2.6f);

            // Streets.
            s.Park("Dock Road", ArenaFeatureKind.Truck, 0f, -26.9f, 7f, 2.5f, 3f, facing: 1f);
            s.Park("Dock Road", ArenaFeatureKind.Trailer, 20f, -24.6f, 9f, 2.5f, 3f, facing: -1f);
            s.Park("Dock Road", ArenaFeatureKind.Truck, -17f, -24.6f, 6f, 2.4f, 2.8f, facing: -1f);
            s.Park("Fish Lane", ArenaFeatureKind.Truck, -4.75f, -15f, 6f, 2.3f, 2.8f, facing: 1f);
            s.AgainstFacade(chandlery, ArenaSide.West, -6f, 1.4f, 1.2f, 1.1f);
            s.AgainstFacade(office, ArenaSide.West, -5f, 1.4f, 2f, 1.2f);
            s.AgainstFacade(shed, ArenaSide.North, 0f, 10f, 1.2f, 2.4f, inside: true);
            s.AgainstFacade(ice, ArenaSide.North, 5f, 3f, 1.4f, 1.2f);
            s.AgainstFacade(customs, ArenaSide.West, 0f, 1.4f, 2f, 1.1f);

            return s.Build(GeneratorId, GeneratorVersion, seed);
        }
    }
}
