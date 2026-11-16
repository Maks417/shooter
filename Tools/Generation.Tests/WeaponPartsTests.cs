using System;
using System.Linq;
using System.Text;
using System.Threading;
using Shooter.Contracts;
using Shooter.Generation;
using Xunit;

/// <summary>ADR-082: geometry.weapon 1.1.0 parametric rifle parts.</summary>
public sealed class WeaponPartsTests
{
    private const string Standard = "{\"barrelFrontMm\":640,\"frontSightZMm\":530,\"gasBlockZMm\":480,\"gasTubeLengthMm\":120,\"gasTubeZMm\":400,\"handguardFrontMm\":485,\"optic\":\"compact\",\"stock\":\"fixed\",\"topRailTeeth\":15}";
    private const string Carbine = "{\"barrelFrontMm\":520,\"frontSightZMm\":400,\"gasBlockZMm\":360,\"gasTubeLengthMm\":80,\"gasTubeZMm\":300,\"handguardFrontMm\":380,\"optic\":\"compact\",\"stock\":\"fixed\",\"topRailTeeth\":10}";

    private static WeaponRecipe Legacy(string kind, string silhouette) => WeaponRecipe.Parse(Encoding.ASCII.GetBytes(
        "{\"schemaId\":\"shooter.recipe.weapon.v1\",\"recipeId\":\"weapons/test\",\"generatorId\":\"geometry.weapon\",\"generatorVersion\":\"1.0.5\",\"seed\":\"460072030\",\"parameters\":{\"kind\":\"" + kind + "\",\"silhouette\":\"" + silhouette + "\"},\"dependencies\":[]}"));

    private static WeaponRecipe Parts(string kind, string parts) => WeaponRecipe.Parse(Encoding.ASCII.GetBytes(
        "{\"schemaId\":\"shooter.recipe.weapon.v1\",\"recipeId\":\"weapons/test\",\"generatorId\":\"geometry.weapon\",\"generatorVersion\":\"1.1.0\",\"seed\":\"460072030\",\"parameters\":{\"kind\":\"" + kind + "\",\"parts\":" + parts + "},\"dependencies\":[]}"));

    private static string With(string parts, string key, string value)
    {
        int start = parts.IndexOf("\"" + key + "\":", StringComparison.Ordinal) + key.Length + 3;
        int end = parts.IndexOfAny(new[] { ',', '}' }, start);
        return parts.Substring(0, start) + value + parts.Substring(end);
    }

    private static void AssertSameMeshes(WeaponBuild expected, WeaponBuild actual)
    {
        for (int lod = 0; lod < 4; lod++)
            Assert.Equal(GeometryCodec.EncodeMesh(expected.Lods[lod]), GeometryCodec.EncodeMesh(actual.Lods[lod]));
        Assert.Equal(GeometryCodec.EncodeMesh(expected.Magazine), GeometryCodec.EncodeMesh(actual.Magazine));
        Assert.Equal(GeometryCodec.EncodeMesh(expected.CyclePart), GeometryCodec.EncodeMesh(actual.CyclePart));
        Assert.Equal(GeometryCodec.EncodeCollision(expected.Collision), GeometryCodec.EncodeCollision(actual.Collision));
        Assert.Equal(expected.Sockets.Select(s => s.Id + s.Position.X + s.Position.Y + s.Position.Z),
            actual.Sockets.Select(s => s.Id + s.Position.X + s.Position.Y + s.Position.Z));
    }

    [Fact]
    public void PartPresetsRebuildTheFrozenSilhouettesByteForByte()
    {
        AssertSameMeshes(WeaponGeometry.Build(Legacy("rifle", "standard")), WeaponGeometry.Build(Parts("rifle", Standard)));
        AssertSameMeshes(WeaponGeometry.Build(Legacy("rifle", "carbine")), WeaponGeometry.Build(Parts("rifle", Carbine)));
        AssertSameMeshes(WeaponGeometry.Build(Legacy("pistol", "standard")), WeaponGeometry.Build(Parts("pistol", "{}")));
    }

    [Fact]
    public void LegacySilhouettesResolveToTheNamedPresets()
    {
        Assert.Equal(WeaponParts.Standard.ToCanonicalJson(), Legacy("rifle", "standard").Parts!.ToCanonicalJson());
        Assert.Equal(Carbine, Legacy("rifle", "carbine").Parts!.ToCanonicalJson());
        Assert.Equal(Standard, WeaponParts.Standard.ToCanonicalJson());
        Assert.Null(Legacy("pistol", "standard").Parts);
        Assert.Equal("1.0.5", Legacy("rifle", "standard").Version);
        Assert.Equal("1.1.0", Parts("rifle", Standard).Version);
    }

