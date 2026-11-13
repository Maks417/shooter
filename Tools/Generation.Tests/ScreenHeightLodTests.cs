using System;
using Shooter.Generation;
using Xunit;

public sealed class ScreenHeightLodTests
{
    [Theory]
    [InlineData(1, 0)] [InlineData(.3f, 1)] [InlineData(.1f, 2)] [InlineData(.01f, 3)]
    public void InitialLevelUsesViewportFraction(float height, int expected) => Assert.Equal(expected, ScreenHeightLod.Select(height));
    [Fact]
    public void RepeatedThresholdJitterDoesNotToggle()
    {
        foreach (var pair in new[] { (.5f, 0), (.2f, 1), (.08f, 2) })
        {
            int near = pair.Item2, far = near + 1;
            for (int i = 0; i < 100; i++)
            {
                float height = pair.Item1 * (i % 2 == 0 ? .98f : 1.02f);
                Assert.Equal(near, ScreenHeightLod.Select(height, near));
                Assert.Equal(far, ScreenHeightLod.Select(height, far));
            }
            Assert.Equal(far, ScreenHeightLod.Select(pair.Item1 * .89f, near));
            Assert.Equal(near, ScreenHeightLod.Select(pair.Item1 * 1.11f, far));
        }
    }
    [Fact]
    public void CameraCutsCanCrossAllLevelsAndViewArmsStayNear()
    {
        Assert.Equal(3, ScreenHeightLod.Select(.001f, 0));
        Assert.Equal(0, ScreenHeightLod.Select(2, 3));
        Assert.Equal(0, ScreenHeightLod.Select(.001f, 3, true));
        Assert.Equal(1f, ScreenHeightLod.PerspectiveHeight(2, 1, 90) / float.MaxValue);
        Assert.InRange(ScreenHeightLod.PerspectiveHeight(2, 10, 90), .09999f, .10001f);
    }
    [Fact]
    public void InvalidProjectionFailsExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ScreenHeightLod.Select(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => ScreenHeightLod.PerspectiveHeight(1, 10, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ScreenHeightLod.Select(1, 4));
    }
}
