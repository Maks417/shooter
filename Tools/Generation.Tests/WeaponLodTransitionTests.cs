using System;
using System.Collections.Generic;
using Shooter.Generation;
using Xunit;

/// <summary>
/// Formal numeric weapon LOD transition harness for M1 P03.
/// Asserts ScreenHeightLod hysteresis, rifle/pistol projection diameters, and documented overrides.
/// Does not establish silhouette/visual acceptance.
/// </summary>
public sealed class WeaponLodTransitionTests
{
    // Gallery-verified bounding diameters (GeneratedGallery: rifle 1.04 m, pistol 0.30 m).
    private const float RifleDiameterMeters = 1.04f;
    private const float PistolDiameterMeters = .30f;
    private const float VerticalFovDegrees = 60f;
    private static readonly float[] Thresholds = { .50f, .20f, .08f };

    [Theory]
    [InlineData(RifleDiameterMeters)]
    [InlineData(PistolDiameterMeters)]
    public void StableLodInsideHysteresisBand(float diameterMeters)
    {
        foreach (var (threshold, near, far) in BandPairs())
        {
            // Stay inside ±10% of the shared threshold; previous LOD must not flip.
            float[] heights =
            {
                threshold * .95f,
                threshold,
                threshold * 1.05f,
                threshold * (1f - ScreenHeightLod.Hysteresis + .005f),
                threshold * (1f + ScreenHeightLod.Hysteresis - .005f),
            };
            foreach (float height in heights)
            {
                Assert.Equal(near, ScreenHeightLod.Select(height, near));
                Assert.Equal(far, ScreenHeightLod.Select(height, far));
            }

            // Same band expressed via perspective projection for the weapon diameter.
            foreach (float depth in DepthsForHeightBand(diameterMeters, threshold * .92f, threshold * 1.08f))
            {
                float projected = ScreenHeightLod.PerspectiveHeight(diameterMeters, depth, VerticalFovDegrees);
                Assert.Equal(near, ScreenHeightLod.Select(projected, near));
                Assert.Equal(far, ScreenHeightLod.Select(projected, far));
            }
        }
    }

    [Theory]
    [InlineData(RifleDiameterMeters)]
    [InlineData(PistolDiameterMeters)]
    public void SwitchOnlyAfterCrossingThresholdWithHysteresis(float diameterMeters)
    {
        foreach (var (threshold, near, far) in BandPairs())
        {
            float stayNear = threshold * (1f - ScreenHeightLod.Hysteresis + .01f);
            float leaveNear = threshold * (1f - ScreenHeightLod.Hysteresis - .01f);
            float stayFar = threshold * (1f + ScreenHeightLod.Hysteresis - .01f);
            float leaveFar = threshold * (1f + ScreenHeightLod.Hysteresis + .01f);

            Assert.Equal(near, ScreenHeightLod.Select(stayNear, near));
            Assert.Equal(far, ScreenHeightLod.Select(leaveNear, near));
            Assert.Equal(far, ScreenHeightLod.Select(stayFar, far));
            Assert.Equal(near, ScreenHeightLod.Select(leaveFar, far));

            // Projection sweep: approach from the near side until hysteresis forces a far switch.
            int lod = near;
            bool switched = false;
            foreach (float depth in DepthsForHeightBand(diameterMeters, threshold * 1.2f, threshold * .7f))
            {
                float height = ScreenHeightLod.PerspectiveHeight(diameterMeters, depth, VerticalFovDegrees);
                int next = ScreenHeightLod.Select(height, lod);
                if (next != lod)
                {
                    Assert.Equal(far, next);
                    Assert.True(height < threshold * (1f - ScreenHeightLod.Hysteresis));
                    switched = true;
                }
                else Assert.Equal(switched ? far : near, next);
                lod = next;
            }
            Assert.True(switched);

            // Reverse: approach from the far side until hysteresis forces a near switch.
            lod = far;
            switched = false;
            foreach (float depth in DepthsForHeightBand(diameterMeters, threshold * .7f, threshold * 1.2f))
            {
                float height = ScreenHeightLod.PerspectiveHeight(diameterMeters, depth, VerticalFovDegrees);
                int next = ScreenHeightLod.Select(height, lod);
                if (next != lod)
                {
                    Assert.Equal(near, next);
                    Assert.True(height > threshold * (1f + ScreenHeightLod.Hysteresis));
                    switched = true;
                }
                else Assert.Equal(switched ? near : far, next);
                lod = next;
            }
            Assert.True(switched);
        }
    }

