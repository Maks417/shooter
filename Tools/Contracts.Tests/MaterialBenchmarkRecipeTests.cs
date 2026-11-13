using Shooter.Bootstrap;
using Xunit;

namespace Shooter.Tests
{
    public sealed class MaterialBenchmarkRecipeTests
    {
        [Theory]
        [InlineData(0u, 0, -6f)] [InlineData(359u, 0, -6f)] [InlineData(360u, 1, -4f)]
        [InlineData(2159u, 5, 4f)] [InlineData(2160u, 6, 6f)] [InlineData(2519u, 6, 6f)]
        [InlineData(2520u, 0, -6f)] [InlineData(3600u, 3, 0f)]
        public void ExactSixSecondCutsWrapAtFortyTwoSeconds(uint tick, int view, float x)
        { Assert.Equal(view, MaterialBenchmarkRecipe.ViewIndex(tick)); Assert.Equal(x, MaterialBenchmarkRecipe.CameraX(tick)); }
        [Fact]
        public void EachCycleHasExactlyEqualViewDurations()
        {
            var counts = new int[7];
            for (uint tick = 0; tick < MaterialBenchmarkRecipe.CycleTicks; tick++) counts[MaterialBenchmarkRecipe.ViewIndex(tick)]++;
            foreach (int count in counts) Assert.Equal(360, count);
            Assert.Equal(64, MaterialBenchmarkRecipe.RouteHash.Length);
        }
    }
}
