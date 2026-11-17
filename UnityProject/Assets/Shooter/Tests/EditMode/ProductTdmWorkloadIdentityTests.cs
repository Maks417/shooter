using NUnit.Framework;
using Shooter.Game.Session;

namespace Shooter.Tests.EditMode
{
    /// <summary>EditMode identity checks for FIX12 product TDM workloads. Not hardware evidence.</summary>
    public sealed class ProductTdmWorkloadIdentityTests
    {
        [Test]
        public void Depot_and_compound_measurement_ids_remain_distinct()
        {
            Assert.IsTrue(ProductTdmWorkloads.TryGet(ProductTdmWorkloads.Compound15v15, out var compound));
            Assert.IsTrue(ProductTdmWorkloads.TryGet(ProductTdmWorkloads.Depot15v15, out var depot));
            Assert.AreEqual("training-compound", compound.MapId.Value);
            Assert.AreEqual("depot-yard", depot.MapId.Value);
            Assert.AreEqual(460072030UL, compound.Seed);
            Assert.AreEqual(460072031UL, depot.Seed);
        }
    }
}
