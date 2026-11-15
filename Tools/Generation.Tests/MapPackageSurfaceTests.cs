using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Xunit;

[Collection("NativeNavProvider")]
public sealed class MapPackageSurfaceTests : IDisposable
{
    [Fact]
    public void FirstPersonFinishTripletsHavePhysicalScaleSpatialChannelsAndDistinctRoughness()
    {
        Assert.Equal(.1f, FirstPersonSurfaceBaker.TileMeters);
        var package = MapPackageBuilder.Build("training-compound", "low", toolchain);
        var payloads = PackagedVisualPayloads.FromBuild(package);
        for (int family = 0; family < MapVisualClosure.FirstPersonSurfacePaths.Length; family++)
        {
            string path = MapVisualClosure.FirstPersonSurfacePaths[family];
            var albedo = MapVisualClosure.DecodeMapMaterial(payloads, path);
            var normal = MapVisualClosure.DecodeMapMaterial(payloads, MapVisualClosure.MaterialCompanionPath(path, "normal"));
            var mask = MapVisualClosure.DecodeMapMaterial(payloads, MapVisualClosure.MaterialCompanionPath(path, "mask"));
            Assert.Equal(128, albedo.Width);
            Assert.Equal(8, albedo.MipBytes.Count);
            Assert.Equal("rgba8-srgb", albedo.ChannelFormat);
            Assert.Equal("rgba8-linear", normal.ChannelFormat);
            Assert.Equal("rgba8-linear", mask.ChannelFormat);
            Assert.True(albedo.MipBytes[0].Distinct().Count() > 8, "Spatial albedo family " + family);
            // Shallow coated-metal relief may span only a few encoded normal values.
            // Require real variation in both tangent axes without demanding exaggerated bump depth.
            Assert.True(normal.MipBytes[0].Where((v, i) => i % 4 == 0).Distinct().Count() > 1);
            Assert.True(normal.MipBytes[0].Where((v, i) => i % 4 == 1).Distinct().Count() > 1);
            Assert.All(normal.MipBytes[0].Where((v, i) => i % 4 == 3), a => Assert.Equal(255, a));
            double roughness = mask.MipBytes[0].Where((v, i) => i % 4 == 2).Average(v => (double)v);
            if (family == 0) Assert.InRange(roughness, 115, 160);
            if (family == 1 || family == 2) Assert.InRange(roughness, 210, 245);
            if (family == 4) Assert.InRange(roughness, 65, 95);
            if (family == 5) Assert.InRange(roughness, 225, 245);
            if (family == 6) Assert.InRange(roughness, 150, 180);
            Assert.All(mask.MipBytes[0].Where((v, i) => i % 4 == 0), m => Assert.Equal(family == 0 ? 190 : family == 4 ? 240 : family == 6 ? 30 : 0, m));
        }
    }

    private readonly string root = Path.Combine(Path.GetTempPath(), "shooter-map-surface-" + Guid.NewGuid().ToString("N"));
    private readonly ContentId toolchain = ContentIdentity.Hash(Encoding.ASCII.GetBytes("map-surface-publication-tests"));

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    [Theory]
    [InlineData("training-compound")]
    [InlineData("depot-yard")]
    [InlineData("oil-depot")]
        public void MapSurfaceTripletsContainSpatialDataAndCompleteMips(string mapId)
    {
        var package = MapPackageBuilder.Build(mapId, "low", toolchain);
        var payloads = PackagedVisualPayloads.FromBuild(package);
        foreach (string path in MapVisualClosure.MapMaterialPaths)
        {
            var albedo = MapVisualClosure.DecodeMapMaterial(payloads, path);
            Assert.Equal("rgba8-srgb", albedo.ChannelFormat);
            foreach (string plane in new[] { "albedo", "normal", "mask" })
            {
                string planePath = plane == "albedo" ? path : MapVisualClosure.MaterialCompanionPath(path, plane);
                var data = MapVisualClosure.DecodeMapMaterial(payloads, planePath);
                Assert.Equal(128, data.Width);
                Assert.Equal(128, data.Height);
                Assert.Equal(8, data.MipBytes.Count);
                Assert.Equal(plane == "albedo" ? "rgba8-srgb" : "rgba8-linear", data.ChannelFormat);
                int size = 128;
                for (int mip = 0; mip < data.MipBytes.Count; mip++)
                {
                    Assert.Equal(size * size * 4, data.MipBytes[mip].Count);
                    size = Math.Max(1, size / 2);
                }
                var pixels = Enumerable.Range(0, 128 * 128).Select(i =>
                    (data.MipBytes[0][i * 4], data.MipBytes[0][i * 4 + 1], data.MipBytes[0][i * 4 + 2]));
                Assert.True(pixels.Distinct().Take(2).Count() > 1, planePath + " must not be a flat color placeholder.");
                Assert.All(Enumerable.Range(0, 128 * 128), i => Assert.Equal(255, data.MipBytes[0][i * 4 + 3]));
            }
        }
    }

