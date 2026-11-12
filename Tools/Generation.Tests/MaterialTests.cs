using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Shooter.Contracts;
using Shooter.Generation;
using Xunit;

public sealed class MaterialTests
{
    private static MaterialRecipe Recipe(string family = "metal", ulong seed = 460072030, int resolution = 32, int? tileMm = null)
        => MaterialRecipe.Parse(Encoding.UTF8.GetBytes(Source(family, seed.ToString(CultureInfo.InvariantCulture), resolution, tileMm ?? MaterialFamilyDefinition.For(family).RecommendedTileMm)));
    private static string Source(string family = "metal", string seed = "460072030", int resolution = 32, int tileMm = 1000) =>
        "{\"schemaId\":\"shooter.recipe.material.v1\",\"recipeId\":\"materials/" + family + "\",\"generatorId\":\"material.surface\",\"generatorVersion\":\"1.0.0\",\"seed\":\"" + seed + "\",\"parameters\":{\"family\":\"" + family + "\",\"resolution\":" + resolution.ToString(CultureInfo.InvariantCulture) + ",\"tileMm\":" + tileMm.ToString(CultureInfo.InvariantCulture) + "},\"dependencies\":[]}";
    public static IEnumerable<object[]> Families => new[] { "metal", "polymer", "concrete", "plaster", "asphalt", "soil", "fabric" }.Select(name => new object[] { name });
    private static byte[] Bytes(FrozenList<byte> bytes) => bytes.ToArray();

