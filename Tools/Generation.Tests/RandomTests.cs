#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shooter.Generation;
using Xunit;

public sealed class RandomTests
{
    private static JsonDocument Golden() => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "golden-vectors.json")));

    [Fact]
    public void PublishedPcgVectorMatchesEveryBit()
    {
        using var golden = Golden();
        var pcg = golden.RootElement.GetProperty("pcg");
        var generator = new Pcg32(ulong.Parse(pcg.GetProperty("initstate").GetString()!, CultureInfo.InvariantCulture), ulong.Parse(pcg.GetProperty("initseq").GetString()!, CultureInfo.InvariantCulture));
        foreach (var value in pcg.GetProperty("outputsHex").EnumerateArray())
            Assert.Equal(uint.Parse(value.GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture), generator.NextUInt());
    }

    [Theory]
    [InlineData(0UL, 0UL, "e4c14788,379c6516,5c4ab3bb,601d23e0")]
    [InlineData(ulong.MaxValue, ulong.MaxValue, "2675c047,7779a837,a145aa13,5f6be726")]
    public void BoundarySeedVectorsFromIndependentPythonVerifier(ulong state, ulong sequence, string expected)
    {
        var generator = new Pcg32(state, sequence);
        Assert.Equal(expected, string.Join(",", Enumerable.Range(0, 4).Select(_ => generator.NextUInt().ToString("x8", CultureInfo.InvariantCulture))));
    }

    [Fact]
    public void BoundedSelectionRejectsLowValuesAndConsumesRetries()
    {
        var generator = new Pcg32(42, 54);
        const uint bound = 0x80000001;
        Assert.Equal(0x215c02b6U, generator.NextBounded(bound));
        // The next published value 7b47f409 is below threshold 7fffffff.
        Assert.Equal(0x3a1d332fU, generator.NextBounded(bound));
        Assert.Equal(0x83d2f293U, generator.NextUInt());
    }

    [Fact]
    public void ZeroBoundRejectsWithoutAdvancingAndUnitBoundStillConsumesOneValue()
    {
        var generator = new Pcg32(42, 54);
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.NextBounded(0));
        Assert.Equal(0U, generator.NextBounded(1));
        Assert.Equal(0x7b47f409U, generator.NextUInt());
    }

    [Theory]
    [InlineData(2U)]
    [InlineData(255U)]
    [InlineData(uint.MaxValue)]
    public void BoundedResultsStayInsideHalfOpenRange(uint bound)
    {
        var generator = new Pcg32(ulong.MaxValue, 0);
        for (int i = 0; i < 10000; i++) Assert.True(generator.NextBounded(bound) < bound);
    }

    [Fact]
    public void UnitFloatUsesTop24BitsAndConsumesExactlyOneValue()
    {
        var generator = new Pcg32(42, 54);
        Assert.Equal((0xa15c02b7U >> 8) / 16777216.0f, generator.NextUnitFloat());
        Assert.Equal(0x7b47f409U, generator.NextUInt());
        for (int i = 0; i < 10000; i++)
        {
            float value = generator.NextUnitFloat();
            Assert.True(value >= 0 && value < 1);
        }
    }

    [Fact]
    public void NamedStreamDigestAndInitialStateMatchIndependentFixture()
    {
        using var golden = Golden();
        var named = golden.RootElement.GetProperty("namedStream");
        var stream = NamedRandomStream.Derive(ulong.Parse(named.GetProperty("rootSeed").GetString()!, CultureInfo.InvariantCulture), named.GetProperty("recipeId").GetString()!, named.GetProperty("featureKey").GetString()!);
        Assert.Equal(named.GetProperty("sha256").GetString(), stream.StreamId.ToString());
        Assert.Equal(named.GetProperty("initstate").GetString(), stream.InitState.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(named.GetProperty("initseq").GetString(), stream.InitSequence.ToString(CultureInfo.InvariantCulture));
        var direct = new Pcg32(stream.InitState, stream.InitSequence);
        var derived = stream.CreateGenerator();
        for (int i = 0; i < 100; i++) Assert.Equal(direct.NextUInt(), derived.NextUInt());
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(ulong.MaxValue)]
    [InlineData(460072030UL)]
    public void CosmeticGenerationAndParallelRequestOrderDoNotChangeLayout(ulong seed)
    {
        uint[] baseline = Sample(seed, "building/03/door/02");
        Parallel.For(0, 32, i => Sample(seed, "grass/" + i.ToString(CultureInfo.InvariantCulture)));
        Assert.Equal(baseline, Sample(seed, "building/03/door/02"));
        Assert.NotEqual(baseline, Sample(seed, "building/03/door/03"));
    }

    [Fact]
    public void LengthPrefixesSeparateAmbiguousStringConcatenations()
    {
        Assert.NotEqual(NamedRandomStream.Derive(1, "a", "bc").StreamId, NamedRandomStream.Derive(1, "ab", "c").StreamId);
        Assert.NotEqual(NamedRandomStream.Derive(0, "a", "b").StreamId, NamedRandomStream.Derive(ulong.MaxValue, "a", "b").StreamId);
    }

    [Fact]
    public void InvalidStreamKeysRejectBeforeHashingAndMaximumLengthIsAccepted()
    {
        foreach (string invalid in new[] { "", "caf\u00e9", "bad\nkey", new string('a', 4097), "\ud800" })
        {
            Assert.Throws<ArgumentException>(() => NamedRandomStream.Derive(1, invalid, "ok"));
            Assert.Throws<ArgumentException>(() => NamedRandomStream.Derive(1, "ok", invalid));
        }
        Assert.Throws<ArgumentNullException>(() => NamedRandomStream.Derive(1, null!, "ok"));
        Assert.Throws<ArgumentNullException>(() => NamedRandomStream.Derive(1, "ok", null!));
        Assert.Equal(NamedRandomStream.Derive(1, new string('a', 4096), "ok").StreamId, NamedRandomStream.Derive(1, new string('a', 4096), "ok").StreamId);
    }

    private static uint[] Sample(ulong seed, string key)
    {
        var generator = NamedRandomStream.Derive(seed, "fixtures/box", key).CreateGenerator();
        return Enumerable.Range(0, 32).Select(_ => generator.NextUInt()).ToArray();
    }
}