    [Fact]
    public void VisualColdWarmForcedPublicationRetainsIdenticalCompanionsOnDisk()
    {
        var cold = MapPackagePublisher.Publish(root, toolchain, new[] { "training-compound" }, new[] { "low" }, false, CancellationToken.None);
        Assert.Equal(ErrorCode.None, cold.Error);
        Assert.False(cold.Reused);
        var package = MapPackageBuilder.Build("training-compound", "low", toolchain);
        string directory = MapPackagePublisher.PackageDirectory(root, package.PackageRootHash);
        var firstBytes = package.Dependencies.ToDictionary(d => d.Path, d => File.ReadAllBytes(Path.Combine(directory, d.Path)));
        var warm = MapPackagePublisher.Publish(root, toolchain, new[] { "training-compound" }, new[] { "low" }, false, CancellationToken.None);
        Assert.Equal(ErrorCode.None, warm.Error);
        Assert.True(warm.Reused);
        var forced = MapPackagePublisher.Publish(root, toolchain, new[] { "training-compound" }, new[] { "low" }, true, CancellationToken.None);
        Assert.Equal(ErrorCode.None, forced.Error);
        foreach (var entry in firstBytes) Assert.Equal(entry.Value, File.ReadAllBytes(Path.Combine(directory, entry.Key)));
        Assert.True(MapVisualClosure.TryLoadFromPackageDirectory(directory, package.PackageRootHash, toolchain,
            out var loaded, out string diagnostic), diagnostic);
        foreach (string path in MapVisualClosure.MapMaterialPaths)
            foreach (string plane in new[] { "normal", "mask" })
            {
                string companion = MapVisualClosure.MaterialCompanionPath(path, plane);
                Assert.True(loaded.Has(companion));
                Assert.Equal(firstBytes[companion], loaded.Bytes(companion));
                Assert.Equal(128, MapVisualClosure.DecodeMapMaterial(loaded, companion).Width);
            }
    }

