using System;
using System.Linq;
using Shooter.Contracts;
using Shooter.Generation;
using Xunit;

/// <summary>ADR-106 material.noise 1.1.0: structured 4 m surfaces with Toksvig roughness mips.</summary>
public sealed class NoiseMaterialStructuredTests
{
    [Theory]
    [InlineData("concrete", 256)]
    [InlineData("asphalt", 512)]
    [InlineData("corrugated-metal", 512)]
    public void Structured_bake_is_deterministic_complete_and_raises_far_mip_roughness(string role, int resolution)
    {
        var recipe = new NoiseMaterialRecipe(role, resolution, 460072030UL, 4000, NoiseMaterialRecipe.StructuredVersion);
        var a = NoiseMaterialBaker.Bake(recipe); var b = NoiseMaterialBaker.Bake(recipe);
        Assert.Equal(GeometryCodec.EncodeMaterial(a.Mask), GeometryCodec.EncodeMaterial(b.Mask));
        Assert.Equal(GeometryCodec.EncodeMaterial(a.Albedo), GeometryCodec.EncodeMaterial(b.Albedo));
        Assert.Equal(resolution, a.Albedo.Width);
        Assert.Equal((int)Math.Log2(resolution) + 1, a.Mask.MipBytes.Count);
        double MeanRoughness(int mip) { var m = a.Mask.MipBytes[mip]; double sum = 0; for (int i = 2; i < m.Count; i += 4) sum += m[i]; return sum / (m.Count / 4); }
        // Specular anti-aliasing: averaged-out relief must not leave far mips smoother than the base level.
        Assert.True(MeanRoughness(4) >= MeanRoughness(0) - 1, role + " far mip lost roughness");
    }

    [Fact]
    public void Versions_keep_their_own_resolution_sets()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NoiseMaterialRecipe("concrete", 512, 1, 2000));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NoiseMaterialRecipe("concrete", 128, 1, 4000, NoiseMaterialRecipe.StructuredVersion));
        var legacy = new NoiseMaterialRecipe("concrete", 128, 460072030UL);
        Assert.Equal(NoiseMaterialRecipe.Version, legacy.Document.GeneratorVersion);
        var structured = NoiseMaterialRecipe.FromDocument(new NoiseMaterialRecipe("gravel", 256, 7, 4000, NoiseMaterialRecipe.StructuredVersion).Document);
        Assert.Equal(NoiseMaterialRecipe.StructuredVersion, structured.GeneratorVersion);
    }

    /// <summary>ADR-116 material.noise 1.2.0: realistic albedo levels and visible hue so maps no longer read grey.</summary>
    [Theory]
    [InlineData("concrete", .25, .45, .010)]
    [InlineData("plaster", .35, .60, .040)]
    [InlineData("painted-steel", .12, .32, .040)]
    [InlineData("corrugated-metal", .20, .45, .004)]
    [InlineData("asphalt", .045, .11, .000)]
    [InlineData("gravel", .12, .30, .030)]
    public void Realistic_bake_has_plausible_albedo_and_colour(string role, double minLuma, double maxLuma, double minChroma)
    {
        var recipe = new NoiseMaterialRecipe(role, 256, 460072030UL, 4000, NoiseMaterialRecipe.RealisticVersion);
        var a = NoiseMaterialBaker.Bake(recipe); var again = NoiseMaterialBaker.Bake(recipe);
        Assert.Equal(GeometryCodec.EncodeMaterial(a.Albedo), GeometryCodec.EncodeMaterial(again.Albedo));
        Assert.Equal(GeometryCodec.EncodeMaterial(a.Mask), GeometryCodec.EncodeMaterial(again.Mask));
        static double Linear(byte s) { double c = s / 255.0; return c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4); }
        var bytes = a.Albedo.MipBytes[0]; double luma = 0, chroma = 0; int texels = bytes.Count / 4;
        for (int i = 0; i < bytes.Count; i += 4)
        {
            double r = Linear(bytes[i]), g = Linear(bytes[i + 1]), b = Linear(bytes[i + 2]);
            luma += .2126 * r + .7152 * g + .0722 * b; chroma += Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
        }
        luma /= texels; chroma /= texels;
        Assert.InRange(luma, minLuma, maxLuma);
        Assert.True(chroma >= minChroma, role + " chroma " + chroma);
    }

    [Fact]
    public void Realistic_version_round_trips_and_differs_from_structured()
    {
        var doc = new NoiseMaterialRecipe("concrete", 256, 7, 4000, NoiseMaterialRecipe.RealisticVersion).Document;
        Assert.Equal(NoiseMaterialRecipe.RealisticVersion, NoiseMaterialRecipe.FromDocument(doc).GeneratorVersion);
        var structured = NoiseMaterialBaker.Bake(new NoiseMaterialRecipe("concrete", 256, 7, 4000, NoiseMaterialRecipe.StructuredVersion));
        var realistic = NoiseMaterialBaker.Bake(new NoiseMaterialRecipe("concrete", 256, 7, 4000, NoiseMaterialRecipe.RealisticVersion));
        Assert.NotEqual(GeometryCodec.EncodeMaterial(structured.Albedo), GeometryCodec.EncodeMaterial(realistic.Albedo));
        Assert.Equal(GeometryCodec.EncodeMaterial(structured.Normal), GeometryCodec.EncodeMaterial(realistic.Normal));
    }
}
