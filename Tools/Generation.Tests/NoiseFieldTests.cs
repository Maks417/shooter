using System;
using Shooter.Generation;
using Shooter.Generation.Noise;
using Xunit;

/// <summary>ADR-075 / T092: vendored FastNoiseLite v1.1.1 behind <see cref="NoiseField"/>.</summary>
public sealed class NoiseFieldTests
{
    private static NoiseField Field(NoiseBasis basis, string feature = "noise/", int period = 8, int octaves = 4) =>
        NoiseField.Create(basis, NamedRandomStream.Derive(460072030UL, "materials/noise-golden", feature + basis), period, octaves);

    // Frozen goldens. The same values are asserted in the Unity EditMode lane (NoiseFieldEditModeTests) so the
    // .NET tools lane and the editor's Mono runtime must agree bit-for-bit on vendored float math.
    public static TheoryData<NoiseBasis, double[], double[]> Goldens => new TheoryData<NoiseBasis, double[], double[]>
    {
        { NoiseBasis.Gradient, new[] { 0d, -0.5491059903256589, 0.24189669620474832 }, new[] { 0.5, 0.46066652163662747, 0.5129477091207834 } },
        { NoiseBasis.Cellular, new[] { -0.5629840500086463, -0.8366196448390766, -0.38779146749814875 }, new[] { 0.4855732777681709, 0.5244052993552571, 0.47416897202472497 } },
        { NoiseBasis.RidgedFbm, new[] { 1d, -0.35797266622167784, 0.23951416196941114 }, new[] { 1d, 0.5189413954625768, 0.5178291899823476 } },
        { NoiseBasis.DomainWarpedFbm, new[] { 0d, 0.23474330857145864, 0.18089531348765306 }, new[] { 0.5, 0.6676345888356159, 0.4783284670636132 } },
    };

    [Theory]
    [MemberData(nameof(Goldens))]
    public void GoldenValuesAreFrozen(NoiseBasis basis, double[] raw, double[] periodic)
    {
        var field = Field(basis);
        Assert.Equal(raw[0], field.Raw(0, 0));
        Assert.Equal(raw[1], field.Raw(1.25, 3.5));
        Assert.Equal(raw[2], field.Raw(7.75, 2.125));
        Assert.Equal(periodic[0], field.SamplePeriodic(0, 0));
        Assert.Equal(periodic[1], field.SamplePeriodic(.3, .7));
        Assert.Equal(periodic[2], field.SamplePeriodic(.999, .5));
    }

    [Theory]
    [InlineData(NoiseBasis.Gradient)]
    [InlineData(NoiseBasis.Cellular)]
    [InlineData(NoiseBasis.RidgedFbm)]
    [InlineData(NoiseBasis.DomainWarpedFbm)]
    public void StreamsAreIndependentAndRepeatable(NoiseBasis basis)
    {
        Assert.Equal(Field(basis).SamplePeriodic(.41, .17), Field(basis).SamplePeriodic(.41, .17));
        Assert.NotEqual(Field(basis).SamplePeriodic(.41, .17), Field(basis, "noise/other/").SamplePeriodic(.41, .17));
    }

    [Theory]
    [InlineData(NoiseBasis.Gradient)]
    [InlineData(NoiseBasis.Cellular)]
    [InlineData(NoiseBasis.RidgedFbm)]
    [InlineData(NoiseBasis.DomainWarpedFbm)]
    public void TileSeamIsContinuousInBothAxes(NoiseBasis basis)
    {
        var field = Field(basis);
        for (int k = 0; k < 200; k++)
        {
            double t = k / 200.0 + .0025;
            Assert.InRange(Math.Abs(field.SamplePeriodic(0, t) - field.SamplePeriodic(1 - 1e-12, t)), 0, 1e-9);
            Assert.InRange(Math.Abs(field.SamplePeriodic(t, 0) - field.SamplePeriodic(t, 1 - 1e-12)), 0, 1e-9);
        }
    }

    [Theory]
    [InlineData(NoiseBasis.Gradient)]
    [InlineData(NoiseBasis.Cellular)]
    [InlineData(NoiseBasis.RidgedFbm)]
    [InlineData(NoiseBasis.DomainWarpedFbm)]
    public void SeamSlopeIsNoRougherThanTheInterior(NoiseBasis basis)
    {
        // Height-derived normals use slopes; the quintic seam blend must not add a crease the noise itself lacks.
        var field = Field(basis);
        const double h = 1e-5;
        double Jump(double u0)
        {
            double sum = 0;
            for (int k = 0; k < 400; k++)
            {
                double v = k / 400.0 + .00125;
                double left = (field.SamplePeriodic(u0 - 1e-13, v) - field.SamplePeriodic(u0 - h, v)) / h;
                double right = (field.SamplePeriodic(u0 + h, v) - field.SamplePeriodic(u0, v)) / h;
                sum += Math.Abs(left - right);
            }
            return sum / 400;
        }
        double interior = Math.Max(Jump(.37), Jump(.61));
        Assert.True(Jump(1) <= 2 * interior, $"seam slope jump {Jump(1)} vs interior {interior}");
    }

    [Theory]
    [InlineData(NoiseBasis.Gradient)]
    [InlineData(NoiseBasis.Cellular)]
    [InlineData(NoiseBasis.RidgedFbm)]
    [InlineData(NoiseBasis.DomainWarpedFbm)]
    public void TileCentreKeepsEdgeContrastAndRange(NoiseBasis basis)
    {
        var field = Field(basis);
        double Std(double from, double to)
        {
            double s = 0, s2 = 0; int n = 0;
            for (int i = 0; i < 60; i++) for (int j = 0; j < 60; j++)
            {
                double value = field.SamplePeriodic(from + (to - from) * i / 60, from + (to - from) * j / 60);
                Assert.InRange(value, 0, 1);
                s += value; s2 += value * value; n++;
            }
            double mean = s / n; return Math.Sqrt(s2 / n - mean * mean);
        }
        double ratio = Std(.4, .6) / Std(-.1, .1);
        Assert.InRange(ratio, .6, 1.6);
    }

    [Fact]
    public void RejectsOutOfRangeParameters()
    {
        var stream = NamedRandomStream.Derive(1, "materials/noise-golden", "noise/args");
        Assert.Throws<ArgumentNullException>(() => NoiseField.Create(NoiseBasis.Gradient, null!, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => NoiseField.Create(NoiseBasis.Gradient, stream, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => NoiseField.Create(NoiseBasis.Gradient, stream, NoiseField.MaxPeriod + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => NoiseField.Create(NoiseBasis.Gradient, stream, 8, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => NoiseField.Create(NoiseBasis.Gradient, stream, 8, NoiseField.MaxOctaves + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => NoiseField.Create((NoiseBasis)99, stream, 8));
        Assert.Throws<ArgumentException>(() => NoiseField.Create(NoiseBasis.Gradient, stream, 8).SamplePeriodic(double.NaN, 0));
    }
}