    [Fact]
    public void MissingOrCorruptSurfaceCompanionFailsClosed()
    {
        var package = MapPackageBuilder.Build("depot-yard", "low", toolchain);
        string companion = MapVisualClosure.MaterialCompanionPath(MapVisualClosure.MapMaterialPaths[0], "normal");
        var incomplete = package.Dependencies.Where(d => d.Path != companion).ToArray();
        Assert.False(MapVisualClosure.TryExtract(incomplete, package.MapId, package.VisualProfile, toolchain, out _, out string missing));
        Assert.Contains(companion, missing);
        Assert.Equal(ErrorCode.None, MapPackagePublisher.Publish(root, toolchain, new[] { "depot-yard" }, new[] { "low" }, false, CancellationToken.None).Error);
        string directory = MapPackagePublisher.PackageDirectory(root, package.PackageRootHash);
        string fullPath = Path.Combine(directory, companion);
        byte[] corrupt = File.ReadAllBytes(fullPath);
        corrupt[corrupt.Length - 1] ^= 1;
        File.WriteAllBytes(fullPath, corrupt);
        Assert.False(MapVisualClosure.TryLoadFromPackageDirectory(directory, package.PackageRootHash, toolchain, out _, out string diagnostic));
        Assert.Contains("hash", diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(corrupt, File.ReadAllBytes(fullPath));
    }

    [Theory]
    [InlineData("training-compound")]
    [InlineData("depot-yard")]
    [InlineData("oil-depot")]
        public void NewSurfacePublicationPreservesGameplayBytesAcrossQualityTiers(string mapId)
    {
        var gameplay = MapPackageBuilder.Build(mapId, "gameplay", toolchain);
        var fingerprint = MapVisualClosure.MapFingerprint(mapId);
        byte[] expectedCollision = ArtifactCodec.Decode(gameplay.CollisionSgaf, ContentKind.Collision, fingerprint, toolchain);
        byte[] expectedNavigation = ArtifactCodec.Decode(gameplay.NavigationSgaf, ContentKind.Navigation, fingerprint, toolchain);
        foreach (string profile in new[] { "low", "balanced", "high" })
        {
            var visual = MapPackageBuilder.Build(mapId, profile, toolchain);
            Assert.Equal(expectedCollision, ArtifactCodec.Decode(visual.CollisionSgaf, ContentKind.Collision, fingerprint, toolchain));
            Assert.Equal(expectedNavigation, ArtifactCodec.Decode(visual.NavigationSgaf, ContentKind.Navigation, fingerprint, toolchain));
            Assert.Equal(gameplay.MetadataBytes, visual.MetadataBytes);
            var payloads = PackagedVisualPayloads.FromBuild(visual);
            var low = PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build(mapId, "low", toolchain));
            foreach (string path in MapVisualClosure.MapMaterialPaths)
            {
                Assert.Equal(low.Bytes(path), payloads.Bytes(path));
                Assert.Equal(low.Bytes(MapVisualClosure.MaterialCompanionPath(path, "normal")), payloads.Bytes(MapVisualClosure.MaterialCompanionPath(path, "normal")));
                Assert.Equal(low.Bytes(MapVisualClosure.MaterialCompanionPath(path, "mask")), payloads.Bytes(MapVisualClosure.MaterialCompanionPath(path, "mask")));
            }
        }
    }

    [Fact]
    public void MapRecipeVersionRejectsHistoricalGenerationInputs()
    {
        byte[] source = MapRecipe.CreateSource("training-compound");
        Assert.Equal("1.0.1", MapRecipe.Parse(source).Document.GeneratorVersion);
        string old = Encoding.UTF8.GetString(source).Replace("\"generatorVersion\":\"1.0.1\"", "\"generatorVersion\":\"1.0.0\"");
        Assert.Throws<FormatException>(() => MapRecipe.Parse(Encoding.UTF8.GetBytes(old)));
    }

    [Theory]
    [InlineData("training-compound")]
    [InlineData("depot-yard")]
    [InlineData("oil-depot")]
        public void PackagedSolidsMeshExposesMaterialRoleSubmeshes(string mapId)
    {
        var package = MapPackageBuilder.Build(mapId, "low", toolchain);
        var payloads = PackagedVisualPayloads.FromBuild(package);
        var mesh = MapVisualClosure.DecodeMapMesh(payloads, MapVisualClosure.MapSolidsMeshPath);
        Assert.True(mesh.Submeshes.Count >= 3);
        var slots = mesh.Submeshes.Select(s => s.MaterialSlot).OrderBy(s => s).ToArray();
        Assert.Contains((ushort)MapSolidsMaterialRole.StructureConcrete, slots);
        Assert.Contains((ushort)MapSolidsMaterialRole.MetalCover, slots);
        Assert.Contains((ushort)MapSolidsMaterialRole.Trim, slots);
        Assert.Contains((ushort)MapSolidsMaterialRole.Foundation, slots);
        if (mapId == "depot-yard")
        {
            Assert.Contains((ushort)MapSolidsMaterialRole.DepotBlue, slots);
            Assert.Contains((ushort)MapSolidsMaterialRole.OxidePaint, slots);
        }
        Assert.All(slots, slot => Assert.InRange(slot, (ushort)0, (ushort)MapSolidsMaterialRole.Foundation));
        Assert.InRange(mesh.Positions.Count, 1, MapSolidsMesh.MaxVisualVertices);
        Assert.All(mesh.Uv, uv => Assert.True(float.IsFinite(uv.X) && float.IsFinite(uv.Y)));
    }
}
