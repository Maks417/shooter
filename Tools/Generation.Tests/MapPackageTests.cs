using System;
using System.IO;
using System.Linq;
using System.Threading;
using Shooter.Content.Maps;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Shooter.World.Navigation;
using Xunit;

[Collection("NativeNavProvider")]
public sealed class MapPackageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "shooter-map-pkg-" + Guid.NewGuid().ToString("N"));
    private readonly ContentId toolchain = ContentIdentity.Hash(System.Text.Encoding.ASCII.GetBytes("map-package-test-toolchain"));

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    [Fact]
    public void MapRecipesParseApprovedSeedsAndRejectUnknown()
    {
        var compound = MapRecipe.Parse(MapRecipe.CreateSource("training-compound"));
        Assert.Equal("training-compound", compound.MapId);
        Assert.Equal(460072030UL, compound.Document.SeedValue);
        var depot = MapRecipe.Parse(MapRecipe.CreateSource("depot-yard"));
        Assert.Equal("depot-yard", depot.MapId);
        Assert.Equal(460072031UL, depot.Document.SeedValue);
        Assert.Equal(460072031UL, MapRecipe.ApprovedSeed("depot-yard"));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapRecipe.ApprovedSeed("unknown"));
    }

    [Fact]
    public void PackageRootsAreByteHashesNotLabelHashes()
    {
        var package = MapPackageBuilder.Build("training-compound", "gameplay", toolchain);
        Assert.Equal(ContentIdentity.Hash(package.PackageRootBytes), package.PackageRootHash);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var label = new ContentId(sha.ComputeHash(System.Text.Encoding.ASCII.GetBytes("visual.low.v1")));
        Assert.NotEqual(label, package.PackageRootHash);
        Assert.Contains(package.Dependencies, d => d.Role == "collision");
        Assert.Contains(package.Dependencies, d => d.Role == "navigation");
        Assert.Contains(package.Dependencies, d => d.Role == "metadata");
        Assert.DoesNotContain(package.Dependencies, d => d.Role == "mesh");
    }

    [Fact]
    public void VisualPackageIncludesRequiredClosureRoles()
    {
        var package = MapPackageBuilder.Build("depot-yard", "low", toolchain);
        Assert.Equal(460072031UL, package.Seed);
        foreach (string role in new[] { "collision", "navigation", "metadata", "mesh", "material", "skeleton", "shader", "font" })
            Assert.Contains(package.Dependencies, d => d.Role == role);
    }

    [Fact]
    public void NavAdapterIsUnityBakeV1AndSettingsHashMatchesSliceDefault()
    {
        var package = MapPackageBuilder.Build("training-compound", "gameplay", toolchain);
        var fingerprint = GeneratorFingerprint.Compute(MapRecipe.CompoundGenerator, MapRecipe.GeneratorVersion);
        var body = ArtifactCodec.Decode(package.NavigationSgaf, ContentKind.Navigation, fingerprint, toolchain);
        var nav = GeometryCodec.DecodeNavigation(body);
        Assert.Equal(UnityNavMeshBakeCodec.AdapterFormat, nav.Format);
        Assert.Equal(UnityNavMeshBakeCodec.AdapterVersion, nav.Version);
        Assert.Equal(NavSettings.SliceDefault.Hash(), nav.SettingsHash);
        Assert.Equal(NavSettings.SliceDefault.Hash(), package.NavSettingsHash);
        var bake = UnityNavMeshBakeCodec.Decode(nav.Blob);
        Assert.True(bake.Polygons.Count > 0);
        Assert.Equal(NavBakePublication.SoftwarePendingProvenance, package.NavBakeProvenance);
    }

    [Fact]
    public void ColdWarmForcedPublicationReproduceIdenticalBytes()
    {
        var cold = MapPackagePublisher.Publish(root, toolchain, new[] { "training-compound", "depot-yard" }, new[] { "gameplay" }, false, CancellationToken.None);
        Assert.Equal(ErrorCode.None, cold.Error);
        Assert.False(cold.Reused);
        Assert.Equal(2, cold.Packages.Count);
        byte[] firstRoot = File.ReadAllBytes(Path.Combine(MapPackagePublisher.PackageDirectory(root, cold.Packages[0].PackageRootHash), "package.json"));

        var warm = MapPackagePublisher.Publish(root, toolchain, new[] { "training-compound", "depot-yard" }, new[] { "gameplay" }, false, CancellationToken.None);
        Assert.Equal(ErrorCode.None, warm.Error);
        Assert.True(warm.Reused);
        Assert.Equal(firstRoot, File.ReadAllBytes(Path.Combine(MapPackagePublisher.PackageDirectory(root, warm.Packages[0].PackageRootHash), "package.json")));

        var force = MapPackagePublisher.Publish(root, toolchain, new[] { "training-compound", "depot-yard" }, new[] { "gameplay" }, true, CancellationToken.None);
        Assert.Equal(ErrorCode.None, force.Error);
        Assert.Equal(firstRoot, File.ReadAllBytes(Path.Combine(MapPackagePublisher.PackageDirectory(root, force.Packages[0].PackageRootHash), "package.json")));
    }

    [Fact]
    public void CancelAndCorruptDoNotSilentSubstitute()
    {
        Assert.Equal(ErrorCode.None, MapPackagePublisher.Publish(root, toolchain, new[] { "training-compound" }, new[] { "gameplay" }, false, CancellationToken.None).Error);
        var package = MapPackageBuilder.Build("training-compound", "gameplay", toolchain);
        string dir = MapPackagePublisher.PackageDirectory(root, package.PackageRootHash);
        string payload = Path.Combine(dir, "collision-gameplay.bin");
        byte[] corrupt = { 9, 9, 9 };
        File.WriteAllBytes(payload, corrupt);
        Assert.False(MapPackagePublisher.TryOpenPublished(dir, package.PackageRootHash, out _, out string diagnostic));
        Assert.Contains("hash", diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(corrupt, File.ReadAllBytes(payload));

        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        string other = Path.Combine(root, "other");
        var cancelled = MapPackagePublisher.Publish(other, toolchain, new[] { "depot-yard" }, new[] { "gameplay" }, false, cancel.Token);
        Assert.Equal(ErrorCode.Cancelled, cancelled.Error);
        Assert.False(Directory.Exists(MapPackagePublisher.PackageDirectory(other, MapPackageBuilder.Build("depot-yard", "gameplay", toolchain).PackageRootHash)));
    }

    [Fact]
    public void DepotSeedFortyTwoIsNotCatalogIdentity()
    {
        var catalogSeed = MapRecipe.ApprovedSeed("depot-yard");
        Assert.Equal(460072031UL, catalogSeed);
        Assert.NotEqual(42UL, catalogSeed);
        Assert.Contains(42UL, CompoundPlanner.CanonicalSeeds);
    }

    [Fact]
    public void MetadataRequiresTwentyFourSpawnSlotsAndStableIds()
    {
        var package = MapPackageBuilder.Build("depot-yard", "gameplay", toolchain);
        var meta = MapJsonValue.Parse(package.MetadataBytes).Object;
        Assert.True(meta["spawnSlots"].Array.Count >= 30);
        Assert.True(meta["colliders"].Array.Count >= 1);
        Assert.Equal(package.CollisionHash.ToString(), meta["collisionArtifactSha256"].String);
        foreach (var row in meta["colliders"].Array)
        {
            Assert.NotEqual("0", row.Object["colliderStableId"].String);
            Assert.NotEqual("0", row.Object["surfaceId"].String);
        }
    }

    [Fact]
    public void PublishWritesReleaseCatalogAndLoadIsDataOnly()
    {
        Assert.Equal(ErrorCode.None, MapPackagePublisher.Publish(root, toolchain, new[] { "training-compound", "depot-yard" }, new[] { "gameplay", "low", "balanced", "high" }, false, CancellationToken.None).Error);
        string catalogPath = ReleaseCatalogDocument.CatalogPath(root);
        Assert.True(File.Exists(catalogPath));
        byte[] bytes = File.ReadAllBytes(catalogPath);
        Assert.True(ReleaseCatalogDocument.TryValidate(bytes, toolchain, out string validateDiagnostic));
        Assert.Equal("ok", validateDiagnostic);

        Assert.True(StaticMapCatalog.TryLoadPublished(root, toolchain, out var loaded, out string loadDiagnostic));
        Assert.Equal("ok", loadDiagnostic);
        Assert.Equal(2, loaded.Count);
        Assert.True(loaded.TryGet(WellKnownMaps.TrainingCompound, out var compound));
        Assert.True(loaded.TryGetGameplay(WellKnownMaps.TrainingCompound, out var gameplay));
        Assert.Equal(compound.GameplayRootHash, loaded.GameplayRootHashes[WellKnownMaps.TrainingCompound.Value]);
        Assert.True(gameplay.SpawnSlots.Count >= 24);

        // Truncate catalog JSON; retain corrupt bytes for repair (no silent substitute).
        byte[] corrupt = new byte[] { (byte)'{', (byte)'"' };
        File.WriteAllBytes(catalogPath, corrupt);
        Assert.False(StaticMapCatalog.TryLoadPublished(root, toolchain, out _, out string corruptDiagnostic));
        Assert.False(string.IsNullOrEmpty(corruptDiagnostic));
        Assert.Equal(corrupt, File.ReadAllBytes(catalogPath));
    }

    [Fact]
    public void CompileSeedRoundTripsThroughPersistedCatalogBytes()
    {
        var compiled = StaticMapCatalog.CompileSeed(toolchain);
        byte[] bytes = ReleaseCatalogDocument.BuildBytes(toolchain);
        Assert.True(StaticMapCatalog.TryParse(bytes, toolchain, out var parsed, out string diagnostic));
        Assert.Equal("ok", diagnostic);
        Assert.Equal(compiled.Count, parsed.Count);
        Assert.True(compiled.TryGet(WellKnownMaps.DepotYard, out var a));
        Assert.True(parsed.TryGet(WellKnownMaps.DepotYard, out var b));
        Assert.Equal(a.GameplayRootHash, b.GameplayRootHash);
        Assert.Equal(a.HighVisualRootHash, b.HighVisualRootHash);
    }
}
