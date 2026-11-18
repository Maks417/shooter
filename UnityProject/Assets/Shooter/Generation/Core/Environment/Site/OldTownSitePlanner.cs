#nullable enable
using Shooter.Contracts;

namespace Shooter.Generation.Environment
{
    /// <summary>
    /// ADR-109 "Old Town" for <c>training-compound</c>: a small town centre. Each team spawns in an inn yard; the town
    /// is point-symmetric about the war memorial in the market square, so each half's left route (weavers' alley and
    /// the chapel) meets the other half's right route (the two-storey town hall with a gallery over the square).
    /// </summary>
    public static class OldTownSitePlanner
    {
        public const string GeneratorId = "environment.town.site";
        public const string GeneratorVersion = "1.0.0";
        public const ulong StableIdBase = 10000;
        public const float TownHallFloorY = 2.5f;

        public static readonly ArenaLayout Layout = new ArenaLayout(
            halfExtentX: 32f, halfExtentZ: 40f,
            stagingMinX: -10f, stagingMaxX: 10f, stagingMinZ: -40f, stagingMaxZ: -32f,
            team1Anchor: new Float3(0f, 0f, -34f),
            team1Exits: new[] { new Float3(-7f, 0f, -29f), new Float3(7f, 0f, -22f), new Float3(17f, 0f, -29f) },
            lanePoints: new[] { new Float3(-29f, 0f, -16f), new Float3(-6f, 0f, -12f), new Float3(28f, 0f, -12f) },
            symmetry: ArenaSymmetry.Rotate180);

        private static SiteRect R(float x0, float z0, float x1, float z1) => new SiteRect(x0, z0, x1, z1);
        private static SiteOpening D(ArenaSide s, float o, float w = 2.4f) => SiteOpening.Door(s, o, w);
        private static SiteOpening W(ArenaSide s, float o, float w = 1.6f) => SiteOpening.Window(s, o, w);
        private static SiteOpening UW(ArenaSide s, float o) => SiteOpening.UpperWindow(s, o, TownHallFloorY);

