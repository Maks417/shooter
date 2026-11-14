#nullable enable
using System.Collections.Generic;
using Shooter.Generation.Environment;
using Xunit;

namespace Shooter.Generation.Tests
{
    public sealed class MapTopologyTests
    {
        [Theory]
        [InlineData("training-compound", 460072030UL, false)]
        [InlineData("depot-yard", 460072031UL, true)]
        public void Approved_maps_satisfy_FIX07_spawn_route_topology(string mapId, ulong seed, bool depot)
        {
            var plan = depot ? DepotPlanner.Generate(seed) : CompoundPlanner.Generate(seed);
            var report = MapTopologyValidation.Validate(mapId, plan);
            Assert.True(report.Passed, report.Diagnostic);
            Assert.True(report.Team1Slots >= 12);
            Assert.True(report.Team2Slots >= 12);
            Assert.True(report.UniqueSlots);
            Assert.True(report.StandingClearance);
            Assert.True(report.InitialSpawnLosBlocked);
            Assert.True(report.Team1StagingExits >= 2);
            Assert.True(report.Team2StagingExits >= 2);
            Assert.True(report.DistinctRoutes >= 3);
            Assert.True(report.HasCoveredRoute);
        }

        [Fact]
        public void Depot_topology_differs_from_compound_and_keeps_approved_seed()
        {
            var depot = DepotPlanner.Generate(460072031UL);
            var compound = CompoundPlanner.Generate(460072030UL);
            Assert.Equal(460072031UL, MapRecipe.ApprovedSeed("depot-yard"));
            Assert.NotEqual(42UL, MapRecipe.ApprovedSeed("depot-yard"));
            Assert.NotEqual(depot.CollisionHash, compound.CollisionHash);
            Assert.True(depot.CollisionBoxes.Count != compound.CollisionBoxes.Count);
            Assert.Empty(depot.Buildings);
            Assert.Equal(12, compound.Buildings.Count);
        }

        [Fact]
        public void Spawn_layout_emits_unique_ids_and_team_balance()
        {
            var plan = CompoundPlanner.Generate(460072030UL);
            var slots = MapSpawnLayout.BuildBothTeams(plan);
            Assert.Equal(24, slots.Count);
            Assert.True(MapSpawnLayout.PositionsUnique(slots));
            var seen = new HashSet<uint>();
            int t1 = 0, t2 = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                Assert.True(seen.Add(slots[i].SlotId));
                if (slots[i].Team == 1) t1++; else t2++;
            }
            Assert.Equal(12, t1);
            Assert.Equal(12, t2);
        }
    }
}