    [Theory]
    [MemberData(nameof(Families))]
    public void AllFamiliesBakeCompleteValidPlanesWithPbrBounds(string family)
    {
        var recipe = Recipe(family); var bake = MaterialBaker.Bake(recipe);
        Assert.Equal("rgba8-srgb", bake.Albedo.ChannelFormat);
        Assert.Equal("rgba8-linear", bake.Normal.ChannelFormat); Assert.Equal("rgba8-linear", bake.Mask.ChannelFormat);
        Assert.Equal(32, bake.Albedo.Width); Assert.Equal(32, bake.Albedo.Height);
        foreach (var plane in new[] { bake.Albedo, bake.Normal, bake.Mask })
        {
            Assert.Equal(6, plane.MipBytes.Count); int width = 32;
            foreach (var mip in plane.MipBytes)
            {
                Assert.Equal(width * width * 4, mip.Count);
                for (int i = 3; i < mip.Count; i += 4) Assert.Equal(255, mip[i]);
                width = Math.Max(1, width / 2);
            }
            Assert.Equal(4, plane.MipBytes.Last().Count);
            Assert.Equal(plane.Width, GeometryCodec.DecodeMaterial(GeometryCodec.EncodeMaterial(plane)).Width);
        }
        double metal = 0, rough = 0, luminance = 0;
        var mask = bake.Mask.MipBytes[0]; var albedo = bake.Albedo.MipBytes[0];
        for (int i = 0; i < mask.Count; i += 4)
        {
            metal += mask[i] / 255.0; rough += mask[i + 2] / 255.0;
            luminance += (albedo[i] + albedo[i + 1] + albedo[i + 2]) / (3 * 255.0);
            Assert.InRange(mask[i + 1], (byte)165, (byte)255);
        }
        int pixels = mask.Count / 4; metal /= pixels; rough /= pixels; luminance /= pixels;
        if (family == "metal") Assert.InRange(metal, .01, .45); else Assert.Equal(0, metal);
        Assert.InRange(rough, .3, .98); Assert.InRange(luminance, .08, .85);
        foreach (var mip in bake.Normal.MipBytes)
            for (int i = 0; i < mip.Count; i += 4)
            {
                double x = mip[i] / 127.5 - 1, y = mip[i + 1] / 127.5 - 1, z = mip[i + 2] / 127.5 - 1;
                Assert.InRange(Math.Sqrt(x * x + y * y + z * z), .988, 1.012); Assert.True(z > 0);
            }
        Assert.Equal(16380, bake.Bytes);
    }
    [Theory]
    [MemberData(nameof(Families))]
    public void AnalyticFieldsArePeriodicIncludingHeightAndMaterialMasks(string family)
    {
        var recipe = Recipe(family);
        foreach (double u in new[] { -.125, 0, .125, .5, 1.0 }) foreach (double v in new[] { -.25, 0, .375, .875 })
        {
            var a = MaterialBaker.Sample(recipe, u, v); var b = MaterialBaker.Sample(recipe, u + 1, v - 2);
            Assert.Equal(a.HeightMeters, b.HeightMeters); Assert.Equal(a.Roughness, b.Roughness); Assert.Equal(a.Metallic, b.Metallic);
            Assert.Equal(a.LinearAlbedo.X, b.LinearAlbedo.X); Assert.Equal(a.LinearAlbedo.Y, b.LinearAlbedo.Y); Assert.Equal(a.LinearAlbedo.Z, b.LinearAlbedo.Z);
        }
        // Seam derivatives are continuous in the underlying periodic fields, not equal first/last texel samples.
        const double epsilon = 1e-6;
        double derivativeLeft = (MaterialBaker.Sample(recipe, 0, .371).HeightMeters - MaterialBaker.Sample(recipe, -epsilon, .371).HeightMeters) / epsilon;
        double derivativeRight = (MaterialBaker.Sample(recipe, epsilon, .371).HeightMeters - MaterialBaker.Sample(recipe, 0, .371).HeightMeters) / epsilon;
        Assert.InRange(Math.Abs(derivativeLeft - derivativeRight), 0, .002);
    }
    [Theory]
    [InlineData(0UL)] [InlineData(42UL)] [InlineData(ulong.MaxValue)]
    public void RepeatRequestsProduceIdenticalPayloadsAtBoundarySeeds(ulong seed)
    {
        var recipe = Recipe("fabric", seed); var generator = new MaterialAssetGenerator();
        GenerationResult Generate(string profile) => generator.Generate(new GenerationRequest(recipe.Document, "windows64", profile, new ContentId(new string('1', 64))), CancellationToken.None);
        var first = Generate("Low"); var second = Generate("Low"); var high = Generate("High");
        Assert.Equal(ErrorCode.None, first.Error); Assert.Equal(3, first.Artifacts.Count);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(first.Artifacts[i].Sha256, second.Artifacts[i].Sha256);
            Assert.Equal(first.Artifacts[i].Sha256, high.Artifacts[i].Sha256); // Resolution is explicit source input, never silently changed by quality.
            Assert.Equal(first.Artifacts[i].Payload.ToArray(), second.Artifacts[i].Payload.ToArray());
            byte[] body = ArtifactCodec.Decode(first.Artifacts[i].Payload, ContentKind.Material, GeneratorFingerprint.Compute(MaterialRecipe.Generator, MaterialRecipe.Version), new ContentId(new string('1', 64)));
            Assert.Equal(32, GeometryCodec.DecodeMaterial(body).Width);
        }
    }
    [Fact]
    public void AlbedoMipsAverageLinearLightAndMaskMipsAverageChannels()
    {
        byte[] blackAndWhite = { 0,0,0,255, 255,255,255,255, 0,0,0,255, 255,255,255,255 };
        var albedo = MaterialBaker.BuildPlane(2, blackAndWhite, "albedo"); var mask = MaterialBaker.BuildPlane(2, blackAndWhite, "mask");
        Assert.Equal(new byte[] { 188,188,188,255 }, Bytes(albedo.MipBytes[1]));
        Assert.Equal(new byte[] { 128,128,128,255 }, Bytes(mask.MipBytes[1]));
    }
    [Fact]
    public void TangentNormalsUseNegativeHeightGradientAndNormalizeFilteredDirections()
    {
        var flat = MaterialBaker.TangentNormal(0, 0); Assert.Equal(0, flat.X); Assert.Equal(0, flat.Y); Assert.Equal(1, flat.Z);
        var upU = MaterialBaker.TangentNormal(1, 0); Assert.True(upU.X < 0); Assert.Equal(0, upU.Y); Assert.InRange(upU.Z, .70710f, .70711f);
        var upV = MaterialBaker.TangentNormal(0, 1); Assert.Equal(0, upV.X); Assert.True(upV.Y < 0);
        byte[] symmetricNormals = { 218,128,218,255, 37,128,218,255, 218,128,218,255, 37,128,218,255 };
        var plane = MaterialBaker.BuildPlane(2, symmetricNormals, "normal"); var mip = plane.MipBytes[1];
        Assert.InRange(mip[0], (byte)127, (byte)128); Assert.InRange(mip[1], (byte)127, (byte)129); Assert.Equal(255, mip[2]);
        Assert.Throws<ArgumentException>(() => MaterialBaker.TangentNormal(double.NaN, 0));
    }
    [Fact]
    public void LowerResolutionBakesAreFilteredMipsOfTheSamePhysicalField()
    {
        var low = MaterialBaker.Bake(Recipe("fabric", resolution: 32)); var full = MaterialBaker.Bake(Recipe("fabric", resolution: 256));
        Assert.Equal(Bytes(full.Albedo.MipBytes[3]), Bytes(low.Albedo.MipBytes[0]));
        Assert.Equal(Bytes(full.Normal.MipBytes[3]), Bytes(low.Normal.MipBytes[0]));
        Assert.Equal(Bytes(full.Mask.MipBytes[3]), Bytes(low.Mask.MipBytes[0]));
    }
    [Fact]
    public void SevenFamiliesHaveDistinctSemanticsAndSeededVariation()
    {
        var fingerprints = new HashSet<ContentId>();
        foreach (object[] item in Families)
        {
            string family = (string)item[0];
            fingerprints.Add(ContentIdentity.Hash(GeometryCodec.EncodeMaterial(MaterialBaker.Bake(Recipe(family)).Albedo)));
        }
        Assert.Equal(7, fingerprints.Count);
        var a = MaterialBaker.Bake(Recipe("concrete", 0)); var b = MaterialBaker.Bake(Recipe("concrete", 42));
        Assert.NotEqual(ContentIdentity.Hash(GeometryCodec.EncodeMaterial(a.Albedo)), ContentIdentity.Hash(GeometryCodec.EncodeMaterial(b.Albedo)));
    }
    [Theory]
    [InlineData("\"family\":\"metal\"", "\"family\":\"wood\"")]
    [InlineData("\"resolution\":32", "\"resolution\":1024")]
    [InlineData("\"resolution\":32", "\"resolution\":33")]
    [InlineData("\"tileMm\":1000", "\"tileMm\":0")]
    [InlineData("\"tileMm\":1000", "\"tileMm\":8001")]
    [InlineData("\"seed\":\"460072030\"", "\"seed\":\"18446744073709551616\"")]
    [InlineData("\"seed\":\"460072030\"", "\"seed\":\"01\"")]
    [InlineData("shooter.recipe.material.v1", "shooter.recipe.material.v2")]
    [InlineData("material.surface", "material.other")]
    [InlineData("\"generatorVersion\":\"1.0.0\"", "\"generatorVersion\":\"2.0.0\"")]
    public void RecipeRejectsUnknownFamiliesRangesSeedsAndVersions(string oldValue, string newValue)
        => Assert.Throws<FormatException>(() => MaterialRecipe.Parse(Encoding.UTF8.GetBytes(Source().Replace(oldValue, newValue))));
    [Fact]
    public void RecipeRejectsAdditionalMissingDuplicateAndDependencyFields()
    {
        Assert.Throws<FormatException>(() => MaterialRecipe.Parse(Encoding.UTF8.GetBytes(Source().Replace("\"family\":\"metal\"", "\"family\":\"metal\",\"unknown\":1"))));
        Assert.Throws<FormatException>(() => MaterialRecipe.Parse(Encoding.UTF8.GetBytes(Source().Replace("\"family\":\"metal\",", ""))));
        Assert.Throws<FormatException>(() => MaterialRecipe.Parse(Encoding.UTF8.GetBytes(Source().Replace("\"family\":\"metal\"", "\"family\":\"metal\",\"family\":\"metal\""))));
        Assert.Throws<FormatException>(() => MaterialRecipe.Parse(Encoding.UTF8.GetBytes(Source().Replace("\"dependencies\":[]", "\"dependencies\":[\"" + new string('0', 64) + "\"]"))));
    }
    [Fact]
    public void ValidationAndGeneratorRejectMalformedOrCancelledRequestsWithoutArtifacts()
    {
        var recipe = Recipe(); Assert.True(new MaterialRecipeValidator().Validate(recipe.Document).Valid);
        byte[] leaked = recipe.CanonicalSource; leaked[0] = 0; Assert.NotEqual(0, recipe.CanonicalSource[0]);
        var generator = new MaterialAssetGenerator();
        var request = new GenerationRequest(recipe.Document, "windows64", "Low", default);
        var cancelled = generator.Generate(request, new CancellationToken(true));
        Assert.Equal(ErrorCode.Cancelled, cancelled.Error); Assert.Empty(cancelled.Artifacts);
        Assert.Throws<OperationCanceledException>(() => MaterialBaker.Bake(recipe, new CancellationToken(true)));
        var gameplay = generator.Generate(new GenerationRequest(recipe.Document, "windows64", "gameplay", default), CancellationToken.None);
        Assert.Equal(ErrorCode.InvalidInput, gameplay.Error); Assert.Empty(gameplay.Artifacts);
        var invalid = new RecipeDocument(recipe.Document.RecipeId, MaterialRecipe.Schema, MaterialRecipe.Generator, MaterialRecipe.Version, "0", Encoding.UTF8.GetBytes("{\"family\":\"metal\",\"resolution\":1024,\"tileMm\":1000}"), Array.Empty<ContentId>());
        Assert.False(new MaterialRecipeValidator().Validate(invalid).Valid);
        Assert.Equal(ErrorCode.InvalidInput, generator.Generate(new GenerationRequest(invalid, "windows64", "Low", default), CancellationToken.None).Error);
    }
}
