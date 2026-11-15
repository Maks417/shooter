using System;
using Xunit;

/// <summary>
/// Offline contract tests for extractable sky/probe parameter math.
/// Canonical production copies: Presentation/Rendering/GeneratedSky.cs (GeneratedSkyMath)
/// and GeneratedReflectionProbe.Validate. Keep formulas identical until promoted into Generation.Core
/// and linked from this project.
/// </summary>
public sealed class GeneratedSkyTests
{
    [Fact]
    public void DefaultGalleryPaletteIsFiniteAndOrdered()
    {
        AssertFiniteColor(GeneratedSkyMath.DefaultZenith);
        AssertFiniteColor(GeneratedSkyMath.DefaultHorizon);
        AssertFiniteColor(GeneratedSkyMath.DefaultGround);
        GeneratedSkyMath.Validate(
            GeneratedSkyMath.DefaultZenith,
            GeneratedSkyMath.DefaultHorizon,
            GeneratedSkyMath.DefaultGround,
            true,
            GeneratedSkyMath.DefaultFogStartMeters,
            GeneratedSkyMath.DefaultFogEndMeters,
            GeneratedSkyMath.DefaultFogDensity);
        Assert.True(GeneratedSkyMath.DefaultFogEndMeters > GeneratedSkyMath.DefaultFogStartMeters);
    }

    [Fact]
    public void GradientEndpointsMatchInputs()
    {
        var zenith = new GeneratedSkyMath.Rgb(.1f, .2f, .3f);
        var horizon = new GeneratedSkyMath.Rgb(.4f, .5f, .6f);
        var ground = new GeneratedSkyMath.Rgb(.7f, .8f, .9f);
        AssertRgb(zenith, GeneratedSkyMath.SampleGradient(zenith, horizon, ground, 1));
        AssertRgb(horizon, GeneratedSkyMath.SampleGradient(zenith, horizon, ground, 0));
        AssertRgb(ground, GeneratedSkyMath.SampleGradient(zenith, horizon, ground, -1));
        var mid = GeneratedSkyMath.SampleGradient(zenith, horizon, ground, .5f);
        Assert.InRange(mid.R, .24f, .26f);
        Assert.InRange(mid.G, .34f, .36f);
        Assert.InRange(mid.B, .44f, .46f);
    }

