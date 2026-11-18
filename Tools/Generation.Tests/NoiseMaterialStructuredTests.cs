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
}