    [Fact]
    public void CameraCutCanCrossAllLevelsInOneSelect()
    {
        // Documented camera-cut behavior: one Select may jump across every level.
        Assert.Equal(3, ScreenHeightLod.Select(.001f, 0));
        Assert.Equal(0, ScreenHeightLod.Select(2f, 3));
        Assert.Equal(2, ScreenHeightLod.Select(.10f, 0));
        Assert.Equal(1, ScreenHeightLod.Select(.25f, 3));
        Assert.Equal(0, ScreenHeightLod.Select(.9f, 3));
    }

    [Fact]
    public void FirstPersonOverrideForcesLodZero()
    {
        // ScreenHeightLod firstPerson flag is the documented view-arms override (inspect is presentation Automatic=false).
        foreach (float height in new[] { .001f, .08f, .2f, .5f, 2f })
        {
            for (int previous = -1; previous <= 3; previous++)
                Assert.Equal(0, ScreenHeightLod.Select(height, previous, firstPerson: true));
        }
    }

    [Theory]
    [InlineData(RifleDiameterMeters)]
    [InlineData(PistolDiameterMeters)]
    public void HeightSweepDoesNotFlickerInsideBands(float diameterMeters)
    {
        // Oscillate inside each deadband; previous near/far must remain sticky (no A-B-A).
        foreach (var (threshold, near, far) in BandPairs())
        {
            int nearLod = near, farLod = far;
            for (int i = 0; i < 64; i++)
            {
                float height = threshold * (i % 2 == 0 ? .98f : 1.02f);
                Assert.Equal(near, ScreenHeightLod.Select(height, nearLod));
                Assert.Equal(far, ScreenHeightLod.Select(height, farLod));
                nearLod = ScreenHeightLod.Select(height, nearLod);
                farLod = ScreenHeightLod.Select(height, farLod);
            }
            Assert.Equal(near, nearLod);
            Assert.Equal(far, farLod);

            float low = threshold * .95f, high = threshold * 1.05f;
            int projectedNear = near, projectedFar = far;
            foreach (float depth in DepthsForHeightBand(diameterMeters, high, low, 32))
            {
                float height = ScreenHeightLod.PerspectiveHeight(diameterMeters, depth, VerticalFovDegrees);
                int nextNear = ScreenHeightLod.Select(height, projectedNear);
                int nextFar = ScreenHeightLod.Select(height, projectedFar);
                Assert.Equal(near, nextNear);
                Assert.Equal(far, nextFar);
                projectedNear = nextNear;
                projectedFar = nextFar;
            }
        }
    }

    [Fact]
    public void InitialSelectionIgnoresHysteresis()
    {
        Assert.Equal(0, ScreenHeightLod.Select(.5f));
        Assert.Equal(1, ScreenHeightLod.Select(.499f));
        Assert.Equal(1, ScreenHeightLod.Select(.2f));
        Assert.Equal(2, ScreenHeightLod.Select(.199f));
        Assert.Equal(2, ScreenHeightLod.Select(.08f));
        Assert.Equal(3, ScreenHeightLod.Select(.079f));
    }

    private static IEnumerable<(float Threshold, int Near, int Far)> BandPairs()
    {
        for (int i = 0; i < Thresholds.Length; i++)
            yield return (Thresholds[i], i, i + 1);
    }

    /// <summary>Monotonic depth samples whose projected heights cover [heightHigh, heightLow] or the reverse.</summary>
    private static IEnumerable<float> DepthsForHeightBand(float diameterMeters, float heightStart, float heightEnd, int steps = 48)
    {
        if (heightStart <= 0 || heightEnd <= 0) throw new ArgumentOutOfRangeException(nameof(heightStart));
        float Depth(float height)
        {
            // height = diameter / (2 * depth * tan(fov/2)) => depth = diameter / (2 * height * tan(fov/2))
            double tanHalf = Math.Tan(VerticalFovDegrees * Math.PI / 360);
            return (float)(diameterMeters / (2 * height * tanHalf));
        }
        float d0 = Depth(heightStart), d1 = Depth(heightEnd);
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            yield return d0 + (d1 - d0) * t;
        }
    }
}
