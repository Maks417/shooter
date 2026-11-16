using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Shooter.Contracts;
using Shooter.Generation;
using Xunit;

/// <summary>ADR-076: material.surface 1.1.0 samples FastNoiseLite through NoiseField; 1.0.1 stays the frozen default.</summary>
public sealed class MaterialNoiseVersionTests
{
    private static readonly string[] Families = { "metal", "polymer", "concrete", "plaster", "asphalt", "soil", "fabric" };

    private static MaterialRecipe Recipe(string family, string version, int resolution = 32) =>
        MaterialRecipe.Parse(Encoding.UTF8.GetBytes(
            "{\"schemaId\":\"shooter.recipe.material.v1\",\"recipeId\":\"materials/" + family + "\",\"generatorId\":\"material.surface\",\"generatorVersion\":\"" + version +
            "\",\"seed\":\"460072030\",\"parameters\":{\"family\":\"" + family + "\",\"resolution\":" + resolution.ToString(CultureInfo.InvariantCulture) +
            ",\"tileMm\":" + MaterialFamilyDefinition.For(family).RecommendedTileMm.ToString(CultureInfo.InvariantCulture) + "},\"dependencies\":[]}"));

    [Fact]
    public void BothVersionsParseAndKeepTheirVersionInCanonicalSource()
    {
        foreach (string version in new[] { MaterialRecipe.Version, MaterialRecipe.NoiseVersion })
        {
            var recipe = Recipe("concrete", version);
            Assert.Equal(version, recipe.GeneratorVersion);
            Assert.Contains("\"generatorVersion\":\"" + version + "\"", Encoding.UTF8.GetString(recipe.CanonicalSource));
            var roundTrip = new MaterialRecipeValidator().Validate(recipe.Document);
            Assert.True(roundTrip.Valid);
        }
        Assert.Throws<FormatException>(() => Recipe("concrete", "1.2.0"));
        Assert.Throws<FormatException>(() => Recipe("concrete", "1.0.0"));
    }

    [Fact]
    public void NoiseVersionIsDeterministicAndDistinctForEveryFamily()
    {
        foreach (string family in Families)
        {
            var first = MaterialBaker.Bake(Recipe(family, MaterialRecipe.NoiseVersion));
            var second = MaterialBaker.Bake(Recipe(family, MaterialRecipe.NoiseVersion));
            var legacy = MaterialBaker.Bake(Recipe(family, MaterialRecipe.Version));
            Assert.Equal(first.Albedo.MipBytes[0].ToArray(), second.Albedo.MipBytes[0].ToArray());
            Assert.Equal(first.Normal.MipBytes[0].ToArray(), second.Normal.MipBytes[0].ToArray());
            // Low-relief families can quantize to flat normals at 32 px, so compare all three planes together.
            Assert.NotEqual(Planes(legacy), Planes(first));
        }
    }

    private static byte[] Planes(MaterialBake bake) =>
        bake.Albedo.MipBytes[0].ToArray().Concat(bake.Normal.MipBytes[0].ToArray()).Concat(bake.Mask.MipBytes[0].ToArray()).ToArray();

    [Fact]
    public void NoiseVersionTilesAndStaysInPhysicalBounds()
    {
        foreach (string family in Families)
        {
            var recipe = Recipe(family, MaterialRecipe.NoiseVersion);
            for (int i = 0; i <= 8; i++)
            {
                double t = i / 8.0;
                MaterialSample edge = MaterialBaker.Sample(recipe, 0, t), wrapped = MaterialBaker.Sample(recipe, 1, t);
                Assert.Equal(edge.HeightMeters, wrapped.HeightMeters);
                Assert.Equal(edge.Roughness, wrapped.Roughness);
                Assert.InRange(edge.Roughness, .08f, .98f);
                Assert.InRange(edge.Occlusion, .65f, 1f);
                Assert.InRange(edge.LinearAlbedo.X, .005f, .9f);
            }
        }
    }

    [Fact]
    public void GeneratorFingerprintFollowsTheRecipeVersion()
    {
        var generator = new MaterialAssetGenerator();
        var toolchain = new ContentId(new string('1', 64));
        GenerationResult legacy = generator.Generate(new GenerationRequest(Recipe("metal", MaterialRecipe.Version).Document, "windows64", "Low", toolchain), CancellationToken.None);
        GenerationResult noise = generator.Generate(new GenerationRequest(Recipe("metal", MaterialRecipe.NoiseVersion).Document, "windows64", "Low", toolchain), CancellationToken.None);
        Assert.Equal(ErrorCode.None, legacy.Error);
        Assert.Equal(ErrorCode.None, noise.Error);
        Assert.NotEqual(legacy.Artifacts.Select(a => a.Sha256.ToString()), noise.Artifacts.Select(a => a.Sha256.ToString()));
    }
}
