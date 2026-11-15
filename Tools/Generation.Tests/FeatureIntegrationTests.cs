using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shooter.Content;
using Shooter.Contracts;
using Shooter.Generation;
using Xunit;

public sealed class FeatureIntegrationTests
{
    private static byte[] Source(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "FeatureRecipes", name + ".json"));
    [Fact]
    public void RegisteredSourcesEnterGenericCompilerWithExactValidatedIdentity()
    {
        foreach (string kind in new[] { "rifle", "pistol" })
        {
            var recipe = WeaponRecipe.Parse(Source(kind));
            var validated = new ValidatedRecipe(recipe.Document, recipe.CanonicalSource, new WeaponRecipeValidator());
            Assert.Equal(recipe.SourceHash, validated.SourceHash);
        }
        foreach (string family in new[] { "metal", "polymer", "concrete", "plaster", "asphalt", "soil", "fabric" })
        {
            var recipe = MaterialRecipe.Parse(Source(family));
            var validated = new ValidatedRecipe(recipe.Document, recipe.CanonicalSource, new MaterialRecipeValidator());
            Assert.Equal(recipe.SourceHash, validated.SourceHash);
        }
    }
    [Fact]
    public void GenericCompilerRejectsMismatchedWholeSourceAndValidatedDocument()
    {
        var metal = MaterialRecipe.Parse(Source("metal")); var polymer = MaterialRecipe.Parse(Source("polymer"));
        Assert.Throws<FormatException>(() => new ValidatedRecipe(polymer.Document, metal.CanonicalSource, new MaterialRecipeValidator()));
    }
    [Theory]
    [InlineData("rifle")]
    [InlineData("metal")]
    public void VersionMigrationFixturesRejectLegacyInsteadOfReinterpreting(string name)
    {
        if (name == "rifle")
        {
            byte[] before = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "FeatureFixtures", "rifle-1.0.1.json"));
            byte[] after = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "FeatureFixtures", "rifle-1.0.5.json"));
            Assert.Throws<FormatException>(() => WeaponRecipe.Parse(before));
            Assert.Throws<FormatException>(() => WeaponRecipe.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "FeatureFixtures", "rifle-1.0.0.json"))));
            Assert.Throws<FormatException>(() => WeaponRecipe.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "FeatureFixtures", "rifle-1.0.2.json"))));
            Assert.Throws<FormatException>(() => WeaponRecipe.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"FeatureFixtures","rifle-1.0.3.json"))));
            Assert.Throws<FormatException>(() => WeaponRecipe.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"FeatureFixtures","rifle-1.0.4.json"))));
            Assert.Equal("1.0.5", WeaponRecipe.Parse(after).Document.GeneratorVersion);
        }
        else
        {
            byte[] before = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "FeatureFixtures", name + "-1.0.0.json"));
            byte[] after = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "FeatureFixtures", name + "-1.0.1.json"));
            Assert.Throws<FormatException>(() => MaterialRecipe.Parse(before));
            Assert.Equal("1.0.1", MaterialRecipe.Parse(after).Document.GeneratorVersion);
        }
    }
    [Fact]
    public async Task ActualWeaponAndMaterialNodesPublishReproduceAndLeaseThroughFrozenCodec()
    {
        string root = Path.Combine(Path.GetTempPath(), "shooter-feature-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var hash = new ContentId(new string('1', 64)); var files = new CacheFiles(root, hash);
            var weapon = WeaponRecipe.Parse(Source("rifle")); var material = MaterialRecipe.Parse(Source("metal"));
            var weaponSource = new ValidatedRecipe(weapon.Document, weapon.CanonicalSource, new WeaponRecipeValidator());
            var nodes = new[] {
                new CompilationNode(weaponSource, "windows64", "Low", hash, new WeaponAssetGenerator()),
                new CompilationNode(weaponSource, "windows64", "gameplay", hash, new WeaponAssetGenerator()),
                new CompilationNode(new ValidatedRecipe(material.Document, material.CanonicalSource, new MaterialRecipeValidator()), "windows64", "Low", hash, new MaterialAssetGenerator(), new[] {"metal"}) };
            var compiler = new ContentCompiler(files);
            var cold = await compiler.CompileAsync(nodes, false, CancellationToken.None);
            Assert.True(cold.Error == ErrorCode.None, cold.Diagnostic); Assert.All(cold.Entries, e => Assert.False(e.Reused));
            var forced = await compiler.CompileAsync(nodes, true, CancellationToken.None);
            Assert.True(forced.Error == ErrorCode.None, forced.Diagnostic); Assert.All(forced.Entries, e => Assert.True(e.Reused));
            using var store = new FileContentStore(files);
            var mesh = await store.AcquireVariantAsync(nodes[0].Id, ContentKind.Mesh, "lod0", CancellationToken.None);
            var mask = await store.AcquireVariantAsync(nodes[2].Id, ContentKind.Material, "mask", CancellationToken.None);
            Assert.Equal(ErrorCode.None, mesh.Error); Assert.Equal(ErrorCode.None, mask.Error);
            using (mesh.Lease) using (mask.Lease)
            {
                Assert.Equal((byte)'S', mesh.Lease![0]); Assert.Equal((byte)'S', mask.Lease![0]);
                Assert.Equal(new[] { "metal" }, files.ReadManifest(nodes[2].Id).MaterialLayerIds);
            }
            Assert.Equal(0, store.ResidentBytes); Assert.Equal(0, store.ActiveLeases);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