    [Fact]
    public void RejectsNanAndOutOfRangeColors()
    {
        var ok = new GeneratedSkyMath.Rgb(.2f, .2f, .2f);
        Assert.Throws<ArgumentOutOfRangeException>(() => GeneratedSkyMath.Validate(new GeneratedSkyMath.Rgb(float.NaN, 0, 0), ok, ok, false, 0, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => GeneratedSkyMath.Validate(new GeneratedSkyMath.Rgb(-.1f, 0, 0), ok, ok, false, 0, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => GeneratedSkyMath.Validate(new GeneratedSkyMath.Rgb(9f, 0, 0), ok, ok, false, 0, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => GeneratedSkyMath.SampleGradient(ok, ok, ok, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => GeneratedSkyMath.SampleGradient(ok, ok, ok, 1.1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => GeneratedSkyMath.Lerp(ok, ok, float.PositiveInfinity));
    }

    [Fact]
    public void RejectsInvalidFogParams()
    {
        var ok = GeneratedSkyMath.DefaultZenith;
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GeneratedSkyMath.Validate(ok, ok, ok, true, 10, 10, .01f));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GeneratedSkyMath.Validate(ok, ok, ok, true, 10, 5, .01f));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GeneratedSkyMath.Validate(ok, ok, ok, true, 0, 10, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GeneratedSkyMath.Validate(ok, ok, ok, true, -1, 10, .01f));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GeneratedSkyMath.Validate(ok, ok, ok, true, 0, 10, 1.5f));
        GeneratedSkyMath.Validate(ok, ok, ok, false, float.NaN, float.NaN, float.NaN);
    }

    [Fact]
    public void ReflectionProbeDefaultsAreAcceptedAndRejectNan()
    {
        GeneratedReflectionProbeMath.Validate(GeneratedReflectionProbeMath.DefaultBoxSizeMeters, GeneratedReflectionProbeMath.DefaultIntensity, GeneratedReflectionProbeMath.DefaultResolution);
        GeneratedReflectionProbeMath.Validate(GeneratedReflectionProbeMath.ProductMapBoxSizeMeters, GeneratedReflectionProbeMath.ProductMapIntensity, GeneratedReflectionProbeMath.DefaultResolution);
        Assert.Throws<ArgumentOutOfRangeException>(() => GeneratedReflectionProbeMath.Validate(float.NaN, 1f, 128));
        Assert.Throws<ArgumentOutOfRangeException>(() => GeneratedReflectionProbeMath.Validate(.5f, 1f, 128));
        Assert.Throws<ArgumentOutOfRangeException>(() => GeneratedReflectionProbeMath.Validate(12f, -1f, 128));
        Assert.Throws<ArgumentOutOfRangeException>(() => GeneratedReflectionProbeMath.Validate(12f, 1f, 48));
    }

    [Fact]
    public void ProductMapPaletteVariantsStayFiniteAndDistinct()
    {
        GeneratedSkyMath.ResolveProductPalette("training-compound", out var compoundZ, out var compoundH, out var compoundG, out float cStart, out float cEnd, out float cDensity);
        GeneratedSkyMath.ResolveProductPalette("depot-yard", out var depotZ, out var depotH, out var depotG, out float dStart, out float dEnd, out float dDensity);
        GeneratedSkyMath.ResolveProductPalette("unknown-map", out var fallbackZ, out _, out _, out _, out _, out _);
        GeneratedSkyMath.Validate(compoundZ, compoundH, compoundG, true, cStart, cEnd, cDensity);
        GeneratedSkyMath.Validate(depotZ, depotH, depotG, true, dStart, dEnd, dDensity);
        Assert.Equal(GeneratedSkyMath.DefaultZenith.R, fallbackZ.R, 5);
        Assert.True(cEnd > cStart);
        Assert.True(dEnd > dStart);
        Assert.NotEqual(compoundZ.B, depotZ.B);
        Assert.True(depotH.R >= compoundH.R);
        // Cooler compound vs warmer depot: zenith leans blue on compound, horizon leans amber on depot.
        Assert.True(compoundZ.B > compoundZ.R);
        Assert.True(depotH.R > depotH.B);
        Assert.True(compoundZ.B > depotZ.B);
        // Linear fog leaves 40–60 m fights clear (factor at 50 m under ~0.4) while still denser than a washed-out sky.
        Assert.True(cStart < 40f && cEnd > 60f);
        Assert.True(dStart < 40f && dEnd > 60f);
        Assert.InRange(LinearFogFactor(50f, cStart, cEnd), 0.05f, 0.40f);
        Assert.InRange(LinearFogFactor(50f, dStart, dEnd), 0.10f, 0.45f);
        Assert.InRange(LinearFogFactor(50f, GeneratedSkyMath.DefaultFogStartMeters, GeneratedSkyMath.DefaultFogEndMeters), 0.10f, 0.40f);
        Assert.True(GeneratedSkyMath.DefaultFogDensity >= .0075f);
        Assert.True(dDensity >= cDensity);
    }

    private static float LinearFogFactor(float distanceMeters, float fogStartMeters, float fogEndMeters)
    {
        Assert.True(fogEndMeters > fogStartMeters);
        float t = (distanceMeters - fogStartMeters) / (fogEndMeters - fogStartMeters);
        if (t < 0f) return 0f;
        if (t > 1f) return 1f;
        return t;
    }

    private static void AssertFiniteColor(GeneratedSkyMath.Rgb color)
    {
        Assert.False(float.IsNaN(color.R) || float.IsNaN(color.G) || float.IsNaN(color.B));
        Assert.InRange(color.R, 0, 8);
        Assert.InRange(color.G, 0, 8);
        Assert.InRange(color.B, 0, 8);
    }

    private static void AssertRgb(GeneratedSkyMath.Rgb expected, GeneratedSkyMath.Rgb actual)
    {
        Assert.Equal(expected.R, actual.R, 5);
        Assert.Equal(expected.G, actual.G, 5);
        Assert.Equal(expected.B, actual.B, 5);
    }
}

/// <summary>Mirror of Presentation GeneratedSkyMath for offline Generation.Tests. Keep in sync.</summary>
internal static class GeneratedSkyMath
{
    public readonly struct Rgb
    {
        public readonly float R, G, B;
        public Rgb(float r, float g, float b) { R = r; G = g; B = b; }
    }

    public static readonly Rgb DefaultZenith = new Rgb(.24f, .38f, .56f);
    public static readonly Rgb DefaultHorizon = new Rgb(.64f, .67f, .68f);
    public static readonly Rgb DefaultGround = new Rgb(.26f, .23f, .18f);
    public const float DefaultFogStartMeters = 30f;
    public const float DefaultFogEndMeters = 100f;
    public const float DefaultFogDensity = .0082f;

    public static void ResolveProductPalette(
        string mapId,
        out Rgb zenith,
        out Rgb horizon,
        out Rgb ground,
        out float fogStartMeters,
        out float fogEndMeters,
        out float fogDensity)
    {
        fogStartMeters = DefaultFogStartMeters;
        fogEndMeters = DefaultFogEndMeters;
        fogDensity = DefaultFogDensity;
        if (string.Equals(mapId, "training-compound", StringComparison.Ordinal))
        {
            zenith = new Rgb(.20f, .36f, .56f);
            horizon = new Rgb(.56f, .64f, .70f);
            ground = new Rgb(.20f, .22f, .20f);
            fogStartMeters = 32f;
            fogEndMeters = 105f;
            fogDensity = .0078f;
            return;
        }
        if (string.Equals(mapId, "depot-yard", StringComparison.Ordinal))
        {
            zenith = new Rgb(.32f, .40f, .46f);
            horizon = new Rgb(.72f, .60f, .50f);
            ground = new Rgb(.34f, .26f, .16f);
            fogStartMeters = 26f;
            fogEndMeters = 92f;
            fogDensity = .0090f;
            return;
        }
        zenith = DefaultZenith;
        horizon = DefaultHorizon;
        ground = DefaultGround;
    }

    public static void Validate(Rgb zenith, Rgb horizon, Rgb ground, bool fogEnabled, float fogStartMeters, float fogEndMeters, float fogDensity)
    {
        ValidateColor(zenith, nameof(zenith));
        ValidateColor(horizon, nameof(horizon));
        ValidateColor(ground, nameof(ground));
        if (!fogEnabled) return;
        if (!Finite(fogStartMeters) || fogStartMeters < 0 || !Finite(fogEndMeters) || fogEndMeters <= fogStartMeters ||
            !Finite(fogDensity) || fogDensity < 0 || fogDensity > 1)
            throw new ArgumentOutOfRangeException(nameof(fogEndMeters), "Fog params must be finite with end>start and density in [0,1].");
    }

    public static Rgb SampleGradient(Rgb zenith, Rgb horizon, Rgb ground, float elevation)
    {
        ValidateColor(zenith, nameof(zenith));
        ValidateColor(horizon, nameof(horizon));
        ValidateColor(ground, nameof(ground));
        if (!Finite(elevation) || elevation < -1 || elevation > 1)
            throw new ArgumentOutOfRangeException(nameof(elevation), "Elevation must be finite in [-1,1] (ground..zenith).");
        if (elevation >= 0) return Lerp(horizon, zenith, elevation);
        return Lerp(horizon, ground, -elevation);
    }

    public static Rgb Lerp(Rgb a, Rgb b, float t)
    {
        if (!Finite(t) || t < 0 || t > 1) throw new ArgumentOutOfRangeException(nameof(t));
        return new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
    }

    private static void ValidateColor(Rgb color, string name)
    {
        if (!Finite(color.R) || !Finite(color.G) || !Finite(color.B) ||
            color.R < 0 || color.G < 0 || color.B < 0 || color.R > 8 || color.G > 8 || color.B > 8)
            throw new ArgumentOutOfRangeException(name, "RGB must be finite and in [0,8] linear.");
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

/// <summary>Mirror of Presentation GeneratedReflectionProbe.Validate for offline Generation.Tests. Keep in sync.</summary>
internal static class GeneratedReflectionProbeMath
{
    public const float DefaultBoxSizeMeters = 12f;
    public const float DefaultIntensity = 1f;
    public const int DefaultResolution = 128;
    public const float ProductMapBoxSizeMeters = 48f;
    public const float ProductMapIntensity = 1.12f;

    public static void Validate(float boxSizeMeters, float intensity, int resolution)
    {
        if (!Finite(boxSizeMeters) || boxSizeMeters < 1f || boxSizeMeters > 64f)
            throw new ArgumentOutOfRangeException(nameof(boxSizeMeters), "Box size must be finite in [1,64] meters.");
        if (!Finite(intensity) || intensity < 0f || intensity > 8f)
            throw new ArgumentOutOfRangeException(nameof(intensity), "Intensity must be finite in [0,8].");
        if (resolution != 16 && resolution != 32 && resolution != 64 && resolution != 128 && resolution != 256)
            throw new ArgumentOutOfRangeException(nameof(resolution), "Resolution must be 16/32/64/128/256.");
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
