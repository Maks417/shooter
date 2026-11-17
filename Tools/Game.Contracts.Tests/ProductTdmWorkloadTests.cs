using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Xunit;

namespace Shooter.Game.Contracts.Tests
{
    public sealed class ProductTdmWorkloadTests
    {
        [Theory]
        [InlineData(ProductTdmWorkloads.Compound5v5, "training-compound", 5, 10UL, 460072030UL)]
        [InlineData(ProductTdmWorkloads.Compound8v8, "training-compound", 8, 16UL, 460072030UL)]
        [InlineData(ProductTdmWorkloads.Compound15v15, "training-compound", 15, 30UL, 460072030UL)]
        [InlineData(ProductTdmWorkloads.Depot5v5, "depot-yard", 5, 10UL, 460072031UL)]
        [InlineData(ProductTdmWorkloads.Depot10v10, "depot-yard", 10, 20UL, 460072031UL)]
        [InlineData(ProductTdmWorkloads.Oil5v5, "oil-depot", 5, 10UL, 460072032UL)]
        [InlineData(ProductTdmWorkloads.Oil15v15, "oil-depot", 15, 30UL, 460072032UL)]
        public void Measurement_identities_keep_distinct_map_seeds(string id, string map, byte teamSize, ulong actors, ulong seed)
        {
            Assert.True(ProductTdmWorkloads.TryGet(id, out var workload));
            Assert.Equal(map, workload.MapId.Value);
            Assert.Equal(teamSize, workload.TeamSize);
            Assert.Equal((int)actors, workload.Actors);
            Assert.Equal(seed, workload.Seed);
            Assert.Contains("leaseAcquires", workload.RequiredCounters);
            Assert.Contains("renderFrames", workload.RequiredCounters);
        }

        [Fact]
        public void Depot_is_not_implied_by_compound_workload_ids()
        {
            var compound = ProductTdmWorkloads.Require(ProductTdmWorkloads.Compound5v5);
            var depot = ProductTdmWorkloads.Require(ProductTdmWorkloads.Depot5v5);
            Assert.Equal(WellKnownMaps.TrainingCompound, compound.MapId);
            Assert.Equal(WellKnownMaps.DepotYard, depot.MapId);
            Assert.NotEqual(compound.Seed, depot.Seed);
        }

        [Fact]
        public void Oil_is_not_implied_by_compound_or_depot_workload_ids()
        {
            var compound = ProductTdmWorkloads.Require(ProductTdmWorkloads.Compound5v5);
            var depot = ProductTdmWorkloads.Require(ProductTdmWorkloads.Depot5v5);
            var oil = ProductTdmWorkloads.Require(ProductTdmWorkloads.Oil5v5);
            Assert.Equal(WellKnownMaps.OilDepot, oil.MapId);
            Assert.NotEqual(compound.Seed, oil.Seed);
            Assert.NotEqual(depot.Seed, oil.Seed);
        }

        [Fact]
        public void B01_and_unknown_ids_are_not_product_tdm_workloads()
        {
            Assert.False(ProductTdmWorkloads.IsProductWorkload("B01-minimal-v1"));
            Assert.False(ProductTdmWorkloads.IsProductWorkload("TDM-5v5-v1-depot-silent"));
            Assert.False(ProductTdmWorkloads.IsProductWorkload("TDM-6v6-v1"));
            Assert.False(ProductTdmWorkloads.IsProductWorkload(""));
        }
    }
}