        public static CompoundPlan Generate(ulong seed)
        {
            var s = new SiteBuilder("Old Town", StableIdBase, Layout);
            s.Boundary(SiteBoundaryKind.BuildingBacks, SiteBoundaryKind.BuildingBacks, SiteBoundaryKind.BuildingBacks, SiteBoundaryKind.BuildingBacks, 7f);

            s.Zone("Inn Yard", SiteZoneKind.Staging, R(-10f, -40f, 10f, -32f));
            s.Zone("Stable Lane", SiteZoneKind.Alley, R(-32f, -32f, -4f, -24f));
            s.Zone("Weavers Alley", SiteZoneKind.Alley, R(-32f, -24f, -26f, -12f));
            s.Zone("Church Walk", SiteZoneKind.Alley, R(-32f, -12f, -14f, -8f));
            s.Zone("High Street", SiteZoneKind.Street, R(2f, -24f, 10f, -8f));
            s.Zone("Mill Yard", SiteZoneKind.Yard, R(12f, -32f, 32f, -20f));
            s.Zone("Mill Lane", SiteZoneKind.Alley, R(10f, -20f, 14f, -12f));
            s.Zone("Market Square", SiteZoneKind.Plaza, R(-14f, -8f, 14f, 8f), mirror: false);

            // Spawn row: cottage, stable, inn yard, smithy, granary.
            var cottage = s.Building("Cottage", SiteBuildingKind.House, R(-32f, -40f, -26f, -32f), 5f,
                new[] { D(ArenaSide.North, 0f, 2f) });
            var stable = s.Building("Stable", SiteBuildingKind.Shed, R(-26f, -40f, -10f, -32f), 5f,
                new[] { D(ArenaSide.East, 0f, 2.6f), D(ArenaSide.North, -4f, 2.6f), W(ArenaSide.North, 3f) });
            var inn = s.Building("Inn", SiteBuildingKind.Inn, R(-4f, -32f, 12f, -24f), 6.5f,
                new[] { D(ArenaSide.South, -1f), D(ArenaSide.North, 3f), W(ArenaSide.South, 4f), W(ArenaSide.West, 0f) });
            var smithy = s.Building("Smithy", SiteBuildingKind.Shop, R(10f, -40f, 20f, -32f), 5f,
                new[] { D(ArenaSide.West, 0f, 2.6f), D(ArenaSide.North, 4f, 2.6f) });
            var granary = s.Building("Granary", SiteBuildingKind.Shed, R(20f, -40f, 32f, -32f), 6f,
                new[] { D(ArenaSide.North, 0f, 3f) });

            // Middle row: bakery, tailor (both cut-throughs), the town hall.
            var bakery = s.Building("Bakery", SiteBuildingKind.Shop, R(-26f, -24f, -14f, -12f), 6f,
                new[] { D(ArenaSide.South, 2f), D(ArenaSide.North, -2f), D(ArenaSide.West, 2f), W(ArenaSide.West, -3f) });
            var tailor = s.Building("Tailor", SiteBuildingKind.Shop, R(-14f, -24f, 2f, -8f), 6f,
                new[] { D(ArenaSide.South, -4f), D(ArenaSide.North, 2f), D(ArenaSide.East, 0f), W(ArenaSide.East, 5f) });
            s.Partition(tailor, alongX: true, at: -16f, from: -13.6f, to: 1.6f, doors: new[] { new ArenaGap(-1f, 2.2f) });

            var hall = s.Building("Town Hall", SiteBuildingKind.TownHall, R(14f, -20f, 32f, 0f), 6.5f,
                new[]
                {
                    D(ArenaSide.South, 0f, 3f), D(ArenaSide.West, -4f), D(ArenaSide.West, 7f),
                    UW(ArenaSide.West, 3f), UW(ArenaSide.West, 8f), UW(ArenaSide.South, -5f), UW(ArenaSide.South, 5f)
                },
                upperFloorY: TownHallFloorY);
            // Gallery along the north wall over the hall, reached by a stair at each end.
            s.Floor(hall, R(14.4f, -8f, 31.6f, -0.4f));
            s.Stair(hall, 16f, -18f, 2f, ascendingPositiveZ: true);
            s.Stair(hall, 30f, -18f, 2f, ascendingPositiveZ: true);
            s.Rail(hall, 23f, -8.1f, 10f, 0.2f);

            // Chapel on the west side of the square.
            var chapel = s.Building("Chapel", SiteBuildingKind.Church, R(-32f, -8f, -14f, 0f), 8f,
                new[] { D(ArenaSide.South, 5f), D(ArenaSide.East, -2f, 2.8f) });

            // Old market hall in the middle of the square (its own image under the 180-degree turn).
            s.Building("Market Hall", SiteBuildingKind.Shop, R(-6f, -2.5f, 6f, 2.5f), 5f,
                new[] { D(ArenaSide.East, 1f), D(ArenaSide.West, -1f) }, mirror: false);
            // Corner shop closes the top of Mill Lane; High Street meets the square through a 4 m mouth.
            s.Building("Apothecary", SiteBuildingKind.Shop, R(10f, -12f, 14f, -8f), 5.5f,
                new[] { D(ArenaSide.North, 0f, 2f) });
            s.Building("Newsagent", SiteBuildingKind.Shop, R(2f, -12f, 6f, -8f), 4.5f,
                new[] { D(ArenaSide.East, 0f, 1.8f) });

            // Props that belong somewhere.
            s.AgainstFacade(stable, ArenaSide.East, -2.5f, 3f, 1.4f, 1.2f, feature: ArenaFeatureKind.Cargo);      // hay cart in the yard
            s.AgainstFacade(smithy, ArenaSide.West, 2.5f, 2.5f, 1.2f, 1.1f);                                    // water trough
            s.AgainstFacade(inn, ArenaSide.South, 6f, 2.4f, 1.2f, 1.2f);                                        // barrel rack
            s.AgainstFacade(bakery, ArenaSide.South, -3f, 3f, 1.2f, 1.2f);                                      // flour sacks
            s.AgainstFacade(bakery, ArenaSide.South, 0f, 3f, 3f, 2.6f);                                         // woodshed lean-to
            s.AgainstFacade(cottage, ArenaSide.North, 2f, 2f, 1.2f, 1.1f);                                      // woodpile
            s.AgainstFacade(bakery, ArenaSide.West, -0.5f, 1.2f, 2.4f, 1.2f);                                   // bins in the alley
            s.AgainstFacade(chapel, ArenaSide.South, -5f, 4f, 1f, 1.1f);                                        // churchyard wall planter
            s.Park("High Street", ArenaFeatureKind.Truck, 4.4f, -15f, 6f, 2.4f, 2.8f, facing: 1f);              // delivery truck at the curb
            s.AgainstFacade(tailor, ArenaSide.North, -3f, 3f, 2f, 2.3f, feature: ArenaFeatureKind.Booth);       // market stall
            s.AgainstFacade(hall, ArenaSide.West, 3f, 2f, 2f, 2.3f, feature: ArenaFeatureKind.Booth);           // market stall
            s.AgainstFacade(granary, ArenaSide.North, -4f, 3f, 1.4f, 1.2f);                                     // sacks
            s.Park("Mill Yard", ArenaFeatureKind.Trailer, 25f, -25f, 6f, 2.4f, 2.6f, facing: -1f);              // farm trailer
            s.AgainstFacade(hall, ArenaSide.West, -8f, 1.4f, 1.2f, 1.2f);                                       // crates by the lane door

            return s.Build(GeneratorId, GeneratorVersion, seed);
        }
    }
}