    [Fact]
    public void StocklessOpticlessShortRifleIsANewSilhouetteInsideTheEnvelope()
    {
        string compact = With(With(With(With(With(With(With(With(Carbine,
            "stock", "\"none\""), "optic", "\"none\""), "handguardFrontMm", "320"), "barrelFrontMm", "430"),
            "topRailTeeth", "8"), "gasBlockZMm", "320"), "gasTubeZMm", "280"), "frontSightZMm", "330");
        compact = With(compact, "gasTubeLengthMm", "60");
        var build = WeaponGeometry.Build(Parts("rifle", compact));
        var standard = WeaponGeometry.Build(Parts("rifle", Standard));
        for (int lod = 0; lod < 4; lod++)
            Assert.True(build.Lods[lod].Positions.Count < standard.Lods[lod].Positions.Count, "lod" + lod);
        float minZ = build.Lods[0].Positions.Min(p => p.Z), maxZ = build.Lods[0].Positions.Max(p => p.Z);
        Assert.True(minZ > -.30f, "no stock behind the receiver: " + minZ);
        Assert.True(maxZ <= .431f, "barrel ends at 430 mm: " + maxZ);
        // Gameplay-facing data stays on the frozen numeric baseline (ADR-072).
        Assert.Equal(GeometryCodec.EncodeCollision(standard.Collision), GeometryCodec.EncodeCollision(build.Collision));
    }

    [Theory]
    [InlineData("barrelFrontMm", "400", "barrelFrontMm")]          // shorter than handguard + 40
    [InlineData("handguardFrontMm", "600", "handguardFrontMm")]
    [InlineData("topRailTeeth", "20", "topRailTeeth")]
    [InlineData("gasTubeZMm", "470", "gasTubeZMm")]
    [InlineData("frontSightZMm", "700", "frontSightZMm")]
    [InlineData("stock", "\"folding\"", "stock")]
    [InlineData("optic", "\"scope\"", "optic")]
    public void OutOfRangeOrUnknownPartsAreRejected(string key, string value, string expected)
    {
        var error = Assert.Throws<FormatException>(() => Parts("rifle", With(Standard, key, value)));
        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public void ShapeErrorsAreRejected()
    {
        Assert.Throws<FormatException>(() => Parts("rifle", Standard.Replace("}", ",\"extraMm\":1}")));
        Assert.Throws<FormatException>(() => Parts("rifle", "{}"));
        Assert.Throws<FormatException>(() => Parts("pistol", Standard));
        Assert.Throws<FormatException>(() => Parts("shotgun", Standard));
        Assert.Throws<FormatException>(() => WeaponRecipe.Parse(Encoding.ASCII.GetBytes(
            "{\"schemaId\":\"shooter.recipe.weapon.v1\",\"recipeId\":\"weapons/test\",\"generatorId\":\"geometry.weapon\",\"generatorVersion\":\"1.1.0\",\"seed\":\"1\",\"parameters\":{\"kind\":\"rifle\",\"silhouette\":\"carbine\"},\"dependencies\":[]}")));
        Assert.Throws<FormatException>(() => WeaponRecipe.Parse(Encoding.ASCII.GetBytes(
            "{\"schemaId\":\"shooter.recipe.weapon.v1\",\"recipeId\":\"weapons/test\",\"generatorId\":\"geometry.weapon\",\"generatorVersion\":\"1.2.0\",\"seed\":\"1\",\"parameters\":{\"kind\":\"pistol\",\"parts\":{}},\"dependencies\":[]}")));
    }

    [Fact]
    public void KeyOrderDoesNotChangeTheRecipeIdentity()
    {
        string shuffled = "{\"topRailTeeth\":15,\"stock\":\"fixed\",\"optic\":\"compact\",\"handguardFrontMm\":485,\"gasTubeZMm\":400,\"gasTubeLengthMm\":120,\"gasBlockZMm\":480,\"frontSightZMm\":530,\"barrelFrontMm\":640}";
        var a = Parts("rifle", Standard);
        var b = Parts("rifle", shuffled);
        Assert.Equal(a.SourceHash, b.SourceHash);
        Assert.Equal(a.Document.Parameters.ToArray(), b.Document.Parameters.ToArray());
    }

    [Fact]
    public void AssetGeneratorCompilesPartsRecipesUnderTheirOwnFingerprint()
    {
        var recipe = Parts("rifle", Carbine);
        var toolchain = ContentIdentity.Hash(Encoding.ASCII.GetBytes("weapon-parts-tests"));
        var result = new WeaponAssetGenerator().Generate(new GenerationRequest(recipe.Document, "windows64", "High", toolchain), CancellationToken.None);
        Assert.Equal(ErrorCode.None, result.Error);
        var fingerprint = GeneratorFingerprint.Compute("geometry.weapon", "1.1.0");
        var lod0 = result.Artifacts.First(a => a.Kind == ContentKind.Mesh);
        var mesh = GeometryCodec.DecodeMesh(ArtifactCodec.Decode(lod0.Payload.ToArray(), ContentKind.Mesh, fingerprint, toolchain));
        Assert.Equal(GeometryCodec.EncodeMesh(WeaponGeometry.Build(Legacy("rifle", "carbine")).Lods[0]), GeometryCodec.EncodeMesh(mesh));
    }
}
