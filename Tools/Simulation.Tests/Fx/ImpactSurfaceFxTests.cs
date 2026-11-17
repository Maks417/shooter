using Shooter.Contracts;
using Shooter.Presentation.Fx;
using Xunit;

namespace Shooter.Simulation.Tests.Fx
{
    public sealed class ImpactSurfaceFxTests
    {
        private static readonly Float3 Up = new Float3(0, 1, 0);
        private static readonly Float3 Side = new Float3(1, 0, 0);

        [Fact]
        public void MetalSolidWinsEverywhere()
        {
            Assert.Equal(ImpactSurface.Metal, ImpactSurfaceFx.Resolve(true, true, new Float3(0, 1, 0), Side, false));
            Assert.Equal(ImpactSurface.Metal, ImpactSurfaceFx.Resolve(true, true, new Float3(0, 0.05f, 0), Up, false));
        }

        [Fact]
        public void FloorHitsFollowRegistryGround()
        {
            var floor = new Float3(3, 0f, -2);
            Assert.Equal(ImpactSurface.Dirt, ImpactSurfaceFx.Resolve(false, false, floor, Up, asphaltGround: false));
            Assert.Equal(ImpactSurface.Concrete, ImpactSurfaceFx.Resolve(false, false, floor, Up, asphaltGround: true));
        }

        [Fact]
        public void WallsRoofsAndLowSolidsStayConcrete()
        {
            Assert.Equal(ImpactSurface.Concrete, ImpactSurfaceFx.Resolve(false, true, new Float3(0, 1.2f, 0), Side, false));
            Assert.Equal(ImpactSurface.Concrete, ImpactSurfaceFx.Resolve(false, false, new Float3(0, 3.5f, 0), Up, false));
            // A curb top at floor height belongs to the packaged solid, not the soil.
            Assert.Equal(ImpactSurface.Concrete, ImpactSurfaceFx.Resolve(false, true, new Float3(0, 0.15f, 0), Up, false));
            // Vertical faces at the floor band are not ground.
            Assert.Equal(ImpactSurface.Concrete, ImpactSurfaceFx.Resolve(false, false, new Float3(0, 0.1f, 0), Side, false));
        }

        [Theory]
        [InlineData(ImpactSurface.Metal, 1, FxKind.ImpactSpark, (byte)0)]
        [InlineData(ImpactSurface.Concrete, 2, FxKind.Dust, ImpactSurfaceFx.ConcreteVariant)]
        [InlineData(ImpactSurface.Dirt, 2, FxKind.Dust, ImpactSurfaceFx.DirtVariant)]
        public void EachSurfaceSpawnsItsOwnSet(ImpactSurface surface, int expected, FxKind first, byte variant)
        {
            var pool = new AnalyticFxPool();
            Assert.Equal(expected, ImpactSurfaceFx.Spawn(pool, surface, new Float3(0, 0, 0), Up));
            Assert.Equal(expected, pool.ActiveCount);
            var fx = pool.Get(0);
            Assert.Equal(first, fx.Kind);
            Assert.Equal(variant, fx.Variant);
            if (expected == 2)
            {
                Assert.Equal(FxKind.Debris, pool.Get(1).Kind);
                Assert.Equal(variant, pool.Get(1).Variant);
            }
        }
    }
}
